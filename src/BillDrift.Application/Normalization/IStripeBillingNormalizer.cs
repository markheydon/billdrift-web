using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import.Stripe;

namespace BillDrift.Application.Normalization;

public interface IStripeBillingNormalizer
{
    IReadOnlyList<StripeBillingItem> Normalize(
        RawStripeCustomer customer,
        IReadOnlyList<RawStripeSubscription> subscriptions,
        IReadOnlyList<RawStripeSubscriptionItem> items,
        IReadOnlyList<RawStripeProduct> products,
        IReadOnlyList<RawStripePrice> prices);
}

public interface IIntendedPriceResolver
{
    IntendedPrice? Resolve(CommercialKey key, IReadOnlyList<IntendedPrice> prices);
}

public sealed class IntendedPriceResolver : IIntendedPriceResolver
{
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
