namespace BillDrift.Domain.Approval;

/// <summary>Surrogate identifier for an <see cref="ApprovalProposal"/>.</summary>
public readonly record struct ApprovalProposalId(Guid Value)
{
    /// <summary>Generates a new unique proposal ID.</summary>
    public static ApprovalProposalId New() => new(Guid.NewGuid());

    /// <summary>Reconstructs an ID from an existing GUID.</summary>
    public static ApprovalProposalId FromGuid(Guid value) => new(value);
}
