using BillDrift.Application.History;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.History;

public sealed class RunHistoryServiceTests
{
    [Fact]
    public async Task ListRuns_filters_by_billing_period()
    {
        var store = new InMemoryRunHistoryStore();
        var service = CreateService(store);

        await SeedRun(store, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        await SeedRun(store, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));

        var response = await service.ListRunsAsync(new RunHistoryListFilter(
            BillingPeriodStart: new DateOnly(2026, 2, 1),
            BillingPeriodEnd: new DateOnly(2026, 2, 28)),
            cancellationToken: TestContext.Current.CancellationToken);

        response.Items.Should().HaveCount(1);
        response.Items[0].BillingPeriod.Start.Should().Be(new DateOnly(2026, 2, 1));
    }

    [Fact]
    public async Task GetRunDetail_returns_summary_without_full_blob_load()
    {
        var store = new InMemoryRunHistoryStore();
        var service = CreateService(store);
        var runId = RunId.New();
        await store.UpsertRunAsync(CreateRecord(runId), TestContext.Current.CancellationToken);

        var detail = await service.GetRunDetailAsync(runId, includeResults: false, cancellationToken: TestContext.Current.CancellationToken);

        detail.Results.Should().BeNull();
        detail.SummaryMetrics.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRunDetail_includes_approval_status_links()
    {
        var store = new InMemoryRunHistoryStore();
        var approvalStore = new Approval.InMemoryApprovalStore();
        var service = CreateService(store, approvalStore);
        var runId = RunId.New();
        await store.UpsertRunAsync(CreateRecord(runId), TestContext.Current.CancellationToken);

        var detail = await service.GetRunDetailAsync(runId, cancellationToken: TestContext.Current.CancellationToken);

        detail.ProposalStatusLinks.Should().NotBeNull();
        detail.ExecutionOutcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task Execution_outcomes_placeholder_is_empty()
    {
        var store = new InMemoryRunHistoryStore();
        var service = CreateService(store);
        var runId = RunId.New();
        await store.UpsertRunAsync(CreateRecord(runId), TestContext.Current.CancellationToken);

        var detail = await service.GetRunDetailAsync(runId, cancellationToken: TestContext.Current.CancellationToken);

        detail.ExecutionOutcomes.Should().BeEmpty();
    }

    private static RunHistoryService CreateService(
        InMemoryRunHistoryStore store,
        Approval.InMemoryApprovalStore? approvalStore = null) =>
        new(
            store,
            store,
            approvalStore ?? new Approval.InMemoryApprovalStore(),
            new RunComparisonService(new StableMismatchKeyFactory()),
            new DriftTrendAnalyzer(),
            new PricingDriftAnalyzer(),
            Microsoft.Extensions.Options.Options.Create(new RunHistoryOptions()));

    private static async Task SeedRun(InMemoryRunHistoryStore store, DateOnly start, DateOnly end)
    {
        var runId = RunId.New();
        await store.UpsertRunAsync(CreateRecord(runId, start, end), TestContext.Current.CancellationToken);
    }

    private static ReconciliationRunRecord CreateRecord(
        RunId runId,
        DateOnly? start = null,
        DateOnly? end = null) =>
        new(
            runId,
            RunArchiveStatus.Completed,
            BillingPeriod.Create(start ?? new DateOnly(2026, 1, 1), end ?? new DateOnly(2026, 1, 31)),
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            "operator@test.com",
            new MappingVersionReference("v1", "hash1", new DateOnly(2026, 1, 1)),
            Enum.GetValues<InputDomainType>().Select(d => new InputSnapshotMetadata(d, true)).ToList(),
            new RunSummaryMetrics(10, 2, new Dictionary<string, int> { ["QuantityMismatch"] = 2 }, 1, false),
            $"{runId.Value}/manifest.json");
}
