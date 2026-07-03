using System.Net;
using BillDrift.Api.Tests.Infrastructure;
using BillDrift.Application.Import;
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
    public async Task Post_with_oversized_optional_products_file_returns_payload_too_large()
    {
        var maxFileSizeBytes = new StripeCsvIngestionOptions().MaxFileSizeBytes;

        using var content = new MultipartFormDataContent();
        content.Add(
            new ByteArrayContent("Customer ID\n"u8.ToArray()),
            "subscriptions",
            "subscriptions.csv");

        var oversizedProducts = new byte[maxFileSizeBytes + 1];
        content.Add(new ByteArrayContent(oversizedProducts), "products", "products.csv");

        var response = await _client.PostAsync("/api/imports/stripe-csv", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
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
