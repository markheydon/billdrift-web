using System.Security.Cryptography;
using BillDrift.Application.Ingestion;

namespace BillDrift.Application.Import.RetailPricing;

/// <summary>
/// Orchestrates retail pricing CSV upload, parsing, resolution, and Azure persistence.
/// </summary>
public sealed class RetailPricingIngestionService : IRetailPricingIngestionService
{
    private readonly IResellerPricingCsvIngester _ingester;
    private readonly IIngestionBlobStore _blobStore;
    private readonly IIngestionRunIndexStore _indexStore;

    public RetailPricingIngestionService(
        IResellerPricingCsvIngester ingester,
        IIngestionBlobStore blobStore,
        IIngestionRunIndexStore indexStore)
    {
        _ingester = ingester;
        _blobStore = blobStore;
        _indexStore = indexStore;
    }

    /// <inheritdoc />
    public async Task<RetailPricingIngestionRun> IngestAndPersistAsync(
        Stream catalogueContent,
        string? originalFileName,
        IReadOnlyList<ManualPriceOverrideRequest>? manualOverrides = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogueContent);

        var bytes = await RetailPricingCsvContentReader.ReadBoundedAsync(
            catalogueContent,
            RetailPricingCsvIngestionOptions.DefaultMaxFileSizeBytes,
            cancellationToken);

        var ingestionId = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;
        var contentFingerprint = ComputeContentFingerprint(bytes);

        var sourceBlobPath = await _blobStore.UploadSourceAsync(
            ingestionId,
            bytes,
            originalFileName,
            cancellationToken);

        byte[]? manualOverridesJson = null;
        if (manualOverrides is { Count: > 0 })
        {
            manualOverridesJson = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(manualOverrides);
            await _blobStore.UploadManualOverridesAsync(ingestionId, manualOverridesJson, cancellationToken);
        }

        var inProgress = new RetailPricingIngestionRun
        {
            IngestionId = ingestionId,
            OriginalFileName = originalFileName,
            ContentFingerprint = contentFingerprint,
            UploadedAt = uploadedAt,
            Status = IngestionRunStatus.InProgress,
            SourceBlobPath = sourceBlobPath
        };

        await _indexStore.CreateRetailPricingInProgressAsync(inProgress, cancellationToken);

        try
        {
            using var parseStream = new MemoryStream(bytes);
            var result = _ingester.Ingest(
                new RetailPricingCsvIngestionRequest(parseStream, originalFileName, manualOverrides),
                cancellationToken) with
            { IngestionId = ingestionId };

            var manifestPath = await _blobStore.PersistRetailPricingResultAsync(
                ingestionId,
                result,
                originalFileName,
                uploadedAt,
                cancellationToken);

            var status = MapStatus(result.Status);
            var completed = new RetailPricingIngestionRun
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
                        "Retail pricing CSV ingestion failed.")
                    : null
            };

            await _indexStore.CompleteRetailPricingAsync(completed, cancellationToken);
            return completed;
        }
        catch (Exception ex)
        {
            var failed = new RetailPricingIngestionRun
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

            await _indexStore.FailRetailPricingAsync(failed, cancellationToken);
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
