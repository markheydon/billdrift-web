namespace BillDrift.Infrastructure.Import.Stripe;

/// <summary>
/// Filters subscription rows by Stripe status for reconciliation-focused imports.
/// </summary>
/// <remarks>
/// Default active set: active, trialing, past_due. Excluded rows are counted, not errored.
/// </remarks>
internal static class StripeStatusFilter
{
    private static readonly HashSet<string> ActiveStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active", "trialing", "past_due"
    };

    private static readonly HashSet<string> InactiveStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "canceled", "unpaid", "incomplete", "incomplete_expired", "paused"
    };

    public static bool ShouldInclude(string? status, bool includeInactive)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        if (includeInactive)
        {
            return true;
        }

        return ActiveStatuses.Contains(status.Trim());
    }

    public static bool IsInactiveStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) && InactiveStatuses.Contains(status.Trim());
}
