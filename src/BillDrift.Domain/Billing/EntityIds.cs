namespace BillDrift.Domain.Billing;

/// <summary>
/// Domain-generated identifier for a normalized <see cref="SupplierCostLine"/>.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct SupplierCostLineId(Guid Value)
{
    /// <summary>Generates a new unique supplier cost line ID.</summary>
    /// <returns>A new ID with a random GUID.</returns>
    public static SupplierCostLineId New() => new(Guid.NewGuid());

    /// <summary>Reconstructs an ID from an existing GUID (e.g. when loading from persistence).</summary>
    /// <param name="value">The GUID to wrap.</param>
    /// <returns>The corresponding supplier cost line ID.</returns>
    public static SupplierCostLineId FromGuid(Guid value) => new(value);
}

/// <summary>
/// Domain-generated identifier for a normalized <see cref="MicrosoftSubscriptionLine"/>.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct MicrosoftSubscriptionLineId(Guid Value)
{
    /// <summary>Generates a new unique subscription line ID.</summary>
    /// <returns>A new ID with a random GUID.</returns>
    public static MicrosoftSubscriptionLineId New() => new(Guid.NewGuid());

    /// <summary>Reconstructs an ID from an existing GUID (e.g. when loading from persistence).</summary>
    /// <param name="value">The GUID to wrap.</param>
    /// <returns>The corresponding subscription line ID.</returns>
    public static MicrosoftSubscriptionLineId FromGuid(Guid value) => new(value);
}

/// <summary>
/// Domain-generated identifier for a normalized <see cref="IntendedPrice"/>.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct IntendedPriceId(Guid Value)
{
    /// <summary>Generates a new unique intended price ID.</summary>
    /// <returns>A new ID with a random GUID.</returns>
    public static IntendedPriceId New() => new(Guid.NewGuid());

    /// <summary>Reconstructs an ID from an existing GUID (e.g. when loading from persistence).</summary>
    /// <param name="value">The GUID to wrap.</param>
    /// <returns>The corresponding intended price ID.</returns>
    public static IntendedPriceId FromGuid(Guid value) => new(value);
}

/// <summary>
/// Domain-generated identifier for a normalized <see cref="StripeBillingItem"/>.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct StripeBillingItemId(Guid Value)
{
    /// <summary>Generates a new unique Stripe billing item ID.</summary>
    /// <returns>A new ID with a random GUID.</returns>
    public static StripeBillingItemId New() => new(Guid.NewGuid());

    /// <summary>Reconstructs an ID from an existing GUID (e.g. when loading from persistence).</summary>
    /// <param name="value">The GUID to wrap.</param>
    /// <returns>The corresponding Stripe billing item ID.</returns>
    public static StripeBillingItemId FromGuid(Guid value) => new(value);
}
