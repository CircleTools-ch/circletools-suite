using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.EntityFrameworkCore;
using Walf.Application;
using Walf.Main.Contracts;
using Walf.Main.Service.Core;
using Walf.Main.Service.Core.Application;
using Walf.Main.Service.Repository;

var builder = WebApplication.CreateBuilder(args);

const string corsPolicyName = "WalfCors";

// Aspire service defaults (OpenTelemetry, health checks, resilience, service discovery).
builder.AddServiceDefaults();

// Application layer (CQRS dispatcher + behaviors) and the Main module.
builder.Services.AddApplication();
builder.Services.AddMainCore();
builder.Services.AddMainRepository(
    builder.Configuration.GetConnectionString("maindb")
    ?? throw new InvalidOperationException("Connection string 'maindb' is not configured."));

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

ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

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
