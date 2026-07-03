using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Infrastructure.CatalogueReconciliation;
using BillDrift.Infrastructure.Tests.Storage;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Tests.CatalogueReconciliation;

public sealed class AzureCatalogueReconciliationStoreTests
{
    [Fact]
    [Trait("Category", AzureStorageTestSupport.IntegrationTrait)]
    public async Task Catalogue_run_round_trips_through_blob_and_table()
    {
        AzureStorageTestSupport.EnsureAvailableOrSkip();

        var options = Options.Create(new CatalogueReconciliationStorageOptions
        {
            BlobContainerName = $"catalogue-runs-{Guid.NewGuid():N}",
            TableName = $"catalogueruns{Guid.NewGuid():N}"
        });

        var connectionString = AzureStorageTestSupport.GetConnectionString();
        var store = new AzureCatalogueReconciliationStore(
            AzureStorageTestSupport.CreateBlobServiceClient(connectionString),
            AzureStorageTestSupport.CreateTableServiceClient(connectionString),
            options);

        var run = new CatalogueReconciliationRun(
            CatalogueRunId.New(),
            DateTimeOffset.UtcNow,
            new CatalogueReconciliationInputs([], [], [], [], new CatalogueInputReferences(null, null, null, null)),
            [],
            [],
            new CatalogueReconciliationSummary(0, new Dictionary<CatalogueExceptionType, int>(), 0, 0, 0, 0),
            new CatalogueReconciliationOptions());

        await store.SaveRunAsync(run, TestContext.Current.CancellationToken);
        var loaded = await store.GetRunAsync(run.RunId, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.RunId.Should().Be(run.RunId);

        var listed = await store.ListRunsAsync(10, TestContext.Current.CancellationToken);
        listed.Should().Contain(r => r.CatalogueRunId == run.RunId);
    }
}
