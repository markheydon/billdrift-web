using System.Security.Cryptography;
using System.Text;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Approval;

/// <summary>Operator-facing orchestration for the approval workflow.</summary>
public sealed class ApprovalService
{
    private readonly IApprovalStore _store;
    private readonly ApprovalIngestionService _ingestionService;
    private readonly ApprovedChangesetBuilder _changesetBuilder;
    private readonly IApprovedChangesetExporter _exporter;
    private readonly IOperatorContext _operatorContext;

    /// <summary>Creates the approval service.</summary>
    public ApprovalService(
        IApprovalStore store,
        ApprovalIngestionService ingestionService,
        ApprovedChangesetBuilder changesetBuilder,
        IApprovedChangesetExporter exporter,
        IOperatorContext operatorContext)
    {
        _store = store;
        _ingestionService = ingestionService;
        _changesetBuilder = changesetBuilder;
        _exporter = exporter;
        _operatorContext = operatorContext;
    }

    /// <inheritdoc cref="ApprovalIngestionService.IngestAsync"/>
    public Task<ApprovalIngestionResult> IngestAsync(
        ApprovalIngestionRequest request,
        CancellationToken cancellationToken = default) =>
        _ingestionService.IngestAsync(request, _store, _operatorContext.OperatorId, cancellationToken);

    /// <summary>Loads the approval queue for a reconciliation run.</summary>
    public async Task<ApprovalQueueViewModel> GetQueueAsync(
        RunId runId,
        ApprovalQueueOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ApprovalQueueOptions();
        var proposals = await _store.ListProposalsByRunAsync(runId, cancellationToken);

        if (options.CustomerMexId is not null)
        {
            proposals = proposals
                .Where(p => p.CustomerMexId.Value == options.CustomerMexId.Value.Value)
                .ToList();
        }

        var groups = proposals
            .GroupBy(p => p.CustomerMexId.Value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var label = g.First().ProductLabel;
                return new ApprovalCustomerGroupViewModel(
                    g.First().CustomerMexId,
                    label,
                    g.Where(p => p.Category == ApprovalProposalCategory.Subscription)
                        .Select(MapViewModel)
                        .ToList(),
                    options.IncludeCatalogue
                        ? g.Where(p => p.Category == ApprovalProposalCategory.Catalogue)
                            .Select(MapViewModel)
                            .ToList()
                        : [],
                    options.IncludeInvestigation
                        ? g.Where(p => p.Category == ApprovalProposalCategory.Investigation)
                            .Select(MapViewModel)
                            .ToList()
                        : []);
            })
            .ToList();

        var summary = new ApprovalQueueSummary(
            proposals.Count(p => p.State == ApprovalDecisionState.Pending),
            proposals.Count(p => p.State == ApprovalDecisionState.Approved),
            proposals.Count(p => p.State == ApprovalDecisionState.Rejected),
            proposals.Count(p => p.State == ApprovalDecisionState.Stale),
            proposals.Count(p => p.Eligibility == ApprovalEligibility.InvestigationOnly),
            proposals.Count(p => p.Category == ApprovalProposalCategory.Catalogue),
            proposals.Count(p => p.Category == ApprovalProposalCategory.Subscription));

        return new ApprovalQueueViewModel(runId, groups, summary);
    }

    /// <summary>Approves a single proposal.</summary>
    public async Task<ApprovalDecision> ApproveAsync(
        ApproveProposalCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureCanApprove();

        var proposal = await RequireProposalAsync(command.RunId, command.ProposalId, cancellationToken);
        ValidateApprovable(proposal, command.AcknowledgeStale);

        var priorState = proposal.State;
        var updated = proposal with
        {
            State = ApprovalDecisionState.Approved,
            ApprovedWhileEligible = proposal.Eligibility == ApprovalEligibility.Eligible,
            LastOperatorId = _operatorContext.OperatorId,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await _store.UpsertProposalAsync(updated, cancellationToken);

        var decision = new ApprovalDecision(
            proposal.Id,
            command.RunId,
            priorState,
            ApprovalDecisionState.Approved,
            _operatorContext.OperatorId,
            DateTimeOffset.UtcNow,
            null,
            command.AcknowledgeStale);

        await _store.AppendDecisionAsync(decision, cancellationToken);
        await AppendAuditAsync(
            command.RunId,
            proposal.Id,
            ApprovalAuditEventType.Decision,
            $"Approved proposal {proposal.Id.Value}",
            cancellationToken);

        return decision;
    }

    /// <summary>Rejects a single proposal with a mandatory reason.</summary>
    public async Task<ApprovalDecision> RejectAsync(
        RejectProposalCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureCanApprove();

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new ApprovalValidationException("Rejection reason is required.");
        }

        var proposal = await RequireProposalAsync(command.RunId, command.ProposalId, cancellationToken);
        if (proposal.State is not (ApprovalDecisionState.Pending or ApprovalDecisionState.Stale))
        {
            throw new ApprovalStateException($"Cannot reject proposal in state {proposal.State}.");
        }

        var priorState = proposal.State;
        var updated = proposal with
        {
            State = ApprovalDecisionState.Rejected,
            LastOperatorId = _operatorContext.OperatorId,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await _store.UpsertProposalAsync(updated, cancellationToken);

        var decision = new ApprovalDecision(
            proposal.Id,
            command.RunId,
            priorState,
            ApprovalDecisionState.Rejected,
            _operatorContext.OperatorId,
            DateTimeOffset.UtcNow,
            command.Reason.Trim(),
            false);

        await _store.AppendDecisionAsync(decision, cancellationToken);
        await AppendAuditAsync(
            command.RunId,
            proposal.Id,
            ApprovalAuditEventType.Decision,
            $"Rejected proposal {proposal.Id.Value}: {command.Reason.Trim()}",
            cancellationToken);

        return decision;
    }

    /// <summary>Previews a bulk approve operation and returns a confirmation token.</summary>
    public async Task<BulkApprovePreview> PreviewBulkApproveAsync(
        RunId runId,
        MexId customerMexId,
        IReadOnlyList<ApprovalProposalId> proposalIds,
        CancellationToken cancellationToken = default)
    {
        var proposals = await LoadAndValidateBulkTargetsAsync(runId, customerMexId, proposalIds, cancellationToken);
        var token = ComputeBulkToken(runId, customerMexId, proposalIds);

        return new BulkApprovePreview(
            token,
            proposals.Count,
            proposals.Count(p => p.Category == ApprovalProposalCategory.Subscription),
            proposals.Count(p => p.Category == ApprovalProposalCategory.Catalogue));
    }

    /// <summary>Bulk approves eligible pending proposals after token confirmation.</summary>
    public async Task<BulkApproveResult> BulkApproveAsync(
        BulkApproveCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureCanApprove();

        var expected = ComputeBulkToken(command.RunId, command.CustomerMexId, command.ProposalIds);
        if (!string.Equals(expected, command.ConfirmationToken, StringComparison.Ordinal))
        {
            throw new ApprovalStateException("Bulk approve confirmation token mismatch.");
        }

        var decisions = new List<ApprovalDecision>();
        foreach (var proposalId in command.ProposalIds)
        {
            var decision = await ApproveAsync(
                new ApproveProposalCommand(command.RunId, proposalId),
                cancellationToken);
            decisions.Add(decision);
        }

        await AppendAuditAsync(
            command.RunId,
            null,
            ApprovalAuditEventType.BulkDecision,
            $"Bulk approved {decisions.Count} proposals for {command.CustomerMexId.Value}",
            cancellationToken);

        return new BulkApproveResult(decisions);
    }

    /// <summary>Exports approved proposals to a deterministic changeset.</summary>
    public async Task<ApprovedChangeset> ExportApprovedChangesetAsync(
        ExportChangesetCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureCanApprove();

        var proposals = await _store.ListProposalsByRunAsync(command.RunId, cancellationToken);
        var approved = proposals
            .Where(p => p.State == ApprovalDecisionState.Approved && p.ApprovedWhileEligible)
            .Where(p => command.CustomerMexId is null || p.CustomerMexId.Value == command.CustomerMexId.Value.Value)
            .ToList();

        if (approved.Count == 0)
        {
            throw new ApprovalValidationException("No approved items available for export.");
        }

        var changeset = _changesetBuilder.Build(command.RunId, approved, _operatorContext.OperatorId);
        changeset = await _exporter.ExportAsync(changeset, cancellationToken);

        await AppendAuditAsync(
            command.RunId,
            null,
            ApprovalAuditEventType.Export,
            $"Exported {changeset.Entries.Count} approved entries",
            cancellationToken);

        return changeset;
    }

    /// <summary>Queries immutable audit history for a run.</summary>
    public Task<IReadOnlyList<ApprovalAuditEvent>> GetAuditHistoryAsync(
        RunId runId,
        ApprovalProposalId? proposalId = null,
        CancellationToken cancellationToken = default) =>
        _store.ListAuditEventsAsync(runId, proposalId, cancellationToken);

    private async Task<ApprovalProposal> RequireProposalAsync(
        RunId runId,
        ApprovalProposalId proposalId,
        CancellationToken cancellationToken)
    {
        var proposal = await _store.GetProposalAsync(runId, proposalId, cancellationToken);
        return proposal ?? throw new ApprovalNotFoundException($"Proposal {proposalId.Value} not found.");
    }

    private static void ValidateApprovable(ApprovalProposal proposal, bool acknowledgeStale)
    {
        if (proposal.Eligibility is ApprovalEligibility.InvestigationOnly or ApprovalEligibility.CatalogueConflict)
        {
            throw new ApprovalValidationException($"Proposal is ineligible: {proposal.Eligibility}.");
        }

        if (proposal.Eligibility == ApprovalEligibility.DependencyBlocked)
        {
            throw new ApprovalValidationException(proposal.EligibilityReason ?? "Dependency blocked.");
        }

        if (proposal.State == ApprovalDecisionState.Stale && !acknowledgeStale)
        {
            throw new ApprovalStateException("Stale proposal requires acknowledgeStale=true.");
        }

        if (proposal.State is not (ApprovalDecisionState.Pending or ApprovalDecisionState.Stale))
        {
            throw new ApprovalStateException($"Cannot approve proposal in state {proposal.State}.");
        }
    }

    private async Task<IReadOnlyList<ApprovalProposal>> LoadAndValidateBulkTargetsAsync(
        RunId runId,
        MexId customerMexId,
        IReadOnlyList<ApprovalProposalId> proposalIds,
        CancellationToken cancellationToken)
    {
        var proposals = new List<ApprovalProposal>();
        foreach (var id in proposalIds)
        {
            var proposal = await RequireProposalAsync(runId, id, cancellationToken);
            if (proposal.CustomerMexId.Value != customerMexId.Value)
            {
                throw new ApprovalValidationException("All bulk targets must belong to the specified customer.");
            }

            ValidateApprovable(proposal, acknowledgeStale: false);
            proposals.Add(proposal);
        }

        return proposals;
    }

    private static string ComputeBulkToken(
        RunId runId,
        MexId customerMexId,
        IReadOnlyList<ApprovalProposalId> proposalIds)
    {
        var payload = string.Join(
            '|',
            proposalIds.OrderBy(id => id.Value).Select(id => id.Value.ToString("N")));

        var bytes = Encoding.UTF8.GetBytes($"{runId.Value}:{customerMexId.Value}:{payload}");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private void EnsureCanApprove()
    {
        if (!_operatorContext.CanApprove)
        {
            throw new ApprovalValidationException("Operator is read-only.");
        }
    }

    private async Task AppendAuditAsync(
        RunId runId,
        ApprovalProposalId? proposalId,
        ApprovalAuditEventType eventType,
        string summary,
        CancellationToken cancellationToken)
    {
        await _store.AppendAuditEventAsync(
            new ApprovalAuditEvent(
                Guid.NewGuid(),
                eventType,
                runId,
                proposalId,
                _operatorContext.OperatorId,
                DateTimeOffset.UtcNow,
                summary,
                null),
            cancellationToken);
    }

    private static ApprovalProposalViewModel MapViewModel(ApprovalProposal proposal)
    {
        var canApprove = proposal.State is ApprovalDecisionState.Pending or ApprovalDecisionState.Stale &&
                         proposal.Eligibility == ApprovalEligibility.Eligible;

        return new ApprovalProposalViewModel(
            proposal.Id,
            proposal.Category,
            proposal.ActionType,
            proposal.State,
            proposal.Eligibility,
            proposal.EligibilityReason,
            proposal.ProductLabel,
            proposal.PriorValues,
            proposal.ProposedValues,
            proposal.ExecutionOrder,
            proposal.RiskIndicator,
            canApprove,
            proposal.State is ApprovalDecisionState.Pending or ApprovalDecisionState.Stale,
            proposal.State == ApprovalDecisionState.Approved && proposal.ApprovedWhileEligible,
            proposal.IngestedAt);
    }
}

/// <summary>Thrown when approval validation fails.</summary>
public sealed class ApprovalValidationException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ApprovalValidationException(string message) : base(message)
    {
    }
}

/// <summary>Thrown when proposal state prevents the requested action.</summary>
public sealed class ApprovalStateException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ApprovalStateException(string message) : base(message)
    {
    }
}

/// <summary>Thrown when a proposal cannot be found.</summary>
public sealed class ApprovalNotFoundException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ApprovalNotFoundException(string message) : base(message)
    {
    }
}
