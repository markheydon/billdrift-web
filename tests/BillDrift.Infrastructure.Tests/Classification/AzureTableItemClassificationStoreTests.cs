using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using BillDrift.Infrastructure.Classification;
using BillDrift.Infrastructure.Tests.Storage;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Tests.Classification;

public sealed class AzureTableItemClassificationStoreTests
{
    [Fact]
    [Trait("Category", AzureStorageTestSupport.IntegrationTrait)]
    public async Task SaveOverrideAsync_RoundTripsOverride()
    {
        AzureStorageTestSupport.EnsureAvailableOrSkip();

        var cancellationToken = TestContext.Current.CancellationToken;
        var tableServiceClient = AzureStorageTestSupport.CreateTableServiceClient(AzureStorageTestSupport.GetConnectionString());
        var options = Options.Create(new ClassificationStorageOptions { TableName = $"itemclassifications-{Guid.NewGuid():N}" });
        var store = new AzureTableItemClassificationStore(tableServiceClient, options);

        var itemRef = ReconciliationItemRef.Create(
            ReconciliationItemKind.SupplierCost,
            $"test:{Guid.NewGuid():N}",
            MexId.Create("MEX-ROUNDTRIP"));

        await store.SaveOverrideAsync(new ClassificationOverride(
            itemRef,
            ReconciliationItemClassification.MicrosoftCsp,
            "round trip test",
            "test-operator",
            DateTimeOffset.UtcNow),
            cancellationToken);

        var loaded = await store.GetOverrideAsync(itemRef, cancellationToken);
        loaded.Should().NotBeNull();
        loaded!.Classification.Should().Be(ReconciliationItemClassification.MicrosoftCsp);
    }
}
