using BillDrift.Domain.Common;

namespace BillDrift.Application.Normalization;

public sealed class NormalizationException : Exception
{
    public NormalizationException(RawImportId rawImportId, string fieldName, string rawValue, string message)
        : base(message)
    {
        RawImportId = rawImportId;
        FieldName = fieldName;
        RawValue = rawValue;
    }

    public RawImportId RawImportId { get; }
    public string FieldName { get; }
    public string RawValue { get; }
}
