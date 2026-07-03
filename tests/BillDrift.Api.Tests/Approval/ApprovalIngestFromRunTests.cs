using System.Net;
using System.Net.Http.Json;
using BillDrift.Api.Tests.Infrastructure;
using FluentAssertions;

namespace BillDrift.Api.Tests.Approval;

public sealed class ApprovalIngestFromRunTests(BillDriftApiWebApplicationFactory factory) : IClassFixture<BillDriftApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Ingest_from_unknown_run_returns_not_found()
    {
        var runId = Guid.NewGuid();
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/reconciliation/{runId}/approvals/ingest-from-run")
        {
            Content = JsonContent.Create(new { includeInvestigationItems = true })
        };

        message.Headers.Add("X-Operator-Id", "test-operator");
        var response = await _client.SendAsync(message, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
