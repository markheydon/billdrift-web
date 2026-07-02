using Azure.Data.Tables;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using BillDrift.Infrastructure.Classification;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Tests.Classification;

public sealed class AzureTableItemClassificationStoreTests
{
    [Fact]
    public async Task SaveOverrideAsync_RoundTripsOverride()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var tableServiceClient = new TableServiceClient(connectionString);
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
