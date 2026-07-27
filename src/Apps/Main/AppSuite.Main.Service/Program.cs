using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Asp.Versioning.Builder;
using AppSuite.Identity.Tokens;
using AppSuite.Main.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using AppSuite.Application;
using AppSuite.Main.Contracts;
using AppSuite.Main.Service.Core;
using AppSuite.Main.Service.Core.Application;
using AppSuite.Main.Service.Repository;

var builder = WebApplication.CreateBuilder(args);

const string corsPolicyName = "AppSuiteCors";

// Aspire service defaults (OpenTelemetry, health checks, resilience, service discovery).
builder.AddServiceDefaults();

// Application layer (CQRS dispatcher + behaviors) and the Main module.
builder.Services.AddApplication();
builder.Services.AddMainCore();
builder.Services.AddMainRepository(
    builder.Configuration.GetConnectionString("maindb")
    ?? throw new InvalidOperationException("Connection string 'maindb' is not configured."));

// Identity, sessions and bearer-token validation — ADR-0009, ADR-0021.
// AddDefaultTokenProviders must be applied here: it lives in the ASP.NET Core framework
// assembly, and the email-confirmation and password-reset tokens depend on it.
builder.Services.AddAppSuiteIdentityCore().AddDefaultTokenProviders();
builder.Services.AddAppSuiteSessions(builder.Configuration);
builder.Services.AddAppSuiteMail(builder.Configuration);

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
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(corsPolicyName, policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Apply pending migrations on startup in Development.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
    db.Database.Migrate();
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

app.Run();
