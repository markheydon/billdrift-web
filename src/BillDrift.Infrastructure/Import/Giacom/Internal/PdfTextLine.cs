namespace BillDrift.Infrastructure.Import.Giacom.Internal;

public sealed record PdfTextLine(
    IReadOnlyList<PdfWord> Words,
    int PageNumber,
    double BaselineY,
    string Text);
