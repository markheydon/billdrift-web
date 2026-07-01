using BillDrift.Domain.Common;

namespace BillDrift.Domain.Reconciliation;

public readonly record struct RunId(Guid Value)
{
    public static RunId New() => new(Guid.NewGuid());
    public static RunId FromGuid(Guid value) => new(value);
}

public readonly record struct MatchGroupId(Guid Value)
{
    public static MatchGroupId New() => new(Guid.NewGuid());
    public static MatchGroupId FromGuid(Guid value) => new(value);
}

public readonly record struct MismatchId(Guid Value)
{
    public static MismatchId New() => new(Guid.NewGuid());
    public static MismatchId FromGuid(Guid value) => new(value);
}

public readonly record struct ProposedChangeId(Guid Value)
{
    public static ProposedChangeId New() => new(Guid.NewGuid());
    public static ProposedChangeId FromGuid(Guid value) => new(value);
}

public readonly record struct IdempotencyKey(string Value)
{
    public static IdempotencyKey Create(RunId runId, MismatchId mismatchId, ProposedActionType actionType) =>
        new($"{runId.Value}:{mismatchId.Value}:{actionType}");
}
