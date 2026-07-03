using BillDrift.Application.Approval;
using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Infrastructure.Approval;

/// <summary>Dependency injection extensions for approval Azure storage.</summary>
public static class ApprovalStorageExtensions
{
    /// <summary>Registers Azure Table and Blob approval persistence.</summary>
    public static IServiceCollection AddApprovalStorage(this IServiceCollection services, bool useInMemory = false)
    {
        services.Configure<ApprovalStorageOptions>(_ => { });

        if (useInMemory)
        {
            services.AddScoped<IApprovalStore, InMemoryApprovalStore>();
            services.AddScoped<IApprovedChangesetExporter, PassThroughApprovedChangesetExporter>();
        }
        else
        {
            services.AddScoped<IApprovalStore, AzureTableApprovalStore>();
            services.AddScoped<IApprovedChangesetExporter, AzureBlobChangesetExporter>();
        }

        return services;
    }
}
