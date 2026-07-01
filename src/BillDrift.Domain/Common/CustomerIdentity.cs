namespace BillDrift.Domain.Common;

/// <summary>
/// Cross-domain customer identity anchoring reconciliation on the Giacom MexId while optionally linking Stripe and tenant identifiers.
/// MexId is required; other identifiers enrich matching when available.
/// </summary>
/// <param name="MexId">Giacom customer identifier used as the primary correlation key across all four data domains.</param>
/// <param name="DisplayName">Human-readable customer name from source data.</param>
/// <param name="TenantId">Microsoft tenant ID from Subscription Management, when known.</param>
/// <param name="StripeCustomerId">Stripe customer ID when the customer exists in Stripe billing.</param>
public sealed record CustomerIdentity(
    MexId MexId,
    string? DisplayName = null,
    TenantId? TenantId = null,
    StripeCustomerId? StripeCustomerId = null)
{
    /// <summary>
    /// Creates a <see cref="CustomerIdentity"/> with the required MexId and optional cross-reference identifiers.
    /// </summary>
    /// <param name="mexId">The Giacom customer identifier (required).</param>
    /// <param name="displayName">Optional display name for operator-facing output.</param>
    /// <param name="tenantId">Optional Microsoft tenant ID.</param>
    /// <param name="stripeCustomerId">Optional Stripe customer ID.</param>
    /// <returns>A customer identity for normalized billing entities.</returns>
    public static CustomerIdentity Create(MexId mexId, string? displayName = null, TenantId? tenantId = null, StripeCustomerId? stripeCustomerId = null) =>
        new(mexId, displayName, tenantId, stripeCustomerId);
}
