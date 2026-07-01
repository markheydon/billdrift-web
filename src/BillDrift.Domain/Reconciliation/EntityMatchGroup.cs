using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;

namespace BillDrift.Domain.Reconciliation;

public sealed record EntityMatchGroup(
    MatchGroupId Id,
    CustomerIdentity Customer,
    CommercialKey? CommercialKey,
    SupplierCostLine? SupplierCostLine,
    MicrosoftSubscriptionLine? SubscriptionLine,
    IntendedPrice? IntendedPrice,
    StripeBillingItem? StripeItem,
    MatchConfidence Confidence);
