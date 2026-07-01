using BillDrift.Application.Import;
using Microsoft.Extensions.DependencyInjection;

namespace BillDrift.Infrastructure.Import.Giacom;

public static class GiacomImportServiceCollectionExtensions
{
    public static IServiceCollection AddGiacomBillingPdfIngestion(this IServiceCollection services)
    {
        services.AddSingleton<IGiacomBillingPdfIngester, GiacomBillingPdfIngester>();
        return services;
    }
}
