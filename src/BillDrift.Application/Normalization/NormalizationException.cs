using BillDrift.Domain.Common;

namespace BillDrift.Application.Normalization;

/// <summary>
/// Thrown when a normalizer cannot convert a raw import field into a valid domain value.
/// Carries the failing record identity and field context so operators can correct source data.
/// </summary>
public sealed class NormalizationException : Exception
{
    /// <summary>
    /// Creates an exception describing which raw record and field failed normalization.
    /// </summary>
    /// <param name="rawImportId">Stable identifier of the raw import record that failed.</param>
    /// <param name="fieldName">Name of the field that could not be normalized.</param>
    /// <param name="rawValue">Original raw string value that failed parsing or validation.</param>
    /// <param name="message">Human-readable explanation of the failure.</param>
    public NormalizationException(RawImportId rawImportId, string fieldName, string rawValue, string message)
        : base(message)
    {
        RawImportId = rawImportId;
        FieldName = fieldName;
        RawValue = rawValue;
    }

    /// <summary>Stable identifier of the raw import record that failed normalization.</summary>
    public RawImportId RawImportId { get; }

    /// <summary>Name of the field that could not be normalized.</summary>
    public string FieldName { get; }

    /// <summary>Original raw string value that failed parsing or validation.</summary>
    public string RawValue { get; }
}
