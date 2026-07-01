using BillDrift.Infrastructure.Import.Giacom.Internal;

namespace BillDrift.Infrastructure.Import.Giacom;

internal static class LineKeyResolver
{
    public static string Resolve(ParsedProductLine line)
    {
        // Prefer the first non-empty supplier reference; otherwise fall back to positional key for idempotency.
        var reference = line.SupplierReferenceIds.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
        if (!string.IsNullOrWhiteSpace(reference))
        {
            return reference.Trim();
        }

        return $"{line.PageNumber}:{line.BlockIndex}:{line.LineIndex}";
    }
}
