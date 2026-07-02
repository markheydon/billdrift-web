using BillDrift.Application.Approval;
using BillDrift.Application.Classification;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Application.Tests.Reconciliation;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.Approval;

public sealed class ApprovalIngestionServiceTests
{
    [Fact]
    public async Task Ingest_creates_pending_proposals_per_quickstart_V1()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryApprovalStore();
        var (run, exceptions) = ApprovalTestFixtureBuilder.Build("missing-in-stripe");
        var service = ApprovalTestFixtureBuilder.CreateService(store);

        var result = await service.IngestAsync(
            new ApprovalIngestionRequest(run, exceptions, null),
            cancellationToken);

        result.PendingCount.Should().BeGreaterThan(0);
        var queue = await service.GetQueueAsync(run.Id, cancellationToken: cancellationToken);
        queue.CustomerGroups.SelectMany(g => g.SubscriptionProposals)
            .Should()
            .OnlyContain(p => p.State == ApprovalDecisionState.Pending);
    }

    [Fact]
    public async Task Ingest_never_auto_approves_per_quickstart_V10()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryApprovalStore();
        var (run, exceptions) = ApprovalTestFixtureBuilder.Build("quantity-mismatch");
        var service = ApprovalTestFixtureBuilder.CreateService(store);

        await service.IngestAsync(new ApprovalIngestionRequest(run, exceptions, null), cancellationToken);

        var proposals = await store.ListProposalsByRunAsync(run.Id, cancellationToken);
        proposals.Should().NotContain(p => p.State == ApprovalDecisionState.Approved);
    }

    [Fact]
    public async Task Ingest_flags_duplicate_catalogue_proposals_as_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryApprovalStore();
        var service = ApprovalTestFixtureBuilder.CreateService(store);

        var run = BuildRunWithDuplicateCatalogueChanges();
        var exceptions = new ExceptionSurfacingService().Surface(
            run,
            null,
            new ClassificationContext(new Dictionary<string, ItemClassification>(), DateTimeOffset.UtcNow));

        await service.IngestAsync(new ApprovalIngestionRequest(run, exceptions, null), cancellationToken);

        var catalogue = (await store.ListProposalsByRunAsync(run.Id, cancellationToken))
            .Where(p => p.ActionType == ProposedActionType.CreateOrUpdateCatalogueEntry)
            .ToList();

        catalogue.Should().HaveCount(2);
        catalogue.Should().Contain(p => p.Eligibility == ApprovalEligibility.CatalogueConflict);
        // The safeguard must prevent both conflicting catalogue entries from becoming approvable.
        catalogue.Count(p => p.Eligibility == ApprovalEligibility.Eligible).Should().BeLessThanOrEqualTo(1);
    }

    private static ReconciliationRun BuildRunWithDuplicateCatalogueChanges()
    {
        var runId = RunId.New();
        var root = CommercialKeyRoot.Create(OfferId.Create("OFFER"), SkuId.Create("SKU"));

        ProposedChange CatalogueChange()
        {
            var mismatchId = MismatchId.New();
            return new ProposedChange(
                ProposedChangeId.New(),
                IdempotencyKey.Create(runId, mismatchId, ProposedActionType.CreateOrUpdateCatalogueEntry),
                mismatchId,
                ProposedActionType.CreateOrUpdateCatalogueEntry,
                new ProposedChangeTarget(),
                new Dictionary<string, string> { ["name"] = "Product" },
                new CatalogueEntryPayload(null, "Product", root, []),
                10);
        }

        return new ReconciliationRun(
            runId,
            DateTimeOffset.UtcNow,
            ReconciliationTestDataBuilder.DefaultScope,
            new ReconciliationInputs([], [], [], [], []),
            [],
            [],
            [CatalogueChange(), CatalogueChange()]);
    }
}
