using Azure.Data.Tables;
using BillDrift.Application.Import;
using BillDrift.Application.Ingestion;
using BillDrift.Domain.Common;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Ingestion;

/// <summary>Azure Table Storage implementation for ingestion run index rows.</summary>
public sealed class AzureTableIngestionRunIndexStore : IIngestionRunIndexStore
{
    private const string PartitionKey = "GiacomSubscriptionManagement";

    private readonly TableClient _tableClient;
    private bool _tableEnsured;

    /// <summary>Creates a store using an Aspire-injected table service client.</summary>
    public AzureTableIngestionRunIndexStore(TableServiceClient tableServiceClient, IOptions<IngestionStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(tableServiceClient);
        _tableClient = tableServiceClient.GetTableClient(options.Value.TableName);
    }

    /// <inheritdoc />
    public async Task CreateInProgressAsync(SubscriptionManagementIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.AddEntityAsync(ToEntity(run), cancellationToken);
    }

    /// <inheritdoc />
    public async Task CompleteAsync(SubscriptionManagementIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.UpsertEntityAsync(ToEntity(run), TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc />
    public async Task FailAsync(SubscriptionManagementIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.UpsertEntityAsync(ToEntity(run), TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SubscriptionManagementIngestionRun?> GetByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter = $"PartitionKey eq '{PartitionKey}' and IngestionId eq '{ingestionId:D}'";

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            return FromEntity(entity);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubscriptionManagementIngestionRun>> ListRecentAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var results = new List<SubscriptionManagementIngestionRun>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
                           $"PartitionKey eq '{PartitionKey}'",
                           cancellationToken: cancellationToken))
        {
            results.Add(FromEntity(entity));
            if (results.Count >= take)
            {
                break;
            }
        }

        return results.OrderByDescending(r => r.UploadedAt).Take(take).ToList();
    }

    /// <inheritdoc />
    public async Task CreateRetailPricingInProgressAsync(RetailPricingIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.AddEntityAsync(ToRetailPricingEntity(run), cancellationToken);
    }

    /// <inheritdoc />
    public async Task CompleteRetailPricingAsync(RetailPricingIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.UpsertEntityAsync(ToRetailPricingEntity(run), TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc />
    public async Task FailRetailPricingAsync(RetailPricingIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.UpsertEntityAsync(ToRetailPricingEntity(run), TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RetailPricingIngestionRun?> GetRetailPricingByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter = $"PartitionKey eq '{RetailPricingPartitionKey}' and IngestionId eq '{ingestionId:D}'";

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            return FromRetailPricingEntity(entity);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RetailPricingIngestionRun>> ListRecentRetailPricingAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var results = new List<RetailPricingIngestionRun>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
                           $"PartitionKey eq '{RetailPricingPartitionKey}'",
                           cancellationToken: cancellationToken))
        {
            results.Add(FromRetailPricingEntity(entity));
            if (results.Count >= take)
            {
                break;
            }
        }

        return results.OrderByDescending(r => r.UploadedAt).Take(take).ToList();
    }

    private const string RetailPricingPartitionKey = "GiacomPriceList";

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (_tableEnsured)
        {
            return;
        }

        await _tableClient.CreateIfNotExistsAsync(cancellationToken);
        _tableEnsured = true;
    }

    private static string EncodeRowKey(DateTimeOffset uploadedAt, Guid ingestionId) =>
        $"{DateTimeOffset.MaxValue.Ticks - uploadedAt.UtcTicks:D19}_{ingestionId:D}";

    private static TableEntity ToEntity(SubscriptionManagementIngestionRun run)
    {
        var entity = new TableEntity(PartitionKey, EncodeRowKey(run.UploadedAt, run.IngestionId))
        {
            ["IngestionId"] = run.IngestionId.ToString("D"),
            ["SourceKind"] = run.SourceKind.ToString(),
            ["ContentFingerprint"] = run.ContentFingerprint,
            ["UploadedAt"] = run.UploadedAt,
            ["Status"] = run.Status.ToString(),
            ["SourceBlobPath"] = run.SourceBlobPath
        };

        if (!string.IsNullOrWhiteSpace(run.OriginalFileName))
        {
            entity["OriginalFileName"] = run.OriginalFileName;
        }

        if (run.CompletedAt is not null)
        {
            entity["CompletedAt"] = run.CompletedAt;
        }

        if (run.Summary is not null)
        {
            entity["RowsEmitted"] = run.Summary.RowsEmitted;
            entity["RowsExcludedByScope"] = run.Summary.RowsExcludedByScope;
            entity["RowsSkipped"] = run.Summary.RowsSkipped;
        }

        if (!string.IsNullOrWhiteSpace(run.ResultManifestBlobPath))
        {
            entity["ManifestBlobPath"] = run.ResultManifestBlobPath;
        }

        if (!string.IsNullOrWhiteSpace(run.FailureReason))
        {
            entity["FailureReason"] = run.FailureReason;
        }

        return entity;
    }

    private static SubscriptionManagementIngestionRun FromEntity(TableEntity entity)
    {
        var ingestionId = Guid.Parse(entity.GetString("IngestionId")!);
        Enum.TryParse(entity.GetString("SourceKind"), out ImportSourceKind sourceKind);
        Enum.TryParse(entity.GetString("Status"), out IngestionRunStatus status);

        SubscriptionManagementCsvIngestionSummary? summary = null;
        if (entity.TryGetValue("RowsEmitted", out _))
        {
            summary = new SubscriptionManagementCsvIngestionSummary
            {
                RowsEmitted = entity.GetInt32("RowsEmitted") ?? 0,
                RowsExcludedByScope = entity.GetInt32("RowsExcludedByScope") ?? 0,
                RowsSkipped = entity.GetInt32("RowsSkipped") ?? 0
            };
        }

        return new SubscriptionManagementIngestionRun
        {
            IngestionId = ingestionId,
            SourceKind = sourceKind,
            OriginalFileName = entity.GetString("OriginalFileName"),
            ContentFingerprint = entity.GetString("ContentFingerprint") ?? string.Empty,
            UploadedAt = entity.GetDateTimeOffset("UploadedAt") ?? DateTimeOffset.MinValue,
            CompletedAt = entity.GetDateTimeOffset("CompletedAt"),
            Status = status,
            Summary = summary,
            SourceBlobPath = entity.GetString("SourceBlobPath") ?? string.Empty,
            ResultManifestBlobPath = entity.GetString("ManifestBlobPath"),
            FailureReason = entity.GetString("FailureReason")
        };
    }

    private static TableEntity ToRetailPricingEntity(RetailPricingIngestionRun run)
    {
        var entity = new TableEntity(RetailPricingPartitionKey, EncodeRowKey(run.UploadedAt, run.IngestionId))
        {
            ["IngestionId"] = run.IngestionId.ToString("D"),
            ["SourceKind"] = run.SourceKind.ToString(),
            ["ContentFingerprint"] = run.ContentFingerprint,
            ["UploadedAt"] = run.UploadedAt,
            ["Status"] = run.Status.ToString(),
            ["SourceBlobPath"] = run.SourceBlobPath
        };

        if (!string.IsNullOrWhiteSpace(run.OriginalFileName))
        {
            entity["OriginalFileName"] = run.OriginalFileName;
        }

        if (run.CompletedAt is not null)
        {
            entity["CompletedAt"] = run.CompletedAt;
        }

        if (run.Summary is not null)
        {
            entity["ResolvedPriceCount"] = run.Summary.ResolvedPriceCount;
            entity["CatalogueRowsSkipped"] = run.Summary.CatalogueRowsSkipped;
            entity["OverrideWinsCount"] = run.Summary.OverrideWinsCount;
        }

        if (!string.IsNullOrWhiteSpace(run.ResultManifestBlobPath))
        {
            entity["ManifestBlobPath"] = run.ResultManifestBlobPath;
        }

        if (!string.IsNullOrWhiteSpace(run.FailureReason))
        {
            entity["FailureReason"] = run.FailureReason;
        }

        return entity;
    }

    private static RetailPricingIngestionRun FromRetailPricingEntity(TableEntity entity)
    {
        var ingestionId = Guid.Parse(entity.GetString("IngestionId")!);
        Enum.TryParse(entity.GetString("SourceKind"), out ImportSourceKind sourceKind);
        Enum.TryParse(entity.GetString("Status"), out IngestionRunStatus status);

        RetailPricingCsvIngestionSummary? summary = null;
        if (entity.TryGetValue("ResolvedPriceCount", out _))
        {
            summary = new RetailPricingCsvIngestionSummary
            {
                ResolvedPriceCount = entity.GetInt32("ResolvedPriceCount") ?? 0,
                CatalogueRowsSkipped = entity.GetInt32("CatalogueRowsSkipped") ?? 0,
                OverrideWinsCount = entity.GetInt32("OverrideWinsCount") ?? 0
            };
        }

        return new RetailPricingIngestionRun
        {
            IngestionId = ingestionId,
            SourceKind = sourceKind,
            OriginalFileName = entity.GetString("OriginalFileName"),
            ContentFingerprint = entity.GetString("ContentFingerprint") ?? string.Empty,
            UploadedAt = entity.GetDateTimeOffset("UploadedAt") ?? DateTimeOffset.MinValue,
            CompletedAt = entity.GetDateTimeOffset("CompletedAt"),
            Status = status,
            Summary = summary,
            SourceBlobPath = entity.GetString("SourceBlobPath") ?? string.Empty,
            ResultManifestBlobPath = entity.GetString("ManifestBlobPath"),
            FailureReason = entity.GetString("FailureReason")
        };
    }

    private const string GiacomPdfPartitionKey = "GiacomBillingPdf";
    private const string StripeCsvPartitionKey = "StripeExport";

    /// <inheritdoc />
    public async Task CreateGiacomPdfInProgressAsync(GiacomPdfIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.AddEntityAsync(ToGiacomPdfEntity(run), cancellationToken);
    }

    /// <inheritdoc />
    public async Task CompleteGiacomPdfAsync(GiacomPdfIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.UpsertEntityAsync(ToGiacomPdfEntity(run), TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc />
    public async Task FailGiacomPdfAsync(GiacomPdfIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.UpsertEntityAsync(ToGiacomPdfEntity(run), TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GiacomPdfIngestionRun?> GetGiacomPdfByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter = $"PartitionKey eq '{GiacomPdfPartitionKey}' and IngestionId eq '{ingestionId:D}'";

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            return FromGiacomPdfEntity(entity);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GiacomPdfIngestionRun>> ListRecentGiacomPdfAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var results = new List<GiacomPdfIngestionRun>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
                           $"PartitionKey eq '{GiacomPdfPartitionKey}'",
                           cancellationToken: cancellationToken))
        {
            results.Add(FromGiacomPdfEntity(entity));
            if (results.Count >= take)
            {
                break;
            }
        }

        return results.OrderByDescending(r => r.UploadedAt).Take(take).ToList();
    }

    /// <inheritdoc />
    public async Task CreateStripeCsvInProgressAsync(StripeCsvIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.AddEntityAsync(ToStripeCsvEntity(run), cancellationToken);
    }

    /// <inheritdoc />
    public async Task CompleteStripeCsvAsync(StripeCsvIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.UpsertEntityAsync(ToStripeCsvEntity(run), TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc />
    public async Task FailStripeCsvAsync(StripeCsvIngestionRun run, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.UpsertEntityAsync(ToStripeCsvEntity(run), TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StripeCsvIngestionRun?> GetStripeCsvByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter = $"PartitionKey eq '{StripeCsvPartitionKey}' and IngestionId eq '{ingestionId:D}'";

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            return FromStripeCsvEntity(entity);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StripeCsvIngestionRun>> ListRecentStripeCsvAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var results = new List<StripeCsvIngestionRun>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
                           $"PartitionKey eq '{StripeCsvPartitionKey}'",
                           cancellationToken: cancellationToken))
        {
            results.Add(FromStripeCsvEntity(entity));
            if (results.Count >= take)
            {
                break;
            }
        }

        return results.OrderByDescending(r => r.UploadedAt).Take(take).ToList();
    }

    private static TableEntity ToGiacomPdfEntity(GiacomPdfIngestionRun run)
    {
        var entity = new TableEntity(GiacomPdfPartitionKey, EncodeRowKey(run.UploadedAt, run.IngestionId))
        {
            ["IngestionId"] = run.IngestionId.ToString("D"),
            ["SourceKind"] = run.SourceKind.ToString(),
            ["ContentFingerprint"] = run.ContentFingerprint,
            ["UploadedAt"] = run.UploadedAt,
            ["Status"] = run.Status.ToString(),
            ["SourceBlobPath"] = run.SourceBlobPath
        };

        if (!string.IsNullOrWhiteSpace(run.OriginalFileName))
        {
            entity["OriginalFileName"] = run.OriginalFileName;
        }

        if (run.CompletedAt is not null)
        {
            entity["CompletedAt"] = run.CompletedAt;
        }

        if (run.Summary is not null)
        {
            entity["LinesExtracted"] = run.Summary.LinesExtracted;
            entity["LinesSkipped"] = run.Summary.LinesSkipped;
        }

        if (!string.IsNullOrWhiteSpace(run.ResultManifestBlobPath))
        {
            entity["ManifestBlobPath"] = run.ResultManifestBlobPath;
        }

        if (!string.IsNullOrWhiteSpace(run.FailureReason))
        {
            entity["FailureReason"] = run.FailureReason;
        }

        return entity;
    }

    private static GiacomPdfIngestionRun FromGiacomPdfEntity(TableEntity entity)
    {
        var ingestionId = Guid.Parse(entity.GetString("IngestionId")!);
        Enum.TryParse(entity.GetString("SourceKind"), out ImportSourceKind sourceKind);
        Enum.TryParse(entity.GetString("Status"), out IngestionRunStatus status);

        GiacomPdfIngestionSummary? summary = null;
        if (entity.TryGetValue("LinesExtracted", out _))
        {
            summary = new GiacomPdfIngestionSummary(
                entity.GetInt32("LinesExtracted") ?? 0,
                entity.GetInt32("LinesSkipped") ?? 0,
                0,
                0,
                0);
        }

        return new GiacomPdfIngestionRun
        {
            IngestionId = ingestionId,
            SourceKind = sourceKind,
            OriginalFileName = entity.GetString("OriginalFileName"),
            ContentFingerprint = entity.GetString("ContentFingerprint") ?? string.Empty,
            UploadedAt = entity.GetDateTimeOffset("UploadedAt") ?? DateTimeOffset.MinValue,
            CompletedAt = entity.GetDateTimeOffset("CompletedAt"),
            Status = status,
            Summary = summary,
            SourceBlobPath = entity.GetString("SourceBlobPath") ?? string.Empty,
            ResultManifestBlobPath = entity.GetString("ManifestBlobPath"),
            FailureReason = entity.GetString("FailureReason")
        };
    }

    private static TableEntity ToStripeCsvEntity(StripeCsvIngestionRun run)
    {
        var entity = new TableEntity(StripeCsvPartitionKey, EncodeRowKey(run.UploadedAt, run.IngestionId))
        {
            ["IngestionId"] = run.IngestionId.ToString("D"),
            ["SourceKind"] = run.SourceKind.ToString(),
            ["ContentFingerprint"] = run.ContentFingerprint,
            ["UploadedAt"] = run.UploadedAt,
            ["Status"] = run.Status.ToString(),
            ["SourceBlobPath"] = run.SourceBlobPath
        };

        if (!string.IsNullOrWhiteSpace(run.OriginalFileName))
        {
            entity["OriginalFileName"] = run.OriginalFileName;
        }

        if (run.CompletedAt is not null)
        {
            entity["CompletedAt"] = run.CompletedAt;
        }

        if (run.Summary is not null)
        {
            entity["SubscriptionItemsExtracted"] = run.Summary.SubscriptionItemsExtracted;
            entity["ProductsExtracted"] = run.Summary.ProductsExtracted;
            entity["PricesExtracted"] = run.Summary.PricesExtracted;
        }

        if (!string.IsNullOrWhiteSpace(run.ResultManifestBlobPath))
        {
            entity["ManifestBlobPath"] = run.ResultManifestBlobPath;
        }

        if (!string.IsNullOrWhiteSpace(run.FailureReason))
        {
            entity["FailureReason"] = run.FailureReason;
        }

        return entity;
    }

    private static StripeCsvIngestionRun FromStripeCsvEntity(TableEntity entity)
    {
        var ingestionId = Guid.Parse(entity.GetString("IngestionId")!);
        Enum.TryParse(entity.GetString("SourceKind"), out ImportSourceKind sourceKind);
        Enum.TryParse(entity.GetString("Status"), out IngestionRunStatus status);

        StripeCsvIngestionSummary? summary = null;
        if (entity.TryGetValue("SubscriptionItemsExtracted", out _))
        {
            summary = new StripeCsvIngestionSummary
            {
                SubscriptionItemsExtracted = entity.GetInt32("SubscriptionItemsExtracted") ?? 0,
                ProductsExtracted = entity.GetInt32("ProductsExtracted") ?? 0,
                PricesExtracted = entity.GetInt32("PricesExtracted") ?? 0
            };
        }

        return new StripeCsvIngestionRun
        {
            IngestionId = ingestionId,
            SourceKind = sourceKind,
            OriginalFileName = entity.GetString("OriginalFileName"),
            ContentFingerprint = entity.GetString("ContentFingerprint") ?? string.Empty,
            UploadedAt = entity.GetDateTimeOffset("UploadedAt") ?? DateTimeOffset.MinValue,
            CompletedAt = entity.GetDateTimeOffset("CompletedAt"),
            Status = status,
            Summary = summary,
            SourceBlobPath = entity.GetString("SourceBlobPath") ?? string.Empty,
            ResultManifestBlobPath = entity.GetString("ManifestBlobPath"),
            FailureReason = entity.GetString("FailureReason")
        };
    }
}
