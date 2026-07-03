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

    /// <summary>Inserts an in-progress Giacom PDF ingestion run.</summary>
    Task CreateGiacomPdfInProgressAsync(GiacomPdfIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Updates a Giacom PDF run to a terminal status.</summary>
    Task CompleteGiacomPdfAsync(GiacomPdfIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Marks a Giacom PDF run as failed.</summary>
    Task FailGiacomPdfAsync(GiacomPdfIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Loads a single Giacom PDF ingestion run by ID.</summary>
    Task<GiacomPdfIngestionRun?> GetGiacomPdfByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default);

    /// <summary>Lists recent Giacom PDF ingestion runs.</summary>
    Task<IReadOnlyList<GiacomPdfIngestionRun>> ListRecentGiacomPdfAsync(
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts an in-progress Stripe CSV ingestion run.</summary>
    Task CreateStripeCsvInProgressAsync(StripeCsvIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Updates a Stripe CSV run to a terminal status.</summary>
    Task CompleteStripeCsvAsync(StripeCsvIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Marks a Stripe CSV run as failed.</summary>
    Task FailStripeCsvAsync(StripeCsvIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>Loads a single Stripe CSV ingestion run by ID.</summary>
    Task<StripeCsvIngestionRun?> GetStripeCsvByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default);

    /// <summary>Lists recent Stripe CSV ingestion runs.</summary>
    Task<IReadOnlyList<StripeCsvIngestionRun>> ListRecentStripeCsvAsync(
        int take = 20,
        CancellationToken cancellationToken = default);
}
