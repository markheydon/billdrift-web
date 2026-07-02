using BillDrift.Application.Approval;
using BillDrift.Application.Classification;
using BillDrift.Application.Reconciliation;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Application.Tests.Reconciliation;
using BillDrift.Domain.Classification;

namespace BillDrift.Application.Tests.Approval;

public static class ApprovalTestFixtureBuilder
{
    public static (Domain.Reconciliation.ReconciliationRun Run, ReconciliationExceptionViewModel Exceptions) Build(string reconciliationFixture)
    {
        var engine = new ReconciliationEngine(new Mapping.ProductMappingResolver());
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load(reconciliationFixture)));

        var surfacing = new ExceptionSurfacingService();
        var exceptions = surfacing.Surface(run, null, new ClassificationContext(new Dictionary<string, ItemClassification>(), DateTimeOffset.UtcNow));
        return (run, exceptions);
    }

    public static ApprovalService CreateService(InMemoryApprovalStore? store = null)
    {
        store ??= new InMemoryApprovalStore();
        var evaluator = new ApprovalEligibilityEvaluator();
        var ingestion = new ApprovalIngestionService(evaluator);
        var builder = new ApprovedChangesetBuilder();
        var exporter = new PassThroughApprovedChangesetExporter();
        var operatorContext = new OperatorContext("test-operator");

        return new ApprovalService(store, ingestion, builder, exporter, operatorContext);
    }
}
