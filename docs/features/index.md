# Feature Specs & Test Plans

A **feature spec** (`FEAT-NNN`) turns approved use cases and requirements into an implementable
design. Each feature has a paired **test plan** (`TP-NNN`).

- New feature spec → copy [`_template-feature-spec.md`](_template-feature-spec.md).
- New test plan → copy [`_template-test-plan.md`](_template-test-plan.md).

## Register

| ID | Title | Bounded context | Status |
|----|-------|-----------------|--------|
| [FEAT-001](FEAT-001-secret-santa-mvp.md) | Secret-Santa MVP (offline, local-first) | SecretSanta | Done |
| [TP-001](TP-001-secret-santa-mvp.md) | Test plan — Secret-Santa MVP | SecretSanta | Reviewed |
| [FEAT-002](FEAT-002-m1-identity.md) | M1 — Identity in the Main shell | Identity | Done |
| [TP-002](TP-002-m1-identity.md) | Test plan — M1 Identity | Identity | Reviewed |
| [FEAT-003](FEAT-003-m2-circles.md) | M2 — Circles in the Main shell | Circles | Done |
| [TP-003](TP-003-m2-circles.md) | Test plan — M2 Circles | Circles | Reviewed |

**M1 is closed, not shipped.** FEAT-002 is `Done` in the sense the process means it — the slice is
built, tested and documented. It cannot reach production until the transactional-email provider and
the hosting/suite-domain decisions land; both are open items in the
[ADR backlog](../adr/index.md#open-decisions-backlog). Two verification criteria were carried to
milestone 7 rather than ticked — see [the backlog](../roadmap/backlog.md) and
[arc42 ch. 11](../arc42/11-risks-and-technical-debt.md).

**M2 is closed on the same terms, and is blocked on one of the same two decisions.** FEAT-003 is
`Done`: the `Circles` context is built, the role matrix and the two NFR-006 fitness functions are
green, and FR-015's sole-owner guard — the criterion M1 carried — is closed. Shipping still waits on
the **transactional-email provider**, which invitations sharpen from account-critical to
onboarding-critical: an invitation that does not arrive is a person who never joins. Three criteria are
carried rather than ticked, two of them to M4 — see the
[requirements register](../requirements/index.md#reading-the-statuses).

The spec is also where the seven circle requirements' deliberately open numbers were decided
(invitation lifetime, the three rate limits, the decline cooldown, the caps) and where four product
questions they left are answered; the reasoning for each sits in the spec's *numbers* table, and the
corrections implementation forced on it are marked in place rather than rewritten away.

**What closing M2 cost, as a warning for M3.** Reconciling TP-003 against the code found four things
missing that a green suite had nothing to say about: the NFR-006 fitness function whose mechanism was
built but never read back, a "role matrix" that was three spot checks, two acceptance criteria unmet in
the **product** (both destructive confirmations), and an invitation-revoke path with no functional test.
[TP-003's closing notes](TP-003-m2-circles.md#what-the-reconciliation-found-2026-07-30) record how each
stayed invisible; the short version is that a test-plan row is not evidence.
