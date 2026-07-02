using BillDrift.Application.Classification;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation;

/// <summary>
/// Classification-aware helpers for reconciliation engine stages.
/// </summary>
internal static class ClassificationReconciliationHelpers
{
    /// <summary>
    /// Returns true when the subscription truth line is classified as Internal.
    /// </summary>
    public static bool IsInternalSubscription(
        MicrosoftSubscriptionLine line,
        ClassificationContext? classifications)
    {
        if (classifications is null)
        {
            return false;
        }

        var itemRef = ReconciliationItemRefFactory.FromSubscriptionLine(line);
        return classifications.Get(itemRef)?.Classification == ReconciliationItemClassification.Internal;
    }

    /// <summary>
    /// Returns true when the subscription truth line is classified as CustomService.
    /// </summary>
    public static bool IsCustomServiceSubscription(
        MicrosoftSubscriptionLine line,
        ClassificationContext? classifications)
    {
        if (classifications is null)
        {
            return false;
        }

        var itemRef = ReconciliationItemRefFactory.FromSubscriptionLine(line);
        return classifications.Get(itemRef)?.Classification == ReconciliationItemClassification.CustomService;
    }

    /// <summary>
    /// Returns true when missing-billing checks should be suppressed for this subscription line.
    /// </summary>
    public static bool ShouldSuppressMissingInStripe(
        MicrosoftSubscriptionLine line,
        ClassificationContext? classifications) =>
        IsInternalSubscription(line, classifications) ||
        IsCustomServiceSubscription(line, classifications);

    /// <summary>
    /// Determines whether an item should be treated as non-CSP for reconciliation matching.
    /// </summary>
    public static bool IsNonCspForReconciliation(
        MicrosoftSubscriptionLine? subscriptionLine,
        ProductMapping? productMapping,
        ClassificationContext? classifications)
    {
        if (subscriptionLine is not null && classifications is not null)
        {
            var itemRef = ReconciliationItemRefFactory.FromSubscriptionLine(subscriptionLine);
            var classification = classifications.Get(itemRef);
            if (classification?.Classification == ReconciliationItemClassification.NonCspSupplier)
            {
                return true;
            }

            if (classification?.Classification == ReconciliationItemClassification.Internal)
            {
                return false;
            }
        }

        return productMapping?.Classification == ProductClassification.NonCsp;
    }

    /// <summary>
    /// Returns true when bill-impacting proposals should be blocked for this match group.
    /// </summary>
    public static bool ShouldBlockBillImpactingProposals(
        EntityMatchGroup group,
        ClassificationContext? classifications)
    {
        if (classifications is null)
        {
            return false;
        }

        if (group.SubscriptionLine is { } subscriptionLine)
        {
            var itemRef = ReconciliationItemRefFactory.FromSubscriptionLine(subscriptionLine);
            if (classifications.Get(itemRef)?.Classification == ReconciliationItemClassification.NonCspSupplier)
            {
                return true;
            }
        }

        if (group.SupplierCostLine is { } supplierLine)
        {
            var itemRef = ReconciliationItemRefFactory.FromSupplierCostLine(supplierLine);
            if (classifications.Get(itemRef)?.Classification == ReconciliationItemClassification.NonCspSupplier)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when fuzzy CSP auto-match should be blocked for a subscription line.
    /// </summary>
    public static bool ShouldBlockFuzzyCspMatch(
        MicrosoftSubscriptionLine line,
        ClassificationContext? classifications)
    {
        if (classifications is null)
        {
            return false;
        }

        var itemRef = ReconciliationItemRefFactory.FromSubscriptionLine(line);
        return classifications.Get(itemRef)?.Classification == ReconciliationItemClassification.NonCspSupplier;
    }
}
