using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

/// <summary>
/// Optional lifecycle and pricing context from Giacom Subscription Management, when columns are present.
/// </summary>
/// <param name="IsNce">Whether the subscription is on New Commerce Experience.</param>
/// <param name="IsTrial">Whether the subscription is a trial.</param>
/// <param name="EndOfTermAction">End-of-term action text (e.g. auto-renew, cancel).</param>
/// <param name="CancellableUntil">Last date the subscription can be cancelled, when known.</param>
/// <param name="MigrationToNce">NCE migration status text, when present.</param>
/// <param name="AssignedLicenceCount">Assigned seat count when distinct from purchased licences.</param>
/// <param name="Price">Wholesale or sell price when present.</param>
/// <param name="ErpPrice">Estimated retail price when present.</param>
public sealed record SubscriptionLifecycleFacts(
    bool? IsNce = null,
    bool? IsTrial = null,
    string? EndOfTermAction = null,
    DateOnly? CancellableUntil = null,
    string? MigrationToNce = null,
    int? AssignedLicenceCount = null,
    Money? Price = null,
    Money? ErpPrice = null);
