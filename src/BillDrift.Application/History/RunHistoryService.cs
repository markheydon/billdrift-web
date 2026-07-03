using BillDrift.Application.Approval;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;
using Microsoft.Extensions.Options;

namespace BillDrift.Application.History;

/// <summary>Read and analysis operations over archived reconciliation runs.</summary>
public sealed class RunHistoryService(
    IRunHistoryStore store,
    IRunBlobArchiveStore blobStore,
    IApprovalStore approvalStore,
    RunComparisonService comparisonService,
    DriftTrendAnalyzer driftTrendAnalyzer,
    PricingDriftAnalyzer pricingDriftAnalyzer,
    IOptions<RunHistoryOptions> options)
{
    /// <summary>Lists archived runs matching the filter criteria.</summary>
    public async Task<RunHistoryListResponse> ListRunsAsync(
        RunHistoryListFilter filter,
        int pageSize = 50,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, nextToken) = await store.ListRunsAsync(filter, pageSize, continuationToken, cancellationToken);
        return new RunHistoryListResponse(
            items.Select(ToListItem).ToList(),
            nextToken);
    }

    /// <summary>Gets run summary from table storage only.</summary>
    public Task<ReconciliationRunRecord?> GetRunSummaryAsync(RunId runId, CancellationToken cancellationToken = default) =>
        store.GetRunAsync(runId, cancellationToken);

    /// <summary>Gets full run detail with optional results and approval links.</summary>
    public async Task<RunDetailViewModel> GetRunDetailAsync(
        RunId runId,
        bool includeResults = false,
        bool includeMatchGroups = false,
        CancellationToken cancellationToken = default)
    {
        var record = await store.GetRunAsync(runId, cancellationToken)
            ?? throw new RunNotFoundException(runId);

        await blobStore.VerifyManifestIntegrityAsync(runId, cancellationToken);

        RunResultsSnapshot? results = null;
        if (includeResults || includeMatchGroups)
        {
            results = await blobStore.LoadResultsSnapshotAsync(runId, cancellationToken);
            if (!includeMatchGroups && results is not null)
            {
                results = results with { MatchGroups = [] };
            }
        }

        var proposals = await approvalStore.ListProposalsByRunAsync(runId, cancellationToken);
        var links = MapProposalLinks(proposals);

        return new RunDetailViewModel(
            record.RunId,
            record.Status,
            record.BillingPeriodScope,
            record.StartedAt,
            record.CompletedAt,
            record.InitiatorId,
            record.MappingVersion,
            record.InputSnapshots,
            record.SummaryMetrics,
            links,
            [],
            results);
    }

    /// <summary>Compares two stored runs.</summary>
    public async Task<RunComparisonReport> CompareRunsAsync(
        CompareRunsRequest request,
        string? operatorId = null,
        CancellationToken cancellationToken = default)
    {
        var earlier = await store.GetRunAsync(request.EarlierRunId, cancellationToken)
            ?? throw new RunNotFoundException(request.EarlierRunId);
        var later = await store.GetRunAsync(request.LaterRunId, cancellationToken)
            ?? throw new RunNotFoundException(request.LaterRunId);

        if (earlier.Status != RunArchiveStatus.Completed || later.Status != RunArchiveStatus.Completed)
        {
            throw new RunsNotComparableException("Both runs must be completed to compare.");
        }

        var earlierResults = await blobStore.LoadResultsSnapshotAsync(request.EarlierRunId, cancellationToken);
        var laterResults = await blobStore.LoadResultsSnapshotAsync(request.LaterRunId, cancellationToken);
        var earlierInputs = await store.GetInputMetadataAsync(request.EarlierRunId, cancellationToken);
        var laterInputs = await store.GetInputMetadataAsync(request.LaterRunId, cancellationToken);

        var report = comparisonService.Compare(
            request.EarlierRunId,
            request.LaterRunId,
            earlierResults,
            laterResults,
            earlier.MappingVersion,
            later.MappingVersion,
            earlierInputs,
            laterInputs,
            request.IncludeInputDeltas);

        await store.AppendAuditEventAsync(
            new RunHistoryAuditEvent(
                Guid.NewGuid(),
                RunHistoryAuditEventType.RunCompared,
                request.LaterRunId,
                DateTimeOffset.UtcNow,
                $"Compared {request.EarlierRunId.Value} vs {request.LaterRunId.Value}",
                operatorId),
            cancellationToken);

        return report;
    }

    /// <summary>Gets recurring drift trends for a time window.</summary>
    public async Task<DriftTrendsResponse> GetDriftTrendsAsync(
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        int minOccurrences = 2,
        MismatchType? mismatchType = null,
        string? customerMexId = null,
        string? operatorId = null,
        CancellationToken cancellationToken = default)
    {
        var entries = await store.QueryDriftIndexAsync(fromDate, toDate, cancellationToken);
        var trends = driftTrendAnalyzer.Analyze(entries, minOccurrences, mismatchType, customerMexId);

        await store.AppendAuditEventAsync(
            new RunHistoryAuditEvent(
                Guid.NewGuid(),
                RunHistoryAuditEventType.DriftTrendsViewed,
                RunId.New(),
                DateTimeOffset.UtcNow,
                $"Drift trends viewed {fromDate:u} to {toDate:u}",
                operatorId),
            cancellationToken);

        return new DriftTrendsResponse(trends, fromDate, toDate);
    }

    /// <summary>Gets pricing drift timeline for a commercial key.</summary>
    public async Task<PricingDriftTimelineResponse> GetPricingDriftTimelineAsync(
        CommercialKey commercialKey,
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        CancellationToken cancellationToken = default)
    {
        var filter = new RunHistoryListFilter(FromDate: fromDate, ToDate: toDate, Status: RunArchiveStatus.Completed);
        var runs = new List<PricingDriftAnalyzer.PricingRunSnapshot>();

        string? token = null;
        do
        {
            var (items, next) = await store.ListRunsAsync(filter, 100, token, cancellationToken);
            foreach (var item in items.OrderBy(r => r.CompletedAt))
            {
                if (item.CompletedAt is null)
                {
                    continue;
                }

                try
                {
                    var intendedJson = await blobStore.LoadInputBlobAsync(item.RunId, InputDomainType.IntendedPricing, cancellationToken);
                    var stripeJson = await blobStore.LoadInputBlobAsync(item.RunId, InputDomainType.StripeBilling, cancellationToken);
                    runs.Add(new PricingDriftAnalyzer.PricingRunSnapshot(
                        item.RunId,
                        item.CompletedAt.Value,
                        PricingDriftAnalyzer.DeserializeIntendedPrices(intendedJson),
                        PricingDriftAnalyzer.DeserializeStripeItems(stripeJson)));
                }
                catch (RunArchiveIntegrityException)
                {
                    // Skip corrupted runs in timeline
                }
            }

            token = next;
        }
        while (token is not null);

        var entries = pricingDriftAnalyzer.Analyze(commercialKey, runs);
        return new PricingDriftTimelineResponse(commercialKey, entries);
    }

    /// <summary>Gets audit events for a run.</summary>
    public async Task<RunAuditResponse> GetAuditEventsAsync(RunId runId, CancellationToken cancellationToken = default)
    {
        var events = await store.ListAuditEventsAsync(runId, cancellationToken);
        return new RunAuditResponse(events);
    }

    /// <summary>Exports a comparison report to blob storage.</summary>
    public async Task<ComparisonExportResponse> ExportComparisonAsync(
        CompareRunsRequest request,
        RunId exportRunId,
        string? operatorId = null,
        CancellationToken cancellationToken = default)
    {
        var report = await CompareRunsAsync(request, operatorId, cancellationToken);
        var (path, hash) = await blobStore.ExportComparisonReportAsync(exportRunId, report, cancellationToken);

        await store.AppendAuditEventAsync(
            new RunHistoryAuditEvent(
                Guid.NewGuid(),
                RunHistoryAuditEventType.RunHistoryExported,
                exportRunId,
                DateTimeOffset.UtcNow,
                $"Exported comparison to {path}",
                operatorId),
            cancellationToken);

        return new ComparisonExportResponse(path, hash);
    }

    /// <summary>Reconstructs a reconciliation run from archived blob snapshots for approval ingest and API reads.</summary>
    public async Task<ReconciliationRun?> LoadArchivedReconciliationRunAsync(
        RunId runId,
        CancellationToken cancellationToken = default)
    {
        var record = await store.GetRunAsync(runId, cancellationToken);
        if (record is null || record.Status != RunArchiveStatus.Completed)
        {
            return null;
        }

        await blobStore.VerifyManifestIntegrityAsync(runId, cancellationToken);
        var snapshot = await blobStore.LoadResultsSnapshotAsync(runId, cancellationToken);

        return new ReconciliationRun(
            runId,
            record.CompletedAt ?? record.StartedAt,
            record.BillingPeriodScope,
            new ReconciliationInputs([], [], [], [], []),
            snapshot.MatchGroups,
            snapshot.Mismatches,
            snapshot.ProposedChanges);
    }

    /// <summary>Applies retention policy stub marking runs past retention.</summary>
    public async Task ApplyRetentionPolicyAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMonths(-options.Value.DefaultRetentionMonths);
        var filter = new RunHistoryListFilter(Status: RunArchiveStatus.Completed, IncludeArchived: false);
        string? token = null;

        do
        {
            var (items, next) = await store.ListRunsAsync(filter, 100, token, cancellationToken);
            foreach (var item in items.Where(r => r.CompletedAt < cutoff && !r.IsArchived))
            {
                var archived = item with
                {
                    IsArchived = true,
                    ArchivedAt = DateTimeOffset.UtcNow
                };
                await store.UpsertRunAsync(archived, cancellationToken);
            }

            token = next;
        }
        while (token is not null);
    }

    private static RunHistoryListItem ToListItem(ReconciliationRunRecord record) =>
        new(
            record.RunId,
            record.Status,
            record.BillingPeriodScope,
            record.CompletedAt,
            record.InitiatorId,
            record.SummaryMetrics.MismatchCount,
            record.SummaryMetrics.ProposedChangeCount,
            record.SummaryMetrics.CleanRun,
            Enum.GetValues<InputDomainType>().ToDictionary(
                d => d.ToString(),
                d => record.InputSnapshots.FirstOrDefault(s => s.Domain == d)?.IsPresent ?? false),
            record.IsArchived);

    private static IReadOnlyList<ProposalStatusLink> MapProposalLinks(IReadOnlyList<ApprovalProposal> proposals) =>
        proposals
            .Where(p => p.ProposedChangeId is not null)
            .Select(p => new ProposalStatusLink(
                p.ProposedChangeId!.Value,
                p.IdempotencyKey,
                p.State,
                p.LastOperatorId,
                p.LastUpdatedAt,
                p.EligibilityReason,
                p.SupersededByRunId))
            .ToList();
}
