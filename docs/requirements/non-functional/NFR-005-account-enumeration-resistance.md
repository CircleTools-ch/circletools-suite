---
id: NFR-005
title: Account-enumeration resistance
type: non-functional
status: Draft
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
| Email change ([FR-013](../functional/FR-013-change-email-address.md)) | identical response whether or not the new address is taken |
| [Invitations](../../glossary/invitation.md) (M2) | *"invitation sent"* either way; the inviter is never told which path applied ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)) |

"Identical" means identical in **response body, HTTP status code, and timing**. Timing is the part
that is easy to get wrong and easy for an attacker to measure: a path that sends an email must not
take measurably longer than one that does not, so mail dispatch is queued rather than awaited.

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

Given any address-accepting endpoint
When a path sends an email and another path does not
Then mail dispatch is queued rather than awaited, so response times do not diverge

Given repeated requests to any address-accepting endpoint
When they exceed the configured threshold
Then they are rate-limited per address and per source with progressive backoff
```

## Quality attribute *(NFR only)*

- **Attribute:** security / privacy (information disclosure).
- **Metric & target:**
  - **zero** endpoints whose response body or status code differs between existing and non-existing
    addresses;
  - **timing:** the difference in median response time between the existing-address and
    non-existing-address cases must be **below the noise floor of the test harness**, and in no case
    may one path await an outbound email while the other does not;
  - **100 %** of address-accepting endpoints rate-limited per address and per source.
- **Measurement approach:** paired **automated tests** per endpoint asserting identical body and
  status; a timing test comparing the two distributions over repeated runs; a review checklist item
  whenever an address-accepting endpoint is added or its error handling changed.

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
- Glossary: [User](../../glossary/user.md), [Invitation](../../glossary/invitation.md)
