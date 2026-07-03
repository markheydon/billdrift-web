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

    /// <summary>Inserts an in-progress retail pricing ingestion run.</summary>
    Task CreateRetailPricingInProgressAsync(RetailPricingIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Updates a retail pricing run to a terminal status.</summary>
    Task CompleteRetailPricingAsync(RetailPricingIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Marks a retail pricing run as failed.</summary>
    Task FailRetailPricingAsync(RetailPricingIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Loads a single retail pricing ingestion run by ID.</summary>
    Task<RetailPricingIngestionRun?> GetRetailPricingByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default);

    /// <summary>Lists recent retail pricing ingestion runs.</summary>
    Task<IReadOnlyList<RetailPricingIngestionRun>> ListRecentRetailPricingAsync(
        int take = 20,
        CancellationToken cancellationToken = default);
}
