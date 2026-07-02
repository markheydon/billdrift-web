using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Application.Classification;

/// <summary>
/// Dependency injection extensions for classification services.
/// </summary>
public static class ClassificationServiceCollectionExtensions
{
    /// <summary>
    /// Registers classification rule engine and orchestration service.
    /// </summary>
    public static IServiceCollection AddClassification(this IServiceCollection services)
    {
        services.AddSingleton<ClassificationRuleEngine>();
        services.AddScoped<ClassificationService>();
        return services;
    }
}
