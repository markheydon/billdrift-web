namespace BillDrift.Domain.Common;

public readonly record struct MexId(string Value)
{
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

public readonly record struct TenantId(string Value)
{
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

public readonly record struct OfferId(string Value)
{
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

public readonly record struct SkuId(string Value)
{
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

public readonly record struct StripeCustomerId(string Value)
{
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

public readonly record struct StripeSubscriptionId(string Value)
{
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

public readonly record struct StripeSubscriptionItemId(string Value)
{
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

public readonly record struct StripeProductId(string Value)
{
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

public readonly record struct StripePriceId(string Value)
{
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

public readonly record struct SupplierReferenceId(string Value)
{
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

public readonly record struct SupplierSubscriptionId(string Value)
{
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
