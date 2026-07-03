using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.History;

/// <summary>Aggregated recurring mismatch trend across multiple runs.</summary>
public sealed record DriftTrendEntry(
    StableMismatchKey StableKey,
    MexId? CustomerMexId,
    CommercialKeyRoot? CommercialKeyRoot,
    MismatchType MismatchType,
    int OccurrenceCount,
    RunId FirstSeenRunId,
    RunId LastSeenRunId,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    bool IsRecurring,
    string? ProposalDecisionSummary = null);
