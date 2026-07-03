namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Identifier for a standalone Stripe catalogue reconciliation run.</summary>
public readonly record struct CatalogueRunId(Guid Value)
{
    /// <summary>Creates a new catalogue run identifier.</summary>
    public static CatalogueRunId New() => new(Guid.NewGuid());

    /// <summary>Creates a catalogue run identifier from an existing GUID.</summary>
    public static CatalogueRunId FromGuid(Guid value) => new(value);
}
