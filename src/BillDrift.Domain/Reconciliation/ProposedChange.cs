using BillDrift.Domain.Common;

namespace BillDrift.Domain.Reconciliation;

/// <summary>
/// Stripe API target identifiers for a <see cref="ProposedChange"/>, specifying which customer, subscription, item, or price to modify.
/// </summary>
/// <param name="CustomerId">Stripe customer to act before subscription-level changes.</param>
/// <param name="SubscriptionId">Stripe subscription to modify.</param>
/// <param name="SubscriptionItemId">Stripe subscription item for quantity or price updates.</param>
/// <param name="PriceId">Stripe price to switch to when changing billing interval or amount.</param>
public sealed record ProposedChangeTarget(
    StripeCustomerId? CustomerId = null,
    StripeSubscriptionId? SubscriptionId = null,
    StripeSubscriptionItemId? SubscriptionItemId = null,
    StripePriceId? PriceId = null);

/// <summary>
/// Payload for <see cref="ProposedActionType.CreateOrUpdateCatalogueEntry"/> actions that create or update Stripe products and prices.
/// </summary>
/// <param name="StripeProductId">Existing Stripe product to update, or <see langword="null"/> to create a new product.</param>
/// <param name="NormalizedName">Canonical product name for the Stripe catalogue entry.</param>
/// <param name="CommercialKeyRoot">Product identity (OfferId + SkuId) for mapping correlation.</param>
/// <param name="PricesToCreate">Term and frequency combinations requiring new Stripe prices.</param>
public sealed record CatalogueEntryPayload(
    StripeProductId? StripeProductId,
    string NormalizedName,
    CommercialKeyRoot CommercialKeyRoot,
    IReadOnlyList<PriceTermKey> PricesToCreate);

/// <summary>
/// A corrective action proposed against Stripe to resolve a <see cref="Mismatch"/> detected during reconciliation.
/// Actions are ordered by <see cref="ExecutionOrder"/> and keyed by <see cref="IdempotencyKey"/> to prevent duplicate execution.
/// </summary>
/// <param name="Id">Unique identifier for this proposed change.</param>
/// <param name="IdempotencyKey">Deterministic key preventing duplicate Stripe API calls for the same correction.</param>
/// <param name="MismatchId">The mismatch this change resolves.</param>
/// <param name="ActionType">Kind of Stripe modification (quantity update, price switch, item creation, or catalogue update).</param>
/// <param name="Target">Stripe entity identifiers to modify.</param>
/// <param name="ProposedValues">Key-value pairs describing the new values to apply in Stripe.</param>
/// <param name="CataloguePayload">Additional product and price creation details for catalogue update actions.</param>
/// <param name="ExecutionOrder">Sequence priority; lower values execute first within the same run.</param>
public sealed record ProposedChange(
    ProposedChangeId Id,
    IdempotencyKey IdempotencyKey,
    MismatchId MismatchId,
    ProposedActionType ActionType,
    ProposedChangeTarget Target,
    IReadOnlyDictionary<string, string> ProposedValues,
    CatalogueEntryPayload? CataloguePayload,
    int ExecutionOrder);
