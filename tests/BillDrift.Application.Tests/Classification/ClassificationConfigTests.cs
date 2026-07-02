using BillDrift.Application.Classification;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using FluentAssertions;

namespace BillDrift.Application.Tests.Classification;

public sealed class ClassificationConfigTests
{
    [Fact]
    public async Task InternalMexIdConfig_AffectsClassification()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var mexId = MexId.Create("CONFIG-INTERNAL-001");
        var store = new InMemoryItemClassificationStore();
        await store.SaveConfigurationAsync(new ClassificationRuleConfiguration([mexId], []), cancellationToken);

        var itemRef = ReconciliationItemRef.Create(
            ReconciliationItemKind.SubscriptionTruth,
            $"{mexId.Value}:truth:test",
            mexId);

        var signals = new ClassificationSignals(
            itemRef,
            mexId,
            false,
            null,
            null,
            null,
            false,
            false,
            false,
            false,
            ProductCategory.Other,
            null);

        var result = new ClassificationRuleEngine().Evaluate(
            signals,
            await store.GetConfigurationAsync(cancellationToken),
            null,
            DateTimeOffset.UtcNow);

        result.Classification.Should().Be(ReconciliationItemClassification.Internal);
    }

    [Fact]
    public async Task ProductCategoryRule_AssignsCustomService()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryItemClassificationStore();
        await store.SaveConfigurationAsync(new ClassificationRuleConfiguration(
            [],
            [new ProductCategoryRule("Professional Services", ProductCategoryMatchKind.ProductNameContains, ProductCategory.CustomService)]),
            cancellationToken);

        var config = await store.GetConfigurationAsync(cancellationToken);
        config.ProductCategoryRules.Should().ContainSingle();
        config.ProductCategoryRules[0].Category.Should().Be(ProductCategory.CustomService);
    }

    [Fact]
    public async Task ConfigChange_DoesNotAlterManualOverrides()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryItemClassificationStore();
        var service = new ClassificationService(store, new ClassificationRuleEngine());
        var itemRef = ReconciliationItemRef.Create(
            ReconciliationItemKind.SupplierCost,
            "MEX-001:supplier:test",
            MexId.Create("MEX-001"));

        await service.ApplyOverrideAsync(new ClassificationOverride(
            itemRef,
            ReconciliationItemClassification.MicrosoftCsp,
            "manual",
            "operator",
            DateTimeOffset.UtcNow),
            cancellationToken);

        await store.SaveConfigurationAsync(
            new ClassificationRuleConfiguration([MexId.Create("MEX-001")], []),
            cancellationToken);

        var activeOverride = await store.GetOverrideAsync(itemRef, cancellationToken);
        activeOverride!.Classification.Should().Be(ReconciliationItemClassification.MicrosoftCsp);
    }
}
