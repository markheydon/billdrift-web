using BillDrift.Infrastructure.Import.Stripe.Internal;

namespace BillDrift.Infrastructure.Import.Stripe;

internal sealed class SubscriptionsCsvParser
{
    public IReadOnlyList<ParsedSubscriptionRow> Parse(StripeCsvReadResult readResult)
    {
        var rows = new List<ParsedSubscriptionRow>();
        var rowNumber = 1;

        foreach (var row in readResult.Rows)
        {
            var metadata = StripeMetadataParser.ExtractFromHeaders(readResult.Headers, row);
            var mappedHeaders = readResult.FieldMap.Values.ToHashSet(StringComparer.Ordinal);

            var parsed = new ParsedSubscriptionRow
            {
                RowNumber = rowNumber++,
                CustomerId = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.CustomerId),
                CustomerName = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.CustomerName),
                SubscriptionId = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.SubscriptionId),
                SubscriptionItemId = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.SubscriptionItemId),
                ProductId = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.ProductId),
                ProductName = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.ProductName),
                PriceId = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.PriceId),
                QuantityRaw = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.Quantity),
                Status = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.Status),
                UnitAmountRaw = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.UnitAmount),
                IntervalRaw = StripeCsvHeaderMap.GetFieldValue(row, readResult.FieldMap, StripeLogicalField.Interval),
                Metadata = metadata
            };

            foreach (var header in readResult.Headers)
            {
                if (!mappedHeaders.Contains(header) &&
                    row.TryGetValue(header, out var extra) &&
                    !string.IsNullOrWhiteSpace(extra) &&
                    !parsed.Metadata.ContainsKey(header))
                {
                    parsed.Metadata[header] = extra.Trim();
                }
            }

            rows.Add(parsed);
        }

        return rows;
    }
}
