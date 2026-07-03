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
}
