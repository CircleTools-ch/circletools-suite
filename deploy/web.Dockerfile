# SPDX-FileCopyrightText: 2026 CircleTools contributors
# SPDX-License-Identifier: Apache-2.0

# Web image (ADR-0036 / ADR-0046): one Caddy that terminates TLS, serves both standalone WASM PWAs
# as static files, and reverse-proxies the API. Build context is the repository root:
#   docker build -f deploy/web.Dockerfile -t circletools-web .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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

FROM caddy:2-alpine AS runtime
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /publish/main/wwwroot /srv/main
COPY --from=build /publish/secretsanta/wwwroot /srv/secretsanta
# The caddy base image's entrypoint runs `caddy run --config /etc/caddy/Caddyfile`.
EXPOSE 80 443
