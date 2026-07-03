using BillDrift.Application.Import;
using BillDrift.Application.Ingestion;
using BillDrift.Infrastructure.Ingestion;
using BillDrift.Infrastructure.Tests.Storage;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Tests.Ingestion;

public sealed class AzureTableIngestionRunIndexStoreTests
{
    [Fact]
    [Trait("Category", AzureStorageTestSupport.IntegrationTrait)]
    public async Task Ingestion_run_round_trips_through_table()
    {
        AzureStorageTestSupport.EnsureAvailableOrSkip();

        var store = CreateStore($"ingestiontable{Guid.NewGuid():N}");
        var ingestionId = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;

        var inProgress = new SubscriptionManagementIngestionRun
        {
            IngestionId = ingestionId,
            ContentFingerprint = "abc123",
            UploadedAt = uploadedAt,
            Status = IngestionRunStatus.InProgress,
            SourceBlobPath = $"{ingestionId:D}/source/sample.csv"
        };

        await store.CreateInProgressAsync(inProgress, TestContext.Current.CancellationToken);

        var completed = inProgress with
        {
            Status = IngestionRunStatus.Completed,
            CompletedAt = uploadedAt.AddMinutes(1),
            ResultManifestBlobPath = $"{ingestionId:D}/result/manifest.json",
            Summary = new Application.Import.SubscriptionManagementCsvIngestionSummary
            {
                RowsEmitted = 3
            }
        };

        await store.CompleteAsync(completed, TestContext.Current.CancellationToken);
        var loaded = await store.GetByIdAsync(ingestionId, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(IngestionRunStatus.Completed);
        loaded.Summary!.RowsEmitted.Should().Be(3);
    }

    [Fact]
    [Trait("Category", AzureStorageTestSupport.IntegrationTrait)]
    public async Task Retail_pricing_run_round_trips_through_table()
    {
        AzureStorageTestSupport.EnsureAvailableOrSkip();

        var store = CreateStore($"retailtable{Guid.NewGuid():N}");
        var ingestionId = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;

        var inProgress = new RetailPricingIngestionRun
        {
            IngestionId = ingestionId,
            ContentFingerprint = "pricing123",
            UploadedAt = uploadedAt,
            Status = IngestionRunStatus.InProgress,
            SourceBlobPath = $"{ingestionId:D}/source/pricing.csv"
        };

        await store.CreateRetailPricingInProgressAsync(inProgress, TestContext.Current.CancellationToken);

        var completed = inProgress with
        {
            Status = IngestionRunStatus.Completed,
            CompletedAt = uploadedAt.AddMinutes(1),
            ResultManifestBlobPath = $"{ingestionId:D}/result/manifest.json",
            Summary = new RetailPricingCsvIngestionSummary
            {
                ResolvedPriceCount = 3
            }
        };

        await store.CompleteRetailPricingAsync(completed, TestContext.Current.CancellationToken);
        var loaded = await store.GetRetailPricingByIdAsync(ingestionId, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(IngestionRunStatus.Completed);
        loaded.Summary!.ResolvedPriceCount.Should().Be(3);
    }

    private static AzureTableIngestionRunIndexStore CreateStore(string tableName)
    {
        var tableServiceClient = AzureStorageTestSupport.CreateTableServiceClient(AzureStorageTestSupport.GetConnectionString());
        return new AzureTableIngestionRunIndexStore(
            tableServiceClient,
            Options.Create(new IngestionStorageOptions { TableName = tableName }));
    }
}
