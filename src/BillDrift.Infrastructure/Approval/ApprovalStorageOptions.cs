namespace BillDrift.Infrastructure.Approval;

/// <summary>Configuration for Azure Table and Blob storage used by the approval workflow.</summary>
public sealed class ApprovalStorageOptions
{
    /// <summary>Azure Table name for proposals, decisions, audit, and export metadata.</summary>
    public string TableName { get; set; } = "reconciliationapprovals";

    /// <summary>Blob container name for approved changeset JSON exports.</summary>
    public string ChangesetContainerName { get; set; } = "approved-changesets";
}
