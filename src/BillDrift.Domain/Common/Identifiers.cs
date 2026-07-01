namespace BillDrift.Domain.Common;

/// <summary>
/// Giacom customer identifier (MexId) used to correlate records across supplier billing, subscription management, and Stripe metadata.
/// </summary>
/// <param name="Value">The non-empty, trimmed MexId string.</param>
public readonly record struct MexId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="MexId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw MexId text; leading and trailing whitespace is trimmed.</param>
    /// <returns>A validated MexId.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is null, empty, or whitespace.</exception>
    public static MexId Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "MexId must be non-empty.");
        }

        return new MexId(trimmed);
    }
}

/// <summary>
/// Microsoft tenant identifier associated with a customer's subscription in Giacom Subscription Management.
/// </summary>
/// <param name="Value">The non-empty, trimmed tenant ID string.</param>
public readonly record struct TenantId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="TenantId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw tenant ID text; leading and trailing whitespace is trimmed.</param>
    /// <returns>A validated tenant ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is null, empty, or whitespace.</exception>
    public static TenantId Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "TenantId must be non-empty when present.");
        }

        return new TenantId(trimmed);
    }
}

/// <summary>
/// Microsoft CSP offer identifier; part of <see cref="CommercialKey"/> for price and product alignment.
/// </summary>
/// <param name="Value">The non-empty, trimmed offer ID string.</param>
public readonly record struct OfferId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="OfferId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw offer ID text; leading and trailing whitespace is trimmed.</param>
    /// <returns>A validated offer ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is null, empty, or whitespace.</exception>
    public static OfferId Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "OfferId must be non-empty.");
        }

        return new OfferId(trimmed);
    }
}

/// <summary>
/// Microsoft CSP SKU identifier; combined with <see cref="OfferId"/> to form product identity.
/// </summary>
/// <param name="Value">The non-empty, trimmed SKU ID string.</param>
public readonly record struct SkuId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="SkuId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw SKU ID text; leading and trailing whitespace is trimmed.</param>
    /// <returns>A validated SKU ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is null, empty, or whitespace.</exception>
    public static SkuId Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "SkuId must be non-empty.");
        }

        return new SkuId(trimmed);
    }
}

/// <summary>
/// Stripe customer identifier; Stripe is the source of truth for customer billing.
/// </summary>
/// <param name="Value">The Stripe customer ID (typically prefixed with <c>cus_</c>).</param>
public readonly record struct StripeCustomerId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="StripeCustomerId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw Stripe customer ID; leading and trailing whitespace is trimmed.</param>
    /// <param name="validatePrefix">When <see langword="true"/>, requires the ID to start with <c>cus_</c>.</param>
    /// <returns>A validated Stripe customer ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is empty or fails prefix validation.</exception>
    public static StripeCustomerId Create(string value, bool validatePrefix = true)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "Stripe customer ID must be non-empty.");
        }

        if (validatePrefix && !trimmed.StartsWith("cus_", StringComparison.Ordinal))
        {
            throw new DomainValidationException(nameof(Value), "Stripe customer ID must start with 'cus_'.");
        }

        return new StripeCustomerId(trimmed);
    }
}

/// <summary>
/// Stripe subscription identifier linking a customer to a set of billable subscription items.
/// </summary>
/// <param name="Value">The Stripe subscription ID (typically prefixed with <c>sub_</c>).</param>
public readonly record struct StripeSubscriptionId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="StripeSubscriptionId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw Stripe subscription ID; leading and trailing whitespace is trimmed.</param>
    /// <param name="validatePrefix">When <see langword="true"/>, requires the ID to start with <c>sub_</c>.</param>
    /// <returns>A validated Stripe subscription ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is empty or fails prefix validation.</exception>
    public static StripeSubscriptionId Create(string value, bool validatePrefix = true)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "Stripe subscription ID must be non-empty.");
        }

        if (validatePrefix && !trimmed.StartsWith("sub_", StringComparison.Ordinal))
        {
            throw new DomainValidationException(nameof(Value), "Stripe subscription ID must start with 'sub_'.");
        }

        return new StripeSubscriptionId(trimmed);
    }
}

/// <summary>
/// Stripe subscription item identifier representing a single billable line on a subscription.
/// </summary>
/// <param name="Value">The Stripe subscription item ID (typically prefixed with <c>si_</c>).</param>
public readonly record struct StripeSubscriptionItemId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="StripeSubscriptionItemId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw Stripe subscription item ID; leading and trailing whitespace is trimmed.</param>
    /// <param name="validatePrefix">When <see langword="true"/>, requires the ID to start with <c>si_</c>.</param>
    /// <returns>A validated Stripe subscription item ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is empty or fails prefix validation.</exception>
    public static StripeSubscriptionItemId Create(string value, bool validatePrefix = true)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "Stripe subscription item ID must be non-empty.");
        }

        if (validatePrefix && !trimmed.StartsWith("si_", StringComparison.Ordinal))
        {
            throw new DomainValidationException(nameof(Value), "Stripe subscription item ID must start with 'si_'.");
        }

        return new StripeSubscriptionItemId(trimmed);
    }
}

/// <summary>
/// Stripe product identifier in the customer billing catalogue.
/// </summary>
/// <param name="Value">The Stripe product ID (typically prefixed with <c>prod_</c>).</param>
public readonly record struct StripeProductId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="StripeProductId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw Stripe product ID; leading and trailing whitespace is trimmed.</param>
    /// <param name="validatePrefix">When <see langword="true"/>, requires the ID to start with <c>prod_</c>.</param>
    /// <returns>A validated Stripe product ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is empty or fails prefix validation.</exception>
    public static StripeProductId Create(string value, bool validatePrefix = true)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "Stripe product ID must be non-empty.");
        }

        if (validatePrefix && !trimmed.StartsWith("prod_", StringComparison.Ordinal))
        {
            throw new DomainValidationException(nameof(Value), "Stripe product ID must start with 'prod_'.");
        }

        return new StripeProductId(trimmed);
    }
}

/// <summary>
/// Stripe price identifier defining unit amount and billing interval for a product.
/// </summary>
/// <param name="Value">The Stripe price ID (typically prefixed with <c>price_</c>).</param>
public readonly record struct StripePriceId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="StripePriceId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw Stripe price ID; leading and trailing whitespace is trimmed.</param>
    /// <param name="validatePrefix">When <see langword="true"/>, requires the ID to start with <c>price_</c>.</param>
    /// <returns>A validated Stripe price ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is empty or fails prefix validation.</exception>
    public static StripePriceId Create(string value, bool validatePrefix = true)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "Stripe price ID must be non-empty.");
        }

        if (validatePrefix && !trimmed.StartsWith("price_", StringComparison.Ordinal))
        {
            throw new DomainValidationException(nameof(Value), "Stripe price ID must start with 'price_'.");
        }

        return new StripePriceId(trimmed);
    }
}

/// <summary>
/// Opaque reference identifier from a Giacom billing PDF column, used to correlate supplier cost lines.
/// </summary>
/// <param name="Value">The non-empty, trimmed supplier reference string.</param>
public readonly record struct SupplierReferenceId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="SupplierReferenceId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw supplier reference text; leading and trailing whitespace is trimmed.</param>
    /// <returns>A validated supplier reference ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is null, empty, or whitespace.</exception>
    public static SupplierReferenceId Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "Supplier reference ID must be non-empty when present.");
        }

        return new SupplierReferenceId(trimmed);
    }
}

/// <summary>
/// Giacom-side subscription identifier from Subscription Management, linking subscription truth to supplier billing.
/// </summary>
/// <param name="Value">The non-empty, trimmed supplier subscription ID string.</param>
public readonly record struct SupplierSubscriptionId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="SupplierSubscriptionId"/> from raw input.
    /// </summary>
    /// <param name="value">Raw supplier subscription ID text; leading and trailing whitespace is trimmed.</param>
    /// <returns>A validated supplier subscription ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is null, empty, or whitespace.</exception>
    public static SupplierSubscriptionId Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "Supplier subscription ID must be non-empty when present.");
        }

        return new SupplierSubscriptionId(trimmed);
    }
}
