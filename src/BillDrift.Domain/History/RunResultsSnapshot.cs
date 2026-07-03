using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.History;

/// <summary>Frozen reconciliation results deserialized from blob storage.</summary>
public sealed record RunResultsSnapshot(
    RunId RunId,
    IReadOnlyList<EntityMatchGroup> MatchGroups,
    IReadOnlyList<Mismatch> Mismatches,
    IReadOnlyList<ProposedChange> ProposedChanges,
    string ContentHash);
