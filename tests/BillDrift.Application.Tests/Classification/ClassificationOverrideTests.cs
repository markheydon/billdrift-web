using BillDrift.Application.Classification;
using BillDrift.Application.Tests.Reconciliation;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Tests.Classification;

public sealed class ClassificationOverrideTests
{
    private static ClassificationService CreateService(InMemoryItemClassificationStore store) =>
        new(store, new ClassificationRuleEngine());

    [Fact]
    public async Task ApplyOverrideAsync_TakesPrecedenceOnNextClassify()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryItemClassificationStore();
        var service = CreateService(store);
        var inputs = ReconciliationTestDataBuilder.NonCspSupplierLine();
        var scope = ReconciliationTestDataBuilder.DefaultScope;
        var itemRef = ReconciliationItemRefFactory.FromSupplierCostLine(inputs.SupplierCostLines[0]);

        var before = await service.ClassifyAsync(inputs, scope, cancellationToken);
        Assert.Equal(ReconciliationItemClassification.NonCspSupplier, before.Get(itemRef)!.Classification);

        await service.ApplyOverrideAsync(new ClassificationOverride(
            itemRef,
            ReconciliationItemClassification.MicrosoftCsp,
            "Operator confirmed CSP mapping",
            "operator-1",
            DateTimeOffset.UtcNow),
            cancellationToken);

        var after = await service.ClassifyAsync(inputs, scope, cancellationToken);
        Assert.Equal(ReconciliationItemClassification.MicrosoftCsp, after.Get(itemRef)!.Classification);
        Assert.Equal(ClassificationSource.ManualOverride, after.Get(itemRef)!.Source);
    }

    [Fact]
    public async Task ClearOverrideAsync_RevertsToAutomaticRules()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryItemClassificationStore();
        var service = CreateService(store);
        var inputs = ReconciliationTestDataBuilder.NonCspSupplierLine();
        var scope = ReconciliationTestDataBuilder.DefaultScope;
        var itemRef = ReconciliationItemRefFactory.FromSupplierCostLine(inputs.SupplierCostLines[0]);

        await service.ApplyOverrideAsync(new ClassificationOverride(
            itemRef,
            ReconciliationItemClassification.MicrosoftCsp,
            "temporary override",
            "operator-1",
            DateTimeOffset.UtcNow),
            cancellationToken);

        var cleared = await service.ClearOverrideAsync(itemRef, "operator-1", inputs, scope, cancellationToken);
        Assert.Equal(ClassificationSource.Automatic, cleared.Source);
        Assert.Equal(ReconciliationItemClassification.NonCspSupplier, cleared.Classification);
    }

    [Fact]
    public async Task ApplyOverrideAsync_ToInternal_RequiresNotes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryItemClassificationStore();
        var service = CreateService(store);
        var inputs = ReconciliationTestDataBuilder.CleanMatchAllDomains();
        var itemRef = ReconciliationItemRefFactory.FromSubscriptionLine(inputs.SubscriptionLines[0]);

        var act = () => service.ApplyOverrideAsync(new ClassificationOverride(
            itemRef,
            ReconciliationItemClassification.Internal,
            "",
            "operator-1",
            DateTimeOffset.UtcNow),
            cancellationToken);

        await Assert.ThrowsAsync<DomainValidationException>(act);
    }
}
