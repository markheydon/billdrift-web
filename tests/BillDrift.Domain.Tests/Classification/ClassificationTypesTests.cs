using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;

namespace BillDrift.Domain.Tests.Classification;

public sealed class ClassificationTypesTests
{
    [Fact]
    public void ReconciliationItemRef_RejectsEmptyStableKey()
    {
        Assert.Throws<DomainValidationException>(() =>
        {
            _ = ReconciliationItemRef.Create(
                ReconciliationItemKind.SupplierCost,
                " ",
                MexId.Create("MEX-001"));
        });
    }

    [Fact]
    public void ClassificationRuleConfiguration_Default_IsEmpty()
    {
        var config = ClassificationRuleConfiguration.Default;
        Assert.Empty(config.InternalMexIds);
        Assert.Empty(config.ProductCategoryRules);
        Assert.True(config.RequireNotesForAlertSuppression);
    }
}
