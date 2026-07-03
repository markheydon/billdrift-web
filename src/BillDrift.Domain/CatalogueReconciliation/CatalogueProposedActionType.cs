namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Corrective action proposed for a catalogue exception.</summary>
public enum CatalogueProposedActionType
{
    CreateProduct,
    CreatePrice,
    CreateReplacementPrice,
    FlagManualCleanup
}
