using BillDrift.Infrastructure.Import.Giacom.Internal;

namespace BillDrift.Infrastructure.Import.Giacom;

internal static class ProductNameMerger
{
    public static IReadOnlyList<ParsedProductLine> Merge(IReadOnlyList<ParsedProductLine> lines)
    {
        if (lines.Count == 0)
        {
            return lines;
        }

        var merged = new List<ParsedProductLine>();
        ParsedProductLine? previous = null;

        foreach (var line in lines)
        {
            // Continuation rows carry only wrapped product name text — append to the preceding product line.
            var isContinuation = string.IsNullOrWhiteSpace(line.QuantityRaw) &&
                                 string.IsNullOrWhiteSpace(line.LineCostRaw) &&
                                 !string.IsNullOrWhiteSpace(line.ProductNameRaw) &&
                                 merged.Count > 0;

            if (isContinuation && previous is not null && !string.IsNullOrWhiteSpace(previous.QuantityRaw))
            {
                var last = merged[^1];
                merged[^1] = last with
                {
                    ProductNameRaw = $"{last.ProductNameRaw} {line.ProductNameRaw}".Trim(),
                    IsContinuationMerged = true
                };
                previous = merged[^1];
                continue;
            }

            merged.Add(line);
            previous = line;
        }

        return merged;
    }
}
