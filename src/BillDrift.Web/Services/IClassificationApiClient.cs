using BillDrift.Domain.Classification;

namespace BillDrift.Web.Services;

/// <summary>HTTP client for classification API endpoints.</summary>
public interface IClassificationApiClient
{
    Task<ItemClassification?> GetClassificationAsync(string stableKey, CancellationToken cancellationToken = default);

    Task<ItemClassification> ApplyOverrideAsync(
        string stableKey,
        ApplyClassificationOverrideRequest request,
        CancellationToken cancellationToken = default);

    Task ClearOverrideAsync(string stableKey, ClearClassificationOverrideRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetInternalMexIdsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> UpdateInternalMexIdsAsync(IReadOnlyList<string> mexIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductCategoryRule>> GetProductCategoryRulesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductCategoryRule>> UpdateProductCategoryRulesAsync(
        IReadOnlyList<ProductCategoryRule> rules,
        CancellationToken cancellationToken = default);
}

/// <summary>Request body for applying a classification override.</summary>
public sealed record ApplyClassificationOverrideRequest(
    ReconciliationItemKind Kind,
    string CustomerMexId,
    ReconciliationItemClassification Classification,
    string Notes,
    string OperatorId);

/// <summary>Request body for clearing a classification override.</summary>
public sealed record ClearClassificationOverrideRequest(
    ReconciliationItemKind Kind,
    string CustomerMexId,
    string OperatorId);
