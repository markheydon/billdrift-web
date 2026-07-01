namespace BillDrift.Infrastructure.Import.Giacom.Internal;

internal sealed record PdfTextLine(
    IReadOnlyList<PdfWord> Words,
    int PageNumber,
    double BaselineY,
    string Text);
