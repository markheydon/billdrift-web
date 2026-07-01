using BillDrift.Application.Import;

namespace BillDrift.Infrastructure.Import.Giacom;

public static class ReportClassifier
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
