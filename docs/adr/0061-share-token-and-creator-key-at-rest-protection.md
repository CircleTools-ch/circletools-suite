---
status: Accepted
date: 2026-09-15
deciders: [Adrian]
tags: [security, privacy, encryption, shared-groups, persistence]
---

# ADR-0061 — Share-token and creator-key at-rest protection

## Context and problem statement

The account-free, shared-by-link store ([ADR-0058](0058-account-free-link-shared-groups.md),
FEAT-013) addresses a group by **share tokens** — an admin token and a join token, each a
high-entropy secret carried in a link — and gates a re-share on a **creator key**. As first built,
the `shared.tokens` table used the **plaintext token as its primary key**, and `shared.groups`
stored the **creator key in the clear**. A read-only Postgres dump therefore yields *working* admin
and join links and the admin-recovery key — the exact opposite of how refresh tokens are handled in
the same system, where only a SHA-256 handle is stored ([ADR-0026](0026-refresh-token-persistence.md)).

A pre-production schema review flagged this as a **one-way door**: the tokens are embedded in links
that, once distributed, cannot be reissued without disrupting recipients, so the representation must
be fixed **before any real link exists**.

The obvious fix — replace the stored token with a one-way hash, as refresh tokens do — collides with
a shipped ADR-0058 behaviour: **resume-share idempotency**. When a `ShareByLink` response is lost and
the creator retries (re-presenting the creator key), the server returns the **same working links** it
minted before. A one-way hash cannot be reversed to reproduce those links. So the token needs a
representation that is simultaneously (a) not readable from a bare dump and (b) recoverable by the
server so a resume returns the original link.

## Decision drivers

- **A database dump alone must not yield working links or the recovery key** — parity with the
  refresh-token handling in the same codebase.
- **Preserve ADR-0058 semantics unchanged** — resume-share idempotency, admin-token rotation, and the
  claim/retire flow must behave exactly as before.
- **Deterministic O(1) lookup** — a token arriving in a request must resolve to its row by primary key,
  not by scanning and trial-decrypting every row.
- **Keep the domain honest** — the token in the link stays the client's credential; only its stored
  form changes.
- **Proportionate** ([ADR-0036](0036-cloud-provider-and-hosting.md)) — Postgres is never published off
  the internal Docker network, so this defends the *dump / backup* threat, not a live-DB compromise.
- **One key story** — any new secret rides the existing two-tier secrets management
  ([ADR-0050](0050-secrets-management.md)), not a new mechanism.

## Considered options

1. **Leave plaintext; accept the risk.** Document that a dump exposes links (unguessable bearer URLs,
   DB unpublished).
2. **Hash the token only** (SHA-256 primary key), and on resume **re-mint** fresh links instead of
   returning the originals.
3. **Hash for lookup *plus* a reversible encrypted copy for resume** — store `SHA-256(token)` as the
   lookup key and `AES-256-GCM(token)` as a second column; hash the creator key (it is only ever
   compared, never returned).
4. **Deterministic (SIV-style) encryption** of the token as the key — reversible and lookup-able in one
   column.

## Decision outcome

**Chosen: Option 3 — deterministic hash for lookup, authenticated-encryption copy for recovery, and a
plain hash for the creator key.**

Concretely:

### Share tokens (`shared.tokens`)

- The primary key holds **`SHA-256(token)`**, Base64Url-encoded (a `ShareSecretHash` helper). Every
  lookup hashes the incoming token and finds by that key — deterministic and O(1). No salt: the token
  already carries 256 bits of entropy ([ADR-0058](0058-account-free-link-shared-groups.md), NFR-009),
  so there is no dictionary to defend against, and a salt would break the primary-key lookup.
- A new column **`TokenCipher` holds `AES-256-GCM(token)`** — a fresh random nonce per token, packed as
  `nonce || tag || ciphertext`, Base64. This is read **only** on the resume-share path, where the stored
  token is decrypted so the creator receives the *same* link. Authenticated encryption means a tampered
  stored value fails to decrypt rather than yielding a forged token.
- The columns keep their names (`Token`, now the hash; `TokenCipher`, new); the entity XML docs state
  plainly that `Token` stores a hash. Minting and rotation are unchanged — the *client* still receives
  the plaintext token in `MintedLinks` / `RotatedAdminLink`.

### Creator key (`shared.groups.CreatorKey`)

Stored as **`SHA-256(creatorKey)`** only. It is compared with a fixed-time equality against the hash of
a presented key and is **never returned to a client**, so it needs no reversible copy — a plain hash is
the complete fix. The high-entropy input again makes a salt unnecessary.

### Key management

A new secret **`SharedGroups:Security:TokenEncryptionKey`** — a Base64 AES-256 (32-byte) key — is added
to the runtime tier of [ADR-0050](0050-secrets-management.md): host `deploy/.env` + the password-manager
authoritative copy, with a dev-only value in `appsettings.Development.json`. There is **no key in the
base `appsettings.json`**, so a production boot without the secret **fails closed** (the cipher throws at
construction) rather than running with a known key.

### Not in scope

`shared.member_claims.DeviceHandle` is stored in clear but is **not a verified credential** — it is never
presented back to prove a claim (release is admin-token-gated), so hashing it would add no security while
breaking nothing it protects. Left as-is and noted here rather than changed.

### Why not the alternatives

- **Option 1 (plaintext)** was the pre-production state; the review's whole point is that it is the one
  cheap-now/expensive-later item, so "accept" defeats the purpose while links are still zero.
- **Option 2 (hash + re-mint on resume)** is simpler but **weakens ADR-0058**: a lost-response retry
  would rotate out links the creator may already have distributed, turning an idempotent safety-net into
  a footgun. The encrypted copy preserves the original guarantee for the cost of one column.
- **Option 4 (deterministic encryption)** collapses lookup and recovery into one column, but a
  deterministic AEAD (SIV) is a heavier, easier-to-misuse primitive than "SHA-256 for lookup + standard
  random-nonce GCM for recovery," and buys nothing here — the two-column split is simpler and each
  primitive is used in its textbook mode.

## Consequences

- **Positive:** a database dump or backup yields neither working links nor the recovery key; parity with
  refresh-token handling. ADR-0058 resume/rotation/claim behaviour is bit-for-bit unchanged. Lookups stay
  O(1) by primary key.
- **Positive:** fail-closed — a missing production key crashes the boot instead of silently using a weak
  one.
- **Negative / accepted:** a **new durable secret** to guard, provision, and back up. **Losing or
  rotating it strands every already-shared link** (their plaintext can no longer be recovered on resume),
  so it joins the "never lose this" tier of [ADR-0050](0050-secrets-management.md). This is a live-DB
  compromise's blast radius unchanged — the defence is explicitly against dumps/backups, not an attacker
  with both the DB and the key.
- **Negative / accepted:** one extra column and a hash+encrypt on mint / hash on lookup — negligible at
  this scale.
- **Follow-ups:**
  - Generate the AES-256 key at provisioning (`openssl rand -base64 32`), store in the vault, derive into
    `deploy/.env`; added to `deploy/.env.example` and the M8 go-live checklist.
  - Migration `ProtectShareTokensAtRestAndAccountIndexes` (shared context) adds `TokenCipher`.
- **New risks:** the token-encryption key becomes a high-value secret; its loss is a data-availability
  event (stranded links), not a confidentiality one.

## Pros and cons of the options

### Option 1 — Leave plaintext
- 👍 No code, no new secret.
- 👎 A dump yields working links and the recovery key; the one-way door stays open.

### Option 2 — Hash + re-mint on resume
- 👍 Single column, DB-dump-safe, no encryption key.
- 👎 Breaks ADR-0058 resume idempotency for the already-distributed-link case.

### Option 3 — Hash for lookup + AES-GCM copy (chosen)
- 👍 DB-dump-safe **and** ADR-0058 behaviour fully preserved; O(1) lookup; standard primitives in
  standard modes.
- 👎 One new durable secret and one extra column.

### Option 4 — Deterministic encryption
- 👍 One column does both.
- 👎 A heavier, more misuse-prone primitive for no gain over the two-column split.

## Links

- Related ADRs: [ADR-0058](0058-account-free-link-shared-groups.md) (shared-by-link groups — the tokens,
  creator key, resume/rotation semantics), [ADR-0026](0026-refresh-token-persistence.md) (refresh-token
  handle precedent), [ADR-0050](0050-secrets-management.md) (where the new key lives),
  [ADR-0062](0062-email-address-hashing-keyed-hmac.md) (the sibling hashing decision for Circles),
  [ADR-0008](0008-entity-id-strategy.md) (client-minted ids)
- Requirements: NFR-004 (data protection), NFR-009 (abuse resistance / token entropy)
- arc42 chapters: [08 Crosscutting concepts](../arc42/08-crosscutting-concepts.md),
  [11 Risks & technical debt](../arc42/11-risks-and-technical-debt.md)
- Deployment: [`docs/deployment/launch-readiness.md`](../deployment/launch-readiness.md),
  [`docs/deployment/web-api-db.md`](../deployment/web-api-db.md)
