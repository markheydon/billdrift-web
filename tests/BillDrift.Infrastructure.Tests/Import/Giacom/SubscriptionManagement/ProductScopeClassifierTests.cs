using BillDrift.Application.Import;
using BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;
using BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement.Internal;

namespace BillDrift.Infrastructure.Tests.Import.Giacom.SubscriptionManagement;

public class ProductScopeClassifierTests
{
    private readonly ProductScopeClassifier _classifier = new();

    [Fact]
    public void Microsoft_365_row_is_included()
    {
        var row = CreateRow(service: "Microsoft 365", productName: "Microsoft 365 Business Standard", productType: "CSP");
        _classifier.Classify(row).Decision.Should().Be(ProductScopeDecision.Include);
    }

    [Fact]
    public void Exclaimer_row_is_excluded()
    {
        var row = CreateRow(service: "Exclaimer", productName: "Exclaimer Cloud Signatures", productType: "Third Party");
        var result = _classifier.Classify(row);

        result.Decision.Should().Be(ProductScopeDecision.Exclude);
        result.Reason.Should().Be(IngestionFailureReason.ProductOutOfScope);
    }

    [Fact]
    public void Sparse_service_with_m365_product_name_is_included_with_warning()
    {
        var row = CreateRow(service: null, productName: "Microsoft 365 E3", productType: null);
        _classifier.Classify(row).Decision.Should().Be(ProductScopeDecision.Include);
    }

    [Fact]
    public void Commercial_keys_without_product_name_are_included_with_warning()
    {
        var row = CreateRow(service: null, productName: null, productType: null, offerId: "OFFER-1", skuId: "SKU-1");
        var result = _classifier.Classify(row);

        result.Decision.Should().Be(ProductScopeDecision.IncludeWithAmbiguityWarning);
        result.Reason.Should().Be(IngestionFailureReason.ProductScopeAmbiguous);
    }

    private static ParsedSubscriptionManagementRow CreateRow(
        string? service,
        string? productName,
        string? productType,
        string? offerId = "OFFER-1",
        string? skuId = "SKU-1")
    {
        return new ParsedSubscriptionManagementRow
        {
            RowNumber = 1,
            Fields = new Dictionary<SubscriptionManagementLogicalField, string?>
            {
                [SubscriptionManagementLogicalField.Service] = service,
                [SubscriptionManagementLogicalField.ProductName] = productName,
                [SubscriptionManagementLogicalField.ProductType] = productType,
                [SubscriptionManagementLogicalField.OfferId] = offerId,
                [SubscriptionManagementLogicalField.SkuId] = skuId
            }
        };
    }
}
