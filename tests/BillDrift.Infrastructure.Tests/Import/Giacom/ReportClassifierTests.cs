using BillDrift.Application.Import;
using BillDrift.Infrastructure.Import.Giacom;

namespace BillDrift.Infrastructure.Tests.Import.Giacom;

public class ReportClassifierTests
{
    [Fact]
    public void Classify_PreBillingMarkers_ReturnsPreBilling()
    {
        var result = ReportClassifier.Classify(["Giacom Pre-Billing Report", "Reseller summary"]);
        Assert.Equal(GiacomReportType.PreBilling, result);
    }

    [Fact]
    public void Classify_PostBillingMarkers_ReturnsPostBilling()
    {
        var result = ReportClassifier.Classify(["Tax Invoice", "Post-Billing"]);
        Assert.Equal(GiacomReportType.PostBilling, result);
    }

    [Fact]
    public void Classify_UnknownMarkers_ReturnsUnknown()
    {
        var result = ReportClassifier.Classify(["Supplier Report"]);
        Assert.Equal(GiacomReportType.Unknown, result);
    }
}
