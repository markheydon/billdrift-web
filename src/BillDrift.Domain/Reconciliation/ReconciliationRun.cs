using BillDrift.Domain.Common;

namespace BillDrift.Domain.Reconciliation;

/// <summary>
/// Immutable result of a deterministic reconciliation comparing supplier cost, subscription truth, intended pricing, and Stripe billing.
/// Produces match groups, mismatches, and proposed Stripe corrections for operator review.
/// </summary>
/// <param name="Id">Unique identifier for this reconciliation run.</param>
/// <param name="ExecutedAt">Timestamp when the run completed.</param>
/// <param name="Scope">Billing period boundary for the reconciliation.</param>
/// <param name="Inputs">Immutable snapshot of normalized inputs compared in this run.</param>
/// <param name="MatchGroups">Cross-domain entity groupings representing the same customer product.</param>
/// <param name="Mismatches">Detected drifts ordered deterministically for operator review.</param>
/// <param name="ProposedChanges">Corrective Stripe actions proposed to resolve mismatches.</param>
public sealed record ReconciliationRun(
    RunId Id,
    DateTimeOffset ExecutedAt,
    BillingPeriod Scope,
    ReconciliationInputs Inputs,
    IReadOnlyList<EntityMatchGroup> MatchGroups,
    IReadOnlyList<Mismatch> Mismatches,
    IReadOnlyList<ProposedChange> ProposedChanges);
