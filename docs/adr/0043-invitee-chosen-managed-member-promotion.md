---
status: Accepted
date: 2026-08-14
deciders: [adrian-moll]
tags: [circles, invitations, privacy, ux]
---

# ADR-0043 — Let the invited person choose which offline profile they join as

## Context and problem statement

A [Circle](../glossary/circle.md) can hold [managed members](../glossary/managed-member.md) — *offline
people* with a profile but no account (Grandma, a young child). FR-019 already lets such a profile be
**promoted**: when the person gets an account, their [membership](../glossary/circle-membership.md) is
*linked* to the new account rather than duplicated, keeping their place and history. Until now this
could only be aimed by the **inviter** — they set a target managed member on the
[invitation](../glossary/invitation.md), and `Circle.Accept` links it.

That leaves a gap the product owner hit directly: if the admin invites an offline person's address
**without** aiming the invitation, the recipient can only ever join as a **brand-new** person, and the
existing offline profile is orphaned. The admin does not always know, at invite time, which offline
profile the newcomer corresponds to — but the recipient always does.

**Should the invited person be allowed to pick, at acceptance time, which offline profile they are
joining as — and if so, how do we show them that list without leaking it?**

The tension: [UC-MAIN-010](../use-cases/main/UC-MAIN-010-respond-to-invitation.md) and
[NFR-006](../requirements/non-functional/NFR-006-circle-authorization.md) say a person deciding whether
to join sees *"never the member list"* — circle contents are member-only data. A self-select list is,
by definition, part of that list.

## Decision drivers

- **Convenience / discoverability** — the recipient is the one who knows who they are; forcing the
  admin to get targeting right every time is the friction we are removing.
- **No new enumeration oracle** — the mailed link is not a bearer key
  ([ADR-0018](0018-circle-and-sharing-model.md), NFR-005); a forwarded-link holder must learn nothing.
- **Minimise the privacy relaxation** — reveal as little as possible, to as few people as possible.
- **Admin intent stays authoritative** — a targeted invitation must not be silently overridden.

## Considered options

1. **Admin-only targeting (status quo).**
2. **Invitee self-select, shown on the anonymous mail-landing preview.**
3. **Invitee self-select, gated to the authenticated, address-matched recipient** (chosen).

## Decision outcome

**Chosen: Option 3, in addition to admin targeting.** Both paths exist:

- If the inviter aimed the invitation at a managed member, that wins — the recipient is simply told
  *"you will join as X"* and has no choice to make.
- Otherwise the recipient is offered the circle's offline people (**managed members only**, display
  names only, no addresses, never account-backed members) and may pick one or join as *someone new*.

The offline roster is returned **only** to a caller who is **signed in** and whose **verified email
hash equals the invitation's** — the exact check that already gates acceptance. A forwarded-link
holder, or a signed-in user who is not the invited address, is refused identically to an unknown
invitation. The anonymous landing **preview** never carries it. This is the narrow, deliberate
relaxation of NFR-006 / UC-MAIN-010's "never the member list": the list is unlocked to precisely the
one person who is about to join and would see everyone a moment later anyway.

The domain guard is unchanged and does the real safety work: `Circle.Accept` links only when the
target still names a *managed* member of *this* circle. A stale id, or an id naming an account-backed
member, cannot seize a real account — it falls back to joining as new.

## Consequences

- **Positive:** the common case ("I'm Grandma") works with no admin foresight; no duplicate profiles;
  history preserved; the enumeration oracle stays closed.
- **Negative:** an invited person, when the admin did not target anyone, can attach their account to
  **any** offline profile in that circle — including the wrong one. This is bounded: the admin invited
  them into the circle in the first place (a trust basis), it only ever affects offline profiles, and
  the admin can remove/re-add to correct it. Admin targeting is the recommended path when the mapping
  is known up front.
- **Follow-ups:** amend UC-MAIN-009 (inviter side) and UC-MAIN-010 (recipient side); the two new
  authorized `…/targets` reads are the only circle endpoints that return managed-member names to a
  not-yet-member, so they carry the address gate explicitly.
- **New risks:** the residual mis-attribution above; accepted for a family/friends product.

## Pros and cons of the options

### Option 1 — Admin-only targeting
- 👍 No privacy relaxation at all.
- 👎 Orphaned offline profiles whenever the admin does not (or cannot) target; the reported defect.

### Option 2 — Self-select on the anonymous preview
- 👍 Simplest to build.
- 👎 Hands the offline roster to anyone holding a forwarded link — a straight NFR-006 breach and a new
  oracle. Rejected.

### Option 3 — Self-select gated to the authenticated, address-matched recipient
- 👍 Convenient; reveals the roster only to the genuine invitee, only after auth.
- 👎 A deliberate, documented relaxation of "never the member list"; carries the mis-attribution risk.

## Links

- Related ADRs: [ADR-0018](0018-circle-and-sharing-model.md) (circle & sharing model, D5)
- Requirements / use cases:
  [FR-019](../requirements/functional/FR-019-manage-managed-members.md),
  [FR-017](../requirements/functional/FR-017-invite-to-circle.md),
  [FR-018](../requirements/functional/FR-018-respond-to-circle-invitation.md),
  [UC-MAIN-009](../use-cases/main/UC-MAIN-009-invite-to-circle.md),
  [UC-MAIN-010](../use-cases/main/UC-MAIN-010-respond-to-invitation.md); constrained by
  [NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md),
  [NFR-006](../requirements/non-functional/NFR-006-circle-authorization.md)
- Glossary: [Managed member](../glossary/managed-member.md), [Invitation](../glossary/invitation.md),
  [Circle membership](../glossary/circle-membership.md)
