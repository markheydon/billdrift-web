using BillDrift.Application.Import;
using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Infrastructure.Import.Stripe;

/// <summary>
/// Dependency-injection registration for the Stripe billing CSV ingestion pipeline.
/// </summary>
public static class StripeImportServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="StripeBillingCsvIngester"/> as the singleton implementation of
    /// <see cref="IStripeBillingCsvIngester"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddStripeBillingCsvIngestion(this IServiceCollection services)
    {
        services.AddSingleton<IStripeBillingCsvIngester, StripeBillingCsvIngester>();
        return services;
    }
}
