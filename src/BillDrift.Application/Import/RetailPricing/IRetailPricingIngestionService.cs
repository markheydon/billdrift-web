using BillDrift.Application.Ingestion;

namespace BillDrift.Application.Import.RetailPricing;

/// <summary>Orchestrates retail pricing CSV upload, parsing, and Azure persistence.</summary>
public interface IRetailPricingIngestionService
{
    /// <summary>Uploads, ingests, resolves pricing strategy, and persists results.</summary>
    Task<RetailPricingIngestionRun> IngestAndPersistAsync(
        Stream catalogueContent,
        string? originalFileName,
        IReadOnlyList<ManualPriceOverrideRequest>? manualOverrides = null,
        CancellationToken cancellationToken = default);
}
