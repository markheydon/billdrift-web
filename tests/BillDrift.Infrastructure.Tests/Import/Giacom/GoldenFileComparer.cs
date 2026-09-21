using System.Text.Json;
using BillDrift.Domain.Import;

namespace BillDrift.Infrastructure.Tests.Import.Giacom;

public static class GoldenFileComparer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void AssertLinesMatchGolden(
        IReadOnlyList<RawGiacomBillingLine> actual,
        string goldenFilePath)
    {
        var expectedJson = File.ReadAllText(goldenFilePath);
        var expected = JsonSerializer.Deserialize<List<GoldenLine>>(expectedJson, JsonOptions)
            ?? throw new InvalidOperationException($"Golden file empty: {goldenFilePath}");

        Assert.Equal(expected.Count, actual.Count);

        for (var i = 0; i < expected.Count; i++)
        {
            var exp = expected[i];
            var act = actual[i];

            Assert.Equal(exp.MexIdRaw, act.MexIdRaw);
            Assert.Equal(exp.ProductNameRaw, act.ProductNameRaw);
            Assert.Equal(exp.QuantityRaw, act.QuantityRaw);
            Assert.Equal(exp.ChargeTypeRaw, act.ChargeTypeRaw);
            Assert.Equal(exp.PeriodStartRaw, act.PeriodStartRaw);
            Assert.Equal(exp.PeriodEndRaw, act.PeriodEndRaw);
            Assert.Equal(exp.LineCostRaw, act.LineCostRaw);
            Assert.Equivalent(exp.SupplierReferenceIds, act.SupplierReferenceIds);
            Assert.Equal(exp.SourceLineKey, act.Id.SourceLineKey);
        }
    }

    public static void WriteGoldenFile(IReadOnlyList<RawGiacomBillingLine> lines, string goldenFilePath)
    {
        var golden = lines.Select(l => new GoldenLine
        {
            MexIdRaw = l.MexIdRaw,
            ProductNameRaw = l.ProductNameRaw,
            QuantityRaw = l.QuantityRaw,
            ChargeTypeRaw = l.ChargeTypeRaw,
            PeriodStartRaw = l.PeriodStartRaw,
            PeriodEndRaw = l.PeriodEndRaw,
            LineCostRaw = l.LineCostRaw,
            SupplierReferenceIds = l.SupplierReferenceIds.ToList(),
            SourceLineKey = l.Id.SourceLineKey
        }).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(goldenFilePath)!);
        File.WriteAllText(goldenFilePath, JsonSerializer.Serialize(golden, JsonOptions));
    }

    private sealed class GoldenLine
    {
        public string MexIdRaw { get; set; } = string.Empty;
        public string ProductNameRaw { get; set; } = string.Empty;
        public string QuantityRaw { get; set; } = string.Empty;
        public string ChargeTypeRaw { get; set; } = string.Empty;
        public string? PeriodStartRaw { get; set; }
        public string? PeriodEndRaw { get; set; }
        public string LineCostRaw { get; set; } = string.Empty;
        public List<string> SupplierReferenceIds { get; set; } = [];
        public string SourceLineKey { get; set; } = string.Empty;
    }
}
