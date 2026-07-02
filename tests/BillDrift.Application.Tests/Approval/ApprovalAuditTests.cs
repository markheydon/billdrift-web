using BillDrift.Application.Approval;
using FluentAssertions;

namespace BillDrift.Application.Tests.Approval;

public sealed class ApprovalAuditTests
{
    [Fact]
    public async Task Rejected_proposal_audit_includes_reason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryApprovalStore();
        var (run, exceptions) = ApprovalTestFixtureBuilder.Build("quantity-mismatch");
        var service = ApprovalTestFixtureBuilder.CreateService(store);
        await service.IngestAsync(new ApprovalIngestionRequest(run, exceptions, null), cancellationToken);

        var proposal = (await store.ListProposalsByRunAsync(run.Id, cancellationToken)).First();
        await service.RejectAsync(new RejectProposalCommand(run.Id, proposal.Id, "Incorrect mapping"), cancellationToken);

        var audit = await service.GetAuditHistoryAsync(run.Id, proposal.Id, cancellationToken);
        audit.Should().Contain(e => e.Summary.Contains("Incorrect mapping", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Re_run_does_not_mutate_historical_audit_entries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryApprovalStore();
        var (run1, exceptions1) = ApprovalTestFixtureBuilder.Build("quantity-mismatch");
        var service = ApprovalTestFixtureBuilder.CreateService(store);

        await service.IngestAsync(new ApprovalIngestionRequest(run1, exceptions1, null), cancellationToken);
        var before = (await service.GetAuditHistoryAsync(run1.Id, cancellationToken: cancellationToken)).Count;

        var run2 = run1 with { Id = Domain.Reconciliation.RunId.New() };
        await service.IngestAsync(
            new ApprovalIngestionRequest(run2, exceptions1 with { RunId = run2.Id }, null),
            cancellationToken);

        (await service.GetAuditHistoryAsync(run1.Id, cancellationToken: cancellationToken)).Count.Should().Be(before);
    }
}
