using BillDrift.Domain.Common;

namespace BillDrift.Domain.Reconciliation;

/// <summary>
/// Domain-generated identifier for a <see cref="ReconciliationRun"/>.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct RunId(Guid Value)
{
    /// <summary>Generates a new unique reconciliation run ID.</summary>
    /// <returns>A new ID with a random GUID.</returns>
    public static RunId New() => new(Guid.NewGuid());

    /// <summary>Reconstructs an ID from an existing GUID (e.g. when loading from persistence).</summary>
    /// <param name="value">The GUID to wrap.</param>
    /// <returns>The corresponding run ID.</returns>
    public static RunId FromGuid(Guid value) => new(value);
}

/// <summary>
/// Domain-generated identifier for an <see cref="EntityMatchGroup"/> within a reconciliation run.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct MatchGroupId(Guid Value)
{
    /// <summary>Generates a new unique match group ID.</summary>
    /// <returns>A new ID with a random GUID.</returns>
    public static MatchGroupId New() => new(Guid.NewGuid());

    /// <summary>Reconstructs an ID from an existing GUID (e.g. when loading from persistence).</summary>
    /// <param name="value">The GUID to wrap.</param>
    /// <returns>The corresponding match group ID.</returns>
    public static MatchGroupId FromGuid(Guid value) => new(value);
}

/// <summary>
/// Domain-generated identifier for a <see cref="Mismatch"/> detected during reconciliation.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct MismatchId(Guid Value)
{
    /// <summary>Generates a new unique mismatch ID.</summary>
    /// <returns>A new ID with a random GUID.</returns>
    public static MismatchId New() => new(Guid.NewGuid());

    /// <summary>Reconstructs an ID from an existing GUID (e.g. when loading from persistence).</summary>
    /// <param name="value">The GUID to wrap.</param>
    /// <returns>The corresponding mismatch ID.</returns>
    public static MismatchId FromGuid(Guid value) => new(value);
}

/// <summary>
/// Domain-generated identifier for a <see cref="ProposedChange"/> targeting Stripe.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct ProposedChangeId(Guid Value)
{
    /// <summary>Generates a new unique proposed change ID.</summary>
    /// <returns>A new ID with a random GUID.</returns>
    public static ProposedChangeId New() => new(Guid.NewGuid());

    /// <summary>Reconstructs an ID from an existing GUID (e.g. when loading from persistence).</summary>
    /// <param name="value">The GUID to wrap.</param>
    /// <returns>The corresponding proposed change ID.</returns>
    public static ProposedChangeId FromGuid(Guid value) => new(value);
}

/// <summary>
/// Idempotency key for proposed Stripe changes, preventing duplicate execution of the same corrective action.
/// Format: <c>{RunId}:{MismatchId}:{ActionType}</c>.
/// </summary>
/// <param name="Value">The composite idempotency key string.</param>
public readonly record struct IdempotencyKey(string Value)
{
    /// <summary>
    /// Creates a deterministic idempotency key from the run, mismatch, and action type.
    /// </summary>
    /// <param name="runId">The reconciliation run that produced the proposed change.</param>
    /// <param name="mismatchId">The mismatch being resolved.</param>
    /// <param name="actionType">The type of corrective action proposed.</param>
    /// <returns>An idempotency key unique to this run/mismatch/action combination.</returns>
    public static IdempotencyKey Create(RunId runId, MismatchId mismatchId, ProposedActionType actionType) =>
        new($"{runId.Value}:{mismatchId.Value}:{actionType}");
}
