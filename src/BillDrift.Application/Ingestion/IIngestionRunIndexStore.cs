namespace BillDrift.Application.Ingestion;

/// <summary>
/// Indexes persisted ingestion runs in Azure Table Storage.
/// </summary>
public interface IIngestionRunIndexStore
{
    /// <summary>Inserts an in-progress ingestion run at upload start.</summary>
    Task CreateInProgressAsync(SubscriptionManagementIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Updates the run to a terminal status with summary and manifest path.</summary>
    Task CompleteAsync(SubscriptionManagementIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Marks the run as failed with a reason.</summary>
    Task FailAsync(SubscriptionManagementIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Loads a single ingestion run by ID.</summary>
    Task<SubscriptionManagementIngestionRun?> GetByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default);

    /// <summary>Lists recent ingestion runs, newest first.</summary>
    Task<IReadOnlyList<SubscriptionManagementIngestionRun>> ListRecentAsync(
        int take = 20,
        CancellationToken cancellationToken = default);
}
