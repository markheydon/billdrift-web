using BillDrift.Application.History;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.History;

public sealed class RunArchiveServiceTests
{
    [Fact]
    public async Task Persist_creates_immutable_run_record()
    {
        var store = new InMemoryRunHistoryStore();
        var service = CreateService(store);
        var run = CreateSampleRun();
        var request = CreatePersistRequest(run);

        var record = await service.PersistAsync(request, TestContext.Current.CancellationToken);

        record.Status.Should().Be(RunArchiveStatus.Completed);
        record.SummaryMetrics.MismatchCount.Should().Be(run.Mismatches.Count);
        (await store.GetRunAsync(run.Id, TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    [Fact]
    public async Task Persist_marks_all_input_domains_present_or_absent()
    {
        var store = new InMemoryRunHistoryStore();
        var service = CreateService(store);
        var run = CreateSampleRun();
        var runWithoutStripe = run with { Inputs = run.Inputs with { StripeItems = [] } };
        var context = CreateContext(includeStripe: false);
        var request = new PersistRunRequest(runWithoutStripe, context);

        var record = await service.PersistAsync(request, TestContext.Current.CancellationToken);

        record.InputSnapshots.Should().HaveCount(5);
        record.InputSnapshots.First(s => s.Domain == InputDomainType.StripeBilling).IsPresent.Should().BeFalse();
    }

    [Fact]
    public async Task Re_persist_completed_run_is_rejected()
    {
        var store = new InMemoryRunHistoryStore();
        var service = CreateService(store);
        var run = CreateSampleRun();
        var request = CreatePersistRequest(run);

        await service.PersistAsync(request, TestContext.Current.CancellationToken);
        var act = async () => await service.PersistAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<RunAlreadyArchivedException>();
    }

    [Fact]
    public async Task Failed_run_is_retained_with_failure_reason()
    {
        var failingBlob = new FailingBlobStore();
        var store = new InMemoryRunHistoryStore();
        var service = new RunArchiveService(store, failingBlob, new StableMismatchKeyFactory(), Microsoft.Extensions.Options.Options.Create(new RunHistoryOptions()));
        var run = CreateSampleRun();

        var act = async () => await service.PersistAsync(CreatePersistRequest(run), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        var record = await store.GetRunAsync(run.Id, TestContext.Current.CancellationToken);
        record!.Status.Should().Be(RunArchiveStatus.Failed);
        record.FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Re_persist_after_failed_attempt_is_rejected()
    {
        var store = new InMemoryRunHistoryStore();
        var failingService = new RunArchiveService(store, new FailingBlobStore(), new StableMismatchKeyFactory(), Microsoft.Extensions.Options.Options.Create(new RunHistoryOptions()));
        var run = CreateSampleRun();
        var request = CreatePersistRequest(run);

        var firstAttempt = async () => await failingService.PersistAsync(request, TestContext.Current.CancellationToken);
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>();

        var retryService = CreateService(store);
        var retry = async () => await retryService.PersistAsync(request, TestContext.Current.CancellationToken);

        await retry.Should().ThrowAsync<RunAlreadyArchivedException>();
        (await store.GetRunAsync(run.Id, TestContext.Current.CancellationToken))!.Status.Should().Be(RunArchiveStatus.Failed);
    }

    private static RunArchiveService CreateService(InMemoryRunHistoryStore store) =>
        new(store, store, new StableMismatchKeyFactory(), Microsoft.Extensions.Options.Options.Create(new RunHistoryOptions()));

    private static ReconciliationRun CreateSampleRun()
    {
        var runId = RunId.New();
        return new ReconciliationRun(
            runId,
            DateTimeOffset.UtcNow,
            BillingPeriod.Create(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
            new ReconciliationInputs([], [], [], [], []),
            [],
            [CreateMismatch()],
            []);
    }

    private static Mismatch CreateMismatch() =>
        new(
            MismatchId.New(),
            MismatchType.QuantityMismatch,
            MismatchSeverity.Warning,
            CustomerIdentity.Create(MexId.Create("MEX001")),
            null,
            new MismatchEntityRefs(),
            "5",
            "3",
            "Quantity mismatch");

    private static PersistRunRequest CreatePersistRequest(ReconciliationRun run) =>
        new(run, CreateContext());

    private static RunArchiveContext CreateContext(bool includeStripe = true)
    {
        var metadata = Enum.GetValues<InputDomainType>()
            .ToDictionary(
                d => d,
                d => new InputSnapshotMetadata(d, d != InputDomainType.StripeBilling || includeStripe));

        return new RunArchiveContext(
            "operator@test.com",
            metadata,
            new MappingVersionReference("2026-07-02", "sha256:abc", new DateOnly(2026, 7, 2)),
            DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    private sealed class FailingBlobStore : IRunBlobArchiveStore
    {
        public Task<RunArchiveWriteResult> WriteRunArchiveAsync(ReconciliationRun run, RunArchiveContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated blob failure");

        public Task<RunResultsSnapshot> LoadResultsSnapshotAsync(RunId runId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string> LoadInputBlobAsync(RunId runId, InputDomainType domain, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task VerifyManifestIntegrityAsync(RunId runId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(string BlobPath, string ContentHash)> ExportComparisonReportAsync(RunId runId, RunComparisonReport report, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
