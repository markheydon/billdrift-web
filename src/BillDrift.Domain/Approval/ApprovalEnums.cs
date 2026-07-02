namespace BillDrift.Domain.Approval;

/// <summary>Operator decision state for an approval proposal.</summary>
public enum ApprovalDecisionState
{
    Pending,
    Approved,
    Rejected,
    Stale,
    Historical
}

/// <summary>Whether a proposal may be approved for bill-impacting export.</summary>
public enum ApprovalEligibility
{
    Eligible,
    InvestigationOnly,
    CatalogueConflict,
    DependencyBlocked
}

/// <summary>High-level category for queue grouping and export ordering.</summary>
public enum ApprovalProposalCategory
{
    Subscription,
    Catalogue,
    Investigation
}

/// <summary>Operator-facing risk flag on an otherwise eligible proposal.</summary>
public enum ApprovalRiskIndicator
{
    None,
    RevenueReduction,
    CatalogueWideImpact
}

/// <summary>Immutable audit event classification.</summary>
public enum ApprovalAuditEventType
{
    Decision,
    BulkDecision,
    Export,
    Supersession,
    Ingest
}
