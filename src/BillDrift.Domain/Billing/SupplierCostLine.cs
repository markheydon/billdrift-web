using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

/// <summary>
/// Normalized supplier cost from a Giacom billing PDF, representing what the supplier charged for a product in a billing period.
/// Pro-rated adjustments (<see cref="ChargeType.ProRatedAdjustment"/>) must not be aggregated with recurring quantities.
/// </summary>
/// <param name="Id">Domain-generated identifier assigned during normalization.</param>
/// <param name="Customer">Customer identity anchored on MexId.</param>
/// <param name="ProductName">Normalized product name as written on the supplier invoice.</param>
/// <param name="Quantity">Licence quantity; must be non-negative for recurring charges.</param>
/// <param name="ChargeType">Whether this is a recurring charge or a pro-rated adjustment.</param>
/// <param name="Period">Billing period covered by this line.</param>
/// <param name="LineCost">Total cost for this line (may be negative for pro-rated credits).</param>
/// <param name="SupplierReferences">Reference IDs extracted from PDF columns for correlation.</param>
/// <param name="Source">Traceability link back to the raw PDF import.</param>
public sealed record SupplierCostLine(
    SupplierCostLineId Id,
    CustomerIdentity Customer,
    string ProductName,
    int Quantity,
    ChargeType ChargeType,
    BillingPeriod Period,
    Money LineCost,
    IReadOnlyList<SupplierReferenceId> SupplierReferences,
    SourceReference Source);
