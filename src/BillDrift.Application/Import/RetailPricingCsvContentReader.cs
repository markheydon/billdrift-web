namespace BillDrift.Application.Import;

/// <summary>Reads reseller price list CSV upload streams with an enforced maximum size.</summary>
public static class RetailPricingCsvContentReader
{
    private const int BufferSize = 81_920;

    /// <summary>Reads the stream into a byte array, rejecting content that exceeds the limit.</summary>
    public static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        long maxFileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (maxFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes), "Maximum file size must be positive.");
        }

        if (stream.CanSeek && stream.Length > maxFileSizeBytes)
        {
            throw new RetailPricingUploadTooLargeException(maxFileSizeBytes);
        }

        using var memory = new MemoryStream();
        var buffer = new byte[BufferSize];
        long totalRead = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > maxFileSizeBytes)
            {
                throw new RetailPricingUploadTooLargeException(maxFileSizeBytes);
            }

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    /// <summary>Synchronous bounded read for parser intake paths.</summary>
    public static byte[] ReadBounded(Stream stream, long maxFileSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (maxFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes), "Maximum file size must be positive.");
        }

        if (stream is MemoryStream ms && ms.TryGetBuffer(out var segment))
        {
            if (segment.Count > maxFileSizeBytes)
            {
                throw new RetailPricingUploadTooLargeException(maxFileSizeBytes);
            }

            return segment.ToArray();
        }

        if (stream.CanSeek && stream.Length > maxFileSizeBytes)
        {
            throw new RetailPricingUploadTooLargeException(maxFileSizeBytes);
        }

        using var memory = new MemoryStream();
        var buffer = new byte[BufferSize];
        long totalRead = 0;

        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += read;
            if (totalRead > maxFileSizeBytes)
            {
                throw new RetailPricingUploadTooLargeException(maxFileSizeBytes);
            }

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }
}
