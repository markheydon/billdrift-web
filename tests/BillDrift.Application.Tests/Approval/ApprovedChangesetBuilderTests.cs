using BillDrift.Application.Approval;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.Approval;

public sealed class ApprovedChangesetBuilderTests
{
    [Fact]
    public void Export_includes_only_approved_items_per_quickstart_V5()
    {
        var builder = new ApprovedChangesetBuilder();
        var runId = RunId.New();
        var approved = CreateProposal(runId, ApprovalDecisionState.Approved, approvedWhileEligible: true);
        var pending = CreateProposal(runId, ApprovalDecisionState.Pending, approvedWhileEligible: false);

        var changeset = builder.Build(runId, [approved, pending], "operator");

        changeset.Entries.Should().HaveCount(1);
        changeset.Entries[0].ProposalId.Should().Be(approved.Id);
    }

    [Fact]
    public void Export_orders_catalogue_before_subscription_per_quickstart_V6()
    {
        var builder = new ApprovedChangesetBuilder();
        var runId = RunId.New();
        var subscription = CreateProposal(
            runId,
            ApprovalDecisionState.Approved,
            approvedWhileEligible: true,
            actionType: ProposedActionType.UpdateQuantity,
            category: ApprovalProposalCategory.Subscription,
            order: 100);
        var catalogue = CreateProposal(
            runId,
            ApprovalDecisionState.Approved,
            approvedWhileEligible: true,
            actionType: ProposedActionType.CreateOrUpdateCatalogueEntry,
            category: ApprovalProposalCategory.Catalogue,
            order: 100);

        var changeset = builder.Build(runId, [subscription, catalogue], "operator");

        changeset.Entries[0].ActionType.Should().Be(ProposedActionType.CreateOrUpdateCatalogueEntry);
    }

    private static ApprovalProposal CreateProposal(
        RunId runId,
        ApprovalDecisionState state,
        bool approvedWhileEligible,
        ProposedActionType actionType = ProposedActionType.UpdateQuantity,
        ApprovalProposalCategory category = ApprovalProposalCategory.Subscription,
        int order = 100) =>
        new(
            ApprovalProposalId.New(),
            runId,
            ProposedChangeId.New(),
            IdempotencyKey.Create(runId, MismatchId.New(), actionType),
            MismatchId.New(),
            category,
            actionType,
            state,
            ApprovalEligibility.Eligible,
            null,
            Domain.Common.MexId.Create("MEX-001"),
            "Product",
            null,
            new Dictionary<string, string> { ["quantity"] = "5" },
            new Dictionary<string, string> { ["quantity"] = "10" },
            order,
            [],
            null,
            DateTimeOffset.UtcNow,
            null,
            approvedWhileEligible,
            "operator",
            DateTimeOffset.UtcNow);
}
