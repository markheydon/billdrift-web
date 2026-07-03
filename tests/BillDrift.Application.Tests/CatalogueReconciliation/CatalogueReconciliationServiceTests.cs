using BillDrift.Application.Approval;
using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Application.Mapping;
using BillDrift.Application.Tests.Approval;
using BillDrift.Infrastructure.CatalogueReconciliation;
using BillDrift.Infrastructure.Ingestion;
using FluentAssertions;

namespace BillDrift.Application.Tests.CatalogueReconciliation;

public sealed class CatalogueReconciliationServiceTests
{
    [Fact]
    public async Task RunAsync_loads_stripe_catalogue_from_ingestion_run_id_when_inline_arrays_empty()
    {
        var inputs = CatalogueReconciliationTestDataBuilder.CleanMatch();
        var stripeIngestionRunId = Guid.NewGuid();
        var pricingIngestionRunId = Guid.NewGuid();

        var blobStore = new InMemoryIngestionBlobStore();
        await blobStore.PersistStripeCatalogueAsync(
            stripeIngestionRunId,
            inputs.StripeProducts,
            inputs.StripePrices,
            TestContext.Current.CancellationToken);
        blobStore.SeedResolvedPrices(pricingIngestionRunId, inputs.IntendedPrices);

        var service = CreateService(blobStore, new InMemoryCatalogueReconciliationStore());

        var run = await service.RunAsync(
            new CatalogueReconciliationRunRequest(
                stripeIngestionRunId,
                pricingIngestionRunId,
                inputs.ProductMappings),
            TestContext.Current.CancellationToken);

        run.Exceptions.Should().BeEmpty();
        run.Inputs.InputReferences.StripeIngestionRunId.Should().Be(stripeIngestionRunId);
        run.Inputs.InputReferences.PricingIngestionRunId.Should().Be(pricingIngestionRunId);
    }

    [Fact]
    public async Task RunAsync_throws_when_stripe_ingestion_run_id_has_no_archived_catalogue()
    {
        var inputs = CatalogueReconciliationTestDataBuilder.CleanMatch();
        var stripeIngestionRunId = Guid.NewGuid();
        var pricingIngestionRunId = Guid.NewGuid();

        // Only the pricing snapshot is archived; the Stripe catalogue blobs are deliberately absent to
        // simulate a broken or not-yet-run producer path.
        var blobStore = new InMemoryIngestionBlobStore();
        blobStore.SeedResolvedPrices(pricingIngestionRunId, inputs.IntendedPrices);

        var service = CreateService(blobStore, new InMemoryCatalogueReconciliationStore());

        var act = async () => await service.RunAsync(
            new CatalogueReconciliationRunRequest(
                stripeIngestionRunId,
                pricingIngestionRunId,
                inputs.ProductMappings),
            TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<CatalogueReconciliationValidationException>())
            .Which.Message.Should().Contain(stripeIngestionRunId.ToString("D"));
    }

    [Fact]
    public async Task RunAsync_prefers_inline_stripe_catalogue_over_ingestion_run_id()
    {
        var inputs = CatalogueReconciliationTestDataBuilder.CleanMatch();
        var stripeIngestionRunId = Guid.NewGuid();
        var pricingIngestionRunId = Guid.NewGuid();

        var blobStore = new InMemoryIngestionBlobStore();
        var archived = CatalogueReconciliationTestDataBuilder.MissingProduct();
        await blobStore.PersistStripeCatalogueAsync(
            stripeIngestionRunId,
            archived.StripeProducts,
            archived.StripePrices,
            TestContext.Current.CancellationToken);
        blobStore.SeedResolvedPrices(pricingIngestionRunId, inputs.IntendedPrices);

        var service = CreateService(blobStore, new InMemoryCatalogueReconciliationStore());

        var run = await service.RunAsync(
            new CatalogueReconciliationRunRequest(
                stripeIngestionRunId,
                pricingIngestionRunId,
                inputs.ProductMappings,
                inputs.StripeProducts,
                inputs.StripePrices),
            TestContext.Current.CancellationToken);

        run.Exceptions.Should().BeEmpty();
    }

    private static CatalogueReconciliationService CreateService(
        InMemoryIngestionBlobStore blobStore,
        ICatalogueReconciliationStore store) =>
        new(
            new CatalogueReconciliationEngine(new ProductMappingResolver()),
            store,
            blobStore,
            new CatalogueApprovalAdapter(),
            new InMemoryApprovalStore(),
            new OperatorContext("operator@test"));
}

