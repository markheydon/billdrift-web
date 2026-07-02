using BillDrift.Application.Reconciliation.Matching;
using BillDrift.Domain.Mapping;
using FluentAssertions;

namespace BillDrift.Application.Tests.Reconciliation;

public class FuzzyNameMatcherTests
{
    private static ProductMapping CreateMapping(string offer, string sku, string variant) =>
        new(
            ProductMappingId.New(),
            Domain.Common.CommercialKeyRoot.Create(
                Domain.Common.OfferId.Create(offer),
                Domain.Common.SkuId.Create(sku)),
            variant,
            Domain.Common.StripeProductId.Create("prod_test"),
            new Dictionary<Domain.Common.PriceTermKey, Domain.Common.StripePriceId>(),
            [new SupplierNameVariant(variant.ToLowerInvariant(), variant)],
            Domain.Common.ProductClassification.Csp,
            Domain.Common.MappingConfidence.High,
            Domain.Common.MappingSource.Manual);

    [Fact]
    public void Single_candidate_at_threshold_qualifies()
    {
        var matcher = new DeterministicFuzzyNameMatcher();
        var mappings = new[]
        {
            CreateMapping("O1", "S1", "Microsoft 365 Business Basic")
        };

        var candidates = matcher.FindCandidates("Microsoft 365 Business Basic", mappings);

        candidates.Should().HaveCount(1);
    }

    [Fact]
    public void Multiple_candidates_above_threshold_is_ambiguous()
    {
        var matcher = new DeterministicFuzzyNameMatcher();
        var mappings = new[]
        {
            CreateMapping("O1", "S1", "Microsoft 365 Business Basic"),
            CreateMapping("O2", "S2", "Microsoft 365 Business Basic")
        };

        var candidates = matcher.FindCandidates("Microsoft 365 Business Basic", mappings);

        candidates.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Tie_break_uses_lexicographically_smallest_offer_sku()
    {
        var matcher = new DeterministicFuzzyNameMatcher();
        var mappings = new[]
        {
            CreateMapping("O2", "S2", "Microsoft 365 Business Basic"),
            CreateMapping("O1", "S1", "Microsoft 365 Business Basic")
        };

        var candidates = matcher.FindCandidates("Microsoft 365 Business Basic", mappings);

        candidates[0].Key.OfferId.Value.Should().Be("O1");
    }
}
