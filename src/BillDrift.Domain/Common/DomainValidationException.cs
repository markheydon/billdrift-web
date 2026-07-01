namespace BillDrift.Domain.Common;

/// <summary>
/// Thrown when a domain value object or entity fails invariant validation during construction.
/// </summary>
public sealed class DomainValidationException : Exception
{
    /// <summary>
    /// Initializes a validation exception for the named property.
    /// </summary>
    /// <param name="propertyName">The property or parameter that failed validation.</param>
    /// <param name="message">A human-readable description of the validation failure.</param>
    public DomainValidationException(string propertyName, string message)
        : base(message)
    {
        PropertyName = propertyName;
    }

    /// <summary>
    /// The property or parameter name associated with the validation failure.
    /// </summary>
    public string PropertyName { get; }
}
