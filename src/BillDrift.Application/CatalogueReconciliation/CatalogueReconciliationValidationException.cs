namespace BillDrift.Application.CatalogueReconciliation;

/// <summary>Thrown when catalogue reconciliation inputs fail validation, before any run is produced or persisted.</summary>
public sealed class CatalogueReconciliationValidationException : Exception
{
    /// <summary>Creates the exception with a validation failure message.</summary>
    public CatalogueReconciliationValidationException(string message)
        : base(message)
    {
    }
}
