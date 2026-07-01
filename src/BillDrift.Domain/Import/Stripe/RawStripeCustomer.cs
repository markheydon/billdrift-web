namespace BillDrift.Domain.Import.Stripe;

public sealed record RawStripeCustomer(
    string CustomerId,
    string? Name,
    IReadOnlyDictionary<string, string> Metadata);
