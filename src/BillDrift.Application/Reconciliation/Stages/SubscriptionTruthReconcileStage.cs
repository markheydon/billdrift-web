using BillDrift.Application.Reconciliation.Detection;

namespace BillDrift.Application.Reconciliation.Stages;

/// <summary>
/// Detects mismatches between subscription truth and Stripe billing for each match group.
/// </summary>
public sealed class SubscriptionTruthReconcileStage : IReconciliationStage
{
    private readonly MismatchDetector _detector;

    /// <summary>
    /// Creates a subscription truth reconcile stage with mismatch detector.
    /// </summary>
    public SubscriptionTruthReconcileStage(MismatchDetector detector)
    {
        _detector = detector;
    }

    /// <inheritdoc />
    public void Execute(ReconciliationContext context)
    {
        foreach (var group in context.MatchGroups)
        {
            _detector.DetectSubscriptionTruthMismatches(context, group);
        }
    }
}
