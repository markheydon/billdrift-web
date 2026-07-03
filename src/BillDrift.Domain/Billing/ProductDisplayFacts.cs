namespace BillDrift.Domain.Billing;

/// <summary>
/// Product names and service labels as written in the Giacom Subscription Management export.
/// Used for operator display only; not used as reconciliation match keys.
/// </summary>
/// <param name="Service">Service or product family label from the export.</param>
/// <param name="ProductName">Product or subscription name as written.</param>
/// <param name="ProductType">Product type label (e.g. CSP, NCE) as written.</param>
public sealed record ProductDisplayFacts(
    string? Service = null,
    string? ProductName = null,
    string? ProductType = null);
