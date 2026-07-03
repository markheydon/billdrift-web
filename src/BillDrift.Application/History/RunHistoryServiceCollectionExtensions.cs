using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Application.History;

/// <summary>Registers run history application services.</summary>
public static class RunHistoryServiceCollectionExtensions
{
    /// <summary>Adds run history orchestration and analysis services.</summary>
    public static IServiceCollection AddRunHistory(this IServiceCollection services)
    {
        services.Configure<RunHistoryOptions>(_ => { });
        services.AddScoped<RunArchiveService>();
        services.AddScoped<RunHistoryService>();
        services.AddScoped<RunComparisonService>();
        services.AddScoped<DriftTrendAnalyzer>();
        services.AddScoped<PricingDriftAnalyzer>();
        services.AddSingleton<StableMismatchKeyFactory>();
        return services;
    }
}
