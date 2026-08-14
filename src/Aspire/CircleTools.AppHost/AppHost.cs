var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL resource + the Main module's database.
var postgres = builder.AddPostgres("postgres");
var maindb = postgres.AddDatabase("maindb");

// Mailpit — the development mail sink (ADR-0023). It stands in for the not-yet-chosen
// transactional email provider so identity flows that send mail (FR-007 verification,
// FR-009 password reset, FR-013 email change) can be built and tested with no external
// account. The app sends through IEmailSender over SMTP and references no provider SDK;
// choosing a provider later is an adapter plus configuration, not a code change.
//   SMTP  → 1025 (consumed by the API below)
//   Inbox → 8025 (browsable from the Aspire dashboard)
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp")
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "inbox");

var smtp = mailpit.GetEndpoint("smtp");

// Main API — receives ConnectionStrings__maindb via WithReference(maindb).
var api = builder.AddProject<Projects.CircleTools_Main_Service>("main-api")
    .WithReference(maindb)
    .WithEnvironment("Mail__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Mail__Port", smtp.Property(EndpointProperty.Port))
    .WithEnvironment("Mail__FromAddress", "no-reply@circletools.test")
    .WaitFor(maindb)
    .WaitFor(mailpit);

// Standalone Blazor WebAssembly PWAs — pure API clients (ADR-0013). Both the Main shell and
// Secret-Santa run this way: offline and account-free by default, but reaching the Main API
// once a session exists (account, sync, circles). Neither is a backend; each is a static WASM
// client, so its API base address is configured in wwwroot/appsettings.json (ApiBaseUrl) —
// the browser cannot read server env vars. The fixed dev ports keep client and API in sync.
var web = builder.AddProject<Projects.CircleTools_Main_WebApp>("main-web")
    .WithReference(api);

var secretSantaWeb = builder.AddProject<Projects.CircleTools_SecretSanta_WebApp>("secretsanta-web")
    .WithReference(api);

// Allow each WASM origin on the API (CORS), one indexed Cors:AllowedOrigins entry per client.
api.WithEnvironment("Cors__AllowedOrigins__0", web.GetEndpoint("https"));
api.WithEnvironment("Cors__AllowedOrigins__1", secretSantaWeb.GetEndpoint("https"));

builder.Build().Run();
