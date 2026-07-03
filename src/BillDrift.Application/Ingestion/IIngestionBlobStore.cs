using BillDrift.Application.Import;
using BillDrift.Domain.Billing;

namespace BillDrift.Application.Ingestion;

/// <summary>
/// Persists ingestion source files and result payloads in Azure Blob Storage.
/// </summary>
public interface IIngestionBlobStore
{
    /// <summary>Uploads the original CSV bytes for an ingestion run.</summary>
    Task<string> UploadSourceAsync(
        Guid ingestionId,
        byte[] content,
        string? originalFileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists parser result payloads and returns the manifest blob path.
    /// Writes result blobs first, then the manifest as the commit marker.
    /// </summary>
    Task<string> PersistResultAsync(
        Guid ingestionId,
        SubscriptionManagementCsvIngestionResult result,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the persisted ingestion result when the manifest is present.</summary>
    Task<SubscriptionManagementCsvIngestionResult?> GetIngestionResultAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default);

    /// <summary>Loads normalized subscription truth lines from blob storage.</summary>
    Task<IReadOnlyList<MicrosoftSubscriptionLine>?> GetSubscriptionTruthAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default);
}
