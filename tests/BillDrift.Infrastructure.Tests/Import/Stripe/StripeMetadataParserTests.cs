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

        Assert.Equal("MEX1", metadata["mex_id"]);
        Assert.Equal("OFF1", metadata["offer_id"]);
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

        Assert.Equal("MEX2", StripeMetadataParser.GetMexId(metadata));
        Assert.Equal("SKU2", StripeMetadataParser.GetSkuId(metadata));
    }

    [Fact]
    public void GetSupplierReferences_collects_supplier_prefix_keys()
    {
        var metadata = new Dictionary<string, string>
        {
            ["supplier_ref"] = "REF-1",
            ["giacom_ref"] = "REF-2"
        };

        Assert.Equivalent(new[] { "REF-1", "REF-2" }, StripeMetadataParser.GetSupplierReferences(metadata));
    }
}
