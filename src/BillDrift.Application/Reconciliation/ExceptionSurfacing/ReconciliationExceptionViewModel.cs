using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing;

/// <summary>Operator workflow category for a surfaced reconciliation exception.</summary>
public enum ExceptionCategory
{
    MissingBillingItem,
    OrphanedBillingItem,
    QuantityLicenceMismatch,
    BillingFrequencyMismatch,
    ProductMismatch,
    StripeProductMissing,
    StripePriceMissing,
    StripePriceRrpMismatch,
    OfferSkuAmbiguousMapping,
    MexIdMismatch,
    NonCspManualReview
}

/// <summary>Which reconciliation comparison domain produced the exception.</summary>
public enum ReconciliationDomain
{
    TruthVsStripe,
    SupplierCostVsMapping,
    PricingVsCatalogue
}

/// <summary>Operator-facing severity aligned with engine mismatch severity.</summary>
public enum ExceptionSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>Source domain for a single evidence datum.</summary>
public enum EvidenceSource
{
    SubscriptionTruth,
    StripeSubscriptionItem,
    SupplierCostLine,
    IntendedRetailPrice,
    StripeCatalogue,
    ProductMapping
}

/// <summary>Suppression rule applied during the suppress phase (audit trail).</summary>
public enum SuppressionRule
{
    RootCauseMapping,
    RootCauseMexId,
    LowConfidence,
    CatalogueSubsumedBySubscription,
    OutOfScopeInactive
}

/// <summary>Deterministic identifier for a surfaced exception within a run.</summary>
public readonly record struct SurfacedExceptionId(string Value)
{
    /// <summary>Creates an ID for a mismatch-backed exception.</summary>
    public static SurfacedExceptionId FromMismatch(RunId runId, MismatchId mismatchId) =>
        new($"{runId.Value}:m:{mismatchId.Value}");

    /// <summary>Creates an ID for a derived detector exception.</summary>
    public static SurfacedExceptionId FromDerived(RunId runId, string ruleCode, string entityRef) =>
        new($"{runId.Value}:d:{ruleCode}:{entityRef}");
}

/// <summary>Top-level view model produced by exception surfacing.</summary>
public sealed record ReconciliationExceptionViewModel(
    RunId RunId,
    BillingPeriod Scope,
    DateTimeOffset GeneratedAt,
    ExceptionRunSummary Summary,
    IReadOnlyList<CustomerExceptionGroup> CustomerGroups)
{
    /// <summary>True when at least one exception was surfaced.</summary>
    public bool HasExceptions => Summary.TotalCount > 0;

    /// <summary>
    /// Returns a deterministic flat list following customer group order and within-group ordering.
    /// </summary>
    public IReadOnlyList<SurfacedException> FlatExceptions() =>
        CustomerGroups.SelectMany(g => g.Exceptions).ToList();
}

/// <summary>Run-level aggregate statistics for surfaced exceptions.</summary>
public sealed record ExceptionRunSummary(
    int TotalCount,
    IReadOnlyDictionary<ExceptionSeverity, int> BySeverity,
    IReadOnlyDictionary<ExceptionCategory, int> ByCategory,
    IReadOnlyDictionary<ReconciliationDomain, int> ByDomain,
    int CustomersAffected,
    int RequiresActionNowCount,
    int SuppressedCount);

/// <summary>Per-customer bucket of surfaced exceptions.</summary>
public sealed record CustomerExceptionGroup(
    CustomerIdentity Customer,
    string DisplayLabel,
    ExceptionSeverity HighestSeverity,
    IReadOnlyDictionary<ExceptionSeverity, int> BySeverity,
    int RequiresActionNowCount,
    IReadOnlyList<SurfacedException> Exceptions);

/// <summary>Single operator-facing reconciliation exception.</summary>
public sealed record SurfacedException(
    SurfacedExceptionId Id,
    ExceptionCategory Category,
    ReconciliationDomain Domain,
    ExceptionSeverity Severity,
    CustomerIdentity Customer,
    ProductContext? Product,
    string Explanation,
    IReadOnlyList<ExceptionEvidence> Evidence,
    bool RequiresActionNow,
    ProposedChangeId? ProposedChangeId,
    int SuppressedSiblingCount,
    IReadOnlyList<MismatchId> SourceMismatchIds,
    MatchGroupId? MatchGroupId = null);

/// <summary>Commercial product context for an exception when known.</summary>
public sealed record ProductContext(
    CommercialKey? CommercialKey,
    string DisplayLabel,
    OfferId? OfferId,
    SkuId? SkuId);

/// <summary>One labelled evidence datum supporting operator verification.</summary>
public sealed record ExceptionEvidence(
    EvidenceSource Source,
    string Field,
    string Value,
    string? EntityRef = null);
