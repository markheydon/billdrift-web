using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing.Phases;

/// <summary>
/// Phase 2 — applies root-cause suppression rules SR-1 through SR-5
/// per contracts/suppression-and-ordering-rules.md.
/// </summary>
public sealed class SuppressPhase
{
    private static readonly HashSet<ExceptionCategory> MappingCategories =
    [
        ExceptionCategory.OfferSkuAmbiguousMapping,
        ExceptionCategory.NonCspManualReview
    ];

    private static readonly HashSet<ExceptionCategory> BillingDependentCategories =
    [
        ExceptionCategory.QuantityLicenceMismatch,
        ExceptionCategory.BillingFrequencyMismatch,
        ExceptionCategory.StripePriceRrpMismatch,
        ExceptionCategory.ProductMismatch,
        ExceptionCategory.MissingBillingItem
    ];

    private static readonly HashSet<ExceptionCategory> MexIdSuppressedCategories =
    [
        ExceptionCategory.MissingBillingItem,
        ExceptionCategory.OrphanedBillingItem,
        ExceptionCategory.QuantityLicenceMismatch,
        ExceptionCategory.BillingFrequencyMismatch,
        ExceptionCategory.StripePriceRrpMismatch,
        ExceptionCategory.ProductMismatch
    ];

    private static readonly HashSet<ExceptionCategory> CatalogueCategories =
    [
        ExceptionCategory.StripeProductMissing,
        ExceptionCategory.StripePriceMissing,
        ExceptionCategory.StripePriceRrpMismatch
    ];

    /// <summary>Filters and trims candidates per suppression rules.</summary>
    public void Execute(SurfacingContext context)
    {
        var candidates = context.Candidates;
        var toRemove = new HashSet<SurfacedExceptionId>();
        var trimmed = new List<SurfacedException>();

        var mappingGroups = FindGroupsWithMappingRootCause(candidates, context);
        var mexIdCustomers = candidates
            .Where(c => c.Category == ExceptionCategory.MexIdMismatch)
            .Select(c => c.Customer.MexId)
            .ToHashSet();

        foreach (var candidate in candidates)
        {
            if (ApplySr5InactiveOrphan(candidate, context, toRemove))
            {
                continue;
            }

            if (ApplySr1MappingSuppression(candidate, mappingGroups, toRemove, context))
            {
                continue;
            }

            if (ApplySr2MexIdSuppression(candidate, mexIdCustomers, toRemove, context))
            {
                continue;
            }

            if (ApplySr4CatalogueSubsumed(candidate, candidates, toRemove, context))
            {
                continue;
            }

            trimmed.Add(ApplySr3LowConfidence(candidate, context));
        }

        context.Candidates.Clear();
        context.Candidates.AddRange(trimmed.Where(c => !toRemove.Contains(c.Id)));
    }

    private static HashSet<MatchGroupId?> FindGroupsWithMappingRootCause(
        IReadOnlyList<SurfacedException> candidates,
        SurfacingContext context) =>
        candidates
            .Where(c => MappingCategories.Contains(c.Category) ||
                        HasEngineMappingMismatch(c, context))
            .Select(c => c.MatchGroupId)
            .ToHashSet();

    private static bool HasEngineMappingMismatch(SurfacedException candidate, SurfacingContext context) =>
        candidate.SourceMismatchIds.Any(id =>
            context.MismatchById.TryGetValue(id, out var mismatch) &&
            mismatch.Type is MismatchType.MappingMissing or MismatchType.MappingAmbiguous);

    private static bool ApplySr1MappingSuppression(
        SurfacedException candidate,
        HashSet<MatchGroupId?> mappingGroups,
        HashSet<SurfacedExceptionId> toRemove,
        SurfacingContext context)
    {
        if (!BillingDependentCategories.Contains(candidate.Category))
        {
            return false;
        }

        if (candidate.MatchGroupId is null || !mappingGroups.Contains(candidate.MatchGroupId))
        {
            return false;
        }

        toRemove.Add(candidate.Id);
        context.Suppressed.Add(new SuppressionRecord(
            candidate.SourceMismatchIds.FirstOrDefault(),
            SuppressionRule.RootCauseMapping,
            "Mapping root cause suppresses dependent billing exception."));
        return true;
    }

    private static bool ApplySr2MexIdSuppression(
        SurfacedException candidate,
        HashSet<MexId> mexIdCustomers,
        HashSet<SurfacedExceptionId> toRemove,
        SurfacingContext context)
    {
        if (!MexIdSuppressedCategories.Contains(candidate.Category))
        {
            return false;
        }

        if (!mexIdCustomers.Contains(candidate.Customer.MexId))
        {
            return false;
        }

        toRemove.Add(candidate.Id);
        context.Suppressed.Add(new SuppressionRecord(
            candidate.SourceMismatchIds.FirstOrDefault(),
            SuppressionRule.RootCauseMexId,
            "Mex ID mismatch suppresses dependent billing exception."));
        return true;
    }

    private static SurfacedException ApplySr3LowConfidence(
        SurfacedException candidate,
        SurfacingContext context)
    {
        if (candidate.MatchGroupId is null ||
            !context.MatchGroupById.TryGetValue(candidate.MatchGroupId.Value, out var group))
        {
            return candidate;
        }

        if (group.Confidence is not (MatchConfidence.Low or MatchConfidence.None))
        {
            return candidate;
        }

        if (MappingCategories.Contains(candidate.Category))
        {
            return candidate;
        }

        if (candidate.ProposedChangeId is null)
        {
            return candidate;
        }

        context.Suppressed.Add(new SuppressionRecord(
            candidate.SourceMismatchIds.FirstOrDefault(),
            SuppressionRule.LowConfidence,
            "Low confidence strips proposed corrective action reference."));

        return candidate with
        {
            ProposedChangeId = null,
            RequiresActionNow = false
        };
    }

    private static bool ApplySr4CatalogueSubsumed(
        SurfacedException candidate,
        IReadOnlyList<SurfacedException> all,
        HashSet<SurfacedExceptionId> toRemove,
        SurfacingContext context)
    {
        if (candidate.Domain != ReconciliationDomain.PricingVsCatalogue ||
            !CatalogueCategories.Contains(candidate.Category))
        {
            return false;
        }

        if (candidate.Product?.CommercialKey is not { } key)
        {
            return false;
        }

        var hasSubscriptionPrice = all.Any(c =>
            c.Domain == ReconciliationDomain.TruthVsStripe &&
            c.Category == ExceptionCategory.StripePriceRrpMismatch &&
            c.Product?.CommercialKey == key);

        if (!hasSubscriptionPrice)
        {
            return false;
        }

        toRemove.Add(candidate.Id);
        context.Suppressed.Add(new SuppressionRecord(
            candidate.SourceMismatchIds.FirstOrDefault(),
            SuppressionRule.CatalogueSubsumedBySubscription,
            "Subscription-level price exception subsumes catalogue exception."));
        return true;
    }

    private static bool ApplySr5InactiveOrphan(
        SurfacedException candidate,
        SurfacingContext context,
        HashSet<SurfacedExceptionId> toRemove)
    {
        if (candidate.Category != ExceptionCategory.OrphanedBillingItem)
        {
            return false;
        }

        if (context.Options.IncludeInactiveSubscriptions)
        {
            return false;
        }

        var itemId = candidate.Id.Value.Split(':').LastOrDefault();
        var item = context.Run.Inputs.StripeItems.FirstOrDefault(i =>
            i.SubscriptionItemId.Value == itemId);

        if (item is null)
        {
            return false;
        }

        // SR-5: suppress only when matching subscription truth exists but is inactive (canceled).
        var matchingTruth = context.Run.Inputs.SubscriptionLines
            .Where(sl =>
                sl.Customer.MexId == item.Customer.MexId &&
                sl.CommercialKeyRoot.OfferId == item.MappingMetadata.OfferId &&
                sl.CommercialKeyRoot.SkuId == item.MappingMetadata.SkuId)
            .ToList();

        if (matchingTruth.Count == 0)
        {
            return false;
        }

        if (matchingTruth.Any(sl => sl.Status == SubscriptionStatus.Active))
        {
            return false;
        }

        toRemove.Add(candidate.Id);
        context.Suppressed.Add(new SuppressionRecord(
            null,
            SuppressionRule.OutOfScopeInactive,
            "Inactive subscription orphan excluded from scope."));
        return true;
    }
}
