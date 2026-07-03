using System.Net;
using System.Net.Http.Json;
using BillDrift.Api.Tests.Infrastructure;
using FluentAssertions;

namespace BillDrift.Api.Tests.Reconciliation;

public sealed class ReconciliationEndpointsTests(BillDriftApiWebApplicationFactory factory) : IClassFixture<BillDriftApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_runs_with_empty_body_returns_bad_request()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/reconciliation/runs",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_unknown_run_returns_not_found()
    {
        var runId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/reconciliation/runs/{runId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
