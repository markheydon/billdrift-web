using System.Net.Sockets;
using Azure.Data.Tables;
using Azure.Storage.Blobs;

namespace BillDrift.Infrastructure.Tests.Storage;

/// <summary>
/// Shared helpers for Azure Blob/Table integration tests against Azurite or a configured storage account.
/// Probes availability once per test process so unavailable-storage cases skip in milliseconds.
/// </summary>
internal static class AzureStorageTestSupport
{
    public const string IntegrationTrait = "Integration";

    private const string ConnectionStringEnvironmentVariable = "AZURE_STORAGE_CONNECTION_STRING";
    private const string DevelopmentStorageConnectionString = "UseDevelopmentStorage=true";

    private static readonly Lazy<bool> IsAvailable = new(ProbeAvailability);

    public static void EnsureAvailableOrSkip()
    {
        if (!IsAvailable.Value)
        {
            Assert.Skip(
                "Azure Storage emulator not available. Start Azurite or set AZURE_STORAGE_CONNECTION_STRING.");
        }
    }

    public static string GetConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
        ?? DevelopmentStorageConnectionString;

    public static BlobServiceClient CreateBlobServiceClient(string connectionString)
    {
        var options = new BlobClientOptions();
        options.Retry.MaxRetries = 0;
        options.Retry.Delay = TimeSpan.Zero;
        options.Retry.MaxDelay = TimeSpan.Zero;
        options.Retry.NetworkTimeout = TimeSpan.FromSeconds(5);

        return new BlobServiceClient(connectionString, options);
    }

    public static TableServiceClient CreateTableServiceClient(string connectionString)
    {
        var options = new TableClientOptions();
        options.Retry.MaxRetries = 0;
        options.Retry.Delay = TimeSpan.Zero;
        options.Retry.MaxDelay = TimeSpan.Zero;
        options.Retry.NetworkTimeout = TimeSpan.FromSeconds(5);

        return new TableServiceClient(connectionString, options);
    }

    private static bool ProbeAvailability()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(connectionString) &&
            !connectionString.Contains("UseDevelopmentStorage", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ProbeAzuriteEndpoints();
    }

    private static bool ProbeAzuriteEndpoints() =>
        ProbeTcpEndpoint("127.0.0.1", 10000) && ProbeTcpEndpoint("127.0.0.1", 10002);

    private static bool ProbeTcpEndpoint(string host, int port)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port, cancellationTokenSource.Token).AsTask();
            connectTask.Wait(cancellationTokenSource.Token);
            return client.Connected;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
