using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using FluentAssertions;

namespace BillDrift.Domain.Tests.Classification;

public sealed class ClassificationTypesTests
{
    [Fact]
    public void ReconciliationItemRef_RejectsEmptyStableKey()
    {
        var act = () => ReconciliationItemRef.Create(
            ReconciliationItemKind.SupplierCost,
            " ",
            MexId.Create("MEX-001"));

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void ClassificationRuleConfiguration_Default_IsEmpty()
    {
        var config = ClassificationRuleConfiguration.Default;
        config.InternalMexIds.Should().BeEmpty();
        config.ProductCategoryRules.Should().BeEmpty();
        config.RequireNotesForAlertSuppression.Should().BeTrue();
    }
}
