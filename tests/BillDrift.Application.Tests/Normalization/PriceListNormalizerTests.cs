using BillDrift.Application.Normalization;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;
using FluentAssertions;

namespace BillDrift.Application.Tests.Normalization;

public sealed class PriceListNormalizerTests
{
    private readonly PriceListNormalizer _normalizer = new();

    [Fact]
    public void Catalogue_row_maps_to_catalogue_source_and_csp_classification()
    {
        var raw = CreateCatalogueRow();

        var price = _normalizer.Normalize(raw);

        price.Source.Should().Be(PriceSource.Catalogue);
        price.Classification.Should().Be(ProductClassification.Csp);
        price.Rrp.Amount.Should().Be(12.00m);
        price.Wholesale.Amount.Should().Be(8.50m);
        price.Margin.Should().NotBeNull();
        price.MarginPercent.Should().Be(29.17m);
        price.Platform.Should().Be(PricingPlatform.Nce);
    }

    [Fact]
    public void Manual_override_maps_to_manual_source_and_non_csp_classification()
    {
        var raw = new RawManualPriceEntry(
            RawImportId.Create(ImportSourceKind.ManualPriceEntry, "override-doc", "override-1"),
            "OFFER-001",
            "SKU-001",
            "Annual",
            "Monthly",
            "7.00",
            "10.00",
            "Customer contract",
            new DateOnly(2026, 1, 1),
            DateTimeOffset.UtcNow);

        var price = _normalizer.Normalize(raw);

        price.Source.Should().Be(PriceSource.ManualOverride);
        price.Classification.Should().Be(ProductClassification.NonCsp);
        price.Rrp.Amount.Should().Be(10.00m);
        price.Wholesale.Amount.Should().Be(7.00m);
    }

    [Theory]
    [InlineData("Annual", Term.Annual)]
    [InlineData("P1Y", Term.Annual)]
    [InlineData("Triennial", Term.Triennial)]
    public void TryParseTerm_supports_retail_pricing_aliases(string raw, Term expected)
    {
        PriceListNormalizer.TryParseTerm(raw, out var term).Should().BeTrue();
        term.Should().Be(expected);
    }

    private static RawPriceListRow CreateCatalogueRow() =>
        new(
            RawImportId.Create(ImportSourceKind.GiacomPriceList, "doc-1", "row-1"),
            "OFFER-MS365-BB",
            "SKU-MS365-BB",
            "Annual",
            "Monthly",
            "8.50",
            "12.00",
            "3.50",
            "29.17",
            "Active",
            "NCE",
            null,
            "doc-1",
            1);
}
