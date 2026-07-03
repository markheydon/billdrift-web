using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Mapping;

namespace BillDrift.Application.CatalogueReconciliation;

/// <summary>Orchestrates catalogue reconciliation runs with optional persistence and approval ingestion.</summary>
public interface ICatalogueReconciliationService
{
    /// <summary>Runs catalogue reconciliation from assembled or loaded inputs.</summary>
    Task<CatalogueReconciliationRun> RunAsync(
        CatalogueReconciliationRunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a persisted catalogue reconciliation run.</summary>
    Task<CatalogueReconciliationRun?> GetRunAsync(CatalogueRunId runId, CancellationToken cancellationToken = default);

    /// <summary>Lists recent catalogue reconciliation runs.</summary>
    Task<IReadOnlyList<CatalogueRunListItem>> ListRunsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests actionable proposed fixes from a catalogue run into the approval queue,
    /// attributed to the authenticated operator resolved from the request context.
    /// </summary>
    Task<CatalogueApprovalIngestionResult> IngestApprovalsAsync(
        CatalogueRunId runId,
        CancellationToken cancellationToken = default);
}

/// <summary>Request to trigger a catalogue reconciliation run.</summary>
public sealed record CatalogueReconciliationRunRequest(
    Guid? StripeIngestionRunId,
    Guid? PricingIngestionRunId,
    IReadOnlyList<ProductMapping> ProductMappings,
    IReadOnlyList<StripeCatalogueProduct>? StripeProducts = null,
    IReadOnlyList<StripeCataloguePrice>? StripePrices = null,
    CatalogueReconciliationOptions? Options = null,
    bool IngestToApprovalQueue = false);

/// <summary>Result of ingesting catalogue fixes into approval workflow.</summary>
public sealed record CatalogueApprovalIngestionResult(
    int IngestedCount,
    int SkippedManualOnly,
    string ApprovalRunReference);
