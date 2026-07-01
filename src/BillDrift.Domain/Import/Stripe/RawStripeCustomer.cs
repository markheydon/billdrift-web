namespace BillDrift.Domain.Import.Stripe;

/// <summary>
/// Raw Stripe customer record from export, preserving source fidelity before normalization.
/// Stripe is the source of truth for customer billing identity.
/// </summary>
/// <param name="CustomerId">Stripe customer ID as exported (e.g. <c>cus_...</c>).</param>
/// <param name="Name">Customer display name from Stripe, if set.</param>
/// <param name="Metadata">Full Stripe metadata dictionary including MexId and other correlation keys.</param>
public sealed record RawStripeCustomer(
    string CustomerId,
    string? Name,
    IReadOnlyDictionary<string, string> Metadata);
