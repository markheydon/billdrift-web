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
        Assert.Equal(ProductScopeDecision.Include, _classifier.Classify(row).Decision);
    }

    [Fact]
    public void Exclaimer_row_is_excluded()
    {
        var row = CreateRow(service: "Exclaimer", productName: "Exclaimer Cloud Signatures", productType: "Third Party");
        var result = _classifier.Classify(row);

        Assert.Equal(ProductScopeDecision.Exclude, result.Decision);
        Assert.Equal(IngestionFailureReason.ProductOutOfScope, result.Reason);
    }

    [Fact]
    public void Sparse_service_with_m365_product_name_is_included_with_warning()
    {
        var row = CreateRow(service: null, productName: "Microsoft 365 E3", productType: null);
        Assert.Equal(ProductScopeDecision.Include, _classifier.Classify(row).Decision);
    }

    [Fact]
    public void Commercial_keys_without_product_name_are_included_with_warning()
    {
        var row = CreateRow(service: null, productName: null, productType: null, offerId: "OFFER-1", skuId: "SKU-1");
        var result = _classifier.Classify(row);

        Assert.Equal(ProductScopeDecision.IncludeWithAmbiguityWarning, result.Decision);
        Assert.Equal(IngestionFailureReason.ProductScopeAmbiguous, result.Reason);
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
