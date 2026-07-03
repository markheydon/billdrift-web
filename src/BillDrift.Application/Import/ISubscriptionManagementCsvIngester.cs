namespace BillDrift.Application.Import;

/// <summary>
/// Parses Giacom Subscription Management CSV exports into raw rows and normalized subscription truth lines.
/// </summary>
public interface ISubscriptionManagementCsvIngester
{
    /// <summary>
    /// Parses <c>SubscriptionManagementReport.csv</c> into raw rows and normalized subscription truth lines.
    /// Never throws for parse failures — inspect <see cref="SubscriptionManagementCsvIngestionResult.Status"/>.
    /// </summary>
    SubscriptionManagementCsvIngestionResult Ingest(
        SubscriptionManagementCsvIngestionRequest request,
        CancellationToken cancellationToken = default);
}
