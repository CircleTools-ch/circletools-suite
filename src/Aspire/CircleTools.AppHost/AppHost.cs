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

// Opt-in local observability stack (ADR-0051): run the SAME Loki + Prometheus + Grafana as production,
// with the same dashboards and datasources, so the observability can be validated before deploying to
// the VM. Off by default (the Aspire dashboard covers day-to-day dev) — flip it on with the
// "https-observability" launch profile, which sets Observability:DevStack=true. See the container block
// at the end of this file.
var enableObservability = string.Equals(
    builder.Configuration["Observability:DevStack"], "true", StringComparison.OrdinalIgnoreCase);

// Main API — receives ConnectionStrings__maindb via WithReference(maindb). The launch profile is
// pinned to "https" so it does not depend on the AppHost's own profile NAME: Aspire otherwise picks
// each child project's profile by matching the AppHost profile name, and any custom AppHost profile
// (e.g. https-observability) would fall back to the project's first profile (http). The CORS wiring
// below resolves each web app's "https" endpoint, so the web apps are pinned the same way.
var api = builder.AddProject<Projects.CircleTools_Main_Service>("main-api", launchProfileName: "https")
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
var web = builder.AddProject<Projects.CircleTools_Main_WebApp>("main-web", launchProfileName: "https")
    .WithReference(api);

var secretSantaWeb = builder.AddProject<Projects.CircleTools_SecretSanta_WebApp>("secretsanta-web", launchProfileName: "https")
    .WithReference(api);

// Allow each WASM origin on the API (CORS), one indexed Cors:AllowedOrigins entry per client.
api.WithEnvironment("Cors__AllowedOrigins__0", web.GetEndpoint("https"));
api.WithEnvironment("Cors__AllowedOrigins__1", secretSantaWeb.GetEndpoint("https"));

// --- Local observability stack (opt-in) ----------------------------------------------------------
// The same three images and versions as production (deploy/compose.yaml), fed the SAME config files
// via bind mount so what renders here is what will render on the VM:
//   • Grafana provisioning (datasources + the CircleTools dashboard) — reused byte-for-byte.
//   • Loki config — reused byte-for-byte.
//   • Prometheus — a dev-only scrape file (prometheus.dev.yml); only the scrape topology differs,
//     because the API runs on the host here rather than in the container network.
// The containers share Aspire's network and resolve each other by resource name (loki, prometheus),
// which is exactly the hostnames the provisioned Grafana datasources use — so no datasource edits.
if (enableObservability)
{
    var loki = builder.AddContainer("loki", "grafana/loki", "3.3.2")
        .WithBindMount("../../../deploy/loki/loki-config.yaml", "/etc/loki/loki-config.yaml", isReadOnly: true)
        .WithArgs("-config.file=/etc/loki/loki-config.yaml")
        .WithHttpEndpoint(port: 3100, targetPort: 3100, name: "http");

    var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "v3.1.0")
        .WithBindMount("../../../deploy/prometheus/prometheus.dev.yml", "/etc/prometheus/prometheus.yml", isReadOnly: true)
        .WithArgs("--config.file=/etc/prometheus/prometheus.yml")
        .WithHttpEndpoint(port: 9090, targetPort: 9090, name: "http");

    builder.AddContainer("grafana", "grafana/grafana", "11.4.0")
        .WithBindMount("../../../deploy/grafana/provisioning", "/etc/grafana/provisioning", isReadOnly: true)
        // Dev convenience only: skip the login wall (the production Grafana keeps its admin login).
        .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
        .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin")
        .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "http")
        .WaitFor(prometheus)
        .WaitFor(loki);

    // Ship the API's structured logs to the dev Loki over OTLP (same code path as production, gated on
    // this config value in ServiceDefaults). The API is a host process, so it reaches the published Loki
    // port on localhost — not the container-network hostname. Setting this makes ServiceDefaults route
    // OTLP to Loki *instead of* the Aspire dashboard (the two OTLP exporters cannot coexist), which is
    // the point of this profile: you use Grafana, not the Aspire dashboard, for logs here.
    // No WaitFor(loki): the OTLP log exporter already retries while Loki is starting, and gating the
    // API on Loki only delays it (Loki's container has no health check, so the wait can hang).
    api.WithEnvironment("Observability__LokiOtlpEndpoint", "http://localhost:3100/otlp/v1/logs");
}

builder.Build().Run();
