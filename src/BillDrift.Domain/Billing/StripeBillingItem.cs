using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

public sealed record StripeBillingItem(
    StripeBillingItemId Id,
    CustomerIdentity Customer,
    StripeSubscriptionId SubscriptionId,
    StripeSubscriptionItemId SubscriptionItemId,
    StripeProductId ProductId,
    StripePriceId PriceId,
    long Quantity,
    BillingFrequency Frequency,
    Money UnitAmount,
    StripeMappingMetadata MappingMetadata,
    SourceReference Source);
