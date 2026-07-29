---
id: NFR-006
title: Circle data authorization
type: non-functional
status: Approved
priority: Must
source: UC-MAIN-008, UC-MAIN-012, ADR-0018
verification: test
related: [UC-MAIN-008, UC-MAIN-009, UC-MAIN-010, UC-MAIN-011, UC-MAIN-012, UC-MAIN-013, UC-MAIN-014, FR-016, FR-017, FR-019, FR-020, FR-022, NFR-005, ADR-0018, ADR-0021, FEAT-003]
---

# NFR-006 — Circle data authorization

## Description

**No [Circle](../../glossary/circle.md) data may reach anyone who is not a member of that circle, and
no action may be performed by anyone whose role does not permit it — enforced server-side, on every
request.**

Concretely:

- **Every** circle endpoint authorizes two things independently: *is the caller a member of this
  circle*, and *does their role permit this action*
  ([FR-020](../functional/FR-020-manage-circle-roles-and-membership.md)'s table is the authority).
- Authorization is evaluated **per request** against current membership. It is never cached into an
  access token, so a removed member, a demoted admin and a transferred owner lose their rights on
  their **next** request rather than at the next token refresh.
- A request from a **non-member** — including a valid, signed-in user who simply is not in this circle
  — must not reveal that the circle exists. Unknown-circle and not-a-member responses are identical.
- **No email addresses** of members are returned by any circle endpoint
  ([NFR-005](NFR-005-account-enumeration-resistance.md)).
- The UI hiding an action is presentation, never the control. Every rule above must hold against a
  direct API call.

## Rationale

M1's protected data belonged to the caller: an account, its passkeys, its profile. Circles are the
first data in the suite that belongs to **other people**, so authorization stops being "am I signed
in?" and becomes a matrix — seven use cases times three roles plus non-members — which is exactly the
shape of thing that is got right once and then quietly broken by the eighth endpoint.

Stating it as one cross-cutting NFR rather than repeating it in seven requirements is the same choice
made for [NFR-005](NFR-005-account-enumeration-resistance.md), and for the same reason: one property,
one test approach, and a single place to point at when a new endpoint is added.

Per-request evaluation matters more here than in M1. With bearer tokens living up to 15 minutes
([ADR-0021](../../adr/0021-web-auth-token-model.md)), any role claim baked into a token would give a
removed member a quarter-hour of continued access to a family's data — and "removed" is precisely the
case where the removal is urgent.

Refusing to distinguish "no such circle" from "not your circle" closes the enumeration equivalent for
circles: identifiers are unguessable ([ADR-0008](../../adr/0008-entity-id-strategy.md)), but a
differing response would still confirm a circle a caller had learned of elsewhere.

## Acceptance criteria

```gherkin
Given a signed-in user who is not a member of a circle
When they call any endpoint of that circle
Then the response is identical to the response for a circle that does not exist

Given each circle endpoint
When it is called by an owner, an admin, a member and a non-member
Then the outcome matches FR-020's role table for every combination

Given a member whose role was just changed
When they make their next request
Then it is authorized under the new role, with no sign-out and no token refresh

Given a member who was just removed from a circle
When they make their next request with a still-valid access token
Then it is refused

Given any circle endpoint that returns people
When the response is inspected
Then it contains display names
And it contains no email addresses

Given a UI that hides an action the caller's role does not permit
When the corresponding API call is made directly
Then it is refused server-side

Given a new circle endpoint added later
When it is reviewed
Then it has a membership check, a role check, and a role-matrix test
```

## Quality attribute *(NFR only)*

- **Attribute:** security (authorization / information disclosure).
- **Metric & target:**
  - **100 %** of circle endpoints covered by an automated **role-matrix test** — owner, admin, member,
    non-member — asserting both the permitted and the refused outcomes;
  - **zero** endpoints returning member email addresses;
  - **zero** endpoints whose response distinguishes *not a member* from *does not exist*;
  - **zero** authorization decisions derived from a token claim rather than from current membership;
  - **effect latency:** a removal, demotion or transfer takes effect on the **next request** (target:
    within one request, not one token lifetime).
- **Measurement approach:** a parameterised integration test per endpoint across the four caller kinds;
  an assertion over the API surface that no circle DTO carries an email field; a test that removes a
  member and immediately reuses their existing access token; a review checklist item whenever a circle
  endpoint is added.

## Dependencies & related

- Derived from: [UC-MAIN-008](../../use-cases/main/UC-MAIN-008-create-circle.md) …
  [UC-MAIN-014](../../use-cases/main/UC-MAIN-014-delete-circle.md) — it constrains all of them
- Related: [FR-020](../functional/FR-020-manage-circle-roles-and-membership.md) (the role table this
  enforces), [NFR-005](NFR-005-account-enumeration-resistance.md),
  [NFR-004](NFR-004-data-protection-and-retention.md)
- Governing ADRs: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (roles),
  [ADR-0021](../../adr/0021-web-auth-token-model.md) (token lifetimes),
  [ADR-0008](../../adr/0008-entity-id-strategy.md) (unguessable identifiers)
- Implemented by: [FEAT-003](../../features/FEAT-003-m2-circles.md) — one `CircleRoleFilter` per
  route, `404` for both *not a member* and *unknown circle*, `403` only for a member whose role is
  insufficient · verified by [TP-003](../../features/TP-003-m2-circles.md), whose role matrix and two
  fitness functions (`EveryCircleRoute_CarriesTheRoleFilter`, `NoCircleDto_CarriesAnEmailField`) carry
  the "a new endpoint added later" criterion
- Guideline: [security & privacy guidelines](../../guidelines/security-privacy-guidelines.md)
- Glossary: [Circle](../../glossary/circle.md),
  [Circle membership](../../glossary/circle-membership.md)
