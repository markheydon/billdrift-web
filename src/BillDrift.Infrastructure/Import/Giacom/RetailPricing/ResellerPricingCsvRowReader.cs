using System.Globalization;
using System.Text;
using BillDrift.Application.Import;
using CsvHelper;
using CsvHelper.Configuration;

namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing;

internal sealed class ResellerPricingCsvReadResult
{
    public bool IsSuccess => FileFailure is null;
    public IngestionFailureReason? FileFailure { get; init; }
    public string? FailureMessage { get; init; }
    public IReadOnlyList<string> Headers { get; init; } = [];
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; init; } = [];
    public IReadOnlyDictionary<ResellerPricingLogicalField, string> FieldMap { get; init; }
        = new Dictionary<ResellerPricingLogicalField, string>();
}

/// <summary>CsvHelper wrapper for Giacom reseller price list exports.</summary>
internal sealed class ResellerPricingCsvRowReader
{
    public ResellerPricingCsvReadResult Read(byte[] content)
    {
        if (content.Length == 0)
        {
            return new ResellerPricingCsvReadResult
            {
                FileFailure = IngestionFailureReason.EmptyFile,
                FailureMessage = "CSV file is empty."
            };
        }

        using var stream = new MemoryStream(content);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true
        };

        using var csv = new CsvReader(reader, config);
        if (!csv.Read() || !csv.ReadHeader())
        {
            return new ResellerPricingCsvReadResult
            {
                FileFailure = IngestionFailureReason.MandatoryHeaderMissing,
                FailureMessage = "CSV file has no header row."
            };
        }

        var headers = csv.HeaderRecord?
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim())
            .ToList() ?? [];

        if (headers.Count == 0)
        {
            return new ResellerPricingCsvReadResult
            {
                FileFailure = IngestionFailureReason.MandatoryHeaderMissing,
                FailureMessage = "CSV header row is empty."
            };
        }

        var missing = ResellerPricingCsvHeaderMap.GetMissingRequiredFields(headers);
        if (missing.Count > 0)
        {
            return new ResellerPricingCsvReadResult
            {
                FileFailure = IngestionFailureReason.MandatoryHeaderMissing,
                FailureMessage = $"Missing required columns: {string.Join(", ", missing)}.",
                Headers = headers
            };
        }

        var fieldMap = ResellerPricingCsvHeaderMap.BuildFieldToHeaderMap(headers);
        var rows = new List<IReadOnlyDictionary<string, string>>();

        while (csv.Read())
        {
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var header in headers)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }

            if (row.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(row);
        }

        if (rows.Count == 0)
        {
            return new ResellerPricingCsvReadResult
            {
                FileFailure = IngestionFailureReason.EmptyFile,
                FailureMessage = "CSV file contains headers but no data rows.",
                Headers = headers,
                FieldMap = fieldMap
            };
        }

        return new ResellerPricingCsvReadResult
        {
            Headers = headers,
            Rows = rows,
            FieldMap = fieldMap
        };
    }
}
