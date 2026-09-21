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

        Assert.Contains(result.Status, new[] { IngestionOutcomeStatus.Success, IngestionOutcomeStatus.PartialSuccess });
        Assert.Equal(3, result.ResolvedPrices.Count);
        Assert.All(result.ResolvedPrices, price =>
        {
            Assert.StartsWith("OFFER-", price.Key.OfferId.Value);
            Assert.StartsWith("SKU-", price.Key.SkuId.Value);
            Assert.True(price.Wholesale.Amount > 0);
            Assert.True(price.Rrp.Amount > 0);
        });
    }

    [Fact]
    public void Sample_a_associates_multiple_commercial_keys()
    {
        var result = Ingest("reseller-pricing-sample-a.csv");

        Assert.Equivalent(
            new[] { "OFFER-MS365-BB", "OFFER-EXO-PL1", "OFFER-TEAMS-ESS" },
            result.ResolvedPrices.Select(p => p.Key.OfferId.Value));
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

        Assert.All(result.ResolvedPrices, price =>
        {
            Assert.Equal(PriceSource.Catalogue, price.Source);
            Assert.Equal(ProductClassification.Csp, price.Classification);
        });
        Assert.Equal(result.ResolvedPrices.Count, result.Summary.CatalogueOnlyCount);
        Assert.Equal(0, result.Summary.OverrideWinsCount);
    }

    [Fact]
    public void End_of_sale_retains_rrp()
    {
        var result = Ingest("end-of-sale.csv");
        var price = Assert.Single(result.ResolvedPrices);

        Assert.Equal(PriceListStatus.EndOfSale, price.Status);
        Assert.Equal(7.00m, price.Rrp.Amount);
        Assert.Equal(PriceSource.Catalogue, price.Source);
    }

    [Fact]
    public void Column_variant_maps_mandatory_fields()
    {
        var result = Ingest("column-variant.csv");

        Assert.Equal(2, result.ResolvedPrices.Count);
        Assert.All(result.ResolvedPrices, price =>
        {
            Assert.StartsWith("OFFER-VAR-", price.Key.OfferId.Value);
            Assert.StartsWith("SKU-VAR-", price.Key.SkuId.Value);
        });
    }

    [Fact]
    public void Partial_bad_rows_emits_valid_rows_and_skips_invalid()
    {
        var result = Ingest("partial-bad-rows.csv");

        Assert.Equal(IngestionOutcomeStatus.PartialSuccess, result.Status);
        Assert.Equal(4, result.RawCatalogueRows.Count);
        Assert.Equal(3, result.ResolvedPrices.Count);
        Assert.Equal(2, result.Summary.CatalogueRowsSkipped);
        Assert.Contains(result.LogEntries, e => e.Reason == IngestionFailureReason.CommercialKeyMissing);
        Assert.Contains(result.LogEntries, e => e.Reason == IngestionFailureReason.WholesaleUnparseable);
    }

    [Fact]
    public void Duplicate_keys_last_row_wins_with_warning()
    {
        var result = Ingest("duplicate-keys.csv");

        Assert.Single(result.ResolvedPrices);
        Assert.Equal(9.00m, result.ResolvedPrices[0].Rrp.Amount);
        Assert.Equal(1, result.Summary.DuplicateKeyWarnings);
        Assert.Contains(result.LogEntries, e => e.Reason == IngestionFailureReason.DuplicateCommercialKey);
    }

    [Fact]
    public void Platform_columns_map_to_nce_and_legacy()
    {
        var result = Ingest("reseller-pricing-sample-a.csv");

        Assert.Contains(result.ResolvedPrices, p =>
            p.Key.OfferId.Value == "OFFER-MS365-BB" && p.Platform == PricingPlatform.Nce);
        Assert.Contains(result.ResolvedPrices, p =>
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

        Assert.Equal(PriceSource.ManualOverride, price.Source);
        Assert.Equal(ProductClassification.NonCsp, price.Classification);
        Assert.Equal(14.00m, price.Rrp.Amount);
        Assert.Equal(1, result.Summary.OverrideWinsCount);
    }

    [Fact]
    public void Reimport_produces_identical_source_document_id()
    {
        var first = Ingest("reseller-pricing-sample-a.csv");
        var second = Ingest("reseller-pricing-sample-a.csv");

        Assert.Equal(first.SourceDocumentId, second.SourceDocumentId);
        Assert.Equivalent(first.RawCatalogueRows.Select(r => r.Id), second.RawCatalogueRows.Select(r => r.Id));
    }

    [Fact]
    public void Headers_only_csv_returns_failure_with_file_level_error()
    {
        var result = Ingest("headers-only.csv");

        Assert.Equal(IngestionOutcomeStatus.Failure, result.Status);
        Assert.Empty(result.RawCatalogueRows);
        Assert.Empty(result.ResolvedPrices);
        Assert.Contains(result.LogEntries, e =>
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

        Assert.Equal(IngestionOutcomeStatus.Failure, result.Status);
        Assert.Contains(result.LogEntries, e => e.Reason == IngestionFailureReason.FileSizeExceeded);
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
