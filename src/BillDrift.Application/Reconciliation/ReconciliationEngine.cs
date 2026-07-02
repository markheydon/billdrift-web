using BillDrift.Application.Mapping;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Reconciliation;
using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Application.Reconciliation;

/// <summary>
/// Executes deterministic billing drift reconciliation over normalized input snapshots.
/// </summary>
public sealed class ReconciliationEngine : IReconciliationEngine
{
    private readonly ReconciliationPipeline _pipeline;

    /// <summary>
    /// Creates a reconciliation engine with the given mapping resolver.
    /// </summary>
    /// <param name="mappingResolver">Resolver for supplier product name to mapping lookup.</param>
    public ReconciliationEngine(IProductMappingResolver mappingResolver)
    {
        _pipeline = new ReconciliationPipeline(mappingResolver);
    }

    /// <inheritdoc />
    public ReconciliationRun Execute(ReconciliationRequest request) =>
        _pipeline.Execute(request);
}

/// <summary>
/// Dependency injection extensions for reconciliation services.
/// </summary>
public static class ReconciliationServiceCollectionExtensions
{
    /// <summary>
    /// Registers reconciliation engine and product mapping resolver services.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReconciliationEngine(this IServiceCollection services)
    {
        services.AddSingleton<IProductMappingResolver, ProductMappingResolver>();
        services.AddSingleton<IReconciliationEngine, ReconciliationEngine>();
        services.AddSingleton<ExceptionSurfacingService>();
        return services;
    }
}
