namespace BillDrift.Domain.Common;

public sealed record CustomerIdentity(
    MexId MexId,
    string? DisplayName = null,
    TenantId? TenantId = null,
    StripeCustomerId? StripeCustomerId = null)
{
    public static CustomerIdentity Create(MexId mexId, string? displayName = null, TenantId? tenantId = null, StripeCustomerId? stripeCustomerId = null) =>
        new(mexId, displayName, tenantId, stripeCustomerId);
}
