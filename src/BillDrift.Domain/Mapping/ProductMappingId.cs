namespace BillDrift.Domain.Mapping;

public readonly record struct ProductMappingId(Guid Value)
{
    public static ProductMappingId New() => new(Guid.NewGuid());
    public static ProductMappingId FromGuid(Guid value) => new(value);
}
