using System.Text;
using BillDrift.Domain.Mapping;

namespace BillDrift.Application.Reconciliation.Matching;

/// <summary>
/// Deterministic fuzzy name matcher using token-set Jaccard similarity (research R3).
/// Minimum threshold 0.85; tie-break by score, then lexicographically smallest commercial key root, then mapping ID.
/// </summary>
public sealed class DeterministicFuzzyNameMatcher
{
    /// <summary>Minimum Jaccard similarity score for a candidate to qualify.</summary>
    public const double MinimumThreshold = 0.85;

    /// <summary>
    /// Finds product mapping candidates whose supplier name variants score above the threshold.
    /// </summary>
    /// <param name="supplierName">Raw supplier product name to match.</param>
    /// <param name="mappings">Available product mappings with name variants.</param>
    /// <returns>Qualifying candidates ordered by score descending, then key, then mapping ID.</returns>
    public IReadOnlyList<ProductMapping> FindCandidates(string supplierName, IReadOnlyList<ProductMapping> mappings)
    {
        var normalizedInput = Normalize(supplierName);
        var inputTokens = Tokenize(normalizedInput);

        var scored = new List<(ProductMapping Mapping, double Score)>();
        foreach (var mapping in mappings)
        {
            var bestScore = mapping.SupplierNameVariants
                .Select(v => Score(inputTokens, Tokenize(Normalize(v.NormalizedName))))
                .DefaultIfEmpty(0)
                .Max();

            if (bestScore >= MinimumThreshold)
            {
                scored.Add((mapping, bestScore));
            }
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Mapping.Key.OfferId.Value, StringComparer.Ordinal)
            .ThenBy(s => s.Mapping.Key.SkuId.Value, StringComparer.Ordinal)
            .ThenBy(s => s.Mapping.Id.Value)
            .Select(s => s.Mapping)
            .ToList();
    }

    /// <summary>
    /// Computes token-set Jaccard similarity between two normalized name strings.
    /// </summary>
    public double ComputeSimilarity(string nameA, string nameB)
    {
        var tokensA = Tokenize(Normalize(nameA));
        var tokensB = Tokenize(Normalize(nameB));
        return Score(tokensA, tokensB);
    }

    /// <summary>
    /// Normalizes a product name: trim, lowercase, strip punctuation, collapse whitespace.
    /// </summary>
    public static string Normalize(string name)
    {
        var trimmed = name.Trim().ToLowerInvariant();
        var sb = new StringBuilder(trimmed.Length);
        var lastWasSpace = false;
        foreach (var c in trimmed)
        {
            if (c is '.' or ',' or '(' or ')' or '-' or '/')
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }

        return sb.ToString().Trim();
    }

    private static HashSet<string> Tokenize(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    private static double Score(HashSet<string> tokensA, HashSet<string> tokensB)
    {
        if (tokensA.Count == 0 && tokensB.Count == 0)
        {
            return 1.0;
        }

        if (tokensA.Count == 0 || tokensB.Count == 0)
        {
            return 0.0;
        }

        var intersection = tokensA.Intersect(tokensB).Count();
        var union = tokensA.Union(tokensB).Count();
        return (double)intersection / union;
    }
}
