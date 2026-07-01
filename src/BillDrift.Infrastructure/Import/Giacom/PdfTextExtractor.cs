using BillDrift.Application.Import;
using BillDrift.Infrastructure.Import.Giacom.Internal;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.Content;

namespace BillDrift.Infrastructure.Import.Giacom;

public sealed class PdfTextExtractor
{
    public sealed record ExtractionResult(
        IReadOnlyList<PdfTextLine> Lines,
        IngestionFailureReason? FailureReason);

    public ExtractionResult Extract(byte[] pdfBytes, CancellationToken cancellationToken)
    {
        try
        {
            using var document = PdfDocument.Open(pdfBytes);
            if (document.NumberOfPages > GiacomIngestionLimits.MaxPageCount)
            {
                return new ExtractionResult([], IngestionFailureReason.PageLimitExceeded);
            }

            var words = new List<PdfWord>();
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var word in page.GetWords())
                {
                    var box = word.BoundingBox;
                    words.Add(new PdfWord(
                        word.Text,
                        box.Left,
                        box.Bottom,
                        box.Width,
                        box.Height,
                        page.Number));
                }
            }

            if (words.Count == 0)
            {
                return new ExtractionResult([], IngestionFailureReason.DocumentUnreadable);
            }

            var lines = GroupWordsIntoLines(words);
            return new ExtractionResult(lines, null);
        }
        catch (PdfDocumentEncryptedException)
        {
            return new ExtractionResult([], IngestionFailureReason.DocumentEncrypted);
        }
        catch (Exception)
        {
            return new ExtractionResult([], IngestionFailureReason.DocumentUnreadable);
        }
    }

    internal static List<PdfTextLine> GroupWordsIntoLines(IReadOnlyList<PdfWord> words)
    {
        return words
            .GroupBy(w => w.PageNumber)
            .OrderBy(g => g.Key)
            .SelectMany(pageWords => GroupWordsOnPage(pageWords.ToList()))
            .ToList();
    }

    private static List<PdfTextLine> GroupWordsOnPage(IReadOnlyList<PdfWord> words)
    {
        var sorted = words
            .OrderByDescending(w => w.Y)
            .ThenBy(w => w.X)
            .ToList();

        var lines = new List<PdfTextLine>();
        if (sorted.Count == 0)
        {
            return lines;
        }

        var currentCluster = new List<PdfWord> { sorted[0] };
        var clusterY = sorted[0].Y;

        for (var i = 1; i < sorted.Count; i++)
        {
            var word = sorted[i];
            if (Math.Abs(word.Y - clusterY) <= GiacomIngestionLimits.LineGroupingYTolerance)
            {
                currentCluster.Add(word);
            }
            else
            {
                lines.Add(BuildLine(currentCluster));
                currentCluster = [word];
                clusterY = word.Y;
            }
        }

        lines.Add(BuildLine(currentCluster));
        return lines;
    }

    private static PdfTextLine BuildLine(List<PdfWord> cluster)
    {
        var ordered = cluster.OrderBy(w => w.X).ToList();
        var baselineY = ordered.Average(w => w.Y);
        var text = string.Join(" ", ordered.Select(w => w.Text));
        return new PdfTextLine(ordered, ordered[0].PageNumber, baselineY, text);
    }
}
