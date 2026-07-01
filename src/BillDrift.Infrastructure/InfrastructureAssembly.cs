using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BillDrift.Infrastructure.Tests")]

namespace BillDrift.Infrastructure;

/// <summary>
/// Assembly marker for the BillDrift infrastructure layer, including the Giacom supplier
/// billing PDF ingestion pipeline and future persistence implementations.
/// </summary>
/// <remarks>
/// Public surface area is intentionally narrow: register ingestion via
/// <see cref="Import.Giacom.GiacomImportServiceCollectionExtensions.AddGiacomBillingPdfIngestion"/>
/// and consume <see cref="BillDrift.Application.Import.IGiacomBillingPdfIngester"/> from Application.
/// Parser stages and internal parse types are not exposed across the assembly boundary.
/// </remarks>
public static class InfrastructureAssembly;
