using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Data.Tables;
using BillDrift.Application.Approval;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Approval;

/// <summary>Azure Table entity mappings for approval persistence.</summary>
internal static class ApprovalTableEntities
{
    public const string ProposalPartition = "proposal";
    public const string DecisionPartition = "decision";
    public const string AuditPartition = "audit";
    public const string ExportPartition = "export";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string EncodeRowKey(RunId runId, IdempotencyKey idempotencyKey) =>
        $"{runId.Value:N}:{Uri.EscapeDataString(idempotencyKey.Value)}";

    public static TableEntity ToProposalEntity(ApprovalProposal proposal)
    {
        var entity = new TableEntity(ProposalPartition, EncodeRowKey(proposal.RunId, proposal.IdempotencyKey))
        {
            ["ProposalId"] = proposal.Id.Value.ToString(),
            ["RunId"] = proposal.RunId.Value.ToString(),
            ["IdempotencyKey"] = proposal.IdempotencyKey.Value,
            ["Category"] = proposal.Category.ToString(),
            ["State"] = proposal.State.ToString(),
            ["Eligibility"] = proposal.Eligibility.ToString(),
            ["CustomerMexId"] = proposal.CustomerMexId.Value,
            ["ProductLabel"] = proposal.ProductLabel,
            ["PriorValuesJson"] = JsonSerializer.Serialize(proposal.PriorValues, JsonOptions),
            ["ProposedValuesJson"] = JsonSerializer.Serialize(proposal.ProposedValues, JsonOptions),
            ["ExecutionOrder"] = proposal.ExecutionOrder,
            ["IngestedAt"] = proposal.IngestedAt,
            ["ApprovedWhileEligible"] = proposal.ApprovedWhileEligible,
            ["LastUpdatedAt"] = proposal.LastUpdatedAt
        };

        if (proposal.ProposedChangeId is not null)
        {
            entity["ProposedChangeId"] = proposal.ProposedChangeId.Value.Value.ToString();
        }

        if (proposal.MismatchId is not null)
        {
            entity["MismatchId"] = proposal.MismatchId.Value.Value.ToString();
        }

        if (proposal.ActionType is not null)
        {
            entity["ActionType"] = proposal.ActionType.Value.ToString();
        }

        if (proposal.EligibilityReason is not null)
        {
            entity["EligibilityReason"] = proposal.EligibilityReason;
        }

        if (proposal.CommercialKeyRoot is not null)
        {
            entity["CommercialKeyRootJson"] = JsonSerializer.Serialize(
                new CommercialKeyRootDto(
                    proposal.CommercialKeyRoot.Value.OfferId.Value,
                    proposal.CommercialKeyRoot.Value.SkuId.Value),
                JsonOptions);
        }

        if (proposal.RiskIndicator is not null)
        {
            entity["RiskIndicator"] = proposal.RiskIndicator.Value.ToString();
        }

        if (proposal.SupersededByRunId is not null)
        {
            entity["SupersededByRunId"] = proposal.SupersededByRunId.Value.Value.ToString();
        }

        if (proposal.LastOperatorId is not null)
        {
            entity["LastOperatorId"] = proposal.LastOperatorId;
        }

        return entity;
    }

    public static ApprovalProposal ToProposal(TableEntity entity)
    {
        var priorJson = (string)entity["PriorValuesJson"];
        var proposedJson = (string)entity["ProposedValuesJson"];

        CommercialKeyRoot? commercialRoot = null;
        if (entity.TryGetValue("CommercialKeyRootJson", out var rootJson) && rootJson is string rootText)
        {
            var parsed = JsonSerializer.Deserialize<CommercialKeyRootDto>(rootText, JsonOptions);
            if (parsed is not null)
            {
                commercialRoot = CommercialKeyRoot.Create(
                    OfferId.Create(parsed.OfferId),
                    SkuId.Create(parsed.SkuId));
            }
        }

        return new ApprovalProposal(
            ApprovalProposalId.FromGuid(Guid.Parse((string)entity["ProposalId"])),
            RunId.FromGuid(Guid.Parse((string)entity["RunId"])),
            entity.TryGetValue("ProposedChangeId", out var pc) && pc is string pcText
                ? ProposedChangeId.FromGuid(Guid.Parse(pcText))
                : null,
            new IdempotencyKey((string)entity["IdempotencyKey"]),
            entity.TryGetValue("MismatchId", out var mid) && mid is string midText
                ? MismatchId.FromGuid(Guid.Parse(midText))
                : null,
            Enum.Parse<ApprovalProposalCategory>((string)entity["Category"]),
            entity.TryGetValue("ActionType", out var at) && at is string atText
                ? Enum.Parse<ProposedActionType>(atText)
                : null,
            Enum.Parse<ApprovalDecisionState>((string)entity["State"]),
            Enum.Parse<ApprovalEligibility>((string)entity["Eligibility"]),
            entity.TryGetValue("EligibilityReason", out var reason) ? reason as string : null,
            MexId.Create((string)entity["CustomerMexId"]),
            (string)entity["ProductLabel"],
            commercialRoot,
            JsonSerializer.Deserialize<Dictionary<string, string>>(priorJson, JsonOptions) ?? [],
            JsonSerializer.Deserialize<Dictionary<string, string>>(proposedJson, JsonOptions) ?? [],
            (int)entity["ExecutionOrder"],
            [],
            entity.TryGetValue("RiskIndicator", out var risk) && risk is string riskText
                ? Enum.Parse<ApprovalRiskIndicator>(riskText)
                : null,
            (DateTimeOffset)entity["IngestedAt"],
            entity.TryGetValue("SupersededByRunId", out var superseded) && superseded is string supersededText
                ? RunId.FromGuid(Guid.Parse(supersededText))
                : null,
            entity.TryGetValue("ApprovedWhileEligible", out var eligible) && eligible is bool eligibleFlag && eligibleFlag,
            entity.TryGetValue("LastOperatorId", out var operatorId) ? operatorId as string : null,
            (DateTimeOffset)entity["LastUpdatedAt"]);
    }

    public static TableEntity ToDecisionEntity(ApprovalDecision decision)
    {
        var rowKey = $"{decision.RunId.Value:N}:{decision.ProposalId.Value:N}:{decision.DecidedAt.UtcTicks:D19}";
        return new TableEntity(DecisionPartition, rowKey)
        {
            ["ProposalId"] = decision.ProposalId.Value.ToString(),
            ["RunId"] = decision.RunId.Value.ToString(),
            ["PriorState"] = decision.PriorState.ToString(),
            ["NewState"] = decision.NewState.ToString(),
            ["OperatorId"] = decision.OperatorId,
            ["DecidedAt"] = decision.DecidedAt,
            ["AcknowledgedStale"] = decision.AcknowledgedStale,
            ["RejectionReason"] = decision.RejectionReason
        };
    }

    public static TableEntity ToAuditEntity(ApprovalAuditEvent auditEvent)
    {
        var rowKey = $"{auditEvent.RunId.Value:N}:{auditEvent.Timestamp.UtcTicks:D19}:{auditEvent.EventId:N}";
        return new TableEntity(AuditPartition, rowKey)
        {
            ["EventType"] = auditEvent.EventType.ToString(),
            ["RunId"] = auditEvent.RunId.Value.ToString(),
            ["Timestamp"] = auditEvent.Timestamp,
            ["Summary"] = auditEvent.Summary,
            ["ProposalId"] = auditEvent.ProposalId?.Value.ToString(),
            ["OperatorId"] = auditEvent.OperatorId,
            ["PayloadJson"] = auditEvent.PayloadJson
        };
    }

    public static ApprovalAuditEvent ToAuditEvent(TableEntity entity) =>
        new(
            Guid.Parse(entity.RowKey.Split(':')[^1]),
            Enum.Parse<ApprovalAuditEventType>((string)entity["EventType"]),
            RunId.FromGuid(Guid.Parse((string)entity["RunId"])),
            entity.TryGetValue("ProposalId", out var proposalId) && proposalId is string proposalText
                ? ApprovalProposalId.FromGuid(Guid.Parse(proposalText))
                : null,
            entity.TryGetValue("OperatorId", out var operatorId) ? operatorId as string : null,
            (DateTimeOffset)entity["Timestamp"],
            (string)entity["Summary"],
            entity.TryGetValue("PayloadJson", out var payload) ? payload as string : null);

    private sealed record CommercialKeyRootDto(string OfferId, string SkuId);
}

/// <summary>Azure Table Storage implementation of <see cref="IApprovalStore"/>.</summary>
public sealed class AzureTableApprovalStore : IApprovalStore
{
    private readonly TableClient _tableClient;
    private bool _tableEnsured;

    /// <summary>Creates a store using an Aspire-injected table service client.</summary>
    public AzureTableApprovalStore(TableServiceClient tableServiceClient, IOptions<ApprovalStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(tableServiceClient);
        _tableClient = tableServiceClient.GetTableClient(options.Value.TableName);
    }

    /// <inheritdoc />
    public async Task UpsertProposalAsync(ApprovalProposal proposal, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.UpsertEntityAsync(
            ApprovalTableEntities.ToProposalEntity(proposal),
            TableUpdateMode.Replace,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApprovalProposal?> GetProposalAsync(
        RunId runId,
        ApprovalProposalId proposalId,
        CancellationToken cancellationToken = default)
    {
        var proposals = await ListProposalsByRunAsync(runId, cancellationToken);
        return proposals.FirstOrDefault(p => p.Id == proposalId);
    }

    /// <inheritdoc />
    public async Task<ApprovalProposal?> GetProposalByIdempotencyKeyAsync(
        RunId runId,
        IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                ApprovalTableEntities.ProposalPartition,
                ApprovalTableEntities.EncodeRowKey(runId, idempotencyKey),
                cancellationToken: cancellationToken);

            return ApprovalTableEntities.ToProposal(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalProposal>> ListProposalsByRunAsync(
        RunId runId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter = $"PartitionKey eq '{ApprovalTableEntities.ProposalPartition}' and RunId eq '{runId.Value}'";
        var results = new List<ApprovalProposal>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            results.Add(ApprovalTableEntities.ToProposal(entity));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalProposal>> ListProposalsByCustomerAsync(
        RunId runId,
        MexId customerMexId,
        CancellationToken cancellationToken = default)
    {
        var proposals = await ListProposalsByRunAsync(runId, cancellationToken);
        return proposals.Where(p => p.CustomerMexId.Value == customerMexId.Value).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalProposal>> FindPriorProposalsAsync(
        MismatchId mismatchId,
        ProposedActionType? actionType,
        RunId currentRunId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter =
            $"PartitionKey eq '{ApprovalTableEntities.ProposalPartition}' and MismatchId eq '{mismatchId.Value}'";
        var results = new List<ApprovalProposal>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            var proposal = ApprovalTableEntities.ToProposal(entity);
            if (proposal.RunId == currentRunId)
            {
                continue;
            }

            if (actionType is not null && proposal.ActionType != actionType)
            {
                continue;
            }

            results.Add(proposal);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task AppendDecisionAsync(ApprovalDecision decision, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.AddEntityAsync(ApprovalTableEntities.ToDecisionEntity(decision), cancellationToken);
    }

    /// <inheritdoc />
    public async Task AppendAuditEventAsync(ApprovalAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _tableClient.AddEntityAsync(ApprovalTableEntities.ToAuditEntity(auditEvent), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalAuditEvent>> ListAuditEventsAsync(
        RunId runId,
        ApprovalProposalId? proposalId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var filter = $"PartitionKey eq '{ApprovalTableEntities.AuditPartition}' and RunId eq '{runId.Value}'";
        var results = new List<ApprovalAuditEvent>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            var audit = ApprovalTableEntities.ToAuditEvent(entity);
            if (proposalId is not null && audit.ProposalId != proposalId)
            {
                continue;
            }

            results.Add(audit);
        }

        return results.OrderByDescending(e => e.Timestamp).ToList();
    }

    /// <inheritdoc />
    public async Task SaveExportMetadataAsync(
        Guid exportId,
        RunId runId,
        string exportedBy,
        string blobPath,
        int entryCount,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var entity = new TableEntity(ApprovalTableEntities.ExportPartition, $"{runId.Value:N}:{exportId:N}")
        {
            ["RunId"] = runId.Value.ToString(),
            ["ExportId"] = exportId.ToString(),
            ["ExportedAt"] = DateTimeOffset.UtcNow,
            ["ExportedBy"] = exportedBy,
            ["BlobPath"] = blobPath,
            ["EntryCount"] = entryCount,
            ["ContentHash"] = contentHash
        };

        await _tableClient.AddEntityAsync(entity, cancellationToken);
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (_tableEnsured)
        {
            return;
        }

        await _tableClient.CreateIfNotExistsAsync(cancellationToken);
        _tableEnsured = true;
    }
}
