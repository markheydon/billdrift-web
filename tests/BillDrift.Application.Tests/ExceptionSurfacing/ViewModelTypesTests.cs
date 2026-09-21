using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

public class ViewModelTypesTests
{
    [Fact]
    public void SurfacedExceptionId_from_mismatch_has_expected_format()
    {
        var runId = RunId.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var mismatchId = MismatchId.FromGuid(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        var id = SurfacedExceptionId.FromMismatch(runId, mismatchId);

        Assert.Equal($"{runId.Value}:m:{mismatchId.Value}", id.Value);
    }

    [Fact]
    public void SurfacedExceptionId_from_derived_has_expected_format()
    {
        var runId = RunId.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var id = SurfacedExceptionId.FromDerived(runId, "OrphanedStripe", "si_abc");

        Assert.Equal($"{runId.Value}:d:OrphanedStripe:si_abc", id.Value);
    }

    [Fact]
    public void ReconciliationExceptionViewModel_HasExceptions_reflects_total_count()
    {
        var empty = CreateViewModel(totalCount: 0);
        var withItems = CreateViewModel(totalCount: 2);

        Assert.False(empty.HasExceptions);
        Assert.True(withItems.HasExceptions);
    }

    [Fact]
    public void FlatExceptions_returns_exceptions_in_group_order()
    {
        var ex1 = CreateException("ex-1", MexId.Create("MEX-A"));
        var ex2 = CreateException("ex-2", MexId.Create("MEX-B"));
        var groupA = new CustomerExceptionGroup(
            CustomerIdentity.Create(MexId.Create("MEX-A")),
            "A",
            ExceptionSeverity.Error,
            new Dictionary<ExceptionSeverity, int> { [ExceptionSeverity.Error] = 1 },
            1,
            [ex1]);
        var groupB = new CustomerExceptionGroup(
            CustomerIdentity.Create(MexId.Create("MEX-B")),
            "B",
            ExceptionSeverity.Warning,
            new Dictionary<ExceptionSeverity, int> { [ExceptionSeverity.Warning] = 1 },
            0,
            [ex2]);

        var vm = new ReconciliationExceptionViewModel(
            RunId.New(),
            BillingPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)),
            DateTimeOffset.UtcNow,
            new ExceptionRunSummary(2, new Dictionary<ExceptionSeverity, int>(), new Dictionary<ExceptionCategory, int>(), new Dictionary<ReconciliationDomain, int>(), 2, 1, 0),
            [groupA, groupB]);

        Assert.Equal(new[] { "ex-1", "ex-2" }, vm.FlatExceptions().Select(e => e.Id.Value));
    }

    private static ReconciliationExceptionViewModel CreateViewModel(int totalCount) =>
        new(
            RunId.New(),
            BillingPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)),
            DateTimeOffset.UtcNow,
            new ExceptionRunSummary(totalCount, new Dictionary<ExceptionSeverity, int>(), new Dictionary<ExceptionCategory, int>(), new Dictionary<ReconciliationDomain, int>(), totalCount > 0 ? 1 : 0, 0, 0),
            []);

    private static SurfacedException CreateException(string id, MexId mex) =>
        new(
            new SurfacedExceptionId(id),
            ExceptionCategory.MissingBillingItem,
            ReconciliationDomain.TruthVsStripe,
            ExceptionSeverity.Error,
            CustomerIdentity.Create(mex),
            null,
            "Test explanation",
            [],
            true,
            null,
            0,
            []);
}
