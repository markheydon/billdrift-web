using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Infrastructure.CatalogueReconciliation;

namespace BillDrift.Infrastructure.Tests.CatalogueReconciliation;

public class InMemoryCatalogueReconciliationStoreTests
{
    [Fact]
    public async Task Save_and_load_round_trip_preserves_run()
    {
        var store = new InMemoryCatalogueReconciliationStore();
        var inputs = new CatalogueReconciliationInputs(
            [],
            [],
            [],
            [],
            new CatalogueInputReferences(null, null, null, null));

        var run = new CatalogueReconciliationRun(
            CatalogueRunId.New(),
            DateTimeOffset.UtcNow,
            inputs,
            [],
            [],
            new CatalogueReconciliationSummary(0, new Dictionary<CatalogueExceptionType, int>(), 0, 0, 0, 0),
            new CatalogueReconciliationOptions());

        await store.SaveRunAsync(run, TestContext.Current.CancellationToken);
        var loaded = await store.GetRunAsync(run.RunId, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.RunId.Should().Be(run.RunId);
    }
}
