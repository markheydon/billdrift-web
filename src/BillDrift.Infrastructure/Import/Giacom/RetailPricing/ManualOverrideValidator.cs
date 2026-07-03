using BillDrift.Application.Import;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;

namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing;

/// <summary>Validates manual price override requests before raw mapping.</summary>
internal static class ManualOverrideValidator
{
    public sealed record ValidationResult(
        bool IsValid,
        string? ErrorMessage);

    public static ValidationResult Validate(ManualPriceOverrideRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OfferId) && string.IsNullOrWhiteSpace(request.SkuId))
        {
            return new ValidationResult(false, "At least one of offer ID or SKU ID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Term))
        {
            return new ValidationResult(false, "Term is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Frequency))
        {
            return new ValidationResult(false, "Billing frequency is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Rrp))
        {
            return new ValidationResult(false, "RRP is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return new ValidationResult(false, "Override reason is required.");
        }

        return new ValidationResult(true, null);
    }

    public static RawManualPriceEntry ToRawEntry(
        ManualPriceOverrideRequest request,
        string sourceDocumentId,
        int lineIndex)
    {
        var lineKey = $"override-{lineIndex}";
        var id = RawImportId.Create(ImportSourceKind.ManualPriceEntry, sourceDocumentId, lineKey);

        return new RawManualPriceEntry(
            id,
            request.OfferId,
            request.SkuId,
            request.Term,
            request.Frequency,
            request.Wholesale,
            request.Rrp,
            request.Reason.Trim(),
            request.EffectiveDate,
            DateTimeOffset.UtcNow);
    }
}
