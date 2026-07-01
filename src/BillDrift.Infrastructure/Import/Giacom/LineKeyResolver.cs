using BillDrift.Infrastructure.Import.Giacom.Internal;

namespace BillDrift.Infrastructure.Import.Giacom;

public static class LineKeyResolver
{
    public static string Resolve(ParsedProductLine line)
    {
        var reference = line.SupplierReferenceIds.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
        if (!string.IsNullOrWhiteSpace(reference))
        {
            return reference.Trim();
        }

        return $"{line.PageNumber}:{line.BlockIndex}:{line.LineIndex}";
    }
}
