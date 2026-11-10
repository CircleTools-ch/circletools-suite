---
id: NFR-005
title: Account-enumeration resistance
type: non-functional
status: Implemented
priority: Must
source: UC-MAIN-001, UC-MAIN-002, UC-MAIN-007, ADR-0018
verification: test
related: [UC-MAIN-001, UC-MAIN-002, UC-MAIN-007, FR-007, FR-008, FR-009, FR-013, ADR-0018]
---

# NFR-005 — Account-enumeration resistance

## Description

**No public endpoint may reveal whether an email address has an account.** This holds for every
address-accepting surface in the suite:

| Surface | Requirement |
|---------|-------------|
| Sign-up ([FR-007](../functional/FR-007-create-suite-account.md)) | identical response; an existing address receives a *"someone tried to sign up"* mail instead of a verification mail |
| Sign-in ([FR-008](../functional/FR-008-sign-in-with-password.md)) | unknown address and wrong password are indistinguishable |
| Password reset ([FR-009](../functional/FR-009-reset-password.md)) | identical response; no mail sent for an unknown address |
| Resend verification ([FR-007](../functional/FR-007-create-suite-account.md)) | the same 202 for an unknown address, an already-verified one, and a genuine resend |
| Email change ([FR-013](../functional/FR-013-change-email-address.md)) | identical response whether or not the new address is taken |
| [Invitations](../../glossary/invitation.md) (M2) | *"invitation sent"* either way; the inviter is never told which path applied ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)) |

"Identical" means identical in **response body, HTTP status code, and timing**. Timing is the part
that is easy to get wrong and easy for an attacker to measure: a path that sends an email must not
take measurably longer than one that does not, so mail dispatch is queued rather than awaited.

**Queueing the mail is necessary and not sufficient**, which M2 established by measurement. Even
queued, the branch that has an account to act on generates a token and writes an outbox row, and the
branch that does not writes nothing — and that residue is enough. Four surfaces leaked on it:
password reset at 1.9x, resend-verification at 1.7x, email change at 1.6x, and the invite form at
1.18x. All four had bodies and status codes that were already identical, and one of them had been
shipping since M1.

Two mechanisms answer it, and which one fits depends on the shape of the asymmetry:

| Mechanism | Where it applies |
|-----------|------------------|
| **Decoy work** — `DecoyPasswordVerifier` spends the same password hash on the branch with no password to check | One dominant, symmetric cost. Sign-up and sign-in. |
| **Constant-time envelope** — `RequireConstantTime` holds the whole response to a fixed budget | A token, a database write, or several — anything a decoy cannot mirror without writing rows nobody reads. Password reset, resend-verification, email change, invitations. |

Lockout and rate-limiting responses are also covered — a lockout message that only appears for real
accounts is itself an oracle. **Rate limiting is part of this requirement, not separate from it:** it
is what stops an identical response from being defeated by volume, and it applies per address and per
source with progressive backoff.

## Rationale

An address-existence oracle is the exact probe used to harvest which addresses are registered, and
harvested lists feed credential stuffing and targeted phishing. [ADR-0018](../../adr/0018-circle-and-sharing-model.md)
already accepted a real UX cost for this on invitations — the inviter deliberately "flies blind" and
gets no *"already a member"* feedback. That cost is only worth paying if the property holds
**everywhere**; a leaky sign-up form would make the careful invitation design pointless.

Stating it as one cross-cutting NFR rather than repeating it in five requirements is deliberate: it is
a single property with a single test approach, and it is the kind of rule that quietly regresses when
someone adds a helpful error message.

## Acceptance criteria

```gherkin
Given an email address with an account and one without
When the sign-up form is submitted with each
Then the two responses are identical in body, status code and timing distribution

Given an email address with an account and one without
When a password reset is requested for each
Then the two responses are identical in body, status code and timing distribution

Given a known email address with a wrong password, and an unknown email address
When sign-in is attempted with each
Then the two failure responses are identical in body, status code and timing distribution

Given a signed-in user requesting an email change
When the new address is already taken, and when it is free
Then the two responses are identical in body, status code and timing distribution

Given an account that is currently rate-limited or locked out
When sign-in is attempted against it and against a non-existent address
Then the responses do not reveal which case applied

Given an email address with an account and one without
When verification is resent for each
Then the two responses are identical in body, status code and timing distribution

Given an invitation to a circle
When it is sent to an address with no account, one with an account, and one already in the circle
Then all three responses are identical in body, status code and timing distribution

Given any address-accepting endpoint
When a path sends an email and another path does not
Then mail dispatch is queued rather than awaited, so response times do not diverge

Given an address-accepting endpoint whose branches differ by a token, a database write, or both
When it is not equalised by decoy work
Then it is held to a constant-time budget, and every branch reaches that budget

Given repeated requests to any address-accepting endpoint
When they exceed the configured threshold
Then they are rate-limited per address and per source with progressive backoff
```

## Quality attribute *(NFR only)*

- **Attribute:** security / privacy (information disclosure).
- **Metric & target:**
  - **zero** endpoints whose response body or status code differs between existing and non-existing
    addresses;
  - **timing:** in no case may one path await an outbound email while the other does not, **nor
    compute a password hash while the other does not** — hashing is deliberately slow and dominates
    these endpoints, so a branch that skips it is the loudest possible signal (measured at 20x
    before it was fixed; see [FEAT-002](../../features/FEAT-002-m1-identity.md)). Beyond that, the
    target depends on the mechanism:
    - a surface equalised by **decoy work** must keep its two paths within a **1.5x ratio** of each
      other;
    - a surface under a **constant-time envelope** must have **every** path reach the budget, within
      timer jitter.

    > **"Below the noise floor of the test harness" was the previous target, and it is why four
    > leaks shipped.** A floor is a hole: `/forgot-password` sat in it for all of M1 at a steady 1.9x
    > because the absolute gap was 1.12 ms, and it surfaced only when a loaded run scaled both paths
    > past 3 ms. A target phrased against the harness's resolution measures the harness. The
    > replacements above measure the endpoint — and the envelope's target is deliberately not a
    > difference at all, because the smallest real leak found (0.70 ms) is inside the resolution of
    > the timer used to look for it.
  - **100 %** of address-accepting endpoints rate-limited per address and per source.
- **Measurement approach:** paired **automated tests** per endpoint asserting identical body and
  status; a timing test per endpoint, comparing the two distributions over repeated runs for a decoy
  surface, and for an envelope surface asserting that every path reaches the budget and that its
  handler fits within half of it; a review checklist item whenever an address-accepting endpoint is
  added or its error handling changed.

## Dependencies & related

- Derived from: [UC-MAIN-001](../../use-cases/main/UC-MAIN-001-create-account.md),
  [UC-MAIN-002](../../use-cases/main/UC-MAIN-002-sign-in-with-password.md),
  [UC-MAIN-007](../../use-cases/main/UC-MAIN-007-reset-password.md)
- Related: [FR-007](../functional/FR-007-create-suite-account.md),
  [FR-008](../functional/FR-008-sign-in-with-password.md),
  [FR-009](../functional/FR-009-reset-password.md),
  [FR-013](../functional/FR-013-change-email-address.md),
  [NFR-004](NFR-004-data-protection-and-retention.md)
- Governing ADR: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (established the rule for
  invitations; this NFR generalises it to every address-accepting surface)
- Guideline: [security & privacy guidelines](../../guidelines/security-privacy-guidelines.md) §1
  and §7 (address-accepting endpoints — which mechanism to use, and how to test it)
- Glossary: [User](../../glossary/user.md), [Invitation](../../glossary/invitation.md)
