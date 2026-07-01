namespace BillDrift.Domain.Common;

public readonly record struct CurrencyCode(string Value)
{
    public static CurrencyCode Gbp => new("GBP");

    public static CurrencyCode Create(string value)
    {
        var trimmed = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Value), "Currency code must be non-empty.");
        }

        return new CurrencyCode(trimmed);
    }
}

public readonly record struct Money(decimal Amount, CurrencyCode Currency)
{
    public static Money Gbp(decimal amount) => new(amount, CurrencyCode.Gbp);

    public static Money Create(decimal amount, CurrencyCode currency, bool allowNegative = false)
    {
        if (!allowNegative && amount < 0)
        {
            throw new DomainValidationException(nameof(Amount), "Money amount must be non-negative.");
        }

        return new Money(amount, currency);
    }
}
