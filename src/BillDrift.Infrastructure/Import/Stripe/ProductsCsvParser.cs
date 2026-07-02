using BillDrift.Infrastructure.Import.Stripe.Internal;

namespace BillDrift.Infrastructure.Import.Stripe;

internal sealed class ProductsCsvParser
{
    public IReadOnlyList<ParsedProductRow> Parse(StripeCsvReadResult readResult)
    {
        var rows = new List<ParsedProductRow>();
        var rowNumber = 1;

        foreach (var row in readResult.Rows)
        {
            var metadata = StripeMetadataParser.ExtractFromHeaders(readResult.Headers, row);
            var mappedHeaders = readResult.FieldMap.Values.ToHashSet(StringComparer.Ordinal);

            var additional = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in readResult.Headers)
            {
                if (!mappedHeaders.Contains(header) &&
                    row.TryGetValue(header, out var value) &&
                    !string.IsNullOrWhiteSpace(value) &&
                    !metadata.ContainsKey(header))
                {
                    additional[header] = value.Trim();
                }
            }

            rows.Add(new ParsedProductRow
            {
                RowNumber = rowNumber++,
                ProductId = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.ProductId),
                Name = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.Name),
                Metadata = metadata,
                AdditionalFields = additional
            });
        }

        return rows;
    }
}
