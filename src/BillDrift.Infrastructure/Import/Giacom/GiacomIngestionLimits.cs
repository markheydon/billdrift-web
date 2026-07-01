namespace BillDrift.Infrastructure.Import.Giacom;

public static class GiacomIngestionLimits
{
    public const int MaxFileSizeBytes = 20 * 1024 * 1024;
    public const int MaxPageCount = 500;
    public const int MaxLogSnippetLength = 200;
    public const double LineGroupingYTolerance = 2.0;
    public const double OuterColumnExtensionPoints = 20.0;
}
