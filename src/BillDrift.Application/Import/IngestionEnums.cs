namespace BillDrift.Application.Import;

/// <summary>
/// Aggregate outcome of a Giacom PDF ingestion run, summarizing whether extraction succeeded fully, partially, or not at all.
/// </summary>
public enum IngestionOutcomeStatus
{
    /// <summary>All identified billing lines were extracted with no skips.</summary>
    Success = 0,

    /// <summary>Some lines or customer blocks were extracted, but others were skipped; inspect <see cref="IngestionLogEntry"/> entries.</summary>
    PartialSuccess = 1,

    /// <summary>No lines were extracted or the document was unreadable; <see cref="GiacomPdfIngestionResult.Lines"/> is empty.</summary>
    Failure = 2
}

/// <summary>
/// Severity of an ingestion log entry, distinguishing recoverable warnings from blocking errors.
/// </summary>
public enum IngestionLogSeverity
{
    /// <summary>Non-fatal issue; extraction continued (e.g. unparseable optional field).</summary>
    Warning = 0,

    /// <summary>Fatal issue for the affected scope; the line or block was skipped.</summary>
    Error = 1
}

/// <summary>
/// Machine-readable reason codes for ingestion skips and failures, enabling operator dashboards and automated triage.
/// </summary>
public enum IngestionFailureReason
{
    /// <summary>PDF could not be opened or text extraction failed.</summary>
    DocumentUnreadable,

    /// <summary>PDF is password-protected or otherwise encrypted.</summary>
    DocumentEncrypted,

    /// <summary>No customer billing blocks were found in the document layout.</summary>
    NoCustomerBlocksFound,

    /// <summary>A customer block lacked the expected header row.</summary>
    BlockHeaderMissing,

    /// <summary>Customer MEX ID was missing or empty in a block header.</summary>
    MexIdMissing,

    /// <summary>Customer name was missing or empty in a block header.</summary>
    CustomerNameMissing,

    /// <summary>Line quantity field could not be parsed as a number.</summary>
    QuantityUnparseable,

    /// <summary>Line cost field could not be parsed as a monetary value.</summary>
    LineCostUnparseable,

    /// <summary>Billing period start or end could not be parsed as a date.</summary>
    PeriodUnparseable,

    /// <summary>Line structure did not match any known Giacom block grammar pattern.</summary>
    AmbiguousLineStructure,

    /// <summary>Document exceeded the configured maximum page count.</summary>
    PageLimitExceeded,

    /// <summary>Document exceeded the configured maximum file size.</summary>
    FileSizeExceeded,

    /// <summary>PDF contained no extractable content.</summary>
    EmptyDocument
}
