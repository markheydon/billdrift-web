var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.BillDrift_Api>("api")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.BillDrift_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
