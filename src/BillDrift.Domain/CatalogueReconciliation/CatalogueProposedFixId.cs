namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Surrogate identifier for a <see cref="CatalogueProposedFix"/>.</summary>
public readonly record struct CatalogueProposedFixId(Guid Value)
{
    /// <summary>Creates a new catalogue proposed fix identifier.</summary>
    public static CatalogueProposedFixId New() => new(Guid.NewGuid());

    /// <summary>Creates a catalogue proposed fix identifier from an existing GUID.</summary>
    public static CatalogueProposedFixId FromGuid(Guid value) => new(value);
}
