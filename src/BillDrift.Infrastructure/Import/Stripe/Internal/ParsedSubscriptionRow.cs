namespace BillDrift.Infrastructure.Import.Stripe.Internal;

/// <summary>
/// Parse-stage subscription row before mapping to domain records.
/// </summary>
internal sealed class ParsedSubscriptionRow
{
    public required int RowNumber { get; init; }
    public string? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? SubscriptionId { get; init; }
    public string? SubscriptionItemId { get; init; }
    public string? ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? PriceId { get; init; }
    public string? QuantityRaw { get; init; }
    public string? Status { get; init; }
    public string? UnitAmountRaw { get; init; }
    public string? IntervalRaw { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
