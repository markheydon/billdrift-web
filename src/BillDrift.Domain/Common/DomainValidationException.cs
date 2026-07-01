namespace BillDrift.Domain.Common;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string propertyName, string message)
        : base(message)
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; }
}
