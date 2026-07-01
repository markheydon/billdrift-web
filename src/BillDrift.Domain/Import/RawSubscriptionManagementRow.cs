using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import;

/// <summary>
/// Raw row from Giacom Subscription Management export, preserving subscription truth before normalization.
/// </summary>
/// <param name="Id">Composite idempotency key for re-import deduplication.</param>
/// <param name="CustomerNameRaw">Customer name as exported from subscription management.</param>
/// <param name="MexIdRaw">MexId as exported without validation.</param>
/// <param name="TenantIdRaw">Microsoft tenant ID text, if present in the export.</param>
/// <param name="OfferIdRaw">Offer ID text to be validated during normalization.</param>
/// <param name="SkuIdRaw">SKU ID text to be validated during normalization.</param>
/// <param name="LicencesRaw">Licence count text to be parsed during normalization.</param>
/// <param name="TermRaw">Contract term text to be mapped to <see cref="Term"/> during normalization.</param>
/// <param name="FrequencyRaw">Billing frequency text to be mapped to <see cref="BillingFrequency"/> during normalization.</param>
/// <param name="RenewalDateRaw">Renewal date text, if present in the export.</param>
/// <param name="StatusRaw">Subscription status text to be mapped to <see cref="SubscriptionStatus"/> during normalization.</param>
/// <param name="SupplierSubscriptionIdRaw">Giacom subscription ID text, if present.</param>
/// <param name="SourceDocumentId">Identifier of the source export file.</param>
/// <param name="RowNumber">1-based row number within the export for traceability.</param>
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
