using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using BillDrift.Application.Import;
using BillDrift.Application.Ingestion;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Ingestion;

/// <summary>Azure Blob Storage implementation for ingestion archive payloads.</summary>
public sealed class AzureBlobIngestionArchiveStore : IIngestionBlobStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(IngestionJsonSerializerContext.Default.Options);

    private readonly BlobContainerClient _containerClient;
    private bool _containerEnsured;

    /// <summary>Creates a store using an Aspire-injected blob service client.</summary>
    public AzureBlobIngestionArchiveStore(BlobServiceClient blobServiceClient, IOptions<IngestionStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(blobServiceClient);
        _containerClient = blobServiceClient.GetBlobContainerClient(options.Value.BlobContainerName);
    }

    /// <inheritdoc />
    public async Task<string> UploadSourceAsync(
        Guid ingestionId,
        byte[] content,
        string? originalFileName,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var path = GetSourcePath(ingestionId, originalFileName);
        var client = _containerClient.GetBlobClient(path);
        using var stream = new MemoryStream(content);
        await client.UploadAsync(stream, overwrite: true, cancellationToken);
        return path;
    }

    /// <inheritdoc />
    public async Task<string> PersistResultAsync(
        Guid ingestionId,
        SubscriptionManagementCsvIngestionResult result,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);

        var sourcePath = GetSourcePath(ingestionId, originalFileName);
        var rawRowsPath = $"{ingestionId:D}/result/raw-rows.json";
        var subscriptionTruthPath = $"{ingestionId:D}/result/subscription-truth.json";
        var manifestPath = $"{ingestionId:D}/result/manifest.json";

        var rawRowsDocument = new RawRowsBlobDocument(result.RawRows, result.LogEntries);
        var rawRowsJson = JsonSerializer.Serialize(rawRowsDocument, JsonOptions);
        var rawRowsHash = ComputeHash(rawRowsJson);
        await UploadJsonAsync(rawRowsPath, rawRowsJson, cancellationToken);

        var truthDocument = new SubscriptionTruthBlobDocument(
            result.SubscriptionLines,
            result.Summary.NormalizationSkipped);
        var truthJson = JsonSerializer.Serialize(truthDocument, JsonOptions);
        var truthHash = ComputeHash(truthJson);
        await UploadJsonAsync(subscriptionTruthPath, truthJson, cancellationToken);

        var manifest = new IngestionManifestDocument(
            ingestionId,
            ImportSourceKind.GiacomSubscriptionManagement,
            originalFileName,
            result.SourceDocumentId,
            uploadedAt,
            result.IngestedAt,
            MapStatus(result.Status),
            result.Summary,
            new IngestionManifestBlobs(sourcePath, rawRowsPath, subscriptionTruthPath),
            new IngestionManifestContentHashes(rawRowsHash, truthHash));

        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        await UploadJsonAsync(manifestPath, manifestJson, cancellationToken);

        var manifestClient = _containerClient.GetBlobClient(manifestPath);
        await manifestClient.SetMetadataAsync(new Dictionary<string, string>
        {
            ["ingestionid"] = ingestionId.ToString("D"),
            ["sourcekind"] = ImportSourceKind.GiacomSubscriptionManagement.ToString(),
            ["status"] = MapStatus(result.Status)
        }, cancellationToken: cancellationToken);

        return manifestPath;
    }

    /// <inheritdoc />
    public async Task<SubscriptionManagementCsvIngestionResult?> GetIngestionResultAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var manifestPath = $"{ingestionId:D}/result/manifest.json";
        var manifest = await TryLoadManifestAsync(manifestPath, cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var rawRowsPath = manifest.Blobs.RawRows;
        var rawRowsClient = _containerClient.GetBlobClient(rawRowsPath);
        var rawRowsJson = await rawRowsClient.DownloadContentAsync(cancellationToken);
        var rawRowsDocument = JsonSerializer.Deserialize<RawRowsBlobDocument>(rawRowsJson.Value.Content.ToString(), JsonOptions);

        var truthPath = manifest.Blobs.SubscriptionTruth;
        var truthClient = _containerClient.GetBlobClient(truthPath);
        var truthJson = await truthClient.DownloadContentAsync(cancellationToken);
        var truthDocument = JsonSerializer.Deserialize<SubscriptionTruthBlobDocument>(truthJson.Value.Content.ToString(), JsonOptions);

        if (rawRowsDocument is null || truthDocument is null)
        {
            return null;
        }

        return new SubscriptionManagementCsvIngestionResult
        {
            IngestionId = ingestionId,
            SourceDocumentId = manifest.ContentFingerprint,
            IngestedAt = manifest.CompletedAt,
            Status = MapRunStatus(manifest.Status),
            RawRows = rawRowsDocument.Records,
            SubscriptionLines = truthDocument.Records,
            LogEntries = rawRowsDocument.LogEntries,
            Summary = manifest.Summary,
            SourceFile = new SubscriptionManagementSourceFileInfo(
                manifest.ContentFingerprint,
                manifest.OriginalFileName,
                manifest.Summary.RowsRead)
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MicrosoftSubscriptionLine>?> GetSubscriptionTruthAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var path = $"{ingestionId:D}/result/subscription-truth.json";
        var client = _containerClient.GetBlobClient(path);

        try
        {
            var content = await client.DownloadContentAsync(cancellationToken);
            var document = JsonSerializer.Deserialize<SubscriptionTruthBlobDocument>(content.Value.Content.ToString(), JsonOptions);
            return document?.Records;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerEnsured)
        {
            return;
        }

        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _containerEnsured = true;
    }

    private async Task UploadJsonAsync(string path, string json, CancellationToken cancellationToken)
    {
        var client = _containerClient.GetBlobClient(path);
        await client.UploadAsync(BinaryData.FromString(json), overwrite: true, cancellationToken);
    }

    private async Task<IngestionManifestDocument?> TryLoadManifestAsync(string path, CancellationToken cancellationToken)
    {
        var client = _containerClient.GetBlobClient(path);
        try
        {
            var content = await client.DownloadContentAsync(cancellationToken);
            return JsonSerializer.Deserialize<IngestionManifestDocument>(content.Value.Content.ToString(), JsonOptions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static string GetSourcePath(Guid ingestionId, string? originalFileName)
    {
        var fileName = string.IsNullOrWhiteSpace(originalFileName)
            ? "SubscriptionManagementReport.csv"
            : Path.GetFileName(originalFileName);
        return $"{ingestionId:D}/source/{fileName}";
    }

    private static string ComputeHash(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return $"sha256:{Convert.ToHexStringLower(hash)}";
    }

    private static string MapStatus(IngestionOutcomeStatus status) => status switch
    {
        IngestionOutcomeStatus.Success => IngestionRunStatus.Completed.ToString(),
        IngestionOutcomeStatus.PartialSuccess => IngestionRunStatus.PartialSuccess.ToString(),
        _ => IngestionRunStatus.Failed.ToString()
    };

    private static IngestionOutcomeStatus MapRunStatus(string status) =>
        Enum.TryParse<IngestionRunStatus>(status, out var parsed) switch
        {
            true when parsed == IngestionRunStatus.Completed => IngestionOutcomeStatus.Success,
            true when parsed == IngestionRunStatus.PartialSuccess => IngestionOutcomeStatus.PartialSuccess,
            _ => IngestionOutcomeStatus.Failure
        };
}
