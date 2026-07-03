namespace BillDrift.Domain.History;

/// <summary>Lifecycle state of an archived reconciliation run.</summary>
public enum RunArchiveStatus
{
    InProgress,
    Completed,
    Failed
}

/// <summary>Input data domain captured in a reconciliation run archive.</summary>
public enum InputDomainType
{
    SupplierCost,
    SubscriptionTruth,
    IntendedPricing,
    StripeBilling,
    ProductMappings
}

/// <summary>Classification of pricing drift events across runs.</summary>
public enum PricingDriftEventType
{
    RrpChanged,
    OverrideAdded,
    OverrideRemoved,
    StripePriceChanged,
    CatalogueMissing,
    CatalogueAligned
}

/// <summary>Future write-back execution state for approved proposals.</summary>
public enum ExecutionOutcomeStatus
{
    NotApplicable,
    Pending,
    Succeeded,
    Failed
}

/// <summary>Run history audit event classification.</summary>
public enum RunHistoryAuditEventType
{
    RunArchiveStarted,
    RunArchived,
    RunArchiveFailed,
    RunCompared,
    DriftTrendsViewed,
    RunHistoryExported
}
