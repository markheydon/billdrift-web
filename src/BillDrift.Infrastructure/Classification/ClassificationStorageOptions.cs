namespace BillDrift.Infrastructure.Classification;

/// <summary>
/// Configuration for Azure Table and optional Blob storage used by classification persistence.
/// </summary>
public sealed class ClassificationStorageOptions
{
    /// <summary>Azure Table name for classification entities.</summary>
    public string TableName { get; set; } = "itemclassifications";

    /// <summary>Optional Blob container for config snapshot exports.</summary>
    public string ConfigBlobContainer { get; set; } = "classification-config";
}
