using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.Detection;

/// <summary>
/// Creates proposed corrective actions with guard rules and idempotency keys (contracts/mismatch-rules.md).
/// </summary>
public sealed class ProposedChangeFactory
{
    /// <summary>
    /// Attempts to create a CreateMissingItem proposal when guards pass.
    /// </summary>
    public void TryCreateMissingItem(ReconciliationContext context, EntityMatchGroup group, Mismatch mismatch)
    {
        if (!CanProposeBillImpacting(context, group))
        {
            return;
        }

        if (group.CommercialKey is not { } key || group.SubscriptionLine is null)
        {
            return;
        }

        StripePriceId? priceId = null;
        if (context.ProductMappingIndex.TryGetByRoot(
                CommercialKeyRoot.Create(key.OfferId, key.SkuId),
                out var mapping))
        {
            var termKey = new PriceTermKey(key.Term, key.Frequency);
            if (mapping.StripePricesByTerm.TryGetValue(termKey, out var pid))
            {
                priceId = pid;
            }
        }

        if (priceId is null)
        {
            priceId = context.StripeCatalogueIndex.FindPriceForKey(key)?.PriceId;
        }

        var values = new Dictionary<string, string>
        {
            ["quantity"] = group.SubscriptionLine.LicenceCount.ToString(),
            ["commercialKey"] = $"{key.OfferId.Value}/{key.SkuId.Value}"
        };

        if (priceId is not null)
        {
            values["priceId"] = priceId.Value.Value;
        }

        AddProposedChange(
            context,
            mismatch,
            ProposedActionType.CreateMissingItem,
            new ProposedChangeTarget(
                group.Customer.StripeCustomerId,
                null,
                null,
                priceId),
            values,
            null,
            20);
    }

    /// <summary>
    /// Attempts to create an UpdateQuantity proposal when guards pass.
    /// </summary>
    public void TryCreateUpdateQuantity(ReconciliationContext context, EntityMatchGroup group, Mismatch mismatch)
    {
        if (!CanProposeBillImpacting(context, group) || group.StripeItem is null || group.SubscriptionLine is null)
        {
            return;
        }

        AddProposedChange(
            context,
            mismatch,
            ProposedActionType.UpdateQuantity,
            new ProposedChangeTarget(
                group.Customer.StripeCustomerId,
                group.StripeItem.SubscriptionId,
                group.StripeItem.SubscriptionItemId,
                group.StripeItem.PriceId),
            new Dictionary<string, string>
            {
                ["proposedQuantity"] = group.SubscriptionLine.LicenceCount.ToString()
            },
            null,
            40);
    }

    /// <summary>
    /// Attempts to create a SwitchPrice proposal when an alternate price exists.
    /// </summary>
    public void TryCreateSwitchPrice(ReconciliationContext context, EntityMatchGroup group, Mismatch mismatch)
    {
        if (!CanProposeBillImpacting(context, group) || group.StripeItem is null || group.CommercialKey is not { } key)
        {
            return;
        }

        var alternatePrice = context.StripeCatalogueIndex.FindPriceForKey(key);
        if (alternatePrice is null || alternatePrice.PriceId == group.StripeItem.PriceId)
        {
            return;
        }

        AddProposedChange(
            context,
            mismatch,
            ProposedActionType.SwitchPrice,
            new ProposedChangeTarget(
                group.Customer.StripeCustomerId,
                group.StripeItem.SubscriptionId,
                group.StripeItem.SubscriptionItemId,
                alternatePrice.PriceId),
            new Dictionary<string, string>
            {
                ["proposedPriceId"] = alternatePrice.PriceId.Value
            },
            null,
            30);
    }

    /// <summary>
    /// Attempts to create a CreateOrUpdateCatalogueEntry proposal when enabled.
    /// </summary>
    public void TryCreateCatalogueEntry(ReconciliationContext context, EntityMatchGroup group, Mismatch mismatch)
    {
        if (!context.Options.ProposeCatalogueChanges || group.CommercialKey is not { } key)
        {
            return;
        }

        ProductMapping? mapping = null;
        context.ProductMappingIndex.TryGetByRoot(
            CommercialKeyRoot.Create(key.OfferId, key.SkuId),
            out mapping);

        var root = CommercialKeyRoot.Create(key.OfferId, key.SkuId);
        var payload = new CatalogueEntryPayload(
            mapping?.StripeProductId,
            mapping?.NormalizedProductName ?? $"{key.OfferId.Value}/{key.SkuId.Value}",
            root,
            [new PriceTermKey(key.Term, key.Frequency)]);

        AddProposedChange(
            context,
            mismatch,
            ProposedActionType.CreateOrUpdateCatalogueEntry,
            new ProposedChangeTarget(),
            new Dictionary<string, string>(),
            payload,
            10);
    }

    private static bool CanProposeBillImpacting(ReconciliationContext context, EntityMatchGroup group)
    {
        if (ClassificationReconciliationHelpers.ShouldBlockBillImpactingProposals(group, context.Classifications))
        {
            return false;
        }

        if (group.Confidence == MatchConfidence.Low || group.Confidence == MatchConfidence.None)
        {
            return false;
        }

        if (group.CommercialKey is { } commercialKey)
        {
            if (!context.ProductMappingIndex.TryGetByRoot(
                    CommercialKeyRoot.Create(commercialKey.OfferId, commercialKey.SkuId),
                    out var mapping))
            {
                return false;
            }

            if (mapping.Classification == ProductClassification.NonCsp &&
                !context.Options.IncludeNonCspProducts)
            {
                return false;
            }
        }

        return true;
    }

    private static void AddProposedChange(
        ReconciliationContext context,
        Mismatch mismatch,
        ProposedActionType actionType,
        ProposedChangeTarget target,
        IReadOnlyDictionary<string, string> values,
        CatalogueEntryPayload? cataloguePayload,
        int executionOrder)
    {
        var idempotencyKey = IdempotencyKey.Create(context.RunId, mismatch.Id, actionType);
        context.ProposedChanges.Add(new ProposedChange(
            context.NextProposedChangeId(mismatch.Id),
            idempotencyKey,
            mismatch.Id,
            actionType,
            target,
            values,
            cataloguePayload,
            executionOrder));
    }
}
