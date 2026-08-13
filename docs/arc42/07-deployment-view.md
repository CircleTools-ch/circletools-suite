# 7. Deployment View

> The cloud provider and topology are **decided** in
> [ADR-0036](../adr/0036-cloud-provider-and-hosting.md) (Infomaniak VPS + Docker Compose), the
> transactional email sender in [ADR-0037](../adr/0037-transactional-email-provider.md), and the CI/CD
> **pipeline** in [ADR-0038](../adr/0038-ci-cd-pipeline.md) (GitHub Actions — the CI gate is built; CD
> to the VPS lands at milestone 8). This chapter captures the shape.

## 7.1 Environments

| Environment | Purpose |
|-------------|---------|
| Local (dev) | Aspire AppHost runs API + web + Postgres containers; dashboard for observability. |
| Cloud (later) | Managed hosting for Web/API/DB under a `.ch`/`.com` domain; apps live as subdomains (`<app>.<suite-domain>`). |
| App stores | Signed Android (Play) & iOS (App Store) artifacts, localized listings (DE/EN/FR). |

## 7.2 Local development (today)

> **Implemented (M5):** `dotnet run --project src/Aspire/CircleTools.AppHost` starts the Aspire dashboard,
> a `postgres` container, the `main-api` service, and the `main-web` WASM PWA. Verified working.

```mermaid
flowchart LR
    Dev[Developer] --> AppHost[CircleTools.AppHost]
    AppHost --> API[Service hosts]
    AppHost --> Web[WebApp hosts]
    AppHost --> PG[(PostgreSQL container)]
    AppHost --> Dash[Aspire dashboard - OTel]
```

## 7.3 Cloud (decided — ADR-0036 / ADR-0037)

- **Web + API:** a single **Infomaniak VPS** (Swiss) running **Docker Compose** — reverse proxy
  (Caddy/Traefik, Let's Encrypt TLS) serving the static WASM PWA, plus the API container. **Cloudflare
  Free** at the edge (DNS, TLS, CDN, DDoS/WAF).
- **Database:** **PostgreSQL as a container** on the same VPS (self-managed), with a **nightly `pg_dump`
  to Infomaniak Swiss Backup** offsite; managed Postgres is the documented upgrade path.
- **Transactional email:** **Brevo** (EU) behind the invitation outbox port
  ([ADR-0037](../adr/0037-transactional-email-provider.md)).
- **Static docs:** the DocFX site published to a static host / Pages.
- Data at rest stays in Switzerland; recurring cost ≈ CHF 12–14/mo. **CI** runs on GitHub Actions
  ([ADR-0038](../adr/0038-ci-cd-pipeline.md)); the **CD** step to this VPS is built at milestone 8 once
  it is provisioned (SSH → `compose pull && up`).

## 7.4 App stores

The native heads deploy through a second, quite different channel: two store review processes rather
than infrastructure we control. Two properties of it shape the architecture rather than just the
release process.

**The build host is a hardware constraint, not a tooling choice.** iOS artifacts can only be produced
on macOS with Apple's toolchain, and the required Xcode version implies a required macOS version,
which implies required hardware — so the toolchain floor propagates all the way down to which Mac
must exist. Settled in [ADR-0031](../adr/0031-ios-build-host.md).

**Some identifiers are permanent.** The bundle identifier / `ApplicationId` cannot change after the
first release, and passkey relying-party IDs do not migrate
([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)). Both derive from the suite domain,
which makes [naming](../process/naming.md) a prerequisite of first submission rather than a cosmetic
step.

Publisher identity is decided in [ADR-0030](../adr/0030-mobile-store-publisher-identity.md); the
recurring platform obligations are stated as
[NFR-007](../requirements/non-functional/NFR-007-store-distributability.md); the procedure lives in
the [mobile-apps runbook](../deployment/mobile-apps.md), and what can be tested on which machine
today in [device testing](../deployment/device-testing.md).
