namespace BillDrift.Domain.Common;

public enum ChargeType
{
    Recurring,
    ProRatedAdjustment
}

public enum SubscriptionStatus
{
    Active,
    Suspended,
    Cancelled,
    Pending,
    Unknown
}

public enum PriceListStatus
{
    Active,
    EndOfSale,
    Unknown
}

public enum PriceSource
{
    Catalogue,
    ManualOverride
}
