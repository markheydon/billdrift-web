using BillDrift.Infrastructure.Import.Giacom;
using BillDrift.Infrastructure.Import.Stripe;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddGiacomBillingPdfIngestion();
builder.Services.AddStripeBillingCsvIngestion();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "BillDrift API is running.");

app.MapDefaultEndpoints();

app.Run();
