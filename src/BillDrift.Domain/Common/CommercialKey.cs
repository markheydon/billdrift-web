namespace BillDrift.Domain.Common;

/// <summary>
/// Composite key aligning intended pricing with Stripe prices: OfferId + SkuId + Term + BillingFrequency.
/// Two entries with the same commercial key represent the same billable product at the same term and frequency.
/// </summary>
/// <param name="OfferId">Microsoft CSP offer identifier.</param>
/// <param name="SkuId">Microsoft CSP SKU identifier.</param>
/// <param name="Term">Contract term length.</param>
/// <param name="Frequency">How often the customer is billed.</param>
public readonly record struct CommercialKey(
    OfferId OfferId,
    SkuId SkuId,
    Term Term,
    BillingFrequency Frequency)
{
    /// <summary>
    /// Creates a <see cref="CommercialKey"/> from validated component identifiers.
    /// </summary>
    /// <param name="offerId">The offer identifier (required).</param>
    /// <param name="skuId">The SKU identifier (required).</param>
    /// <param name="term">The contract term.</param>
    /// <param name="frequency">The billing frequency.</param>
    /// <returns>A commercial key for price alignment.</returns>
    public static CommercialKey Create(OfferId offerId, SkuId skuId, Term term, BillingFrequency frequency) =>
        new(offerId, skuId, term, frequency);
}

/// <summary>
/// Product identity without term or frequency; used by <see cref="Mapping.ProductMapping"/> to link supplier names to Stripe products.
/// </summary>
/// <param name="OfferId">Microsoft CSP offer identifier.</param>
/// <param name="SkuId">Microsoft CSP SKU identifier.</param>
public readonly record struct CommercialKeyRoot(OfferId OfferId, SkuId SkuId)
{
    /// <summary>
    /// Creates a <see cref="CommercialKeyRoot"/> from validated offer and SKU identifiers.
    /// </summary>
    /// <param name="offerId">The offer identifier.</param>
    /// <param name="skuId">The SKU identifier.</param>
    /// <returns>A commercial key root for product mapping.</returns>
    public static CommercialKeyRoot Create(OfferId offerId, SkuId skuId) => new(offerId, skuId);
}

/// <summary>
/// Term and billing frequency pair used to select the correct Stripe price for a product mapping.
/// </summary>
/// <param name="Term">Contract term length.</param>
/// <param name="Frequency">Billing frequency.</param>
public readonly record struct PriceTermKey(Term Term, BillingFrequency Frequency);
