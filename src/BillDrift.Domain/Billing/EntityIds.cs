namespace BillDrift.Domain.Billing;

public readonly record struct SupplierCostLineId(Guid Value)
{
    public static SupplierCostLineId New() => new(Guid.NewGuid());
    public static SupplierCostLineId FromGuid(Guid value) => new(value);
}

public readonly record struct MicrosoftSubscriptionLineId(Guid Value)
{
    public static MicrosoftSubscriptionLineId New() => new(Guid.NewGuid());
    public static MicrosoftSubscriptionLineId FromGuid(Guid value) => new(value);
}

public readonly record struct IntendedPriceId(Guid Value)
{
    public static IntendedPriceId New() => new(Guid.NewGuid());
    public static IntendedPriceId FromGuid(Guid value) => new(value);
}

public readonly record struct StripeBillingItemId(Guid Value)
{
    public static StripeBillingItemId New() => new(Guid.NewGuid());
    public static StripeBillingItemId FromGuid(Guid value) => new(value);
}
