IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

var password = builder.AddParameter("sql-password", secret: true);

var sql = builder
    .AddSqlServer("sql", password: password)
    .WithDataVolume("sql-data")
    .WithHostPort(1433)
    .WithLifetime(ContainerLifetime.Persistent);

var routingDb = sql.AddDatabase("routing-db");
var tenantADb = sql.AddDatabase("tenant-a");
var tenantBDb = sql.AddDatabase("tenant-b");

// Azure Service Bus — existing namespace, connection string supplied via AppHost user secrets
var serviceBusConstring = builder.AddConnectionString("service-bus");

builder.AddProject<Projects.Web_Api>("web-api")
    .WithReference(routingDb)
    .WithReference(tenantADb)
    .WithReference(tenantBDb)
    .WaitFor(routingDb);

builder.AddProject<Projects.Worker>("worker")
    .WithReference(routingDb)
    .WithReference(serviceBusConstring)
    .WithEnvironment("Worker__UseLocalJobQueue", "false")
    .WaitFor(routingDb)
    .WaitFor(serviceBusConstring);

builder.Build().Run();
