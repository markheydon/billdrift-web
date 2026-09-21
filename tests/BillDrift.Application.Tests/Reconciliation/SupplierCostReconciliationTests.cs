using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Tests.Reconciliation;

public class SupplierCostReconciliationTests
{
    [Fact]
    public void Non_csp_line_flags_mapping_missing_without_bill_impacting_proposals()
    {
        var engine = new ReconciliationEngine(new Mapping.ProductMappingResolver());
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("non-csp-supplier-line")));

        Assert.Contains(run.Mismatches, m =>
            m.Type == MismatchType.MappingMissing &&
            m.Description.StartsWith("Non-CSP line requires manual mapping:"));
        Assert.Empty(run.ProposedChanges);
    }

    [Fact]
    public void Pro_rata_lines_excluded_from_quantity_comparison()
    {
        var inputs = ReconciliationTestDataBuilder.QuantityMismatch();
        var engine = new ReconciliationEngine(new Mapping.ProductMappingResolver());
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            inputs));

        var qtyMismatch = run.Mismatches.First(m => m.Type == MismatchType.QuantityMismatch);
        Assert.Equal("10", qtyMismatch.ExpectedValue);
        Assert.Equal("5", qtyMismatch.ActualValue);
    }
}
