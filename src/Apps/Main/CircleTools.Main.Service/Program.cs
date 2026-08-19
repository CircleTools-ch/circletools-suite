using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Asp.Versioning.Builder;
using CircleTools.Identity.Tokens;
using CircleTools.Main.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using CircleTools.Application;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core;
using CircleTools.Main.Service.Core.Application;
using CircleTools.Main.Service.Core.Domain;
using CircleTools.Main.Service.Repository;
using CircleTools.Main.Service.Repository.Linking;
using CircleTools.Circles.Core;
using CircleTools.Circles.Core.Domain;
using CircleTools.Circles.Repository;

var builder = WebApplication.CreateBuilder(args);

const string corsPolicyName = "CircleToolsCors";

// Aspire service defaults (OpenTelemetry, health checks, resilience, service discovery).
builder.AddServiceDefaults();

// Application layer (CQRS dispatcher + behaviors) and the Main module.
builder.Services.AddApplication();
builder.Services.AddMainCore();

var mainConnectionString = builder.Configuration.GetConnectionString("maindb")
    ?? throw new InvalidOperationException("Connection string 'maindb' is not configured.");

builder.Services.AddMainRepository(mainConnectionString);

// Sync (FEAT-004). A third context and schema in the same database, on the same seam as Circles —
// the account-scoped store that carries a claimed group between a user's devices (ADR-0016/0033).
builder.Services.AddSyncRepository(mainConnectionString);

// Linking (FEAT-005). A fourth context and schema — the one server-side, per-caller-authorized store
// ADR-0020 sanctioned (member-links, the assignment projection, self-owned wishlists). It sits beside
// the opaque sync store, and is the only place the host understands the Secret-Santa schema.
builder.Services.AddLinkingRepository(mainConnectionString);

// Circles (FEAT-003). Its own DbContext and its own schema inside the same database — a modular
// monolith with a visible seam, not a second database (ADR-0003).
builder.Services.AddCirclesCore();
builder.Services.AddCirclesRepository(mainConnectionString);
builder.Services.AddCirclesRetention(builder.Configuration);
builder.Services.Configure<CircleLimits>(builder.Configuration.GetSection(CircleLimits.SectionName));

// The two Circles ports no other project can supply: one needs Identity, the other needs the
// outbox, and the Circles context references neither. This host is the only place that has both.
builder.Services.AddScoped<IAccountDirectory, IdentityAccountDirectory>();
builder.Services.AddScoped<IInvitationMailer, OutboxInvitationMailer>();

// The two account-deletion seams, in the direction Main defines them (FR-015). The guard is what
// M1 shipped IAccountDeletionGuard for and left unregistered on purpose; the cleanup is its other
// half, without which a deleted account leaves memberships pointing at nobody.
builder.Services.AddScoped<IAccountDeletionGuard, SoleOwnerCircleGuard>();
builder.Services.AddScoped<IAccountDeletionCleanup, CircleMembershipCleanup>();

// The linking context's half of account deletion (FR-015): a second cleanup that unlinks every member
// the departing account backed, so no group is left with a dangling UserId. The delete flow resolves
// the whole set of cleanups and runs each.
builder.Services.AddScoped<IAccountDeletionCleanup, LinkingAccountCleanup>();

// Identity, sessions and bearer-token validation — ADR-0009, ADR-0021.
// AddDefaultTokenProviders must be applied here: it lives in the ASP.NET Core framework
// assembly, and the email-confirmation and password-reset tokens depend on it.
// SignInManager drives the WebAuthn ceremonies (FR-010/011) and reads the current request, so it
// needs IHttpContextAccessor — nothing else in this host registers it, and without it every
// passkey endpoint fails at runtime with "HttpContext must not be null".
builder.Services.AddHttpContextAccessor();
builder.Services.AddCircleToolsIdentityCore().AddDefaultTokenProviders().AddSignInManager();

// Passkey relying-party settings (FR-010). ServerDomain is the RP ID: the *registrable* suite
// domain, never an app subdomain, so one passkey works across the shell and every app. Passkeys
// do not migrate between RP IDs, so this value must be settled before real users register any.
builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    var serverDomain = builder.Configuration["Auth:Passkey:ServerDomain"];

    if (!string.IsNullOrWhiteSpace(serverDomain))
    {
        options.ServerDomain = serverDomain;
    }
});
builder.Services.AddCircleToolsSessions(builder.Configuration);
builder.Services.AddCircleToolsMail(builder.Configuration);
builder.Services.AddCircleToolsRetention(builder.Configuration);

// Cross-app SSO hand-off (FR-031, ADR-0039). The code store is a singleton because it holds the
// short-lived codes in memory across requests; the return-uri validator reads the same
// Cors:AllowedOrigins the CORS policy below reads, so the two allow-lists cannot drift.
builder.Services.AddSingleton<IConnectAuthorizationCodeStore, InMemoryConnectAuthorizationCodeStore>();
builder.Services.AddScoped<ConnectAuthorizationCodeService>();
builder.Services.AddSingleton<ConnectReturnUriValidator>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,

            // A 15-minute access token leaves no room for generous clock drift.
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

// The WebAuthn ceremony helpers on SignInManager persist their challenge through a *sign-in*
// authentication handler, and refuse to run without one ("No sign-in authentication handlers are
// registered"). Registering this cookie scheme is what makes FR-010/011 work at all.
//
// This is narrower than it looks and does NOT reopen ADR-0021: the cookie carries ceremony state
// for the seconds a passkey prompt is open, never a session. Sign-in still yields a bearer token
// exactly as password sign-in does. Flagged as an ADR-0021 follow-up because it is a deviation
// from "no cookies" that a reader would otherwise assume was an oversight.
builder.Services.AddAuthentication().AddCookie(IdentityConstants.TwoFactorUserIdScheme, options =>
{
    // The client is a separate origin (ADR-0013), so this cookie is set and returned on cross-origin
    // fetches. SameSite=Lax would be dropped on those, which fails the ceremony with a "challenge not
    // found" that looks nothing like its actual cause. None requires Secure, which is satisfied
    // everywhere the app runs: HTTPS in deployment, and localhost counts as a secure context.
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;

    // Ceremony state, not a session: it is worthless once the prompt is answered.
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
});

builder.Services.AddAuthorization();

// Rate limiting for the address-accepting endpoints (NFR-005). This is what stops the identical
// responses those endpoints return from being defeated by volume or timing.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(AccountEndpoints.SensitiveRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:SensitivePermitLimit", 20),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // A rejection must look the same whether or not the account exists — a lockout message that
    // only appears for real accounts is itself an enumeration oracle (NFR-005).
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many requests. Please try again later." },
            cancellationToken);
    };
});

// API versioning (URL-segment, /api/v1) — ADR-0010.
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// CORS: allow the origins configured under "Cors:AllowedOrigins".
//
// AllowCredentials is required by the WebAuthn ceremonies alone: without it the browser refuses to
// send or store the challenge cookie on a cross-origin fetch, and FR-010/011 cannot complete. It is
// only legal alongside an explicit origin list — pairing it with AllowAnyOrigin is rejected by
// ASP.NET Core at startup, and would be a genuine hole rather than a technicality. Every other
// endpoint authenticates with a bearer header and is unaffected.
// In Development the origin cannot be pinned down in advance. Aspire publishes each project on the
// port from its launch profile but proxies it to a Kestrel port the host assigns at start-up, and
// depending on how the app is opened — dashboard link, Visual Studio, a bookmark — the browser can
// end up on either one. A single configured origin then rejects the other with a bare preflight
// failure, which surfaces to the user as "the server is unreachable" and says nothing about why.
// Loopback origins only, and only here; a deployed environment still gets the explicit list.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var isDevelopment = builder.Environment.IsDevelopment();
builder.Services.AddCors(options => options.AddPolicy(corsPolicyName, policy =>
{
    if (isDevelopment)
    {
        policy.SetIsOriginAllowed(IsLoopbackOrigin).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
    else if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

static bool IsLoopbackOrigin(string origin) =>
    Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback;

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Apply pending migrations for the four contexts (independent histories, one database). Automatic in
// Development; elsewhere it is an *explicit* release step, never silent (ADR-0036 runbook): run the
// image with the `--migrate` argument as a one-shot job that migrates and exits, or set
// `Database:ApplyMigrationsOnStartup=true` on the running host. The API container starts with neither,
// so a normal boot never migrates in production — the deploy runs the migrator first.
var runMigrateAndExit = args.Contains("--migrate");
if (app.Environment.IsDevelopment()
    || runMigrateAndExit
    || app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    services.GetRequiredService<MainDbContext>().Database.Migrate();
    services.GetRequiredService<SyncDbContext>().Database.Migrate();
    services.GetRequiredService<CirclesDbContext>().Database.Migrate();
    services.GetRequiredService<LinkingDbContext>().Database.Migrate();

    if (runMigrateAndExit)
    {
        return; // One-shot migrator: migrations applied, do not start the web host.
    }
}

app.UseHttpsRedirection();
app.UseCors(corsPolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

var account = app
    .MapGroup("/api/v{version:apiVersion}/account")
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(new ApiVersion(1, 0));

account.MapAccountEndpoints();
account.MapPasskeyEndpoints();

var circles = app
    .MapGroup("/api/v{version:apiVersion}/circles")
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(new ApiVersion(1, 0));

circles.MapCircleEndpoints();

var invitations = app
    .MapGroup("/api/v{version:apiVersion}/invitations")
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(new ApiVersion(1, 0));

invitations.MapInvitationEndpoints();

var sync = app
    .MapGroup("/api/v{version:apiVersion}/sync")
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(new ApiVersion(1, 0));

sync.MapSyncEndpoints();

// Linking (FEAT-005): member-links, the group-scoped assignment/wishlist/publish routes, and the two
// cross-group overviews. All bearer-authorized and per-caller-scoped on this same host.
app.MapGroup("/api/v{version:apiVersion}/member-links")
    .WithApiVersionSet(versionSet).MapToApiVersion(new ApiVersion(1, 0))
    .MapMemberLinkEndpoints();

app.MapGroup("/api/v{version:apiVersion}/groups")
    .WithApiVersionSet(versionSet).MapToApiVersion(new ApiVersion(1, 0))
    .MapGroupLinkingEndpoints();

app.MapGroup("/api/v{version:apiVersion}/my-assignments")
    .WithApiVersionSet(versionSet).MapToApiVersion(new ApiVersion(1, 0))
    .MapMyAssignmentsEndpoint();

app.MapGroup("/api/v{version:apiVersion}/my-wishlists")
    .WithApiVersionSet(versionSet).MapToApiVersion(new ApiVersion(1, 0))
    .MapMyWishlistsEndpoint();

app.MapGroup("/api/v{version:apiVersion}/my-memberships")
    .WithApiVersionSet(versionSet).MapToApiVersion(new ApiVersion(1, 0))
    .MapMyMembershipsEndpoint();

var heartbeats = app
    .MapGroup("/api/v{version:apiVersion}/heartbeats")
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(new ApiVersion(1, 0));

heartbeats.MapPost("/", async (CreateHeartbeatRequest request, ISender sender, CancellationToken cancellationToken) =>
    {
        var id = await sender.Send(new CreateHeartbeatCommand(request.Note), cancellationToken);
        return Results.Created($"/api/v1/heartbeats/{id}", id);
    })
    .WithName("CreateHeartbeat");

heartbeats.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
    {
        var result = await sender.Send(new GetHeartbeatsQuery(), cancellationToken);
        return Results.Ok(result);
    })
    .WithName("GetHeartbeats");

app.MapDefaultEndpoints();

// Liveness for the container orchestrator (ADR-0036 Compose healthcheck). MapDefaultEndpoints exposes
// /health and /alive only in Development for the reasons its comment gives; a deployed container still
// needs a cheap, unauthenticated liveness probe. This maps *only* the "live"-tagged checks (process is
// up) — never the full readiness set with its detail — so it is safe to expose in production.
if (!app.Environment.IsDevelopment())
{
    app.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = static r => r.Tags.Contains("live") });
}

app.Run();
