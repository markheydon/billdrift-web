using BillDrift.Domain.Common;
using BillDrift.Domain.Import;
using BillDrift.Domain.Import.Stripe;
using FluentAssertions;

namespace BillDrift.Domain.Tests.Import;

public class RawImportTests
{
    [Fact]
    public void RawImportId_equality_is_based_on_composite_key()
    {
        var a = RawImportId.Create(ImportSourceKind.GiacomBillingPdf, "doc-1", "line-1");
        var b = RawImportId.Create(ImportSourceKind.GiacomBillingPdf, "doc-1", "line-1");
        var c = RawImportId.Create(ImportSourceKind.GiacomBillingPdf, "doc-1", "line-2");

        a.Should().Be(b);
        a.Should().NotBe(c);
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

        line.ProductNameRaw.Should().Be("Product As Written");
        line.MexIdRaw.Should().Be(" MEX99 ");
        line.ChargeTypeRaw.Should().Be("Recurring");
    }

    [Fact]
    public void RawStripe_types_preserve_metadata()
    {
        var item = new RawStripeSubscriptionItem(
            "si_test",
            "sub_test",
            "price_test",
            "prod_test",
            3,
            new Dictionary<string, string> { ["mex_id"] = "MEX1" });

        item.Metadata["mex_id"].Should().Be("MEX1");
        item.Quantity.Should().Be(3);
    }
}
