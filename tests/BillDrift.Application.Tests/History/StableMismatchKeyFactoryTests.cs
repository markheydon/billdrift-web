using BillDrift.Application.History;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.History;

public sealed class StableMismatchKeyFactoryTests
{
    private readonly StableMismatchKeyFactory _factory = new();

    [Fact]
    public void Create_is_deterministic_for_identical_mismatches()
    {
        var mismatch = CreateMismatch();
        var key1 = _factory.Create(mismatch);
        var key2 = _factory.Create(mismatch);

        key1.Should().Be(key2);
    }

    [Fact]
    public void Create_differs_for_different_customers()
    {
        var key1 = _factory.Create(CreateMismatch("MEX001"));
        var key2 = _factory.Create(CreateMismatch("MEX002"));

        key1.Should().NotBe(key2);
    }

    private static Mismatch CreateMismatch(string mexId = "MEX001") =>
        new(
            MismatchId.New(),
            MismatchType.QuantityMismatch,
            MismatchSeverity.Warning,
            CustomerIdentity.Create(MexId.Create(mexId)),
            null,
            new MismatchEntityRefs(),
            "5",
            "3",
            "Quantity mismatch");
}
