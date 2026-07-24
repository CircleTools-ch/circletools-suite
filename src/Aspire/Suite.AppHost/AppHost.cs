var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL resource + the Main module's database.
var postgres = builder.AddPostgres("postgres");
var maindb = postgres.AddDatabase("maindb");

// Main API — receives ConnectionStrings__maindb via WithReference(maindb).
var api = builder.AddProject<Projects.Suite_Main_Service>("main-api")
    .WithReference(maindb)
    .WaitFor(maindb);

// Standalone Blazor WebAssembly PWA — pure API client.
var web = builder.AddProject<Projects.Suite_Main_WebApp>("main-web")
    .WithReference(api);

// Allow the web origin on the API (CORS). Note: because the WebApp is a standalone
// WASM client, its API base address is configured in wwwroot/appsettings.json
// (ApiBaseUrl) — the browser cannot read server env vars. The fixed dev ports keep
// the two in sync locally.
api.WithEnvironment("Cors__AllowedOrigins__0", web.GetEndpoint("https"));

builder.Build().Run();
