using BillDrift.Infrastructure.Import.Giacom.Internal;

namespace BillDrift.Infrastructure.Import.Giacom;

internal static class ColumnDetector
{
    // Maps header tokens to logical column names; aliases tolerate Giacom label drift across report variants.
    private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ProductName"] = ["Product", "Description", "Product Name"],
        ["Quantity"] = ["Qty", "Quantity", "Licences"],
        ["ChargeType"] = ["Type", "Charge Type", "Charge"],
        ["Period"] = ["Period", "Period From", "Start", "From", "Period To", "End", "To"],
        ["LineCost"] = ["Cost", "Amount", "Line Total", "Net"],
        ["References"] = ["Ref", "Reference", "Order Ref", "Sub Ref"]
    };

    public static IReadOnlyList<ColumnDefinition> DetectColumns(IReadOnlyList<PdfTextLine> lines)
    {
        var headerLine = FindHeaderLine(lines);
        if (headerLine is null)
        {
            return DefaultColumns();
        }

        var headerWords = headerLine.Words.OrderBy(w => w.X).ToList();
        var columnCenters = new List<(string Name, double CenterX)>();

        foreach (var word in headerWords)
        {
            var logicalName = ResolveColumnName(word.Text);
            if (logicalName is not null)
            {
                columnCenters.Add((logicalName, word.X + (word.Width / 2)));
            }
        }

        if (columnCenters.Count < 3)
        {
            return DefaultColumns();
        }

        columnCenters = columnCenters
            .GroupBy(c => c.Name)
            .Select(g => g.First())
            .OrderBy(c => c.CenterX)
            .ToList();

        var definitions = new List<ColumnDefinition>();
        for (var i = 0; i < columnCenters.Count; i++)
        {
            // Column X-ranges are midpoints between adjacent header centers; outer columns extend by a fixed margin.
            var minX = i == 0
                ? columnCenters[i].CenterX - GiacomIngestionLimits.OuterColumnExtensionPoints
                : (columnCenters[i - 1].CenterX + columnCenters[i].CenterX) / 2;

            var maxX = i == columnCenters.Count - 1
                ? columnCenters[i].CenterX + GiacomIngestionLimits.OuterColumnExtensionPoints
                : (columnCenters[i].CenterX + columnCenters[i + 1].CenterX) / 2;

            // Product name column absorbs wrapped text — keep it wide by stopping before the next column center.
            if (columnCenters[i].Name == "ProductName" && i + 1 < columnCenters.Count)
            {
                maxX = columnCenters[i + 1].CenterX - 5;
            }

            definitions.Add(new ColumnDefinition(columnCenters[i].Name, minX, maxX));
        }

        return definitions;
    }

    private static PdfTextLine? FindHeaderLine(IReadOnlyList<PdfTextLine> lines)
    {
        // Scan the first three pages for a row with at least three recognized header aliases.
        var pages = lines.Select(l => l.PageNumber).Distinct().OrderBy(p => p).Take(3);
        foreach (var page in pages)
        {
            foreach (var line in lines.Where(l => l.PageNumber == page))
            {
                var aliasHits = line.Words.Count(w => ResolveColumnName(w.Text) is not null);
                if (aliasHits >= 3)
                {
                    return line;
                }
            }
        }

        return null;
    }

    private static string? ResolveColumnName(string token)
    {
        foreach (var (name, aliases) in HeaderAliases)
        {
            if (aliases.Any(a => a.Equals(token, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }

        return null;
    }

    private static IReadOnlyList<ColumnDefinition> DefaultColumns() =>
    [
        new("ProductName", 0, 280),
        new("Quantity", 280, 340),
        new("ChargeType", 340, 410),
        new("Period", 410, 490),
        new("LineCost", 490, 560),
        new("References", 560, 650)
    ];
}
