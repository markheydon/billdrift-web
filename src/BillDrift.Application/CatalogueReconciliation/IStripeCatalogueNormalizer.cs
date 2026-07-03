using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Import.Stripe;

namespace BillDrift.Application.CatalogueReconciliation;

/// <summary>Normalizes raw Stripe export records into catalogue reconciliation snapshots.</summary>
public interface IStripeCatalogueNormalizer
{
    /// <summary>Maps raw Stripe products to catalogue product snapshots.</summary>
    IReadOnlyList<StripeCatalogueProduct> NormalizeProducts(IReadOnlyList<RawStripeProduct> products);

    /// <summary>Maps raw Stripe prices to catalogue price snapshots.</summary>
    IReadOnlyList<StripeCataloguePrice> NormalizePrices(IReadOnlyList<RawStripePrice> prices);
}
