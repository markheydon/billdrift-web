using System.Text.RegularExpressions;
using BillDrift.Infrastructure.Import.Giacom.Internal;

namespace BillDrift.Infrastructure.Import.Giacom;

public sealed class ProductLineParser
{
    private static readonly Regex PeriodRangePattern = new(
        @"(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})\s*(?:-|to)\s*(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<ParsedProductLine> ParseBlockLines(
        IReadOnlyList<PdfTextLine> blockLines,
        IReadOnlyList<ColumnDefinition> columns,
        int blockIndex,
        int pageNumber)
    {
        var lines = new List<ParsedProductLine>();
        var lineIndex = 0;

        foreach (var pdfLine in blockLines)
        {
            if (string.IsNullOrWhiteSpace(pdfLine.Text) || IsNonProductRow(pdfLine.Text))
            {
                continue;
            }

            var fields = ExtractFields(pdfLine, columns);

            if (IsContinuationRow(fields))
            {
                lines.Add(new ParsedProductLine(
                    blockIndex,
                    lineIndex++,
                    pdfLine.PageNumber,
                    fields.ProductNameRaw ?? string.Empty,
                    null,
                    null,
                    null,
                    null,
                    null,
                    [],
                    false));
                continue;
            }

            if (!IsProductLineCandidate(fields))
            {
                continue;
            }

            var (periodStart, periodEnd) = SplitPeriod(fields.PeriodRaw);
            lines.Add(new ParsedProductLine(
                blockIndex,
                lineIndex++,
                pdfLine.PageNumber,
                fields.ProductNameRaw ?? string.Empty,
                fields.QuantityRaw,
                NormalizeChargeTypeRaw(fields.ChargeTypeRaw),
                periodStart,
                periodEnd,
                fields.LineCostRaw,
                fields.ReferenceIds,
                false));
        }

        return lines;
    }

    internal static FieldValues ExtractFields(PdfTextLine line, IReadOnlyList<ColumnDefinition> columns)
    {
        var buckets = columns.ToDictionary(c => c.Name, _ => new List<string>());

        foreach (var word in line.Words)
        {
            var centerX = word.X + (word.Width / 2);
            var column = ResolveColumn(centerX, columns);
            if (column is null)
            {
                buckets["ProductName"].Add(word.Text);
            }
            else
            {
                buckets[column.Name].Add(word.Text);
            }
        }

        var values = new FieldValues(
            Join(buckets, "ProductName"),
            Join(buckets, "Quantity"),
            Join(buckets, "ChargeType"),
            Join(buckets, "Period"),
            Join(buckets, "LineCost"),
            buckets.GetValueOrDefault("References")?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList() ?? []);

        return SplitMergedPeriodAndCost(values);
    }

    private static ColumnDefinition? ResolveColumn(double centerX, IReadOnlyList<ColumnDefinition> columns)
    {
        var matches = columns.Where(c => c.Contains(centerX)).ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        return matches.Count == 1
            ? matches[0]
            : matches.OrderByDescending(c => c.MinX).First();
    }

    private static FieldValues SplitMergedPeriodAndCost(FieldValues fields)
    {
        if (!string.IsNullOrWhiteSpace(fields.LineCostRaw) ||
            string.IsNullOrWhiteSpace(fields.PeriodRaw))
        {
            return fields;
        }

        var match = Regex.Match(fields.PeriodRaw, @"^(?<period>.+?)(?<cost>-?\d+\.\d{2})$");
        if (!match.Success)
        {
            return fields;
        }

        return fields with
        {
            PeriodRaw = match.Groups["period"].Value.Trim(),
            LineCostRaw = match.Groups["cost"].Value.Trim()
        };
    }

    internal static bool IsProductLineCandidate(FieldValues fields)
    {
        var populated = 0;
        if (!string.IsNullOrWhiteSpace(fields.ProductNameRaw))
        {
            populated++;
        }

        if (!string.IsNullOrWhiteSpace(fields.QuantityRaw))
        {
            populated++;
        }

        if (!string.IsNullOrWhiteSpace(fields.LineCostRaw))
        {
            populated++;
        }

        return populated >= 2;
    }

    internal static bool IsContinuationRow(FieldValues fields) =>
        !string.IsNullOrWhiteSpace(fields.ProductNameRaw) &&
        string.IsNullOrWhiteSpace(fields.QuantityRaw) &&
        string.IsNullOrWhiteSpace(fields.ChargeTypeRaw) &&
        string.IsNullOrWhiteSpace(fields.LineCostRaw);

    private static string? NormalizeChargeTypeRaw(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Recurring";
        }

        return raw.Trim();
    }

    private static (string? Start, string? End) SplitPeriod(string? periodRaw)
    {
        if (string.IsNullOrWhiteSpace(periodRaw))
        {
            return (null, null);
        }

        var match = PeriodRangePattern.Match(periodRaw);
        if (match.Success)
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }

        return (periodRaw, null);
    }

    private static bool IsNonProductRow(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("subtotal") || lower.Contains("total") ||
               (lower.Contains("product") && lower.Contains("qty"));
    }

    private static string? Join(Dictionary<string, List<string>> buckets, string key) =>
        buckets.TryGetValue(key, out var values) && values.Count > 0
            ? string.Join(" ", values).Trim()
            : null;

    internal sealed record FieldValues(
        string? ProductNameRaw,
        string? QuantityRaw,
        string? ChargeTypeRaw,
        string? PeriodRaw,
        string? LineCostRaw,
        IReadOnlyList<string> ReferenceIds);
}
