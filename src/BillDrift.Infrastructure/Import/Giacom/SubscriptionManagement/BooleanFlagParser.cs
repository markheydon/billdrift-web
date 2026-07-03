namespace BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;

/// <summary>
/// Parses NCE and trial flag columns from Giacom Subscription Management CSV exports.
/// </summary>
internal static class BooleanFlagParser
{
    public static bool? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "y" or "yes" or "true" or "1" => true,
            "n" or "no" or "false" or "0" => false,
            _ => null
        };
    }

    public static bool IsRecognised(string? raw) =>
        string.IsNullOrWhiteSpace(raw) || Parse(raw) is not null;
}
