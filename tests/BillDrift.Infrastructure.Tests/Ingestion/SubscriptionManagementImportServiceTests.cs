using BillDrift.Application.Import;
using BillDrift.Application.Import.SubscriptionManagement;
using BillDrift.Application.Ingestion;
using BillDrift.Application.Normalization;
using BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;
using BillDrift.Infrastructure.Ingestion;

namespace BillDrift.Infrastructure.Tests.Ingestion;

public sealed class SubscriptionManagementImportServiceTests
{
    [Fact]
    public async Task In_memory_upload_round_trip_persists_run_and_truth()
    {
        var ingester = new SubscriptionManagementCsvIngester(new SubscriptionManagementNormalizer());
        var blobStore = new InMemoryIngestionBlobStore();
        var indexStore = new InMemoryIngestionRunIndexStore();
        var service = new SubscriptionManagementIngestionService(ingester, blobStore, indexStore);

        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "subscription-management",
            "subscription-management-sample-a.csv");

        await using var stream = File.OpenRead(fixturePath);
        var run = await service.IngestAndPersistAsync(
            stream,
            "subscription-management-sample-a.csv",
            TestContext.Current.CancellationToken);

        Assert.Contains(run.Status, new[] { IngestionRunStatus.Completed, IngestionRunStatus.PartialSuccess });
        Assert.False(string.IsNullOrWhiteSpace(run.ContentFingerprint));

        var indexed = await indexStore.GetByIdAsync(run.IngestionId, TestContext.Current.CancellationToken);
        Assert.NotNull(indexed);

        var truth = await blobStore.GetSubscriptionTruthAsync(run.IngestionId, TestContext.Current.CancellationToken);
        Assert.NotNull(truth);
        Assert.NotEmpty(truth!);
    }

    [Fact]
    public async Task Oversized_stream_is_rejected_before_persisting_run()
    {
        var ingester = new SubscriptionManagementCsvIngester(new SubscriptionManagementNormalizer());
        var blobStore = new InMemoryIngestionBlobStore();
        var indexStore = new InMemoryIngestionRunIndexStore();
        var service = new SubscriptionManagementIngestionService(ingester, blobStore, indexStore);

        await using var stream = new MemoryStream(
            new byte[SubscriptionManagementCsvIngestionOptions.DefaultMaxFileSizeBytes + 1]);

        var act = () => service.IngestAndPersistAsync(
            stream,
            "oversized.csv",
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<SubscriptionManagementUploadTooLargeException>(act);

        var runs = await indexStore.ListRecentAsync(10, TestContext.Current.CancellationToken);
        Assert.Empty(runs);
    }
}
