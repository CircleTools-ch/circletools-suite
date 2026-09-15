# Database schema (PostgreSQL)

The backend is **one PostgreSQL database** whose tables are partitioned into **five schemas**, one per
bounded context, each with its own EF Core `DbContext` and its own `__EFMigrationsHistory` table so the
migration sets stay independent ([ADR-0003](../adr/0003-modular-monolith-with-aspire.md)):
`public` (Identity + tokens + outbox), `circles`, `sync`, `linking`, and `shared`.

A deliberate rule cuts across all of them: **no foreign key references `AspNetUsers` from another
context** ([ADR-0019](../adr/0019-member-user-linking.md)). An account is held as a **value** (a bare
`uuid`), and account deletion is a host-orchestrated cleanup, not a database cascade — so a circle or a
shared group survives the deletion of any one account. The diagrams below therefore show foreign keys
*within* an aggregate only; account ids and cross-context group/member ids are value references, drawn as
dashed links and called out in prose.

The diagrams reflect the schema as of **2026-09-15**, including the pre-launch hardening in
[ADR-0061](../adr/0061-share-token-and-creator-key-at-rest-protection.md) (share tokens/creator key at
rest) and [ADR-0062](../adr/0062-email-address-hashing-keyed-hmac.md) (email hashing). Only the defining
columns are shown; the full column set lives in the entity types.

## `public` — Identity, tokens & outbox (Main context)

ASP.NET Core Identity with a `uuid` key ([ADR-0008](../adr/0008-entity-id-strategy.md)); passwords are
PBKDF2-hashed by Identity, and a refresh token is stored only as the **SHA-256 handle** of its secret
([ADR-0026](../adr/0026-refresh-token-persistence.md)). The standard `AspNetRoles`,
`AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUserTokens`, and `AspNetRoleClaims`
tables also exist and are omitted here for clarity.

```mermaid
erDiagram
    AspNetUsers ||--o{ AspNetUserPasskeys : "has"
    refresh_token_chains ||--o{ refresh_tokens : "issues"

    AspNetUsers {
        uuid Id PK
        varchar Email "NormalizedEmail unique"
        text PasswordHash "PBKDF2"
        varchar DisplayName
        varchar PreferredLanguage
        timestamptz CreatedAt
    }
    AspNetUserPasskeys {
        bytea CredentialId PK
        jsonb Data "owned, ToJson"
    }
    refresh_token_chains {
        uuid Id PK
        uuid UserId "account value"
        timestamptz RevokedAt "nullable"
    }
    refresh_tokens {
        varchar Handle PK "SHA-256 of the secret"
        uuid ChainId FK
        timestamptz IssuedAt
        timestamptz ExpiresAt
        timestamptz ConsumedAt "nullable"
        varchar SuccessorHandle "nullable"
    }
    outbox_messages {
        uuid Id PK
        varchar Type
        text Payload
        timestamptz EnqueuedAt
        timestamptz ProcessedAt "nullable"
        int Attempts
    }
```

## `circles` — the Circle aggregate

A circle owns its memberships, pending invitations, and decline records (in-aggregate cascade delete).
`UserId` on a membership is a nullable account **value** (null = a managed member with no account).
`EmailHash` / `RecipientEmailHash` are **keyed HMAC-SHA256** of the address
([ADR-0062](../adr/0062-email-address-hashing-keyed-hmac.md)); the pending `invitations` row also keeps
the plaintext `Email` because sending the mail needs it, and is deleted the moment it is consumed. The
`circles` row carries a `xmin` system-column rowversion for optimistic concurrency.

```mermaid
erDiagram
    circles ||--o{ circle_memberships : "has"
    circles ||--o{ invitations : "has"
    circles ||--o{ invitation_declines : "has"

    circles {
        uuid Id PK
        varchar Name
        varchar Label
        timestamptz CreatedAt
        xid xmin "rowversion"
    }
    circle_memberships {
        uuid Id PK
        uuid CircleId FK
        uuid UserId "account value, nullable"
        varchar DisplayName
        varchar Role "enum as string"
        timestamptz JoinedAt
    }
    invitations {
        uuid Id PK
        uuid CircleId FK
        varchar Email "plaintext, to send mail"
        varchar EmailHash "keyed HMAC"
        uuid InvitedByUserId "account value"
        varchar SecretHandle "hash, unique"
        timestamptz ExpiresAt
    }
    invitation_declines {
        uuid Id PK
        uuid CircleId FK
        varchar EmailHash "keyed HMAC"
        timestamptz DeclinedAt
    }
    invitation_attempts {
        uuid Id PK
        uuid InviterUserId "account value"
        varchar RecipientEmailHash "keyed HMAC"
        timestamptz AttemptedAt
    }
```

## `sync` — account-scoped opaque sync store

The generic sync tier ([ADR-0016](../adr/0016-sync-and-conflict-resolution.md),
[ADR-0033](../adr/0033-which-aggregates-sync.md)): each aggregate is an **opaque `jsonb` body** the
server never interprets, keyed by `(AccountId, AggregateId)`, with a server sequence for ordering. No
foreign keys — every id is a value.

```mermaid
erDiagram
    aggregates {
        uuid AccountId PK
        uuid AggregateId PK
        varchar Kind
        jsonb Body "opaque"
        bigint Version
        bigint ServerSeq
        timestamptz ServerModifiedAt
    }
    markers {
        uuid AccountId PK
        uuid AggregateId PK
        varchar MarkerType "enum as string"
        bigint ServerSeq
    }
    devices {
        uuid AccountId PK
        varchar DeviceId PK
        bigint LastCursor
        timestamptz LastSeenAt
    }
    account_counters {
        uuid AccountId PK
        bigint NextSeq
    }
```

## `linking` — the ADR-0020 per-caller assignment surface

The one server-side store that understands the Secret-Santa shape
([ADR-0020](../adr/0020-server-side-assignment-privacy.md),
[ADR-0042](../adr/0042-events-as-first-class-entities.md), FEAT-005). No foreign keys: `GroupId`,
`EventId`, member ids and account ids are all values into the opaque group document. `member_links`
carries a **partial unique index** on `(GroupId, TargetAccountId) WHERE State = 'Linked'` — the database
invariant behind "one account backs at most one member per group". Budget money is `numeric(10,2)`.

```mermaid
erDiagram
    member_links {
        uuid Id PK
        uuid GroupId "value; UQ(GroupId,TargetAccountId) when Linked"
        uuid MemberId "value"
        uuid OrganizerAccountId "account value"
        uuid TargetAccountId "account value, nullable"
        varchar State "enum as string"
        timestamptz ExpiresAt
    }
    assignments {
        uuid GroupId PK
        uuid EventId PK
        uuid GiverAccountId PK "account value"
        varchar RecipientName
        jsonb RecipientWishlistSnapshot
        numeric BudgetAmount "numeric(10,2), nullable"
        numeric BudgetMinAmount "numeric(10,2), nullable"
        varchar BudgetCurrency "char(3), nullable"
        bigint DrawVersion
    }
    member_wishlists {
        uuid GroupId PK
        uuid EventId PK
        uuid MemberAccountId PK "account value"
        jsonb Items
        bigint Version
    }
    group_events {
        uuid GroupId PK
        uuid EventId PK
        uuid OrganizerAccountId "account value"
        timestamptz Date "nullable"
    }
    resolutions {
        uuid Id PK
        uuid OrganizerAccountId "account value"
        varchar Kind "enum as string"
        uuid UserId "account value, nullable"
        timestamptz CreatedAt
    }
```

## `shared` — account-free, link-shared groups

The third storage tier ([ADR-0058](../adr/0058-account-free-link-shared-groups.md), FEAT-013): a group
lives here addressed by **share tokens**, with an optional owning account (`OwnerAccountId`, a value,
null while account-free). Per [ADR-0061](../adr/0061-share-token-and-creator-key-at-rest-protection.md),
a token is looked up by the **SHA-256 hash** stored in `tokens.Token` (the primary key) and kept
recoverable for the resume-share path as **AES-256-GCM** ciphertext in `TokenCipher`; the creator key is
stored only as its **SHA-256 hash**. `member_claims.AccountId` and `groups.OwnerAccountId` are indexed
for account-deletion cleanup.

```mermaid
erDiagram
    groups {
        uuid GroupId PK
        jsonb Body "opaque group document"
        bigint Version
        varchar CreatorKey "SHA-256 hash, nullable"
        uuid OwnerAccountId "account value, nullable, indexed"
        varchar AdminClaimedByName "nullable"
        timestamptz LastActivityAt "retention sweep"
    }
    tokens {
        varchar Token PK "SHA-256 hash of the link token"
        varchar TokenCipher "AES-256-GCM of the token"
        uuid GroupId "value, indexed"
        varchar Capability "enum as string"
        bool Active
        bool RetiredByClaim
        timestamptz CreatedAt
    }
    member_claims {
        uuid GroupId PK
        uuid MemberId PK
        varchar ClaimKind "enum as string"
        varchar DeviceHandle "nullable"
        uuid AccountId "account value, nullable, indexed"
        timestamptz ClaimedAt
    }
```

> The `groups` ⇄ `tokens` ⇄ `member_claims` link is by `GroupId` **value** (indexed), not a foreign key —
> the three tables are cleaned up together in one transaction by the store, not by a database cascade.

## Regenerating

These are inline Mermaid diagrams (no pre-render step). Keep them in step with the EF Core entity
configurations under `src/Apps/Main/CircleTools.Main.Service.Repository` and
`src/BuildingBlocks/CircleTools.Circles.Repository`; a schema change should update the matching diagram in
the same PR.
