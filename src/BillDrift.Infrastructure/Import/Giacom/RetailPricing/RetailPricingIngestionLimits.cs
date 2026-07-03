namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing;

/// <summary>Intake limits for reseller price list CSV uploads.</summary>
internal static class RetailPricingIngestionLimits
{
    /// <summary>Default maximum file size (10 MB).</summary>
    public const long DefaultMaxFileSizeBytes = 10_485_760;
}
