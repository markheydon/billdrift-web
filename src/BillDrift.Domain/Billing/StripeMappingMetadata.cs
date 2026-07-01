using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

/// <summary>
/// Correlation metadata stored on Stripe subscription items, used to link customer billing back to Giacom identifiers.
/// Missing metadata triggers <see cref="MismatchType.MappingMissing"/> rather than silent matching during reconciliation.
/// </summary>
/// <param name="MexId">Giacom customer identifier from Stripe metadata, when present.</param>
/// <param name="OfferId">Microsoft CSP offer ID from Stripe metadata, when present.</param>
/// <param name="SkuId">Microsoft CSP SKU ID from Stripe metadata, when present.</param>
/// <param name="SupplierReferences">Supplier reference IDs from Stripe metadata for cross-domain correlation.</param>
/// <param name="Additional">Any other Stripe metadata key-value pairs not mapped to typed fields.</param>
public sealed record StripeMappingMetadata(
    MexId? MexId,
    OfferId? OfferId,
    SkuId? SkuId,
    IReadOnlyList<SupplierReferenceId> SupplierReferences,
    IReadOnlyDictionary<string, string> Additional);
