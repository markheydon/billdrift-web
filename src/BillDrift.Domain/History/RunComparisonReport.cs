using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.History;

/// <summary>Per-domain input change summary between two runs.</summary>
public sealed record InputChangeSummary(
    InputDomainType Domain,
    int EarlierRecordCount,
    int LaterRecordCount,
    string? EarlierFingerprint,
    string? LaterFingerprint,
    bool FingerprintChanged);

/// <summary>Mismatch appearing in a single run during comparison.</summary>
public sealed record ComparedMismatch(
    StableMismatchKey StableKey,
    Mismatch Mismatch,
    RunId RunId);

/// <summary>Mismatch persisting across two runs.</summary>
public sealed record PersistingMismatch(
    StableMismatchKey StableKey,
    Mismatch EarlierMismatch,
    Mismatch LaterMismatch,
    bool ValuesChanged,
    string? ApprovalStatusSummary = null,
    bool MayBeMappingDriven = false);

/// <summary>Exception delta classification between two runs.</summary>
public sealed record ExceptionDeltaReport(
    IReadOnlyList<ComparedMismatch> NewExceptions,
    IReadOnlyList<ComparedMismatch> ResolvedExceptions,
    IReadOnlyList<PersistingMismatch> PersistingExceptions);

/// <summary>Proposal changes between two runs (summary only in v1).</summary>
public sealed record ProposalDeltaReport(
    int EarlierProposalCount,
    int LaterProposalCount);

/// <summary>Structured month-to-month comparison report.</summary>
public sealed record RunComparisonReport(
    RunId EarlierRunId,
    RunId LaterRunId,
    DateTimeOffset GeneratedAt,
    bool MappingVersionChanged,
    IReadOnlyList<InputChangeSummary> InputChangeSummaries,
    ExceptionDeltaReport ExceptionDeltas,
    ProposalDeltaReport ProposalDeltas);
