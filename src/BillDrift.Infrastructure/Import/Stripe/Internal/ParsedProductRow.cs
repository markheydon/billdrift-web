namespace BillDrift.Infrastructure.Import.Stripe.Internal;

/// <summary>
/// Parse-stage product row before mapping to domain records.
/// </summary>
internal sealed class ParsedProductRow
{
    public required int RowNumber { get; init; }
    public string? ProductId { get; init; }
    public string? Name { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> AdditionalFields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
