# Deployment — Web / API / Database

> _Placeholder._ To be completed at the deployment milestone.

## Targets
- **Web app** (Blazor) and **Web API** hosted as containers behind HTTPS on the suite domain.
- Apps served as **subdomains**: `<app>.<suite>.ch`.
- **Managed PostgreSQL** with Swiss data residency (provider TBD via ADR).
- **Docs site** (DocFX `_site`) published to a static host / Pages.

## Open decisions (ADRs)
- Cloud provider & regions; CI/CD pipeline; secrets management; migration strategy; backup/restore;
  observability backend.

## Checklist (to build out)
- [ ] Provision infra (Aspire deployment model)
- [ ] Configure environments & secrets
- [ ] Run database migrations
- [ ] Deploy & smoke-test
- [ ] Monitoring & rollback documented
