using System.Collections.Concurrent;
using BillDrift.Application.Approval;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.Approval;

/// <summary>In-memory approval store for unit and service tests.</summary>
public sealed class InMemoryApprovalStore : IApprovalStore
{
    private readonly ConcurrentDictionary<string, ApprovalProposal> _proposals = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<ApprovalDecision> _decisions = [];
    private readonly ConcurrentBag<ApprovalAuditEvent> _auditEvents = [];

    /// <inheritdoc />
    public Task UpsertProposalAsync(ApprovalProposal proposal, CancellationToken cancellationToken = default)
    {
        _proposals[Key(proposal.RunId, proposal.IdempotencyKey)] = proposal;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ApprovalProposal?> GetProposalAsync(
        RunId runId,
        ApprovalProposalId proposalId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_proposals.Values.FirstOrDefault(p => p.RunId == runId && p.Id == proposalId));

    /// <inheritdoc />
    public Task<ApprovalProposal?> GetProposalByIdempotencyKeyAsync(
        RunId runId,
        IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_proposals.TryGetValue(Key(runId, idempotencyKey), out var proposal) ? proposal : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalProposal>> ListProposalsByRunAsync(
        RunId runId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ApprovalProposal>>(_proposals.Values.Where(p => p.RunId == runId).ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalProposal>> ListProposalsByCustomerAsync(
        RunId runId,
        MexId customerMexId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ApprovalProposal>>(
            _proposals.Values
                .Where(p => p.RunId == runId && p.CustomerMexId.Value == customerMexId.Value)
                .ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalProposal>> FindPriorProposalsAsync(
        MismatchId mismatchId,
        ProposedActionType? actionType,
        RunId currentRunId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ApprovalProposal>>(
            _proposals.Values
                .Where(p => p.RunId != currentRunId && p.MismatchId == mismatchId &&
                            (actionType is null || p.ActionType == actionType))
                .ToList());

    /// <inheritdoc />
    public Task AppendDecisionAsync(ApprovalDecision decision, CancellationToken cancellationToken = default)
    {
        _decisions.Add(decision);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AppendAuditEventAsync(ApprovalAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _auditEvents.Add(auditEvent);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalAuditEvent>> ListAuditEventsAsync(
        RunId runId,
        ApprovalProposalId? proposalId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ApprovalAuditEvent>>(
            _auditEvents
                .Where(e => e.RunId == runId && (proposalId is null || e.ProposalId == proposalId))
                .OrderByDescending(e => e.Timestamp)
                .ToList());

    /// <inheritdoc />
    public Task SaveExportMetadataAsync(
        Guid exportId,
        RunId runId,
        string exportedBy,
        string blobPath,
        int entryCount,
        string contentHash,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private static string Key(RunId runId, IdempotencyKey idempotencyKey) =>
        $"{runId.Value:N}:{idempotencyKey.Value}";
}

/// <summary>Pass-through exporter for unit tests without blob storage.</summary>
public sealed class PassThroughApprovedChangesetExporter : IApprovedChangesetExporter
{
    private readonly Dictionary<string, ApprovedChangeset> _exports = [];

    /// <inheritdoc />
    public Task<ApprovedChangeset> ExportAsync(ApprovedChangeset changeset, CancellationToken cancellationToken = default)
    {
        var stored = changeset with { BlobUri = $"memory://{changeset.ExportId}" };
        _exports[$"{changeset.RunId.Value}/{changeset.ExportId}.json"] = stored;
        return Task.FromResult(stored);
    }

    /// <inheritdoc />
    public Task<string> DownloadAsync(string blobPath, CancellationToken cancellationToken = default) =>
        _exports.TryGetValue(blobPath, out var changeset)
            ? Task.FromResult($"{{\"exportId\":\"{changeset.ExportId}\"}}")
            : throw new KeyNotFoundException($"No exported changeset at '{blobPath}'.");
}
