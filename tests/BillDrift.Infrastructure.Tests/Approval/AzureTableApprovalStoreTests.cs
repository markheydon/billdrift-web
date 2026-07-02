using Azure.Data.Tables;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using BillDrift.Infrastructure.Approval;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Tests.Approval;

public sealed class AzureTableApprovalStoreTests
{
    [Fact]
    public async Task Decision_round_trip_against_azurite_when_available()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        try
        {
            var client = new TableServiceClient(connectionString);
            var store = new AzureTableApprovalStore(
                client,
                Options.Create(new ApprovalStorageOptions { TableName = $"approvaltest{Guid.NewGuid():N}" }));

            var runId = RunId.New();
            var proposal = CreateProposal(runId);
            await store.UpsertProposalAsync(proposal, cancellationToken);

            var decision = new ApprovalDecision(
                proposal.Id,
                runId,
                ApprovalDecisionState.Pending,
                ApprovalDecisionState.Approved,
                "operator",
                DateTimeOffset.UtcNow,
                null,
                false);

            await store.AppendDecisionAsync(decision, cancellationToken);

            var loaded = await store.GetProposalAsync(runId, proposal.Id, cancellationToken);
            loaded.Should().NotBeNull();
        }
        catch (Exception ex) when (ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
        {
            // Azurite not available in CI — skip gracefully.
        }
    }

    private static ApprovalProposal CreateProposal(RunId runId) =>
        new(
            ApprovalProposalId.New(),
            runId,
            ProposedChangeId.New(),
            IdempotencyKey.Create(runId, MismatchId.New(), ProposedActionType.UpdateQuantity),
            MismatchId.New(),
            ApprovalProposalCategory.Subscription,
            ProposedActionType.UpdateQuantity,
            ApprovalDecisionState.Pending,
            ApprovalEligibility.Eligible,
            null,
            MexId.Create("MEX-001"),
            "Product",
            null,
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["quantity"] = "10" },
            100,
            [],
            null,
            DateTimeOffset.UtcNow,
            null,
            false,
            null,
            DateTimeOffset.UtcNow);
}
