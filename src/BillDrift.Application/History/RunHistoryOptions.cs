namespace BillDrift.Application.History;

/// <summary>Application-level configuration for run history behavior.</summary>
public sealed class RunHistoryOptions
{
    /// <summary>Default retention period in months for archived runs.</summary>
    public int DefaultRetentionMonths { get; set; } = 24;
}
