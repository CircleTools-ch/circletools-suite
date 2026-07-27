---
term: Circle membership
contexts: [Circles]
status: Draft
---

# Circle membership

## Definition

The link between a [Circle](circle.md) and one person in it, carrying that person's **role** in the
circle. The person is either a [User](user.md) with their own account, or a
[Managed member](managed-member.md) with a profile but no account.

Roles:

| Role | May |
|------|-----|
| **Owner** | Everything an admin may, plus delete the circle and transfer ownership. Exactly one per circle. |
| **Admin** | Invite and remove people, manage managed members, edit circle name and label. |
| **Member** | See the circle and its people, edit their own profile, leave the circle. |

A user may hold memberships in several circles, with a different role in each. Membership is what a
person *leaves*; removing a membership never deletes the underlying user.

## Bounded context(s)

- **Circles:** an entity within the circle aggregate. Creating one is the result of an accepted
  [Invitation](invitation.md), or of an admin adding a managed member directly.

## Synonyms / do-not-use

- **Synonyms:** membership (when the circle context is already clear).
- **Do NOT use:** "member" unqualified — that word belongs to Secret-Santa
  ([Member](member.md)) and confusing the two is the most likely modelling mistake here;
  "role" alone for the whole membership (the role is one attribute of it); "permission", "grant".

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Mitgliedschaft *(Rollen: Eigentümer:in / Administrator:in / Mitglied)* |
| EN | Membership *(roles: owner / admin / member)* |
| FR | Adhésion *(rôles : propriétaire / administrateur·rice / membre)* |

## Related terms

- [Circle](circle.md), [User](user.md), [Managed member](managed-member.md), [Invitation](invitation.md)

## First appearance

- [Brainstorming — Accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md)
