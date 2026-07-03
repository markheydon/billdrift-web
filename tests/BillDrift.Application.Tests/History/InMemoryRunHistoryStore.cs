using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BillDrift.Application.History;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.History;

/// <summary>In-memory run history store for unit and service tests.</summary>
public sealed class InMemoryRunHistoryStore : IRunHistoryStore, IRunBlobArchiveStore
{
    private readonly ConcurrentDictionary<Guid, ReconciliationRunRecord> _runs = new();
    private readonly ConcurrentDictionary<string, InputSnapshotMetadata> _inputs = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<DriftIndexEntry> _driftEntries = [];
    private readonly ConcurrentBag<RunHistoryAuditEvent> _auditEvents = [];
    private readonly ConcurrentDictionary<string, string> _blobs = new(StringComparer.Ordinal);
    private readonly StableMismatchKeyFactory _keyFactory = new();

    /// <inheritdoc />
    public Task UpsertRunAsync(ReconciliationRunRecord record, CancellationToken cancellationToken = default)
    {
        _runs[record.RunId.Value] = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ReconciliationRunRecord?> GetRunAsync(RunId runId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_runs.TryGetValue(runId.Value, out var record) ? record : null);

    /// <inheritdoc />
    public Task<(IReadOnlyList<ReconciliationRunRecord> Items, string? ContinuationToken)> ListRunsAsync(
        RunHistoryListFilter filter,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var items = _runs.Values
            .Where(r => filter.Status is null || r.Status == filter.Status)
            .Where(r => filter.IncludeArchived || !r.IsArchived)
            .Where(r => filter.CleanRunsOnly != true || r.SummaryMetrics.CleanRun)
            .Where(r => filter.BillingPeriodStart is null || r.BillingPeriodScope.End >= filter.BillingPeriodStart)
            .Where(r => filter.BillingPeriodEnd is null || r.BillingPeriodScope.Start <= filter.BillingPeriodEnd)
            .Where(r => filter.FromDate is null || r.CompletedAt >= filter.FromDate)
            .Where(r => filter.ToDate is null || r.CompletedAt <= filter.ToDate)
            .OrderByDescending(r => r.CompletedAt)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<(IReadOnlyList<ReconciliationRunRecord>, string?)>((items, null));
    }

    /// <inheritdoc />
    public Task UpsertInputMetadataAsync(
        RunId runId,
        IReadOnlyList<InputSnapshotMetadata> snapshots,
        CancellationToken cancellationToken = default)
    {
        foreach (var snapshot in snapshots)
        {
            _inputs[$"{runId.Value:N}:{snapshot.Domain}"] = snapshot;
        }

        if (_runs.TryGetValue(runId.Value, out var record))
        {
            _runs[runId.Value] = record with { InputSnapshots = snapshots.ToList() };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<InputSnapshotMetadata>> GetInputMetadataAsync(
        RunId runId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InputSnapshotMetadata>>(
            _inputs.Values.Where(i => _runs.ContainsKey(runId.Value)).ToList());

    /// <inheritdoc />
    public Task UpsertDriftIndexRowsAsync(
        RunId runId,
        DateTimeOffset completedAt,
        IReadOnlyList<DriftIndexEntry> rows,
        CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            _driftEntries.Add(row);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DriftIndexEntry>> QueryDriftIndexAsync(
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DriftIndexEntry>>(
            _driftEntries.Where(e => e.CompletedAt >= fromDate && e.CompletedAt <= toDate).ToList());

    /// <inheritdoc />
    public Task AppendAuditEventAsync(RunHistoryAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _auditEvents.Add(auditEvent);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RunHistoryAuditEvent>> ListAuditEventsAsync(
        RunId runId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RunHistoryAuditEvent>>(
            _auditEvents.Where(e => e.RunId == runId).OrderByDescending(e => e.Timestamp).ToList());

    /// <inheritdoc />
    public Task<RunArchiveWriteResult> WriteRunArchiveAsync(
        ReconciliationRun run,
        RunArchiveContext context,
        CancellationToken cancellationToken = default)
    {
        var runPrefix = run.Id.Value.ToString("D");
        var snapshots = new List<InputSnapshotMetadata>();

        WriteInput(runPrefix, InputDomainType.SupplierCost, run.Inputs.SupplierCostLines.Count, context, snapshots);
        WriteInput(runPrefix, InputDomainType.SubscriptionTruth, run.Inputs.SubscriptionLines.Count, context, snapshots);
        WriteInput(runPrefix, InputDomainType.IntendedPricing, run.Inputs.IntendedPrices.Count, context, snapshots);
        WriteInput(runPrefix, InputDomainType.StripeBilling, run.Inputs.StripeItems.Count, context, snapshots);
        WriteInput(runPrefix, InputDomainType.ProductMappings, run.Inputs.ProductMappings.Count, context, snapshots);

        var resultsJson = JsonSerializer.Serialize(new
        {
            matchGroups = run.MatchGroups,
            mismatches = run.Mismatches,
            proposedChanges = run.ProposedChanges
        });

        var hash = Hash(resultsJson);
        _blobs[$"{runPrefix}/results/combined.json"] = resultsJson;
        _blobs[$"{runPrefix}/manifest.json"] = "{}";

        var summary = new RunSummaryMetrics(
            run.MatchGroups.Count,
            run.Mismatches.Count,
            run.Mismatches.GroupBy(m => m.Type.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            run.ProposedChanges.Count,
            run.Mismatches.Count == 0);

        return Task.FromResult(new RunArchiveWriteResult(
            $"{runPrefix}/manifest.json",
            snapshots,
            summary,
            hash));
    }

    /// <inheritdoc />
    public Task<RunResultsSnapshot> LoadResultsSnapshotAsync(RunId runId, CancellationToken cancellationToken = default)
    {
        if (!_runs.TryGetValue(runId.Value, out _))
        {
            throw new RunNotFoundException(runId);
        }

        if (!_blobs.TryGetValue($"{runId.Value:D}/results/combined.json", out var json))
        {
            return Task.FromResult(new RunResultsSnapshot(runId, [], [], [], string.Empty));
        }

        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(new RunResultsSnapshot(
            runId,
            [],
            [],
            [],
            Hash(json)));
    }

    /// <inheritdoc />
    public Task<string> LoadInputBlobAsync(RunId runId, InputDomainType domain, CancellationToken cancellationToken = default)
    {
        var key = $"{runId.Value:D}/inputs/{domain}.json";
        return Task.FromResult(_blobs.TryGetValue(key, out var json) ? json : "{\"records\":[]}");
    }

    /// <inheritdoc />
    public Task VerifyManifestIntegrityAsync(RunId runId, CancellationToken cancellationToken = default)
    {
        if (!_runs.ContainsKey(runId.Value))
        {
            throw new RunNotFoundException(runId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<(string BlobPath, string ContentHash)> ExportComparisonReportAsync(
        RunId runId,
        RunComparisonReport report,
        CancellationToken cancellationToken = default)
    {
        var path = $"{runId.Value:D}/exports/comparison.json";
        var json = JsonSerializer.Serialize(report);
        _blobs[path] = json;
        return Task.FromResult((path, Hash(json)));
    }

    private void WriteInput(
        string runPrefix,
        InputDomainType domain,
        int count,
        RunArchiveContext context,
        List<InputSnapshotMetadata> snapshots)
    {
        var meta = context.InputMetadata.GetValueOrDefault(domain);
        var present = meta?.IsPresent ?? count > 0;
        var path = $"{runPrefix}/inputs/{domain}.json";
        _blobs[path] = "{\"records\":[]}";
        snapshots.Add(new InputSnapshotMetadata(domain, present, meta?.SourceFileName, RecordCount: present ? count : 0, BlobPath: path));
    }

    private static string Hash(string content) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant()}";
}
