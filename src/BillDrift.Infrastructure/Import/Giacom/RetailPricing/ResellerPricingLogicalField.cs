namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing;

/// <summary>Logical fields mapped from ResellerPricingVsRRP.csv column headers.</summary>
internal enum ResellerPricingLogicalField
{
    OfferId,
    SkuId,
    Term,
    Frequency,
    Wholesale,
    Rrp,
    Margin,
    MarginPercent,
    Status,
    Platform,
    Currency
}
