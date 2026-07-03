using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using BillDrift.Application.Import;
using BillDrift.Application.Ingestion;
using BillDrift.Domain.Billing;
using BillDrift.Domain.CatalogueReconciliation;
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

    /// <inheritdoc />
    public async Task<string?> UploadManualOverridesAsync(
        Guid ingestionId,
        byte[]? manualOverridesJson,
        CancellationToken cancellationToken = default)
    {
        if (manualOverridesJson is null || manualOverridesJson.Length == 0)
        {
            return null;
        }

        await EnsureContainerAsync(cancellationToken);
        var path = $"{ingestionId:D}/source/manual-overrides.json";
        var client = _containerClient.GetBlobClient(path);
        await client.UploadAsync(BinaryData.FromBytes(manualOverridesJson), overwrite: true, cancellationToken);
        return path;
    }

    /// <inheritdoc />
    public async Task<string> PersistRetailPricingResultAsync(
        Guid ingestionId,
        RetailPricingCsvIngestionResult result,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);

        // Must match the path written by UploadSourceAsync so the manifest references the blob that actually exists.
        var sourcePath = GetSourcePath(ingestionId, originalFileName);
        var rawCataloguePath = $"{ingestionId:D}/result/raw-catalogue-rows.json";
        var cataloguePricesPath = $"{ingestionId:D}/result/catalogue-prices.json";
        var manualPricesPath = $"{ingestionId:D}/result/manual-prices.json";
        var resolvedPricesPath = $"{ingestionId:D}/result/resolved-prices.json";
        var manifestPath = $"{ingestionId:D}/result/manifest.json";

        var rawDocument = new RawCatalogueRowsBlobDocument(result.RawCatalogueRows, result.LogEntries);
        var rawJson = JsonSerializer.Serialize(rawDocument, JsonOptions);
        var rawHash = ComputeHash(rawJson);
        await UploadJsonAsync(rawCataloguePath, rawJson, cancellationToken);

        var catalogueJson = JsonSerializer.Serialize(
            new CataloguePricesBlobDocument(result.CataloguePrices),
            JsonOptions);
        await UploadJsonAsync(cataloguePricesPath, catalogueJson, cancellationToken);

        var manualJson = JsonSerializer.Serialize(
            new ManualPricesBlobDocument(result.ManualPrices, result.RawManualEntries),
            JsonOptions);
        await UploadJsonAsync(manualPricesPath, manualJson, cancellationToken);

        var resolvedDocument = new ResolvedPricesBlobDocument(
            result.ResolvedPrices,
            result.ResolutionDetails,
            result.LogEntries);
        var resolvedJson = JsonSerializer.Serialize(resolvedDocument, JsonOptions);
        var resolvedHash = ComputeHash(resolvedJson);
        await UploadJsonAsync(resolvedPricesPath, resolvedJson, cancellationToken);

        var manifest = new RetailPricingManifestDocument(
            ingestionId,
            ImportSourceKind.GiacomPriceList,
            originalFileName,
            result.SourceDocumentId,
            uploadedAt,
            result.IngestedAt,
            MapStatus(result.Status),
            result.Summary,
            new RetailPricingManifestBlobs(
                sourcePath,
                null,
                rawCataloguePath,
                cataloguePricesPath,
                manualPricesPath,
                resolvedPricesPath),
            new RetailPricingManifestContentHashes(rawHash, resolvedHash));

        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        await UploadJsonAsync(manifestPath, manifestJson, cancellationToken);

        var manifestClient = _containerClient.GetBlobClient(manifestPath);
        await manifestClient.SetMetadataAsync(new Dictionary<string, string>
        {
            ["ingestionid"] = ingestionId.ToString("D"),
            ["sourcekind"] = ImportSourceKind.GiacomPriceList.ToString(),
            ["status"] = MapStatus(result.Status)
        }, cancellationToken: cancellationToken);

        return manifestPath;
    }

    /// <inheritdoc />
    public async Task<RetailPricingCsvIngestionResult?> GetRetailPricingResultAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var manifestPath = $"{ingestionId:D}/result/manifest.json";
        var manifest = await TryLoadRetailPricingManifestAsync(manifestPath, cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var resolvedClient = _containerClient.GetBlobClient(manifest.Blobs.ResolvedPrices);
        var resolvedJson = await resolvedClient.DownloadContentAsync(cancellationToken);
        var resolvedDocument = JsonSerializer.Deserialize<ResolvedPricesBlobDocument>(
            resolvedJson.Value.Content.ToString(),
            JsonOptions);

        var rawClient = _containerClient.GetBlobClient(manifest.Blobs.RawCatalogueRows);
        var rawJson = await rawClient.DownloadContentAsync(cancellationToken);
        var rawDocument = JsonSerializer.Deserialize<RawCatalogueRowsBlobDocument>(
            rawJson.Value.Content.ToString(),
            JsonOptions);

        if (resolvedDocument is null || rawDocument is null)
        {
            return null;
        }

        var catalogueDocument = await TryDownloadJsonAsync<CataloguePricesBlobDocument>(
            manifest.Blobs.CataloguePrices,
            cancellationToken);
        var manualDocument = await TryDownloadJsonAsync<ManualPricesBlobDocument>(
            manifest.Blobs.ManualPrices,
            cancellationToken);

        return new RetailPricingCsvIngestionResult
        {
            IngestionId = ingestionId,
            SourceDocumentId = manifest.ContentFingerprint,
            IngestedAt = manifest.CompletedAt,
            Status = MapRunStatus(manifest.Status),
            RawCatalogueRows = rawDocument.Records,
            RawManualEntries = manualDocument?.RawEntries ?? [],
            CataloguePrices = catalogueDocument?.Records ?? [],
            ManualPrices = manualDocument?.Records ?? [],
            ResolvedPrices = resolvedDocument.Records,
            ResolutionDetails = resolvedDocument.ResolutionDetails,
            LogEntries = resolvedDocument.LogEntries,
            Summary = manifest.Summary
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IntendedPrice>?> GetResolvedPricesAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var path = $"{ingestionId:D}/result/resolved-prices.json";
        var client = _containerClient.GetBlobClient(path);

        try
        {
            var content = await client.DownloadContentAsync(cancellationToken);
            var document = JsonSerializer.Deserialize<ResolvedPricesBlobDocument>(
                content.Value.Content.ToString(),
                JsonOptions);
            return document?.Records;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task PersistStripeCatalogueAsync(
        Guid ingestionId,
        IReadOnlyList<StripeCatalogueProduct> products,
        IReadOnlyList<StripeCataloguePrice> prices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(prices);

        await EnsureContainerAsync(cancellationToken);

        // Write both blobs unconditionally so a reader can distinguish "run archived with no products"
        // (empty array) from "no catalogue archived for this run" (blob absent → 404 → null).
        var productsJson = JsonSerializer.Serialize(new StripeCatalogueProductsBlobDocument(products), JsonOptions);
        await UploadJsonAsync($"{ingestionId:D}/result/stripe-catalogue-products.json", productsJson, cancellationToken);

        var pricesJson = JsonSerializer.Serialize(new StripeCataloguePricesBlobDocument(prices), JsonOptions);
        await UploadJsonAsync($"{ingestionId:D}/result/stripe-catalogue-prices.json", pricesJson, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StripeCatalogueProduct>?> GetStripeCatalogueProductsAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var path = $"{ingestionId:D}/result/stripe-catalogue-products.json";
        var document = await TryDownloadJsonAsync<StripeCatalogueProductsBlobDocument>(path, cancellationToken);
        return document?.Records;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StripeCataloguePrice>?> GetStripeCataloguePricesAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var path = $"{ingestionId:D}/result/stripe-catalogue-prices.json";
        var document = await TryDownloadJsonAsync<StripeCataloguePricesBlobDocument>(path, cancellationToken);
        return document?.Records;
    }

    /// <inheritdoc />
    public async Task<string> PersistSupplierCostLinesAsync(
        Guid ingestionId,
        GiacomPdfIngestionResult result,
        IReadOnlyList<SupplierCostLine> supplierCostLines,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);

        var sourcePath = GetSourcePath(ingestionId, originalFileName);
        var rawLinesPath = $"{ingestionId:D}/result/raw-lines.json";
        var supplierCostPath = $"{ingestionId:D}/result/supplier-cost.json";
        var manifestPath = $"{ingestionId:D}/result/manifest.json";

        var rawLinesJson = JsonSerializer.Serialize(result.Lines, JsonOptions);
        await UploadJsonAsync(rawLinesPath, rawLinesJson, cancellationToken);

        var normalizationSkipped = result.Summary.LinesExtracted - supplierCostLines.Count;
        var supplierDocument = new SupplierCostBlobDocument(supplierCostLines, normalizationSkipped);
        var supplierJson = JsonSerializer.Serialize(supplierDocument, JsonOptions);
        await UploadJsonAsync(supplierCostPath, supplierJson, cancellationToken);

        var manifest = new GiacomPdfManifestDocument(
            ingestionId,
            ImportSourceKind.GiacomBillingPdf,
            originalFileName,
            result.SourceDocumentId,
            uploadedAt,
            result.IngestedAt,
            MapStatus(result.Status),
            result.Summary,
            new GiacomPdfManifestBlobs(sourcePath, rawLinesPath, supplierCostPath));

        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        await UploadJsonAsync(manifestPath, manifestJson, cancellationToken);

        return manifestPath;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierCostLine>?> GetSupplierCostLinesAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var path = $"{ingestionId:D}/result/supplier-cost.json";
        var document = await TryDownloadJsonAsync<SupplierCostBlobDocument>(path, cancellationToken);
        return document?.Records;
    }

    /// <inheritdoc />
    public async Task<string> PersistStripeBillingItemsAsync(
        Guid ingestionId,
        StripeCsvIngestionResult result,
        IReadOnlyList<StripeBillingItem> billingItems,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);

        var sourcePath = GetSourcePath(ingestionId, originalFileName);
        var billingPath = $"{ingestionId:D}/result/stripe-billing.json";
        var manifestPath = $"{ingestionId:D}/result/manifest.json";

        var normalizationSkipped = Math.Max(0, result.SubscriptionItems.Count - billingItems.Count);
        var billingDocument = new StripeBillingItemsBlobDocument(billingItems, normalizationSkipped);
        var billingJson = JsonSerializer.Serialize(billingDocument, JsonOptions);
        await UploadJsonAsync(billingPath, billingJson, cancellationToken);

        var manifest = new StripeCsvManifestDocument(
            ingestionId,
            ImportSourceKind.StripeExport,
            originalFileName,
            result.BundleId,
            uploadedAt,
            result.IngestedAt,
            MapStatus(result.Status),
            result.Summary,
            new StripeCsvManifestBlobs(sourcePath, billingPath));

        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        await UploadJsonAsync(manifestPath, manifestJson, cancellationToken);

        return manifestPath;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StripeBillingItem>?> GetStripeBillingItemsAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var path = $"{ingestionId:D}/result/stripe-billing.json";
        var document = await TryDownloadJsonAsync<StripeBillingItemsBlobDocument>(path, cancellationToken);
        return document?.Records;
    }

    private async Task<T?> TryDownloadJsonAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        var client = _containerClient.GetBlobClient(path);
        try
        {
            var content = await client.DownloadContentAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(content.Value.Content.ToString(), JsonOptions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task<RetailPricingManifestDocument?> TryLoadRetailPricingManifestAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var client = _containerClient.GetBlobClient(path);
        try
        {
            var content = await client.DownloadContentAsync(cancellationToken);
            return JsonSerializer.Deserialize<RetailPricingManifestDocument>(
                content.Value.Content.ToString(),
                JsonOptions);
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
