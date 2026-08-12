---
id: TP-005
feature: FEAT-005
status: Reviewed
related: [UC-SS-009, UC-SS-010, UC-SS-011, UC-SS-012, FR-027, FR-028, FR-029, FR-030, FR-015, FR-019, FR-022, NFR-001, NFR-002, NFR-005, NFR-006]
---

# TP-005 — Test plan for FEAT-005 (M4 — Linking)

## Scope & objectives

Verify the M4 linking slice ([FEAT-005](FEAT-005-m4-linking.md)): creating a
[Group](../glossary/group.md) from a [Circle](../glossary/circle.md) as a snapshot, linking a
[Member](../glossary/member.md) to a [User](../glossary/user.md) with consent and unlinking cleanly,
a signed-in linked member seeing **only their own** [Assignment](../glossary/assignment.md), and a
linked member owning their own [Wishlist](../glossary/wishlist.md) — with the account-free product and
every unlinked member **completely unaffected** (NFR-001).

One property carries nearly all the risk and gets nearly all the testing, for the same reason M3's plan
concentrated on convergence and M2's on enumeration resistance: **it fails silently, and it is the
sharpest failure mode in the whole suite.**

- **Assignment authorization ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md),
  [ADR-0020](../adr/0020-server-side-assignment-privacy.md)).** A signed-in member may read **only their
  own** pairing. A request for anyone else's is **denied — not filtered to an empty result, denied.** A
  non-organizer response **never** contains more than one assignment. This is only observable with **two
  accounts** (an organizer and a linked member who is *not* the member they probe for), so the
  two-account harness below is the spine of this plan, not an add-on. The account-free design had nothing
  to leak; here a single wrong rule leaks the exact secret the app exists to keep — so the cross-member
  **deny** is a first-class assertion, verified by authorization tests, not a happy-path corollary.

Two more properties fail quietly and are tested for directly:

- **Snapshot integrity (FR-027).** A group created from a circle is a **copy**. A later change to the
  circle, and the circle's **deletion**, must leave the group unchanged and openable — the dangling
  origin reference is never dereferenced. This closes FR-019's and FR-022's carried Secret-Santa halves,
  and a green happy-path says nothing about the deletion case.
- **Graceful degradation on unlink and account deletion (FR-028, FR-015).** Unlinking, and a linked user
  **deleting their account**, must revert the member to unlinked — the member, its name and its wishlist
  **survive** and the group opens. A dangling `UserId` that throws instead of degrading is a broken group
  for everyone in it.

The single accepted staleness window is the **reveal-link snapshot** (ADR-0014): a giver holding an
already-issued link sees the wishlist as it was at generation. In-app reveal is always current. A test
asserts both halves and that the member is **told** (FR-030), because a self-owned wishlist that silently
fails to reach a link-holding giver is exactly the trap FR-030 calls out.

Out of scope: circle sync (ADR-0033 — only the negative assertion, that no circle data is written
locally), notifications (parked), per-member envelope encryption (ADR-0020 P3, future hardening), a
conflict-resolution UI and continuous background sync (parked, ADR-0016).

## Test levels

- **Unit** — pure decision logic, **no I/O**: the create-from-circle **mapping** (accounted person →
  linked member with `UserId`; managed member / no account → unlinked; origin id recorded; the
  duplicate-account guard picks exactly one to link); the rule that **linking/unlinking does not
  invalidate a completed draw** while editing members does; wishlist ownership resolution from link
  state; empty-item drop. Clock and transport injected.
- **Integration — the two-account authorization harness (`AppSuite.Main.Service.Test`)** — **the
  load-bearing level.** An **organizer** account and a **linked-member** account (distinct bearer
  tokens) driving the **real** member-link, assignment and self-wishlist surface against **real
  Postgres** ([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)). It asserts **end states
  and status codes**: my-assignment denied (not empty) for a non-member; the one-assignment cap for a
  non-organizer; the current-not-stale pairing after a re-run; the consent flow (send → accept →
  linked; decline/expire → unlinked); the one-user-per-group guard; and the account-deletion cascade.
  One account cannot express "a member reads only their own"; two are two genuine callers.
- **Contract** — the member-link, assignment and self-wishlist DTOs in
  `AppSuite.SecretSanta.Contracts`, and that the **generic M3 sync store stays opaque** (the group body
  still round-trips without the server deserializing it) while the new Secret-Santa surface is the
  **only** place the Main host understands the schema.
- **Repository (`AppSuite.Main.Service.Repository.Test`)** — the `linking` schema against real Postgres:
  the assignment row keyed for a single authorized lookup by giver account, `draw_version` replacement
  on re-run, the member-link lifecycle (expiry, revoke), and account-deletion removing the caller's
  assignment/wishlist rows.
- **Architecture** — `SecretSanta.Core` gains **only** `UserId?` and `OriginCirclePersonId?` and
  references **neither** Identity nor Circles; the host maps circle DTOs to member creation, not the
  domain; the assignment/link surface does not leak into the generic sync store.
- **UI/component (bUnit)** — the create-from-circle picker (everyone pre-selected; the **singular**
  count; the duplicate-account notice); the link/unlink flow (*request sent* / *link pending*; the
  managed-member refusal offering promotion; unlink keeps the wishlist); the assignment view behind the
  **anti-spoiler** step; the wishlist editor with the **reveal-link caveat**.
- **End-to-end / exploratory** — the browser walkthrough `CLAUDE.md` requires, in all three languages,
  with **two accounts / two devices** — the only place the cross-account flow is seen whole and on
  screen (the reveal-page precedent: real defects here were invisible to a green suite).

## Test matrix

Every acceptance-criterion block from FR-027, FR-028, FR-029, FR-030 maps to at least one row. Class and
method names are **plan-time** and will be reconciled against the code at close (the M2 lesson: a
plan-time name is a promise, not evidence).

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| **FR-029 request for another's assignment is denied, not filtered** | `AssignmentAuthorizationTests.ARequestForAnotherMembersAssignment_Is403_NotAnEmptyResult` | Integration | ✅ |
| **FR-029 non-organizer response never carries >1 assignment** | `AssignmentAuthorizationTests.ANonOrganizerResponse_ContainsAtMostOneAssignment` | Integration | ✅ |
| FR-029 linked member sees own recipient + wishlist + budget + event | `AssignmentReadTests.ALinkedMember_SeesTheirOwnRecipientWishlistBudgetAndEvent` | Integration | ✅ |
| FR-029 recipient wishlist shown is the **current** one | `AssignmentReadTests.TheRecipientsCurrentWishlist_IsShown_NotADrawTimeSnapshot` | Integration | ✅ |
| FR-029 re-run shows current pairing, never the previous | `AssignmentReadTests.AfterAReDraw_TheLinkedMemberSeesTheNewPairing_AndNeverThePrevious` | Integration | ✅ |
| FR-029 draw not run → nothing shown, nothing stored | `AssignmentReadTests.BeforeTheDraw_NoAssignmentIsShown_AndNoneIsStored` | Integration | ✅ |
| FR-029 linked in several groups → one per group, each gated | `AssignmentReadTests.AUserLinkedInThreeGroups_GetsEachAssignment_BehindItsOwnRevealStep` | Integration / Component | ✅ |
| FR-029 offline: own assignment readable, no other on device | `AssignmentOfflineTests.OfflineTheOwnAssignmentIsReadable_AndNoOtherMembersIsPresentOnTheDevice` | Integration | ✅ |
| FR-029 anti-spoiler step gates the reveal | `AssignmentViewTests.TheRecipientIsHiddenBehindADeliberateRevealStep` | Component | ✅ |
| FR-029 unlinked member: in-app assignment disappears, no error | `AssignmentReadTests.WhenUnlinked_TheInAppAssignmentDisappears_WithoutError` | Integration | ✅ |
| FR-029 labels localized, wishlist untranslated (DE/EN/FR) | `AssignmentViewTests.LabelsAreLocalized_AndTheFreeTextWishlistIsShownUntranslated` | Component / Localization | ✅ |
| FR-027 create from circle → one member per person, all pre-selected | `CreateFromCircleTests.CreatesOneMemberPerChosenPerson_WithEveryoneSelectedByDefault` | Unit / Integration | ✅ |
| FR-027 accounted person → linked; managed / none → unlinked | `CreateFromCircleMappingTests.AnAccountedPersonBecomesLinked_AManagedMemberBecomesUnlinked` | Unit | ✅ |
| **FR-027 later circle change does not alter the group** | `CreateFromCircleSnapshotTests.AddingOrRemovingAPersonInTheCircleLater_LeavesTheGroupUnchanged` | Integration | ✅ |
| **FR-027 circle deletion → group still opens, ref not dereferenced** | `CreateFromCircleSnapshotTests.AfterTheOriginCircleIsDeleted_TheGroupOpens_AndTheDanglingReferenceIsNotDereferenced` | Integration | ✅ |
| FR-027 completed draw unchanged when circle changes | `CreateFromCircleSnapshotTests.AGroupsCompletedDraw_IsUnchangedWhenItsOriginCircleChanges` | Integration | ✅ |
| FR-027 managed member copied in → unaffected by later removal | `CreateFromCircleSnapshotTests.AManagedMemberCopiedIn_IsUnaffectedByRemovalFromTheCircle` (FR-019 half) | Integration | ✅ |
| **FR-027 two people → same account → one linked, other unlinked + told** | `CreateFromCircleTests.TwoPeopleResolvingToTheSameAccount_LinkOnlyOne_AndTheUserIsInformed` | Unit / Component | ✅ |
| FR-027 group is account-scoped and syncs without a separate claim | `CreateFromCircleTests.TheNewGroup_IsAccountScopedAndSyncs_WithNoSeparateClaimStep` | Integration | ✅ |
| FR-027 any circle member may create from it (authorized read) | `CreateFromCircleAuthTests.AnyCircleMember_MayCreateAGroupFromIt_AsAnAuthorizedRead` (NFR-006) | Integration | ✅ |
| **FR-027 circle-picker count reads as singular in DE/EN/FR** | `CirclePickerTests.PeopleCount_UsesTheSingularAtOne_InEveryLanguage` (DE/EN/FR theory) — the M2 `"1 Personen"` trap | Component / Localization | ✅ |
| FR-028 send link request → "request sent", reveals nothing | `MemberLinkTests.SendingALinkRequest_Returns202RequestSent_WithoutRevealingAccountExistence` (NFR-005) | Integration | ✅ |
| FR-028 accept establishes the link, wishlist ownership moves | `MemberLinkTests.OnAcceptance_TheMemberBecomesLinked_AndWishlistOwnershipMovesToThatUser` | Integration | ✅ |
| **FR-028 decline and expiry are indistinguishable to the organizer** | `MemberLinkEnumerationTests.DeclineAndExpiry_AreIndistinguishableToTheOrganizer` (NFR-005) | Integration | ✅ |
| FR-028 self-link is auto-accepted, no second step | `MemberLinkTests.AnOrganizerLinkingTheirOwnAccount_IsAutoAccepted` | Integration | ✅ |
| **FR-028 one user, one member per group** | `MemberLinkGuardTests.ARequestLinkingAUserAlreadyLinkedInThatGroup_IsRefused` | Integration | ✅ |
| FR-028 managed member cannot be linked directly, promotion offered | `MemberLinkGuardTests.LinkingAManagedMemberDirectly_IsRefused_AndPromotionIsOffered` (ADR-0018) | Integration / Component | ✅ |
| FR-028 unlink reverts, wishlist retained under organizer, read lost | `MemberLinkTests.Unlinking_RevertsToUnlinked_KeepsTheWishlist_AndRemovesTheUsersRead` | Integration | ✅ |
| **FR-028 / FR-015 account deletion cascades to a graceful unlink** | `AccountDeletionLinkTests.DeletingALinkedUsersAccount_UnlinksEveryReferencingMember_TheMemberAndWishlistSurvive_AndTheGroupOpens` | Integration | ✅ |
| FR-028 mixed group: unlinked members still get reveal links | `MemberLinkTests.InAMixedGroup_UnlinkedMembersReceiveRevealLinksExactlyAsBefore` (NFR-001) | Integration | ✅ |
| FR-028 link request write failure reported, member stays unlinked | `MemberLinkTests.WhenTheLinkRequestWriteFails_ItIsReported_AndTheMemberStaysUnlinked` (ADR-0029) | Unit / Component | ✅ |
| FR-030 add/edit/reorder/remove ideas saved and synced | `MyWishlistTests.EditsToOwnWishlist_AreSavedAndSynced` | Integration | ✅ |
| FR-030 empty item ignored | `MyWishlistTests.AnEmptyItem_IsIgnoredAndNotSaved` | Unit | ✅ |
| FR-030 linked user can edit; organizer sees but cannot edit | `MyWishlistOwnershipTests.OnLink_TheUserCanEdit_AndTheOrganizerCanSeeButNotEdit` | Integration | ✅ |
| FR-030 unlink → ownership returns to organizer, content intact | `MyWishlistOwnershipTests.OnUnlink_OwnershipReturnsToTheOrganizer_WithContentUnchanged` | Integration | ✅ |
| FR-030 offline edits reconciled last-write-wins | `MyWishlistOfflineTests.OfflineEdits_AreReconciledLastWriteWins` (FR-024) | Integration | ✅ |
| FR-030 two groups → two independent wishlists | `MyWishlistTests.AUserLinkedInTwoGroups_HasTwoIndependentWishlists` | Integration | ✅ |
| FR-030 new membership offers to copy a previous wishlist | `MyWishlistTests.JoiningANewGroup_OffersToCopyAPreviousWishlist` | Component | ✅ |
| **FR-030 old reveal link stays a snapshot; member told in advance** | `MyWishlistSnapshotTests.AWishlistEditedAfterALinkWasIssued_LeavesThatLinkShowingTheOldText_AndTheMemberWasToldInAdvance` (ADR-0014) | Integration / Component | ✅ |
| FR-030 editor localized (DE/EN/FR); content untranslated | `MyWishlistTests.TheEditorIsLocalized_AndContentIsStoredAndShownUntranslated` | Component / Localization | ✅ |
| Assignment publication runs on draw **and** re-run (replace) | `AssignmentPublicationTests.PublishingRunsOnDrawAndReRun_AndReplacesPriorRows` | Integration | ✅ |
| Backfill: pre-M4 drawn group gets rows on first post-M4 sync | `AssignmentPublicationTests.APreM4DrawnGroup_GetsItsAssignmentRowsBackfilled_OnTheFirstSync` | Integration | ✅ |
| Group stored opaquely; only the new surface understands the schema | `LinkingContractTests.TheGroupStaysAnOpaqueDocument_WhileTheAssignmentSurfaceIsTheOnlySchemaAwarePoint` | Contract / Architecture | ✅ |
| Core gains only UserId?/OriginCirclePersonId?, no Identity/Circles ref | `LinkingLayeringTests.SecretSantaCore_ReferencesNeitherIdentityNorCircles` | Architecture | ✅ |
| No circle data written locally for a later push | `CreateFromCircleTests.NoCircleDataIsWrittenLocally` (ADR-0033) | Unit | ✅ |
| Localization parity + placeholder parity for every new key | `LocalizationParityTests.EverySupportedCulture_DefinesEveryNewKey` + `.EveryPlaceholderSurvivesIntoEveryLanguage` | Localization | ✅ |

## Test data & environments

- **Postgres** via Aspire/Testcontainers, fresh database per class, the `linking` migration applied as
  part of the suite.
- **Two accounts minimum** in every authorization test — an organizer and a linked member — with the
  acting account an explicit parameter, never an ambient default, plus a **third** identity for the
  cross-member **deny** case (a signed-in user who is *not* the member being probed). One account
  cannot express the property under test.
- **A managed-member fixture** for the direct-link refusal and the promotion path.
- **Time via injected `TimeProvider`** for member-link expiry; no wall-clock waits.
- **Mail through the outbox / test transport** (as M2) so the enumeration-resistant "request sent" is
  asserted without a live provider; decline and expiry captured as indistinguishable.
- **A pre-M4 drawn-and-claimed group fixture** for the backfill row.

## Localization & accessibility notes

- **Localization:** parity fails if any `de-CH`/`en`/`fr-CH` key is missing or empty; placeholder parity
  fails if a translation drops a `{0}`. The circle picker is tested **at one** in all three languages —
  the count that broke M2 and the one a picker shows most. The reveal-link caveat and the operator-readable
  privacy line are asserted present in all three.
- **Accessibility:** the anti-spoiler reveal is a keyboard-reachable deliberate action, not a hover; a
  denied assignment surfaces nothing about another member (no leak via error text); *request sent* /
  *link pending* are `role="status"` (facts), write failures `role="alert"`; the managed-member refusal
  states the promotion path in text, not by a disabled control alone.

## Exit criteria

- [ ] All acceptance criteria of FR-027/028/029/030 covered by automated tests (gaps, if any, recorded
      here — not silently truncated)
- [ ] The **two-account authorization harness** is green: a cross-member assignment request is
      **denied, not filtered**; a non-organizer response carries **at most one** assignment; a re-run
      shows the current pairing and never the previous; the offline cache holds only the caller's own
- [ ] Snapshot integrity proven: a later circle change and the circle's **deletion** leave the group
      unchanged and openable (FR-019 and FR-022 carried halves closed)
- [ ] The account-deletion cascade is wired and asserted — member, name and wishlist survive, the group
      opens (FR-015); not a stub
- [ ] Assignment publication runs on draw **and** re-run (replace) and **backfills** pre-M4 drawn groups
- [ ] The member-link request is enumeration-resistant: "request sent" reveals nothing, decline and
      expiry are indistinguishable (NFR-005); the one-user-per-group and managed-member guards hold
- [ ] Wishlist ownership moves on link and returns on unlink with content intact; an old reveal link
      stays a snapshot and the member was told (FR-030)
- [ ] Architecture fitness functions green: `SecretSanta.Core` references neither Identity nor Circles;
      the group stays an opaque document; the schema-aware assignment/link surface is isolated
- [ ] Localization parity + placeholder parity green, including the circle-picker **count at one**
- [ ] Browser walkthrough completed in DE/EN/FR, **two accounts / two devices** — create from circle,
      link (send + accept, and a self-link), draw, linked member sees only their own assignment behind
      the anti-spoiler step, member edits their own wishlist, giver sees it in-app, unlink (wishlist
      kept), delete the linked account (member survives, group opens) — with a clean console
- [ ] Matrix reconciled against the tests that actually exist, so every row names a real class
- [ ] Suite green locally
- [ ] **"CI green" carried to milestone 7** — no pipeline exists yet
