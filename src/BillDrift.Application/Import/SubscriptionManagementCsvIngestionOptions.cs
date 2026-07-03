namespace BillDrift.Application.Import;

/// <summary>
/// Parser options for Giacom Subscription Management CSV ingestion.
/// </summary>
/// <param name="MaxFileSizeBytes">Maximum allowed CSV size in bytes.</param>
/// <param name="NormalizeOutput">When false, returns raw rows only (test hook).</param>
public sealed record SubscriptionManagementCsvIngestionOptions(
    long MaxFileSizeBytes = 10_485_760,
    bool NormalizeOutput = true)
{
    /// <summary>Default maximum CSV upload size (10 MiB).</summary>
    public const long DefaultMaxFileSizeBytes = 10_485_760;
}
