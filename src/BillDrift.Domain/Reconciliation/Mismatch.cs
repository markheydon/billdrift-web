using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;

namespace BillDrift.Domain.Reconciliation;

public sealed record MismatchEntityRefs(
    SupplierCostLineId? SupplierCostLineId = null,
    MicrosoftSubscriptionLineId? SubscriptionLineId = null,
    IntendedPriceId? IntendedPriceId = null,
    StripeBillingItemId? StripeBillingItemId = null);

public sealed record Mismatch(
    MismatchId Id,
    MismatchType Type,
    MismatchSeverity Severity,
    CustomerIdentity? Customer,
    CommercialKey? CommercialKey,
    MismatchEntityRefs InvolvedEntityIds,
    string? ExpectedValue,
    string? ActualValue,
    string Description);
