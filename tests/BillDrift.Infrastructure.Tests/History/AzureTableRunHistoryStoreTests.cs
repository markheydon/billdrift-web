using Azure.Data.Tables;
using Azure.Storage.Blobs;
using BillDrift.Application.History;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;
using BillDrift.Infrastructure.History;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Tests.History;

public sealed class AzureTableRunHistoryStoreTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") ?? "UseDevelopmentStorage=true";

    [Fact]
    public async Task Run_index_round_trips_through_table()
    {
        try
        {
            var tableName = $"runhistorytest{Guid.NewGuid():N}";
            var store = CreateStore(tableName);
            var runId = RunId.New();
            var record = CreateRecord(runId);

            await store.UpsertRunAsync(record, TestContext.Current.CancellationToken);
            var loaded = await store.GetRunAsync(runId, TestContext.Current.CancellationToken);

            loaded.Should().NotBeNull();
            loaded!.RunId.Should().Be(runId);
            loaded.Status.Should().Be(RunArchiveStatus.Completed);
        }
        catch (Exception ex) when (IsStorageUnavailable(ex))
        {
            // Azurite not available
        }
    }

    [Fact]
    public async Task Audit_events_append_on_persist_operations()
    {
        try
        {
            var tableName = $"runhistoryaudit{Guid.NewGuid():N}";
            var store = CreateStore(tableName);
            var runId = RunId.New();

            await store.AppendAuditEventAsync(
                new RunHistoryAuditEvent(
                    Guid.NewGuid(),
                    RunHistoryAuditEventType.RunArchived,
                    runId,
                    DateTimeOffset.UtcNow,
                    "Test audit"),
                TestContext.Current.CancellationToken);

            var events = await store.ListAuditEventsAsync(runId, TestContext.Current.CancellationToken);
            events.Should().HaveCount(1);
        }
        catch (Exception ex) when (IsStorageUnavailable(ex))
        {
            // Azurite not available
        }
    }

    internal static bool IsStorageUnavailable(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static AzureTableRunHistoryStore CreateStore(string tableName)
    {
        var client = new TableServiceClient(ConnectionString);
        return new AzureTableRunHistoryStore(client, Options.Create(new RunHistoryStorageOptions { TableName = tableName }));
    }

    private static ReconciliationRunRecord CreateRecord(RunId runId) =>
        new(
            runId,
            RunArchiveStatus.Completed,
            BillingPeriod.Create(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            "test",
            new MappingVersionReference("v1", "hash", new DateOnly(2026, 6, 1)),
            [new InputSnapshotMetadata(InputDomainType.SupplierCost, true, RecordCount: 10)],
            new RunSummaryMetrics(5, 1, new Dictionary<string, int>(), 1, false),
            $"{runId.Value}/manifest.json");
}

public sealed class AzureBlobRunArchiveStoreTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") ?? "UseDevelopmentStorage=true";

    [Fact]
    public async Task Blob_round_trip_and_manifest_hash_validates()
    {
        try
        {
            var containerName = $"runhistoryblob{Guid.NewGuid():N}";
            var store = CreateStore(containerName);
            var run = new ReconciliationRun(
                RunId.New(),
                DateTimeOffset.UtcNow,
                BillingPeriod.Create(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
                new ReconciliationInputs([], [], [], [], []),
                [],
                [],
                []);

            var context = new RunArchiveContext(
                "test",
                Enum.GetValues<InputDomainType>().ToDictionary(d => d, d => new InputSnapshotMetadata(d, false)),
                new MappingVersionReference("v1", "hash", new DateOnly(2026, 6, 1)),
                DateTimeOffset.UtcNow);

            var result = await store.WriteRunArchiveAsync(run, context, TestContext.Current.CancellationToken);
            result.ManifestBlobPath.Should().Contain(run.Id.Value.ToString("D"));

            await store.VerifyManifestIntegrityAsync(run.Id, TestContext.Current.CancellationToken);
        }
        catch (Exception ex) when (AzureTableRunHistoryStoreTests.IsStorageUnavailable(ex))
        {
            // Azurite not available
        }
    }

    [Fact]
    public async Task Absent_input_domain_reads_as_empty_snapshot_not_error()
    {
        try
        {
            var containerName = $"runhistoryblob{Guid.NewGuid():N}";
            var store = CreateStore(containerName);
            var run = new ReconciliationRun(
                RunId.New(),
                DateTimeOffset.UtcNow,
                BillingPeriod.Create(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
                new ReconciliationInputs([], [], [], [], []),
                [],
                [],
                []);

            var context = new RunArchiveContext(
                "test",
                Enum.GetValues<InputDomainType>().ToDictionary(d => d, d => new InputSnapshotMetadata(d, false)),
                new MappingVersionReference("v1", "hash", new DateOnly(2026, 6, 1)),
                DateTimeOffset.UtcNow);

            await store.WriteRunArchiveAsync(run, context, TestContext.Current.CancellationToken);

            var content = await store.LoadInputBlobAsync(run.Id, InputDomainType.StripeBilling, TestContext.Current.CancellationToken);
            content.Should().Contain("\"records\":[]");
        }
        catch (Exception ex) when (AzureTableRunHistoryStoreTests.IsStorageUnavailable(ex))
        {
            // Azurite not available
        }
    }

    [Fact]
    public async Task Missing_run_still_throws_not_found_on_input_read()
    {
        try
        {
            var containerName = $"runhistoryblob{Guid.NewGuid():N}";
            var store = CreateStore(containerName);
            var act = async () => await store.LoadInputBlobAsync(RunId.New(), InputDomainType.StripeBilling, TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<RunNotFoundException>();
        }
        catch (Exception ex) when (AzureTableRunHistoryStoreTests.IsStorageUnavailable(ex))
        {
            // Azurite not available
        }
    }

    private static AzureBlobRunArchiveStore CreateStore(string containerName)
    {
        var client = new BlobServiceClient(ConnectionString);
        return new AzureBlobRunArchiveStore(client, Options.Create(new RunHistoryStorageOptions { BlobContainerName = containerName }));
    }
}
