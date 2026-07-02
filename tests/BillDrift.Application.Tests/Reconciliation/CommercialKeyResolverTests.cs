using BillDrift.Application.Reconciliation.Indexing;
using BillDrift.Application.Reconciliation.Matching;
using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;
using FluentAssertions;

namespace BillDrift.Application.Tests.Reconciliation;

public class CommercialKeyResolverTests
{
    private static CommercialKeyResolver CreateResolver(IReadOnlyList<ProductMapping> mappings)
    {
        var fuzzy = new DeterministicFuzzyNameMatcher();
        var index = ProductMappingIndex.Build(
            mappings,
            new Mapping.ProductMappingResolver(),
            fuzzy);
        return new CommercialKeyResolver(index, fuzzy);
    }

    [Fact]
    public void Resolves_offer_sku_from_subscription_truth_at_high_confidence()
    {
        var inputs = ReconciliationTestDataBuilder.CleanMatchAllDomains();
        var resolver = CreateResolver(inputs.ProductMappings);
        var line = inputs.SubscriptionLines[0];

        var resolution = resolver.Resolve(line);

        resolution.Confidence.Should().Be(MatchConfidence.High);
        resolution.ResolutionPath.Should().Be(ProductResolutionPath.ExplicitOfferSku);
        resolution.CommercialKeyRoot.Should().NotBeNull();
    }

    [Fact]
    public void Resolves_exact_name_variant_at_medium_confidence()
    {
        var mapping = ReconciliationTestDataBuilder.CleanMatchAllDomains().ProductMappings[0];
        var resolver = CreateResolver([mapping]);

        var resolution = resolver.ResolveByName("Microsoft 365 Business Basic");

        resolution.Confidence.Should().Be(MatchConfidence.Medium);
        resolution.ResolutionPath.Should().Be(ProductResolutionPath.NameVariantExact);
    }
}
