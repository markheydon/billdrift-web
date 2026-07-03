namespace BillDrift.Infrastructure.Ingestion;

/// <summary>Configuration for Azure Blob and Table ingestion persistence.</summary>
public sealed class IngestionStorageOptions
{
    /// <summary>Blob container for ingestion uploads and result payloads.</summary>
    public string BlobContainerName { get; set; } = "ingestion-uploads";

    /// <summary>Table name for ingestion run index rows.</summary>
    public string TableName { get; set; } = "ingestionruns";
}
