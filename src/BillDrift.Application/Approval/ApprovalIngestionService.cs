using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Approval;

/// <summary>Maps reconciliation run output into approval proposal snapshots.</summary>
public sealed class ApprovalIngestionService
{
    private readonly ApprovalEligibilityEvaluator _eligibilityEvaluator;

    /// <summary>Creates the ingestion service.</summary>
    public ApprovalIngestionService(ApprovalEligibilityEvaluator eligibilityEvaluator)
    {
        _eligibilityEvaluator = eligibilityEvaluator;
    }

    /// <summary>
    /// Builds proposal snapshots from a reconciliation run and applies supersession rules.
    /// </summary>
    public async Task<ApprovalIngestionResult> IngestAsync(
        ApprovalIngestionRequest request,
        IApprovalStore store,
        string operatorId,
        CancellationToken cancellationToken = default)
    {
        var run = request.Run;
        var existing = await store.ListProposalsByRunAsync(run.Id, cancellationToken);
        var proposals = new List<ApprovalProposal>();
        var supersededCount = 0;
        var now = DateTimeOffset.UtcNow;

        var mismatchById = run.Mismatches.ToDictionary(m => m.Id);
        var matchGroupById = run.MatchGroups.ToDictionary(g => g.Id);
        var exceptionByProposedChange = request.Exceptions.FlatExceptions()
            .Where(e => e.ProposedChangeId is not null)
            .GroupBy(e => e.ProposedChangeId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var proposedChange in run.ProposedChanges)
        {
            mismatchById.TryGetValue(proposedChange.MismatchId, out var mismatch);
            EntityMatchGroup? matchGroup = null;
            SurfacedException? linkedException = null;

            if (exceptionByProposedChange.TryGetValue(proposedChange.Id, out linkedException) &&
                linkedException.MatchGroupId is not null)
            {
                matchGroupById.TryGetValue(linkedException.MatchGroupId.Value, out matchGroup);
            }

            var evaluation = _eligibilityEvaluator.EvaluateProposedChange(
                proposedChange,
                mismatch,
                matchGroup,
                linkedException,
                request.Classifications,
                proposals);

            var prior = BuildPriorValues(mismatch, proposedChange);
            var customer = ResolveCustomer(mismatch, matchGroup);
            var productLabel = ResolveProductLabel(mismatch, proposedChange, linkedException);
            var commercialRoot = proposedChange.CataloguePayload?.CommercialKeyRoot;

            var existingProposal = await store.GetProposalByIdempotencyKeyAsync(
                run.Id,
                proposedChange.IdempotencyKey,
                cancellationToken);

            var initialState = existingProposal?.State switch
            {
                ApprovalDecisionState.Approved or ApprovalDecisionState.Rejected => existingProposal.State,
                _ => ApprovalDecisionState.Pending
            };

            var proposalId = existingProposal?.Id ?? ApprovalProposalId.New();
            var proposal = new ApprovalProposal(
                proposalId,
                run.Id,
                proposedChange.Id,
                proposedChange.IdempotencyKey,
                proposedChange.MismatchId,
                evaluation.Category,
                proposedChange.ActionType,
                initialState,
                evaluation.Eligibility,
                evaluation.Reason,
                customer,
                productLabel,
                commercialRoot,
                prior,
                proposedChange.ProposedValues,
                proposedChange.ExecutionOrder,
                [],
                evaluation.RiskIndicator,
                existingProposal?.IngestedAt ?? now,
                existingProposal?.SupersededByRunId,
                existingProposal?.ApprovedWhileEligible ?? false,
                operatorId,
                now);

            proposals.Add(proposal);
            await store.UpsertProposalAsync(proposal, cancellationToken);

            supersededCount += await SupersedePriorRunsAsync(
                store,
                run.Id,
                proposedChange.MismatchId,
                proposedChange.ActionType,
                cancellationToken);
        }

        if (request.IncludeInvestigationItems)
        {
            foreach (var exception in request.Exceptions.FlatExceptions()
                         .Where(e => e.ProposedChangeId is null && IsInvestigationCategory(e.Category)))
            {
                var evaluation = _eligibilityEvaluator.EvaluateInvestigation(exception);
                var idempotencyKey = new IdempotencyKey($"{run.Id.Value}:investigation:{exception.Id.Value}");
                var proposal = new ApprovalProposal(
                    ApprovalProposalId.New(),
                    run.Id,
                    null,
                    idempotencyKey,
                    exception.SourceMismatchIds.FirstOrDefault(),
                    evaluation.Category,
                    null,
                    ApprovalDecisionState.Pending,
                    evaluation.Eligibility,
                    evaluation.Reason,
                    exception.Customer.MexId,
                    exception.Product?.DisplayLabel ?? "Investigation item",
                    exception.Product?.CommercialKey is { } commercialKey
                        ? CommercialKeyRoot.Create(commercialKey.OfferId, commercialKey.SkuId)
                        : null,
                    BuildInvestigationPriorValues(exception),
                    new Dictionary<string, string>(),
                    int.MaxValue,
                    Array.Empty<ApprovalProposalId>(),
                    evaluation.RiskIndicator,
                    now,
                    null,
                    false,
                    operatorId,
                    now);

                proposals.Add(proposal);
                await store.UpsertProposalAsync(proposal, cancellationToken);
            }
        }

        supersededCount += await ApplySupersessionAuditAsync(run, store, operatorId, cancellationToken);

        return new ApprovalIngestionResult(
            run.Id,
            proposals.Count,
            proposals.Count(p => p.State == ApprovalDecisionState.Pending),
            proposals.Count(p => p.Eligibility == ApprovalEligibility.InvestigationOnly),
            supersededCount);
    }

    private static async Task<int> SupersedePriorRunsAsync(
        IApprovalStore store,
        RunId currentRunId,
        MismatchId mismatchId,
        ProposedActionType actionType,
        CancellationToken cancellationToken)
    {
        var prior = await store.FindPriorProposalsAsync(mismatchId, actionType, currentRunId, cancellationToken);
        var superseded = 0;

        foreach (var existing in prior)
        {
            if (existing.State == ApprovalDecisionState.Pending)
            {
                await store.UpsertProposalAsync(
                    existing with
                    {
                        State = ApprovalDecisionState.Stale,
                        SupersededByRunId = currentRunId,
                        LastUpdatedAt = DateTimeOffset.UtcNow
                    },
                    cancellationToken);
                superseded++;
            }
            else if (existing.State is ApprovalDecisionState.Approved or ApprovalDecisionState.Rejected)
            {
                await store.UpsertProposalAsync(
                    existing with
                    {
                        State = ApprovalDecisionState.Historical,
                        SupersededByRunId = currentRunId,
                        LastUpdatedAt = DateTimeOffset.UtcNow
                    },
                    cancellationToken);
                superseded++;
            }
        }

        return superseded;
    }

    private static async Task<int> ApplySupersessionAuditAsync(
        ReconciliationRun run,
        IApprovalStore store,
        string operatorId,
        CancellationToken cancellationToken)
    {
        await store.AppendAuditEventAsync(
            new ApprovalAuditEvent(
                Guid.NewGuid(),
                ApprovalAuditEventType.Ingest,
                run.Id,
                null,
                operatorId,
                DateTimeOffset.UtcNow,
                $"Ingested approval proposals for run {run.Id.Value}",
                null),
            cancellationToken);

        return 0;
    }

    private static IReadOnlyDictionary<string, string> BuildPriorValues(Mismatch? mismatch, ProposedChange proposedChange)
    {
        if (mismatch?.ExpectedValue is not null || mismatch?.ActualValue is not null)
        {
            var prior = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (mismatch.ExpectedValue is not null)
            {
                prior["expected"] = mismatch.ExpectedValue;
            }

            if (mismatch.ActualValue is not null)
            {
                prior["actual"] = mismatch.ActualValue;
            }

            return prior;
        }

        return proposedChange.ProposedValues.ToDictionary(
            kvp => $"prior_{kvp.Key}",
            kvp => string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> BuildInvestigationPriorValues(SurfacedException exception) =>
        exception.Evidence.ToDictionary(e => e.Field, e => e.Value, StringComparer.OrdinalIgnoreCase);

    private static MexId ResolveCustomer(Mismatch? mismatch, EntityMatchGroup? matchGroup) =>
        mismatch?.Customer?.MexId ?? matchGroup?.Customer.MexId ?? MexId.Create("unknown");

    private static string ResolveProductLabel(
        Mismatch? mismatch,
        ProposedChange proposedChange,
        SurfacedException? linkedException)
    {
        if (proposedChange.CataloguePayload?.NormalizedName is { Length: > 0 } name)
        {
            return name;
        }

        if (linkedException?.Product?.DisplayLabel is { Length: > 0 } label)
        {
            return label;
        }

        return mismatch?.CommercialKey?.ToString() ?? proposedChange.ActionType.ToString();
    }

    private static bool IsInvestigationCategory(ExceptionCategory category) =>
        category is ExceptionCategory.OfferSkuAmbiguousMapping
            or ExceptionCategory.NonCspManualReview
            or ExceptionCategory.StripeProductMissing
            or ExceptionCategory.StripePriceMissing
            or ExceptionCategory.ProductMismatch;
}
