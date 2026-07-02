using BillDrift.Application.Approval;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.Approval;

public sealed class ApprovalServiceDecisionTests
{
    [Fact]
    public async Task Approve_quantity_update_per_quickstart_V2()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryApprovalStore();
        var (run, exceptions) = ApprovalTestFixtureBuilder.Build("quantity-mismatch");
        var service = ApprovalTestFixtureBuilder.CreateService(store);
        await service.IngestAsync(new ApprovalIngestionRequest(run, exceptions, null), cancellationToken);

        var proposal = (await store.ListProposalsByRunAsync(run.Id, cancellationToken))
            .First(p => p.ActionType == ProposedActionType.UpdateQuantity);

        var decision = await service.ApproveAsync(
            new ApproveProposalCommand(run.Id, proposal.Id),
            cancellationToken);

        decision.NewState.Should().Be(ApprovalDecisionState.Approved);
        var updated = await store.GetProposalAsync(run.Id, proposal.Id, cancellationToken);
        updated!.ApprovedWhileEligible.Should().BeTrue();
    }

    [Fact]
    public async Task Reject_requires_reason_per_quickstart_V3()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryApprovalStore();
        var (run, exceptions) = ApprovalTestFixtureBuilder.Build("quantity-mismatch");
        var service = ApprovalTestFixtureBuilder.CreateService(store);
        await service.IngestAsync(new ApprovalIngestionRequest(run, exceptions, null), cancellationToken);

        var proposal = (await store.ListProposalsByRunAsync(run.Id, cancellationToken)).First();

        var act = () => service.RejectAsync(new RejectProposalCommand(run.Id, proposal.Id, " "), cancellationToken);
        await act.Should().ThrowAsync<ApprovalValidationException>();

        var decision = await service.RejectAsync(
            new RejectProposalCommand(run.Id, proposal.Id, "Manual fix in Stripe"),
            cancellationToken);

        decision.NewState.Should().Be(ApprovalDecisionState.Rejected);
    }

    [Fact]
    public async Task Supersession_preserves_prior_decision_rows_per_quickstart_V7()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryApprovalStore();
        var (run1, exceptions1) = ApprovalTestFixtureBuilder.Build("quantity-mismatch");
        var service = ApprovalTestFixtureBuilder.CreateService(store);

        await service.IngestAsync(new ApprovalIngestionRequest(run1, exceptions1, null), cancellationToken);
        var proposal1 = (await store.ListProposalsByRunAsync(run1.Id, cancellationToken)).First();
        await service.ApproveAsync(new ApproveProposalCommand(run1.Id, proposal1.Id), cancellationToken);
        var decisionsBefore = (await store.ListAuditEventsAsync(run1.Id, cancellationToken: cancellationToken)).Count;

        var run2 = run1 with { Id = RunId.New() };
        var exceptions2 = exceptions1 with { RunId = run2.Id };
        await service.IngestAsync(new ApprovalIngestionRequest(run2, exceptions2, null), cancellationToken);

        var historical = await store.GetProposalAsync(run1.Id, proposal1.Id, cancellationToken);
        historical!.State.Should().Be(ApprovalDecisionState.Historical);
        (await store.ListAuditEventsAsync(run1.Id, cancellationToken: cancellationToken)).Count
            .Should().BeGreaterThanOrEqualTo(decisionsBefore);
    }
}
