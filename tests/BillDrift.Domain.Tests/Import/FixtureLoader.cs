using System.Text.Json;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;

namespace BillDrift.Domain.Tests.Import;

public static class FixtureLoader
{
    public static RawGiacomBillingLine LoadGiacomBillingSample()
    {
        var path = Path.Combine(GetFixturesPath(), "giacom-billing-sample.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var id = root.GetProperty("id");

        return new RawGiacomBillingLine(
            RawImportId.Create(
                Enum.Parse<ImportSourceKind>(id.GetProperty("sourceKind").GetString()!),
                id.GetProperty("sourceDocumentId").GetString()!,
                id.GetProperty("sourceLineKey").GetString()!),
            root.GetProperty("mexIdRaw").GetString()!,
            root.GetProperty("productNameRaw").GetString()!,
            root.GetProperty("quantityRaw").GetString()!,
            root.GetProperty("chargeTypeRaw").GetString()!,
            root.GetProperty("periodStartRaw").GetString(),
            root.GetProperty("periodEndRaw").GetString(),
            root.GetProperty("lineCostRaw").GetString()!,
            root.GetProperty("supplierReferenceIds").EnumerateArray().Select(e => e.GetString()!).ToList(),
            root.GetProperty("sourceDocumentId").GetString()!,
            root.GetProperty("extractedAt").GetDateTimeOffset());
    }

    private static string GetFixturesPath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures"));
}
