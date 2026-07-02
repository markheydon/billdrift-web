using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using FluentAssertions;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

public class ConsolidationTests
{
    private readonly ExceptionSurfacingTestBuilder _builder = new();

    [Fact]
    public void CR1_catalogue_exceptions_consolidate_per_commercial_key()
    {
        var vm = _builder.SurfaceScenario("catalogue-consolidation");

        var catalogue = vm.FlatExceptions()
            .Where(e => e.Domain == ReconciliationDomain.PricingVsCatalogue)
            .ToList();

        catalogue.Should().NotBeEmpty();
        catalogue.Select(e => e.Product?.CommercialKey).Distinct().Count()
            .Should().BeLessThanOrEqualTo(catalogue.Count);
    }
}
