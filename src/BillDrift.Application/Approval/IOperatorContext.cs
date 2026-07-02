namespace BillDrift.Application.Approval;

/// <summary>Provides the current operator identity and permissions for approval actions.</summary>
public interface IOperatorContext
{
    /// <summary>Unique operator identifier from authenticated claims (or dev fallback).</summary>
    string OperatorId { get; }

    /// <summary>Whether the operator may approve, reject, and export.</summary>
    bool CanApprove { get; }
}

/// <summary>
/// Immutable operator context value. Identity and permissions are decided by the caller
/// (server-side); this type only carries the resolved values into the application layer.
/// </summary>
public sealed class OperatorContext : IOperatorContext
{
    private const string DefaultOperatorId = "system";

    /// <summary>Creates context from a resolved operator identity and permission.</summary>
    public OperatorContext(string? operatorId, bool canApprove = true)
    {
        OperatorId = string.IsNullOrWhiteSpace(operatorId) ? DefaultOperatorId : operatorId;
        CanApprove = canApprove;
    }

    /// <inheritdoc />
    public string OperatorId { get; }

    /// <inheritdoc />
    public bool CanApprove { get; }
}
