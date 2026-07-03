using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Mapping;

namespace BillDrift.Application.Reconciliation;

/// <summary>HTTP request to start a reconciliation run.</summary>
public sealed record StartReconciliationRunRequest(
    BillingPeriod BillingPeriod,
    Guid? SupplierCostIngestionId = null,
    Guid? SubscriptionTruthIngestionId = null,
    Guid? IntendedPricingIngestionId = null,
    Guid? StripeBillingIngestionId = null,
    IReadOnlyList<ProductMapping>? ProductMappings = null,
    ReconciliationOptions? Options = null,
    bool PersistRun = true,
    string? InitiatorId = null);

/// <summary>Summary counts for a reconciliation run response.</summary>
public sealed record ReconciliationRunSummary(
    int MismatchCount,
    int ProposedChangeCount,
    IReadOnlyDictionary<string, int> MismatchesByCategory,
    bool CleanRun);

/// <summary>Margin display severity for operator dashboards.</summary>
public enum MarginSeverity
{
    Healthy,
    Low,
    Negative,
    Unknown
}

/// <summary>Margin display row derived from reconciliation evidence.</summary>
public sealed record MarginLineViewModel(
    string CustomerLabel,
    string ProductLabel,
    Money? Cost,
    Money? Rrp,
    Money? MarginAmount,
    decimal? MarginPercent,
    MarginSeverity Severity);

/// <summary>HTTP response for reconciliation orchestration.</summary>
public sealed record ReconciliationRunResponse(
    Guid RunId,
    BillingPeriod BillingPeriod,
    ReconciliationRunSummary Summary,
    ReconciliationExceptionViewModel Exceptions,
    IReadOnlyList<MarginLineViewModel> MarginLines,
    bool Archived,
    ReconciliationRunRecord? ArchiveRecord = null);

/// <summary>Optional body for ingest-from-run convenience endpoint.</summary>
public sealed record IngestApprovalsFromRunRequest(bool IncludeInvestigationItems = true);
