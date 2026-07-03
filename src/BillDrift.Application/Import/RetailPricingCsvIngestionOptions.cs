namespace BillDrift.Application.Import;

/// <summary>Configuration for <see cref="IResellerPricingCsvIngester"/> intake and normalization.</summary>
public sealed record RetailPricingCsvIngestionOptions
{
    /// <summary>Default maximum upload size (10 MB).</summary>
    public const long DefaultMaxFileSizeBytes = 10_485_760;

    /// <summary>Maximum bytes accepted for the catalogue CSV.</summary>
    public long MaxFileSizeBytes { get; init; } = DefaultMaxFileSizeBytes;

    /// <summary>Maximum manual override entries per upload.</summary>
    public int MaxManualOverrides { get; init; } = 500;

    /// <summary>When false, returns raw rows only (test hook).</summary>
    public bool NormalizeOutput { get; init; } = true;

    /// <summary>Default billing currency when the export has no currency column.</summary>
    public string DefaultCurrency { get; init; } = "GBP";
}
