using System.Text.RegularExpressions;
using BillDrift.Infrastructure.Import.Giacom.Internal;

namespace BillDrift.Infrastructure.Import.Giacom;

internal static class CustomerBlockSegmenter
{
    // Mex ID appears as a labeled field or standalone token (MEX#####).
    private static readonly Regex MexIdToken = new(@"MEX\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MexIdLabel = new(@"(?i)(?:mex\s*id|sub\s*account)\s*:?\s*(MEX\d+)", RegexOptions.Compiled);
    private static readonly Regex CustomerLabel = new(@"(?i)customer(?:\s*name)?\s*:\s*(.+?)(?:\s+mex|\s*$)", RegexOptions.Compiled);

    public static IReadOnlyList<(int StartLineIndex, string? CustomerName, string? MexId, int PageNumber)> FindBlockHeaders(
        IReadOnlyList<PdfTextLine> lines)
    {
        var headers = new List<(int StartLineIndex, string? CustomerName, string? MexId, int PageNumber)>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (IsIgnoredLine(line.Text))
            {
                continue;
            }

            var mexId = ExtractMexId(line.Text);
            string? customerName = null;

            if (mexId is not null)
            {
                customerName = ExtractCustomerName(line.Text, lines, i);
                headers.Add((i, customerName, mexId, line.PageNumber));
                continue;
            }

            // Standalone Mex ID on a short line — customer name is usually on the preceding row.
            if (MexIdToken.IsMatch(line.Text) && line.Text.Trim().Length <= 20)
            {
                mexId = MexIdToken.Match(line.Text).Value;
                customerName = i > 0 ? lines[i - 1].Text.Trim() : null;
                headers.Add((i, customerName, mexId, line.PageNumber));
            }
        }

        return headers;
    }

    public static IReadOnlyList<CustomerBlock> Segment(
        IReadOnlyList<PdfTextLine> lines,
        IReadOnlyList<ColumnDefinition> columns,
        ProductLineParser lineParser)
    {
        var headers = FindBlockHeaders(lines);
        if (headers.Count == 0)
        {
            return [];
        }

        var blocks = new List<CustomerBlock>();
        for (var h = 0; h < headers.Count; h++)
        {
            var (startIndex, customerName, mexId, pageNumber) = headers[h];
            var endIndex = h + 1 < headers.Count ? headers[h + 1].StartLineIndex : lines.Count;
            var blockLines = lines.Skip(startIndex + 1).Take(endIndex - startIndex - 1).ToList();
            var productLines = lineParser.ParseBlockLines(blockLines, columns, h, pageNumber);
            blocks.Add(new CustomerBlock(h, pageNumber, customerName, mexId, productLines));
        }

        return blocks;
    }

    private static string? ExtractMexId(string text)
    {
        var labelMatch = MexIdLabel.Match(text);
        if (labelMatch.Success)
        {
            return labelMatch.Groups[1].Value;
        }

        var tokenMatch = MexIdToken.Match(text);
        return tokenMatch.Success ? tokenMatch.Value : null;
    }

    private static string? ExtractCustomerName(string text, IReadOnlyList<PdfTextLine> lines, int lineIndex)
    {
        var labelMatch = CustomerLabel.Match(text);
        if (labelMatch.Success)
        {
            return labelMatch.Groups[1].Value.Trim();
        }

        // Text before "Mex ID" on the same line when no explicit label is present.
        var beforeMex = Regex.Split(text, @"(?i)mex\s*id")[0].Trim();
        if (!string.IsNullOrWhiteSpace(beforeMex) && !beforeMex.Contains(':'))
        {
            return beforeMex;
        }

        if (lineIndex > 0)
        {
            var previous = lines[lineIndex - 1].Text.Trim();
            if (!IsIgnoredLine(previous) && !MexIdToken.IsMatch(previous))
            {
                return previous;
            }
        }

        return null;
    }

    private static bool IsIgnoredLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (text.Contains("Page ", StringComparison.OrdinalIgnoreCase) &&
            text.Contains(" of ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Repeated column header rows inside multi-page blocks.
        var lower = text.ToLowerInvariant();
        return lower.Contains("product") && lower.Contains("qty") && lower.Contains("cost");
    }
}
