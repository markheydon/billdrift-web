using BillDrift.Application.Classification;
using BillDrift.Application.Reconciliation;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Application.Tests.Reconciliation;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.Classification;

public sealed class ClassificationIntegrationTests
{
    private static readonly ProductCategoryRule Microsoft365Rule = new(
        "OFFER-MS365",
        ProductCategoryMatchKind.OfferIdPrefix,
        ProductCategory.Microsoft365);

    [Fact]
    public async Task InternalCustomer_SuppressesMissingInStripe()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var mexId = MexId.Create("INTERNAL-MEX-001");
        var inputs = ReconciliationTestDataBuilder.MissingInStripe() with
        {
            SubscriptionLines =
            [
                ReconciliationTestDataBuilder.CleanMatchAllDomains().SubscriptionLines[0] with
                {
                    Customer = CustomerIdentity.Create(mexId, "Internal Customer")
                }
            ]
        };

        var store = new InMemoryItemClassificationStore();
        store.SetConfiguration(new ClassificationRuleConfiguration([mexId], [Microsoft365Rule]));

        var classificationService = new ClassificationService(store, new ClassificationRuleEngine());
        var classifications = await classificationService.ClassifyAsync(
            inputs,
            ReconciliationTestDataBuilder.DefaultScope,
            cancellationToken);

        var engine = new ReconciliationEngine(new BillDrift.Application.Mapping.ProductMappingResolver());
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            inputs,
            Classifications: classifications));

        run.Mismatches.Should().NotContain(m => m.Type == MismatchType.MissingInStripe);
    }

    [Fact]
    public async Task InternalCustomer_SuppressesMissingBillingException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var mexId = MexId.Create("INTERNAL-MEX-002");
        var inputs = ReconciliationTestDataBuilder.MissingInStripe() with
        {
            SubscriptionLines =
            [
                ReconciliationTestDataBuilder.CleanMatchAllDomains().SubscriptionLines[0] with
                {
                    Customer = CustomerIdentity.Create(mexId, "Internal Customer")
                }
            ]
        };

        var store = new InMemoryItemClassificationStore();
        store.SetConfiguration(new ClassificationRuleConfiguration([mexId], [Microsoft365Rule]));

        var classificationService = new ClassificationService(store, new ClassificationRuleEngine());
        var classifications = await classificationService.ClassifyAsync(
            inputs,
            ReconciliationTestDataBuilder.DefaultScope,
            cancellationToken);

        var engine = new ReconciliationEngine(new BillDrift.Application.Mapping.ProductMappingResolver());
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            inputs,
            Classifications: classifications));

        var surfacing = new ExceptionSurfacingService();
        var viewModel = surfacing.Surface(run, options: null, classifications);

        viewModel.FlatExceptions().Should().NotContain(e => e.Category == ExceptionCategory.MissingBillingItem);
    }

    [Fact]
    public async Task NonCspSupplier_SurfacesManualReview_AndBlocksBillImpactingProposals()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var inputs = ReconciliationTestDataBuilder.NonCspSupplierLine();
        var store = new InMemoryItemClassificationStore();
        var classificationService = new ClassificationService(store, new ClassificationRuleEngine());
        var classifications = await classificationService.ClassifyAsync(
            inputs,
            ReconciliationTestDataBuilder.DefaultScope,
            cancellationToken);

        var engine = new ReconciliationEngine(new BillDrift.Application.Mapping.ProductMappingResolver());
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            inputs,
            Classifications: classifications));

        run.ProposedChanges.Should().BeEmpty();

        var surfacing = new ExceptionSurfacingService();
        var viewModel = surfacing.Surface(run, options: null, classifications);
        viewModel.FlatExceptions().Should().Contain(e => e.Category == ExceptionCategory.NonCspManualReview);
    }

    [Fact]
    public async Task CustomService_StripeOnly_ClassifiesAsCustomService()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stripeItem = ReconciliationTestDataBuilder.CleanMatchAllDomains().StripeItems[0] with
        {
            MappingMetadata = new BillDrift.Domain.Billing.StripeMappingMetadata(
                MexId.Create("MEX-TEST-001"),
                null,
                null,
                [],
                new Dictionary<string, string>())
        };

        var inputs = new ReconciliationInputs([], [], [], [stripeItem], []);

        var store = new InMemoryItemClassificationStore();
        var classificationService = new ClassificationService(store, new ClassificationRuleEngine());
        var classifications = await classificationService.ClassifyAsync(
            inputs,
            ReconciliationTestDataBuilder.DefaultScope,
            cancellationToken);

        var stripeRef = ReconciliationItemRefFactory.FromStripeBillingItem(inputs.StripeItems[0]);
        classifications.Get(stripeRef)!.Classification.Should().Be(ReconciliationItemClassification.CustomService);
    }
}
