# SPDX-FileCopyrightText: 2026 CircleTools contributors
# SPDX-License-Identifier: Apache-2.0

# Web image (ADR-0036 / ADR-0046): one Caddy that terminates TLS, serves both standalone WASM PWAs
# as static files, and reverse-proxies the API. Build context is the repository root:
#   docker build -f deploy/web.Dockerfile -t circletools-web .
# Multi-arch (ADR-0098): `docker buildx build --platform linux/amd64,linux/arm64 …`.
#
# Base images are pinned by digest (ADR-0098) and refreshed by Dependabot (`docker` ecosystem); the tag in
# front of the digest is for people, the digest is what Docker pulls.

# The WASM output is the same on every architecture, so the SDK stage runs once, natively, on the build
# machine ($BUILDPLATFORM); only the Caddy runtime stage differs per architecture.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0@sha256:2794448a07118a6eaf00f29ab5d2bf44928517ce5ff86505a63a69230374f599 AS build
WORKDIR /src

# Version stamped into the assemblies. The deploy workflow computes it from git (nbgv, ADR-0047) and
# passes it as a build-arg; .git is not in the build context, so ContainerBuild=true excludes the nbgv
# package and Version is supplied explicitly. Defaults to a dev marker for a plain local `docker build`.
ARG VERSION=0.0.0-dev

COPY . .

# Publish each app-type's WASM PWA to its own static root. Each app bakes its production origins from
# wwwroot/appsettings.Production.json (ApiBaseUrl, SsoAuthorityUrl, SuiteApps) — no secrets, all public.
RUN dotnet publish src/Apps/Main/CircleTools.Main.WebApp/CircleTools.Main.WebApp.csproj \
    -c Release -o /publish/main -p:ContainerBuild=true -p:Version=$VERSION
RUN dotnet publish src/Apps/SecretSanta/CircleTools.SecretSanta.WebApp/CircleTools.SecretSanta.WebApp.csproj \
    -c Release -o /publish/secretsanta -p:ContainerBuild=true -p:Version=$VERSION

FROM caddy:2.11.9-alpine@sha256:003a97808e781aa9f7e75cc2f43d34d461099031faf5e20faefb9f61126836f0 AS runtime
ARG VERSION=0.0.0-dev
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /publish/main/wwwroot /srv/main
COPY --from=build /publish/secretsanta/wwwroot /srv/secretsanta

# OCI labels: `source` links the GHCR package to the repository (ADR-0098). The commit is added by the
# workflow (`org.opencontainers.image.revision`).
LABEL org.opencontainers.image.title="CircleTools Web" \
      org.opencontainers.image.description="CircleTools web apps (hub and Secret-Santa) served by Caddy" \
      org.opencontainers.image.source="https://github.com/CircleTools-ch/circletools-suite" \
      org.opencontainers.image.licenses="Apache-2.0" \
      org.opencontainers.image.version="$VERSION"

# Still runs as root, on purpose for now: this image terminates TLS on 80/443 for the hosted service, and
# its certificates live in the root-owned `caddy_data` / `caddy_config` volumes on the server. Moving to a
# non-root user comes with the self-host plain-HTTP mode on 8080 (ADR-0095, backlog SH-2), together with a
# one-time ownership change of those volumes on our server.
# The caddy base image's entrypoint runs `caddy run --config /etc/caddy/Caddyfile`.
EXPOSE 80 443
