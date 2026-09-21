using BillDrift.Domain.Common;
using BillDrift.Domain.Import;
using BillDrift.Domain.Import.Stripe;

namespace BillDrift.Domain.Tests.Import;

public class RawImportTests
{
    [Fact]
    public void RawImportId_equality_is_based_on_composite_key()
    {
        var a = RawImportId.Create(ImportSourceKind.GiacomBillingPdf, "doc-1", "line-1");
        var b = RawImportId.Create(ImportSourceKind.GiacomBillingPdf, "doc-1", "line-1");
        var c = RawImportId.Create(ImportSourceKind.GiacomBillingPdf, "doc-1", "line-2");

        Assert.Equal(b, a);
        Assert.NotEqual(c, a);
    }

    [Fact]
    public void RawGiacomBillingLine_preserves_source_fields()
    {
        var id = RawImportId.Create(ImportSourceKind.GiacomBillingPdf, "billing.pdf", "REF-1");
        var line = new RawGiacomBillingLine(
            id,
            " MEX99 ",
            "Product As Written",
            "5",
            "Recurring",
            "2026-01-01",
            "2026-01-31",
            "50.00",
            ["REF-1"],
            "billing.pdf",
            DateTimeOffset.Parse("2026-01-01Z"));

        Assert.Equal("Product As Written", line.ProductNameRaw);
        Assert.Equal(" MEX99 ", line.MexIdRaw);
        Assert.Equal("Recurring", line.ChargeTypeRaw);
    }

    [Fact]
    public void RawStripe_types_preserve_metadata()
    {
        var item = new RawStripeSubscriptionItem(
            RawImportId.Create(ImportSourceKind.StripeExport, "doc-1", "si_test"),
            "si_test",
            "sub_test",
            "price_test",
            "prod_test",
            "cus_test",
            3,
            "Product",
            "active",
            "10.00",
            "month",
            1,
            new Dictionary<string, string> { ["mex_id"] = "MEX1" });

        Assert.Equal("MEX1", item.Metadata["mex_id"]);
        Assert.Equal(3, item.Quantity);
    }
}
