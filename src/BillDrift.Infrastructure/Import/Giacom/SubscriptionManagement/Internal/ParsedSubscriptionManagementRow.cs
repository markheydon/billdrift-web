namespace BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement.Internal;

/// <summary>
/// Parse-stage row with logical field values after header alias resolution.
/// </summary>
internal sealed class ParsedSubscriptionManagementRow
{
    public required int RowNumber { get; init; }
    public required IReadOnlyDictionary<SubscriptionManagementLogicalField, string?> Fields { get; init; }
}
