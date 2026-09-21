using System.Text.Json;
using BillDrift.Application.Import;

namespace BillDrift.Infrastructure.Tests.Import.Giacom.SubscriptionManagement;

public static class GoldenFileComparer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void AssertResultMatchesGolden(
        SubscriptionManagementCsvIngestionResult actual,
        string goldenFilePath)
    {
        var expectedJson = File.ReadAllText(goldenFilePath);
        var expected = JsonSerializer.Deserialize<GoldenResult>(expectedJson, JsonOptions)
            ?? throw new InvalidOperationException($"Golden file empty: {goldenFilePath}");

        Assert.Equal(expected.RawRows.Count, actual.RawRows.Count);
        Assert.Equal(expected.SubscriptionLines.Count, actual.SubscriptionLines.Count);
        Assert.Equal(expected.Status, actual.Status.ToString());
        Assert.Equal(expected.Summary.RowsEmitted, actual.Summary.RowsEmitted);
        Assert.Equal(expected.Summary.RowsExcludedByScope, actual.Summary.RowsExcludedByScope);
        Assert.Equal(expected.Summary.RowsSkipped, actual.Summary.RowsSkipped);

        for (var i = 0; i < expected.RawRows.Count; i++)
        {
            var exp = expected.RawRows[i];
            var act = actual.RawRows[i];

            Assert.Equal(exp.MexIdRaw, act.MexIdRaw);
            Assert.Equal(exp.OfferIdRaw, act.OfferIdRaw);
            Assert.Equal(exp.SkuIdRaw, act.SkuIdRaw);
            Assert.Equal(exp.LicencesRaw, act.LicencesRaw);
            Assert.Equal(exp.StatusRaw, act.StatusRaw);
            Assert.Equal(exp.SourceLineKey, act.Id.SourceLineKey);
        }

        for (var i = 0; i < expected.SubscriptionLines.Count; i++)
        {
            var exp = expected.SubscriptionLines[i];
            var act = actual.SubscriptionLines[i];

            Assert.Equal(exp.MexId, act.Customer.MexId.Value);
            Assert.Equal(exp.OfferId, act.CommercialKeyRoot.OfferId.Value);
            Assert.Equal(exp.SkuId, act.CommercialKeyRoot.SkuId.Value);
            Assert.Equal(exp.LicenceCount, act.LicenceCount);
            Assert.Equal(exp.Status, act.Status.ToString());
        }
    }

    public static void WriteGoldenFile(SubscriptionManagementCsvIngestionResult result, string goldenFilePath)
    {
        var golden = new GoldenResult
        {
            Status = result.Status.ToString(),
            Summary = new GoldenSummary
            {
                RowsEmitted = result.Summary.RowsEmitted,
                RowsExcludedByScope = result.Summary.RowsExcludedByScope,
                RowsSkipped = result.Summary.RowsSkipped
            },
            RawRows = result.RawRows.Select(r => new GoldenRawRow
            {
                MexIdRaw = r.MexIdRaw,
                OfferIdRaw = r.OfferIdRaw,
                SkuIdRaw = r.SkuIdRaw,
                LicencesRaw = r.LicencesRaw,
                StatusRaw = r.StatusRaw,
                SourceLineKey = r.Id.SourceLineKey
            }).ToList(),
            SubscriptionLines = result.SubscriptionLines.Select(l => new GoldenSubscriptionLine
            {
                MexId = l.Customer.MexId.Value,
                OfferId = l.CommercialKeyRoot.OfferId.Value,
                SkuId = l.CommercialKeyRoot.SkuId.Value,
                LicenceCount = l.LicenceCount,
                Status = l.Status.ToString()
            }).ToList()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(goldenFilePath)!);
        File.WriteAllText(goldenFilePath, JsonSerializer.Serialize(golden, JsonOptions));
    }

    private sealed class GoldenResult
    {
        public string Status { get; set; } = string.Empty;
        public GoldenSummary Summary { get; set; } = new();
        public List<GoldenRawRow> RawRows { get; set; } = [];
        public List<GoldenSubscriptionLine> SubscriptionLines { get; set; } = [];
    }

    private sealed class GoldenSummary
    {
        public int RowsEmitted { get; set; }
        public int RowsExcludedByScope { get; set; }
        public int RowsSkipped { get; set; }
    }

    private sealed class GoldenRawRow
    {
        public string MexIdRaw { get; set; } = string.Empty;
        public string OfferIdRaw { get; set; } = string.Empty;
        public string SkuIdRaw { get; set; } = string.Empty;
        public string LicencesRaw { get; set; } = string.Empty;
        public string StatusRaw { get; set; } = string.Empty;
        public string SourceLineKey { get; set; } = string.Empty;
    }

    private sealed class GoldenSubscriptionLine
    {
        public string MexId { get; set; } = string.Empty;
        public string OfferId { get; set; } = string.Empty;
        public string SkuId { get; set; } = string.Empty;
        public int LicenceCount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
