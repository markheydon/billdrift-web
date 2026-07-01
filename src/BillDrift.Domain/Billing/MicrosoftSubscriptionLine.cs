using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

/// <summary>
/// Normalized subscription line from Giacom Subscription Management, representing subscription truth (licence counts and renewal state).
/// Only <see cref="SubscriptionStatus.Active"/> lines participate in quantity reconciliation by default.
/// </summary>
/// <param name="Id">Domain-generated identifier assigned during normalization.</param>
/// <param name="Customer">Customer identity with MexId and optional TenantId.</param>
/// <param name="CommercialKeyRoot">Product identity (OfferId + SkuId) without term or frequency.</param>
/// <param name="LicenceCount">Number of licences on this subscription line.</param>
/// <param name="Term">Contract term length.</param>
/// <param name="Frequency">Billing frequency.</param>
/// <param name="RenewalDate">Next renewal date, when known.</param>
/// <param name="Status">Subscription lifecycle status from the source export.</param>
/// <param name="SupplierSubscriptionId">Giacom-side subscription identifier for correlation.</param>
/// <param name="Source">Traceability link back to the raw subscription management import.</param>
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
