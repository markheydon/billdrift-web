namespace BillDrift.Domain.Common;

public readonly record struct CommercialKey(
    OfferId OfferId,
    SkuId SkuId,
    Term Term,
    BillingFrequency Frequency)
{
    public static CommercialKey Create(OfferId offerId, SkuId skuId, Term term, BillingFrequency frequency) =>
        new(offerId, skuId, term, frequency);
}

public readonly record struct CommercialKeyRoot(OfferId OfferId, SkuId SkuId)
{
    public static CommercialKeyRoot Create(OfferId offerId, SkuId skuId) => new(offerId, skuId);
}

public readonly record struct PriceTermKey(Term Term, BillingFrequency Frequency);
