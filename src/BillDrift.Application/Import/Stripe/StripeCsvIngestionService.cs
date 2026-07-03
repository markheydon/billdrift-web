using System.Security.Cryptography;
using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Application.Ingestion;
using BillDrift.Application.Normalization;
using BillDrift.Domain.Billing;

namespace BillDrift.Application.Import.Stripe;

/// <summary>
/// Orchestrates Stripe CSV upload, parsing, normalization, and Azure persistence.
/// </summary>
public sealed class StripeCsvIngestionService : IStripeCsvIngestionService
{
    private readonly IStripeBillingCsvIngester _ingester;
    private readonly IStripeBillingNormalizer _billingNormalizer;
    private readonly IStripeCatalogueNormalizer _catalogueNormalizer;
    private readonly IIngestionBlobStore _blobStore;
    private readonly IIngestionRunIndexStore _indexStore;

    public StripeCsvIngestionService(
        IStripeBillingCsvIngester ingester,
        IStripeBillingNormalizer billingNormalizer,
        IStripeCatalogueNormalizer catalogueNormalizer,
        IIngestionBlobStore blobStore,
        IIngestionRunIndexStore indexStore)
    {
        _ingester = ingester;
        _billingNormalizer = billingNormalizer;
        _catalogueNormalizer = catalogueNormalizer;
        _blobStore = blobStore;
        _indexStore = indexStore;
    }

    /// <inheritdoc />
    public async Task<StripeCsvIngestionRun> IngestAndPersistAsync(
        StripeCsvUploadFiles files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var subscriptionBytes = await ReadStreamAsync(files.Subscriptions, cancellationToken);
        var productsBytes = files.Products is not null ? await ReadStreamAsync(files.Products, cancellationToken) : null;
        var pricesBytes = files.Prices is not null ? await ReadStreamAsync(files.Prices, cancellationToken) : null;

        var ingestionId = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;
        var contentFingerprint = ComputeBundleFingerprint(subscriptionBytes, productsBytes, pricesBytes);
        var label = files.SubscriptionsFileName ?? "subscriptions.csv";

        var sourceBlobPath = await _blobStore.UploadSourceAsync(
            ingestionId,
            subscriptionBytes,
            label,
            cancellationToken);

        var inProgress = new StripeCsvIngestionRun
        {
            IngestionId = ingestionId,
            OriginalFileName = label,
            ContentFingerprint = contentFingerprint,
            UploadedAt = uploadedAt,
            Status = IngestionRunStatus.InProgress,
            SourceBlobPath = sourceBlobPath
        };

        await _indexStore.CreateStripeCsvInProgressAsync(inProgress, cancellationToken);

        try
        {
            var requestFiles = new List<StripeCsvFileInput>
            {
                new(StripeCsvFileKind.Subscriptions, new MemoryStream(subscriptionBytes), files.SubscriptionsFileName)
            };

            if (productsBytes is not null)
            {
                requestFiles.Add(new StripeCsvFileInput(
                    StripeCsvFileKind.Products,
                    new MemoryStream(productsBytes),
                    files.ProductsFileName));
            }

            if (pricesBytes is not null)
            {
                requestFiles.Add(new StripeCsvFileInput(
                    StripeCsvFileKind.Prices,
                    new MemoryStream(pricesBytes),
                    files.PricesFileName));
            }

            var result = _ingester.Ingest(new StripeCsvIngestionRequest(requestFiles), cancellationToken);

            if (result.Status == IngestionOutcomeStatus.Failure)
            {
                var failedRun = new StripeCsvIngestionRun
                {
                    IngestionId = ingestionId,
                    OriginalFileName = label,
                    ContentFingerprint = contentFingerprint,
                    UploadedAt = uploadedAt,
                    CompletedAt = result.IngestedAt,
                    Status = IngestionRunStatus.Failed,
                    Summary = result.Summary,
                    SourceBlobPath = sourceBlobPath,
                    FailureReason = IngestionFailureReasonBuilder.Build(
                        result.Status,
                        result.LogEntries,
                        "Stripe CSV ingestion failed.")
                };

                await _indexStore.FailStripeCsvAsync(failedRun, cancellationToken);
                return failedRun;
            }

            var billingItems = NormalizeBillingItems(result);
            var manifestPath = await _blobStore.PersistStripeBillingItemsAsync(
                ingestionId,
                result,
                billingItems,
                label,
                uploadedAt,
                cancellationToken);

            if (result.Products.Count > 0 || result.Prices.Count > 0)
            {
                var catalogueProducts = _catalogueNormalizer.NormalizeProducts(result.Products);
                var cataloguePrices = _catalogueNormalizer.NormalizePrices(result.Prices);
                await _blobStore.PersistStripeCatalogueAsync(
                    ingestionId,
                    catalogueProducts,
                    cataloguePrices,
                    cancellationToken);
            }

            var completed = new StripeCsvIngestionRun
            {
                IngestionId = ingestionId,
                OriginalFileName = label,
                ContentFingerprint = contentFingerprint,
                UploadedAt = uploadedAt,
                CompletedAt = result.IngestedAt,
                Status = MapStatus(result.Status),
                Summary = result.Summary,
                SourceBlobPath = sourceBlobPath,
                ResultManifestBlobPath = manifestPath
            };

            await _indexStore.CompleteStripeCsvAsync(completed, cancellationToken);
            return completed;
        }
        catch (Exception ex)
        {
            var failed = new StripeCsvIngestionRun
            {
                IngestionId = ingestionId,
                OriginalFileName = label,
                ContentFingerprint = contentFingerprint,
                UploadedAt = uploadedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Status = IngestionRunStatus.Failed,
                SourceBlobPath = sourceBlobPath,
                FailureReason = ex.Message
            };

            await _indexStore.FailStripeCsvAsync(failed, cancellationToken);
            throw;
        }
    }

    private List<StripeBillingItem> NormalizeBillingItems(StripeCsvIngestionResult result)
    {
        var items = new List<StripeBillingItem>();
        var itemsByCustomer = result.SubscriptionItems
            .GroupBy(i => i.CustomerId, StringComparer.Ordinal);

        foreach (var customerGroup in itemsByCustomer)
        {
            var customer = result.Customers.FirstOrDefault(c => c.CustomerId == customerGroup.Key);
            if (customer is null)
            {
                continue;
            }

            var customerSubscriptions = result.Subscriptions
                .Where(s => s.CustomerId == customer.CustomerId)
                .ToList();
            var customerItems = customerGroup.ToList();

            items.AddRange(_billingNormalizer.Normalize(
                customer,
                customerSubscriptions,
                customerItems,
                result.Products,
                result.Prices));
        }

        return items;
    }

    private static async Task<byte[]> ReadStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    private static IngestionRunStatus MapStatus(IngestionOutcomeStatus status) => status switch
    {
        IngestionOutcomeStatus.Success => IngestionRunStatus.Completed,
        IngestionOutcomeStatus.PartialSuccess => IngestionRunStatus.PartialSuccess,
        _ => IngestionRunStatus.Failed
    };

    private static string ComputeBundleFingerprint(
        byte[] subscriptions,
        byte[]? products,
        byte[]? prices)
    {
        var parts = new List<string> { HashBytes(subscriptions) };
        if (products is not null)
        {
            parts.Add(HashBytes(products));
        }

        if (prices is not null)
        {
            parts.Add(HashBytes(prices));
        }

        parts.Sort(StringComparer.Ordinal);
        var combined = string.Join("|", parts);
        return HashBytes(System.Text.Encoding.UTF8.GetBytes(combined));
    }

    private static string HashBytes(ReadOnlySpan<byte> content)
    {
        var hash = SHA256.HashData(content);
        return Convert.ToHexStringLower(hash);
    }
}
