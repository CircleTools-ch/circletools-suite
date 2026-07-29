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
| [FEAT-003](FEAT-003-m2-circles.md) | M2 — Circles in the Main shell | Circles | Draft |
| [TP-003](TP-003-m2-circles.md) | Test plan — M2 Circles | Circles | Draft |

**M1 is closed, not shipped.** FEAT-002 is `Done` in the sense the process means it — the slice is
built, tested and documented. It cannot reach production until the transactional-email provider and
the hosting/suite-domain decisions land; both are open items in the
[ADR backlog](../adr/index.md#open-decisions-backlog). Two verification criteria were carried to
milestone 7 rather than ticked — see [the backlog](../roadmap/backlog.md) and
[arc42 ch. 11](../arc42/11-risks-and-technical-debt.md).

**M2 is specified, not built.** FEAT-003 and TP-003 are `Draft` — awaiting review, with no code
behind them. The spec is where the seven circle requirements' deliberately open numbers get decided
(invitation lifetime, the three rate limits, the decline cooldown, the caps) and where four product
questions they left are answered; the reasoning for each sits in the spec's *numbers* table.
