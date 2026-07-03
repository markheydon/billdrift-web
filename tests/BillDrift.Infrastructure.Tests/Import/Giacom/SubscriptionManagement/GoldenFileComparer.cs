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

        actual.RawRows.Should().HaveCount(expected.RawRows.Count);
        actual.SubscriptionLines.Should().HaveCount(expected.SubscriptionLines.Count);
        actual.Status.ToString().Should().Be(expected.Status);
        actual.Summary.RowsEmitted.Should().Be(expected.Summary.RowsEmitted);
        actual.Summary.RowsExcludedByScope.Should().Be(expected.Summary.RowsExcludedByScope);
        actual.Summary.RowsSkipped.Should().Be(expected.Summary.RowsSkipped);

        for (var i = 0; i < expected.RawRows.Count; i++)
        {
            var exp = expected.RawRows[i];
            var act = actual.RawRows[i];

            act.MexIdRaw.Should().Be(exp.MexIdRaw);
            act.OfferIdRaw.Should().Be(exp.OfferIdRaw);
            act.SkuIdRaw.Should().Be(exp.SkuIdRaw);
            act.LicencesRaw.Should().Be(exp.LicencesRaw);
            act.StatusRaw.Should().Be(exp.StatusRaw);
            act.Id.SourceLineKey.Should().Be(exp.SourceLineKey);
        }

        for (var i = 0; i < expected.SubscriptionLines.Count; i++)
        {
            var exp = expected.SubscriptionLines[i];
            var act = actual.SubscriptionLines[i];

            act.Customer.MexId.Value.Should().Be(exp.MexId);
            act.CommercialKeyRoot.OfferId.Value.Should().Be(exp.OfferId);
            act.CommercialKeyRoot.SkuId.Value.Should().Be(exp.SkuId);
            act.LicenceCount.Should().Be(exp.LicenceCount);
            act.Status.ToString().Should().Be(exp.Status);
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
