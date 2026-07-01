using BillDrift.Application.Import;
using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Infrastructure.Import.Giacom;

/// <summary>
/// Dependency-injection registration for the Giacom supplier billing PDF ingestion pipeline.
/// </summary>
public static class GiacomImportServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="GiacomBillingPdfIngester"/> as the singleton implementation of
    /// <see cref="IGiacomBillingPdfIngester"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    /// <remarks>
    /// The ingester extracts supplier cost lines from Giacom pre-billing and post-billing PDFs.
    /// Output is non-authoritative raw import data for downstream normalization; no Stripe writes occur.
    /// </remarks>
    public static IServiceCollection AddGiacomBillingPdfIngestion(this IServiceCollection services)
    {
        services.AddSingleton<IGiacomBillingPdfIngester, GiacomBillingPdfIngester>();
        return services;
    }
}
