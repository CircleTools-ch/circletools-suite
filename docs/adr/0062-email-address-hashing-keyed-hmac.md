---
status: Accepted
date: 2026-09-15
deciders: [Adrian]
tags: [security, privacy, hashing, circles, persistence]
---

# ADR-0062 — Email address hashing with a keyed HMAC

## Context and problem statement

The Circles context reduces an email address to a hash wherever it does not need the address itself
([ADR-0018](0018-circle-and-sharing-model.md), FEAT-003): a **decline record** remembers a declined
address for a cooldown without keeping the address (NFR-004); an **invitation** is matched to the
caller's verified address by comparing hashes, never plaintext; the per-recipient **rate-limit** key is
the hash. As first built, that hash was an **unsalted SHA-256** of the upper-invariant address.

A pre-production review flagged this: the email address space is small and enumerable, so an *unsalted*
digest is **reversible by dictionary or rainbow-table attack**. For the rows whose whole purpose is to
hold *only* a hash and no address (`invitation_declines`, `invitation_attempts`), a reversible hash gives
away exactly what the design set out to protect. Because the algorithm choice fixes the value of every
stored hash, it is a **one-way door** — it must be settled before real data accumulates.

The constraint that rules out the textbook answer (a per-value salt) is that these hashes are **compared
for equality across rows and requests**: "is this the invited address?", "is this address in cooldown?",
"has this recipient been invited too often?". A random per-address salt would make equal addresses hash
differently, breaking every one of those lookups.

## Decision drivers

- **Not reversible from a database dump** — a stored hash must not be dictionary-attackable to recover the
  address.
- **Deterministic equality preserved** — the same address must always yield the same value, or invitation
  matching, decline cooldown, and the rate-limit key all break.
- **The domain stays pure** — the Circles aggregate has no clock, no randomness, no I/O
  (see the [glossary](../glossary/index.md)); it must not hold a crypto key.
- **Proportionate** ([ADR-0036](0036-cloud-provider-and-hosting.md)) — defends the dump/backup threat;
  Postgres is never published.
- **One key story** — any new secret rides [ADR-0050](0050-secrets-management.md).
- **No enumeration-timing regression** — the invitation flow's constant-work property
  ([InviteToCircle](0018-circle-and-sharing-model.md), NFR-005) must be preserved.

## Considered options

1. **Keep the unsalted SHA-256.** Accept the reversibility.
2. **Per-address salt** (e.g. bcrypt/argon2 per value). Non-reversible, but destroys the cross-row
   equality the feature depends on.
3. **Keyed HMAC-SHA256 with a single server key.** Deterministic (equal addresses → equal output) yet
   non-reversible without the key — a "keyed digest" / pepper.

## Decision outcome

**Chosen: Option 3 — HMAC-SHA256 of the canonicalised address under a single server-held key.**

- A new port **`IEmailHasher`** (in the Circles domain) exposes `Hash(email)`. The implementation,
  **`HmacEmailHasher`** (Circles repository), canonicalises the address exactly as `EmailAddress` compares
  — `Trim().ToUpperInvariant()` — then computes `Base64Url(HMAC-SHA256(key, canonical))`.
- The hash is computed at the **application boundary** and passed **into** the aggregate as an opaque
  value — the same shape the pre-hashed invitation *secret handle* already uses. `Circle.Invite` and
  `Circle.Accept` now take a hash argument instead of hashing internally; `EmailAddress.HashOf` is
  removed. The domain never sees the key, so it stays pure and the layering fitness tests still hold.
- The key, **`Circles:Security:EmailHashKey`** (Base64), joins the runtime tier of
  [ADR-0050](0050-secrets-management.md): host `deploy/.env` + vault, dev-only value in
  `appsettings.Development.json`, **none in the base `appsettings.json`** so a production boot without it
  **fails closed**.
- **No schema change**: an HMAC-SHA256 digest is the same length as the old SHA-256, so every `EmailHash`
  column keeps its shape — only the values differ, and there are none yet.

### Scope note — plaintext addresses that remain by necessity

A **pending invitation** still stores the address in clear (`invitations.Email`), because sending and
resending the mail needs it (NFR-004 keeps it only for that, and the row is deleted the moment the
invitation is consumed, revoked, or expires). This ADR does not change that: it protects the rows that
hold **only a hash** (`invitation_declines`, `invitation_attempts`) and the hash *comparison* everywhere.
Encrypting the transient pending-invitation address is a separate, lower-value question left open.

### Why not the alternatives

- **Option 1** is the pre-production state the review exists to close, while the hashes are still zero.
- **Option 2 (per-value salt)** is the right tool for *passwords*, which are only ever compared against
  themselves — but here the hash is a **lookup and equality key across rows**, which a per-value salt
  destroys. (Passwords are, correctly, already salted-and-stretched by ASP.NET Identity — a different
  problem.)

## Consequences

- **Positive:** the hash-only rows (declines, attempts) and every stored `EmailHash` are no longer
  dictionary-attackable from a dump. Deterministic equality — and therefore invitation matching, the
  decline cooldown, and the rate-limit key — is unchanged. The domain stays key-free and pure; the
  application layer gains one injected dependency.
- **Positive:** enumeration timing is unaffected — an HMAC is constant work, so the constant-budget
  invitation path ([InvitationTimingTests](0018-circle-and-sharing-model.md)) still holds.
- **Negative / accepted:** a **new durable secret**. **Changing or losing it orphans every stored email
  hash** — declined addresses would silently leave cooldown and pending invitations would stop matching —
  so it is stable-for-the-life-of-the-data and joins the "never lose this" tier of
  [ADR-0050](0050-secrets-management.md).
- **Negative / accepted:** the key is a single global pepper, not per-tenant. Proportionate for one small
  suite; a compromise of *both* the DB and the key defeats it, which is the accepted boundary (the defence
  is against dumps/backups, not a live host compromise).
- **Follow-ups:**
  - Generate the key at provisioning (`openssl rand -base64 48`), store in the vault, derive into
    `deploy/.env`; added to `deploy/.env.example` and the M8 go-live checklist.
  - No migration (unchanged column shape). The `PersistedModelReviewTests` data-minimisation guard is
    unaffected.
  - Optional, out of scope: revisit encrypting the transient plaintext address on pending invitations.

## Pros and cons of the options

### Option 1 — Unsalted SHA-256
- 👍 Nothing to build or provision.
- 👎 Reversible by dictionary/rainbow attack; the hash-only rows leak their addresses from a dump.

### Option 2 — Per-address salt
- 👍 Non-reversible.
- 👎 Breaks cross-row equality — invitation matching, decline cooldown, and the rate-limit key all fail.

### Option 3 — Keyed HMAC-SHA256 (chosen)
- 👍 Non-reversible without the key **and** deterministic; no schema change; domain stays pure; timing
  unchanged.
- 👎 One new durable secret whose loss orphans the stored hashes.

## Links

- Related ADRs: [ADR-0018](0018-circle-and-sharing-model.md) (circle & invitation model — the hash's
  consumers), [ADR-0050](0050-secrets-management.md) (where the new key lives),
  [ADR-0061](0061-share-token-and-creator-key-at-rest-protection.md) (the sibling at-rest decision for
  shared groups), [ADR-0022](0022-account-deletion-and-data-export.md) (retention/minimisation)
- Requirements: NFR-004 (data protection & retention), NFR-005/NFR-006 (enumeration resistance)
- arc42 chapters: [08 Crosscutting concepts](../arc42/08-crosscutting-concepts.md),
  [11 Risks & technical debt](../arc42/11-risks-and-technical-debt.md)
- Deployment: [`docs/deployment/launch-readiness.md`](../deployment/launch-readiness.md)
