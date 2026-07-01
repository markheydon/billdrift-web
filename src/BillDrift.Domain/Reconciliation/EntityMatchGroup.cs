using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;

namespace BillDrift.Domain.Reconciliation;

/// <summary>
/// Groups entities from all four data domains that represent the same customer product for cross-domain comparison.
/// Not all slots are populated — partial groups indicate missing data in one or more domains.
/// </summary>
/// <param name="Id">Unique identifier for this match group within the run.</param>
/// <param name="Customer">Customer identity shared by matched entities.</param>
/// <param name="CommercialKey">Full commercial key when term and frequency are known.</param>
/// <param name="SupplierCostLine">Matched supplier cost line from Giacom billing PDF, if present.</param>
/// <param name="SubscriptionLine">Matched subscription line from Giacom Subscription Management, if present.</param>
/// <param name="IntendedPrice">Matched intended price for this commercial key, if present.</param>
/// <param name="StripeItem">Matched Stripe billing item (customer billing source of truth), if present.</param>
/// <param name="Confidence">How confidently the entities in this group represent the same commercial product.</param>
public sealed record EntityMatchGroup(
    MatchGroupId Id,
    CustomerIdentity Customer,
    CommercialKey? CommercialKey,
    SupplierCostLine? SupplierCostLine,
    MicrosoftSubscriptionLine? SubscriptionLine,
    IntendedPrice? IntendedPrice,
    StripeBillingItem? StripeItem,
    MatchConfidence Confidence);
