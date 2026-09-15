---
term: Share token
contexts: [Platform, Secret-Santa]
status: Draft
---

# Share token

## Definition

The **unguessable identifier** a [shared-by-link group](shared-by-link-group.md) is keyed by on the
server, in place of a [User](user.md) id ([ADR-0058](../adr/0058-account-free-link-shared-groups.md)).
Holding a link that carries the token *is* the credential — the same *"the link is the secret"* model
as a [reveal link](reveal-link.md), scaled from one assignment to the whole group. There are token-bound
links for each capability: the [admin link](admin-link.md) and the [join link](join-link.md).

A token is **rotatable**: an admin can disable the current one and mint a new one, which invalidates the
old links (the *disable & regenerate* action). It must have enough entropy to resist guessing and is
subject to the anti-abuse and retention limits recorded as a follow-up to ADR-0058.

## Bounded context(s)

- **Platform:** how an account-free group is addressed and authorized on the server.

## Synonyms / do-not-use

- **Synonyms:** group token, sharing token.
- **Do NOT use:** "group id" (the aggregate keeps its own GUID id, [ADR-0008](../adr/0008-entity-id-strategy.md);
  the share token is a separate, rotatable capability key); "password".

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | — (internal; surfaced only inside links) |
| EN | — (internal; surfaced only inside links) |
| FR | — (internal; surfaced only inside links) |

## Related terms

- [Shared-by-link group](shared-by-link-group.md), [Admin link](admin-link.md),
  [Join link](join-link.md), [Reveal link](reveal-link.md)

## First appearance

- [ADR-0058](../adr/0058-account-free-link-shared-groups.md).
