namespace BillDrift.Infrastructure.History;

/// <summary>Configuration for run history Azure storage.</summary>
public sealed class RunHistoryStorageOptions
{
    /// <summary>Azure Table name for run history entities.</summary>
    public string TableName { get; set; } = "reconciliationrunhistory";

    /// <summary>Azure Blob container for run archives.</summary>
    public string BlobContainerName { get; set; } = "reconciliation-runs";

    /// <summary>Default retention period in months.</summary>
    public int DefaultRetentionMonths { get; set; } = 24;
}
