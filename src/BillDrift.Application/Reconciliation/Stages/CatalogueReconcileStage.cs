using BillDrift.Application.Reconciliation.Detection;

namespace BillDrift.Application.Reconciliation.Stages;

/// <summary>
/// Detects catalogue gaps and price drift for required commercial keys.
/// </summary>
public sealed class CatalogueReconcileStage : IReconciliationStage
{
    private readonly MismatchDetector _detector;

    /// <summary>
    /// Creates a catalogue reconcile stage with mismatch detector.
    /// </summary>
    public CatalogueReconcileStage(MismatchDetector detector)
    {
        _detector = detector;
    }

    /// <inheritdoc />
    public void Execute(ReconciliationContext context)
    {
        foreach (var group in context.MatchGroups)
        {
            _detector.DetectCatalogueMismatches(context, group);
        }
    }
}
