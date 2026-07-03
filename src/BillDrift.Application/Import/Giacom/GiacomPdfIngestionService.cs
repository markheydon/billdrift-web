using System.Security.Cryptography;
using BillDrift.Application.Ingestion;
using BillDrift.Application.Normalization;
using BillDrift.Domain.Billing;

namespace BillDrift.Application.Import.Giacom;

/// <summary>
/// Orchestrates Giacom billing PDF upload, parsing, normalization, and Azure persistence.
/// </summary>
public sealed class GiacomPdfIngestionService : IGiacomPdfIngestionService
{
    private readonly IGiacomBillingPdfIngester _ingester;
    private readonly IGiacomBillingNormalizer _normalizer;
    private readonly IIngestionBlobStore _blobStore;
    private readonly IIngestionRunIndexStore _indexStore;

    public GiacomPdfIngestionService(
        IGiacomBillingPdfIngester ingester,
        IGiacomBillingNormalizer normalizer,
        IIngestionBlobStore blobStore,
        IIngestionRunIndexStore indexStore)
    {
        _ingester = ingester;
        _normalizer = normalizer;
        _blobStore = blobStore;
        _indexStore = indexStore;
    }

    /// <inheritdoc />
    public async Task<GiacomPdfIngestionRun> IngestAndPersistAsync(
        Stream pdfContent,
        string? originalFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfContent);

        using var memory = new MemoryStream();
        await pdfContent.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();

        if (bytes.Length > GiacomPdfIngestionOptions.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"PDF file exceeds maximum allowed size of {GiacomPdfIngestionOptions.MaxFileSizeBytes} bytes.");
        }

        var ingestionId = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;
        var contentFingerprint = ComputeContentFingerprint(bytes);

        var sourceBlobPath = await _blobStore.UploadSourceAsync(
            ingestionId,
            bytes,
            originalFileName,
            cancellationToken);

        var inProgress = new GiacomPdfIngestionRun
        {
            IngestionId = ingestionId,
            OriginalFileName = originalFileName,
            ContentFingerprint = contentFingerprint,
            UploadedAt = uploadedAt,
            Status = IngestionRunStatus.InProgress,
            SourceBlobPath = sourceBlobPath
        };

        await _indexStore.CreateGiacomPdfInProgressAsync(inProgress, cancellationToken);

        try
        {
            using var parseStream = new MemoryStream(bytes);
            var result = _ingester.Ingest(parseStream, cancellationToken);

            if (result.Status == IngestionOutcomeStatus.Failure || result.Lines.Count == 0)
            {
                var failedRun = new GiacomPdfIngestionRun
                {
                    IngestionId = ingestionId,
                    OriginalFileName = originalFileName,
                    ContentFingerprint = contentFingerprint,
                    UploadedAt = uploadedAt,
                    CompletedAt = result.IngestedAt,
                    Status = IngestionRunStatus.Failed,
                    Summary = result.Summary,
                    SourceBlobPath = sourceBlobPath,
                    FailureReason = IngestionFailureReasonBuilder.Build(
                        result.Status,
                        result.LogEntries,
                        result.Lines.Count == 0
                            ? "No valid billing lines were extracted from the PDF."
                            : "PDF ingestion failed.")
                };

                await _indexStore.FailGiacomPdfAsync(failedRun, cancellationToken);
                return failedRun;
            }

            var supplierCostLines = new List<SupplierCostLine>();
            foreach (var line in result.Lines)
            {
                try
                {
                    supplierCostLines.Add(_normalizer.Normalize(line));
                }
                catch (NormalizationException)
                {
                    // Skip lines that fail normalization; counts reflected in summary.
                }
            }

            var manifestPath = await _blobStore.PersistSupplierCostLinesAsync(
                ingestionId,
                result,
                supplierCostLines,
                originalFileName,
                uploadedAt,
                cancellationToken);

            var completed = new GiacomPdfIngestionRun
            {
                IngestionId = ingestionId,
                OriginalFileName = originalFileName,
                ContentFingerprint = contentFingerprint,
                UploadedAt = uploadedAt,
                CompletedAt = result.IngestedAt,
                Status = MapStatus(result.Status),
                Summary = result.Summary,
                SourceBlobPath = sourceBlobPath,
                ResultManifestBlobPath = manifestPath
            };

            await _indexStore.CompleteGiacomPdfAsync(completed, cancellationToken);
            return completed;
        }
        catch (Exception ex)
        {
            var failed = new GiacomPdfIngestionRun
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

            await _indexStore.FailGiacomPdfAsync(failed, cancellationToken);
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
