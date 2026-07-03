using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

/// <summary>
/// Intended customer pricing for a <see cref="CommercialKey"/>, sourced from the price list catalogue or a manual override.
/// When duplicate keys exist, <see cref="PriceSource.ManualOverride"/> beats <see cref="PriceSource.Catalogue"/>.
/// </summary>
/// <param name="Id">Domain-generated identifier assigned during normalization.</param>
/// <param name="Key">Commercial key (OfferId + SkuId + Term + BillingFrequency) for price alignment with Stripe.</param>
/// <param name="Wholesale">Wholesale cost from the price list or manual entry.</param>
/// <param name="Rrp">Recommended retail price charged to the customer.</param>
/// <param name="Margin">Absolute margin amount, when provided by the source.</param>
/// <param name="MarginPercent">Margin as a percentage (0–100), when provided by the source.</param>
/// <param name="Status">Catalogue availability status (active, end of sale, etc.).</param>
/// <param name="Source">Whether this price came from the catalogue or a manual override.</param>
/// <param name="SourceReference">Traceability link back to the raw price list or manual entry import.</param>
/// <param name="Platform">Commerce platform (NCE/Legacy) when known from the price list.</param>
/// <param name="Classification">CSP catalogue item or non-CSP bespoke override.</param>
public sealed record IntendedPrice(
    IntendedPriceId Id,
    CommercialKey Key,
    Money Wholesale,
    Money Rrp,
    Money? Margin,
    decimal? MarginPercent,
    PriceListStatus Status,
    PriceSource Source,
    SourceReference SourceReference,
    PricingPlatform Platform = PricingPlatform.Unknown,
    ProductClassification Classification = ProductClassification.Csp);
