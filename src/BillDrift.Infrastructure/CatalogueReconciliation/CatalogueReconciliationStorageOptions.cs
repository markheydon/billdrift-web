namespace BillDrift.Infrastructure.CatalogueReconciliation;

/// <summary>Storage options for catalogue reconciliation runs.</summary>
public sealed class CatalogueReconciliationStorageOptions
{
    /// <summary>Azure Blob container name for catalogue run archives.</summary>
    public string BlobContainerName { get; set; } = "catalogue-reconciliation-runs";

    /// <summary>Azure Table name for catalogue run index.</summary>
    public string TableName { get; set; } = "cataloguereconciliationruns";
}
