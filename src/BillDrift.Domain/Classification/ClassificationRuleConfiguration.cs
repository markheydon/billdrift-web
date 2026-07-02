using BillDrift.Domain.Common;

namespace BillDrift.Domain.Classification;

/// <summary>
/// A single product category inference rule evaluated in declaration order.
/// </summary>
public sealed record ProductCategoryRule(
    string MatchPattern,
    ProductCategoryMatchKind MatchKind,
    ProductCategory Category);

/// <summary>
/// Operator-configurable classification rules loaded from persistence.
/// </summary>
public sealed record ClassificationRuleConfiguration(
    IReadOnlyList<MexId> InternalMexIds,
    IReadOnlyList<ProductCategoryRule> ProductCategoryRules,
    bool RequireNotesForAlertSuppression = true)
{
    /// <summary>Default empty configuration.</summary>
    public static ClassificationRuleConfiguration Default { get; } = new([], []);
}
