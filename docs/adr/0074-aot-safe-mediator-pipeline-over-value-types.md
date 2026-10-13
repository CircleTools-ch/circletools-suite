---
status: Accepted
date: 2026-10-07
deciders: [Adrian]
tags: [mobile, ios, maui, aot, application, cqrs, mediation]
---

# ADR-0074 — AOT-safe mediator pipeline over value-type results

## Context and problem statement

After [ADR-0072](0072-reflection-json-on-native-heads.md) restored reflection-based JSON on the native
heads, on-device testing on **iPad** hit the *next* iOS full-AOT wall. Creating or editing a group failed
in the UI with:

```
Die Gruppe konnte nicht erstellt werden:
InvalidOperationException: AotCannotCreateGenericValueType,
CircleTools.Application.IPipelineBehavior`2[CircleTools.SecretSanta.Core.Application.CreateGroupCommand,System.Guid], System.Guid
```

The same build worked on **Android** and the **web**.

Root cause: iOS Release is **fully trimmed + AOT** (Mono full-AOT; see ADR-0072). Mono full-AOT **cannot
create a generic instantiation over a value-type type argument at runtime** — each value-type
instantiation needs its own specialised code emitted at build time, whereas reference-type
instantiations all share one canonical body ("shared generics"). The hand-rolled CQRS dispatcher
([ADR-0006](0006-application-layer-and-mediation.md)) resolves its pipeline behaviors from DI closed over
the request's **result type**:

```csharp
var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResult));
var behaviors = provider.GetServices(behaviorType); // materialises LoggingBehavior<…,TResult>, AutoSyncBehavior<…,TResult>
```

When `TResult` is a **value type**, DI must materialise e.g. `LoggingBehavior<CreateGroupCommand, Guid>`,
which full-AOT refuses. The value-type results are:

- **`Guid`** — `CreateGroupCommand`, `AddMemberCommand`, `AddEventCommand`.
- The internal **`Unit` struct** — *every* void command (`EditMemberCommand`, `RenameGroupCommand`,
  `DeleteGroupCommand`, `SetBudgetCommand`, …), dispatched as `Dispatch<Unit>`.

That is why **create, add, and all edits/deletes** were dead on iPad while reference-type results (the
`GetGroups`/`GetGroup` queries returning DTOs, the `Ping` → `string` test) worked. Nothing in the app
statically references these closed value-type behavior types, so the AOT compiler never emitted them.

**How should the dispatcher run cross-cutting behaviors so a value-type command result does not force a
value-type generic instantiation on a full-AOT head?**

## Decision drivers

- **iOS must work** — create/add/edit/delete are core; they are dead on iPad otherwise, and each iOS
  attempt is a TestFlight round trip (no local iOS loop, [ADR-0065](0065-hosted-macos-ios-builds.md)), so
  the fix must be comprehensive, not whack-a-mole.
- **Cover every value-type result at once** — `Guid` today, but also the `Unit` struct and any future
  `int`/`bool`/record-struct result; a per-type workaround reopens the crash on the next command.
- **Keep [ADR-0006](0006-application-layer-and-mediation.md) intact** — the public
  `IPipelineBehavior<TRequest,TResponse>` contract, the behavior implementations, and the open-generic DI
  registrations should not change.
- **Behaviour-preserving on Android and the web**, where the same code already works.

## Considered options

1. **Run the behavior pipeline over a boxed `object` response.** The dispatcher closes
   `IPipelineBehavior<,>` over `object` instead of the real `TResult`, so the behaviors are only ever
   instantiated over **reference types** (shared generics, which full-AOT supports). The handler's real
   result is boxed on the way out and unboxed once at the end.
2. **Force the value-type instantiations to exist at build time** — a `DynamicDependency`/explicit-
   reference "AOT root" that statically names `LoggingBehavior<T, Guid>`, `AutoSyncBehavior<T, Unit>`, …
   so the AOT compiler emits them.
3. **Make `IPipelineBehavior` non-generic** (operate on `object` request *and* response). The behaviors
   already only read `typeof(TRequest).Name` / the request's interfaces, obtainable from `request.GetType()`.
4. **Relax iOS trimming/AOT** (e.g. ship the Mono interpreter / `TrimMode=partial`) so runtime
   value-type instantiation is allowed.

## Decision outcome

**Chosen: option 1 — run the pipeline over a boxed `object` response.** In `Sender.Dispatch<TResult>` the
behavior type is closed over `object`
(`typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(object))`), the innermost continuation
boxes the handler's `TResult` to `object?`, the behaviors run over `object?`, and the final result is
unboxed back to `TResult` once. `TRequest` stays the real (reference-type) command type, so shared
generics cover it too.

This is the smallest change that is also **comprehensive**: it removes value-type generic instantiation
from the mediator entirely, so `Guid`, `Unit`, and any future value-type result are all covered without a
registry of AOT roots to maintain. It leaves the public `IPipelineBehavior<TRequest,TResponse>` contract,
both behavior implementations, and the open-generic registrations **unchanged** — behaviours only ever
pass the response through untouched, so boxing it is invisible to them. The one added cost is a single
box/unbox per dispatch (negligible). It is a dispatcher-internal correctness fix implementing ADR-0006 on
a full-AOT target, not a change to that decision.

Option 3 is the cleaner long-term shape (no generics in the pipeline at all) but changes a public
building-block contract for no behavioural gain over option 1. Option 2 is brittle (every new value-type
result needs a new root; `Unit` is internal; one miss re-ships a dead iOS build). Option 4 is a blunt
lever with a larger binary / wider behavioural surface than a targeted code fix.

## Consequences

- **Positive:** iPad create/add/edit/delete work again; the fix is head-agnostic (one change in the shared
  `CircleTools.Application` dispatcher fixes iOS without touching the web/Android code that already
  worked); covers all value-type results at once; no public API, behavior, or registration change.
- **Negative / accepted:** the response is boxed/unboxed once per dispatch; a maintainer must not
  "optimise" the behavior type back to `typeof(TResult)` — a code comment in `Sender.cs` and this ADR
  guard against that regression.
- **Follow-ups:**
  1. Consider option 3 (a non-generic `IPipelineBehavior`) as a later simplification if more behaviors
     arrive — it would let the dispatcher drop reflection on the behavior path entirely.
  2. Watch for the *same class* of error from any other runtime `MakeGenericType`/`GetServices` over a
     value-type argument on the native heads (none known today).
- **New risks:** none beyond the above; the change is exercised by the existing dispatcher test
  (`DispatcherTests`, reference-type result) and the full command/query set on web + Android.

## Pros and cons of the options

### 1. Boxed `object` response (chosen)
- 👍 Removes value-type instantiation from the mediator for good; covers `Guid` + `Unit` + future types at
  once; no public contract / behavior / registration change; one small dispatcher change, head-agnostic.
- 👎 One box/unbox per dispatch; the "why" must be documented so it is not reverted.

### 2. AOT roots for each value-type instantiation
- 👍 Keeps the pipeline strongly typed end to end.
- 👎 Brittle and easy to miss (`Unit` is internal; every new value-type result needs a new root); a single
  miss re-ships a dead iOS build — exactly the TestFlight round trip ADR-0065 makes expensive.

### 3. Non-generic `IPipelineBehavior`
- 👍 No generics in the pipeline at all; most AOT-robust end state.
- 👎 Changes a public building-block contract (ADR-0006) for no behavioural gain over option 1.

### 4. Relax iOS trimming / ship the interpreter
- 👍 Allows runtime value-type instantiation without code changes.
- 👎 Blunt; larger binary and wider behavioural surface than a targeted fix; works against the trimming
  posture ADR-0072 is converging back toward.

## Links

- Related ADRs: [ADR-0006](0006-application-layer-and-mediation.md) (the hand-rolled dispatcher this fixes
  on AOT), [ADR-0072](0072-reflection-json-on-native-heads.md) (the previous iOS full-AOT/trim wall and the
  same symptom shape), [ADR-0065](0065-hosted-macos-ios-builds.md) (no local iOS loop — why a single-shot
  fix matters), [ADR-0028](0028-browser-platform-adapters-block.md) (platform seams)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md)
- Guideline: [coding-guidelines.md](../guidelines/coding-guidelines.md) (CQRS dispatcher + pipeline pattern)
- Features: [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md) (mobile polish)
