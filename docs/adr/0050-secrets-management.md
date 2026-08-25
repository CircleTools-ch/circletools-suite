---
status: Accepted
date: 2026-08-25
deciders: [Adrian]
tags: [security, secrets, deployment, ci-cd, operations, github, docker]
---

# ADR-0050 — Secrets management (host + CI store)

## Context and problem statement

Three deployment ADRs left the **same** follow-up open: [ADR-0036](0036-cloud-provider-and-hosting.md)
(cloud provider), [ADR-0038](0038-ci-cd-pipeline.md) (CI/CD), and
[ADR-0047](0047-versioning-and-release-strategy.md) each name **secrets management** as a decision to be
recorded "at M8". Milestone 8 (deployment) is the active milestone, and the deploy scaffold now exists —
`deploy/compose.yaml`, `deploy/.env.example`, and `.github/workflows/deploy.yml` — so the question comes
due before a VPS can be provisioned.

The scaffold already reads secrets from **two distinct places**, so the decision is not "invent a store"
but "ratify where each tier durably lives, how it is provisioned, recovered, and rotated":

| Tier | Consumer | Secrets today | Read from |
|------|----------|---------------|-----------|
| **Runtime / host** | `compose.yaml` → `api`, `migrator`, `postgres`, `grafana` | `POSTGRES_USER`, `POSTGRES_PASSWORD`, `ConnectionStrings__maindb`, `Auth__Jwt__SigningKey`, `Mail__Username`, `Mail__Password`, `GRAFANA_ADMIN_PASSWORD` ([ADR-0051](0051-production-observability-backend.md)) | git-ignored `deploy/.env` on the VM |
| **CI / deploy** | `.github/workflows/deploy.yml` (SSH rollout) | `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS`; `GITHUB_TOKEN` (built-in, GHCR push) | GitHub Actions encrypted secrets |
| **Mobile signing** (future) | `release-mobile.yml` | Play / App Store Connect signing + API keys | not yet wired — [ADR-0030](0030-mobile-store-publisher-identity.md) follow-up |

Two hard constraints frame the answer. First, the
[security & privacy guidelines](../guidelines/security-privacy-guidelines.md) §2 forbid **any secret in
the repo** — `.gitignore` excludes `secrets.json`, `.env*`, and `appsettings.*.local.json`. Second,
`CLAUDE.md` rule 9 wants all **knowledge** in the repo under git — which this ADR satisfies by recording
the decision and the inventory *of which secrets exist*; the secret **values** are explicitly not
knowledge and stay out of git.

So: **where do the runtime and CI secret values durably live, how are they recovered if the single VM is
lost, and how are they rotated — proportionately to a ~CHF 12/mo single-VPS, seasonal, single-maintainer
service?**

## Decision drivers

- **No plaintext secret in the repo, ever** — the security guideline's non-negotiable.
- **Proportionate** — the same "don't over-engineer a tiny seasonal single-service load" test that
  rejected Kubernetes and hyperscalers in [ADR-0036](0036-cloud-provider-and-hosting.md). No new paid
  service, no daemon to babysit.
- **Recoverable** — the VM is a single point of failure ([ADR-0036](0036-cloud-provider-and-hosting.md)
  documents single-VM recovery); losing the box must not lose the only copy of the secrets.
- **Rotatable** — every secret must have a written "how to change it" path; a leaked JWT signing key or
  DB password must be replaceable without archaeology.
- **Least exposure** — CI holds only what it needs (SSH access), never the app's runtime secrets; the DB
  is never published off the VM's internal Docker network.
- **Works with the scaffold as built** — `compose.yaml` reads `deploy/.env`; `deploy.yml` reads GitHub
  Actions secrets. The decision should ratify and document this, not force a rewrite.
- **Single maintainer today, room for a second** — no per-user access control needed now, but the choice
  should not paint us into a corner if the team grows.

## Considered options

1. **Two-tier native stores (formalize the scaffold): GitHub Actions encrypted secrets for CI/deploy +
   a git-ignored `deploy/.env` on the host for runtime, with the authoritative copy of every value held
   in the maintainer's password manager for recovery and rotation.**
2. **SOPS + age: encrypted secrets committed to the repo**, decrypted on the host / in CI with a private
   key held out-of-band. Single versioned source of truth in git.
3. **A dedicated secrets manager** — HashiCorp Vault, Infisical, Doppler, or Bitwarden Secrets Manager —
   as the runtime + CI source, injected at deploy time.
4. **Cloud-provider secret store / KMS** (e.g. Infomaniak or an external KMS) as the backing store.

## Decision outcome

**Chosen: Option 1 — two-tier native stores, with a password manager as the authoritative off-box copy;
SOPS + age recorded as the trigger-gated upgrade path.**

Concretely:

### Runtime / host secrets → git-ignored `deploy/.env` on the VM

`deploy/.env` (copied from the committed `deploy/.env.example` template) lives only on the VPS at
`/opt/circletools/.env`, `chmod 600`, owned by the deploy user. `compose.yaml` already injects it into
`api`/`migrator`/`postgres`; no secret is baked into any image (the Dockerfiles take only the public
`VERSION` build-arg). Postgres is **not** published off the internal Docker network, so the DB
credentials never leave the box. This is exactly what the scaffold does — the decision is to keep it and
make its provisioning and recovery explicit (below), not to add a layer.

### CI / deploy secrets → GitHub Actions encrypted secrets

`deploy.yml` reads `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS` from repository
encrypted secrets; GHCR push uses the built-in `GITHUB_TOKEN`. CI holds **only deploy access**, never the
app's runtime secrets — a compromised workflow can roll the box, but cannot read the DB password or JWT
key because they are not there. The SSH key is a **dedicated deploy key** with docker-group access on the
VM, revocable independently of the maintainer's own key. `DEPLOY_KNOWN_HOSTS` pins the host key (already
in the scaffold), preventing a MITM on the rollout.

### Authoritative copy & recovery → the maintainer's password manager

The single source of truth for every secret **value** is an entry in the maintainer's password manager
(the one already used for the Infomaniak / GitHub / store logins). `deploy/.env` on the VM and the GitHub
Actions secrets are **derived copies**, re-created from the password manager. This closes the single-VM
recovery gap in [ADR-0036](0036-cloud-provider-and-hosting.md): rebuild the box → restore the Postgres
dump → re-create `deploy/.env` from the vault → `compose up`. It costs nothing, needs no new service, and
keeps zero plaintext in git — consistent with the security guideline while giving rule 9 its due (the
*record* — this ADR and `.env.example` — is in git; the *values* are not knowledge).

### Rotation

Each secret gets a one-line rotation path, recorded in the deployment runbook: **DB password** — change
in the vault, update `deploy/.env`, `docker compose up -d` (Postgres re-reads on restart; connection
string updated in lockstep). **JWT signing key** ([ADR-0021](0021-web-auth-token-model.md)) — rotating it
invalidates live sessions by design; do it on suspected compromise and accept the re-sign-in. **Brevo
SMTP key** ([ADR-0037](0037-transactional-email-provider.md)) — reissue in Brevo, update `.env`.
**Deploy SSH key** — regenerate, update the GitHub secret and the VM's `authorized_keys`.

### Why not the alternatives (now)

- **SOPS + age (Option 2)** is genuinely attractive — an *encrypted* blob in git is versioned, is a
  single source of truth, and survives VM loss, all while honoring "no *plaintext* secret in the repo."
  But it adds tooling (the `sops` + `age` binaries on the host and in CI, a decryption step in the deploy
  path, and its own key to guard) for a benefit — DR + versioning — that a password manager already
  provides for free at this scale. It is the **right upgrade** the moment its extra machinery pays off,
  so it is recorded as the explicit upgrade path below rather than adopted speculatively (mirroring how
  [ADR-0036](0036-cloud-provider-and-hosting.md) records managed Postgres as a trigger-gated upgrade).
- **A dedicated secrets manager (Option 3)** — Vault/Infisical/Doppler — is real operational overhead
  (a service to run and back up, or a paid SaaS + a new third party in the trust boundary) that is
  disproportionate to a handful of secrets on one box. Same proportionality test ADR-0036 applied to
  Kubernetes.
- **Cloud/KMS store (Option 4)** adds a provider dependency and API wiring for no benefit over the
  password manager at this size, and would put another party between us and the values.

## Consequences

- **Positive:** no new service, no new cost, no plaintext in git; the choice ratifies the scaffold as
  built, so nothing is rewritten. CI's blast radius is limited to deploy access. Recovery and rotation
  become documented, tested steps rather than tribal knowledge.
- **Positive:** the trust boundary is small and legible — runtime secrets live only on the VM; CI holds
  only SSH; the DB is unreachable from outside the Docker network.
- **Negative / accepted:** the password manager is an **out-of-repo** store of the authoritative values.
  This does **not** violate rule 9 (credentials are not project knowledge, and the security guideline
  positively requires them out of git) — but it means "restore from the vault" is a manual human step,
  not an automated pull.
- **Negative / accepted:** `deploy/.env` is plaintext **on the VM**. Its protection is filesystem
  permissions + the VM's own hardening, not encryption at rest. Acceptable for a single-tenant box we
  control; the SOPS path below removes even this if it ever feels too loose.
- **Negative / accepted:** rotation is manual (edit vault → edit `.env` / GitHub secret → redeploy). Fine
  at this cadence; automation is not worth its weight yet.
- **Follow-ups:**
  - Add a **"Secrets & recovery"** section to [`docs/deployment/web-api-db.md`](../deployment/web-api-db.md)
    with the provisioning order, the per-secret rotation table above, and the full single-VM recovery
    drill (rebuild → restore dump → recreate `.env` → `up`).
  - Generate real values at provisioning: `openssl rand -base64 48` for `Auth__Jwt__SigningKey`, a strong
    random `POSTGRES_PASSWORD`, a dedicated deploy SSH keypair; store each in the vault first, then derive
    `.env` / GitHub secrets.
  - When the **mobile store upload** legs are wired ([ADR-0030](0030-mobile-store-publisher-identity.md),
    [ADR-0047](0047-versioning-and-release-strategy.md)), their signing/API keys join the **CI tier**
    (GitHub Actions secrets) under this same policy — this ADR already covers them.
  - Flip the "secrets management" checklist item and the `[~]` note in the deploy docs to done **once this
    ADR is Accepted** and the VM `.env` is provisioned.
- **New risks:** the password manager and the deploy SSH key become high-value targets; both must have
  strong auth (MFA on the vault; a passphrase on the deploy key or a hardware-backed key). A leaked
  `deploy/.env` exposes DB + JWT + mail at once — hence Postgres stays off any public port and the JWT
  rotation path is written down.

## Upgrade path — SOPS + age (trigger-gated)

Adopt **SOPS with age** when any of these becomes true, and record it as a superseding ADR:

- a **second maintainer** joins (per-user decryptable secrets in git beat sharing a vault export), or
- the **secret count or rotation cadence** grows enough that "edit the vault, edit `.env`" is error-prone,
  or
- we want **plaintext-at-rest gone** from the VM entirely.

The migration is small: `sops`-encrypt a `deploy/secrets.enc.yaml` (a filename outside the `.env*`
ignore, safe to commit *because it is ciphertext*), hold the age private key in the vault + as a CI
secret, and add a decrypt step to the deploy path. The runtime and CI tiers and the trust boundary stay
the same; only the *source* of the values moves from the vault into (encrypted) git.

## Pros and cons of the options

### Option 1 — Two-tier native + password manager (chosen)
- 👍 Zero new cost/infra; ratifies the scaffold as built; no plaintext in git.
- 👍 Small, legible trust boundary; CI can't read runtime secrets; free, real DR via the vault.
- 👎 Authoritative copy is out-of-repo (a password manager); recovery and rotation are manual.
- 👎 `.env` is plaintext on the VM (protected by permissions, not encryption).

### Option 2 — SOPS + age in the repo
- 👍 Single **versioned** source of truth in git; survives VM loss; no plaintext at rest anywhere.
- 👍 Per-user keys scale cleanly to more maintainers.
- 👎 Extra tooling (`sops`/`age`) on host + CI and a decrypt step, plus an age key to guard — overhead a
  password manager already covers for free at this scale. **Chosen as the upgrade path, not now.**

### Option 3 — Dedicated secrets manager (Vault / Infisical / Doppler)
- 👍 Dynamic secrets, audit, fine-grained access, rotation tooling.
- 👎 A service to run + back up (or a paid SaaS + a new third party in the trust boundary);
  disproportionate to a handful of secrets on one box.

### Option 4 — Cloud/KMS store
- 👍 Managed, durable backing store.
- 👎 Provider lock-in + API wiring for no gain over the vault at this size; another party between us and
  the values.

## Links

- Related ADRs: [ADR-0036](0036-cloud-provider-and-hosting.md) (provider/topology — raised this
  follow-up; single-VM recovery), [ADR-0038](0038-ci-cd-pipeline.md) (CI/CD — the SSH deploy secrets),
  [ADR-0047](0047-versioning-and-release-strategy.md) (versioning/release — deferred this to M8),
  [ADR-0021](0021-web-auth-token-model.md) (JWT signing key), [ADR-0037](0037-transactional-email-provider.md)
  (Brevo SMTP key), [ADR-0030](0030-mobile-store-publisher-identity.md) (future store-signing secrets)
- Guidelines: [security & privacy](../guidelines/security-privacy-guidelines.md) §2 (no secrets in the repo)
- arc42 chapters: [07 Deployment view](../arc42/07-deployment-view.md),
  [08 Crosscutting concepts](../arc42/08-crosscutting-concepts.md),
  [11 Risks & technical debt](../arc42/11-risks-and-technical-debt.md)
- Runbooks: [`docs/deployment/web-api-db.md`](../deployment/web-api-db.md),
  [`deploy/README.md`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)
- Roadmap: [milestone 8](../roadmap/roadmap.md)
</content>
</invoke>
