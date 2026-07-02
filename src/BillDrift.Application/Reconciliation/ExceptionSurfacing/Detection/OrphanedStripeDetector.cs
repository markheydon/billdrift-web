using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing.Detection;

/// <summary>
/// Detects orphaned Stripe billing items with no matching active subscription truth (research R8).
/// </summary>
public sealed class OrphanedStripeDetector
{
    /// <summary>
    /// Finds in-scope Stripe items not joined to subscription truth on the same MexId and commercial key root.
    /// </summary>
    public IReadOnlyList<SurfacedException> Detect(SurfacingContext context)
    {
        var results = new List<SurfacedException>();
        var coveredItems = new HashSet<StripeBillingItemId>();

        foreach (var group in context.Run.MatchGroups)
        {
            if (group.StripeItem is { } item && group.SubscriptionLine is not null)
            {
                coveredItems.Add(item.Id);
            }
        }

        foreach (var item in context.Run.Inputs.StripeItems)
        {
            if (coveredItems.Contains(item.Id))
            {
                continue;
            }

            if (item.MappingMetadata.OfferId is null || item.MappingMetadata.SkuId is null)
            {
                continue;
            }

            var hasMatchingTruth = context.Run.Inputs.SubscriptionLines.Any(sl =>
                sl.Customer.MexId == item.Customer.MexId &&
                sl.CommercialKeyRoot.OfferId == item.MappingMetadata.OfferId &&
                sl.CommercialKeyRoot.SkuId == item.MappingMetadata.SkuId &&
                (sl.Status == SubscriptionStatus.Active || context.Options.IncludeInactiveSubscriptions));

            if (hasMatchingTruth)
            {
                continue;
            }

            var key = CommercialKey.Create(
                item.MappingMetadata.OfferId.Value,
                item.MappingMetadata.SkuId.Value,
                Term.P1M,
                item.Frequency);

            results.Add(new SurfacedException(
                SurfacedExceptionId.FromDerived(
                    context.Run.Id,
                    "OrphanedStripe",
                    item.SubscriptionItemId.Value),
                ExceptionCategory.OrphanedBillingItem,
                ReconciliationDomain.TruthVsStripe,
                ExceptionSeverity.Error,
                item.Customer,
                new ProductContext(
                    key,
                    $"{key.OfferId.Value}/{key.SkuId.Value}",
                    key.OfferId,
                    key.SkuId),
                "Stripe is billing an item with no matching active subscription.",
                [],
                RequiresActionNow: false,
                null,
                0,
                [],
                null));
        }

        return results;
    }
}
