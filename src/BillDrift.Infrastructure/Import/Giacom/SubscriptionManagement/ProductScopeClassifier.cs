using BillDrift.Application.Import;
using BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement.Internal;

namespace BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;

internal enum ProductScopeDecision
{
    Include,
    Exclude,
    IncludeWithAmbiguityWarning
}

internal sealed record ProductScopeClassification(
    ProductScopeDecision Decision,
    IngestionFailureReason? Reason,
    string? Message);

/// <summary>
/// Classifies parsed rows as in-scope Microsoft 365 / CSP subscription truth or out-of-scope products.
/// </summary>
internal sealed class ProductScopeClassifier
{
    private readonly SubscriptionManagementScopeOptions _options;

    public ProductScopeClassifier(SubscriptionManagementScopeOptions? options = null)
    {
        _options = options ?? new SubscriptionManagementScopeOptions();
    }

    public ProductScopeClassification Classify(ParsedSubscriptionManagementRow row)
    {
        var service = GetField(row, SubscriptionManagementLogicalField.Service);
        var productType = GetField(row, SubscriptionManagementLogicalField.ProductType);
        var productName = GetField(row, SubscriptionManagementLogicalField.ProductName);
        var offerId = GetField(row, SubscriptionManagementLogicalField.OfferId);
        var skuId = GetField(row, SubscriptionManagementLogicalField.SkuId);

        if (ContainsDenyToken(service) || ContainsDenyToken(productType) || ContainsDenyToken(productName))
        {
            return new ProductScopeClassification(
                ProductScopeDecision.Exclude,
                IngestionFailureReason.ProductOutOfScope,
                "Row excluded because the product is outside Microsoft 365 / CSP scope.");
        }

        if (ContainsAllowServiceToken(service) ||
            ContainsAllowProductTypeToken(productType) ||
            ContainsAllowProductNameToken(productName))
        {
            return new ProductScopeClassification(ProductScopeDecision.Include, null, null);
        }

        if (!string.IsNullOrWhiteSpace(productName) && ContainsAllowProductNameToken(productName))
        {
            return AmbiguousInclude("Product name suggests Microsoft 365 but scope signals were sparse.");
        }

        if (string.IsNullOrWhiteSpace(productName) &&
            (!string.IsNullOrWhiteSpace(offerId) || !string.IsNullOrWhiteSpace(skuId)))
        {
            return AmbiguousInclude("Commercial keys present without product name; operator review recommended.");
        }

        if (!string.IsNullOrWhiteSpace(productName))
        {
            return new ProductScopeClassification(
                ProductScopeDecision.Exclude,
                IngestionFailureReason.ProductScopeAmbiguous,
                "Row excluded because product scope could not be confirmed as Microsoft 365 / CSP.");
        }

        return AmbiguousInclude("Scope classification was ambiguous; row included for operator review.");
    }

    private static ProductScopeClassification AmbiguousInclude(string message) =>
        new(ProductScopeDecision.IncludeWithAmbiguityWarning, IngestionFailureReason.ProductScopeAmbiguous, message);

    private static string? GetField(ParsedSubscriptionManagementRow row, SubscriptionManagementLogicalField field) =>
        row.Fields.TryGetValue(field, out var value) ? value : null;

    private bool ContainsDenyToken(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        _options.DenyTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private bool ContainsAllowServiceToken(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        _options.AllowServiceTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private bool ContainsAllowProductTypeToken(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        _options.AllowProductTypeTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private bool ContainsAllowProductNameToken(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        _options.AllowProductNameTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
}
