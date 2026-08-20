# Deployment

> Provider and topology are **decided** ([ADR-0036](../adr/0036-cloud-provider-and-hosting.md),
> [ADR-0037](../adr/0037-transactional-email-provider.md)); the CI/CD **pipeline** is still an open
> ADR (see the [ADR backlog](../adr/index.md)). Runbooks are executed at the deployment milestone.

Two tracks:

- **[Web / API / Database](web-api-db.md)** — Infomaniak VPS + Docker Compose (self-managed Postgres,
  Swiss Backup, Cloudflare) under the suite domain; Brevo for transactional email.
  - **[Server setup](server-setup.md)** — first-time VPS provisioning runbook (hardening, Docker,
    stack bring-up), executed on the box.
- **[Mobile apps](mobile-apps.md)** — Android (Play) & iOS (App Store): costs, enrolment,
  submission and the recurring platform deadlines.

And, before either of them, **[device testing](device-testing.md)** — which of our machines can
build each head, which devices can run it, and what to type. Nothing there needs a store account.

Both tracks are gated for *execution* on the [brand & domain](../process/naming.md); the current
day-to-day focus is local development via .NET Aspire, see [arc42 ch. 7](../arc42/07-deployment-view.md).
