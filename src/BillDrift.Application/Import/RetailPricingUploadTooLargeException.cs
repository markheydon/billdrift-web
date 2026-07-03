namespace BillDrift.Application.Import;

/// <summary>
/// Thrown when an uploaded reseller price list CSV exceeds the configured maximum size.
/// </summary>
public sealed class RetailPricingUploadTooLargeException : Exception
{
    /// <summary>
    /// Creates an exception for a file that exceeds <paramref name="maxFileSizeBytes"/>.
    /// </summary>
    public RetailPricingUploadTooLargeException(long maxFileSizeBytes)
        : base($"CSV file exceeds maximum allowed size of {maxFileSizeBytes} bytes.")
    {
        MaxFileSizeBytes = maxFileSizeBytes;
    }

    /// <summary>The configured maximum allowed size in bytes.</summary>
    public long MaxFileSizeBytes { get; }
}
