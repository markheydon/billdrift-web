namespace BillDrift.Domain.Import.Stripe;

/// <summary>
/// Raw Stripe price record from export, defining unit amount and recurring billing interval for a product.
/// </summary>
/// <param name="PriceId">Stripe price ID as exported (e.g. <c>price_...</c>).</param>
/// <param name="ProductId">Parent Stripe product ID.</param>
/// <param name="UnitAmount">Unit amount in the smallest currency unit (e.g. pence), if set.</param>
/// <param name="Currency">ISO currency code as exported from Stripe.</param>
/// <param name="RecurringInterval">Billing interval string (e.g. "month", "year"), if recurring.</param>
/// <param name="RecurringIntervalCount">Number of intervals between billings, if recurring.</param>
/// <param name="Metadata">Full Stripe metadata dictionary for term and frequency correlation.</param>
public sealed record RawStripePrice(
    string PriceId,
    string ProductId,
    long? UnitAmount,
    string Currency,
    string? RecurringInterval,
    long? RecurringIntervalCount,
    IReadOnlyDictionary<string, string> Metadata);
