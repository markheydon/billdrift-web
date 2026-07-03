using BillDrift.Application.CatalogueReconciliation;
using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Infrastructure.CatalogueReconciliation;

/// <summary>Registers catalogue reconciliation services.</summary>
public static class CatalogueReconciliationServiceCollectionExtensions
{
    /// <summary>Registers catalogue reconciliation engine and application services.</summary>
    public static IServiceCollection AddCatalogueReconciliation(this IServiceCollection services)
    {
        services.AddSingleton<ICatalogueReconciliationEngine, CatalogueReconciliationEngine>();
        services.AddSingleton<IStripeCatalogueNormalizer, StripeCatalogueNormalizer>();
        services.AddSingleton<CatalogueApprovalAdapter>();
        services.AddScoped<ICatalogueReconciliationService, CatalogueReconciliationService>();
        return services;
    }

    /// <summary>Registers Azure or in-memory catalogue reconciliation storage.</summary>
    public static IServiceCollection AddCatalogueReconciliationStorage(
        this IServiceCollection services,
        bool useInMemory = false)
    {
        services.Configure<CatalogueReconciliationStorageOptions>(_ => { });

        if (useInMemory)
        {
            services.AddSingleton<ICatalogueReconciliationStore, InMemoryCatalogueReconciliationStore>();
        }
        else
        {
            services.AddSingleton<ICatalogueReconciliationStore, AzureCatalogueReconciliationStore>();
        }

        return services;
    }
}
