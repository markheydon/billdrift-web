using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace BillDrift.Infrastructure.Tests.Import.Giacom;

/// <summary>
/// Generates synthetic Giacom-format PDF fixtures with positioned text for parser regression tests.
/// </summary>
public static class SyntheticGiacomPdfBuilder
{
    private const int RowHeight = 14;
    private const int LeftMargin = 40;
    private const int TopY = 750;

    public static byte[] BuildPreBillingSampleA() =>
        BuildDocument("Giacom Pre-Billing Report",
        [
            new CustomerFixture("Acme Corp", "MEX10001",
            [
                new ProductFixture("365 Business Basic", "10", "Recurring", "01/01/2026-31/01/2026", "120.00", ["REF-1001"], null)
            ]),
            new CustomerFixture("Beta Ltd", "MEX10002",
            [
                new ProductFixture("365 Premium", "5", "Recurring", "01/01/2026-31/01/2026", "85.50", ["REF-2001"], null)
            ])
        ]);

    public static byte[] BuildPreBillingSampleB() =>
        BuildDocument("Giacom Pre Billing Estimate",
        [
            new CustomerFixture("Gamma Inc", "MEX10003",
            [
                new ProductFixture("Teams Essentials", "3", "Recurring", "01/02/2026-28/02/2026", "45.00", ["REF-3001"], null),
                new ProductFixture("Backup Add-on", "3", "Adjustment", "15/02/2026-28/02/2026", "-5.00", ["REF-3002"], null)
            ])
        ]);

    public static byte[] BuildPostBillingSampleA() =>
        BuildDocument("Giacom Post-Billing Tax Invoice",
        [
            new CustomerFixture("Acme Corp", "MEX10001",
            [
                new ProductFixture("365 Business Basic", "10", "Recurring", "01/01/2026-31/01/2026", "120.00", ["REF-1001"], null)
            ])
        ]);

    public static byte[] BuildPostBillingSampleB() =>
        BuildDocument("Giacom Post Billing Invoice",
        [
            new CustomerFixture("Delta Co", "MEX10004",
            [
                new ProductFixture("365 E3", "2", "Recurring", "01/03/2026-31/03/2026", "200.00", ["REF-4001"], null)
            ])
        ]);

    public static byte[] BuildWrappedProductNameSample() =>
        BuildDocument("Giacom Pre-Billing Report",
        [
            new CustomerFixture("Wrap Test Ltd", "MEX10005",
            [
                new ProductFixture("365 Premium", "1", "Recurring", "01/01/2026-31/01/2026", "22.00", ["REF-5001"], "Security Add-on")
            ])
        ]);

    public static byte[] BuildPartialSuccessSample() =>
        BuildDocument("Giacom Pre-Billing Report",
        [
            new CustomerFixture("Partial Co", "MEX10006",
            [
                new ProductFixture("Valid Line", "2", "Recurring", "01/01/2026-31/01/2026", "50.00", ["REF-6001"], null),
                new ProductFixture("Bad Qty Line", "", "Recurring", "01/01/2026-31/01/2026", "10.00", ["REF-6002"], null)
            ])
        ]);

    public static byte[] BuildLargeSample(int customerCount, int linesPerCustomer)
    {
        var customers = Enumerable.Range(1, customerCount).Select(i =>
            new CustomerFixture($"Customer {i}", $"MEX{i + 20000:D5}",
                Enumerable.Range(1, linesPerCustomer).Select(j =>
                    new ProductFixture($"P{i}-{j}", "1", "Recurring", "01/01/2026-31/01/2026", "10.00", [$"REF-{i}-{j}"], null)
                ).ToArray()
            )).ToArray();

        return BuildDocument("Giacom Pre-Billing Report", customers);
    }

    public static byte[] BuildEmptyCoverSheet() =>
        BuildDocument("Giacom Pre-Billing Report", []);

    public static void WriteFixturesToDisk(string directory)
    {
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "expected"));

        File.WriteAllBytes(Path.Combine(directory, "pre-billing-sample-a.pdf"), BuildPreBillingSampleA());
        File.WriteAllBytes(Path.Combine(directory, "pre-billing-sample-b.pdf"), BuildPreBillingSampleB());
        File.WriteAllBytes(Path.Combine(directory, "post-billing-sample-a.pdf"), BuildPostBillingSampleA());
        File.WriteAllBytes(Path.Combine(directory, "post-billing-sample-b.pdf"), BuildPostBillingSampleB());
        File.WriteAllBytes(Path.Combine(directory, "partial-success-sample.pdf"), BuildPartialSuccessSample());
        File.WriteAllBytes(Path.Combine(directory, "wrapped-product-name.pdf"), BuildWrappedProductNameSample());
    }

    private static byte[] BuildDocument(
        string title,
        IReadOnlyList<CustomerFixture> customers,
        int columnOffset = 0)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        var y = TopY;

        page.AddText(title, 14, new PdfPoint(LeftMargin, y), font);
        y -= RowHeight * 2;

        var columns = GetColumnPositions(columnOffset);
        WriteHeaderRow(page, font, ref y, columns);

        foreach (var customer in customers)
        {
            if (y < 80)
            {
                page = builder.AddPage(PageSize.A4);
                y = TopY;
                WriteHeaderRow(page, font, ref y, columns);
            }

            y -= RowHeight;
            page.AddText($"Customer: {customer.Name}    Mex ID: {customer.MexId}", 10, new PdfPoint(LeftMargin, y), font);

            foreach (var product in customer.Products)
            {
                if (y < 80)
                {
                    page = builder.AddPage(PageSize.A4);
                    y = TopY;
                    WriteHeaderRow(page, font, ref y, columns);
                }

                y -= RowHeight;
                WriteProductRow(page, font, y, columns, product);

                if (!string.IsNullOrWhiteSpace(product.ContinuationName))
                {
                    if (y < 80)
                    {
                        page = builder.AddPage(PageSize.A4);
                        y = TopY;
                        WriteHeaderRow(page, font, ref y, columns);
                    }

                    y -= RowHeight;
                    page.AddText(product.ContinuationName, 10, new PdfPoint(columns.ProductName, y), font);
                }
            }

            y -= RowHeight;
        }

        return builder.Build();
    }

    private static void WriteHeaderRow(PdfPageBuilder page, PdfDocumentBuilder.AddedFont font, ref int y, ColumnPositions columns)
    {
        page.AddText("Product", 10, new PdfPoint(columns.ProductName, y), font);
        page.AddText("Qty", 10, new PdfPoint(columns.Quantity, y), font);
        page.AddText("Type", 10, new PdfPoint(columns.ChargeType, y), font);
        page.AddText("Period", 10, new PdfPoint(columns.Period, y), font);
        page.AddText("Cost", 10, new PdfPoint(columns.LineCost, y), font);
        page.AddText("Ref", 10, new PdfPoint(columns.References, y), font);
        y -= RowHeight;
    }

    private static void WriteProductRow(
        PdfPageBuilder page,
        PdfDocumentBuilder.AddedFont font,
        int y,
        ColumnPositions columns,
        ProductFixture product)
    {
        page.AddText(product.Name, 10, new PdfPoint(columns.ProductName, y), font);
        if (!string.IsNullOrWhiteSpace(product.Quantity))
        {
            page.AddText(product.Quantity, 10, new PdfPoint(columns.Quantity, y), font);
        }

        page.AddText(product.ChargeType, 10, new PdfPoint(columns.ChargeType, y), font);
        page.AddText(product.Period, 10, new PdfPoint(columns.Period, y), font);
        if (!string.IsNullOrWhiteSpace(product.LineCost))
        {
            page.AddText(product.LineCost, 10, new PdfPoint(columns.LineCost, y), font);
        }

        if (product.References.Count > 0)
        {
            page.AddText(product.References[0], 10, new PdfPoint(columns.References, y), font);
        }
    }

    private static ColumnPositions GetColumnPositions(int offset) =>
        new(
            LeftMargin + offset,
            180 + offset,
            240 + offset,
            320 + offset,
            520 + offset,
            560 + offset);

    private sealed record CustomerFixture(string Name, string MexId, IReadOnlyList<ProductFixture> Products);

    private sealed record ProductFixture(
        string Name,
        string Quantity,
        string ChargeType,
        string Period,
        string LineCost,
        IReadOnlyList<string> References,
        string? ContinuationName);

    private sealed record ColumnPositions(
        int ProductName,
        int Quantity,
        int ChargeType,
        int Period,
        int LineCost,
        int References);
}
