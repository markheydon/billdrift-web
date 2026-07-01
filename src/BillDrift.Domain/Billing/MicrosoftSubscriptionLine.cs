using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

public sealed record MicrosoftSubscriptionLine(
    MicrosoftSubscriptionLineId Id,
    CustomerIdentity Customer,
    CommercialKeyRoot CommercialKeyRoot,
    int LicenceCount,
    Term Term,
    BillingFrequency Frequency,
    DateOnly? RenewalDate,
    SubscriptionStatus Status,
    SupplierSubscriptionId? SupplierSubscriptionId,
    SourceReference Source);
