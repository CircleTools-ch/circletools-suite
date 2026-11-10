---
id: TP-003
feature: FEAT-003
status: Reviewed
related: [UC-MAIN-008, UC-MAIN-009, UC-MAIN-010, UC-MAIN-011, UC-MAIN-012, UC-MAIN-013, UC-MAIN-014, FR-015, FR-016, FR-017, FR-018, FR-019, FR-020, FR-021, FR-022, NFR-003, NFR-004, NFR-005, NFR-006]
---

# TP-003 — Test plan for FEAT-003 (M2 — Circles)

## Scope & objectives

Verify the M2 circles slice ([FEAT-003](FEAT-003-m2-circles.md)): creating a circle with its label,
inviting by email without leaking account existence, both response paths, managed members and their
promotion, roles / removal / ownership transfer, leaving, and deletion — plus the cross-cutting
[NFR-006](../requirements/non-functional/NFR-006-circle-authorization.md) and the invite form's share
of [NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md).

Three areas carry disproportionate risk and get disproportionate testing:

1. **Authorization (NFR-006).** This is the first data in the suite that belongs to *other people*.
   The matrix is seven use cases × three roles × non-member, and its failure mode — one endpoint that
   forgot a check — is invisible to every functional test that only ever calls it as an owner.
2. **Enumeration resistance on the invite form.** ADR-0018 paid real UX for it, and M1 showed the
   leak is usually **timing**, not wording: two endpoints there answered 20× faster on the branch
   with less work to do. The invite form has the same asymmetry — an address with an account takes a
   different path from one without.
3. **The aggregate invariants.** "Exactly one owner", "a managed member is never an admin", "one
   pending invitation per address", the caps, and an atomic transfer. These are cheap to drive
   exhaustively at unit level and expensive to discover in production.

Out of scope: creating a Secret-Santa group from a circle (M4), sync and offline circles (M3),
notifications of any kind (not built).

## Test levels

- **Unit (`CircleTools.Circles.Core.Test`)** — the `Circle` aggregate and the handlers, with no I/O:
  role transitions, the ownership invariant, transfer atomicity, managed-member rules, promotion by
  linking, invitation lifecycle and the caps. Clock injected via `TimeProvider`; **no wall-clock
  waits** for the 14-day expiry or the 7-day cooldown.
- **Integration (`CircleTools.Main.Service.Test`)** — endpoints against **real Postgres** and **real
  SMTP** (Mailpit, [ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)), asserting on mail
  actually delivered rather than on a mocked sender, as M1 does.
- **Authorization (role matrix)** — one parameterised suite over **every** circle route × {owner,
  admin, member, non-member, anonymous}, asserting the permitted *and* the refused outcome, plus that
  a non-member's response is byte-identical to an unknown circle's.
- **Security / timing** — paired enumeration tests for `POST /invitations`: an address with an
  account, one without, and one already in the circle must be indistinguishable in body, status and
  timing distribution.
- **Repository (`CircleTools.Circles.Repository.Test`)** — the `circles` schema mapping, the optimistic
  concurrency token, cascade behaviour on deletion, the retention sweeps and the three invitation
  rate limits, against real Postgres. Applying the migration is itself part of the suite: `xmin` is a
  system column that cannot be created, and every test here fails at startup if the provider ever
  stops filtering it out of the generated SQL. Two providers per test are two concurrent callers —
  the concurrency assertions are worthless against a single change tracker.
- **Architecture** — `Circles.Core` free of ASP.NET/EF; no Secret-Santa project references Circles;
  no Circles project references `CircleTools.Identity`; plus the two NFR-006 fitness functions.
- **UI/component (bUnit, `CircleTools.Main.Ui.Test`)** — the circle pages over a stub transport, as M1's
  account pages are. The themes: the deliberately uninformative invite confirmation, the destructive
  confirmations, and role-dependent rendering.
- **Localization** — key parity across `de-CH`/`en`/`fr-CH`, plus the invitation mail template.
- **Exploratory** — the browser walkthrough `CLAUDE.md` requires, in all three languages. M1's
  walkthrough found six defects every automated level passed; this one has two accounts and two
  browsers in play, which is more, not less, room for that.

## Test matrix

**Reconciled against the code on 2026-07-30.** Every class and method named below exists; the
plan-time names that drifted are noted in the rows where the difference is worth knowing, and the two
rows that needed something *built* rather than renamed are marked. What the reconciliation found, and
what it cost, is in [the closing notes](#what-the-reconciliation-found-2026-07-30).

Where a row sits at a different **level** than the plan asked for, that is stated rather than
smoothed over: several properties the plan expected at integration level are enforced in the
aggregate and are tested there, because that is where they are decided.

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-016 create + owner membership | `CircleTests.Create_MakesTheCreatorTheSoleOwner`; `HandlerTests.CreateCircle_MakesTheCallerTheOwner_AndDefaultsTheLabelToTheirLanguage`; `CircleEndpointsTests.Create_ReturnsTheCircleWithItsOwner` + `.Create_ThenTheCircleIsInTheOwnersList` | Unit / Integration | ✅ |
| FR-016 label defaults per language | `CircleTests.Create_EmptyLabel_FallsBackToTheLanguageDefault` (DE/EN/FR + no language, as a theory); `CirclePageTests.MyCircles_PrefillsTheLabelForTheCurrentLanguage` | Unit / Component | ✅ |
| FR-016 the label is user data | `CircleEndpointsTests.Label_IsNotRetranslatedForAViewerInAnotherLanguage`; `CircleTests.Create_KeepsTheLabelItWasGiven` + `.Update_WithABlankLabel_KeepsTheStoredOne`; `CirclePageTests.MyCircles_ShowsTheStoredLabel_NotOneLookedUpForTheViewer`; `MailTemplateRendererTests.Invitation_DoesNotRetranslateTheCirclesLabel` — the rule holds in the API, the aggregate, the page **and** the mail | Integration / Unit / Component | ✅ |
| FR-016 name required | `CircleTests.Create_BlankName_IsRefused` | Unit | ✅ |
| FR-016 the list's size fragment reads correctly at one | `CirclePageTests.MyCircles_UsesTheSingularSizeString_OnlyForACircleOfOne`. **Found on the screen** (2026-07-30), the same defect as FR-022's count sentence and in a worse place: a circle just created holds one person, so the first circle a new user ever sees was labelled *"1 Personen" / "1 people" / "1 personnes"*. Singular is now its own entry (`CirclesPeopleCountOne`) | Component | ✅ |
| FR-016 verified address required | `CircleEndpointsTests.Create_UnverifiedAccount_IsRefusedWithAResendOffer` | Integration | ✅ |
| FR-016 atomicity | `CircleRepositoryTests.FailedCreate_LeavesNeitherCircleNorMembership` | Repository | ✅ |
| FR-016 / FR-019 the mapping is faithful | `CircleRepositoryTests.Circle_SurvivesARoundTripWholeWithEveryPersonInvitationAndDecline` — the aggregate reloaded from Postgres in a second context, managed member and all; `.Queries_FindCirclesByMembership_Ownership_InvitationHandleAndInvitedAddress` | Repository | ✅ |
| FR-016 caps | `CircleTests.Create_BeyondTheOwnedCircleCap_IsRefused`; `HandlerTests.CreateCircle_BeyondTheDailyRate_IsRefused` — *the plan put the daily rate at integration level* (`RateLimitTests`); it is a handler decision over the repository's own count, and it is driven there. The integration factory raises both creation limits so `CircleRoleMatrixTests` can build a circle per row, which is why they cannot also be asserted through the API | Unit | ✅ |
| **FR-017 identical response** | `InvitationTimingTests.Invite_NoAccount_WithAccount_AndAlreadyAMember_AreIndistinguishable` (timing, three interleaved arms, compared on the fastest sample each managed) **and** `InvitationMailTests.Invite_AnswersIdentically_WhetherTheAddressHasAnAccountIsUnknownOrIsAlreadyInTheCircle` (status and body, same three arms). *Renamed from the plan's `InvitationEnumerationTests`, and deliberately two tests:* timing needs many samples and a statistic, parity needs one call each and an exact comparison — one test doing both would have to be lenient about one of them | Security | ✅ |
| FR-017 resend rather than duplicate | `CircleTests.Invite_SameAddressTwice_ResendsTheExistingInvitation` + `.Resend_IssuesAFreshSecretAndKillsThePreviousLink` | Unit | ✅ |
| FR-017 revoke | `CircleTests.Revoke_DeletesTheInvitation_SoItsLinkStopsWorkingAndTheAddressIsFreeAgain`, `.Revoke_AnInvitationThatIsAlreadyGone_IsRefusedRatherThanSilentlyAccepted`, `.Revoke_ByAMember_IsRefused`; the route's own authorization in `CircleRoleMatrixTests` (`DELETE …/invitations/{iid}`). *Written at close:* the plan named an `InvitationEndpointsTests` that was never built, and revoke had no functional test at all — only its route filter | Unit / Authorization | ✅ |
| FR-017 expiry (14 days) | `CircleTests.Invitation_PastItsExpiry_LeavesThePendingListAndFreesTheAddress` (virtual clock) | Unit | ✅ |
| FR-017 role-only-member | `CircleTests.Invitation_NeverGrantsMoreThanMember` | Unit | ✅ |
| FR-017 rate limits | `InvitationRateLimiterTests` (Repository, real Postgres) — per inviter (`AnInviter_IsStoppedAfterTwentyInvitationsInAnHour_AndToldWhenTheyMayContinue`, `.AnInviter_IsStoppedAfterSixtyInvitationsInADay`), **per recipient address** (`.AnAddress_ReceivesNoMoreThanThreeInvitationsADay_WhoeverSendsThem`), the rolling window (`.TheHourlyWindow_Rolls_RatherThanResettingOnTheHour`), that a refused attempt is not counted, and that one inviter's usage does not count against another; the handler's side in `HandlerTests.Invite_WhenTheLimiterRefuses_IsRefusedAndSendsNothing` + `.Invite_ChargesTheRateLimiterEvenWhenItSendsNothing`; the per-circle 50-pending cap in `CircleTests.PendingInvitations_AreCappedPerCircle`. *The plan's `InvitationRateLimitTests` (integration) does not exist:* the endpoint's relay of `Retry-After` is asserted by the limiter and the handler, and the integration factory has to raise these limits to test anything else | Repository / Unit | ✅ |
| FR-017 delivery failure is not reported | Structural, and asserted as such: `OutboxInvitationMailerTests.QueueAsync_EnqueuesOneInvitationMessage` + `.WhatIsQueued_IsWhatTheTemplateCanRender` — the port enqueues and returns, so the endpoint has already answered before any delivery is attempted and has nothing to report. `MailDeliveryTests.Dispatcher_MarksMessagesProcessed_SoTheyAreNotSentTwice` covers the dispatch side. A **real SMTP failure** is not exercised — see gap 3 | Unit / Integration | ⚠️ partial |
| FR-017 mail language | `InvitationMailTests.Mail_UsesTheRecipientsAccountLanguage_OtherwiseTheInvitersUsingMailpit` (delivered messages); `HandlerTests.Invite_QueuesOneMailInTheRecipientsLanguage_WhenTheAddressHasAnAccount` + `.Invite_UsesTheInvitersLanguage_WhenTheAddressHasNoAccount` | Integration / Unit | ✅ |
| FR-017 dispatch is queued | `OutboxInvitationMailerTests.QueueAsync_EnqueuesOneInvitationMessage` + `.QueueAsync_CarriesTheChosenLanguage_AndLeavesNullToTheRenderer` + `.QueueAsync_RecordsWhetherTheRecipientHasAnAccount` — the structural half of the timing property, at the port rather than the endpoint | Unit | ✅ |
| FR-018 no-account path | `HandlerTests.AcceptInvitation_FromTheLink_JoinsTheCircleAsAMember`; `CirclePageTests.InvitationLanding_ToSomeoneNotSignedIn_OffersBothWaysIn_WithoutSayingWhichApplies` — the landing page offers signing in **and** signing up and never says which applies (NFR-005). The register-then-verify-then-accept journey end to end is now driven in one browser session by `InvitationSignUpTests.Invitation_link_carries_a_new_person_through_sign_up_into_the_circle` (E2E, gap 7 / arc42 D8) — mail link → create account → verify → sign in → join, asserting membership server-side | Unit / Component / E2E | ✅ |
| FR-018 account path | `HandlerTests.JoinRequests_ListOnlyTheInvitationsForTheCallersVerifiedAddress`; `CircleFixture.AddMemberAsync` drives the whole in-app path (invite → list → accept, no mail token) in **every** integration test that puts somebody in a circle; `CirclePageTests.JoinRequests_NamesTheCircleAndWhoInvitedYou` + `.JoinRequests_WithNothingWaiting_SaysSo` | Unit / Integration / Component | ✅ |
| **FR-018 the forwarded link** | `HandlerTests.AcceptInvitation_BySomeoneElsesVerifiedAddress_CreatesNothingAndRevealsNothing` and `CircleTests.Accept_BySomeoneElsesVerifiedAddress_CreatesNothing` — the single most important assertion in this plan, made where the single accept path checks it. *Level differs from the plan:* it is one comparison inside the aggregate, and the integration suite reaches the same code through `CircleRoleMatrixTests`' invitation rows | Unit | ✅ |
| FR-018 decline without an account | `HandlerTests.DeclineInvitation_NeedsNoAccount_AndConsumesTheInvitation`; `.DeclineJoinRequest_ByAnotherAccount_IsRefused`; `CirclePageTests.InvitationLanding_Declining_NeedsNoAccount_AndConfirmsPlainly` | Unit / Component | ✅ |
| FR-018 decline cooldown is silent | `CircleTests.ReInviteInsideTheCooldown_LooksIdenticalAndSendsNothing` + `.ReInviteAfterTheCooldown_IsSentAgain` (virtual clock). *The plan's integration half (`NoMailIsDeliveredInsideTheCooldown`) does not exist:* asserting that **no** mail arrives means waiting for a timeout, and the decision is the aggregate's | Unit | ✅ |
| FR-018 already a member | `CircleTests.Accept_WhenAlreadyAMember_CreatesNoSecondMembership`; `.Invite_AnAddressAlreadyInTheCircle_SendsNothingAndSaysNothing`; `HandlerTests.Invite_ToSomeoneAlreadyInTheCircle_AnswersTheSameAndQueuesNothing` | Unit | ✅ |
| FR-018 invalid / used / expired link | `CirclePageTests.InvitationLanding_WhenTheInvitationIsNoLongerValid_SaysSoPlainly_WithoutSayingWhy`; `CircleTests.Accept_AfterExpiry_IsRefusedAndTheInvitationIsGone`. **The plan asked for the wrong thing here** and the build is right: it wanted "each gets their own plain message", but the API answers expired, revoked, consumed and never-existed with one `invitation_not_found`, so the page says *no longer valid* without guessing why. Four distinguishable messages would tell whoever holds a forwarded link which of the four it was | Component / Unit | ✅ |
| FR-018 abandoned sign-up resumes | `CircleTests.AnInvitationNobodyAnswered_StaysPendingAndItsLinkKeepsWorking` — nothing but accepting or declining consumes an invitation, so an unfinished sign-up leaves it waiting. *Written at close* | Unit | ✅ |
| FR-018 pre-accept visibility | `HandlerTests.InvitationPreview_ShowsOnlyTheCircleNameLabelAndInviter`; `CirclePageTests.InvitationLanding_ToSomeoneNotSignedIn_OffersBothWaysIn_WithoutSayingWhichApplies` asserts the page shows nothing more | Unit / Component | ✅ |
| FR-019 add / edit / mark | `CircleTests.ManagedMember_IsAddedInTheMemberRole_WithNoAddress` + `.ManagedMembers_MayShareADisplayName`; `PersistedModelReviewTests.AManagedMember_PersistsNothingBeyondANameAndALanguage`; `CirclePageTests.AManagedMember_IsMarkedAndIsNeverOfferedARole`. *The plan's `ManagedMemberTests` class does not exist* — these are rules about the aggregate and live with it | Unit / Repository / Component | ✅ |
| FR-019 never elevated | `CircleTests.ManagedMember_CannotBeGivenAdminOrOwner`; `.Transfer_ToAManagedMember_IsRefused` | Unit | ✅ |
| FR-019 promotion links | `CircleTests.Promotion_LinksTheExistingMembership_AndCreatesNoDuplicate` + `.Promotion_LetsTheAccountsDisplayNameWin` | Unit | ✅ |
| FR-019 survives its creator | `CircleRepositoryTests.PurgeAccount_RemovesTheMembershipAndTheirInvitations_ButKeepsTheirManagedMembers` (the persisted half); `CircleTests.PurgeAccount_RemovesTheirMembership_ButLeavesManagedMembersInPlace`; `HandlerTests.PurgeAccountFromCircles_RemovesTheirMemberships_AndLeavesManagedMembers`; `AccountDeletionCleanupTests.Deletion_LeavesTheManagedMembersTheAccountAdded` (through real account deletion) | Repository / Unit / Integration | ✅ |
| FR-019 Secret-Santa copy unaffected | **not testable in M2** — nothing copies a circle into a group until M4. See the coverage notes. | — | n/a |
| FR-020 role change is owner-only | `CircleRoleMatrixTests` rows for `PUT …/role` (owner permitted, admin and member refused, non-member `404`, anonymous `401`); `CircleTests.AdminChangingARole_IsRefused`; `.ChangeRole_ToOwner_IsRefusedInFavourOfTransfer`; `CirclePageTests.AnAdmin_CanInviteButCannotDeleteTheCircleOrChangeRoles` | Authorization / Unit / Component | ✅ |
| FR-020 admin may not remove an admin | `CircleTests.Admin_CannotRemoveAnotherAdmin` + `.Admin_MayRemoveAMemberAndAManagedMember`; `CirclePageTests.AnAdmin_MayRemoveAMemberButNotAnotherAdmin` | Unit / Component | ✅ |
| FR-020 removal deletes only the membership | `CircleAuthorizationTests.RemovedMember_IsRefusedOnTheNextRequest_WithTheSameAccessToken` — the removed account still works, still holds its session, and simply no longer has this circle; `CircleTests.Admin_MayRemoveAMemberAndAManagedMember`; `CirclePageTests.Removing_AsksFirst_AndNamesThePerson`. *The plan's `Removal_LeavesTheAccountAndSecretSantaDataUntouched` does not exist as one test:* the Secret-Santa half is untestable until M4 (gap 1), and the account half is the assertion above | Authorization / Unit / Component | ✅ |
| **FR-020 / NFR-006 effect is immediate** | `CircleAuthorizationTests.RemovedMember_IsRefusedOnTheNextRequest_WithTheSameAccessToken` and `.DemotedAdmin_IsAuthorizedUnderTheNewRoleWithoutSigningOut` — both with the token issued *before* the change. *Written at close* | Authorization | ✅ |
| FR-020 owner cannot self-remove or demote | `CircleTests.Owner_CannotBeRemovedOrDemoted_EvenByThemselves`; `CirclePageTests.TheOwner_IsNeverOfferedAWayToRemoveThemselvesOrBeRemoved` | Unit / Component | ✅ |
| FR-020 transfer | `CircleTests.Transfer_MakesTheTargetOwnerAndThePreviousOwnerAdmin_Atomically`, `.Transfer_ToAManagedMember_IsRefused`, `.Transfer_ByAnAdmin_IsRefused`; `CircleRepositoryTests.Transfer_WhenTheTargetLeftConcurrently_Fails_AndTheCircleKeepsItsOwner` + `.TwoConcurrentEdits_DoNotBothWin_EvenWhenOnlyChildrenChanged` — the second is the general case, and the one that fails if the concurrency token ever stops covering the whole aggregate | Unit / Repository | ✅ |
| **NFR-006 no addresses** | `CircleContractsTests.NoCircleDto_CarriesAnEmailField` — an assembly scan, not a per-endpoint assertion — plus `.EveryExemptedContractTypeStillExists`, which stops the two-name exemption list outliving its types | Architecture | ✅ |
| **NFR-006 role matrix** | `CircleRoleMatrixTests` — all **14** circle-scoped routes × owner / admin / member / non-member / anonymous, permitted **and** refused outcomes, 71 rows. `.TheMatrix_CoversEveryCircleScopedRoute` ties the table to the endpoint data source so a new route cannot be missing from it. *Written at close:* the class did not exist; `CircleAuthorizationTests` held three spot checks | Authorization | ✅ |
| **NFR-006 non-member ≡ unknown** | `CircleRoleMatrixTests.ANonMember_IsAnsweredExactlyAsForACircleThatDoesNotExist` — every route, status **and** body, against the same request aimed at a circle id that resolves to nothing; `CircleAuthorizationTests.NonMemberAndUnknownCircle_AnswerIdentically`; `HandlerTests.GetCircle_ForANonMember_AnswersExactlyAsAnUnknownCircleDoes`; `CircleTests.Update_ByANonMember_IsRefusedAsIfTheCircleWereUnknown`; `CirclePageTests.ACircleTheCallerIsNotIn_ReadsAsNoSuchCircle` | Authorization / Unit / Component | ✅ |
| **NFR-006 the next endpoint** | `CircleEndpointGuardTests.EveryCircleRoute_CarriesTheRoleFilter` — walks the endpoint data source, so an endpoint added later without a check fails the build rather than a review; `.ACircleRouteWithoutARoleRequirement_StillRequiresAnAccount` (no route becomes public by omission); `.EveryRouteNamingACircleAnywhere_IsGuardedOrExempted` with a three-name exemption list for the in-app invitation routes, `.EveryExemptedRouteStillExists`, and `.TheScanActuallyFindsTheCircleRoutes` so none of them can pass vacuously. *Written at close:* the metadata existed to be read back, and nothing read it | Architecture | ✅ |
| NFR-006 UI hiding is not the control | `CircleRoleMatrixTests` covers it by construction — every refused row is a direct API call; `CirclePageTests.AMember_SeesNeitherTheInviteFormNorTheDeleteSection` is the cosmetic half only | Authorization / Component | ✅ |
| FR-021 leave | `CircleEndpointsTests.Leave_DeletesTheMembership_AndTheCircleVanishesFromTheirList` + `.Leave_ThenARequestWithAStillValidToken_IsRefused`; `CircleTests.Leave_DeletesTheMembership`. *Written at close* | Integration / Unit | ✅ |
| FR-021 the confirmation says what survives | `CirclePageTests.LeaveConfirmation_SaysWhatLeavingDoesNotTake`. **The reconciliation found this criterion unmet in the product, not merely untested:** the confirmation said only that rejoining needs a new invitation. `CircleLeaveKeeps` was added in DE/EN/FR to say that the account stays and the user's own groups, members and completed draws are untouched | Component | ✅ |
| FR-021 sole-owner branches | `CircleTests.Owner_WithOtherAccountBackedPeople_CannotLeave_AndIsOfferedTransfer` + `.Owner_WithOnlyManagedMembers_IsOfferedDeletion` + `.IsSoleOwner_IsTrueOnlyWhileNobodyElseCouldOwnIt`; `HandlerTests.SoleOwnedCircles_TellTheCallerWhetherAnyoneElseCouldTakeOver`; `CirclePageTests.AnOwnerLeaving_IsToldWhichWayOutTheyHave` | Unit / Component | ✅ |
| FR-021 rejoin does not restore a role | `CircleTests.Rejoining_AfterLeaving_ComesBackAsMember` | Unit | ✅ |
| FR-021 already removed | `CircleTests.Leave_WhenAlreadyRemoved_ReportsNotAMember` — *unit rather than the planned integration:* the endpoint's `404` for a non-member is one of `CircleRoleMatrixTests`' rows | Unit / Authorization | ✅ |
| FR-022 deletion scope | `CircleRepositoryTests.Delete_TakesEveryPersonInvitationAndDeclineWithIt` + `.Delete_LeavesOtherCirclesUntouched` (what goes, and what does not); `CircleEndpointsTests.WhenTheCircleIsDeleted_ItsPendingInvitationIsGone_AndTheRecipientIsToldSo` (through the API, and no account is touched — both accounts keep working afterwards); `HandlerTests.DeleteCircle_NeedsTheNameTypedBack`. *The plan's `CircleDeletionTests` class does not exist* | Repository / Integration / Unit | ✅ |
| FR-022 the confirmation | `CirclePageTests.DeleteConfirmation_NamesTheCircle_CountsWhoLosesAccess_AndNamesEveryManagedMember` + `.DeleteConfirmation_WithNoManagedMembers_LeavesThatSentenceOut` + `.DeleteButton_StaysDisabledUntilTheNameIsTypedBack`. **Also unmet in the product before close:** the section stated the scope of deletion generically. It now names the circle, counts who loses access, names every managed member, and states that Secret-Santa groups, draws and reveal links are unaffected (`CircleDeleteNames`, `CircleDeleteLosesAccess`, `CircleDeleteManagedNamed`, `CircleDeleteKeeps`) | Component | ✅ |
| FR-022 the count reads correctly at one | `CircleConfirmationTextTests.DeleteConfirmation_UsesTheSingular_WhenOnlyTheOwnerLosesAccess`. **Found on the screen, not in a test** (2026-07-30): the count sentence was one format string, so a circle whose owner is its only account-backed person read *"1 Personen verlieren"*, *"1 people will lose"*, *"1 personnes en perdront"* — the state every circle is in the moment it is created. All three languages inflect the noun **and** the verb, so the singular is its own entry (`CircleDeleteLosesAccessOne`, no placeholder) chosen by the page. The tests above could not see it: each renders three account-backed people | Component | ✅ |
| FR-022 admin cannot delete | `CircleRoleMatrixTests` row for `DELETE /circles/{id}`; `HandlerTests.DeleteCircle_ByAnAdmin_IsRefused`; `CirclePageTests.AnAdmin_CanInviteButCannotDeleteTheCircleOrChangeRoles` | Authorization / Unit / Component | ✅ |
| FR-022 one transaction | `CircleRepositoryTests.FailedDelete_LeavesEverythingInPlace` — and it asserts the `DELETE` was actually issued before the failure, so it cannot pass vacuously; plus `.Delete_TakesEveryPersonInvitationAndDeclineWithIt` and `.Delete_LeavesOtherCirclesUntouched` for the cascade's scope | Repository | ✅ |
| FR-022 dangling circle id tolerated | `CircleEndpointsTests.WhenTheCircleIsDeleted_ItsPendingInvitationIsGone_AndTheRecipientIsToldSo` — a circle id that resolves to nothing is answered `404`, not an error; the Secret-Santa half arrives with M4 (gap 2) | Integration | ⚠️ partial |
| FR-022 pending invitation dies with it | `CircleEndpointsTests.WhenTheCircleIsDeleted_ItsPendingInvitationIsGone_AndTheRecipientIsToldSo` — the join request disappears from the recipient's list and accepting it answers *no such invitation*. *Written at close* | Integration | ✅ |
| **FR-015 sole-owner guard** | `SoleOwnerCircleGuardTests.AnAccountThatOwnsNothing_MayBeDeleted`, `.ACircleSomebodyElseCouldOwn_IsStillABlocker_AndIsOfferedTransfer`, `.ACircleNobodyElseCouldOwn_IsOfferedDeletion`, `.EveryOwnedCircleIsNamedAndIdentified_SoTheUserIsNotSentHunting`; `AccountDeletionCleanupTests` (`.Deletion_IsRefusedWhileTheAccountOwnsACircle`, `.WithOthersInTheCircle_TheRefusalOffersTransferInstead`, `.OnceOwnershipIsHandedOver_TheAccountCanBeDeleted`, `.DeletingTheCircleFirst_UnblocksTheAccount`, `.Deletion_RemovesTheAccountFromOtherPeoplesCircles`, `.Deletion_LeavesTheManagedMembersTheAccountAdded`, `.Deletion_WithdrawsTheInvitationsTheAccountSent`, `.ARefusedDeletion_ChangesNothing`) | Unit / Integration | ✅ |
| NFR-004 managed-member minimisation | `CircleTools.Circles.Repository.Test.PersistedModelReviewTests.NoPersistedProperty_HoldsDataTheSuitePromisedNotToKeep` — the same scan for birth-date/postal/phone/payment fragments the Main model has carried since M1, over a model describing people who did not enter their own data — plus `.AManagedMember_PersistsNothingBeyondANameAndALanguage` pinned as an exact column list. A sibling class rather than an extension of Main's: the two contexts have separate models, and coupling Main's test project to Circles to share one list would breach the layering this milestone is enforcing | Architecture | ✅ |
| NFR-004 invitation retention | `InvitationPurgeTests.ExpiredInvitations_ArePurged_AndPendingOnesAreNot`, `.ADeclineIsSwept_OnceItSuppressesNothing`, `.CountedInvitationAttempts_AreSweptOnceNoLimitCountsThem`, `.Purge_IsSafeWhenNothingHasExpired` (virtual clock, real Postgres) | Repository | ✅ |
| NFR-004 declines keep no address | `CircleTests.ADecline_StoresOnlyAHash`; `CircleRepositoryTests.ADeclinedInvitation_LeavesARowThatHoldsNoAddress` + `.ASecondDeclineByTheSameAddress_ReplacesTheRecordRatherThanColliding` — the domain's intent, and then what is actually in the row | Unit / Repository | ✅ |
| NFR-004 the invitation secret is never stored | `PersistedModelReviewTests.AnInvitation_StoresTheHandleOfItsSecretAndNeverTheSecret`; `.TheCounters_HoldAHashOfTheRecipient_NotTheAddress` | Architecture | ✅ |
| **FR-020 / ADR-0003 the schema seam** | `PersistedModelReviewTests.EveryTable_LivesInTheCirclesSchema`; `CirclesLayeringTests.Repository_DoesNotDependOnWeb` | Architecture | ✅ |
| NFR-003 string parity | `LocalizationParityTests.EverySupportedCulture_DefinesEveryNeutralKey` covers the new keys, and `.EveryPlaceholderInANeutralString_SurvivesIntoEveryLanguage` — *added at close* — covers what key parity cannot see: a translation that keeps the key and drops the `{0}` renders a sentence that reads well and names nothing, which is exactly how FR-022's confirmation would fail silently | Localization | ✅ |
| NFR-003 invitation mail in three languages | `MailTemplateRendererTests.InvitationTemplate_RendersInAllThreeLanguages`; `.Invitation_ToAnAddressWithoutAnAccount_CarriesTheLink` + `.Invitation_ToAnAddressWithAnAccount_PointsAtTheAppAndCarriesNoSecret` | Unit | ✅ |
| NFR-006 / ADR-0011 every refusal is translatable | `CircleErrorCodeCoverageTests.EveryRefusalCodeTheApiCanSend_HasATranslatedSentence` + `.TheUiTable_DoesNotDescribeRefusalsTheApiCannotSend` — *not in the plan at all.* It arrived with `ApiError.Code`, and it is the fitness function that keeps a new refusal code from reaching a DE/EN/FR client as a bare conflict | Architecture | ✅ |
| Layering | `CirclesLayeringTests.Core_DoesNotDependOnInfrastructureOrWeb`, `.Contracts_DoesNotDependOnInfrastructureOrWeb`, `.Repository_DoesNotDependOnWeb`, `.Circles_DoesNotDependOnTheIdentityModel`, `.SecretSanta_DoesNotDependOnCircles` | Architecture | ✅ |

## Test data & environments

- **Postgres** via Aspire/Testcontainers, fresh database per class, both migration sets applied.
- **Mailpit** as the SMTP sink, asserted through its HTTP API — the invitation is verified as a
  *delivered message*, including its language and its link.
- **Two accounts minimum** in every invitation and role test, and the fixtures make the *caller* an
  explicit parameter rather than an ambient default. M1's tests could get away with one identity;
  here, "which user is this?" is the thing under test.
- Time via injected `TimeProvider` — the 14-day expiry, the 7-day cooldown and the retention purge are
  advanced virtually. The enumeration timing test is the one exception and compares distributions.

## Localization & accessibility notes

- **Localization:** parity fails if any `de-CH`/`en`/`fr-CH` key is missing or empty. One assertion
  covers the rule that a stored **label is never re-translated** — the mistake a resource lookup would
  invite.
- **Accessibility:** failures render through `ProblemAlert` (`role="alert"`), successes through
  `role="status"`. Both destructive confirmations — leave and delete — are keyboard-reachable, are not
  the primary style, and are disabled until their deliberate step is complete (the typed circle name
  for deletion). The people list's role and managed markers must not be **colour alone**.

## Coverage notes / known gaps (no silent truncation)

1. **FR-019's "the Secret-Santa copy is unaffected"** has nothing to act on: no code copies a circle
   into a group before M4. Marked `n/a` above, not ✅.
2. **FR-022's dangling-identifier criterion is only half testable.** M2 can assert that the API
   tolerates a circle id that resolves to nothing; the Secret-Santa side has no circle id to hold
   until M4.
3. **Deliverability** (SPF/DKIM/DMARC, bounces, abuse reports) is untestable against a local sink and
   belongs to the transactional-email provider decision — sharper here than in M1, because an
   invitation goes to someone with no relationship to the product.
4. **No end-to-end journey across Web and native**, deferred to milestone 7 with M1's carry-overs.
5. **"CI green"** remains unsatisfiable until the CI/CD ADR and pipeline exist (milestone 7); the exit
   criterion below says so rather than quietly claiming it.
6. ~~**The invite form's body/status parity is not yet asserted.**~~ **Closed at reconciliation.**
   `InvitationMailTests.Invite_AnswersIdentically_WhetherTheAddressHasAnAccountIsUnknownOrIsAlreadyInTheCircle`
   now compares status and body across the same three arms `InvitationTimingTests` compares on timing.
   The fourth fact the endpoint withholds — a recent decline — is asserted where the decision is made
   (`CircleTests.ReInviteInsideTheCooldown_LooksIdenticalAndSendsNothing`), because reaching it through
   the API would mean declining through a mail link first.
7. **The sign-up-from-a-link journey is now automated end to end** (✅ closed 2026-08-19, arc42
   D8). Its two halves — M1's register → verify → sign in, and this milestone's accept
   (`HandlerTests.AcceptInvitation_FromTheLink_JoinsTheCircleAsAMember`, with the landing page in
   `CirclePageTests`) — were each covered, but nothing drove a real mail link through sign-up in one
   test; the join was walked in the browser instead. `InvitationSignUpTests` now drives it in one
   Chromium session on the milestone-7 browser-driving host ([ADR-0045](../adr/0045-browser-driving-e2e-test-host.md),
   the same host gap 4 / D4 built): an admin invites an address with no account (over the API), and
   the recipient follows the invitation mail link to the anonymous landing, creates an account,
   verifies it, signs in and joins — with membership asserted server-side (and NFR-006 upheld: the
   address never appears in the people list). It runs in the gating `webauthn-e2e` CI job.

### What the timing work found (2026-07-30)

M1's `EnumerationTimingTests` had one hole and it was load-dependent: a **3 ms absolute noise floor**
that passed any difference beneath it whatever the ratio said. `/forgot-password` sat in that hole
for the whole of M1 at a steady 1.9x (2.36 ms against 1.24 ms), and surfaced only when a loaded
full-suite run scaled both paths past 3 ms — which is why it read as a flaky test rather than a leak.

Reviewing the shape rather than the symptom found the same asymmetry on three more surfaces —
`/resend-verification` (1.67x), `/change-email` (1.63x) and the invite form itself (1.18x, 2.79 ms).
All four now carry `RequireConstantTime`; NFR-005 records the mechanism and when to reach for it.
Two lessons worth keeping in this plan:

- **A stable ratio across differently loaded runs is evidence of structure**, even when the absolute
  difference is small. Noise does not hold a ratio.
- **These tests are more trustworthy under load than on an idle machine**, which inverts the usual
  instinct to re-run a failure in isolation and call it flaky.

**Correction (2026-11-07):** that second lesson does not cover the 2 ms spread check on the envelope
routes, which failed on identical requests as often as on the real arms. The check was measuring the
timer, so the tests now assert the handler's cost against the budget directly. See
[backlog PR-16](../roadmap/backlog.md) for the measurements and the limit at saturation.

### What the reconciliation found (2026-07-30)

Reconciling the matrix against the code was not the clerical exercise the plan expected. Naming drift
was the least of it — `InvitationEndpointsTests`, `ManagedMemberTests`, `CircleDeletionTests`,
`InvitationPageTests`, `CircleRoleMatrixTests`, `CircleEndpointGuardTests`, `RateLimitTests` and
`InvitationRateLimitTests` were all plan-time names, and most of what they promised turned out to be
covered elsewhere, at a different level, under a different name. Four things were **not** covered at
all, and three of those were the ones the plan itself called load-bearing:

- **`EveryCircleRoute_CarriesTheRoleFilter` did not exist.** `CircleRoleRequirement` was deliberately
  built as endpoint *metadata* — with a comment saying that this is what lets a fitness function read
  it back — and nothing ever read it. The mechanism was there and the assertion was missing, which is
  the most easily overlooked kind of gap: the design intent is visible in the code and reads as done.
- **The "role matrix" was three spot checks**, not a matrix. It is now 14 routes × 5 caller kinds,
  71 rows, with the table tied to the endpoint data source.
- **Two acceptance criteria were unmet in the product, not merely untested.** FR-021's leave
  confirmation did not say that the account and the user's Secret-Santa data survive; FR-022's delete
  confirmation did not name the circle, count who loses access, or name the managed members. Both were
  found by reading the criteria against the screen rather than against the tests — a green suite had
  nothing to say about either, because no test asserted the sentences that were missing.
- **Revoking an invitation had no functional test.** Only its route filter was covered, so the matrix
  proved an admin may call it and nothing proved what it did.

The lesson worth carrying to M3 is narrower than "reconcile the matrix": **a test plan row is not
evidence, and a fitness function that is designed but not written leaves the design comment behind as
false reassurance.** The three checks that mattered most here were all cheap once written; what kept
them missing was that everything around them looked finished.

The browser walk-through that closed the last box added a second lesson, and it is about the test
data rather than the tests. Both counted strings in the product — the circle list's size and FR-022's
delete sentence — had **no singular**, and read *"1 Personen"*, *"1 people"*, *"1 personnes"*. Every
test that touched them was correct and green: each had been written with three or four people in the
circle, because that is what a realistic circle looks like. The value that broke them is the one the
product starts at. **Counted strings need a test at one**, and the rule now lives in the
[localization guidelines](../guidelines/localization-guidelines.md) rather than only here — this is a
whole-product trap, not a Circles one.

## Exit criteria

- [x] All acceptance criteria covered by automated tests except the gaps recorded above — and the two
      that were unmet in the **product** (FR-021's and FR-022's confirmations) fixed rather than
      recorded
- [x] The **role matrix** is complete — 14 routes × 5 caller kinds, and `CircleEndpointGuardTests`
      green, so the next endpoint cannot skip it
- [x] Enumeration parity green on the invite form: body, status **and** timing
- [x] `AcceptInvitation_BySomeoneElsesVerifiedAddress_CreatesNothingAndRevealsNothing` green — the
      forwarded-link invariant is the security core of this milestone
- [x] Aggregate invariants green: exactly one owner, atomic transfer, no elevated managed member
- [x] FR-015's sole-owner guard green end to end, closing the criterion carried from M1
- [x] Architecture fitness functions green, including the extended NFR-004 model review and the
      unplanned `CircleErrorCodeCoverageTests`
- [x] Localization parity green, including the invitation mail and the new placeholder-parity check
- [x] Browser walkthrough completed in DE/EN/FR with a clean console — **two accounts, two browser
      profiles**: create → invite an address with no account → sign up from the link → invite an
      address that has one → accept in-app → decline → managed member → promote → role change →
      transfer → leave → delete. The M2 walkthrough was completed before close; the two confirmation
      screens changed at close were **walked again on 2026-07-30**, in all three languages, against a
      running AppHost (Postgres + Mailpit), driven over the DevTools protocol. It found two defects
      the suite could not, both the same one — a counted string with no singular, in FR-022's
      confirmation and in the circle list (rows above) — which is the third and fourth time in this
      milestone that reading the screen beat reading the tests
- [x] Matrix reconciled against the tests that actually exist, so every row names a real class
- [x] Suite green locally; **"CI green" is carried to milestone 7**, not ticked
