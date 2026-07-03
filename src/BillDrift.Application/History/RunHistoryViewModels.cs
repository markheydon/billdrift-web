using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.History;

/// <summary>Context metadata supplied when archiving a reconciliation run.</summary>
public sealed record RunArchiveContext(
    string? InitiatorId,
    IReadOnlyDictionary<InputDomainType, InputSnapshotMetadata> InputMetadata,
    MappingVersionReference MappingVersion,
    DateTimeOffset StartedAt);

/// <summary>Request to persist a completed reconciliation run.</summary>
public sealed record PersistRunRequest(
    ReconciliationRun Run,
    RunArchiveContext Context);

/// <summary>Filter criteria for listing archived runs.</summary>
public sealed record RunHistoryListFilter(
    DateOnly? BillingPeriodStart = null,
    DateOnly? BillingPeriodEnd = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    RunArchiveStatus? Status = null,
    bool? CleanRunsOnly = null,
    bool IncludeArchived = false);

/// <summary>Run list item for API responses.</summary>
public sealed record RunHistoryListItem(
    RunId RunId,
    RunArchiveStatus Status,
    BillingPeriod BillingPeriod,
    DateTimeOffset? CompletedAt,
    string? InitiatorId,
    int MismatchCount,
    int ProposedChangeCount,
    bool CleanRun,
    IReadOnlyDictionary<string, bool> InputPresence,
    bool IsArchived);

/// <summary>Paginated run list response.</summary>
public sealed record RunHistoryListResponse(
    IReadOnlyList<RunHistoryListItem> Items,
    string? ContinuationToken);

/// <summary>Full run detail view model.</summary>
public sealed record RunDetailViewModel(
    RunId RunId,
    RunArchiveStatus Status,
    BillingPeriod BillingPeriod,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? InitiatorId,
    MappingVersionReference MappingVersion,
    IReadOnlyList<InputSnapshotMetadata> InputSnapshots,
    RunSummaryMetrics SummaryMetrics,
    IReadOnlyList<ProposalStatusLink> ProposalStatusLinks,
    IReadOnlyList<ExecutionOutcome> ExecutionOutcomes,
    RunResultsSnapshot? Results = null);

/// <summary>Drift trends response.</summary>
public sealed record DriftTrendsResponse(
    IReadOnlyList<DriftTrendEntry> Entries,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd);

/// <summary>Pricing drift timeline response.</summary>
public sealed record PricingDriftTimelineResponse(
    CommercialKey CommercialKey,
    IReadOnlyList<PricingDriftTimelineEntry> Entries);

/// <summary>Compare runs request.</summary>
public sealed record CompareRunsRequest(
    RunId EarlierRunId,
    RunId LaterRunId,
    bool IncludeInputDeltas = true,
    bool IncludeProposalStatus = true);

/// <summary>Audit events response.</summary>
public sealed record RunAuditResponse(IReadOnlyList<RunHistoryAuditEvent> Events);

/// <summary>Comparison export response.</summary>
public sealed record ComparisonExportResponse(string ExportBlobPath, string ContentHash);
