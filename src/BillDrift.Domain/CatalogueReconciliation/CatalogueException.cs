using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;

namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>A detected catalogue issue from Stripe catalogue reconciliation.</summary>
public sealed record CatalogueException(
    CatalogueExceptionId Id,
    CatalogueExceptionType Type,
    CommercialKey? CommercialKey,
    CommercialKeyRoot? CommercialKeyRoot,
    MismatchSeverity Severity,
    string Description,
    string? ExpectedValue,
    string? ActualValue,
    IReadOnlyList<StripeProductId> AffectedStripeProductIds,
    IReadOnlyList<StripePriceId> AffectedStripePriceIds,
    ProductMappingId? MappingId,
    string RuleId);
