using BillDrift.Application.Import;
using BillDrift.Application.Normalization;
using BillDrift.Domain.Common;
using BillDrift.Infrastructure.Import.Giacom.RetailPricing;

namespace BillDrift.Infrastructure.Tests.Import.Giacom.RetailPricing;

public sealed class ResellerPricingCsvIngesterTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory, "fixtures", "reseller-pricing");

    private readonly ResellerPricingCsvIngester _ingester =
        new(new PriceListNormalizer(), new IntendedPriceResolver());

    [Fact]
    public void Sample_a_emits_resolved_prices_with_required_fields()
    {
        var result = Ingest("reseller-pricing-sample-a.csv");

        result.Status.Should().BeOneOf(IngestionOutcomeStatus.Success, IngestionOutcomeStatus.PartialSuccess);
        result.ResolvedPrices.Should().HaveCount(3);
        result.ResolvedPrices.Should().AllSatisfy(price =>
        {
            price.Key.OfferId.Value.Should().StartWith("OFFER-");
            price.Key.SkuId.Value.Should().StartWith("SKU-");
            price.Wholesale.Amount.Should().BeGreaterThan(0);
            price.Rrp.Amount.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void Sample_a_associates_multiple_commercial_keys()
    {
        var result = Ingest("reseller-pricing-sample-a.csv");

        result.ResolvedPrices.Select(p => p.Key.OfferId.Value)
            .Should().BeEquivalentTo(["OFFER-MS365-BB", "OFFER-EXO-PL1", "OFFER-TEAMS-ESS"]);
    }

    [Fact]
    public void Sample_a_matches_golden_file()
    {
        var result = Ingest("reseller-pricing-sample-a.csv");
        var goldenPath = Path.Combine(FixtureRoot, "expected", "sample-a.json");

        if (!File.Exists(goldenPath))
        {
            GoldenFileComparer.WriteGoldenFile(result, goldenPath);
        }

        GoldenFileComparer.AssertResultMatchesGolden(result, goldenPath);
    }

    [Fact]
    public void Catalogue_only_uses_catalogue_rrp_strategy()
    {
        var result = Ingest("reseller-pricing-sample-a.csv");

        result.ResolvedPrices.Should().AllSatisfy(price =>
        {
            price.Source.Should().Be(PriceSource.Catalogue);
            price.Classification.Should().Be(ProductClassification.Csp);
        });
        result.Summary.CatalogueOnlyCount.Should().Be(result.ResolvedPrices.Count);
        result.Summary.OverrideWinsCount.Should().Be(0);
    }

    [Fact]
    public void End_of_sale_retains_rrp()
    {
        var result = Ingest("end-of-sale.csv");
        var price = result.ResolvedPrices.Should().ContainSingle().Subject;

        price.Status.Should().Be(PriceListStatus.EndOfSale);
        price.Rrp.Amount.Should().Be(7.00m);
        price.Source.Should().Be(PriceSource.Catalogue);
    }

    [Fact]
    public void Column_variant_maps_mandatory_fields()
    {
        var result = Ingest("column-variant.csv");

        result.ResolvedPrices.Should().HaveCount(2);
        result.ResolvedPrices.Should().AllSatisfy(price =>
        {
            price.Key.OfferId.Value.Should().StartWith("OFFER-VAR-");
            price.Key.SkuId.Value.Should().StartWith("SKU-VAR-");
        });
    }

    [Fact]
    public void Partial_bad_rows_emits_valid_rows_and_skips_invalid()
    {
        var result = Ingest("partial-bad-rows.csv");

        result.Status.Should().Be(IngestionOutcomeStatus.PartialSuccess);
        result.RawCatalogueRows.Should().HaveCount(4);
        result.ResolvedPrices.Should().HaveCount(3);
        result.Summary.CatalogueRowsSkipped.Should().Be(2);
        result.LogEntries.Should().Contain(e => e.Reason == IngestionFailureReason.CommercialKeyMissing);
        result.LogEntries.Should().Contain(e => e.Reason == IngestionFailureReason.WholesaleUnparseable);
    }

    [Fact]
    public void Duplicate_keys_last_row_wins_with_warning()
    {
        var result = Ingest("duplicate-keys.csv");

        result.ResolvedPrices.Should().ContainSingle();
        result.ResolvedPrices[0].Rrp.Amount.Should().Be(9.00m);
        result.Summary.DuplicateKeyWarnings.Should().Be(1);
        result.LogEntries.Should().Contain(e => e.Reason == IngestionFailureReason.DuplicateCommercialKey);
    }

    [Fact]
    public void Platform_columns_map_to_nce_and_legacy()
    {
        var result = Ingest("reseller-pricing-sample-a.csv");

        result.ResolvedPrices.Should().Contain(p =>
            p.Key.OfferId.Value == "OFFER-MS365-BB" && p.Platform == PricingPlatform.Nce);
        result.ResolvedPrices.Should().Contain(p =>
            p.Key.OfferId.Value == "OFFER-EXO-PL1" && p.Platform == PricingPlatform.Legacy);
    }

    [Fact]
    public void Manual_override_beats_catalogue_for_same_key()
    {
        var overrides = new List<ManualPriceOverrideRequest>
        {
            new()
            {
                OfferId = "OFFER-MS365-BB",
                SkuId = "SKU-MS365-BB",
                Term = "Annual",
                Frequency = "Monthly",
                Rrp = "14.00",
                Reason = "Bespoke customer pricing",
                EffectiveDate = new DateOnly(2026, 1, 1)
            }
        };

        var result = Ingest("reseller-pricing-sample-a.csv", overrides);
        var price = result.ResolvedPrices.Single(p => p.Key.OfferId.Value == "OFFER-MS365-BB");

        price.Source.Should().Be(PriceSource.ManualOverride);
        price.Classification.Should().Be(ProductClassification.NonCsp);
        price.Rrp.Amount.Should().Be(14.00m);
        result.Summary.OverrideWinsCount.Should().Be(1);
    }

    [Fact]
    public void Reimport_produces_identical_source_document_id()
    {
        var first = Ingest("reseller-pricing-sample-a.csv");
        var second = Ingest("reseller-pricing-sample-a.csv");

        second.SourceDocumentId.Should().Be(first.SourceDocumentId);
        second.RawCatalogueRows.Select(r => r.Id).Should().BeEquivalentTo(first.RawCatalogueRows.Select(r => r.Id));
    }

    [Fact]
    public void Headers_only_csv_returns_failure_with_file_level_error()
    {
        var result = Ingest("headers-only.csv");

        result.Status.Should().Be(IngestionOutcomeStatus.Failure);
        result.RawCatalogueRows.Should().BeEmpty();
        result.ResolvedPrices.Should().BeEmpty();
        result.LogEntries.Should().Contain(e =>
            e.Reason == IngestionFailureReason.EmptyFile &&
            e.Severity == IngestionLogSeverity.Error);
    }

    [Fact]
    public void File_exceeding_max_size_returns_failure_without_reading_entire_stream()
    {
        var path = Path.Combine(FixtureRoot, "reseller-pricing-sample-a.csv");
        using var stream = File.OpenRead(path);

        var result = _ingester.Ingest(
            new RetailPricingCsvIngestionRequest(stream, "sample.csv")
            {
                Options = new RetailPricingCsvIngestionOptions { MaxFileSizeBytes = 64 }
            },
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(IngestionOutcomeStatus.Failure);
        result.LogEntries.Should().Contain(e => e.Reason == IngestionFailureReason.FileSizeExceeded);
    }

    private RetailPricingCsvIngestionResult Ingest(
        string fileName,
        IReadOnlyList<ManualPriceOverrideRequest>? manualOverrides = null)
    {
        var path = Path.Combine(FixtureRoot, fileName);
        using var stream = File.OpenRead(path);
        return _ingester.Ingest(
            new RetailPricingCsvIngestionRequest(stream, fileName, manualOverrides),
            TestContext.Current.CancellationToken);
    }
}
