using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.Detection;

/// <summary>
/// Rule dispatch for detecting typed mismatches per match group (contracts/mismatch-rules.md).
/// Mapping failures suppress later billing comparisons.
/// </summary>
public sealed class MismatchDetector
{
    private readonly ProposedChangeFactory _proposedChangeFactory;

    /// <summary>
    /// Creates a mismatch detector with proposed change factory.
    /// </summary>
    public MismatchDetector(ProposedChangeFactory proposedChangeFactory)
    {
        _proposedChangeFactory = proposedChangeFactory;
    }

    /// <summary>
    /// Detects subscription truth vs Stripe mismatches for a match group.
    /// </summary>
    public void DetectSubscriptionTruthMismatches(ReconciliationContext context, EntityMatchGroup group)
    {
        if (group.SubscriptionLine is null)
        {
            return;
        }

        if (HasMappingIssue(context, group))
        {
            return;
        }

        if (group.Confidence == MatchConfidence.Low || group.Confidence == MatchConfidence.None)
        {
            return;
        }

        var truth = group.SubscriptionLine;
        var key = group.CommercialKey;

        if (group.StripeItem is null)
        {
            var mismatch = CreateMismatch(
                context,
                MismatchType.MissingInStripe,
                MismatchSeverity.Error,
                group,
                truth.LicenceCount.ToString(),
                "Not billed in Stripe",
                $"Active subscription truth line has {truth.LicenceCount} licences but no Stripe billing item.");

            context.Mismatches.Add(mismatch);
            _proposedChangeFactory.TryCreateMissingItem(context, group, mismatch);
            return;
        }

        var stripe = group.StripeItem;

        if (stripe.Frequency != truth.Frequency && truth.Frequency != BillingFrequency.Unknown)
        {
            var mismatch = CreateMismatch(
                context,
                MismatchType.BillingFrequencyMismatch,
                MismatchSeverity.Error,
                group,
                truth.Frequency.ToString().ToLowerInvariant(),
                stripe.Frequency.ToString().ToLowerInvariant(),
                $"Billing frequency mismatch: expected {truth.Frequency}, actual {stripe.Frequency}.");

            context.Mismatches.Add(mismatch);
            _proposedChangeFactory.TryCreateSwitchPrice(context, group, mismatch);
            return;
        }

        if (truth.LicenceCount != stripe.Quantity)
        {
            var mismatch = CreateMismatch(
                context,
                MismatchType.QuantityMismatch,
                MismatchSeverity.Error,
                group,
                truth.LicenceCount.ToString(),
                stripe.Quantity.ToString(),
                $"Quantity mismatch: subscription truth has {truth.LicenceCount} licences, Stripe has {stripe.Quantity}.");

            context.Mismatches.Add(mismatch);
            _proposedChangeFactory.TryCreateUpdateQuantity(context, group, mismatch);
        }

        if (key.HasValue && group.IntendedPrice is not null)
        {
            var tolerance = context.Options.PriceTolerance;
            var intended = group.IntendedPrice.Rrp;
            var stripeAmount = stripe.UnitAmount;
            var diff = Math.Abs(stripeAmount.Amount - intended.Amount);

            if (diff > tolerance.Amount)
            {
                var mismatch = CreateMismatch(
                    context,
                    MismatchType.PriceMismatch,
                    MismatchSeverity.Error,
                    group,
                    $"{intended.Amount:F2} {intended.Currency.Value}",
                    $"{stripeAmount.Amount:F2} {stripeAmount.Currency.Value}",
                    $"Price mismatch: intended RRP {intended.Amount:F2}, Stripe unit amount {stripeAmount.Amount:F2}.");

                context.Mismatches.Add(mismatch);
                _proposedChangeFactory.TryCreateSwitchPrice(context, group, mismatch);
            }
        }
    }

    /// <summary>
    /// Detects supplier cost attachment issues for a match group.
    /// </summary>
    public void DetectSupplierCostMismatches(ReconciliationContext context, EntityMatchGroup group)
    {
        if (group.SupplierCostLine is null || group.SubscriptionLine is null || group.StripeItem is null)
        {
            return;
        }

        if (group.SupplierCostLine.ChargeType == ChargeType.ProRatedAdjustment)
        {
            return;
        }

        // Pro-rata lines are excluded from quantity totals (FR-016); recurring qty used for comparison.
    }

    /// <summary>
    /// Detects catalogue gaps for a match group.
    /// </summary>
    public void DetectCatalogueMismatches(ReconciliationContext context, EntityMatchGroup group)
    {
        if (!context.Options.ProposeCatalogueChanges)
        {
            return;
        }

        if (group.CommercialKey is not { } key)
        {
            return;
        }

        if (HasMappingIssue(context, group))
        {
            return;
        }

        if (!context.ProductMappingIndex.TryGetByRoot(
                CommercialKeyRoot.Create(key.OfferId, key.SkuId),
                out var mapping))
        {
            return;
        }

        if (mapping.Classification == ProductClassification.NonCsp &&
            !context.Options.IncludeNonCspProducts)
        {
            return;
        }

        var price = context.StripeCatalogueIndex.FindPriceForKey(key);
        if (price is not null)
        {
            return;
        }

        var mismatch = CreateMismatch(
            context,
            MismatchType.CatalogueMissing,
            MismatchSeverity.Warning,
            group,
            $"Stripe price for {key.OfferId.Value}/{key.SkuId.Value}",
            "Not found in catalogue",
            $"Catalogue missing: no Stripe price for commercial key {key.OfferId.Value}/{key.SkuId.Value}.");

        context.Mismatches.Add(mismatch);
        _proposedChangeFactory.TryCreateCatalogueEntry(context, group, mismatch);
    }

    private static bool HasMappingIssue(ReconciliationContext context, EntityMatchGroup group)
    {
        return context.Mismatches.Any(m =>
            m.Type is MismatchType.MappingMissing or MismatchType.MappingAmbiguous &&
            m.Customer?.MexId == group.Customer.MexId &&
            (m.CommercialKey is null || m.CommercialKey == group.CommercialKey));
    }

    private static Mismatch CreateMismatch(
        ReconciliationContext context,
        MismatchType type,
        MismatchSeverity severity,
        EntityMatchGroup group,
        string? expected,
        string? actual,
        string description)
    {
        return new Mismatch(
            context.NextMismatchId(),
            type,
            severity,
            group.Customer,
            group.CommercialKey,
            new MismatchEntityRefs(
                group.SupplierCostLine?.Id,
                group.SubscriptionLine?.Id,
                group.IntendedPrice?.Id,
                group.StripeItem?.Id),
            expected,
            actual,
            description);
    }
}
