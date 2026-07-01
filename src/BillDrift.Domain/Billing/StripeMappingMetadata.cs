using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

public sealed record StripeMappingMetadata(
    MexId? MexId,
    OfferId? OfferId,
    SkuId? SkuId,
    IReadOnlyList<SupplierReferenceId> SupplierReferences,
    IReadOnlyDictionary<string, string> Additional);
