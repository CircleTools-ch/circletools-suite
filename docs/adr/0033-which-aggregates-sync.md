---
status: Accepted
date: 2026-08-03
deciders: [Adrian]
tags: [architecture, sync, offline, authorization]
---

# ADR-0033 — Which aggregates sync (narrowing ADR-0016)

## Context and problem statement

[ADR-0016](0016-sync-and-conflict-resolution.md) chose aggregate-granular last-write-wins as the sync
mechanism and named **two** sync units by example: the Secret-Santa [Group](../glossary/group.md) and
the [Circle](../glossary/circle.md). At the time both were hypothetical — neither had been built, and
the ADR was deciding the *mechanism*, not enumerating the catalogue.

M2 then built circles, and built them differently from what the sync ADR assumed. A
[Circle](../glossary/circle.md) is **online-only and server-authoritative**: it is created inside an
account, never exists account-free, and every mutation passes a server-side authorization decision
([NFR-006](../requirements/non-functional/NFR-006-circle-authorization.md)) that decides whether *this
caller, in this role* may make *this* change ([FEAT-003](../features/FEAT-003-m2-circles.md);
[arc42 ch. 5 §5.3](../arc42/05-building-block-view.md)). M3 is now being specified, and its lead
requirement [FR-023](../requirements/functional/FR-023-sync-across-devices.md) states plainly that
**circle data does not sync** — only claimed groups and profile preferences do.

So the accepted ADR and the requirement that implements it disagree on the catalogue. **Does circle
data participate in local↔server sync, and does ADR-0016's last-write-wins apply to it?** This must be
answered — and the answer recorded against ADR-0016 — before M3 is built, rather than left as a silent
divergence between an Accepted ADR and the code (the ADR backlog flags it as *"due before M3 is
built"*).

## Decision drivers

- **LWW is safe for data one account owns, and unsafe for data several people share.** A group belongs
  to one account; discarding the losing side of a genuine collision loses *that user's own* edit, which
  is the bounded, accepted cost in ADR-0016. A circle is administered by up to four people; the same
  rule would discard **other people's** edits — a data-loss window that is neither bounded to the
  editing user nor visible to them.
- **An offline write cannot make a server-side authorization decision.** NFR-006 requires every circle
  mutation to be authorized against the caller's current role *on the server*. A change queued offline
  and pushed later has no way to make that decision at the moment of the edit, and the role may have
  changed by push time. Group sync has no equivalent gate — a claimed group is authorized by ownership
  of the account, which the bearer token already proves.
- **Record reality, don't re-litigate it.** M2 already shipped circles online-only. This ADR is not
  choosing a new architecture; it is narrowing an over-broad example in ADR-0016 to match what was
  built and what the requirements ask for.
- **Local-first is unweakened either way.** Circles were never part of the account-free product
  ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)); excluding them from sync
  takes nothing away from the offline promise, which is about the Secret-Santa app.

## Considered options

1. **Narrow ADR-0016: Group + profile preferences sync; Circle does not** — circles stay
   server-authoritative and online-only as M2 built them.
2. **Bring circles into sync under LWW as ADR-0016 literally reads** — treat a circle as just another
   aggregate under last-write-wins.
3. **Design a different sync model for circles now** (field-level merge, or per-mutation replay with
   server-side re-authorization) so they can sync without the LWW hazard.

## Decision outcome

**Chosen: Option 1.** In M3 the sync catalogue is **the claimed Secret-Santa `Group`** (the whole
aggregate — members, wishlists, exclusions, event details, and the [Draw](../glossary/draw.md) with its
[assignments](../glossary/assignment.md) if one has run) and **profile preferences** (display name,
preferred language). The **`Circle` is excluded**: it remains online-only and server-authoritative
exactly as M2 built it, reached only by a signed-in, verified account over a live API.

This is a **narrowing of ADR-0016, not a reversal**. ADR-0016's mechanism — aggregate as the unit,
version token, cursor, tombstones, LWW by server-recorded time, the telemetry-emitting escalation hook
— stands unchanged for everything that *does* sync. ADR-0016 stays **Accepted**; this ADR is recorded
against it as a refinement of its catalogue, and ADR-0016 carries a "narrowed by ADR-0033" note in its
Links.

Options 2 and 3 are rejected for the reasons under the drivers: option 2 imports a data-loss window
onto shared data and still cannot honour NFR-006 offline; option 3 spends real design and machinery now
to solve a problem (offline circle *editing*) that nobody has asked for and that the product does not
need — circles being a thing you do together, online, by definition.

### What would bring circles into sync later — and the distinction that matters

Excluding circles from sync in M3 is not a permanent verdict, and the door is left open **on one
specific axis**:

- **An offline *read cache* for circles is a different decision from offline *writes*.** Letting a
  device show a circle's people while briefly offline is a caching question — the data is read-only in
  that state, no authorization decision is deferred, and nothing is pushed back. That could be
  reconsidered on a concrete trigger (users report that circle screens are unusable on flaky
  connections) **without** reopening the write question, and without any LWW hazard, because a read
  cache never resolves a conflict.
- **Offline circle *writes*** — the thing this ADR actually excludes — would require solving both
  problems the drivers name: a conflict model that does not silently discard a co-admin's edit, and a
  way to carry or re-take the NFR-006 authorization decision at push time. Only a real need for
  editing-while-offline (not merely reading) reopens *that*, and it would want its own ADR.

Keeping these two apart is the point: a future "circles work offline" request must be pinned to *which*
of the two it means before it is scoped.

## Consequences

- **Positive:** the ADR register and the M2 code agree again; M3 implements exactly one sync path
  (groups + profile), which is the one ADR-0016's mechanism was actually designed for. No shared data
  is exposed to silent last-write-wins.
- **Positive:** NFR-006 stays intact — every circle mutation keeps making its authorization decision on
  the server, at the moment of the change, because there is no offline circle write to defer it.
- **Negative / accepted:** circle screens require a connection, and M3 must say so rather than fail
  opaquely — FR-023's acceptance criteria already require that an offline circle screen states it needs
  a connection and that **no circle data is written locally for a later push**. That is a small piece of
  UI work M3 owns.
- **Negative / accepted:** a user who is offline cannot review who is in their circle. Accepted for M3;
  it is exactly the read-cache trigger noted above, and is deliberately left as a separate, later
  decision.
- **Follow-ups:** [FR-023](../requirements/functional/FR-023-sync-across-devices.md),
  [glossary/sync.md](../glossary/sync.md) and
  [arc42 ch. 5 §5.3](../arc42/05-building-block-view.md) are updated to cite this ADR instead of
  describing the exclusion as "pending an ADR". The offline read-cache for circles is **parked** in the
  ADR backlog on the trigger above.
- **New risks:** none introduced. The one to watch is scope creep in the other direction — a later
  feature that quietly starts caching circles for offline *editing* under the read-cache banner. The
  distinction above exists to catch exactly that.

## Pros and cons of the options

### 1. Narrow ADR-0016 — Group + profile sync, Circle excluded (chosen)
- 👍 Matches what M2 built and what FR-023 asks; no shared data under LWW; NFR-006 untouched.
- 👍 Smallest possible change — records reality and closes the divergence.
- 👎 Circles are unusable offline until a separate read-cache decision is taken.

### 2. Circles sync under LWW, as ADR-0016 literally reads
- 👍 One uniform rule; no exception to document.
- 👎 Silently discards a co-administrator's edit — a data-loss window on shared data that is invisible to
  the person who lost the edit.
- 👎 Cannot honour NFR-006: an offline circle write has no server-side role to authorize against.

### 3. A different sync model for circles now (field-merge or replay + re-authorization)
- 👍 Would let circles sync without the LWW hazard.
- 👎 Substantial machinery for a capability nobody has asked for; circles are inherently an online,
  collaborative activity. Speculative work against a hypothetical need.

## Links

- Related ADRs: [ADR-0016](0016-sync-and-conflict-resolution.md) (**narrowed by this ADR**),
  [ADR-0017](0017-anonymous-to-account-claim.md), [ADR-0018](0018-circle-and-sharing-model.md),
  [ADR-0020](0020-server-side-assignment-privacy.md)
- arc42 chapters: [05 Building-block view §5.3](../arc42/05-building-block-view.md)
- Requirements / use cases:
  [FR-023](../requirements/functional/FR-023-sync-across-devices.md),
  [NFR-006](../requirements/non-functional/NFR-006-circle-authorization.md),
  [NFR-008](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md),
  [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)
- Feature: [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md)
- Glossary: [Sync](../glossary/sync.md), [Circle](../glossary/circle.md), [Group](../glossary/group.md)
