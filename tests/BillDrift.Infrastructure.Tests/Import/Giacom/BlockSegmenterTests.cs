using BillDrift.Infrastructure.Import.Giacom;
using BillDrift.Infrastructure.Import.Giacom.Internal;

namespace BillDrift.Infrastructure.Tests.Import.Giacom;

public class BlockSegmenterTests
{
    [Fact]
    public void FindBlockHeaders_DetectsMexIdHeaders()
    {
        var lines = new List<PdfTextLine>
        {
            Line(1, "Customer: Acme Corp    Mex ID: MEX10001"),
            Line(1, "Microsoft 365 10 Recurring 120.00 REF-1"),
            Line(1, "Customer: Beta Ltd    Mex ID: MEX10002"),
            Line(1, "Teams 5 Recurring 50.00 REF-2")
        };

        var headers = CustomerBlockSegmenter.FindBlockHeaders(lines);

        Assert.Equal(2, headers.Count);
        Assert.Equal("MEX10001", headers[0].MexId);
        Assert.Equal("Acme Corp", headers[0].CustomerName);
        Assert.Equal("MEX10002", headers[1].MexId);
    }

    private static PdfTextLine Line(int page, string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((w, i) => new PdfWord(w, 40 + (i * 50), 700, 40, 10, page))
            .ToList();
        return new PdfTextLine(words, page, 700, text);
    }
}
