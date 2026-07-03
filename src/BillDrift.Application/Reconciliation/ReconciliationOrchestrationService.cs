using BillDrift.Application.History;
using BillDrift.Application.Ingestion;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation;

/// <summary>
/// Loads ingestion snapshots, executes the reconciliation engine, surfaces exceptions, and archives runs.
/// </summary>
public sealed class ReconciliationOrchestrationService
{
    private readonly IIngestionBlobStore _blobStore;
    private readonly IIngestionRunIndexStore _indexStore;
    private readonly IReconciliationEngine _engine;
    private readonly ExceptionSurfacingService _surfacing;
    private readonly RunArchiveService _archiveService;
    private readonly RunHistoryService _historyService;

    public ReconciliationOrchestrationService(
        IIngestionBlobStore blobStore,
        IIngestionRunIndexStore indexStore,
        IReconciliationEngine engine,
        ExceptionSurfacingService surfacing,
        RunArchiveService archiveService,
        RunHistoryService historyService)
    {
        _blobStore = blobStore;
        _indexStore = indexStore;
        _engine = engine;
        _surfacing = surfacing;
        _archiveService = archiveService;
        _historyService = historyService;
    }

    /// <summary>Executes reconciliation from ingestion run references.</summary>
    public async Task<ReconciliationRunResponse> ExecuteAsync(
        StartReconciliationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!HasAnyIngestionId(request))
        {
            throw new ReconciliationOrchestrationException("At least one ingestion run ID is required.");
        }

        var inputs = await LoadInputsAsync(request, cancellationToken);
        var options = request.Options ?? new ReconciliationOptions();
        var run = _engine.Execute(new ReconciliationRequest(
            RunId: null,
            request.BillingPeriod,
            inputs,
            options));

        var exceptions = _surfacing.Surface(run, options);
        ReconciliationRunRecord? archiveRecord = null;

        if (request.PersistRun)
        {
            var context = BuildArchiveContext(request, inputs);
            archiveRecord = await _archiveService.PersistAsync(
                new PersistRunRequest(run, context),
                cancellationToken);
        }

        return BuildResponse(run, exceptions, archiveRecord);
    }

    /// <summary>Loads a persisted reconciliation run response from run history.</summary>
    public async Task<ReconciliationRunResponse?> GetRunAsync(
        RunId runId,
        bool includeResults = true,
        CancellationToken cancellationToken = default)
    {
        var run = await _historyService.LoadArchivedReconciliationRunAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var exceptions = includeResults
            ? _surfacing.Surface(run, options: null)
            : new ReconciliationExceptionViewModel(
                run.Id,
                run.Scope,
                DateTimeOffset.UtcNow,
                new ExceptionRunSummary(0, new Dictionary<ExceptionSeverity, int>(), new Dictionary<ExceptionCategory, int>(), new Dictionary<ReconciliationDomain, int>(), 0, 0, 0),
                []);

        var record = await _historyService.GetRunSummaryAsync(runId, cancellationToken);
        return BuildResponse(run, exceptions, record);
    }

    /// <summary>Builds margin display rows for a reconciliation run.</summary>
    public async Task<IReadOnlyList<MarginLineViewModel>> GetMarginLinesAsync(
        RunId runId,
        CancellationToken cancellationToken = default)
    {
        var run = await _historyService.LoadArchivedReconciliationRunAsync(runId, cancellationToken)
            ?? throw new RunNotFoundException(runId);

        return BuildMarginLines(run);
    }

    private async Task<ReconciliationInputs> LoadInputsAsync(
        StartReconciliationRunRequest request,
        CancellationToken cancellationToken)
    {
        var missing = new List<string>();

        IReadOnlyList<SupplierCostLine> supplierCost = [];
        if (request.SupplierCostIngestionId is { } pdfId)
        {
            var loaded = await _blobStore.GetSupplierCostLinesAsync(pdfId, cancellationToken);
            if (loaded is null)
            {
                missing.Add($"SupplierCost ({pdfId})");
            }
            else
            {
                supplierCost = loaded;
            }
        }

        IReadOnlyList<MicrosoftSubscriptionLine> subscriptionTruth = [];
        if (request.SubscriptionTruthIngestionId is { } subId)
        {
            var loaded = await _blobStore.GetSubscriptionTruthAsync(subId, cancellationToken);
            if (loaded is null)
            {
                missing.Add($"SubscriptionTruth ({subId})");
            }
            else
            {
                subscriptionTruth = loaded;
            }
        }

        IReadOnlyList<IntendedPrice> intendedPricing = [];
        if (request.IntendedPricingIngestionId is { } priceId)
        {
            var loaded = await _blobStore.GetResolvedPricesAsync(priceId, cancellationToken);
            if (loaded is null)
            {
                missing.Add($"IntendedPricing ({priceId})");
            }
            else
            {
                intendedPricing = loaded;
            }
        }

        IReadOnlyList<StripeBillingItem> stripeBilling = [];
        if (request.StripeBillingIngestionId is { } stripeId)
        {
            var loaded = await _blobStore.GetStripeBillingItemsAsync(stripeId, cancellationToken);
            if (loaded is null)
            {
                missing.Add($"StripeBilling ({stripeId})");
            }
            else
            {
                stripeBilling = loaded;
            }
        }

        if (missing.Count > 0)
        {
            throw new ReconciliationOrchestrationException(
                $"Missing or invalid ingestion references: {string.Join(", ", missing)}");
        }

        return new ReconciliationInputs(
            supplierCost,
            subscriptionTruth,
            intendedPricing,
            stripeBilling,
            request.ProductMappings ?? []);
    }

    private static bool HasAnyIngestionId(StartReconciliationRunRequest request) =>
        request.SupplierCostIngestionId is not null ||
        request.SubscriptionTruthIngestionId is not null ||
        request.IntendedPricingIngestionId is not null ||
        request.StripeBillingIngestionId is not null;

    private static RunArchiveContext BuildArchiveContext(
        StartReconciliationRunRequest request,
        ReconciliationInputs inputs)
    {
        var metadata = new Dictionary<InputDomainType, InputSnapshotMetadata>
        {
            [InputDomainType.SupplierCost] = Snapshot(InputDomainType.SupplierCost, inputs.SupplierCostLines.Count),
            [InputDomainType.SubscriptionTruth] = Snapshot(InputDomainType.SubscriptionTruth, inputs.SubscriptionLines.Count),
            [InputDomainType.IntendedPricing] = Snapshot(InputDomainType.IntendedPricing, inputs.IntendedPrices.Count),
            [InputDomainType.StripeBilling] = Snapshot(InputDomainType.StripeBilling, inputs.StripeItems.Count),
            [InputDomainType.ProductMappings] = Snapshot(InputDomainType.ProductMappings, inputs.ProductMappings.Count)
        };

        return new RunArchiveContext(
            request.InitiatorId,
            metadata,
            new MappingVersionReference("session", "inline", DateOnly.FromDateTime(DateTime.UtcNow), "Session mappings"),
            DateTimeOffset.UtcNow);
    }

    private static InputSnapshotMetadata Snapshot(InputDomainType domain, int count) =>
        new(domain, count > 0, RecordCount: count);

    private static ReconciliationRunResponse BuildResponse(
        ReconciliationRun run,
        ReconciliationExceptionViewModel exceptions,
        ReconciliationRunRecord? archiveRecord) =>
        new(
            run.Id.Value,
            run.Scope,
            new ReconciliationRunSummary(
                run.Mismatches.Count,
                run.ProposedChanges.Count,
                run.Mismatches
                    .GroupBy(m => m.Type.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                run.Mismatches.Count == 0),
            exceptions,
            BuildMarginLines(run),
            archiveRecord is not null,
            archiveRecord);

    private static IReadOnlyList<MarginLineViewModel> BuildMarginLines(ReconciliationRun run)
    {
        var lines = new List<MarginLineViewModel>();

        foreach (var mismatch in run.Mismatches.Where(m => m.ExpectedValue is not null || m.ActualValue is not null))
        {
            var cost = TryParseMoney(mismatch.ActualValue);
            var rrp = TryParseMoney(mismatch.ExpectedValue);
            Money? marginAmount = cost is not null && rrp is not null
                ? Money.Gbp(rrp.Value.Amount - cost.Value.Amount)
                : null;
            decimal? marginPercent = marginAmount is not null && rrp is not null && rrp.Value.Amount > 0
                ? Math.Round(marginAmount.Value.Amount / rrp.Value.Amount * 100m, 2)
                : null;

            lines.Add(new MarginLineViewModel(
                mismatch.Customer?.DisplayName ?? mismatch.Customer?.MexId.Value ?? "Unknown",
                mismatch.CommercialKey?.ToString() ?? mismatch.Description ?? "Product",
                cost,
                rrp,
                marginAmount,
                marginPercent,
                ClassifyMargin(marginPercent)));
        }

        return lines;
    }

    private static Money? TryParseMoney(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = raw.Trim().Replace("£", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(cleaned, out var amount) ? Money.Gbp(amount) : null;
    }

    private static MarginSeverity ClassifyMargin(decimal? marginPercent) => marginPercent switch
    {
        null => MarginSeverity.Unknown,
        < 0 => MarginSeverity.Negative,
        < 10 => MarginSeverity.Low,
        _ => MarginSeverity.Healthy
    };
}

/// <summary>Thrown when orchestration pre-checks fail.</summary>
public sealed class ReconciliationOrchestrationException : Exception
{
    public ReconciliationOrchestrationException(string message) : base(message)
    {
    }
}
