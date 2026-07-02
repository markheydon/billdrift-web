using BillDrift.Application.Reconciliation.Indexing;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing;

/// <summary>Mutable workspace for a single Surface invocation.</summary>
public sealed class SurfacingContext
{
    /// <summary>Source reconciliation run.</summary>
    public ReconciliationRun Run { get; }

    /// <summary>Options passed through for scope-aware derived detection.</summary>
    public ReconciliationOptions Options { get; }

    /// <summary>Match groups indexed by ID.</summary>
    public IReadOnlyDictionary<MatchGroupId, EntityMatchGroup> MatchGroupById { get; }

    /// <summary>Mismatches indexed by ID.</summary>
    public IReadOnlyDictionary<MismatchId, Mismatch> MismatchById { get; }

    /// <summary>Proposed changes indexed by backing mismatch ID.</summary>
    public IReadOnlyDictionary<MismatchId, ProposedChange> ProposedChangeByMismatchId { get; }

    /// <summary>Stripe catalogue index rebuilt from run inputs for subdivision and evidence.</summary>
    public StripeCatalogueIndex CatalogueIndex { get; }

    /// <summary>Lookup from entity IDs to owning match group.</summary>
    public IReadOnlyDictionary<Guid, MatchGroupId> EntityToMatchGroup { get; }

    /// <summary>Surfaced exception candidates (mutable during pipeline).</summary>
    public List<SurfacedException> Candidates { get; } = [];

    /// <summary>Audit trail of suppressions applied during the suppress phase.</summary>
    public List<SuppressionRecord> Suppressed { get; } = [];

    /// <summary>
    /// Creates a surfacing context from a completed reconciliation run.
    /// </summary>
    public SurfacingContext(ReconciliationRun run, ReconciliationOptions? options)
    {
        Run = run ?? throw new ArgumentNullException(nameof(run));
        Options = options ?? new ReconciliationOptions();

        MatchGroupById = run.MatchGroups.ToDictionary(g => g.Id);
        MismatchById = run.Mismatches.ToDictionary(m => m.Id);
        ProposedChangeByMismatchId = run.ProposedChanges.ToDictionary(p => p.MismatchId);
        CatalogueIndex = StripeCatalogueIndex.Build(run.Inputs.StripeItems);
        EntityToMatchGroup = BuildEntityIndex(run.MatchGroups);
    }

    /// <summary>Resolves the match group for a mismatch via involved entity references.</summary>
    public EntityMatchGroup? FindMatchGroup(Mismatch mismatch)
    {
        var refs = mismatch.InvolvedEntityIds;
        if (refs.SupplierCostLineId is { } sc && EntityToMatchGroup.TryGetValue(sc.Value, out var g1))
        {
            return MatchGroupById[g1];
        }

        if (refs.SubscriptionLineId is { } sl && EntityToMatchGroup.TryGetValue(sl.Value, out var g2))
        {
            return MatchGroupById[g2];
        }

        if (refs.StripeBillingItemId is { } si && EntityToMatchGroup.TryGetValue(si.Value, out var g3))
        {
            return MatchGroupById[g3];
        }

        if (refs.IntendedPriceId is { } ip && EntityToMatchGroup.TryGetValue(ip.Value, out var g4))
        {
            return MatchGroupById[g4];
        }

        if (mismatch.Customer is not null)
        {
            return Run.MatchGroups.FirstOrDefault(g =>
                g.Customer.MexId == mismatch.Customer.MexId &&
                (mismatch.CommercialKey is null || g.CommercialKey == mismatch.CommercialKey));
        }

        return null;
    }

    /// <summary>Resolves match group ID for a mismatch.</summary>
    public MatchGroupId? FindMatchGroupId(Mismatch mismatch) => FindMatchGroup(mismatch)?.Id;

    private static IReadOnlyDictionary<Guid, MatchGroupId> BuildEntityIndex(
        IReadOnlyList<EntityMatchGroup> groups)
    {
        var index = new Dictionary<Guid, MatchGroupId>();
        foreach (var group in groups)
        {
            if (group.SupplierCostLine is { } sc)
            {
                index[sc.Id.Value] = group.Id;
            }

            if (group.SubscriptionLine is { } sl)
            {
                index[sl.Id.Value] = group.Id;
            }

            if (group.IntendedPrice is { } ip)
            {
                index[ip.Id.Value] = group.Id;
            }

            if (group.StripeItem is { } si)
            {
                index[si.Id.Value] = group.Id;
            }
        }

        return index;
    }
}

/// <summary>Audit record for a suppressed exception candidate.</summary>
public sealed record SuppressionRecord(
    MismatchId? MismatchId,
    SuppressionRule Rule,
    string Reason);
