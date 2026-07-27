---
status: Accepted
date: 2026-07-27
deciders: [Adrian]
tags: [architecture, domain, circles, identity, privacy, security]
---

# ADR-0018 — Circle & sharing model

## Context and problem statement

v2 introduces the concept the suite has never had: a **standing set of people**. Today every
Secret-Santa [Group](../glossary/group.md) is typed in from scratch and belongs to one organizer.
A family or friend-group has no lasting existence, so nothing can be reused across apps or years.

This ADR defines that bounded context: what the aggregate is, what roles exist, how people are
invited without leaking who has an account, how people **without** an account participate, and how a
circle relates to a Secret-Santa group.

The product serves "families **and** groups of friends", which makes naming a genuine architectural
concern rather than a cosmetic one — the chosen word ends up in tables, DTOs and URLs.

## Decision drivers

- **The Secret-Santa domain must not become dependent on this context.** Groups stay creatable
  standalone and offline; that is non-negotiable
  ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Not everyone has, or will get, an email address.** A family Secret-Santa without the children is
  pointless.
- **Invitations must not become an account-enumeration oracle**
  ([security & privacy guidelines](../guidelines/security-privacy-guidelines.md) §1).
- **A completed draw is history** and must stay reproducible even as the circle changes.
- Ubiquitous language: the name must be defined in the [glossary](../glossary/index.md) before any
  code uses it (`CLAUDE.md` rule 7).

## Considered options

**Naming**

1. `Family` — warm, matches the **Walf** brand (*We Are Like Family*).
2. `Circle` — neutral, covers both families and friend-groups.
3. **One concept, canonical code name + user-chosen display label.**

**Circle → group membership**

- (a) **Snapshot at draw time.** (b) Live link — the group always mirrors the circle.
  (c) Hybrid — live until the draw, frozen after.

**Invitation & account existence**

- (a) **Never reveal existence to the inviter.** (b) Tell the inviter which path applied.

## Decision outcome

**Chosen: option 3 for naming, (a)/(c) for membership, (a) for invitations.**

### The aggregate and its name

[`Circle`](../glossary/circle.md) is the aggregate root and the **canonical code name** — aggregates,
DB tables, DTOs and URLs all say `Circle`. The name users *see* is a **per-circle label they choose
themselves**, defaulting to "Family" (*Familie* / *Famille*).

The split exists because neither word is honest for the other case: "Family" is literally wrong for a
group of friends, and a table should not lie. Putting the warmth in the label layer costs nothing,
varies per circle, and means most users never see the word "Circle". **`Group` stays reserved** for
the Secret-Santa aggregate — a circle is the standing set of people, a group is one gift exchange.

### Membership and roles

A [`CircleMembership`](../glossary/circle-membership.md) links a circle to one person and carries
their role. A [User](../glossary/user.md) may hold memberships in several circles with a different
role in each.

| Role | May |
|------|-----|
| **Owner** | Everything an admin may, plus delete the circle and transfer ownership. **Exactly one** per circle. |
| **Admin** | Invite and remove people, manage managed members, edit the circle's name and label. |
| **Member** | See the circle and its people, edit their own profile, leave the circle. |

Membership is what a person *leaves*; removing one never deletes the underlying user.

### Managed members (people without an account)

A [managed member](../glossary/managed-member.md) is a person in a circle with a profile but **no
account**, maintained by an owner or admin — young children, relatives who will not sign up. They
hold a membership like anyone else, always in the `member` role, and can be carried into a
Secret-Santa group exactly like an account-backed person. Because they cannot sign in, they receive
their [Assignment](../glossary/assignment.md) the account-free way, through a reveal link the
organizer passes on.

A managed member may later be **promoted**: when the person does get an account, the existing profile
is *linked* to the new user rather than duplicated, so their history survives.

This is in the model from the start even though the UI may ship later. Retrofitting it would mean
unpicking an assumption ("member = account") baked into invitations, sync and linking.

### Invitations and account-enumeration resistance

An [Invitation](../glossary/invitation.md) is a pending offer to join a circle, addressed to an email
address. It resolves down one of two paths, **decided entirely on the recipient's side**:

- the address has no account → the recipient signs up and lands in the circle;
- the address already has a user → the recipient sees an in-app **join request** and accepts or
  declines.

**The inviter is never told which path applied.** The UI says *"invitation sent"* either way.
Reporting "this address already has an account" would turn the invite form into a free
account-enumeration oracle — the exact probe used to harvest which addresses are registered.

Invitations **expire**, can be **resent and revoked** by an admin, and issuing them is
**rate-limited per inviter** (a rate limit is what stops the "invitation sent" response from being
enumerated by timing or volume instead).

Managed members are added directly by an admin and never go through an invitation.

### Circle → Secret-Santa group

A group may be created **from** a circle, which copies its people in as
[Members](../glossary/member.md). From then on the group owns its own membership.

Mechanically this is the hybrid (c): the group tracks the circle while it is still being set up, and
**freezes when the draw runs** (a). A completed draw is history — a circle that gains or loses someone
in January must not silently rewrite a December draw, and every reveal link already sent refers to
the member set as it was. This matches the existing rule that changing members invalidates a draw.

The dependency points **one way only**: the Secret-Santa domain holds at most a circle identifier and
never references the Circles context. A group created from a circle is indistinguishable from one
typed in by hand.

## Consequences

- **Positive:** people are entered once and reused across groups, years and future apps; the
  account-free and offline paths are entirely unaffected; children and non-signups are first-class
  participants; the naming split gives brand warmth in the UI without a dishonest domain model; the
  invite flow is enumeration-resistant by construction rather than by later hardening.
- **Negative / accepted — the inviter flies blind.** They cannot see whether their friend already has
  an account, cannot tell an accepted invitation from an ignored one until it is accepted, and get no
  "already a member" feedback. This is a real UX cost, paid deliberately for enumeration resistance.
- **Negative / accepted — two "member" words.** `Member` (Secret-Santa) and `CircleMembership`
  (Circles) sit next to each other and are easy to confuse. Mitigated in the glossary, where each
  entry names the other under *do-not-use*, but it remains the most likely modelling mistake in v2.
- **Negative / accepted:** "exactly one owner" needs an explicit transfer flow, and an edge case when
  the sole owner deletes their account — the circle must either promote an admin or be deleted with
  it. Specified in the M2 feature spec, not here.
- **Negative / accepted:** a per-circle label means the UI must never hard-code "Family"; every
  surface that names the circle reads the label, and the three locales need a default each.
- **Follow-ups:** M2 feature spec (invitation lifetimes, rate-limit numbers, owner-deletion edge
  case, promotion of a managed member); `UC-MAIN-*` use cases; arc42 ch. 5 must add the **Circles**
  bounded context, confirming the provisional context label used in the glossary.
- **New risks:** invitation email delivery becomes a hard dependency for onboarding into a circle —
  if delivery proves unreliable, the parked "invite by link or short code" idea becomes the fallback.

## Pros and cons of the options

### 3. Canonical `Circle` + user-chosen label (chosen)
- 👍 Never wrong in either direction; brand warmth preserved in the UI; one concept, one table.
- 👎 Two names for one thing; a little extra UI and localization work.

### 1. `Family` everywhere
- 👍 Matches the brand in code as well as UI; one word to learn.
- 👎 Literally inaccurate for friend-groups — rows that lie about what they hold.

### 2. `Circle` everywhere
- 👍 Honest and neutral; no label machinery.
- 👎 Colder; drifts from the Walf story; users see a word nobody uses for their family.

### Membership: (a)/(c) snapshot at draw (chosen)
- 👍 Draws stay reproducible; already-sent reveal links stay correct.
- 👎 Requires explicit UI wording that drawing fixes the participant list.

### Membership: (b) live link
- 👍 Always current; nothing to explain.
- 👎 **Breaks existing draws** — a January change rewrites a December result. Rejected.

### Invitations: (a) never reveal (chosen)
- 👍 No enumeration oracle; consistent with the security guidelines.
- 👎 Weaker inviter feedback.

### Invitations: (b) tell the inviter
- 👍 Clearly better UX.
- 👎 Leaks which email addresses have accounts — a privacy defect in a family app. Rejected.

## Links

- Related ADRs: [ADR-0019](0019-member-user-linking.md),
  [ADR-0016](0016-sync-and-conflict-resolution.md),
  [ADR-0009](0009-authentication-and-passkeys.md), [ADR-0008](0008-entity-id-strategy.md)
- arc42: [05 Building-block view](../arc42/05-building-block-view.md) (Circles context — follow-up),
  [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md) §8.8, §8.10
- Guideline: [security & privacy guidelines](../guidelines/security-privacy-guidelines.md)
- Requirements: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)
- Source: [brainstorming — accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decisions D2, D5, D6, D7
- Glossary: [Circle](../glossary/circle.md), [Circle membership](../glossary/circle-membership.md), [Invitation](../glossary/invitation.md), [Managed member](../glossary/managed-member.md)
