using BillDrift.Application.Import;
using BillDrift.Application.Normalization;
using BillDrift.Infrastructure.Import.Giacom.RetailPricing;
using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Infrastructure.Import.Giacom;

/// <summary>
/// Dependency-injection registration for Giacom supplier import pipelines.
/// </summary>
public static class GiacomImportServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="GiacomBillingPdfIngester"/> as the singleton implementation of
    /// <see cref="IGiacomBillingPdfIngester"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddGiacomBillingPdfIngestion(this IServiceCollection services)
    {
        services.AddSingleton<IGiacomBillingPdfIngester, GiacomBillingPdfIngester>();
        services.AddSingleton<IGiacomBillingNormalizer, GiacomBillingNormalizer>();
        return services;
    }

    /// <summary>
    /// Registers Subscription Management CSV ingestion and normalization services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddGiacomSubscriptionManagementCsvIngestion(this IServiceCollection services)
    {
        services.AddSingleton<ISubscriptionManagementNormalizer, SubscriptionManagementNormalizer>();
        services.AddSingleton<ISubscriptionManagementCsvIngester, SubscriptionManagement.SubscriptionManagementCsvIngester>();
        return services;
    }

    /// <summary>
    /// Registers reseller price list CSV ingestion, normalization, and pricing strategy resolution.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddGiacomRetailPricingCsvIngestion(this IServiceCollection services)
    {
        services.AddSingleton<IPriceListNormalizer, PriceListNormalizer>();
        services.AddSingleton<IIntendedPriceResolver, IntendedPriceResolver>();
        services.AddSingleton<IResellerPricingCsvIngester, ResellerPricingCsvIngester>();
        return services;
    }
}
