using BillDrift.Domain.Billing;
using BillDrift.Domain.Import;

namespace BillDrift.Application.Normalization;

/// <summary>
/// Transforms a raw Giacom billing line from PDF ingestion into a normalized <see cref="SupplierCostLine"/> for reconciliation.
/// </summary>
public interface IGiacomBillingNormalizer
{
    /// <summary>
    /// Normalizes a single <see cref="RawGiacomBillingLine"/> into domain billing fields (customer, period, cost, charge type).
    /// </summary>
    /// <param name="raw">Raw line produced by <see cref="Import.IGiacomBillingPdfIngester"/>.</param>
    /// <returns>A fully populated <see cref="SupplierCostLine"/>.</returns>
    /// <exception cref="NormalizationException">Thrown when a required field cannot be parsed or validated.</exception>
    SupplierCostLine Normalize(RawGiacomBillingLine raw);
}

/// <summary>
/// Transforms a raw Microsoft subscription management row into a normalized <see cref="MicrosoftSubscriptionLine"/>.
/// </summary>
public interface ISubscriptionManagementNormalizer
{
    /// <summary>
    /// Normalizes a subscription management CSV row into domain fields (status, licence count, commercial key).
    /// </summary>
    /// <param name="raw">Raw row from subscription management import.</param>
    /// <returns>A fully populated <see cref="MicrosoftSubscriptionLine"/>.</returns>
    /// <exception cref="NormalizationException">Thrown when a required field cannot be parsed or validated.</exception>
    MicrosoftSubscriptionLine Normalize(RawSubscriptionManagementRow raw);
}

/// <summary>
/// Transforms raw price list or manual price entries into normalized <see cref="IntendedPrice"/> records for reconciliation.
/// </summary>
public interface IPriceListNormalizer
{
    /// <summary>
    /// Normalizes a reseller price list CSV row into an <see cref="IntendedPrice"/> with <see cref="BillDrift.Domain.Common.PriceSource.Catalogue"/> provenance.
    /// </summary>
    /// <param name="raw">Raw row from the reseller pricing CSV.</param>
    /// <returns>A fully populated <see cref="IntendedPrice"/>.</returns>
    /// <exception cref="NormalizationException">Thrown when a required field cannot be parsed or validated.</exception>
    IntendedPrice Normalize(RawPriceListRow raw);

    /// <summary>
    /// Normalizes an operator-entered manual price override into an <see cref="IntendedPrice"/> with <see cref="BillDrift.Domain.Common.PriceSource.ManualOverride"/> provenance.
    /// </summary>
    /// <param name="raw">Raw manual price entry supplied by an operator.</param>
    /// <returns>A fully populated <see cref="IntendedPrice"/>.</returns>
    /// <exception cref="NormalizationException">Thrown when a required field cannot be parsed or validated.</exception>
    IntendedPrice Normalize(RawManualPriceEntry raw);
}
