---
status: Accepted
date: 2026-10-13
deciders: [Adrian]
tags: [security, privacy, operations, database, postgres, deployment]
---

# ADR-0075 — Operator read-only access to the production database

## Context and problem statement

Production data lives in one self-managed **PostgreSQL 17** container (`maindb`) on the Infomaniak VPS
([ADR-0036](0036-cloud-provider-and-hosting.md)). The container **publishes no port** — it is reachable
only on the internal Docker network — and migrations are applied by a one-shot `migrator` service, never
by a human. There is today **no defined way for the operator to inspect what data exists** (debugging a
support question, confirming a migration landed, sanity-checking a release) other than using the
application's own full-privilege connection string.

Two constraints make "just connect with the app credentials" wrong. First, that role is read-write and
DDL-capable — a slip could mutate or drop production data. Second, and more importantly,
[ADR-0020](0020-server-side-assignment-privacy.md) records that in account mode the server stores draw
results (giver→recipient pairings) and names **in plaintext**, protected only by application authorization:
*"the operator can technically read every giver→recipient pairing, as can anyone with database or backup
access."* PII also sits in `AspNetUsers` and transiently on `circles.invitations`. So any raw DB access
exposes the single most sensitive data in the product, and the grant must be deliberate, least-privilege,
revocable, and kept off the public surface.

For the common question — *"how much data exists / how fast is it growing?"* — a safe aggregate answer
already exists: the business-metrics dashboard ([FEAT-022](../features/FEAT-022-business-metrics-dashboard.md))
on the existing Grafana stack ([ADR-0051](0051-production-observability-backend.md)), which emits PII-free
counts only. This ADR is about the **remaining** need: occasional row-level inspection.

**How should the operator get read-only access to production data securely, without opening a new public
attack surface or handing out write-capable credentials?**

## Decision drivers

- **Least privilege** — read-only, no DDL, no write; scoped and revocable; not the app/superuser role.
- **No new public attack surface** — preserve ADR-0036's "DB has no published port"; nothing listening on
  the internet in front of the most sensitive data in the suite.
- **Privacy / residency** — exposure of pairings + PII limited to the already-trusted operator
  (ADR-0020, [security-privacy-guidelines §5](../guidelines/security-privacy-guidelines.md), NFR-004);
  routine use should prefer aggregates (FEAT-022) over raw rows.
- **Off-repo secrets** — any credential handled per [ADR-0050](0050-secrets-management.md): VM `.env` +
  password manager, never committed.
- **Low operator overhead** — a solo maintainer; no heavy IAM machinery.
- **Auditable & reversible** — the grant can be rotated or dropped in one step.

## Considered options

1. **SSH tunnel + a dedicated read-only Postgres role.** Reach the VM over SSH, port-forward to the
   internal Postgres container, and connect a local client (`psql`/DBeaver/pgAdmin) as a
   `circletools_readonly` login role with `SELECT`-only privileges. No published DB port.
2. **A web-based DB UI** (Adminer / CloudBeaver / pgweb) behind Caddy + auth on a subdomain
   (e.g. `db.circletools.ch`), like the Grafana vhost.
3. **Reuse the application's connection string** over the tunnel.
4. **No direct DB access at all** — rely solely on the FEAT-022 aggregates, and for a deep dive restore a
   `pg_dump`/restic backup into a throwaway local database (the restore drill already exists,
   `deploy/restore.sh`).

## Decision outcome

**Chosen: option 1 — SSH tunnel + a dedicated read-only role — with option 4 as the preferred first
resort.** Concretely:

- A login role **`circletools_readonly`**: `GRANT CONNECT` on `maindb`, `USAGE` on each schema
  (`public`, `circles`, `sync`, `linking`, `shared`), and `SELECT` on their tables, plus `ALTER DEFAULT
  PRIVILEGES` so new tables created by future migrations are covered. **No** `INSERT`/`UPDATE`/`DELETE`,
  no DDL, not a superuser, `NOSUPERUSER NOCREATEDB NOCREATEROLE`.
- Access path: `ssh` to the VM, then a local port-forward onto the Postgres container; the DB port stays
  **unpublished** (ADR-0036 preserved). No DB listener on the internet.
- The role password lives in the VM `.env` and the operator's password manager (ADR-0050); it is never
  committed and can be rotated or the role dropped at any time.
- **Routine "how much data" questions go to the FEAT-022 Grafana board, not the DB.** Raw read access is
  reserved for genuine row-level debugging, and the operator is reminded (in `deploy/README.md`) that using
  it means looking at plaintext pairings + PII (ADR-0020).
- Bootstrap is an **idempotent SQL script** committed under `deploy/sql/` (role + grants + default
  privileges; password supplied from the environment, not in the file), applied **once by the operator** —
  not by the automatic migrator, because creating a role with a secret is an operator action.

This is the only option that satisfies every driver at once: least-privilege and read-only (unlike
option 3), no new public surface (unlike option 2), and an immediate answer for live inspection (unlike
option 4 alone). Option 4 is kept as the **first** thing to try because it exposes nothing live — the
aggregates answer most questions, and a restored dump is a throwaway copy — so the standing read-only grant
is used only when row-level truth on live data is actually needed.

Column-level masking (read-only **views** that omit the sensitive pairing/PII columns, granting the role
`SELECT` only on those) is **not** adopted now: the sole grantee is the operator, who is already the
ADR-0020 trusted party, so masking adds maintenance for no gain today. It is recorded as the mechanism to
adopt the moment a *second, less-trusted* person needs access (see follow-ups).

## Consequences

- **Positive:** the operator can inspect production safely; read-only means a slip cannot mutate data; no
  new internet-facing listener; the grant is least-privilege, auditable, and revocable in one statement;
  FEAT-022 covers the common case without touching raw rows.
- **Negative / accepted:** the role still exposes plaintext pairings + PII to whoever holds it — inherent
  to the ADR-0020 plaintext model, mitigated by reserving its use and preferring aggregates/views;
  bootstrap is a manual operator step; using it requires SSH access to the VM.
- **Follow-ups:**
  1. ~~Add the idempotent bootstrap script and a setup runbook.~~ **Done:** `deploy/sql/readonly-role.sql`
     and [docs/deployment/operator-db-read-access.md](../deployment/operator-db-read-access.md) (the
     `ssh -L` recipe + the privacy reminder). Executed on the box at the deployment milestone.
  2. Provide **masked read-only views** (excluding `linking.assignments` pairings, the `shared.groups` /
     `shared.reveal_slices` and `sync.aggregates` jsonb bodies, `circles.invitations.email`, and
     `AspNetUsers` email/hash columns) and a view-only role **if/when** a less-trusted grantee appears.
  3. Revisit scope if ADR-0020's parked P3 (per-member envelope encryption of assignments) lands — it would
     shrink what raw access can reveal.
  4. If the backup restore target (option 4) is used routinely, apply the same role there.
- **New risks:** a leaked `circletools_readonly` password exposes read access to all production data —
  mitigated by off-repo storage (ADR-0050), rotation, and that the role cannot write; SSH to the VM remains
  the gate.

## Pros and cons of the options

### 1. SSH tunnel + dedicated read-only role (chosen)
- 👍 Read-only & least-privilege; no public DB port (ADR-0036 preserved); revocable; standard tooling;
  off-repo secret (ADR-0050).
- 👎 Manual one-time bootstrap; needs SSH access; still sees plaintext pairings/PII per ADR-0020.

### 2. Web DB UI behind Caddy + auth
- 👍 Convenient; no local client or tunnel; same vhost pattern as Grafana.
- 👎 Puts an internet-facing, credential-guarded surface in front of the most sensitive data in the suite —
  a materially larger attack surface for a solo operator who can already SSH; rejected.

### 3. Reuse the application connection string
- 👍 Nothing new to create.
- 👎 Read-write + DDL (a slip mutates/drops prod data); not scoped, not separately revocable, not auditable
  as "read access"; violates least privilege; rejected.

### 4. No direct access — aggregates + restored backup for deep dives
- 👍 Exposes nothing live; strongest privacy posture; reuses the existing restore drill.
- 👎 Cannot answer a live row-level question quickly (a restore is a copy, possibly stale); kept as the
  preferred *first* resort and complement, not the sole mechanism.

## Links

- Related ADRs: [ADR-0036](0036-cloud-provider-and-hosting.md) (hosting; DB has no published port),
  [ADR-0050](0050-secrets-management.md) (where the role password lives),
  [ADR-0020](0020-server-side-assignment-privacy.md) (why raw access is sensitive — operator-readable
  plaintext), [ADR-0022](0022-account-deletion-and-data-export.md) (data obligations),
  [ADR-0051](0051-production-observability-backend.md) (the Grafana stack behind the aggregate alternative)
- Feature: [FEAT-022](../features/FEAT-022-business-metrics-dashboard.md) (PII-free aggregates — the
  routine-use alternative to raw access)
- Runbook: [docs/deployment/operator-db-read-access.md](../deployment/operator-db-read-access.md) (setup
  and connection steps)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md),
  [11 Risks & technical debt](../arc42/11-risks-and-technical-debt.md)
- Guideline: [security-privacy-guidelines.md](../guidelines/security-privacy-guidelines.md); requirement
  NFR-004 (data minimisation)
