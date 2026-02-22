using Projects;
using SqlAuditedQueryTool.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql", port: 44444)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpointProxySupport(false);

var db = sql.AddDatabase("db")
    .WithDatabaseSeeding(Path.Combine(builder.AppHostDirectory, "..", "database", "seed.sql"));

var ollama = builder.AddOllama("ollama")
    .WithDataVolume()
    .WithGPUSupport()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithOpenWebUI();

var ollamaModel = ollama.AddModel("ollamaModel", "qwen2.5-coder:7b");

var api = builder.AddProject<SqlAuditedQueryTool_App>("api")
    .WithReference(db).WaitFor(db)
    .WithReference(ollamaModel).WaitFor(ollamaModel);

builder.AddViteApp("frontend", "../src/SqlAuditedQueryTool.App/ClientApp")
    .WithHttpEndpoint(port: 5173, name: "vite")
    .WithReference(api).WaitFor(api);

builder.Build().Run();
