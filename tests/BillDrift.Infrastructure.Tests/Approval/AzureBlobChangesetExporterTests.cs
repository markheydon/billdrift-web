using BillDrift.Application.Approval;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using BillDrift.Infrastructure.Approval;
using BillDrift.Infrastructure.Tests.Storage;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Tests.Approval;

public sealed class AzureBlobChangesetExporterTests
{
    [Fact]
    [Trait("Category", AzureStorageTestSupport.IntegrationTrait)]
    public async Task Blob_round_trip_against_azurite_when_available()
    {
        AzureStorageTestSupport.EnsureAvailableOrSkip();

        var cancellationToken = TestContext.Current.CancellationToken;
        var blobClient = AzureStorageTestSupport.CreateBlobServiceClient(AzureStorageTestSupport.GetConnectionString());
        var store = new InMemoryApprovalStoreForInfrastructureTests();
        var exporter = new AzureBlobChangesetExporter(
            blobClient,
            Options.Create(new ApprovalStorageOptions { ChangesetContainerName = $"changesets{Guid.NewGuid():N}" }),
            store);

        var runId = RunId.New();
        var changeset = new ApprovedChangeset(
            Guid.NewGuid(),
            runId,
            DateTimeOffset.UtcNow,
            "operator",
            [
                new ApprovedChangesetEntry(
                    ApprovalProposalId.New(),
                    IdempotencyKey.Create(runId, MismatchId.New(), ProposedActionType.UpdateQuantity),
                    ProposedActionType.UpdateQuantity,
                    MexId.Create("MEX-001"),
                    "Product",
                    new Dictionary<string, string> { ["quantity"] = "5" },
                    new Dictionary<string, string> { ["quantity"] = "10" },
                    DateTimeOffset.UtcNow,
                    "operator",
                    100)
            ],
            null);

        var exported = await exporter.ExportAsync(changeset, cancellationToken);
        exported.BlobUri.Should().NotBeNullOrEmpty();
    }

    private sealed class InMemoryApprovalStoreForInfrastructureTests : IApprovalStore
    {
        public Task UpsertProposalAsync(ApprovalProposal proposal, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ApprovalProposal?> GetProposalAsync(RunId runId, ApprovalProposalId proposalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovalProposal?>(null);

        public Task<ApprovalProposal?> GetProposalByIdempotencyKeyAsync(RunId runId, IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovalProposal?>(null);

        public Task<IReadOnlyList<ApprovalProposal>> ListProposalsByRunAsync(RunId runId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ApprovalProposal>>([]);

        public Task<IReadOnlyList<ApprovalProposal>> ListProposalsByCustomerAsync(RunId runId, MexId customerMexId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ApprovalProposal>>([]);

        public Task<IReadOnlyList<ApprovalProposal>> FindPriorProposalsAsync(MismatchId mismatchId, ProposedActionType? actionType, RunId currentRunId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ApprovalProposal>>([]);

        public Task AppendDecisionAsync(ApprovalDecision decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AppendAuditEventAsync(ApprovalAuditEvent auditEvent, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ApprovalAuditEvent>> ListAuditEventsAsync(RunId runId, ApprovalProposalId? proposalId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ApprovalAuditEvent>>([]);

        public Task SaveExportMetadataAsync(Guid exportId, RunId runId, string exportedBy, string blobPath, int entryCount, string contentHash, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
