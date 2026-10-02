---
status: Accepted
date: 2026-10-02
deciders: [Adrian]
tags: [mobile, ios, android, maui, trimming, aot, json, serialization]
---

# ADR-0072 — Reflection-based JSON on the native heads (iOS full-trim)

## Context and problem statement

The first on-device test of the native heads found that on **iOS** (iPad/iPhone) nothing could be
created, edited, deleted, or opened, and the app became unresponsive — while the **same build worked on
Android** and on the web. Member edits failed with "could not be saved"; opening/creating a group did
nothing; and separately, the account/session reads failed.

Root cause: **iOS Release builds are fully trimmed + AOT**, which sets the MSBuild feature switch
`JsonSerializerIsReflectionEnabledByDefault = false`. Any `System.Text.Json` call that serializes via
**reflection** (i.e. without a source-generated `JsonTypeInfo`/`JsonSerializerContext`) then throws
`InvalidOperationException` at runtime. **Android Release** uses partial trimming and the **web** head is
WASM-interpreted, so reflection-based JSON stays enabled there — which is why the identical code only
failed on iOS.

The codebase already standardised on source-generated JSON for exactly this reason (see
`SyncJsonContext`, `GroupStateJsonContext`, …), but a few client paths still used reflection:

- `RecentAccessService` — `JsonSerializer.Serialize(List<Guid>)` / `Deserialize<List<Guid>>` for the
  recently-opened groups/events (fires on open/create/navigate).
- `ApiResponseReader.ReadAsync<T>` (shared `CircleTools.ApiClient`, used by `AccountApiClient`) — reads
  typed API responses with a reflection `JsonSerializerOptions` (fires on session reads → logout on iOS).

**How should the native heads handle these reflection-based JSON paths so iOS works like Android/web?**

## Decision drivers

- **iOS must work** — create/edit/open and account reads are core; they are dead on iOS otherwise.
- **Lowest-risk, single-shot fix** — each iOS attempt is a TestFlight round trip (no local iOS loop,
  [ADR-0065](0065-hosted-macos-ios-builds.md)), so the fix must be comprehensive, not whack-a-mole.
- **Keep the codebase's source-gen convention** — reflection JSON is the exception to remove over time,
  not the norm to embrace.

## Considered options

1. **Convert every reflection-based JSON call to source generation.** The "correct" end state, but
   `ApiResponseReader.ReadAsync<T>` is generic over many account/circle DTOs with no existing source-gen
   context; doing it safely is a broad refactor across the shared API client — high risk for a hotfix,
   and easy to miss a path and ship another dead iOS build.
2. **Re-enable reflection-based JSON for the native app** by setting
   `JsonSerializerIsReflectionEnabledByDefault=true` in the MAUI head. One line; restores the exact
   behaviour Android/web already have; covers every reflection path at once, known or missed.
3. **Relax iOS trimming** (`TrimMode=partial`) to match Android. Also works, but a blunter lever with a
   larger binary and wider behavioural surface than the targeted feature switch.

## Decision outcome

**Chosen: option 2 now, option 1 as the follow-up.** The MAUI app sets
`<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>`, which
re-enables reflection-based `System.Text.Json` on the native heads and brings iOS to parity with Android
and the web, where the same code already works. It is comprehensive (covers `RecentAccessService`,
`ApiResponseReader`, and any path not individually spotted) and minimal.

The proper fix — **source-generating the remaining client JSON** (`RecentAccessService`'s `List<Guid>`,
and `ApiResponseReader`/`AccountApiClient`'s DTOs) so full trimming can be re-enabled — is tracked as a
follow-up. Option 3 is held in reserve if a residual trimming issue (a trimmed DTO member) shows up on
device.

## Consequences

- **Positive:** iOS create/edit/open/navigate and account/session reads work again, with a one-line,
  low-risk change; no shared-API-client refactor under incident pressure.
- **Negative / accepted:** reflection-based JSON is slower and keeps more metadata (slightly larger app /
  less trimming) than source-gen; this is the pre-existing Android/web behaviour, now matched on iOS.
- **Residual risk:** full trimming can still remove a DTO member only referenced via reflection; not seen
  (Android/web exercise the same types), and the source-gen follow-up removes the risk entirely.
- **Follow-ups:**
  1. Source-generate `RecentAccessService` (`List<Guid>`) and the `ApiResponseReader`/`AccountApiClient`
     response DTOs; then drop this switch and re-enable full trimming.
  2. Add a guard (analyzer or arch test) that flags reflection-based `JsonSerializer` calls in client code
     so a new one cannot silently reach a trimmed/AOT head again.

## Pros and cons of the options

### 1. Source-generate everything now
- 👍 Correct end state; keeps full trimming.
- 👎 Broad, risky refactor of the shared API client for a hotfix; easy to miss a path and re-ship a dead
  iOS build.

### 2. Re-enable reflection JSON on the native app (chosen)
- 👍 One line; comprehensive; matches the known-good Android/web behaviour; low risk.
- 👎 Masks (does not remove) the reflection usage; defers the proper source-gen work.

### 3. Relax iOS trimming
- 👍 Also restores reflection metadata; matches Android's trim level.
- 👎 Blunter than the targeted switch; larger binary; wider behavioural change.

## Links

- Related ADRs: [ADR-0028](0028-browser-platform-adapters-block.md) (platform seams),
  [ADR-0065](0065-hosted-macos-ios-builds.md) (no local iOS loop — why a single-shot fix matters),
  [ADR-0066](0066-mobile-native-passkeys.md) / [ADR-0069](0069-passkey-origin-allow-list-for-native-heads.md)
  (other native-head specifics)
- Features: [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md) (mobile polish)
