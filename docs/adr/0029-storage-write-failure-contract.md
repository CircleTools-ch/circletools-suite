---
status: Accepted
date: 2026-07-28
deciders: [Adrian]
tags: [architecture, ui, frontend, storage, data-integrity, local-first]
---

# ADR-0029 — What an `IKeyValueStore` write does when storage refuses it

## Context and problem statement

`CircleTools.Ui.Abstractions.IKeyValueStore` was written for M1, where everything it held was
*recoverable*: a language preference and a refresh token. Its documented contract said reads **and
writes** never throw — implementations swallow storage errors and behave as though nothing was
stored. For a preference that is right: web storage is unavailable in private-browsing modes and
wherever site data is blocked, and a forgotten language is not worth failing a page render over.

Migrating Secret-Santa onto the shared blocks ([ADR-0025](0025-shared-ui-building-blocks.md)'s
follow-up) put a second kind of caller behind the same interface. Secret-Santa is **local-first with
no backend** ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)): the store
holds the **only copy** of a group. Adopting the contract verbatim would have converted the current
loud failure — an exception the user sees — into a silent no-op, with the UI reporting a successful
save. That is data loss the user learns about later, if at all.

**How does one storage interface serve a caller that can afford to lose a write and a caller that
cannot?**

## Decision drivers

- NFR-001: the local store is the system of record for an account-free group. Losing it silently is
  the worst failure this app has.
- The preference and refresh-token callers must keep working unchanged in a blocked-storage browser
  — throwing there would break the page, which is what the original contract correctly avoided.
- One interface. Two stores with two contracts is what the migration exists to remove.
- Whether a lost write matters is knowledge the **caller** has and the store does not.

## Considered options

1. **`SetAsync` returns `ValueTask<bool>`** — the store reports whether the value stuck; each caller
   decides what that means.
2. **Keep two interfaces** — Secret-Santa keeps a private non-swallowing store for group data; the
   shared one carries preferences only.
3. **Adopt the shared contract as-is** — swallow everywhere.
4. **Make the store throw** — and have preference callers catch.

## Decision outcome

**Chosen: option 1 — `ValueTask<bool> SetAsync(...)`.**

The contract is now split by operation rather than by caller:

- **Reads and removes never throw.** Unreadable is indistinguishable from absent, and every caller
  wants the same answer.
- **Writes never throw either, but they report.** `true` if the value was stored, `false` if the
  storage refused it — unavailable, blocked, or full.

Callers then say what they mean, in one line each:

| Caller | Behaviour |
|---|---|
| `CultureProvider.SetCultureAsync` | ignores the result — the language still applies to this session |
| `RefreshTokenStore` | ignores the result — a lost token costs a re-login |
| `GroupRepository.SaveStatesAsync` | throws `StorageUnavailableException` — this was the only copy |

`StorageUnavailableException` lives in `Ui.Abstractions` beside the contract, and is raised by the
*caller*, never by the store. That is the point: the store knows the write failed, only the caller
knows whether that is a shrug or a catastrophe.

## Consequences

- **Positive:** the difference between "we forgot your language" and "we did not save your group" is
  now expressible, and expressed, instead of being flattened into one behaviour.
- **Positive:** it is testable without a browser. `InMemoryKeyValueStore.RefuseWrites` and
  `FakeKeyValueStore.RefuseWrites` simulate a blocked store, and two new tests pin that a refused
  write reaches the user as a failure rather than a shrug.
- **Negative / accepted:** an ignored return value is easy to write by accident. Every deliberate
  ignore is written as `_ = await store.SetAsync(...)` with a comment saying why, so the ones that
  matter stand out from the ones that do not.
- **Negative / accepted:** Secret-Santa's UI does not yet *present* the failure — the exception
  reaches Blazor's error UI, which is what a failed write already did before this change. Making it
  a readable message is a follow-up, not a regression.
- **Follow-ups:** catch `StorageUnavailableException` in the Secret-Santa pages and show a
  translated message (DE/EN/FR) telling the user their change was not saved; revisit when
  [Sync](../glossary/sync.md) (M3) gives the data a second home and changes the stakes.
- **New risks:** none identified. The interface change is compile-breaking, so no implementation can
  silently keep the old behaviour.

## Pros and cons of the options

### 1. `SetAsync` returns `bool` (chosen)
- 👍 One interface, both policies; the decision sits with the caller that has the context.
- 👎 Touches every implementation and call site; a result that can be ignored silently.

### 2. Two interfaces
- 👍 Smallest behavioural risk; no shared contract change.
- 👎 Leaves Secret-Santa's own `IKeyValueStore` alive, which is half the thing ADR-0025's follow-up
  exists to remove, and puts two storage abstractions in front of the same `localStorage`.

### 3. Swallow everywhere
- 👍 Least code; the follow-up completes literally.
- 👎 A quota-exceeded or private-mode write silently drops the user's only copy of a group while the
  UI says it saved. Unacceptable under NFR-001.

### 4. Throw always
- 👍 Nothing is ever lost quietly.
- 👎 Breaks the page for anyone browsing with storage blocked — the failure mode the original
  contract was written to prevent, and a bad trade for a language preference.

## Links

- Related ADRs: [ADR-0025](0025-shared-ui-building-blocks.md),
  [ADR-0026](0026-refresh-token-persistence.md), [ADR-0028](0028-browser-platform-adapters-block.md)
- Requirements: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)
- arc42: [05 Building-block view](../arc42/05-building-block-view.md),
  [08 Crosscutting concepts](../arc42/08-crosscutting-concepts.md)
