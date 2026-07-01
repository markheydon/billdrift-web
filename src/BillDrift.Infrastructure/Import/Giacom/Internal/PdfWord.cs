namespace BillDrift.Infrastructure.Import.Giacom.Internal;

public sealed record PdfWord(
    string Text,
    double X,
    double Y,
    double Width,
    double Height,
    int PageNumber);
