using BillDrift.Application.Classification;
using BillDrift.Application.Tests.Reconciliation;
using BillDrift.Domain.Classification;
using FluentAssertions;

namespace BillDrift.Application.Tests.Classification;

public sealed class ClassificationServiceTests
{
    private static ClassificationService CreateService(InMemoryItemClassificationStore store) =>
        new(store, new ClassificationRuleEngine());

    [Fact]
    public async Task ClassifyAsync_IsDeterministic_ForSameInputs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryItemClassificationStore();
        store.SetConfiguration(new ClassificationRuleConfiguration(
            [],
            [new ProductCategoryRule("OFFER-MS365", ProductCategoryMatchKind.OfferIdPrefix, ProductCategory.Microsoft365)]));

        var service = CreateService(store);
        var inputs = ReconciliationTestDataBuilder.CleanMatchAllDomains();
        var scope = ReconciliationTestDataBuilder.DefaultScope;

        var first = await service.ClassifyAsync(inputs, scope, cancellationToken);
        var second = await service.ClassifyAsync(inputs, scope, cancellationToken);

        first.ByStableKey.Keys.Should().BeEquivalentTo(second.ByStableKey.Keys);
        foreach (var key in first.ByStableKey.Keys)
        {
            first.ByStableKey[key].Classification.Should().Be(second.ByStableKey[key].Classification);
            first.ByStableKey[key].RuleBasis.Should().Be(second.ByStableKey[key].RuleBasis);
            first.ByStableKey[key].Confidence.Should().Be(second.ByStableKey[key].Confidence);
        }
    }
}
