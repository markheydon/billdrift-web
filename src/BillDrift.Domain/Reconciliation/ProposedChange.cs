using BillDrift.Domain.Common;

namespace BillDrift.Domain.Reconciliation;

public sealed record ProposedChangeTarget(
    StripeCustomerId? CustomerId = null,
    StripeSubscriptionId? SubscriptionId = null,
    StripeSubscriptionItemId? SubscriptionItemId = null,
    StripePriceId? PriceId = null);

public sealed record CatalogueEntryPayload(
    StripeProductId? StripeProductId,
    string NormalizedName,
    CommercialKeyRoot CommercialKeyRoot,
    IReadOnlyList<PriceTermKey> PricesToCreate);

public sealed record ProposedChange(
    ProposedChangeId Id,
    IdempotencyKey IdempotencyKey,
    MismatchId MismatchId,
    ProposedActionType ActionType,
    ProposedChangeTarget Target,
    IReadOnlyDictionary<string, string> ProposedValues,
    CatalogueEntryPayload? CataloguePayload,
    int ExecutionOrder);
