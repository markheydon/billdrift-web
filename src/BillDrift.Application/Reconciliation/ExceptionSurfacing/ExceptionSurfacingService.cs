using BillDrift.Application.Reconciliation.ExceptionSurfacing.Phases;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing;

/// <summary>
/// Transforms a completed <see cref="ReconciliationRun"/> into a UI-ready exception view model.
/// </summary>
public sealed class ExceptionSurfacingService
{
    private readonly CollectPhase _collect = new();
    private readonly SuppressPhase _suppress = new();
    private readonly ConsolidatePhase _consolidate = new();
    private readonly FinalizePhase _finalize = new();

    /// <summary>
    /// Surfaces operator-facing exceptions from a reconciliation run through collect → suppress → consolidate → finalize.
    /// </summary>
    /// <param name="run">Completed reconciliation run (immutable).</param>
    /// <param name="options">Optional scope flags for derived detection; defaults applied when null.</param>
    /// <returns>Deterministic view model except for <see cref="ReconciliationExceptionViewModel.GeneratedAt"/>.</returns>
    public ReconciliationExceptionViewModel Surface(ReconciliationRun run, ReconciliationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(run);

        var context = new SurfacingContext(run, options);
        _collect.Execute(context);
        _suppress.Execute(context);
        _consolidate.Execute(context);
        return _finalize.Execute(context);
    }
}
