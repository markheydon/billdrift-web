namespace BillDrift.Domain.Mapping;

/// <summary>
/// Domain-generated identifier for a <see cref="ProductMapping"/>.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct ProductMappingId(Guid Value)
{
    /// <summary>Generates a new unique product mapping ID.</summary>
    /// <returns>A new ID with a random GUID.</returns>
    public static ProductMappingId New() => new(Guid.NewGuid());

    /// <summary>Reconstructs an ID from an existing GUID (e.g. when loading from persistence).</summary>
    /// <param name="value">The GUID to wrap.</param>
    /// <returns>The corresponding product mapping ID.</returns>
    public static ProductMappingId FromGuid(Guid value) => new(value);
}
