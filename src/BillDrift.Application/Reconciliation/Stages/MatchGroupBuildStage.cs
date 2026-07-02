using BillDrift.Application.Classification;
using BillDrift.Application.Reconciliation.Matching;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.Stages;

/// <summary>
/// Builds initial match groups driven by subscription truth lines (research R5).
/// </summary>
public sealed class MatchGroupBuildStage : IReconciliationStage
{
    private readonly CommercialKeyResolver _keyResolver;
    private readonly StripeItemMatcher _stripeMatcher;
    private readonly CustomerMatcher _customerMatcher;

    /// <summary>
    /// Creates a match group build stage with matching dependencies.
    /// </summary>
    public MatchGroupBuildStage(
        CommercialKeyResolver keyResolver,
        StripeItemMatcher stripeMatcher,
        CustomerMatcher customerMatcher)
    {
        _keyResolver = keyResolver;
        _stripeMatcher = stripeMatcher;
        _customerMatcher = customerMatcher;
    }

    /// <inheritdoc />
    public void Execute(ReconciliationContext context)
    {
        var inputs = context.Request.Inputs;
        var scope = context.Request.Scope;
        var options = context.Options;
        var groupIndex = new Dictionary<string, EntityMatchGroup>();
        var groupCounter = 0;

        var subscriptionLines = inputs.SubscriptionLines ?? [];
        foreach (var line in subscriptionLines)
        {
            if (!ShouldIncludeSubscription(line, options))
            {
                continue;
            }

            var resolution = _keyResolver.Resolve(line);
            var commercialKey = resolution.CommercialKey
                ?? CommercialKey.Create(
                    line.CommercialKeyRoot.OfferId,
                    line.CommercialKeyRoot.SkuId,
                    line.Term,
                    line.Frequency);

            var groupKey = $"{line.Customer.MexId.Value}:{commercialKey.OfferId.Value}:{commercialKey.SkuId.Value}:{(int)commercialKey.Term}:{(int)commercialKey.Frequency}";

            if (groupIndex.TryGetValue(groupKey, out var existing))
            {
                // Duplicate truth lines: use max licence count (conservative for revenue protection).
                var maxLicences = Math.Max(existing.SubscriptionLine?.LicenceCount ?? 0, line.LicenceCount);
                if (existing.SubscriptionLine?.LicenceCount != line.LicenceCount)
                {
                    var updatedLine = line with { LicenceCount = maxLicences };
                    groupIndex[groupKey] = existing with { SubscriptionLine = updatedLine };
                }

                continue;
            }

            var hasMapping = context.ProductMappingIndex.TryGetByRoot(line.CommercialKeyRoot, out var productMapping);
            var mappingBlocksBilling = false;

            if (!hasMapping)
            {
                EmitSubscriptionMappingMissing(
                    context,
                    line,
                    commercialKey,
                    $"Cannot map subscription truth line to a known product. Source: {line.CommercialKeyRoot.OfferId.Value}/{line.CommercialKeyRoot.SkuId.Value}. No product mapping found.");
                mappingBlocksBilling = true;
            }
            else if (ClassificationReconciliationHelpers.IsNonCspForReconciliation(
                         line,
                         productMapping,
                         context.Classifications) &&
                     !options.IncludeNonCspProducts)
            {
                EmitSubscriptionNonCspMapping(context, line, commercialKey, productMapping.NormalizedProductName);
                mappingBlocksBilling = true;
            }

            var stripeMatch = _stripeMatcher.Match(
                context.StripeCatalogueIndex,
                line.Customer,
                commercialKey,
                options.IncludeInactiveSubscriptions);

            if (ClassificationReconciliationHelpers.ShouldBlockFuzzyCspMatch(line, context.Classifications))
            {
                stripeMatch = new StripeItemMatchResult(null, [], false);
            }

            StripeBillingItem? stripeItem = stripeMatch.IsAmbiguous ? null : stripeMatch.Item;
            var confidence = mappingBlocksBilling ? MatchConfidence.None : resolution.Confidence;

            if (stripeMatch.IsAmbiguous)
            {
                confidence = MatchConfidence.None;
            }
            else if (!mappingBlocksBilling && stripeItem is not null && confidence < MatchConfidence.High)
            {
                confidence = MatchConfidence.High;
            }

            context.IntendedPriceIndex.TryGet(commercialKey, out var intendedPrice);

            var group = new EntityMatchGroup(
                ReconciliationContext.CreateMatchGroupId(line.Customer, commercialKey, groupCounter++),
                line.Customer,
                commercialKey,
                null,
                line,
                intendedPrice,
                stripeItem,
                confidence);

            groupIndex[groupKey] = group;
            context.MatchGroups.Add(group);

            if (stripeMatch.IsAmbiguous)
            {
                EmitAmbiguousStripe(context, line.Customer, commercialKey, stripeMatch.Candidates);
            }
        }

        AttachOrphanSupplierLines(context, groupIndex, ref groupCounter);
    }

    private void AttachOrphanSupplierLines(
        ReconciliationContext context,
        Dictionary<string, EntityMatchGroup> groupIndex,
        ref int groupCounter)
    {
        var inputs = context.Request.Inputs;
        var supplierLines = inputs.SupplierCostLines ?? [];

        foreach (var line in supplierLines)
        {
            if (!_customerMatcher.HasValidMexId(line.Customer))
            {
                EmitMappingMissing(context, line.Customer, null, "Supplier line with unknown customer Mex ID", line.Id);
                continue;
            }

            var resolution = _keyResolver.Resolve(line);
            if (resolution.Confidence == MatchConfidence.None)
            {
                EmitMappingMissing(context, line.Customer, null,
                    $"Cannot map supplier cost line to a known product/customer. Source: {line.ProductName}. Resolution attempted: {resolution.ResolutionPath}.",
                    line.Id);
                continue;
            }

            if (resolution.Mapping?.Classification == ProductClassification.NonCsp &&
                !context.Options.IncludeNonCspProducts)
            {
                EmitNonCspMapping(context, line);
                continue;
            }

            if (context.Classifications is not null)
            {
                var supplierRef = ReconciliationItemRefFactory.FromSupplierCostLine(line);
                if (context.Classifications.Get(supplierRef)?.Classification ==
                    ReconciliationItemClassification.NonCspSupplier &&
                    !context.Options.IncludeNonCspProducts)
                {
                    EmitNonCspMapping(context, line);
                    continue;
                }
            }

            var attached = false;
            foreach (var group in context.MatchGroups.ToList())
            {
                if (group.Customer.MexId != line.Customer.MexId)
                {
                    continue;
                }

                if (group.CommercialKey is null || resolution.CommercialKeyRoot is null)
                {
                    continue;
                }

                if (group.CommercialKey.Value.OfferId != resolution.CommercialKeyRoot.Value.OfferId ||
                    group.CommercialKey.Value.SkuId != resolution.CommercialKeyRoot.Value.SkuId)
                {
                    continue;
                }

                if (group.SupplierCostLine is null && line.ChargeType == ChargeType.Recurring)
                {
                    var idx = context.MatchGroups.IndexOf(group);
                    context.MatchGroups[idx] = group with { SupplierCostLine = line };
                    attached = true;
                    break;
                }
            }

            if (!attached)
            {
                var group = new EntityMatchGroup(
                    ReconciliationContext.CreateMatchGroupId(line.Customer, resolution.CommercialKey, groupCounter++),
                    line.Customer,
                    resolution.CommercialKey,
                    line,
                    null,
                    null,
                    null,
                    resolution.Confidence);

                context.MatchGroups.Add(group);
            }
        }
    }

    private static bool ShouldIncludeSubscription(MicrosoftSubscriptionLine line, ReconciliationOptions options) =>
        line.Status == SubscriptionStatus.Active || options.IncludeInactiveSubscriptions;

    private static void EmitAmbiguousStripe(
        ReconciliationContext context,
        CustomerIdentity customer,
        CommercialKey key,
        IReadOnlyList<StripeBillingItem> candidates)
    {
        var ids = string.Join(", ", candidates.Select(c => c.SubscriptionItemId.Value));
        var mismatch = new Mismatch(
            context.NextMismatchId(),
            MismatchType.MappingAmbiguous,
            MismatchSeverity.Error,
            customer,
            key,
            new MismatchEntityRefs(StripeBillingItemId: candidates[0].Id),
            null,
            null,
            $"Ambiguous match for customer {customer.MexId.Value} product {key.OfferId.Value}/{key.SkuId.Value}. Candidates: {ids}.");

        context.Mismatches.Add(mismatch);
    }

    private static void EmitMappingMissing(
        ReconciliationContext context,
        CustomerIdentity? customer,
        CommercialKey? key,
        string description,
        SupplierCostLineId? supplierLineId)
    {
        context.Mismatches.Add(new Mismatch(
            context.NextMismatchId(),
            MismatchType.MappingMissing,
            MismatchSeverity.Error,
            customer,
            key,
            new MismatchEntityRefs(SupplierCostLineId: supplierLineId),
            null,
            null,
            description));
    }

    private static void EmitNonCspMapping(ReconciliationContext context, SupplierCostLine line)
    {
        context.Mismatches.Add(new Mismatch(
            context.NextMismatchId(),
            MismatchType.MappingMissing,
            MismatchSeverity.Warning,
            line.Customer,
            null,
            new MismatchEntityRefs(SupplierCostLineId: line.Id),
            null,
            null,
            $"Non-CSP line requires manual mapping: {line.ProductName}."));
    }

    private static void EmitSubscriptionMappingMissing(
        ReconciliationContext context,
        MicrosoftSubscriptionLine line,
        CommercialKey key,
        string description)
    {
        context.Mismatches.Add(new Mismatch(
            context.NextMismatchId(),
            MismatchType.MappingMissing,
            MismatchSeverity.Error,
            line.Customer,
            key,
            new MismatchEntityRefs(SubscriptionLineId: line.Id),
            null,
            null,
            description));
    }

    private static void EmitSubscriptionNonCspMapping(
        ReconciliationContext context,
        MicrosoftSubscriptionLine line,
        CommercialKey key,
        string productName)
    {
        context.Mismatches.Add(new Mismatch(
            context.NextMismatchId(),
            MismatchType.MappingMissing,
            MismatchSeverity.Warning,
            line.Customer,
            key,
            new MismatchEntityRefs(SubscriptionLineId: line.Id),
            null,
            null,
            $"Non-CSP line requires manual mapping: {productName}."));
    }
}
