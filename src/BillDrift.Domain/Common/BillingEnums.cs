namespace BillDrift.Domain.Common;

/// <summary>
/// Distinguishes recurring supplier charges from one-off pro-rated adjustments on Giacom billing PDFs.
/// Pro-rated adjustments must be excluded from recurring quantity totals to avoid double-counting.
/// </summary>
public enum ChargeType
{
    /// <summary>Standard recurring licence charge for the billing period.</summary>
    Recurring,

    /// <summary>Mid-cycle adjustment (credit or debit) that must not be aggregated with recurring quantities.</summary>
    ProRatedAdjustment
}

/// <summary>
/// Lifecycle state of a Microsoft subscription line from Giacom Subscription Management.
/// Only <see cref="Active"/> lines participate in quantity reconciliation by default.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>Subscription is active and billable.</summary>
    Active,

    /// <summary>Subscription is temporarily suspended.</summary>
    Suspended,

    /// <summary>Subscription has been cancelled.</summary>
    Cancelled,

    /// <summary>Subscription is pending activation or renewal.</summary>
    Pending,

    /// <summary>Status could not be determined from the source data.</summary>
    Unknown
}

/// <summary>
/// Availability status of a price list entry from the Giacom catalogue.
/// </summary>
public enum PriceListStatus
{
    /// <summary>Product is currently available for sale.</summary>
    Active,

    /// <summary>Product has reached end of sale but may still have active subscriptions.</summary>
    EndOfSale,

    /// <summary>Status could not be determined from the source data.</summary>
    Unknown
}

/// <summary>
/// Origin of an <see cref="Billing.IntendedPrice"/> record.
/// When two entries share the same <see cref="CommercialKey"/>, <see cref="ManualOverride"/> takes precedence over <see cref="Catalogue"/>.
/// </summary>
public enum PriceSource
{
    /// <summary>Price derived from the Giacom price list catalogue.</summary>
    Catalogue,

    /// <summary>Price entered manually and overriding the catalogue for the same commercial key.</summary>
    ManualOverride
}
