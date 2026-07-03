using BillDrift.Application.CatalogueReconciliation.Detection;

namespace BillDrift.Application.CatalogueReconciliation.Stages;

/// <summary>Attaches proposed fixes to detected exceptions.</summary>
public sealed class AttachProposedFixesStage : ICatalogueReconciliationStage
{
    private readonly CatalogueProposedFixFactory _factory = new();

    /// <inheritdoc />
    public void Execute(CatalogueReconciliationContext context)
    {
        if (context.ValidationError is not null)
        {
            return;
        }

        _factory.AttachFixes(context);
    }
}
