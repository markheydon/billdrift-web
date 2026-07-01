using BillDrift.Domain.Common;

namespace BillDrift.Domain.Reconciliation;

public sealed record ReconciliationRun(
    RunId Id,
    DateTimeOffset ExecutedAt,
    BillingPeriod Scope,
    ReconciliationInputs Inputs,
    IReadOnlyList<EntityMatchGroup> MatchGroups,
    IReadOnlyList<Mismatch> Mismatches,
    IReadOnlyList<ProposedChange> ProposedChanges);
