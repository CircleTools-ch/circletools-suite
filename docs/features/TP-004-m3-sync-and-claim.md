---
id: TP-004
feature: FEAT-004
status: Draft
related: [UC-MAIN-015, UC-MAIN-016, UC-SS-007, UC-SS-008, FR-012, FR-023, FR-024, FR-025, FR-026, NFR-002, NFR-008]
---

# TP-004 — Test plan for FEAT-004 (M3 — Sync & Claim)

## Scope & objectives

Verify the M3 sync-and-claim slice ([FEAT-004](FEAT-004-m3-sync-and-claim.md)): delta sync of claimed
[Groups](../glossary/group.md) and profile preferences across a user's devices, offline editing and
reconciliation, [claiming](../glossary/claim.md) local groups per group, and taking one back out — with
[convergence and data safety](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md)
holding throughout. [Circles](../glossary/circle.md) are out of scope by
[ADR-0033](../adr/0033-which-aggregates-sync.md); the only circle assertion here is the negative one —
that nothing circle-related is written locally for a later push.

Two properties carry nearly all the risk, and get nearly all the testing, for the same reason M2's
plan concentrated on enumeration resistance: **they fail silently**. A group that quietly stops
arriving on the second device, a delete that undeletes itself, an edit that disappears under
last-write-wins, a claim that duplicated a group — none of these throw, and none is visible in a green
run unless a test was written to look for exactly it.

1. **Convergence (NFR-008).** After one sync cycle with no further edits, every device holds the same
   aggregates at the same versions; a seen [tombstone](../glossary/tombstone.md) does not resurrect,
   and it does not re-delete a later re-creation. This is only observable with **two clients**, so the
   harness below is the spine of this plan, not an add-on.
2. **Data safety (NFR-008).** No account action removes local data. Sign-in, claim, un-claim, sign-out
   and account deletion all leave the device's group count unchanged. The **only** thing that removes a
   local group is the user deleting it on purpose. This is asserted with a local-storage **count around
   every account action** — the cheapest possible test for the most expensive possible bug.

The single accepted data loss is **last-write-wins**, and it is bounded and **counted**: every LWW
discard emits a telemetry event. A test asserts the event fires, because — per the M2 lesson carried
into FR-024 — a hook that is designed but not observed leaves its own design comment behind as false
reassurance.

Out of scope: circle sync (ADR-0033), linking a member to a user and creating a group from a circle
(M4), a conflict-resolution UI and continuous background sync (parked, ADR-0016).

## Test levels

- **Unit** — the client sync engine's decision logic with **no I/O**: version bump on local edit, dirty
  tracking, cursor advance, tombstone vs un-claim-marker distinction, the LWW comparison, and the
  claim/un-claim state transitions on the `GroupState` memento. Clock and transport injected; no
  wall-clock waits.
- **Integration — the two-client harness (`AppSuite.Main.Service.Test`)** — **the load-bearing level.**
  Two independent client stores (two `deviceId`s, two cursors) driving **real** sync cycles against the
  API over **real Postgres** ([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)). It
  drives offline edits, concurrent edits, deletes, un-claims, replayed/interrupted pushes and no-op
  cycles, and asserts the **end states** rather than the transport. A single-client change tracker
  cannot express "device B sees device A's change"; two stores are two genuine callers.
- **Contract** — the `/api/v1/sync` envelope in `AppSuite.Main.Contracts` and the group body in
  `AppSuite.SecretSanta.Contracts`: a group round-trips through the server as an **opaque document**
  (Platform never deserializes the Secret-Santa schema), and the envelope stays additive.
- **Repository (`AppSuite.Main.Service.Repository.Test`)** — the `sync` schema mapping against real
  Postgres: per-account `server_seq` monotonicity, the LWW ordering key, marker retention below
  `min(device last_cursor)`, and applying the migration as part of the suite.
- **Architecture** — sync stays **behind the ports**: `SecretSanta.Core` gains no reference to
  sync/HTTP and its aggregate is unchanged; Platform stores app aggregates as opaque documents and does
  not depend on the Secret-Santa domain; no new cross-context dependency.
- **UI/component (bUnit, `AppSuite.Main.Ui.Test` / Secret-Santa pages)** — the claim screen (nothing
  pre-selected; the standing warnings; the assignment-upload warning for a drawn group; the **singular**
  count), the sync-status surface (fact, not error; manual retry), the un-claim confirmation (distinct
  from delete; offline-unavailable), and the offline circle notice.
- **Localization** — DE/EN/FR key parity for every new string, the **placeholder-parity** check M2
  added, and a **count-at-one** test for the claim list (the M2 `"1 Personen"` trap).
- **Exploratory** — the browser walkthrough `CLAUDE.md` requires, in all three languages, with **two
  browser profiles / two devices** — more room for silent divergence than M1/M2, not less.

## Test matrix

Every acceptance-criterion block from FR-023, FR-024, FR-025, FR-026 and NFR-008 maps to at least one
row. Class and method names are **plan-time** and will be reconciled against the code at close (the M2
lesson: a plan-time name is a promise, not evidence).

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-023 same data on a second device | `TwoClientSyncTests.ClaimedGroupOnA_AppearsOnB_WithMembersWishlistsExclusionsAndDraw` | Integration | ✅ |
| FR-023 change on A reaches B | `TwoClientSyncTests.EditOnA_IsHeldByB_AfterItsNextSync` | Integration | ✅ |
| FR-023 language change reaches B | `TwoClientSyncTests.PreferredLanguageChangedOnA_IsAppliedOnB_AfterSync` (profile document) | Integration | ✅ |
| FR-023 delete propagates, no resurrection | `TwoClientSyncTests.GroupDeletedOnA_IsRemovedOnB_AndDoesNotReappear` | Integration | ✅ |
| FR-023 no-op cycle transfers no bodies | `SyncPayloadTests.NoChangeCycle_TransfersNoAggregateBodies_EitherDirection` (asserts request and response body lists empty) | Integration | ✅ |
| FR-023 token expiry mid-sync | `SyncAuthTests.AccessTokenExpiringDuringSync_IsRefreshedTransparently_AndTheSyncCompletes` | Integration | ✅ |
| FR-023 refresh failure signs out, keeps data | `SyncAuthTests.RefreshFailure_SignsTheUserOut_AndRemovesNoLocalData` | Integration | ✅ |
| FR-023 signed-out app stays usable, sends nothing | `SyncOfflineTests.AfterSignOut_TheLocalCopyIsUsable_AndNothingIsSent` | Unit / Integration | ✅ |
| FR-023 / ADR-0033 offline circle notice, no local write | `CirclePageTests.OfflineCircleScreen_SaysItNeedsAConnection`; `SyncOfflineTests.NoCircleDataIsWrittenLocallyForPush` | Component / Unit | ✅ |
| FR-023 account deletion: server copies gone, local kept | `AccountDeletionSyncTests.Deletion_RemovesEveryServerSideGroupCopy_AndLeavesEachLocalCopyAsAccountFree` | Integration | ✅ |
| FR-024 full offline flow persists locally | `SyncOfflineTests.Offline_CreateAddMembersExclusionsDrawReveal_AllSucceedAndPersist` | Integration | ✅ |
| FR-024 offline language change applies + later pushes | `SyncOfflineTests.OfflineLanguageChange_AppliesImmediately_AndPushesOnNextSync` — **closes FR-012's carried criterion** | Integration | ✅ |
| FR-024 pending change pushes on reconnect | `TwoClientSyncTests.PendingChange_IsPushedWithoutTheUserAsking_WhenConnectivityReturns_AndStatusClears` | Integration | ✅ |
| FR-024 replay is idempotent | `SyncIdempotencyTests.AlreadyAppliedPush_ReplayedAfterAnInterruption_ChangesNothingAndDuplicatesNothing` | Integration | ✅ |
| FR-024 offline delete → tombstone propagates | `TwoClientSyncTests.OfflineDelete_PushesATombstone_AndIsGoneOnOtherDevices` | Integration | ✅ |
| **FR-024 / NFR-008 concurrent edit → one survivor + telemetry** | `TwoClientSyncTests.SameGroupEditedOfflineOnBoth_LaterServerTimeSurvivesInFull_OtherDiscardedInFull`; `SyncTelemetryTests.AnLwwDiscard_EmitsACollisionEvent_WithAggregateKindAndId` | Integration | ✅ |
| FR-024 partial push failure isolates the failure | `SyncPartialFailureTests.OneAggregateFailsToPush_TheOthersAreMarkedSynced_AndTheFailedOneStaysQueuedAndRetries` | Integration | ✅ |
| FR-024 queued changes push after same account re-signs-in | `SyncQueueTests.QueuedChanges_ArePushed_WhenTheSameAccountSignsInAgainOnThatDevice` | Integration | ✅ |
| FR-024 queued changes never pushed to a different account | `SyncQueueTests.QueuedChanges_AreNotPushed_WhenADifferentAccountSignsInOnThatDevice` | Integration | ✅ |
| FR-024 refused local write is reported | `SyncStorageTests.WhenLocalStorageRefusesAWrite_TheUserIsToldItWasNotSaved` (ADR-0029) | Unit / Component | ✅ |
| FR-025 first sign-in lists all local groups, none selected | `ClaimPageTests.FirstSignIn_ListsEveryAccountFreeGroupByName_WithNoneSelected` | Component | ✅ |
| FR-025 claim screen states local copy stays / unclaimed live here only | `ClaimPageTests.ClaimScreen_StatesTheLocalCopyStays_AndThatUnclaimedGroupsLiveOnThisDeviceOnly` | Component | ✅ |
| FR-025 drawn group states assignments upload | `ClaimPageTests.ADrawnGroup_StatesItsAssignmentsWillBeUploaded` (NFR-002) | Component | ✅ |
| FR-025 confirming claims the selected, leaves the rest, keeps all local | `ClaimFlowTests.ConfirmingWithTwoOfThreeSelected_ClaimsThoseTwo_LeavesTheThirdLocal_AndKeepsAllThreeOnTheDevice` | Integration | ✅ |
| FR-025 selecting nothing uploads nothing, prompt does not return | `ClaimFlowTests.SelectingNothing_UploadsNothing_AndThePromptDoesNotReappearOnThisDevice` | Integration / Component | ✅ |
| FR-025 later per-group claim has identical outcome | `ClaimFlowTests.ClaimingAnUnclaimedGroupLaterFromItsOwnScreen_HasTheSameOutcomeAsTheFirstSignInPrompt` | Integration | ✅ |
| FR-025 partial upload → arrived updated, not duplicated | `ClaimIdempotencyTests.AfterAFailureFollowingTheFirstOfTwoGroups_ARetryUpdatesTheArrivedGroupRatherThanDuplicatingIt` | Integration | ✅ |
| FR-025 no local groups → no claim screen | `ClaimPageTests.WithNoAccountFreeGroups_NoClaimScreenIsShown` | Component | ✅ |
| FR-025 claimed group's reveal links still decode | `RevealLinkTests.AClaimedGroupsAlreadySharedRevealLinks_DecodeAndDisplayExactlyAsBefore` | Unit | ✅ |
| **FR-025 claim list count reads as singular in DE/EN/FR** | `ClaimPageTests.ClaimList_UsesTheSingularCountString_ForExactlyOneGroup` (DE/EN/FR theory) — the M2 `"1 Personen"` trap | Component / Localization | ✅ |
| FR-026 un-claim: no server copy, still local, stops syncing | `UnclaimTests.RemoveFromAccount_LeavesNoServerCopy_KeepsTheLocalCopyUsable_AndStopsSyncing` | Integration | ✅ |
| FR-026 confirmation distinguishes remove from delete | `UnclaimPageTests.RemovalConfirmation_StatesTheLocalCopyStays_AndDistinguishesRemovalFromDeleting` | Component | ✅ |
| FR-026 un-claim on A → B stops syncing, keeps local | `TwoClientSyncTests.UnclaimOnA_StopsSyncOnB_ButBKeepsItsLocalCopy` (the un-claim marker, not a tombstone) | Integration | ✅ |
| FR-026 drawn group's server assignments deleted | `UnclaimTests.RemovingADrawnGroup_DeletesTheServerSideAssignments` (ADR-0020) | Integration | ✅ |
| FR-026 reveal links unaffected | `RevealLinkTests.AfterUnclaim_PreviouslySharedRevealLinks_DecodeAsBefore` | Unit | ✅ |
| FR-026 re-claim syncs from the claiming device | `ClaimFlowTests.AReRemovedGroup_WhenClaimedAgain_SyncsFromTheClaimingDevicesCopy` | Integration | ✅ |
| FR-026 already removed elsewhere → clear scope, no error | `UnclaimTests.RemovingAGroupAlreadyRemovedElsewhere_ClearsTheLocalScope_AndSurfacesNoError` | Integration | ✅ |
| FR-026 offline → unavailable, reason stated | `UnclaimPageTests.Offline_RemovalIsUnavailable_AndTheReasonIsStated` | Component | ✅ |
| **NFR-008 convergence in one cycle** | `TwoClientSyncTests.AfterOneCycleWithNoFurtherEdits_BothDevicesHoldTheSameAggregatesAtTheSameVersions` | Integration | ✅ |
| **NFR-008 no resurrection past a seen tombstone** | `TwoClientSyncTests.OnceEveryDeviceHasCycled_ADeletedGroupIsAbsentEverywhere_AndDoesNotReappearOnAnyLaterCycle` | Integration | ✅ |
| **NFR-008 replay idempotency after interruption** | `SyncIdempotencyTests.AReplayedSequenceOfPushes_LeavesServerStateIdenticalToASingleCleanPush` | Integration | ✅ |
| **NFR-008 no local loss around any account action** | `LocalDataSafetyTests.NeitherSignInClaimUnclaimSignOutNorAccountDeletion_ChangesTheLocalGroupCount` (count assertion around each) | Integration | ✅ |
| **NFR-008 exactly one survivor + collision event** | covered by `TwoClientSyncTests.SameGroupEditedOfflineOnBoth_...` + `SyncTelemetryTests.AnLwwDiscard_EmitsACollisionEvent_...` above | Integration | ✅ |
| NFR-008 all pending pushed within one cycle | `SyncOfflineTests.AfterBeingOfflineWithPendingChanges_AllPendingAggregatesArePushedWithinOneCycle` | Integration | ✅ |
| NFR-008 write acknowledged with zero awaited network | `SyncLatencyTests.ALocalEdit_IsAcknowledgedWithNoAwaitedNetworkCall` | Unit | ✅ |
| NFR-008 ≤200 aggregates converge in ≤5 s | `SyncPerformanceTests.ASeeded200AggregateAccount_CompletesACycleWithinFiveSeconds` (measured, seeded) | Integration | ⚠️ perf gate |
| Retention: marker pruned only below min device cursor | `SyncRetentionTests.AMarkerIsPruned_OnlyOnceEveryDeviceHasAdvancedPastIt` | Repository | ✅ |
| `server_seq` monotonic per account | `SyncRepositoryTests.ServerSequence_IsMonotonicPerAccount_AcrossConcurrentApplies` | Repository | ✅ |
| Group stored as opaque document | `SyncContractTests.AGroupRoundTripsThroughTheServer_WithoutPlatformDeserializingItsSchema` | Contract | ✅ |
| Sync stays behind the ports | `SyncLayeringTests.SecretSantaCore_DependsOnNoSyncOrHttpType`; `.Platform_DoesNotDependOnTheSecretSantaDomain` | Architecture | ✅ |
| Localization parity + placeholder parity | `LocalizationParityTests.EverySupportedCulture_DefinesEveryNewKey` + `.EveryPlaceholderSurvivesIntoEveryLanguage` | Localization | ✅ |

## Test data & environments

- **Postgres** via Aspire/Testcontainers, fresh database per class, the `sync` migration applied as part
  of the suite.
- **Two client stores minimum** in every convergence test — two `deviceId`s, two cursors, two change
  trackers — with the acting device an explicit parameter, never an ambient default. One store cannot
  express the property under test.
- **Time via injected `TimeProvider`**; `server_modified_at` is the server clock. No wall-clock waits.
- **A seeded 200-aggregate account** for the timing gate.
- **Telemetry captured through a test sink** so the collision event is asserted as emitted, not merely
  logged.

## Localization & accessibility notes

- **Localization:** parity fails if any `de-CH`/`en`/`fr-CH` key is missing or empty; placeholder parity
  fails if a translation drops a `{0}`. The claim list is tested **at one** in all three languages —
  the count that broke M2, and the one a claim screen shows most.
- **Accessibility:** sync status is `role="status"` (a fact), storage-failure and refresh-failure are
  `role="alert"`. The un-claim confirmation is keyboard-reachable, is not the primary style, and states
  its offline-unavailable reason in text, not by colour or a disabled control alone.

## Exit criteria

- [ ] All acceptance criteria of FR-023/024/025/026 and NFR-008 covered by automated tests (gaps, if
      any, recorded here — not silently truncated)
- [ ] The **two-client harness** is green: convergence in one cycle, no resurrection past a seen
      tombstone, replay idempotency, and a local-storage count unchanged around every account action
- [ ] A concurrent edit yields exactly one survivor **and** a collision telemetry event — the hook is
      wired and observed, not a stub
- [ ] The no-op cycle transfers no aggregate bodies; a local edit is acknowledged with no awaited network
- [ ] Un-claim is proven distinct from delete on a second device (marker, not tombstone)
- [ ] Claim is idempotent under partial-upload retry — arrived groups updated, never duplicated
- [ ] FR-012's offline language change closed by an integration test
- [ ] Architecture fitness functions green: sync behind the ports, `SecretSanta.Core` unchanged,
      Platform ignorant of the Secret-Santa schema
- [ ] Localization parity + placeholder parity green, including the claim-list **count at one**
- [ ] Browser walkthrough completed in DE/EN/FR, **two devices / two profiles** — claim, cross-device
      edit, offline reconcile, delete, un-claim, offline circle notice — with a clean console
- [ ] Matrix reconciled against the tests that actually exist, so every row names a real class
- [ ] Suite green locally; **"CI green" carried to milestone 7**, not ticked (no pipeline exists yet)

## Build reconciliation — the two-client harness slice

The load-bearing integration level (§ Test levels) is built and green:
`tests/Apps/Main/AppSuite.Main.Service.Test/Sync/`. Two real client stores
(`SyncClient`, each its own `deviceId` / cursor / change tracker over an in-memory `localStorage`
stand-in) drive the **real** Secret-Santa client engine (`LocalSyncStore` + `SyncingGroupRepository`
+ `SyncService`) against the **real** Main API and Postgres `sync` schema (`TwoClientSyncFactory`).
The collision hook is proven **observed, not stubbed**: the host's `ISyncTelemetry` is swapped for a
`CapturingSyncTelemetry` the tests read.

**Matrix rows now covered by a real class/method** (plan-time names reconciled to code):

| Plan-time row | Actual class.method |
|---------------|---------------------|
| `ClaimedGroupOnA_AppearsOnB_...` | `TwoClientSyncTests.ClaimedGroupOnA_AppearsOnB_WithMembersWishlistsExclusionsAndDraw` |
| `EditOnA_IsHeldByB_...` | `TwoClientSyncTests.EditOnA_IsHeldByB_AfterItsNextSync` |
| `GroupDeletedOnA_...` / no-resurrection | `TwoClientSyncTests.GroupDeletedOnA_IsRemovedOnB_AndDoesNotReappear`, `.OnceEveryDeviceHasCycled_ADeletedGroupIsAbsentEverywhere_AndDoesNotReappearOnAnyLaterCycle` |
| convergence in one cycle | `TwoClientSyncTests.AfterOneCycleWithNoFurtherEdits_BothDevicesHoldTheSameAggregatesAtTheSameVersions` |
| pending change pushed on reconnect | `TwoClientSyncTests.PendingChange_IsPushedWithoutTheUserAsking_WhenConnectivityReturns_AndStatusClears` |
| offline delete → tombstone | `TwoClientSyncTests.OfflineDelete_PushesATombstone_AndIsGoneOnOtherDevices` |
| concurrent edit → one survivor | `TwoClientSyncTests.SameGroupEditedOfflineOnBoth_LaterServerTimeSurvivesInFull_OtherDiscardedInFull` |
| LWW discard emits collision event | `SyncTelemetryTests.AnLwwDiscard_EmitsACollisionEvent_WithAggregateKindAndId` |
| no-op cycle transfers no bodies | `SyncPayloadTests.NoChangeCycle_TransfersNoAggregateBodies_EitherDirection` |
| replay idempotency (both rows) | `SyncIdempotencyTests.AlreadyAppliedPush_ReplayedAfterAnInterruption_ChangesNothingAndDuplicatesNothing`, `.AReplayedSequenceOfPushes_LeavesServerStateIdenticalToASingleCleanPush` |
| all pending pushed within one cycle | `SyncOfflineTests.AfterBeingOfflineWithPendingChanges_AllPendingAggregatesArePushedWithinOneCycle` |
| offline flow persists locally | `SyncOfflineTests.OfflineWithNoCycle_CreateAddMembersAndDraw_AllPersistLocally_AndSendNothing` |
| no local loss around account actions (subset) | `LocalDataSafetyTests.NeitherTheInitialReconcileClaimNorASubsequentCycle_ChangesTheLocalGroupCount`, `.AGroupClaimedOnAnotherDevice_ArrivingHere_AddsToButNeverReplacesTheLocalGroups` |

**Gaps recorded, not silently truncated** — these ride routes/slices not yet built and are *not*
covered by this slice:

- **Account-deletion sync** (`AccountDeletionSyncTests.*`): needs deletion to cascade the `sync`
  store. The `LocalDataSafetyTests` account-deletion count is therefore deferred with it.
- **Claim-flow / claim-page / un-claim-page component rows** (`ClaimFlowTests.*`,
  `ClaimIdempotencyTests.*`, `ClaimPageTests.*`): the claim **screen** and the dedicated
  `POST …/claim` route land with the UI slice. This slice claims by scoping a local group and letting
  the normal cycle push it (`SyncClient.ClaimAsync`), which exercises every convergence path a claimed
  group rides but not the claim UI or its idempotent partial-retry route.
- **Un-claim *page* row** (`UnclaimPageTests.*`): the confirmation UI — distinct from delete,
  offline-unavailable reason — lands with the claim/un-claim screen slice. The un-claim *behaviour* it
  will drive is built and covered (below).
- **Auth mid-sync** (`SyncAuthTests.*`): token-refresh-during-sync and refresh-failure sign-out.
- **Perf gate** (`SyncPerformanceTests.*`, ⚠) and **localization parity** (`LocalizationParityTests.*`).

## Build reconciliation — the un-claim slice (FR-026)

The `DELETE /api/v1/sync/groups/{id}` route and its **un-claim marker** are now built: the server
deletes the account-scoped copy (its in-body assignments with it, ADR-0020) and records an un-claim
marker — never a tombstone — that the delta exchange already returned. The client engine gains
`SyncService.UnclaimAsync`: it calls the server first and drops the local account scope only on
confirmation, keeping the local copy; an unreachable server leaves everything untouched (FR-026
requires a connection). A standing tombstone for the same id **outranks** an un-claim, so a delete is
never downgraded into a keep-the-copy signal (no resurrection, NFR-008).

**Matrix rows now covered by a real class/method** (plan-time names reconciled to code):

| Plan-time row | Actual class.method |
|---------------|---------------------|
| un-claim: no server copy, still local, stops syncing | `UnclaimTests.RemoveFromAccount_LeavesNoServerCopy_KeepsTheLocalCopyUsable_AndStopsSyncing` |
| drawn group's server assignments deleted | `UnclaimTests.RemovingADrawnGroup_DeletesTheServerSideAssignments` |
| already removed elsewhere → clear scope, no error | `UnclaimTests.RemovingAGroupAlreadyRemovedElsewhere_ClearsTheLocalScope_AndSurfacesNoError` |
| un-claim on A → B stops syncing, keeps local | `TwoClientSyncTests.UnclaimOnA_StopsSyncOnB_ButBKeepsItsLocalCopy` |
| re-claim syncs from the claiming device | `TwoClientSyncTests.ReclaimAfterUnclaim_SyncsFromTheClaimingDevicesCopy` |
| un-claim store behaviour, idempotency, tombstone precedence | `SyncStoreTests.Unclaiming_DeletesTheServerCopy_AndRecordsAnUnclaimMarkerNotATombstone`, `.Unclaiming_IsIdempotent_AndSurfacesNoError`, `.AStandingTombstone_IsNotDowngradedByAnUnclaim`, `.AReclaimAfterAnUnclaim_RecreatesTheAggregate_AndClearsTheStaleMarker` |
| route wired, bearer-authorized | `SyncEndpointTests.Unclaim_WithoutABearerToken_Is401`, `.Unclaim_RemovesTheServerCopy_AndPropagatesAnUnclaimMarker` |
| client un-claim decision logic (offline, idempotent, scope drop) | `SyncServiceTests.Unclaim_CallsTheServerFirst_ThenDropsTheScope_KeepingTheLocalCopy`, `.Unclaim_WhenTheServerIsUnreachable_ChangesNothingLocally`, `.Unclaim_IsIdempotent_WhenTheGroupIsAlreadyAccountFree` |

Still deferred to the UI slice: the un-claim **confirmation screen** (`UnclaimPageTests.*`).

**Finding surfaced by the harness — replay counted as a collision.** `SyncStore.ApplyChangeAsync`
treats any push whose version is `<=` the stored version as a last-write-wins collision, including a
byte-identical **replay** of an already-applied push (the interrupted-cycle case
`SyncIdempotencyTests` reproduces). Convergence and no-duplication hold, so the suite is green — but
the replay still emits a collision telemetry event, inflating the very counter the parked conflict UI
is meant to be parked *on*. Suggested guard: when `change.Version == existing.Version` **and** the
body is unchanged, treat it as an idempotent no-op (no collision, no counter increment); keep the
collision path only for a same-or-lower version with a *different* body. Not fixed in this slice —
flagged for a decision, with a ready assertion to add once guarded.
