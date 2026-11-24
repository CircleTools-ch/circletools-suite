# SPDX-FileCopyrightText: 2026 CircleTools contributors
# SPDX-License-Identifier: Apache-2.0

# Main API image (ADR-0036). Build context is the repository root:
#   docker build -f deploy/api.Dockerfile -t circletools-api .
# Multi-arch (ADR-0098): `docker buildx build --platform linux/amd64,linux/arm64 …`.
#
# Base images are pinned by digest (ADR-0098) and refreshed by Dependabot (`docker` ecosystem); the tag in
# front of the digest is for people, the digest is what Docker pulls.

# The SDK stage always runs on the build machine's own architecture ($BUILDPLATFORM), never under
# emulation: the API is published framework-dependent without a runtime identifier, so its output is
# portable IL that runs on amd64 and arm64 alike. Only the runtime stage below differs per architecture.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4 AS build
WORKDIR /src

# Version stamped into the assemblies. The deploy workflow computes it from git (nbgv, ADR-0047) and
# passes it as a build-arg; .git is not in the build context, so ContainerBuild=true excludes the nbgv
# package and Version is supplied explicitly. Defaults to a dev marker for a plain local `docker build`.
ARG VERSION=0.0.0-dev

# Copy the whole context and publish the API project. ProjectReferences and Central Package
# Management (Directory.Packages.props) resolve from the copied tree; global.json pins the SDK band.
# UseAppHost=false: no native launcher for the build machine's architecture — the image starts the API
# with `dotnet <dll>`, which works on every architecture.
COPY . .
RUN dotnet publish src/Apps/Main/CircleTools.Main.Service/CircleTools.Main.Service.csproj \
    -c Release -o /app -p:ContainerBuild=true -p:Version=$VERSION -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:12b1135f5f8b3c0a214f2a680ff2033dff4f684f445d6c697d9524ceeb303586 AS runtime
ARG VERSION=0.0.0-dev
# curl backs the Compose liveness healthcheck (GET /alive). Non-chiseled aspnet image has apt.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

# OCI labels: `source` links the GHCR package to the repository (ADR-0098); the rest is shown by registries
# and `docker inspect`. The commit is added by the workflow (`org.opencontainers.image.revision`).
LABEL org.opencontainers.image.title="CircleTools API" \
      org.opencontainers.image.description="CircleTools API server (accounts, circles, sync, Secret-Santa)" \
      org.opencontainers.image.source="https://github.com/CircleTools-ch/circletools-suite" \
      org.opencontainers.image.licenses="Apache-2.0" \
      org.opencontainers.image.version="$VERSION"

# Kestrel listens on plain HTTP inside the container; Caddy terminates TLS in front (ADR-0036).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the aspnet image's built-in non-root `app` user (ADR-0098). 8080 needs no privilege, and the API
# writes nothing to the container's file system.
USER $APP_UID

# Runs the API by default. The one-shot migrator service overrides this with `--migrate` (compose),
# which applies migrations and exits without starting the web host (see Main.Service/Program.cs).
ENTRYPOINT ["dotnet", "CircleTools.Main.Service.dll"]
