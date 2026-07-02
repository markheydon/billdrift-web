namespace BillDrift.Domain.Classification;

/// <summary>
/// Origin classification for a reconciliation item, driving engine guards and exception suppression.
/// </summary>
public enum ReconciliationItemClassification
{
    MicrosoftCsp,
    NonCspSupplier,
    Internal,
    CustomService
}

/// <summary>
/// Whether classification was assigned automatically or via operator override.
/// </summary>
public enum ClassificationSource
{
    Automatic,
    ManualOverride
}

/// <summary>
/// Confidence tier for an automatic classification assignment.
/// </summary>
public enum ClassificationConfidence
{
    High,
    Medium,
    Low
}
