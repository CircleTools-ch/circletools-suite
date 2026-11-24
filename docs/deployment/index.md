# Deployment

> Provider and topology are **decided** ([ADR-0036](../adr/0036-cloud-provider-and-hosting.md),
> [ADR-0037](../adr/0037-transactional-email-provider.md)); the CI/CD **pipeline** is still an open
> ADR (see the [ADR backlog](../adr/index.md)). Runbooks are executed at the deployment milestone.

Two tracks:

- **[Web / API / Database](web-api-db.md)** — Infomaniak VPS + Docker Compose (self-managed Postgres,
  Swiss Backup, Infomaniak DNS, Caddy TLS) under the suite domain; Brevo for transactional email.
  - **[Server setup](server-setup.md)** — first-time VPS provisioning runbook (hardening, Docker,
    stack bring-up), executed on the box.
  - **[Container images](container-images.md)** — the API and web images: amd64 + arm64, tags
    (`:edge`, `:stable`), making a server release, verifying signatures ([ADR-0098](../adr/0098-container-image-distribution.md)).
  - **[Backups & restore](backups.md)** — nightly `pg_dump` → Swiss Backup via restic, and the
    (safe) restore drill that keeps GL-5's "restore tested" honest.
  - **[Operator read-only DB access](operator-db-read-access.md)** — a `SELECT`-only role reached over an
    SSH tunnel for inspecting production data, with the ADR-0020 privacy caveat (ADR-0075).
- **[Mobile apps](mobile-apps.md)** — Android (Play) & iOS (App Store): costs, enrolment,
  submission and the recurring platform deadlines.

And, before either of them, **[device testing](device-testing.md)** — which of our machines can
build each head, which devices can run it, and what to type. Nothing there needs a store account.

The **[documentation site](docs-site.md)** is this DocFX site itself, published to **public** GitHub
Pages ([ADR-0093](../adr/0093-publish-the-repository.md), superseding ADR-0076) — a one-time owner
enablement, after which every push to `main` republishes it.

For the observability the VM will run, **[local observability](dev-observability.md)** brings up the
same Loki + Prometheus + Grafana stack under Aspire — with the production dashboards and datasources —
so you can validate what you'll see on the box **before** deploying it.

Across both tracks, the **[launch-readiness assessment](launch-readiness.md)** (2026-09-15) is the
point-in-time verdict on whether the suite can go live — what is production-grade already and the
operational, legal and pre-data items that still gate a first deploy.

The **[production-release plan](production-release-plan.md)** (2026-10-20) takes it from there to a
public 1.0 in both app stores: the Play closed-test clock, the Apple setup, dependency updates, the full
review and the store listings — as parallel tracks, in order. Its last mile, click by click, is the
**[store submission guide](store-submission-guide.md)** (2026-10-28): TestFlight test information, the
App Store record, the Play declarations, screenshots, and the production applications.

Both tracks are gated for *execution* on the [brand & domain](../process/naming.md); the current
day-to-day focus is local development via .NET Aspire, see [arc42 ch. 7](../arc42/07-deployment-view.md).
