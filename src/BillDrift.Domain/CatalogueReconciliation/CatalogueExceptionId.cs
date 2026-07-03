namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Surrogate identifier for a <see cref="CatalogueException"/>.</summary>
public readonly record struct CatalogueExceptionId(Guid Value)
{
    /// <summary>Creates a new catalogue exception identifier.</summary>
    public static CatalogueExceptionId New() => new(Guid.NewGuid());

    /// <summary>Creates a catalogue exception identifier from an existing GUID.</summary>
    public static CatalogueExceptionId FromGuid(Guid value) => new(value);
}
