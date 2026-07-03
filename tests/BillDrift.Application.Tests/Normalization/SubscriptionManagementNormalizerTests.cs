using BillDrift.Application.Normalization;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;
using FluentAssertions;

namespace BillDrift.Application.Tests.Normalization;

public class SubscriptionManagementNormalizerTests
{
    private readonly SubscriptionManagementNormalizer _normalizer = new();

    [Fact]
    public void Normalizes_mex_id_to_uppercase_trimmed_form()
    {
        var raw = CreateRaw(mexId: "  mex001  ", offerId: "OFFER-1", skuId: "SKU-1", licences: "5");
        var line = _normalizer.Normalize(raw);

        line.Customer.MexId.Value.Should().Be("MEX001");
        raw.MexIdRaw.Should().Be("  mex001  ");
    }

    [Fact]
    public void Normalizes_commercial_keys_with_trim()
    {
        var raw = CreateRaw(mexId: "MEX001", offerId: " OFFER-1 ", skuId: " SKU-1 ", licences: "5");
        var line = _normalizer.Normalize(raw);

        line.CommercialKeyRoot.OfferId.Value.Should().Be("OFFER-1");
        line.CommercialKeyRoot.SkuId.Value.Should().Be("SKU-1");
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

        line.Customer.DisplayName.Should().Be("Contoso Ltd");
    }

    [Fact]
    public void Throws_when_commercial_key_is_incomplete()
    {
        var raw = CreateRaw(mexId: "MEX001", offerId: "", skuId: "SKU-1", licences: "5");
        var act = () => _normalizer.Normalize(raw);
        act.Should().Throw<NormalizationException>();
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
