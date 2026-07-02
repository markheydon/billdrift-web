namespace BillDrift.Infrastructure.Import.Stripe.Internal;

/// <summary>
/// Parse-stage price row before mapping to domain records.
/// </summary>
internal sealed class ParsedPriceRow
{
    public required int RowNumber { get; init; }
    public string? PriceId { get; init; }
    public string? ProductId { get; init; }
    public string? Currency { get; init; }
    public string? UnitAmountRaw { get; init; }
    public string? RecurringInterval { get; init; }
    public string? RecurringIntervalCountRaw { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> AdditionalFields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
