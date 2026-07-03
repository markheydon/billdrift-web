namespace BillDrift.Application.Import;

/// <summary>
/// Thrown when an uploaded Subscription Management CSV exceeds the configured maximum size.
/// </summary>
public sealed class SubscriptionManagementUploadTooLargeException : Exception
{
    /// <summary>
    /// Creates an exception for a file that exceeds <paramref name="maxFileSizeBytes"/>.
    /// </summary>
    /// <param name="maxFileSizeBytes">The configured maximum allowed size in bytes.</param>
    public SubscriptionManagementUploadTooLargeException(long maxFileSizeBytes)
        : base($"CSV file exceeds maximum allowed size of {maxFileSizeBytes} bytes.")
    {
        MaxFileSizeBytes = maxFileSizeBytes;
    }

    /// <summary>The configured maximum allowed size in bytes.</summary>
    public long MaxFileSizeBytes { get; }
}
