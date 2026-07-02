using BillDrift.Application.Reconciliation.Detection;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.Stages;

/// <summary>
/// Attaches supplier cost lines and flags non-CSP and orphan supplier lines (research R6, R8).
/// </summary>
public sealed class SupplierCostReconcileStage : IReconciliationStage
{
    private readonly MismatchDetector _detector;

    /// <summary>
    /// Creates a supplier cost reconcile stage with mismatch detector.
    /// </summary>
    public SupplierCostReconcileStage(MismatchDetector detector)
    {
        _detector = detector;
    }

    /// <inheritdoc />
    public void Execute(ReconciliationContext context)
    {
        foreach (var group in context.MatchGroups)
        {
            _detector.DetectSupplierCostMismatches(context, group);
        }

        FlagOrphanSupplierLines(context);
    }

    private static void FlagOrphanSupplierLines(ReconciliationContext context)
    {
        var attachedIds = context.MatchGroups
            .Where(g => g.SupplierCostLine is not null)
            .Select(g => g.SupplierCostLine!.Id)
            .ToHashSet();

        var supplierLines = context.Request.Inputs.SupplierCostLines ?? [];
        foreach (var line in supplierLines)
        {
            if (attachedIds.Contains(line.Id))
            {
                continue;
            }

            if (line.ChargeType == ChargeType.ProRatedAdjustment)
            {
                continue;
            }

            context.Mismatches.Add(new Mismatch(
                context.NextMismatchId(),
                MismatchType.MappingMissing,
                MismatchSeverity.Error,
                line.Customer,
                null,
                new MismatchEntityRefs(SupplierCostLineId: line.Id),
                null,
                null,
                $"Cannot map supplier cost line to a known product/customer. Source: {line.ProductName}. Orphan supplier line."));
        }
    }
}
