using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.Indexing;

/// <summary>
/// Index of intended prices keyed by <see cref="CommercialKey"/>.
/// Manual override entries take precedence over catalogue entries for the same key (FR-017).
/// </summary>
public sealed class IntendedPriceIndex
{
    private readonly Dictionary<CommercialKey, IntendedPrice> _byKey = new();

    /// <summary>
    /// Builds an index from normalized intended price records.
    /// On duplicate <see cref="CommercialKey"/>, <see cref="PriceSource.ManualOverride"/> wins over <see cref="PriceSource.Catalogue"/>.
    /// </summary>
    /// <param name="prices">Intended prices from the reconciliation input snapshot.</param>
    /// <returns>A populated intended price index.</returns>
    public static IntendedPriceIndex Build(IReadOnlyList<IntendedPrice> prices)
    {
        var index = new IntendedPriceIndex();
        foreach (var price in prices)
        {
            if (index._byKey.TryGetValue(price.Key, out var existing))
            {
                // Manual override wins on key collision (research R4).
                if (price.Source == PriceSource.ManualOverride ||
                    existing.Source != PriceSource.ManualOverride)
                {
                    index._byKey[price.Key] = price;
                }
            }
            else
            {
                index._byKey[price.Key] = price;
            }
        }

        return index;
    }

    /// <summary>
    /// Attempts to retrieve the winning intended price for a commercial key.
    /// </summary>
    /// <param name="key">The commercial key to look up.</param>
    /// <param name="price">The winning intended price when found.</param>
    /// <returns><see langword="true"/> when a price exists for the key.</returns>
    public bool TryGet(CommercialKey key, out IntendedPrice price) =>
        _byKey.TryGetValue(key, out price!);

    /// <summary>
    /// Returns all commercial keys present in the index.
    /// </summary>
    /// <returns>Read-only set of indexed commercial keys.</returns>
    public IReadOnlySet<CommercialKey> GetAllKeys() => _byKey.Keys.ToHashSet();
}
