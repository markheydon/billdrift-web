namespace BillDrift.Infrastructure.Import.Giacom.Internal;

internal sealed record ColumnDefinition(
    string Name,
    double MinX,
    double MaxX)
{
    public bool Contains(double x) => x >= MinX && x <= MaxX;
}
