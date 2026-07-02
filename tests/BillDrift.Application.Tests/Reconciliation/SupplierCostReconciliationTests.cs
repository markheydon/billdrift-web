using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Common;
using FluentAssertions;

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

        run.Mismatches.Should().Contain(m =>
            m.Type == MismatchType.MappingMissing &&
            m.Description.StartsWith("Non-CSP line requires manual mapping:"));
        run.ProposedChanges.Should().BeEmpty();
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
        qtyMismatch.ExpectedValue.Should().Be("10");
        qtyMismatch.ActualValue.Should().Be("5");
    }
}
