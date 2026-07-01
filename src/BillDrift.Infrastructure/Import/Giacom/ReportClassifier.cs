using BillDrift.Application.Import;

namespace BillDrift.Infrastructure.Import.Giacom;

internal static class ReportClassifier
{
    private static readonly string[] PreBillingMarkers =
    [
        "pre-billing",
        "pre billing",
        "estimate"
    ];

    private static readonly string[] PostBillingMarkers =
    [
        "post-billing",
        "post billing",
        "tax invoice",
        "invoice"
    ];

    public static GiacomReportType Classify(IReadOnlyList<string> firstPageLines)
    {
        // Classification uses first-page text only; unknown markers do not fail ingestion.
        var text = string.Join('\n', firstPageLines).ToLowerInvariant();

        if (PreBillingMarkers.Any(text.Contains))
        {
            return GiacomReportType.PreBilling;
        }

        if (PostBillingMarkers.Any(text.Contains))
        {
            return GiacomReportType.PostBilling;
        }

        return GiacomReportType.Unknown;
    }
}
