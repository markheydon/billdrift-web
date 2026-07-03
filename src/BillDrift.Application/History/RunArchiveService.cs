using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using Microsoft.Extensions.Options;

namespace BillDrift.Application.History;

/// <summary>Archives completed reconciliation runs to Azure storage.</summary>
public sealed class RunArchiveService(
    IRunHistoryStore store,
    IRunBlobArchiveStore blobStore,
    StableMismatchKeyFactory keyFactory,
    IOptions<RunHistoryOptions> options)
{
    /// <summary>Persists a reconciliation run as an immutable archive record.</summary>
    public async Task<ReconciliationRunRecord> PersistAsync(
        PersistRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runId = request.Run.Id;
        var existing = await store.GetRunAsync(runId, cancellationToken);
        if (existing is not null)
        {
            // Every archived run attempt is immutable regardless of status. A prior
            // InProgress or Failed record must never be overwritten — retries must be
            // performed through an explicit re-run producing a new RunId.
            throw new RunAlreadyArchivedException(runId);
        }

        var startedEvent = new RunHistoryAuditEvent(
            Guid.NewGuid(),
            RunHistoryAuditEventType.RunArchiveStarted,
            runId,
            DateTimeOffset.UtcNow,
            $"Archive started for run {runId.Value}",
            request.Context.InitiatorId);

        await store.AppendAuditEventAsync(startedEvent, cancellationToken);

        try
        {
            var inProgress = BuildRecord(
                request,
                RunArchiveStatus.InProgress,
                inputSnapshots: BuildPlaceholderSnapshots(request.Context),
                summary: EmptySummary(),
                failureReason: null,
                completedAt: null);

            await store.UpsertRunAsync(inProgress, cancellationToken);

            var writeResult = await blobStore.WriteRunArchiveAsync(request.Run, request.Context, cancellationToken);
            await store.UpsertInputMetadataAsync(runId, writeResult.InputSnapshots, cancellationToken);

            var driftRows = request.Run.Mismatches
                .Select(m => new DriftIndexEntry(
                    keyFactory.Create(m),
                    runId,
                    m.Customer?.MexId,
                    m.CommercialKey is not null
                        ? CommercialKeyRoot.Create(m.CommercialKey.Value.OfferId, m.CommercialKey.Value.SkuId)
                        : null,
                    m.Type,
                    m.Severity,
                    m.Id,
                    request.Run.ExecutedAt,
                    Truncate(m.Description, 256)))
                .ToList();

            await store.UpsertDriftIndexRowsAsync(runId, request.Run.ExecutedAt, driftRows, cancellationToken);

            var retentionMonths = options.Value.DefaultRetentionMonths;
            var completedAt = request.Run.ExecutedAt;
            var record = BuildRecord(
                request,
                RunArchiveStatus.Completed,
                writeResult.InputSnapshots,
                writeResult.SummaryMetrics,
                failureReason: null,
                completedAt,
                writeResult.ManifestBlobPath,
                retentionExpiresAt: completedAt.AddMonths(retentionMonths));

            await store.UpsertRunAsync(record, cancellationToken);

            await store.AppendAuditEventAsync(
                new RunHistoryAuditEvent(
                    Guid.NewGuid(),
                    RunHistoryAuditEventType.RunArchived,
                    runId,
                    DateTimeOffset.UtcNow,
                    $"Run {runId.Value} archived successfully",
                    request.Context.InitiatorId),
                cancellationToken);

            return record;
        }
        catch (Exception ex) when (ex is not RunAlreadyArchivedException)
        {
            var failed = BuildRecord(
                request,
                RunArchiveStatus.Failed,
                BuildPlaceholderSnapshots(request.Context),
                EmptySummary(),
                ex.Message,
                completedAt: DateTimeOffset.UtcNow);

            await store.UpsertRunAsync(failed, cancellationToken);

            await store.AppendAuditEventAsync(
                new RunHistoryAuditEvent(
                    Guid.NewGuid(),
                    RunHistoryAuditEventType.RunArchiveFailed,
                    runId,
                    DateTimeOffset.UtcNow,
                    $"Archive failed: {ex.Message}",
                    request.Context.InitiatorId),
                cancellationToken);

            throw;
        }
    }

    private static ReconciliationRunRecord BuildRecord(
        PersistRunRequest request,
        RunArchiveStatus status,
        IReadOnlyList<InputSnapshotMetadata> inputSnapshots,
        RunSummaryMetrics summary,
        string? failureReason,
        DateTimeOffset? completedAt,
        string? manifestPath = null,
        DateTimeOffset? retentionExpiresAt = null) =>
        new(
            request.Run.Id,
            status,
            request.Run.Scope,
            request.Context.StartedAt,
            completedAt,
            request.Context.InitiatorId,
            request.Context.MappingVersion,
            inputSnapshots,
            summary,
            manifestPath ?? $"{request.Run.Id.Value}/manifest.json",
            failureReason,
            RetentionExpiresAt: retentionExpiresAt);

    private static IReadOnlyList<InputSnapshotMetadata> BuildPlaceholderSnapshots(RunArchiveContext context) =>
        Enum.GetValues<InputDomainType>()
            .Select(domain => context.InputMetadata.TryGetValue(domain, out var meta)
                ? meta with { Domain = domain }
                : new InputSnapshotMetadata(domain, IsPresent: false))
            .ToList();

    private static RunSummaryMetrics EmptySummary() =>
        new(0, 0, new Dictionary<string, int>(), 0, CleanRun: true);

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
