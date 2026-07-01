using BillDrift.Infrastructure.Import.Giacom;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddGiacomBillingPdfIngestion();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "BillDrift API is running.");

app.MapDefaultEndpoints();

app.Run();
