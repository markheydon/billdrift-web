using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Infrastructure.History;

/// <summary>Registers Azure run history storage implementations.</summary>
public static class RunHistoryStorageExtensions
{
    /// <summary>Adds Azure table and blob stores for run history.</summary>
    public static IServiceCollection AddRunHistoryStorage(this IServiceCollection services)
    {
        services.Configure<RunHistoryStorageOptions>(_ => { });
        services.AddScoped<Application.History.IRunHistoryStore, AzureTableRunHistoryStore>();
        services.AddScoped<Application.History.IRunBlobArchiveStore, AzureBlobRunArchiveStore>();
        return services;
    }
}
