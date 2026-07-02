using BillDrift.Infrastructure.Import.Stripe;

namespace BillDrift.Infrastructure.Tests.Import.Stripe;

public class StripeMetadataParserTests
{
    [Fact]
    public void ExtractFromHeaders_parses_bracket_columns()
    {
        var headers = new[] { "metadata[mex_id]", "metadata[offer_id]" };
        var row = new Dictionary<string, string>
        {
            ["metadata[mex_id]"] = "MEX1",
            ["metadata[offer_id]"] = "OFF1"
        };

        var metadata = StripeMetadataParser.ExtractFromHeaders(headers, row);

        metadata["mex_id"].Should().Be("MEX1");
        metadata["offer_id"].Should().Be("OFF1");
    }

    [Fact]
    public void ExtractFromHeaders_parses_flat_known_keys()
    {
        var headers = new[] { "MexId", "SkuId" };
        var row = new Dictionary<string, string>
        {
            ["MexId"] = "MEX2",
            ["SkuId"] = "SKU2"
        };

        var metadata = StripeMetadataParser.ExtractFromHeaders(headers, row);

        StripeMetadataParser.GetMexId(metadata).Should().Be("MEX2");
        StripeMetadataParser.GetSkuId(metadata).Should().Be("SKU2");
    }

    [Fact]
    public void GetSupplierReferences_collects_supplier_prefix_keys()
    {
        var metadata = new Dictionary<string, string>
        {
            ["supplier_ref"] = "REF-1",
            ["giacom_ref"] = "REF-2"
        };

        StripeMetadataParser.GetSupplierReferences(metadata).Should().BeEquivalentTo(["REF-1", "REF-2"]);
    }
}
