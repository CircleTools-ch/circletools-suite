---
status: Proposed
date: 2026-11-13
deciders: [Adrian]
tags: [deployment, self-hosting, configuration, docker, security]
---

# ADR-0095 — Self-host deployment: one domain, configuration at runtime, minimal stack

## Context and problem statement

The production stack ([ADR-0036](0036-cloud-provider-and-hosting.md), `deploy/`) is built
for **our** server:

- three origins (`circletools.ch`, `secretsanta.circletools.ch`, `api.circletools.ch`) with the apex as
  passkey RP-ID ([ADR-0046](0046-suite-packaging-and-app-composition.md));
- the web image bakes each app's `wwwroot/appsettings.Production.json` (API origin, SSO authority, suite
  apps) into the build, and the `Caddyfile` hardcodes site names and the API origin in the CSP;
- the API's CORS list, passkey domain and origins, mail branding and link origins live in its
  `appsettings.Production.json`;
- `compose.yaml` *requires* our operations stack (Brevo, Grafana alerting, ntfy, healthchecks.io) and
  Grafana/cAdvisor/node-exporter;
- the legal pages describe the CircleTools hosted service.

A self-hoster typically has **one hostname** (e.g. `santa.example.net`), often a **Raspberry Pi or NAS**,
and an **existing reverse proxy** (Traefik, Nginx Proxy Manager, Synology, Caddy) that handles TLS.

**How does someone run the suite on their own hardware, under their own domain, without rebuilding
images or editing code?**

## Decision drivers

- No rebuild for a self-hoster: the same published images run our service and theirs.
- One hostname must be enough; several subdomains stay possible (we keep ours).
- Works behind an existing reverse proxy; also works stand-alone with automatic TLS.
- Our own operations stack is optional, never required.
- Secure by default: no default secrets, nothing published that should be internal.
- The design must scale to future apps without a new topology per app.

## Considered options

1. **Single-origin mode + runtime configuration from environment variables, minimal compose file**
2. **Keep subdomains only**, document how to map them to a self-hoster's domain
3. **One all-in-one container** (API + web + Postgres in one image)

## Decision outcome

**Chosen: Option 1.**

**Two topologies, one set of images:**

| Mode | Origins | Used by |
|------|---------|---------|
| `multi-origin` | hub at the apex, one subdomain per app, API on its own subdomain | the hosted service (unchanged) |
| `single-origin` | one host: hub at `/`, each app under `/<app>/` (e.g. `/secretsanta/`), API under `/api/` | self-hosters (default in the self-host compose file) |

In `single-origin` mode all apps share one origin, so CORS is not needed, the CSP's `connect-src`
is `'self'`, and the passkey RP-ID is simply the host. Each WASM app gets a configurable base path
(`<base href>`), and the SSO hand-off ([ADR-0039](0039-cross-app-single-sign-on.md)) becomes a
same-origin navigation.

**Configuration at runtime, not at build time:**

- The **web image** generates each app's `appsettings.json` at container start from environment
  variables (e.g. `CIRCLETOOLS_PUBLIC_URL`, `CIRCLETOOLS_MODE`), and renders the CSP from the same values.
  The baked `appsettings.Production.json` files remain as the hosted defaults.
- The **API** derives CORS, passkey RP-ID and origins, mail link origins and the SSO return-uri allow-list
  from the same `CIRCLETOOLS_PUBLIC_URL` (plus optional explicit overrides). Android passkey origins for
  the official app are a documented default ([ADR-0097](0097-passkeys-and-links-on-self-hosted-servers.md)).
- Every value is documented in one configuration reference in `docs/deployment/`.

**Minimal self-host stack (`deploy/selfhost/compose.yaml`):** `postgres`, `api`, `web`. Migrations run
through the same `--migrate` step, offered as an opt-in `CIRCLETOOLS_MIGRATE_ON_START=true` for
convenience (default off for our production, where the explicit step stays). Observability (Loki,
Prometheus, Grafana) is an **optional profile**; ntfy, cAdvisor and node-exporter are not part of it.

**TLS:** the web image serves **plain HTTP on one port** by default for use behind an existing reverse
proxy; setting `CIRCLETOOLS_TLS=auto` enables Caddy's automatic Let's Encrypt for stand-alone use. The
API trusts forwarded headers only from the configured proxy network.

**Mail is optional:** with no SMTP configured, mail-sending features (invitation emails) are switched off
and the UI says so; invitations by link ([ADR-0058](0058-account-free-link-shared-groups.md)) keep
working. Any SMTP server works (not only Brevo).

**Registration policy:** `CIRCLETOOLS_REGISTRATION=open|invite|closed` (default `invite` for self-host, so a
public instance is not an open sign-up by accident); the first account created becomes the instance
admin.

**Secrets:** a `setup` command (shell script and documented `openssl rand` equivalents) generates the JWT
signing key, the share-token encryption key ([ADR-0061](0061-share-token-and-creator-key-at-rest-protection.md))
and the email HMAC key ([ADR-0062](0062-email-address-hashing-keyed-hmac.md)) into `.env`. The API keeps
failing closed when they are missing.

**Legal pages:** an operator supplies their own imprint/privacy text (Markdown via a mounted file or URL);
without it the pages say that this is a self-hosted instance and name the operator from configuration.
The CircleTools texts are shown only on the hosted service.

**Future apps** ship in the same images, enabled by `CIRCLETOOLS_APPS=secretsanta,…`. A new app means a new
image version, not a new deployment design.

## Consequences

- **Positive:** a self-hoster needs one hostname, three containers and a `.env`; the hosted service runs the
  same images with different configuration; adding an app is a config change.
- **Negative:** two topologies to test; base-path support touches routing, service worker scope and static
  asset paths in every WASM app; runtime config generation adds startup code to the web image.
- **Follow-ups:** base-path support in the WASM apps; config generation in the web image; API options
  derived from one public URL; self-host compose file and `setup` script; configurable legal pages;
  registration policy; self-hosting guide and configuration reference in `docs/deployment/`; a CI job that
  boots the self-host stack and runs an E2E smoke test against it.
- **New risks:** a misconfigured proxy (wrong forwarded headers) breaks passkeys or links — the guide covers
  the common proxies, and the API logs a clear error when the request origin does not match the configured
  public URL.

## Pros and cons of the options

### Single-origin mode + runtime config
- 👍 Fits how people self-host; no rebuild; scales to more apps.
- 👎 Base-path work in every app; two modes to keep green.

### Subdomains only
- 👍 No app changes.
- 👎 Needs wildcard DNS and certificates and a rebuild per domain — most self-hosters stop there.

### All-in-one container
- 👍 Simplest to start.
- 👎 Postgres inside an app image is hard to back up and upgrade; against container best practice.

## Links

- Related ADRs: [ADR-0036](0036-cloud-provider-and-hosting.md), [ADR-0046](0046-suite-packaging-and-app-composition.md),
  [ADR-0039](0039-cross-app-single-sign-on.md), [ADR-0021](0021-web-auth-token-model.md) (CSP),
  [ADR-0049](0049-disable-wasm-fingerprinting-for-csp.md), [ADR-0050](0050-secrets-management.md),
  [ADR-0037](0037-transactional-email-provider.md), [ADR-0051](0051-production-observability-backend.md),
  [ADR-0096](0096-client-server-selection-and-discovery.md), [ADR-0097](0097-passkeys-and-links-on-self-hosted-servers.md),
  [ADR-0098](0098-container-image-distribution.md)
- Deployment: `deploy/README.md`, [web-api-db runbook](../deployment/web-api-db.md)
- Backlog: [Open source & self-hosting](../roadmap/backlog.md#open-source--self-hosting-2026-11-13) (SH-1…SH-9)
