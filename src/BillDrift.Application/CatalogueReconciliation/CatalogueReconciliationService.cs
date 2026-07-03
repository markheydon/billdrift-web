using BillDrift.Application.Approval;
using BillDrift.Application.Ingestion;
using BillDrift.Domain.Approval;
using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Application.CatalogueReconciliation;

/// <summary>Loads inputs, executes the engine, persists runs, and optionally ingests approvals.</summary>
public sealed class CatalogueReconciliationService : ICatalogueReconciliationService
{
    private readonly ICatalogueReconciliationEngine _engine;
    private readonly ICatalogueReconciliationStore _store;
    private readonly IIngestionBlobStore _ingestionBlobStore;
    private readonly CatalogueApprovalAdapter _approvalAdapter;
    private readonly IApprovalStore _approvalStore;
    private readonly IOperatorContext _operatorContext;

    /// <summary>Creates the catalogue reconciliation service.</summary>
    public CatalogueReconciliationService(
        ICatalogueReconciliationEngine engine,
        ICatalogueReconciliationStore store,
        IIngestionBlobStore ingestionBlobStore,
        CatalogueApprovalAdapter approvalAdapter,
        IApprovalStore approvalStore,
        IOperatorContext operatorContext)
    {
        _engine = engine;
        _store = store;
        _ingestionBlobStore = ingestionBlobStore;
        _approvalAdapter = approvalAdapter;
        _approvalStore = approvalStore;
        _operatorContext = operatorContext;
    }

    /// <inheritdoc />
    public async Task<CatalogueReconciliationRun> RunAsync(
        CatalogueReconciliationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (stripeProducts, stripePrices) = await LoadStripeCatalogueAsync(request, cancellationToken);

        var intendedPrices = request.PricingIngestionRunId is { } pricingId
            ? await _ingestionBlobStore.GetResolvedPricesAsync(pricingId, cancellationToken) ?? []
            : [];

        var inputs = new CatalogueReconciliationInputs(
            stripeProducts,
            stripePrices,
            request.ProductMappings,
            intendedPrices,
            new CatalogueInputReferences(
                request.StripeIngestionRunId,
                request.PricingIngestionRunId,
                null,
                null));

        // Engine throws CatalogueReconciliationValidationException on invalid input, so an invalid
        // run is never persisted below.
        var run = _engine.Execute(inputs, request.Options);
        await _store.SaveRunAsync(run, cancellationToken);

        if (request.IngestToApprovalQueue)
        {
            await IngestApprovalsAsync(run.RunId, cancellationToken);
        }

        return run;
    }

    /// <summary>
    /// Resolves the Stripe catalogue snapshot for a run. An inline snapshot on the request takes
    /// precedence; otherwise the snapshot archived for the referenced Stripe ingestion run is loaded.
    /// When a run ID is supplied the caller expects catalogue data, so absent blobs indicate a broken
    /// or not-yet-persisted import and cause an immediate failure rather than a silent empty catalogue.
    /// </summary>
    private async Task<(IReadOnlyList<StripeCatalogueProduct> Products, IReadOnlyList<StripeCataloguePrice> Prices)> LoadStripeCatalogueAsync(
        CatalogueReconciliationRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StripeProducts is { Count: > 0 } || request.StripePrices is { Count: > 0 })
        {
            return (request.StripeProducts ?? [], request.StripePrices ?? []);
        }

        if (request.StripeIngestionRunId is not { } stripeIngestionRunId)
        {
            return ([], []);
        }

        var products = await _ingestionBlobStore.GetStripeCatalogueProductsAsync(stripeIngestionRunId, cancellationToken);
        if (products is null)
        {
            throw new CatalogueReconciliationValidationException(
                $"Stripe ingestion run '{stripeIngestionRunId:D}' has no archived catalogue products. " +
                "Ensure Stripe catalogue ingestion has persisted its snapshot before running reconciliation against this run ID.");
        }

        var prices = await _ingestionBlobStore.GetStripeCataloguePricesAsync(stripeIngestionRunId, cancellationToken) ?? [];
        return (products, prices);
    }

    /// <inheritdoc />
    public Task<CatalogueReconciliationRun?> GetRunAsync(CatalogueRunId runId, CancellationToken cancellationToken = default) =>
        _store.GetRunAsync(runId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<CatalogueRunListItem>> ListRunsAsync(int limit, CancellationToken cancellationToken = default) =>
        _store.ListRunsAsync(limit, cancellationToken);

    /// <inheritdoc />
    public async Task<CatalogueApprovalIngestionResult> IngestApprovalsAsync(
        CatalogueRunId runId,
        CancellationToken cancellationToken = default)
    {
        var run = await _store.GetRunAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Catalogue run {runId.Value:D} not found.");

        var proposals = _approvalAdapter.ToApprovalProposals(run, _operatorContext.OperatorId);
        var ingested = 0;
        var skipped = 0;

        foreach (var proposal in proposals)
        {
            if (proposal.Eligibility == ApprovalEligibility.CatalogueConflict)
            {
                skipped++;
            }

            var existing = await _approvalStore.GetProposalByIdempotencyKeyAsync(
                proposal.RunId,
                proposal.IdempotencyKey,
                cancellationToken);

            if (existing is not null)
            {
                continue;
            }

            await _approvalStore.UpsertProposalAsync(proposal, cancellationToken);
            if (proposal.Eligibility == ApprovalEligibility.Eligible)
            {
                ingested++;
            }
        }

        return new CatalogueApprovalIngestionResult(
            ingested,
            skipped,
            $"catalogue-run:{runId.Value:D}");
    }
}
