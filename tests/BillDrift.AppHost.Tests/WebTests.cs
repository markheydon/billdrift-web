using Microsoft.Extensions.Logging;

namespace BillDrift.AppHost.Tests;

public class WebTests
{
    // The AppHost graph now includes an Azurite storage emulator container, which must be
    // pulled/started by the container runtime before the app is considered started. Allow
    // enough time for a first-run image pull.
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // The testing builder does not apply launch profiles, so without this the AppHost and its
        // child projects run as Production. ServiceDefaults only maps /health and /alive in
        // Development, so the AppHost's WithHttpHealthCheck("/health") probes would 404 forever and
        // WaitFor(api) would never complete. Running as Development mirrors local `aspire run`.
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BillDrift_AppHost>(
            [],
            (_, settings) => settings.EnvironmentName = "Development",
            cancellationToken);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        var httpClient = app.CreateHttpClient("webfrontend");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        var response = await httpClient.GetAsync("/", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
