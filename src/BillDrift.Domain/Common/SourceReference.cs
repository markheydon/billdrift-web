namespace BillDrift.Domain.Common;

public enum ImportSourceKind
{
    GiacomBillingPdf,
    GiacomSubscriptionManagement,
    GiacomPriceList,
    ManualPriceEntry,
    StripeExport
}

public readonly record struct RawImportId(ImportSourceKind SourceKind, string SourceDocumentId, string SourceLineKey)
{
    public static RawImportId Create(ImportSourceKind sourceKind, string sourceDocumentId, string sourceLineKey)
    {
        if (string.IsNullOrWhiteSpace(sourceDocumentId))
        {
            throw new DomainValidationException(nameof(SourceDocumentId), "Source document ID must be non-empty.");
        }

        if (string.IsNullOrWhiteSpace(sourceLineKey))
        {
            throw new DomainValidationException(nameof(SourceLineKey), "Source line key must be non-empty.");
        }

        return new RawImportId(sourceKind, sourceDocumentId.Trim(), sourceLineKey.Trim());
    }
}

public readonly record struct SourceReference(
    ImportSourceKind SourceKind,
    string SourceDocumentId,
    string SourceLineKey)
{
    public static SourceReference FromRawImportId(RawImportId id) =>
        new(id.SourceKind, id.SourceDocumentId, id.SourceLineKey);
}
