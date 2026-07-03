using BillDrift.Application.Import.Giacom;
using BillDrift.Application.Import.RetailPricing;
using BillDrift.Application.Import.Stripe;
using BillDrift.Application.Import.SubscriptionManagement;
using BillDrift.Application.Ingestion;
using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Infrastructure.Ingestion;

/// <summary>Registers Azure and in-memory ingestion persistence stores.</summary>
public static class IngestionServiceCollectionExtensions
{
    /// <summary>Adds ingestion blob and table index stores.</summary>
    public static IServiceCollection AddIngestionStorage(this IServiceCollection services, bool useInMemory = false)
    {
        services.Configure<IngestionStorageOptions>(_ => { });

        if (useInMemory)
        {
            services.AddSingleton<IIngestionBlobStore, InMemoryIngestionBlobStore>();
            services.AddSingleton<IIngestionRunIndexStore, InMemoryIngestionRunIndexStore>();
        }
        else
        {
            services.AddSingleton<IIngestionBlobStore, AzureBlobIngestionArchiveStore>();
            services.AddSingleton<IIngestionRunIndexStore, AzureTableIngestionRunIndexStore>();
        }

        services.AddSingleton<ISubscriptionManagementIngestionService, SubscriptionManagementIngestionService>();
        services.AddSingleton<IRetailPricingIngestionService, RetailPricingIngestionService>();
        services.AddSingleton<IGiacomPdfIngestionService, GiacomPdfIngestionService>();
        services.AddSingleton<IStripeCsvIngestionService, StripeCsvIngestionService>();
        return services;
    }
}
