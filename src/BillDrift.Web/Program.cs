using BillDrift.Web.Components;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddFluentUIComponents();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddHttpClient<BillDrift.Web.Services.IApprovalApiClient, BillDrift.Web.Services.ApprovalApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
});

builder.Services.AddHttpClient<BillDrift.Web.Services.IRunHistoryApiClient, BillDrift.Web.Services.RunHistoryApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
});

builder.Services.AddHttpClient<BillDrift.Web.Services.IIngestionApiClient, BillDrift.Web.Services.IngestionApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
});

builder.Services.AddHttpClient<BillDrift.Web.Services.IReconciliationApiClient, BillDrift.Web.Services.ReconciliationApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
});

builder.Services.AddHttpClient<BillDrift.Web.Services.IClassificationApiClient, BillDrift.Web.Services.ClassificationApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
});

builder.Services.AddHttpClient<BillDrift.Web.Services.ICatalogueReconciliationApiClient, BillDrift.Web.Services.CatalogueReconciliationApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseOutputCache();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
