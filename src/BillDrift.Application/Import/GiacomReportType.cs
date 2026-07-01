namespace BillDrift.Application.Import;

/// <summary>
/// Classifies which Giacom billing report variant was ingested, affecting downstream normalization expectations.
/// </summary>
public enum GiacomReportType
{
    /// <summary>Report type could not be determined from document structure or headers.</summary>
    Unknown = 0,

    /// <summary>Pre-billing report issued before final invoice generation.</summary>
    PreBilling = 1,

    /// <summary>Post-billing report reflecting finalized charges.</summary>
    PostBilling = 2
}
