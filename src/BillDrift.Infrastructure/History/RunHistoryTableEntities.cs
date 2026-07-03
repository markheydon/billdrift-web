using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Data.Tables;
using BillDrift.Application.History;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.History;

/// <summary>Azure Table entity mappings for run history persistence.</summary>
internal static class RunHistoryTableEntities
{
    public const string RunPartition = "run";
    public const string InputPartition = "input";
    public const string DriftPartition = "drift";
    public const string AuditPartition = "audit";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string EncodeRunRowKey(DateTimeOffset completedAt, RunId runId) =>
        $"{(DateTimeOffset.MaxValue.Ticks - completedAt.UtcTicks):D19}:{runId.Value:N}";

    public static TableEntity ToRunEntity(ReconciliationRunRecord record)
    {
        var completedAt = record.CompletedAt ?? record.StartedAt;
        var entity = new TableEntity(RunPartition, EncodeRunRowKey(completedAt, record.RunId))
        {
            ["RunId"] = record.RunId.Value.ToString(),
            ["Status"] = record.Status.ToString(),
            ["BillingPeriodStart"] = record.BillingPeriodScope.Start.ToString("O"),
            ["BillingPeriodEnd"] = record.BillingPeriodScope.End.ToString("O"),
            ["StartedAt"] = record.StartedAt,
            ["InitiatorId"] = record.InitiatorId,
            ["MappingVersionId"] = record.MappingVersion.VersionId,
            ["MappingContentHash"] = record.MappingVersion.ContentHash,
            ["MappingEffectiveDate"] = record.MappingVersion.EffectiveDate.ToString("O"),
            ["ManifestBlobPath"] = record.ManifestBlobPath,
            ["MatchGroupCount"] = record.SummaryMetrics.MatchGroupCount,
            ["MismatchCount"] = record.SummaryMetrics.MismatchCount,
            ["ProposedChangeCount"] = record.SummaryMetrics.ProposedChangeCount,
            ["CleanRun"] = record.SummaryMetrics.CleanRun,
            ["MismatchCountByCategoryJson"] = JsonSerializer.Serialize(record.SummaryMetrics.MismatchCountByCategory, JsonOptions),
            ["IsArchived"] = record.IsArchived,
            ["InputPresenceFlags"] = ComputeInputPresenceFlags(record.InputSnapshots)
        };

        if (record.CompletedAt is not null)
        {
            entity["CompletedAt"] = record.CompletedAt;
        }

        if (record.FailureReason is not null)
        {
            entity["FailureReason"] = record.FailureReason;
        }

        if (record.ArchivedAt is not null)
        {
            entity["ArchivedAt"] = record.ArchivedAt;
        }

        if (record.RetentionExpiresAt is not null)
        {
            entity["RetentionExpiresAt"] = record.RetentionExpiresAt;
        }

        return entity;
    }

    public static ReconciliationRunRecord ToRunRecord(TableEntity entity)
    {
        var categoryJson = entity.TryGetValue("MismatchCountByCategoryJson", out var cat) ? cat as string : null;
        var categories = string.IsNullOrEmpty(categoryJson)
            ? new Dictionary<string, int>()
            : JsonSerializer.Deserialize<Dictionary<string, int>>(categoryJson, JsonOptions) ?? [];

        return new ReconciliationRunRecord(
            RunId.FromGuid(Guid.Parse((string)entity["RunId"])),
            Enum.Parse<RunArchiveStatus>((string)entity["Status"]),
            BillingPeriod.Create(
                DateOnly.Parse((string)entity["BillingPeriodStart"]),
                DateOnly.Parse((string)entity["BillingPeriodEnd"])),
            (DateTimeOffset)entity["StartedAt"],
            entity.TryGetValue("CompletedAt", out var completed) ? (DateTimeOffset?)completed : null,
            entity.TryGetValue("InitiatorId", out var initiator) ? initiator as string : null,
            new MappingVersionReference(
                (string)entity["MappingVersionId"],
                (string)entity["MappingContentHash"],
                DateOnly.Parse((string)entity["MappingEffectiveDate"])),
            [],
            new RunSummaryMetrics(
                (int)entity["MatchGroupCount"],
                (int)entity["MismatchCount"],
                categories,
                (int)entity["ProposedChangeCount"],
                (bool)entity["CleanRun"]),
            (string)entity["ManifestBlobPath"],
            entity.TryGetValue("FailureReason", out var reason) ? reason as string : null,
            entity.TryGetValue("IsArchived", out var archived) && archived is bool archivedFlag && archivedFlag,
            entity.TryGetValue("ArchivedAt", out var archivedAt) ? (DateTimeOffset?)archivedAt : null,
            entity.TryGetValue("RetentionExpiresAt", out var retention) ? (DateTimeOffset?)retention : null);
    }

    public static TableEntity ToInputEntity(RunId runId, InputSnapshotMetadata snapshot) =>
        new(InputPartition, $"{runId.Value:N}:{snapshot.Domain}")
        {
            ["RunId"] = runId.Value.ToString(),
            ["Domain"] = snapshot.Domain.ToString(),
            ["IsPresent"] = snapshot.IsPresent,
            ["SourceFileName"] = snapshot.SourceFileName,
            ["UploadedAt"] = snapshot.UploadedAt,
            ["ContentFingerprint"] = snapshot.ContentFingerprint,
            ["BillingPeriodStart"] = snapshot.BillingPeriodScope?.Start.ToString("O"),
            ["BillingPeriodEnd"] = snapshot.BillingPeriodScope?.End.ToString("O"),
            ["RecordCount"] = snapshot.RecordCount,
            ["BlobPath"] = snapshot.BlobPath,
            ["ContentHash"] = snapshot.ContentHash
        };

    public static InputSnapshotMetadata ToInputMetadata(TableEntity entity)
    {
        BillingPeriod? period = null;
        if (entity.TryGetValue("BillingPeriodStart", out var start) && start is string startText &&
            entity.TryGetValue("BillingPeriodEnd", out var end) && end is string endText)
        {
            period = BillingPeriod.Create(DateOnly.Parse(startText), DateOnly.Parse(endText));
        }

        return new InputSnapshotMetadata(
            Enum.Parse<InputDomainType>((string)entity["Domain"]),
            (bool)entity["IsPresent"],
            entity.TryGetValue("SourceFileName", out var file) ? file as string : null,
            entity.TryGetValue("UploadedAt", out var uploaded) ? (DateTimeOffset?)uploaded : null,
            entity.TryGetValue("ContentFingerprint", out var fp) ? fp as string : null,
            period,
            (int)entity["RecordCount"],
            entity.TryGetValue("BlobPath", out var path) ? path as string : null,
            entity.TryGetValue("ContentHash", out var hash) ? hash as string : null);
    }

    public static TableEntity ToDriftEntity(DriftIndexEntry entry) =>
        new(DriftPartition, $"{HashPrefix(entry.StableKey.Value)}:{entry.RunId.Value:N}")
        {
            ["RunId"] = entry.RunId.Value.ToString(),
            ["StableMismatchKey"] = entry.StableKey.Value,
            ["StableKeyHash"] = HashPrefix(entry.StableKey.Value),
            ["CustomerMexId"] = entry.CustomerMexId?.Value,
            ["CommercialKeyRootJson"] = entry.CommercialKeyRoot is not null
                ? JsonSerializer.Serialize(new CommercialKeyRootDto(
                    entry.CommercialKeyRoot.Value.OfferId.Value,
                    entry.CommercialKeyRoot.Value.SkuId.Value), JsonOptions)
                : null,
            ["MismatchType"] = entry.MismatchType.ToString(),
            ["Severity"] = entry.Severity.ToString(),
            ["MismatchId"] = entry.MismatchId.Value.ToString(),
            ["CompletedAt"] = entry.CompletedAt,
            ["DescriptionSummary"] = entry.DescriptionSummary
        };

    public static DriftIndexEntry ToDriftEntry(TableEntity entity)
    {
        CommercialKeyRoot? root = null;
        if (entity.TryGetValue("CommercialKeyRootJson", out var rootJson) && rootJson is string rootText)
        {
            var parsed = JsonSerializer.Deserialize<CommercialKeyRootDto>(rootText, JsonOptions);
            if (parsed is not null)
            {
                root = CommercialKeyRoot.Create(OfferId.Create(parsed.OfferId), SkuId.Create(parsed.SkuId));
            }
        }

        return new DriftIndexEntry(
            StableMismatchKey.Create((string)entity["StableMismatchKey"]),
            RunId.FromGuid(Guid.Parse((string)entity["RunId"])),
            entity.TryGetValue("CustomerMexId", out var mex) && mex is string mexText ? MexId.Create(mexText) : null,
            root,
            Enum.Parse<MismatchType>((string)entity["MismatchType"]),
            Enum.Parse<MismatchSeverity>((string)entity["Severity"]),
            MismatchId.FromGuid(Guid.Parse((string)entity["MismatchId"])),
            (DateTimeOffset)entity["CompletedAt"],
            (string)entity["DescriptionSummary"]);
    }

    public static TableEntity ToAuditEntity(RunHistoryAuditEvent auditEvent) =>
        new(AuditPartition, $"{auditEvent.RunId.Value:N}:{auditEvent.Timestamp.UtcTicks:D19}:{auditEvent.EventId:N}")
        {
            ["EventType"] = auditEvent.EventType.ToString(),
            ["RunId"] = auditEvent.RunId.Value.ToString(),
            ["OperatorId"] = auditEvent.OperatorId,
            ["Timestamp"] = auditEvent.Timestamp,
            ["Summary"] = auditEvent.Summary,
            ["PayloadJson"] = auditEvent.PayloadJson
        };

    public static RunHistoryAuditEvent ToAuditEvent(TableEntity entity) =>
        new(
            Guid.Parse(entity.RowKey.Split(':')[^1]),
            Enum.Parse<RunHistoryAuditEventType>((string)entity["EventType"]),
            RunId.FromGuid(Guid.Parse((string)entity["RunId"])),
            (DateTimeOffset)entity["Timestamp"],
            (string)entity["Summary"],
            entity.TryGetValue("OperatorId", out var op) ? op as string : null,
            entity.TryGetValue("PayloadJson", out var payload) ? payload as string : null);

    private static int ComputeInputPresenceFlags(IReadOnlyList<InputSnapshotMetadata> snapshots)
    {
        var flags = 0;
        foreach (var snapshot in snapshots.Where(s => s.IsPresent))
        {
            flags |= 1 << (int)snapshot.Domain;
        }

        return flags;
    }

    private static string HashPrefix(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();

    private sealed record CommercialKeyRootDto(string OfferId, string SkuId);
}

/// <summary>Azure Table Storage implementation for run history index.</summary>
public sealed class AzureTableRunHistoryStore : IRunHistoryStore
{
    private readonly TableClient _tableClient;
    private bool _tableEnsured;

    /// <summary>Creates a store using an Aspire-injected table service client.</summary>
    public AzureTableRunHistoryStore(TableServiceClient tableServiceClient, IOptions<RunHistoryStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(tableServiceClient);
        _tableClient = tableServiceClient.GetTableClient(options.Value.TableName);
    }

    /// <inheritdoc />
    public async Task UpsertRunAsync(ReconciliationRunRecord record, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.UpsertEntityAsync(RunHistoryTableEntities.ToRunEntity(record), TableUpdateMode.Replace, cancellationToken);

        if (record.InputSnapshots.Count > 0)
        {
            await UpsertInputMetadataAsync(record.RunId, record.InputSnapshots, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<ReconciliationRunRecord?> GetRunAsync(RunId runId, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter = $"PartitionKey eq '{RunHistoryTableEntities.RunPartition}' and RunId eq '{runId.Value}'";

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            var record = RunHistoryTableEntities.ToRunRecord(entity);
            var inputs = await GetInputMetadataAsync(runId, cancellationToken);
            return record with { InputSnapshots = inputs };
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ReconciliationRunRecord> Items, string? ContinuationToken)> ListRunsAsync(
        RunHistoryListFilter filter,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var conditions = new List<string> { $"PartitionKey eq '{RunHistoryTableEntities.RunPartition}'" };

        if (filter.Status is not null)
        {
            conditions.Add($"Status eq '{filter.Status}'");
        }

        if (filter.BillingPeriodStart is not null)
        {
            conditions.Add($"BillingPeriodEnd ge '{filter.BillingPeriodStart.Value:O}'");
        }

        if (filter.BillingPeriodEnd is not null)
        {
            conditions.Add($"BillingPeriodStart le '{filter.BillingPeriodEnd.Value:O}'");
        }

        if (!filter.IncludeArchived)
        {
            conditions.Add("IsArchived eq false");
        }

        if (filter.CleanRunsOnly == true)
        {
            conditions.Add("CleanRun eq true");
        }

        var odataFilter = string.Join(" and ", conditions);
        var results = new List<ReconciliationRunRecord>();
        string? nextToken = null;
        var count = 0;

        await foreach (var page in _tableClient.QueryAsync<TableEntity>(odataFilter, cancellationToken: cancellationToken).AsPages(continuationToken, pageSize))
        {
            foreach (var entity in page.Values)
            {
                var record = RunHistoryTableEntities.ToRunRecord(entity);
                if (filter.FromDate is not null && record.CompletedAt < filter.FromDate)
                {
                    continue;
                }

                if (filter.ToDate is not null && record.CompletedAt > filter.ToDate)
                {
                    continue;
                }

                results.Add(record);
                count++;
            }

            nextToken = page.ContinuationToken;
            break;
        }

        return (results, nextToken);
    }

    /// <inheritdoc />
    public async Task UpsertInputMetadataAsync(
        RunId runId,
        IReadOnlyList<InputSnapshotMetadata> snapshots,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        foreach (var snapshot in snapshots)
        {
            await _tableClient.UpsertEntityAsync(
                RunHistoryTableEntities.ToInputEntity(runId, snapshot),
                TableUpdateMode.Replace,
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InputSnapshotMetadata>> GetInputMetadataAsync(
        RunId runId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter = $"PartitionKey eq '{RunHistoryTableEntities.InputPartition}' and RunId eq '{runId.Value}'";
        var results = new List<InputSnapshotMetadata>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            results.Add(RunHistoryTableEntities.ToInputMetadata(entity));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task UpsertDriftIndexRowsAsync(
        RunId runId,
        DateTimeOffset completedAt,
        IReadOnlyList<DriftIndexEntry> rows,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        foreach (var row in rows)
        {
            await _tableClient.UpsertEntityAsync(
                RunHistoryTableEntities.ToDriftEntity(row),
                TableUpdateMode.Replace,
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DriftIndexEntry>> QueryDriftIndexAsync(
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter = $"PartitionKey eq '{RunHistoryTableEntities.DriftPartition}' and CompletedAt ge datetime'{fromDate.UtcDateTime:O}' and CompletedAt le datetime'{toDate.UtcDateTime:O}'";
        var results = new List<DriftIndexEntry>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            results.Add(RunHistoryTableEntities.ToDriftEntry(entity));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task AppendAuditEventAsync(RunHistoryAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.AddEntityAsync(RunHistoryTableEntities.ToAuditEntity(auditEvent), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RunHistoryAuditEvent>> ListAuditEventsAsync(
        RunId runId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter = $"PartitionKey eq '{RunHistoryTableEntities.AuditPartition}' and RunId eq '{runId.Value}'";
        var results = new List<RunHistoryAuditEvent>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            results.Add(RunHistoryTableEntities.ToAuditEvent(entity));
        }

        return results.OrderByDescending(e => e.Timestamp).ToList();
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
}
