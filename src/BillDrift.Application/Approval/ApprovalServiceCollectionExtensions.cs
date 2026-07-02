using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Application.Approval;

/// <summary>Dependency injection extensions for approval workflow services.</summary>
public static class ApprovalServiceCollectionExtensions
{
    /// <summary>Registers approval orchestration and ingestion services.</summary>
    public static IServiceCollection AddApproval(this IServiceCollection services)
    {
        services.AddScoped<ApprovalEligibilityEvaluator>();
        services.AddScoped<ApprovalIngestionService>();
        services.AddScoped<ApprovedChangesetBuilder>();
        services.AddScoped<ApprovalService>();
        return services;
    }
}
