namespace BillDrift.Application.Import.Giacom;

/// <summary>Intake limits for Giacom billing PDF uploads.</summary>
public static class GiacomPdfIngestionOptions
{
    /// <summary>Maximum PDF file size (20 MB), matching feature 002 intake.</summary>
    public const int MaxFileSizeBytes = 20 * 1024 * 1024;
}
