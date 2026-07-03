using BillDrift.Api.Approval;
using BillDrift.Api.CatalogueReconciliation;
using BillDrift.Api.Classification;
using BillDrift.Api.History;
using BillDrift.Api.Imports;
using BillDrift.Api.Reconciliation;
using BillDrift.Application.Approval;
using BillDrift.Application.Classification;
using BillDrift.Application.History;
using BillDrift.Application.Reconciliation;
using BillDrift.Infrastructure.Approval;
using BillDrift.Infrastructure.CatalogueReconciliation;
using BillDrift.Infrastructure.Classification;
using BillDrift.Infrastructure.History;
using BillDrift.Infrastructure.Import.Giacom;
using BillDrift.Infrastructure.Import.Stripe;
using BillDrift.Infrastructure.Ingestion;

var builder = WebApplication.CreateBuilder(args);

var useInMemoryStorage = builder.Environment.IsEnvironment("Testing");

builder.AddServiceDefaults();
if (!useInMemoryStorage)
{
    builder.AddAzureTableServiceClient("tables");
    builder.AddAzureBlobServiceClient("blobs");
}

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IOperatorContext>(sp => OperatorContextResolver.Resolve(
    sp.GetRequiredService<IHttpContextAccessor>().HttpContext,
    sp.GetRequiredService<IHostEnvironment>()));
builder.Services.AddGiacomBillingPdfIngestion();
builder.Services.AddGiacomSubscriptionManagementCsvIngestion();
builder.Services.AddGiacomRetailPricingCsvIngestion();
builder.Services.AddStripeBillingCsvIngestion();
builder.Services.AddIngestionStorage(useInMemoryStorage);
builder.Services.AddReconciliationEngine();
builder.Services.AddClassification();
builder.Services.AddClassificationStorage(useInMemoryStorage);
builder.Services.AddApproval();
builder.Services.AddApprovalStorage(useInMemoryStorage);
builder.Services.AddRunHistory();
builder.Services.AddRunHistoryStorage(useInMemoryStorage);
builder.Services.AddCatalogueReconciliation();
builder.Services.AddCatalogueReconciliationStorage(useInMemoryStorage);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "BillDrift API is running.");

app.MapDefaultEndpoints();
app.MapClassificationEndpoints();
app.MapApprovalEndpoints();
app.MapRunHistoryEndpoints();
app.MapSubscriptionManagementImportEndpoints();
app.MapRetailPricingImportEndpoints();
app.MapGiacomPdfImportEndpoints();
app.MapStripeCsvImportEndpoints();
app.MapReconciliationEndpoints();
app.MapCatalogueReconciliationEndpoints();

app.Run();

/// <summary>Marker type for WebApplicationFactory integration tests.</summary>
public partial class Program;
