namespace BillDrift.Domain.Common;

/// <summary>
/// Identifies which external data source produced an imported or normalized record.
/// </summary>
public enum ImportSourceKind
{
    /// <summary>Line extracted from a Giacom supplier billing PDF.</summary>
    GiacomBillingPdf,

    /// <summary>Row from Giacom Subscription Management export (subscription truth).</summary>
    GiacomSubscriptionManagement,

    /// <summary>Row from the Giacom wholesale price list catalogue.</summary>
    GiacomPriceList,

    /// <summary>Manually entered price override for a specific commercial key.</summary>
    ManualPriceEntry,

    /// <summary>Record imported from a Stripe export (customer billing source of truth).</summary>
    StripeExport
}

/// <summary>
/// Composite idempotency key for raw import records; the same triple yields the same record on re-import.
/// </summary>
/// <param name="SourceKind">Which external source produced the record.</param>
/// <param name="SourceDocumentId">Identifier of the source document (e.g. blob path or upload ID).</param>
/// <param name="SourceLineKey">Unique key for the line within the document (e.g. row number or PDF line hash).</param>
public readonly record struct RawImportId(ImportSourceKind SourceKind, string SourceDocumentId, string SourceLineKey)
{
    /// <summary>
    /// Creates a validated <see cref="RawImportId"/> ensuring document and line keys are non-empty.
    /// </summary>
    /// <param name="sourceKind">The import source kind.</param>
    /// <param name="sourceDocumentId">The source document identifier; trimmed.</param>
    /// <param name="sourceLineKey">The line key within the document; trimmed.</param>
    /// <returns>A validated raw import ID.</returns>
    /// <exception cref="DomainValidationException">Thrown when document ID or line key is null, empty, or whitespace.</exception>
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

/// <summary>
/// Links a normalized billing entity back to its originating raw import record for audit and traceability.
/// </summary>
/// <param name="SourceKind">Which external source produced the original record.</param>
/// <param name="SourceDocumentId">Identifier of the source document.</param>
/// <param name="SourceLineKey">Unique key for the line within the document.</param>
public readonly record struct SourceReference(
    ImportSourceKind SourceKind,
    string SourceDocumentId,
    string SourceLineKey)
{
    /// <summary>
    /// Creates a <see cref="SourceReference"/> from a <see cref="RawImportId"/>.
    /// </summary>
    /// <param name="id">The raw import idempotency key.</param>
    /// <returns>A source reference pointing to the same import origin.</returns>
    public static SourceReference FromRawImportId(RawImportId id) =>
        new(id.SourceKind, id.SourceDocumentId, id.SourceLineKey);
}
