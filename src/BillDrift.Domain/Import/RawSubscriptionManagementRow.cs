using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import;

public sealed record RawSubscriptionManagementRow(
    RawImportId Id,
    string CustomerNameRaw,
    string MexIdRaw,
    string? TenantIdRaw,
    string OfferIdRaw,
    string SkuIdRaw,
    string LicencesRaw,
    string TermRaw,
    string FrequencyRaw,
    string? RenewalDateRaw,
    string StatusRaw,
    string? SupplierSubscriptionIdRaw,
    string SourceDocumentId,
    int RowNumber);
