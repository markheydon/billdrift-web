using BillDrift.Application.Mapping;
using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;
using FluentAssertions;

namespace BillDrift.Domain.Tests.Mapping;

public class ProductMappingTests
{
    [Fact]
    public void Resolver_finds_mapping_for_supplier_name_variant()
    {
        var mapping = CreateMapping(["microsoft 365 business basic", "ms365 business basic"]);
        var resolver = new ProductMappingResolver();

        var result = resolver.Resolve("MS365 Business Basic", [mapping]);

        result.Status.Should().Be(MappingResolutionStatus.Found);
        result.Mapping.Should().NotBeNull();
    }

    [Fact]
    public void Resolver_returns_ambiguous_when_multiple_mappings_match()
    {
        var mapping1 = CreateMapping(["office 365"]);
        var mapping2 = CreateMapping(["office 365"]);
        var resolver = new ProductMappingResolver();

        var result = resolver.Resolve("Office 365", [mapping1, mapping2]);

        result.Status.Should().Be(MappingResolutionStatus.Ambiguous);
    }

    private static ProductMapping CreateMapping(IEnumerable<string> variants) =>
        new(
            ProductMappingId.New(),
            CommercialKeyRoot.Create(OfferId.Create("OFFER-1"), SkuId.Create("SKU-1")),
            "Microsoft 365 Business Basic",
            StripeProductId.Create("prod_test123"),
            new Dictionary<PriceTermKey, StripePriceId>
            {
                [new PriceTermKey(Term.Annual, BillingFrequency.Monthly)] = StripePriceId.Create("price_test123")
            },
            variants.Select(v => new SupplierNameVariant(v, v)).ToList(),
            ProductClassification.Csp,
            MappingConfidence.High,
            MappingSource.Manual);
}
