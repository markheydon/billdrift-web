namespace BillDrift.Domain.Import.Stripe;

public sealed record RawStripePrice(
    string PriceId,
    string ProductId,
    long? UnitAmount,
    string Currency,
    string? RecurringInterval,
    long? RecurringIntervalCount,
    IReadOnlyDictionary<string, string> Metadata);
