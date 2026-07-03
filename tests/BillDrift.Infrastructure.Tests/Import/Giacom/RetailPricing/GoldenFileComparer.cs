using System.Text.Json;
using BillDrift.Application.Import;

namespace BillDrift.Infrastructure.Tests.Import.Giacom.RetailPricing;

public static class GoldenFileComparer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void AssertResultMatchesGolden(
        RetailPricingCsvIngestionResult actual,
        string goldenFilePath)
    {
        var expectedJson = File.ReadAllText(goldenFilePath);
        var expected = JsonSerializer.Deserialize<GoldenResult>(expectedJson, JsonOptions)
            ?? throw new InvalidOperationException($"Golden file empty: {goldenFilePath}");

        actual.Status.ToString().Should().Be(expected.Status);
        actual.RawCatalogueRows.Should().HaveCount(expected.RawCatalogueRows.Count);
        actual.ResolvedPrices.Should().HaveCount(expected.ResolvedPrices.Count);
        actual.Summary.CatalogueRowsEmitted.Should().Be(expected.Summary.CatalogueRowsEmitted);
        actual.Summary.CatalogueRowsSkipped.Should().Be(expected.Summary.CatalogueRowsSkipped);
        actual.Summary.ResolvedPriceCount.Should().Be(expected.Summary.ResolvedPriceCount);

        for (var i = 0; i < expected.RawCatalogueRows.Count; i++)
        {
            var exp = expected.RawCatalogueRows[i];
            var act = actual.RawCatalogueRows[i];

            act.OfferIdRaw.Should().Be(exp.OfferIdRaw);
            act.SkuIdRaw.Should().Be(exp.SkuIdRaw);
            act.TermRaw.Should().Be(exp.TermRaw);
            act.FrequencyRaw.Should().Be(exp.FrequencyRaw);
            act.WholesaleRaw.Should().Be(exp.WholesaleRaw);
            act.RrpRaw.Should().Be(exp.RrpRaw);
            act.Id.SourceLineKey.Should().Be(exp.SourceLineKey);
        }

        for (var i = 0; i < expected.ResolvedPrices.Count; i++)
        {
            var exp = expected.ResolvedPrices[i];
            var act = actual.ResolvedPrices[i];

            act.Key.OfferId.Value.Should().Be(exp.OfferId);
            act.Key.SkuId.Value.Should().Be(exp.SkuId);
            act.Key.Term.ToString().Should().Be(exp.Term);
            act.Key.Frequency.ToString().Should().Be(exp.Frequency);
            act.Rrp.Amount.Should().Be(exp.Rrp);
            act.Wholesale.Amount.Should().Be(exp.Wholesale);
            act.Source.ToString().Should().Be(exp.Source);
            act.Status.ToString().Should().Be(exp.Status);
        }
    }

    public static void WriteGoldenFile(RetailPricingCsvIngestionResult result, string goldenFilePath)
    {
        var golden = new GoldenResult
        {
            Status = result.Status.ToString(),
            Summary = new GoldenSummary
            {
                CatalogueRowsEmitted = result.Summary.CatalogueRowsEmitted,
                CatalogueRowsSkipped = result.Summary.CatalogueRowsSkipped,
                ResolvedPriceCount = result.Summary.ResolvedPriceCount
            },
            RawCatalogueRows = result.RawCatalogueRows.Select(r => new GoldenRawRow
            {
                OfferIdRaw = r.OfferIdRaw,
                SkuIdRaw = r.SkuIdRaw,
                TermRaw = r.TermRaw,
                FrequencyRaw = r.FrequencyRaw,
                WholesaleRaw = r.WholesaleRaw,
                RrpRaw = r.RrpRaw,
                SourceLineKey = r.Id.SourceLineKey
            }).ToList(),
            ResolvedPrices = result.ResolvedPrices.Select(p => new GoldenResolvedPrice
            {
                OfferId = p.Key.OfferId.Value,
                SkuId = p.Key.SkuId.Value,
                Term = p.Key.Term.ToString(),
                Frequency = p.Key.Frequency.ToString(),
                Wholesale = p.Rrp.Amount,
                Rrp = p.Rrp.Amount,
                Source = p.Source.ToString(),
                Status = p.Status.ToString()
            }).ToList()
        };

        for (var i = 0; i < golden.ResolvedPrices.Count; i++)
        {
            golden.ResolvedPrices[i] = golden.ResolvedPrices[i] with
            {
                Wholesale = result.ResolvedPrices[i].Wholesale.Amount,
                Rrp = result.ResolvedPrices[i].Rrp.Amount
            };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(goldenFilePath)!);
        File.WriteAllText(goldenFilePath, JsonSerializer.Serialize(golden, JsonOptions));
    }

    private sealed class GoldenResult
    {
        public string Status { get; set; } = string.Empty;
        public GoldenSummary Summary { get; set; } = new();
        public List<GoldenRawRow> RawCatalogueRows { get; set; } = [];
        public List<GoldenResolvedPrice> ResolvedPrices { get; set; } = [];
    }

    private sealed class GoldenSummary
    {
        public int CatalogueRowsEmitted { get; set; }
        public int CatalogueRowsSkipped { get; set; }
        public int ResolvedPriceCount { get; set; }
    }

    private sealed class GoldenRawRow
    {
        public string OfferIdRaw { get; set; } = string.Empty;
        public string SkuIdRaw { get; set; } = string.Empty;
        public string TermRaw { get; set; } = string.Empty;
        public string FrequencyRaw { get; set; } = string.Empty;
        public string WholesaleRaw { get; set; } = string.Empty;
        public string RrpRaw { get; set; } = string.Empty;
        public string SourceLineKey { get; set; } = string.Empty;
    }

    private sealed record GoldenResolvedPrice
    {
        public string OfferId { get; init; } = string.Empty;
        public string SkuId { get; init; } = string.Empty;
        public string Term { get; init; } = string.Empty;
        public string Frequency { get; init; } = string.Empty;
        public decimal Wholesale { get; init; }
        public decimal Rrp { get; init; }
        public string Source { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }
}
