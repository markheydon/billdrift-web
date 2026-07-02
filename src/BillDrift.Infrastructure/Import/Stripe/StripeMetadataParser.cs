using System.Text.RegularExpressions;

namespace BillDrift.Infrastructure.Import.Stripe;

/// <summary>
/// Extracts mapping metadata from Stripe CSV metadata columns (bracket syntax and flat keys).
/// </summary>
/// <remarks>
/// Canonical keys are lowercased (mex_id, offer_id, sku_id). Supplier reference keys are preserved.
/// Missing keys are not invented — callers emit warnings instead.
/// </remarks>
internal static partial class StripeMetadataParser
{
    private static readonly string[] KnownFlatKeys =
        ["mex_id", "MexId", "offer_id", "OfferId", "sku_id", "SkuId"];

    private static readonly string[] SupplierPrefixes =
        ["supplier_ref", "supplier_reference", "giacom_ref"];

    [GeneratedRegex(@"^metadata\[(.+)\]$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MetadataBracketPattern();

    public static Dictionary<string, string> ExtractFromHeaders(
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, string> row)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            if (!row.TryGetValue(header, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var bracket = MetadataBracketPattern().Match(header.Trim());
            if (bracket.Success)
            {
                var key = NormalizeKey(bracket.Groups[1].Value);
                metadata[key] = value.Trim();
                continue;
            }

            if (IsKnownMetadataHeader(header))
            {
                metadata[NormalizeKey(header)] = value.Trim();
            }
        }

        return metadata;
    }

    public static string? GetMexId(IReadOnlyDictionary<string, string> metadata) =>
        GetValue(metadata, "mex_id", "MexId");

    public static string? GetOfferId(IReadOnlyDictionary<string, string> metadata) =>
        GetValue(metadata, "offer_id", "OfferId");

    public static string? GetSkuId(IReadOnlyDictionary<string, string> metadata) =>
        GetValue(metadata, "sku_id", "SkuId");

    public static IReadOnlyList<string> GetSupplierReferences(IReadOnlyDictionary<string, string> metadata) =>
        metadata
            .Where(kvp => SupplierPrefixes.Any(p =>
                kvp.Key.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.StartsWith("supplier_", StringComparison.OrdinalIgnoreCase)))
            .Select(kvp => kvp.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static Dictionary<string, string> NormalizeMetadataKeys(IReadOnlyDictionary<string, string> metadata)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in metadata)
        {
            normalized[NormalizeKey(key)] = value;
        }

        return normalized;
    }

    private static bool IsKnownMetadataHeader(string header)
    {
        if (KnownFlatKeys.Any(k => header.Equals(k, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return SupplierPrefixes.Any(p => header.Equals(p, StringComparison.OrdinalIgnoreCase))
            || header.StartsWith("supplier_", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    private static string? GetValue(IReadOnlyDictionary<string, string> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
