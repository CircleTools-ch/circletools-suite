# Deployment

> _Placeholder / to be detailed_ — concrete provider and pipelines are pending ADRs
> (see the [ADR backlog](../adr/index.md)). Runbooks are filled in at the deployment milestone.

Two tracks:

- **[Web / API / Database](web-api-db.md)** — cloud hosting under the suite domain.
- **[Mobile apps](mobile-apps.md)** — Android (Play) & iOS (App Store): costs, enrolment,
  submission and the recurring platform deadlines.

And, before either of them, **[device testing](device-testing.md)** — which of our machines can
build each head, which devices can run it, and what to type. Nothing there needs a store account.

The web/API/database track is still a placeholder pending the cloud-provider ADR; the current focus
is local development via .NET Aspire, see [arc42 ch. 7](../arc42/07-deployment-view.md).
