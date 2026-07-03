namespace BillDrift.Domain.History;

/// <summary>Deterministic cross-run identity for a reconciliation mismatch.</summary>
/// <param name="Value">Pipe-delimited stable key string (max 512 chars).</param>
public readonly record struct StableMismatchKey(string Value)
{
    /// <summary>Creates a stable mismatch key from a validated value.</summary>
    public static StableMismatchKey Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 512)
        {
            value = value[..512];
        }

        return new StableMismatchKey(value);
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
