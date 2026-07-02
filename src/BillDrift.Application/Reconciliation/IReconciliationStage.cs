namespace BillDrift.Application.Reconciliation;

/// <summary>
/// A single ordered step in the reconciliation pipeline.
/// </summary>
public interface IReconciliationStage
{
    /// <summary>
    /// Executes this stage, mutating the shared <see cref="ReconciliationContext"/>.
    /// </summary>
    /// <param name="context">Per-run workspace with indexes, match groups, and detected issues.</param>
    void Execute(ReconciliationContext context);
}
