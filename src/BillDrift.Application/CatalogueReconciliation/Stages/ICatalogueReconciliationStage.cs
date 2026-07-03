namespace BillDrift.Application.CatalogueReconciliation.Stages;

/// <summary>A single stage in the catalogue reconciliation pipeline.</summary>
public interface ICatalogueReconciliationStage
{
    /// <summary>Executes this stage against the shared context.</summary>
    void Execute(CatalogueReconciliationContext context);
}
