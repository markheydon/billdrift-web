using BillDrift.Api.Approval;
using BillDrift.Api.Classification;
using BillDrift.Api.History;
using BillDrift.Api.Imports;
using BillDrift.Application.Approval;
using BillDrift.Application.Classification;
using BillDrift.Application.History;
using BillDrift.Application.Reconciliation;
using BillDrift.Infrastructure.Approval;
using BillDrift.Infrastructure.Classification;
using BillDrift.Infrastructure.History;
using BillDrift.Infrastructure.Import.Giacom;
using BillDrift.Infrastructure.Import.Stripe;
using BillDrift.Infrastructure.Ingestion;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureTableServiceClient("tables");
builder.AddAzureBlobServiceClient("blobs");
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IOperatorContext>(sp => OperatorContextResolver.Resolve(
    sp.GetRequiredService<IHttpContextAccessor>().HttpContext,
    sp.GetRequiredService<IHostEnvironment>()));
builder.Services.AddGiacomBillingPdfIngestion();
builder.Services.AddGiacomSubscriptionManagementCsvIngestion();
builder.Services.AddStripeBillingCsvIngestion();
builder.Services.AddIngestionStorage();
builder.Services.AddReconciliationEngine();
builder.Services.AddClassification();
builder.Services.AddClassificationStorage();
builder.Services.AddApproval();
builder.Services.AddApprovalStorage();
builder.Services.AddRunHistory();
builder.Services.AddRunHistoryStorage();

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

app.Run();
