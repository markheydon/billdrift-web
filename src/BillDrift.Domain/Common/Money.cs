namespace BillDrift.Domain.Common;

/// <summary>
/// ISO 4217 currency code (e.g. GBP) for monetary amounts in the billing domain.
/// </summary>
/// <param name="Value">The uppercase ISO 4217 currency code.</param>
public readonly record struct CurrencyCode(string Value)
{
    /// <summary>British pound sterling.</summary>
    public static CurrencyCode Gbp => new("GBP");

    /// <summary>
    /// Creates a validated <see cref="CurrencyCode"/> from raw input.
    /// </summary>
    /// <param name="value">Raw currency code; trimmed and converted to uppercase.</param>
    /// <returns>A validated currency code.</returns>
    /// <exception cref="DomainValidationException">Thrown when the value is null, empty, or whitespace.</exception>
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

/// <summary>
/// Monetary amount with currency, used for wholesale, RRP, margins, and Stripe unit prices.
/// </summary>
/// <param name="Amount">The decimal amount in the specified currency.</param>
/// <param name="Currency">The ISO 4217 currency of the amount.</param>
public readonly record struct Money(decimal Amount, CurrencyCode Currency)
{
    /// <summary>
    /// Creates a GBP <see cref="Money"/> value without validation (for known-safe literals).
    /// </summary>
    /// <param name="amount">The amount in pounds sterling.</param>
    /// <returns>A GBP money value.</returns>
    public static Money Gbp(decimal amount) => new(amount, CurrencyCode.Gbp);

    /// <summary>
    /// Creates a validated <see cref="Money"/> value.
    /// </summary>
    /// <param name="amount">The decimal amount.</param>
    /// <param name="currency">The currency code.</param>
    /// <param name="allowNegative">When <see langword="true"/>, permits negative amounts (e.g. pro-rated credits on supplier cost lines).</param>
    /// <returns>A validated money value.</returns>
    /// <exception cref="DomainValidationException">Thrown when amount is negative and <paramref name="allowNegative"/> is <see langword="false"/>.</exception>
    public static Money Create(decimal amount, CurrencyCode currency, bool allowNegative = false)
    {
        if (!allowNegative && amount < 0)
        {
            throw new DomainValidationException(nameof(Amount), "Money amount must be non-negative.");
        }

        return new Money(amount, currency);
    }
}
