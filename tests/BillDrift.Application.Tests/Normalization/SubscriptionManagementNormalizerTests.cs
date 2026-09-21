using BillDrift.Application.Normalization;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;

namespace BillDrift.Application.Tests.Normalization;

public class SubscriptionManagementNormalizerTests
{
    private readonly SubscriptionManagementNormalizer _normalizer = new();

    [Fact]
    public void Normalizes_mex_id_to_uppercase_trimmed_form()
    {
        var raw = CreateRaw(mexId: "  mex001  ", offerId: "OFFER-1", skuId: "SKU-1", licences: "5");
        var line = _normalizer.Normalize(raw);

        Assert.Equal("MEX001", line.Customer.MexId.Value);
        Assert.Equal("  mex001  ", raw.MexIdRaw);
    }

    [Fact]
    public void Normalizes_commercial_keys_with_trim()
    {
        var raw = CreateRaw(mexId: "MEX001", offerId: " OFFER-1 ", skuId: " SKU-1 ", licences: "5");
        var line = _normalizer.Normalize(raw);

        Assert.Equal("OFFER-1", line.CommercialKeyRoot.OfferId.Value);
        Assert.Equal("SKU-1", line.CommercialKeyRoot.SkuId.Value);
    }

    [Fact]
    public void Preserves_customer_display_name_without_cross_row_merge()
    {
        var raw = CreateRaw(
            mexId: "MEX001",
            offerId: "OFFER-1",
            skuId: "SKU-1",
            licences: "5",
            customerName: "  Contoso Ltd  ");
        var line = _normalizer.Normalize(raw);

        Assert.Equal("Contoso Ltd", line.Customer.DisplayName);
    }

    [Fact]
    public void Throws_when_commercial_key_is_incomplete()
    {
        var raw = CreateRaw(mexId: "MEX001", offerId: "", skuId: "SKU-1", licences: "5");
        var act = () => _normalizer.Normalize(raw);
        Assert.Throws<NormalizationException>(act);
    }

    private static RawSubscriptionManagementRow CreateRaw(
        string mexId,
        string offerId,
        string skuId,
        string licences,
        string customerName = "Customer")
    {
        return new RawSubscriptionManagementRow(
            RawImportId.Create(ImportSourceKind.GiacomSubscriptionManagement, "doc-1", "1"),
            customerName,
            mexId,
            null,
            offerId,
            skuId,
            licences,
            "Annual",
            "Monthly",
            null,
            "Active",
            null,
            "doc-1",
            1);
    }
}
