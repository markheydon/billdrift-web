namespace BillDrift.Application.Import;

/// <summary>
/// Identifies which Stripe dashboard CSV export type is being ingested.
/// </summary>
public enum StripeCsvFileKind
{
    /// <summary>Subscriptions export (All Columns).</summary>
    Subscriptions = 0,

    /// <summary>Products catalogue export.</summary>
    Products = 1,

    /// <summary>Prices catalogue export.</summary>
    Prices = 2
}
