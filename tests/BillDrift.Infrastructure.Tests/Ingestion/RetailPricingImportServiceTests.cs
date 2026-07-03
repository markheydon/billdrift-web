using BillDrift.Application.Import;
using BillDrift.Application.Import.RetailPricing;
using BillDrift.Application.Ingestion;
using BillDrift.Application.Normalization;
using BillDrift.Infrastructure.Import.Giacom.RetailPricing;
using BillDrift.Infrastructure.Ingestion;

namespace BillDrift.Infrastructure.Tests.Ingestion;

public sealed class RetailPricingImportServiceTests
{
    [Fact]
    public async Task In_memory_upload_round_trip_persists_run_and_resolved_prices()
    {
        var ingester = new ResellerPricingCsvIngester(new PriceListNormalizer(), new IntendedPriceResolver());
        var blobStore = new InMemoryIngestionBlobStore();
        var indexStore = new InMemoryIngestionRunIndexStore();
        var service = new RetailPricingIngestionService(ingester, blobStore, indexStore);

        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "reseller-pricing",
            "reseller-pricing-sample-a.csv");

        await using var stream = File.OpenRead(fixturePath);
        var run = await service.IngestAndPersistAsync(
            stream,
            "reseller-pricing-sample-a.csv",
            cancellationToken: TestContext.Current.CancellationToken);

        run.Status.Should().BeOneOf(IngestionRunStatus.Completed, IngestionRunStatus.PartialSuccess);
        run.ContentFingerprint.Should().NotBeNullOrWhiteSpace();

        var indexed = await indexStore.GetRetailPricingByIdAsync(run.IngestionId, TestContext.Current.CancellationToken);
        indexed.Should().NotBeNull();

        var prices = await blobStore.GetResolvedPricesAsync(run.IngestionId, TestContext.Current.CancellationToken);
        prices.Should().NotBeNull();
        prices!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Oversized_stream_is_rejected_before_persisting_run()
    {
        var ingester = new ResellerPricingCsvIngester(new PriceListNormalizer(), new IntendedPriceResolver());
        var blobStore = new InMemoryIngestionBlobStore();
        var indexStore = new InMemoryIngestionRunIndexStore();
        var service = new RetailPricingIngestionService(ingester, blobStore, indexStore);

        await using var stream = new MemoryStream(
            new byte[RetailPricingCsvIngestionOptions.DefaultMaxFileSizeBytes + 1]);

        var act = () => service.IngestAndPersistAsync(
            stream,
            "oversized.csv",
            cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<RetailPricingUploadTooLargeException>();

        var runs = await indexStore.ListRecentRetailPricingAsync(10, TestContext.Current.CancellationToken);
        runs.Should().BeEmpty();
    }
}
