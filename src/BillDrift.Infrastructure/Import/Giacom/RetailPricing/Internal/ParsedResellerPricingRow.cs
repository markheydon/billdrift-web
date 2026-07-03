namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing.Internal;

/// <summary>Intermediate row after CSV header mapping, before raw domain mapping.</summary>
internal sealed class ParsedResellerPricingRow
{
    public required int RowNumber { get; init; }

    public required IReadOnlyDictionary<ResellerPricingLogicalField, string?> Fields { get; init; }
}
