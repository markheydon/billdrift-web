namespace BillDrift.Application.Import;

public enum IngestionOutcomeStatus
{
    Success = 0,
    PartialSuccess = 1,
    Failure = 2
}

public enum IngestionLogSeverity
{
    Warning = 0,
    Error = 1
}

public enum IngestionFailureReason
{
    DocumentUnreadable,
    DocumentEncrypted,
    NoCustomerBlocksFound,
    BlockHeaderMissing,
    MexIdMissing,
    CustomerNameMissing,
    QuantityUnparseable,
    LineCostUnparseable,
    PeriodUnparseable,
    AmbiguousLineStructure,
    PageLimitExceeded,
    FileSizeExceeded,
    EmptyDocument
}
