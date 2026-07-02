using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing.Detection;

/// <summary>
/// Detects product mismatches where subscription truth and Stripe reference different commercial keys.
/// </summary>
public sealed class ProductMismatchDetector
{
    /// <summary>
    /// Finds match groups where truth and Stripe commercial keys differ with sufficient confidence.
    /// </summary>
    public IReadOnlyList<SurfacedException> Detect(SurfacingContext context)
    {
        var results = new List<SurfacedException>();

        foreach (var group in context.Run.MatchGroups)
        {
            if (group.SubscriptionLine is null || group.StripeItem is null)
            {
                continue;
            }

            if (group.Confidence < MatchConfidence.Medium)
            {
                continue;
            }

            var truthRoot = group.SubscriptionLine.CommercialKeyRoot;
            if (group.StripeItem.MappingMetadata.OfferId is null ||
                group.StripeItem.MappingMetadata.SkuId is null)
            {
                continue;
            }

            var stripeRoot = CommercialKeyRoot.Create(
                group.StripeItem.MappingMetadata.OfferId.Value,
                group.StripeItem.MappingMetadata.SkuId.Value);

            if (truthRoot.OfferId == stripeRoot.OfferId && truthRoot.SkuId == stripeRoot.SkuId)
            {
                continue;
            }

            results.Add(new SurfacedException(
                SurfacedExceptionId.FromDerived(
                    context.Run.Id,
                    "ProductMismatch",
                    group.Id.Value.ToString()),
                ExceptionCategory.ProductMismatch,
                ReconciliationDomain.TruthVsStripe,
                ExceptionSeverity.Error,
                group.Customer,
                group.CommercialKey is { } key
                    ? new ProductContext(key, $"{key.OfferId.Value}/{key.SkuId.Value}", key.OfferId, key.SkuId)
                    : null,
                "Subscription truth and Stripe reference different products.",
                [],
                RequiresActionNow: false,
                null,
                0,
                [],
                group.Id));
        }

        return results;
    }
}
