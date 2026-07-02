var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = storage.AddTables("tables");
var blobs = storage.AddBlobs("blobs");

var api = builder.AddProject<Projects.BillDrift_Api>("api")
    .WithHttpHealthCheck("/health")
    .WithReference(tables)
    .WithReference(blobs);

builder.AddProject<Projects.BillDrift_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
