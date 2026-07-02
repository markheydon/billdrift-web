using BillDrift.Infrastructure.Import.Stripe.Internal;

namespace BillDrift.Infrastructure.Import.Stripe;

internal sealed class PricesCsvParser
{
    public IReadOnlyList<ParsedPriceRow> Parse(StripeCsvReadResult readResult)
    {
        var rows = new List<ParsedPriceRow>();
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

            rows.Add(new ParsedPriceRow
            {
                RowNumber = rowNumber++,
                PriceId = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.PriceId),
                ProductId = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.ProductId),
                Currency = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.Currency),
                UnitAmountRaw = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.UnitAmount),
                RecurringInterval = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.RecurringInterval),
                RecurringIntervalCountRaw = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.RecurringIntervalCount),
                Description = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.Description),
                Metadata = metadata,
                AdditionalFields = additional
            });
        }

        return rows;
    }
}
