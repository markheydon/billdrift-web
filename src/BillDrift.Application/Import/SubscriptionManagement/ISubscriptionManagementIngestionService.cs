namespace BillDrift.Application.Import.SubscriptionManagement;

/// <summary>
/// Orchestrates Subscription Management CSV upload, parsing, and Azure persistence.
/// </summary>
public interface ISubscriptionManagementIngestionService
{
    /// <summary>
    /// Upload workflow: persist source CSV, run ingester, persist results, write table index.
    /// </summary>
    Task<Ingestion.SubscriptionManagementIngestionRun> IngestAndPersistAsync(
        Stream csvContent,
        string? originalFileName,
        CancellationToken cancellationToken = default);
}
