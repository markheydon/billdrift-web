using BillDrift.Application.Ingestion;

namespace BillDrift.Application.Import.Stripe;

/// <summary>Orchestrates Stripe CSV bundle upload, parsing, normalization, and persistence.</summary>
public interface IStripeCsvIngestionService
{
    /// <summary>Ingests Stripe CSV files and persists normalized billing items and catalogue snapshots.</summary>
    Task<StripeCsvIngestionRun> IngestAndPersistAsync(
        StripeCsvUploadFiles files,
        CancellationToken cancellationToken = default);
}

/// <summary>Uploaded Stripe CSV file bundle.</summary>
public sealed record StripeCsvUploadFiles(
    Stream Subscriptions,
    string? SubscriptionsFileName,
    Stream? Products = null,
    string? ProductsFileName = null,
    Stream? Prices = null,
    string? PricesFileName = null);
