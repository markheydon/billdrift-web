using System.Security.Cryptography;
using BillDrift.Application.Ingestion;

namespace BillDrift.Application.Import.SubscriptionManagement;

/// <summary>
/// Orchestrates Subscription Management CSV upload, parsing, and Azure persistence.
/// </summary>
public sealed class SubscriptionManagementIngestionService : ISubscriptionManagementIngestionService
{
    private readonly ISubscriptionManagementCsvIngester _ingester;
    private readonly IIngestionBlobStore _blobStore;
    private readonly IIngestionRunIndexStore _indexStore;

    public SubscriptionManagementIngestionService(
        ISubscriptionManagementCsvIngester ingester,
        IIngestionBlobStore blobStore,
        IIngestionRunIndexStore indexStore)
    {
        _ingester = ingester;
        _blobStore = blobStore;
        _indexStore = indexStore;
    }

    /// <inheritdoc />
    public async Task<SubscriptionManagementIngestionRun> IngestAndPersistAsync(
        Stream csvContent,
        string? originalFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(csvContent);

        var bytes = await SubscriptionManagementCsvContentReader.ReadBoundedAsync(
            csvContent,
            SubscriptionManagementCsvIngestionOptions.DefaultMaxFileSizeBytes,
            cancellationToken);

        var ingestionId = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;

        var contentFingerprint = ComputeContentFingerprint(bytes);
        var sourceBlobPath = await _blobStore.UploadSourceAsync(
            ingestionId,
            bytes,
            originalFileName,
            cancellationToken);

        var inProgress = new SubscriptionManagementIngestionRun
        {
            IngestionId = ingestionId,
            OriginalFileName = originalFileName,
            ContentFingerprint = contentFingerprint,
            UploadedAt = uploadedAt,
            Status = IngestionRunStatus.InProgress,
            SourceBlobPath = sourceBlobPath
        };

        await _indexStore.CreateInProgressAsync(inProgress, cancellationToken);

        try
        {
            using var parseStream = new MemoryStream(bytes);
            var result = _ingester.Ingest(
                new SubscriptionManagementCsvIngestionRequest(parseStream, originalFileName),
                cancellationToken) with
            { IngestionId = ingestionId };

            var manifestPath = await _blobStore.PersistResultAsync(
                ingestionId,
                result,
                originalFileName,
                uploadedAt,
                cancellationToken);

            var status = MapStatus(result.Status);
            var completed = new SubscriptionManagementIngestionRun
            {
                IngestionId = ingestionId,
                OriginalFileName = originalFileName,
                ContentFingerprint = contentFingerprint,
                UploadedAt = uploadedAt,
                CompletedAt = result.IngestedAt,
                Status = status,
                Summary = result.Summary,
                SourceBlobPath = sourceBlobPath,
                ResultManifestBlobPath = manifestPath,
                FailureReason = status == IngestionRunStatus.Failed
                    ? IngestionFailureReasonBuilder.Build(
                        result.Status,
                        result.LogEntries,
                        "Subscription management CSV ingestion failed.")
                    : null
            };

            await _indexStore.CompleteAsync(completed, cancellationToken);
            return completed;
        }
        catch (Exception ex)
        {
            var failed = new SubscriptionManagementIngestionRun
            {
                IngestionId = ingestionId,
                OriginalFileName = originalFileName,
                ContentFingerprint = contentFingerprint,
                UploadedAt = uploadedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Status = IngestionRunStatus.Failed,
                SourceBlobPath = sourceBlobPath,
                FailureReason = ex.Message
            };

            await _indexStore.FailAsync(failed, cancellationToken);
            throw;
        }
    }

    private static IngestionRunStatus MapStatus(IngestionOutcomeStatus status) => status switch
    {
        IngestionOutcomeStatus.Success => IngestionRunStatus.Completed,
        IngestionOutcomeStatus.PartialSuccess => IngestionRunStatus.PartialSuccess,
        _ => IngestionRunStatus.Failed
    };

    private static string ComputeContentFingerprint(ReadOnlySpan<byte> content)
    {
        var hash = SHA256.HashData(content);
        return Convert.ToHexStringLower(hash);
    }
}
