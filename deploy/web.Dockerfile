# Web image (ADR-0036 / ADR-0046): one Caddy that terminates TLS, serves both standalone WASM PWAs
# as static files, and reverse-proxies the API. Build context is the repository root:
#   docker build -f deploy/web.Dockerfile -t circletools-web .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .

# Publish each app-type's WASM PWA to its own static root. Each app bakes its production origins from
# wwwroot/appsettings.Production.json (ApiBaseUrl, SsoAuthorityUrl, SuiteApps) — no secrets, all public.
RUN dotnet publish src/Apps/Main/CircleTools.Main.WebApp/CircleTools.Main.WebApp.csproj \
    -c Release -o /publish/main
RUN dotnet publish src/Apps/SecretSanta/CircleTools.SecretSanta.WebApp/CircleTools.SecretSanta.WebApp.csproj \
    -c Release -o /publish/secretsanta

FROM caddy:2-alpine AS runtime
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /publish/main/wwwroot /srv/main
COPY --from=build /publish/secretsanta/wwwroot /srv/secretsanta
# The caddy base image's entrypoint runs `caddy run --config /etc/caddy/Caddyfile`.
EXPOSE 80 443
