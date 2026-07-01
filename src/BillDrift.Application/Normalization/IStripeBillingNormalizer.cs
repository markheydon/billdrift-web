using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import.Stripe;

namespace BillDrift.Application.Normalization;

/// <summary>
/// Flattens raw Stripe export objects into normalized <see cref="StripeBillingItem"/> records for reconciliation against supplier costs.
/// </summary>
public interface IStripeBillingNormalizer
{
    /// <summary>
    /// Joins Stripe customer, subscription, item, product, and price data into one billing item per subscription item.
    /// </summary>
    /// <param name="customer">Stripe customer record containing MEX ID metadata.</param>
    /// <param name="subscriptions">Subscription records for the customer.</param>
    /// <param name="items">Subscription line items to flatten.</param>
    /// <param name="products">Product catalogue for joining product metadata.</param>
    /// <param name="prices">Price catalogue for resolving billing frequency and unit amounts.</param>
    /// <returns>One <see cref="StripeBillingItem"/> per subscription item with resolved commercial keys.</returns>
    IReadOnlyList<StripeBillingItem> Normalize(
        RawStripeCustomer customer,
        IReadOnlyList<RawStripeSubscription> subscriptions,
        IReadOnlyList<RawStripeSubscriptionItem> items,
        IReadOnlyList<RawStripeProduct> products,
        IReadOnlyList<RawStripePrice> prices);
}

/// <summary>
/// Resolves the effective intended price for a <see cref="BillDrift.Domain.Common.CommercialKey"/> from a set of catalogue and override prices.
/// </summary>
public interface IIntendedPriceResolver
{
    /// <summary>
    /// Returns the effective intended price for a commercial key.
    /// <see cref="BillDrift.Domain.Common.PriceSource.ManualOverride"/> takes precedence over <see cref="BillDrift.Domain.Common.PriceSource.Catalogue"/>.
    /// </summary>
    /// <param name="key">Commercial key (offer, SKU, term, frequency) to match.</param>
    /// <param name="prices">Available intended prices from catalogue and manual overrides.</param>
    /// <returns>The winning <see cref="IntendedPrice"/>, or <c>null</c> when no price matches the key.</returns>
    IntendedPrice? Resolve(CommercialKey key, IReadOnlyList<IntendedPrice> prices);
}

/// <summary>
/// Default implementation of <see cref="IIntendedPriceResolver"/> that prefers manual overrides over catalogue prices.
/// </summary>
public sealed class IntendedPriceResolver : IIntendedPriceResolver
{
    /// <inheritdoc />
    public IntendedPrice? Resolve(CommercialKey key, IReadOnlyList<IntendedPrice> prices)
    {
        var matches = prices.Where(p =>
            p.Key.OfferId.Equals(key.OfferId) &&
            p.Key.SkuId.Equals(key.SkuId) &&
            p.Key.Term == key.Term &&
            p.Key.Frequency == key.Frequency).ToList();

        if (matches.Count == 0)
        {
            return null;
        }

        return matches.FirstOrDefault(p => p.Source == PriceSource.ManualOverride)
            ?? matches.FirstOrDefault(p => p.Source == PriceSource.Catalogue);
    }
}
