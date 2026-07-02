using BillDrift.Api.Approval;
using BillDrift.Api.Classification;
using BillDrift.Application.Approval;
using BillDrift.Application.Classification;
using BillDrift.Application.Reconciliation;
using BillDrift.Infrastructure.Approval;
using BillDrift.Infrastructure.Classification;
using BillDrift.Infrastructure.Import.Giacom;
using BillDrift.Infrastructure.Import.Stripe;

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
builder.Services.AddStripeBillingCsvIngestion();
builder.Services.AddReconciliationEngine();
builder.Services.AddClassification();
builder.Services.AddClassificationStorage();
builder.Services.AddApproval();
builder.Services.AddApprovalStorage();

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

app.Run();
