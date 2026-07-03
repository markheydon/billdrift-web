using System.Net;
using BillDrift.Api.Tests.Infrastructure;
using FluentAssertions;

namespace BillDrift.Api.Tests.Imports;

public sealed class StripeCsvImportEndpointsTests(BillDriftApiWebApplicationFactory factory) : IClassFixture<BillDriftApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_without_files_returns_bad_request()
    {
        var response = await _client.PostAsync("/api/imports/stripe-csv", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_list_returns_ok_with_empty_array()
    {
        var response = await _client.GetAsync("/api/imports/stripe-csv", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Be("[]");
    }
}
