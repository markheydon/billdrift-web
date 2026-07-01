namespace BillDrift.Infrastructure.Import.Giacom.Internal;

internal sealed record PdfWord(
    string Text,
    double X,
    double Y,
    double Width,
    double Height,
    int PageNumber);
