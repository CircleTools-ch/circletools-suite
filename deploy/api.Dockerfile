# Main API image (ADR-0036). Build context is the repository root:
#   docker build -f deploy/api.Dockerfile -t circletools-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the whole context and publish the API project. ProjectReferences and Central Package
# Management (Directory.Packages.props) resolve from the copied tree; global.json pins the SDK band.
COPY . .
RUN dotnet publish src/Apps/Main/CircleTools.Main.Service/CircleTools.Main.Service.csproj \
    -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl backs the Compose liveness healthcheck (GET /alive). Non-chiseled aspnet image has apt.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

# Kestrel listens on plain HTTP inside the container; Caddy terminates TLS in front (ADR-0036).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Runs the API by default. The one-shot migrator service overrides this with `--migrate` (compose),
# which applies migrations and exits without starting the web host (see Main.Service/Program.cs).
ENTRYPOINT ["dotnet", "CircleTools.Main.Service.dll"]
