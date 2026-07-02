using BillDrift.Application.Import;
using BillDrift.Domain.Import.Stripe;
using BillDrift.Infrastructure.Import.Stripe.Internal;

namespace BillDrift.Infrastructure.Import.Stripe;

/// <summary>
/// Orchestrates the Stripe billing CSV ingestion pipeline from file intake through
/// <see cref="RawStripeSubscriptionItem"/> emission.
/// </summary>
/// <remarks>
/// <para>
/// Pipeline stages: intake → header detection → row parsing → catalogue assembly →
/// subscription assembly → metadata checks → status filtering → catalogue cross-check →
/// output assembly → logging.
/// </para>
/// <para>
/// <b>Deterministic identity:</b> per-file <c>SourceDocumentId</c> is SHA-256 hex of CSV bytes;
/// <c>BundleId</c> hashes sorted per-file fingerprints.
/// </para>
/// <para>
/// <b>Partial failure tiers:</b> row skip (unparseable fields, missing IDs), file fail
/// (mandatory headers missing), partial success when valid siblings exist.
/// </para>
/// </remarks>
public sealed class StripeBillingCsvIngester : IStripeBillingCsvIngester
{
    private const int MaxSnippetLength = 200;

    private readonly StripeCsvRowReader _rowReader = new();
    private readonly SubscriptionsCsvParser _subscriptionsParser = new();
    private readonly ProductsCsvParser _productsParser = new();
    private readonly PricesCsvParser _pricesParser = new();

    /// <inheritdoc />
    public StripeCsvIngestionResult Ingest(
        StripeCsvIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = request.Options ?? new StripeCsvIngestionOptions();
        var ingestedAt = DateTimeOffset.UtcNow;
        var logs = new List<IngestionLogEntry>();

        if (request.Files.Count == 0)
        {
            throw new ArgumentException("At least one CSV file is required.", nameof(request));
        }

        var subscriptionsInput = request.Files.FirstOrDefault(f => f.FileKind == StripeCsvFileKind.Subscriptions);
        if (subscriptionsInput is null)
        {
            throw new ArgumentException("A subscriptions CSV file is required.", nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var fileData = new List<FileData>();
        foreach (var file in request.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(file.Content);

            byte[] bytes;
            using (file.Content)
            {
                bytes = ReadStream(file.Content);
            }
            if (bytes.Length > options.MaxFileSizeBytes)
            {
                var docId = StripeFileIdentity.ComputeSourceDocumentId(bytes);
                return FailureResult(string.Empty, ingestedAt, docId, StripeCsvFileKind.Subscriptions,
                    IngestionFailureReason.FileSizeExceeded, "CSV file exceeds maximum allowed size.", logs);
            }

            fileData.Add(new FileData(file.FileKind, file.OriginalFileName, bytes));
        }

        var sourceFiles = new List<StripeCsvSourceFileInfo>();
        var fileHashes = new List<(StripeCsvFileKind Kind, string Hash)>();

        StripeCsvReadResult? subscriptionsRead = null;
        string subscriptionsDocId = string.Empty;
        IReadOnlyList<ParsedSubscriptionRow> subscriptionRows = [];

        var products = new List<RawStripeProduct>();
        var prices = new List<RawStripePrice>();
        var hasProductsFile = false;
        var hasPricesFile = false;
        var productsSkipped = 0;
        var pricesSkipped = 0;

        foreach (var data in fileData.OrderBy(f => f.Kind))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var docId = StripeFileIdentity.ComputeSourceDocumentId(data.Bytes);
            fileHashes.Add((data.Kind, docId));

            var readResult = _rowReader.Read(data.Bytes, data.Kind);
            if (!readResult.IsSuccess)
            {
                if (data.Kind == StripeCsvFileKind.Subscriptions &&
                    readResult.FileFailure == IngestionFailureReason.EmptyFile)
                {
                    subscriptionsRead = readResult;
                    subscriptionsDocId = docId;
                    subscriptionRows = [];
                    sourceFiles.Add(new StripeCsvSourceFileInfo(data.Kind, docId, data.OriginalFileName, 0));
                    logs.Add(CreateFileLog(
                        IngestionLogSeverity.Warning,
                        IngestionFailureReason.EmptyFile,
                        readResult.FailureMessage ?? "Subscriptions file has no data rows.",
                        StripeCsvFileKind.Subscriptions,
                        docId));
                    continue;
                }

                if (data.Kind == StripeCsvFileKind.Subscriptions)
                {
                    return FailureResult(string.Empty, ingestedAt, docId, data.Kind,
                        readResult.FileFailure!.Value,
                        readResult.FailureMessage ?? "Subscriptions file could not be read.",
                        logs);
                }

                logs.Add(CreateFileLog(
                    IngestionLogSeverity.Error,
                    readResult.FileFailure!.Value,
                    readResult.FailureMessage ?? "Catalogue file could not be read.",
                    data.Kind,
                    docId));
                sourceFiles.Add(new StripeCsvSourceFileInfo(data.Kind, docId, data.OriginalFileName, 0));
                continue;
            }

            sourceFiles.Add(new StripeCsvSourceFileInfo(
                data.Kind, docId, data.OriginalFileName, readResult.Rows.Count));

            switch (data.Kind)
            {
                case StripeCsvFileKind.Subscriptions:
                    subscriptionsRead = readResult;
                    subscriptionsDocId = docId;
                    subscriptionRows = _subscriptionsParser.Parse(readResult);
                    break;

                case StripeCsvFileKind.Products:
                    hasProductsFile = true;
                    foreach (var row in _productsParser.Parse(readResult))
                    {
                        var mapped = RawStripeRecordMapper.MapProduct(row, docId);
                        if (mapped is null)
                        {
                            productsSkipped++;
                            logs.Add(CreateRowLog(
                                IngestionLogSeverity.Error,
                                IngestionFailureReason.StripeIdMissing,
                                "Product row skipped because product ID or name is missing.",
                                StripeCsvFileKind.Products,
                                row.RowNumber,
                                docId,
                                row.ProductId));
                        }
                        else
                        {
                            products.Add(mapped);
                        }
                    }

                    break;

                case StripeCsvFileKind.Prices:
                    hasPricesFile = true;
                    foreach (var row in _pricesParser.Parse(readResult))
                    {
                        if (!string.IsNullOrWhiteSpace(row.UnitAmountRaw) &&
                            !RawStripeRecordMapper.TryParseAmount(row.UnitAmountRaw, row.Currency ?? "gbp", out _))
                        {
                            pricesSkipped++;
                            logs.Add(CreateRowLog(
                                IngestionLogSeverity.Error,
                                IngestionFailureReason.AmountUnparseable,
                                "Price row skipped because unit amount could not be parsed.",
                                StripeCsvFileKind.Prices,
                                row.RowNumber,
                                docId,
                                row.UnitAmountRaw));
                            continue;
                        }

                        var mapped = RawStripeRecordMapper.MapPrice(row, docId);
                        if (mapped is null)
                        {
                            pricesSkipped++;
                            logs.Add(CreateRowLog(
                                IngestionLogSeverity.Error,
                                IngestionFailureReason.StripeIdMissing,
                                "Price row skipped because price ID, product ID, or currency is missing.",
                                StripeCsvFileKind.Prices,
                                row.RowNumber,
                                docId,
                                row.PriceId));
                        }
                        else
                        {
                            prices.Add(mapped);
                        }
                    }

                    break;
            }
        }

        if (subscriptionsRead is null)
        {
            return FailureResult(string.Empty, ingestedAt, subscriptionsDocId, StripeCsvFileKind.Subscriptions,
                IngestionFailureReason.MandatoryHeaderMissing, "Subscriptions file was not processed.", logs);
        }

        var customers = new Dictionary<string, RawStripeCustomer>(StringComparer.Ordinal);
        var subscriptions = new Dictionary<string, RawStripeSubscription>(StringComparer.Ordinal);
        var subscriptionItems = new List<RawStripeSubscriptionItem>();
        var itemsSkipped = 0;
        var filteredByStatus = 0;
        var metadataWarnings = 0;
        var catalogueWarnings = 0;

        var productIds = products.Select(p => p.ProductId).ToHashSet(StringComparer.Ordinal);
        var priceIds = prices.Select(p => p.PriceId).ToHashSet(StringComparer.Ordinal);

        foreach (var row in subscriptionRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!StripeStatusFilter.ShouldInclude(row.Status, options.IncludeInactiveSubscriptions))
            {
                filteredByStatus++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.CustomerId) ||
                string.IsNullOrWhiteSpace(row.SubscriptionId) ||
                string.IsNullOrWhiteSpace(row.ProductId) ||
                string.IsNullOrWhiteSpace(row.PriceId))
            {
                itemsSkipped++;
                logs.Add(CreateRowLog(
                    IngestionLogSeverity.Error,
                    IngestionFailureReason.StripeIdMissing,
                    "Subscription row skipped because a required Stripe ID is missing.",
                    StripeCsvFileKind.Subscriptions,
                    row.RowNumber,
                    subscriptionsDocId,
                    row.SubscriptionId));
                continue;
            }

            if (!RawStripeRecordMapper.TryParseQuantity(row.QuantityRaw, out _))
            {
                itemsSkipped++;
                logs.Add(CreateRowLog(
                    IngestionLogSeverity.Error,
                    IngestionFailureReason.QuantityUnparseable,
                    "Subscription row skipped because quantity could not be parsed.",
                    StripeCsvFileKind.Subscriptions,
                    row.RowNumber,
                    subscriptionsDocId,
                    row.QuantityRaw));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.UnitAmountRaw) &&
                !RawStripeRecordMapper.TryParseAmount(row.UnitAmountRaw, "gbp", out _))
            {
                itemsSkipped++;
                logs.Add(CreateRowLog(
                    IngestionLogSeverity.Error,
                    IngestionFailureReason.AmountUnparseable,
                    "Subscription row skipped because unit amount could not be parsed.",
                    StripeCsvFileKind.Subscriptions,
                    row.RowNumber,
                    subscriptionsDocId,
                    row.UnitAmountRaw));
                continue;
            }

            var metadataWarning = EvaluateMetadataWarnings(row, subscriptionsDocId, logs);
            if (metadataWarning)
            {
                metadataWarnings++;
            }

            var item = RawStripeRecordMapper.MapSubscriptionItem(row, subscriptionsDocId);
            if (item is null)
            {
                itemsSkipped++;
                continue;
            }

            subscriptionItems.Add(item);

            var customer = RawStripeRecordMapper.MapCustomer(row);
            if (customer is not null)
            {
                customers.TryAdd(customer.CustomerId, customer);
            }

            var subscription = RawStripeRecordMapper.MapSubscription(row);
            if (subscription is not null)
            {
                subscriptions.TryAdd(subscription.SubscriptionId, subscription);
            }

            if (hasProductsFile && !productIds.Contains(item.ProductId))
            {
                catalogueWarnings++;
                logs.Add(CreateRowLog(
                    IngestionLogSeverity.Warning,
                    IngestionFailureReason.CatalogueReferenceUnresolved,
                    $"Subscription item references product '{item.ProductId}' not found in products CSV.",
                    StripeCsvFileKind.Subscriptions,
                    row.RowNumber,
                    subscriptionsDocId,
                    item.ProductId));
            }

            if (hasPricesFile && !priceIds.Contains(item.PriceId))
            {
                catalogueWarnings++;
                logs.Add(CreateRowLog(
                    IngestionLogSeverity.Warning,
                    IngestionFailureReason.CatalogueReferenceUnresolved,
                    $"Subscription item references price '{item.PriceId}' not found in prices CSV.",
                    StripeCsvFileKind.Subscriptions,
                    row.RowNumber,
                    subscriptionsDocId,
                    item.PriceId));
            }
        }

        var bundleId = StripeFileIdentity.ComputeBundleId(fileHashes);
        var summary = new StripeCsvIngestionSummary
        {
            SubscriptionItemsExtracted = subscriptionItems.Count,
            SubscriptionItemsSkipped = itemsSkipped,
            SubscriptionsFilteredByStatus = filteredByStatus,
            ProductsExtracted = products.Count,
            ProductsSkipped = productsSkipped,
            PricesExtracted = prices.Count,
            PricesSkipped = pricesSkipped,
            MetadataWarnings = metadataWarnings,
            CatalogueWarnings = catalogueWarnings,
            CustomersExtracted = customers.Count
        };

        var status = DetermineStatus(subscriptionItems.Count, itemsSkipped, logs, subscriptionsRead);

        return new StripeCsvIngestionResult
        {
            BundleId = bundleId,
            IngestedAt = ingestedAt,
            Status = status,
            Customers = customers.Values.ToList(),
            Subscriptions = subscriptions.Values.ToList(),
            SubscriptionItems = subscriptionItems,
            Products = products,
            Prices = prices,
            LogEntries = logs,
            Summary = summary,
            SourceFiles = sourceFiles
        };
    }

    private static IngestionOutcomeStatus DetermineStatus(
        int itemsExtracted,
        int itemsSkipped,
        IReadOnlyList<IngestionLogEntry> logs,
        StripeCsvReadResult subscriptionsRead)
    {
        if (subscriptionsRead.FileFailure == IngestionFailureReason.MandatoryHeaderMissing)
        {
            return IngestionOutcomeStatus.Failure;
        }

        if (itemsExtracted == 0 && subscriptionsRead.Rows.Count > 0)
        {
            return IngestionOutcomeStatus.Failure;
        }

        if (subscriptionsRead.FileFailure == IngestionFailureReason.EmptyFile && itemsExtracted == 0)
        {
            return IngestionOutcomeStatus.Success;
        }

        if (itemsSkipped > 0 || logs.Any(l => l.Severity == IngestionLogSeverity.Warning))
        {
            return itemsExtracted > 0 ? IngestionOutcomeStatus.PartialSuccess : IngestionOutcomeStatus.Failure;
        }

        return IngestionOutcomeStatus.Success;
    }

    private static bool EvaluateMetadataWarnings(
        ParsedSubscriptionRow row,
        string sourceDocumentId,
        List<IngestionLogEntry> logs)
    {
        var mexId = StripeMetadataParser.GetMexId(row.Metadata);
        var offerId = StripeMetadataParser.GetOfferId(row.Metadata);
        var skuId = StripeMetadataParser.GetSkuId(row.Metadata);
        var warned = false;

        if (string.IsNullOrWhiteSpace(mexId))
        {
            warned = true;
            logs.Add(CreateRowLog(
                IngestionLogSeverity.Warning,
                IngestionFailureReason.MetadataIncomplete,
                "Subscription row metadata is missing mex_id.",
                StripeCsvFileKind.Subscriptions,
                row.RowNumber,
                sourceDocumentId,
                null));
        }

        var hasOffer = !string.IsNullOrWhiteSpace(offerId);
        var hasSku = !string.IsNullOrWhiteSpace(skuId);
        if (hasOffer ^ hasSku)
        {
            warned = true;
            logs.Add(CreateRowLog(
                IngestionLogSeverity.Warning,
                IngestionFailureReason.MetadataInconsistent,
                "Subscription row has offer_id without sku_id or vice versa.",
                StripeCsvFileKind.Subscriptions,
                row.RowNumber,
                sourceDocumentId,
                offerId ?? skuId));
        }

        return warned;
    }

    private static StripeCsvIngestionResult FailureResult(
        string bundleId,
        DateTimeOffset ingestedAt,
        string sourceDocumentId,
        StripeCsvFileKind fileKind,
        IngestionFailureReason reason,
        string message,
        List<IngestionLogEntry> logs)
    {
        logs.Add(CreateFileLog(IngestionLogSeverity.Error, reason, message, fileKind, sourceDocumentId));

        return new StripeCsvIngestionResult
        {
            BundleId = bundleId,
            IngestedAt = ingestedAt,
            Status = IngestionOutcomeStatus.Failure,
            Customers = [],
            Subscriptions = [],
            SubscriptionItems = [],
            Products = [],
            Prices = [],
            LogEntries = logs,
            Summary = new StripeCsvIngestionSummary(),
            SourceFiles = []
        };
    }

    private static IngestionLogEntry CreateRowLog(
        IngestionLogSeverity severity,
        IngestionFailureReason reason,
        string message,
        StripeCsvFileKind fileKind,
        int rowNumber,
        string sourceDocumentId,
        string? snippet)
    {
        return new IngestionLogEntry(
            severity,
            reason,
            message,
            new IngestionLocation(0, (int)fileKind, rowNumber),
            CapSnippet(snippet),
            sourceDocumentId);
    }

    private static IngestionLogEntry CreateFileLog(
        IngestionLogSeverity severity,
        IngestionFailureReason reason,
        string message,
        StripeCsvFileKind fileKind,
        string sourceDocumentId) =>
        new(
            severity,
            reason,
            message,
            new IngestionLocation(0, (int)fileKind, null),
            null,
            sourceDocumentId);

    private static string? CapSnippet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaxSnippetLength ? trimmed : trimmed[..MaxSnippetLength];
    }

    private static byte[] ReadStream(Stream stream)
    {
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var segment))
        {
            return segment.ToArray();
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed record FileData(StripeCsvFileKind Kind, string? OriginalFileName, byte[] Bytes);
}
