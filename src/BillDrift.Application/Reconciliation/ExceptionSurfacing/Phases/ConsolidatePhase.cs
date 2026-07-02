namespace BillDrift.Application.Reconciliation.ExceptionSurfacing.Phases;

/// <summary>
/// Phase 3 — merges catalogue exceptions sharing the same commercial key (CR-1).
/// </summary>
public sealed class ConsolidatePhase
{
    private static readonly HashSet<ExceptionCategory> CatalogueCategories =
    [
        ExceptionCategory.StripeProductMissing,
        ExceptionCategory.StripePriceMissing,
        ExceptionCategory.StripePriceRrpMismatch
    ];

    /// <summary>Consolidates catalogue-domain exceptions per commercial key.</summary>
    public void Execute(SurfacingContext context)
    {
        var catalogue = context.Candidates
            .Where(c => c.Domain == ReconciliationDomain.PricingVsCatalogue &&
                        CatalogueCategories.Contains(c.Category))
            .ToList();

        var others = context.Candidates
            .Except(catalogue)
            .ToList();

        var merged = catalogue
            .GroupBy(c => c.Product?.CommercialKey)
            .Select(g => MergeGroup(g.ToList()))
            .ToList();

        context.Candidates.Clear();
        context.Candidates.AddRange(others);
        context.Candidates.AddRange(merged);
    }

    private static SurfacedException MergeGroup(IReadOnlyList<SurfacedException> group)
    {
        if (group.Count == 1)
        {
            return group[0];
        }

        var ordered = group.OrderBy(c => c.Id.Value, StringComparer.Ordinal).ToList();
        var survivor = ordered[0];
        var maxSeverity = ordered.Max(c => c.Severity);
        var evidence = ordered
            .SelectMany(c => c.Evidence)
            .GroupBy(e => (e.Source, e.Field, e.Value))
            .Select(g => g.First())
            .ToList();

        var siblingCount = ordered.Sum(c => c.SuppressedSiblingCount) + (ordered.Count - 1);

        return survivor with
        {
            Severity = maxSeverity,
            Evidence = evidence,
            SuppressedSiblingCount = siblingCount,
            SourceMismatchIds = ordered.SelectMany(c => c.SourceMismatchIds).Distinct().ToList()
        };
    }
}
