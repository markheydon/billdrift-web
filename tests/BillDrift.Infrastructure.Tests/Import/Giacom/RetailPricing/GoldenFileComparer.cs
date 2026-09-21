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

        Assert.Equal(expected.Status, actual.Status.ToString());
        Assert.Equal(expected.RawCatalogueRows.Count, actual.RawCatalogueRows.Count);
        Assert.Equal(expected.ResolvedPrices.Count, actual.ResolvedPrices.Count);
        Assert.Equal(expected.Summary.CatalogueRowsEmitted, actual.Summary.CatalogueRowsEmitted);
        Assert.Equal(expected.Summary.CatalogueRowsSkipped, actual.Summary.CatalogueRowsSkipped);
        Assert.Equal(expected.Summary.ResolvedPriceCount, actual.Summary.ResolvedPriceCount);

        for (var i = 0; i < expected.RawCatalogueRows.Count; i++)
        {
            var exp = expected.RawCatalogueRows[i];
            var act = actual.RawCatalogueRows[i];

            Assert.Equal(exp.OfferIdRaw, act.OfferIdRaw);
            Assert.Equal(exp.SkuIdRaw, act.SkuIdRaw);
            Assert.Equal(exp.TermRaw, act.TermRaw);
            Assert.Equal(exp.FrequencyRaw, act.FrequencyRaw);
            Assert.Equal(exp.WholesaleRaw, act.WholesaleRaw);
            Assert.Equal(exp.RrpRaw, act.RrpRaw);
            Assert.Equal(exp.SourceLineKey, act.Id.SourceLineKey);
        }

        for (var i = 0; i < expected.ResolvedPrices.Count; i++)
        {
            var exp = expected.ResolvedPrices[i];
            var act = actual.ResolvedPrices[i];

            Assert.Equal(exp.OfferId, act.Key.OfferId.Value);
            Assert.Equal(exp.SkuId, act.Key.SkuId.Value);
            Assert.Equal(exp.Term, act.Key.Term.ToString());
            Assert.Equal(exp.Frequency, act.Key.Frequency.ToString());
            Assert.Equal(exp.Rrp, act.Rrp.Amount);
            Assert.Equal(exp.Wholesale, act.Wholesale.Amount);
            Assert.Equal(exp.Source, act.Source.ToString());
            Assert.Equal(exp.Status, act.Status.ToString());
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
