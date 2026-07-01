using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

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
