namespace BillDrift.Domain.Mapping;

/// <summary>
/// A supplier product name variant as it appears on Giacom billing PDFs, normalized for lookup matching.
/// </summary>
/// <param name="NormalizedName">Lowercase trimmed name used for deterministic lookup against supplier cost lines.</param>
/// <param name="DisplayName">Original variant text as written on the supplier invoice, for operator display.</param>
public sealed record SupplierNameVariant(
    string NormalizedName,
    string DisplayName);
