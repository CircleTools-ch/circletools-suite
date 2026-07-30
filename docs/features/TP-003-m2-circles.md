---
id: TP-003
feature: FEAT-003
status: In progress
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

- **Unit (`AppSuite.Circles.Core.Test`)** — the `Circle` aggregate and the handlers, with no I/O:
  role transitions, the ownership invariant, transfer atomicity, managed-member rules, promotion by
  linking, invitation lifecycle and the caps. Clock injected via `TimeProvider`; **no wall-clock
  waits** for the 14-day expiry or the 7-day cooldown.
- **Integration (`AppSuite.Main.Service.Test`)** — endpoints against **real Postgres** and **real
  SMTP** (Mailpit, [ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)), asserting on mail
  actually delivered rather than on a mocked sender, as M1 does.
- **Authorization (role matrix)** — one parameterised suite over **every** circle route × {owner,
  admin, member, non-member, anonymous}, asserting the permitted *and* the refused outcome, plus that
  a non-member's response is byte-identical to an unknown circle's.
- **Security / timing** — paired enumeration tests for `POST /invitations`: an address with an
  account, one without, and one already in the circle must be indistinguishable in body, status and
  timing distribution.
- **Repository (`AppSuite.Circles.Repository.Test`)** — the `circles` schema mapping, the optimistic
  concurrency token, cascade behaviour on deletion, the retention sweeps and the three invitation
  rate limits, against real Postgres. Applying the migration is itself part of the suite: `xmin` is a
  system column that cannot be created, and every test here fails at startup if the provider ever
  stops filtering it out of the generated SQL. Two providers per test are two concurrent callers —
  the concurrency assertions are worthless against a single change tracker.
- **Architecture** — `Circles.Core` free of ASP.NET/EF; no Secret-Santa project references Circles;
  no Circles project references `AppSuite.Identity`; plus the two NFR-006 fitness functions.
- **UI/component (bUnit, `AppSuite.Main.Ui.Test`)** — the circle pages over a stub transport, as M1's
  account pages are. The themes: the deliberately uninformative invite confirmation, the destructive
  confirmations, and role-dependent rendering.
- **Localization** — key parity across `de-CH`/`en`/`fr-CH`, plus the invitation mail template.
- **Exploratory** — the browser walkthrough `CLAUDE.md` requires, in all three languages. M1's
  walkthrough found six defects every automated level passed; this one has two accounts and two
  browsers in play, which is more, not less, room for that.

## Test matrix

Test class names below are **plan-time names**. M1 closed with ~17 rows naming classes that were never
written under those names; this plan is to be reconciled against the code at close, and the exit
criteria say so.

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-016 create + owner membership | `CircleTests.Create_MakesTheCreatorTheSoleOwner`; `CircleEndpointsTests.Create_ReturnsTheCircleWithItsOwner` | Unit / Integration | ✅ |
| FR-016 label defaults per language | `CircleTests.Create_EmptyLabel_FallsBackToTheLanguageDefault`; `CirclePageTests.Create_PrefillsTheLabelForTheCurrentLanguage` (DE/EN/FR) | Unit / Component | ✅ |
| FR-016 the label is user data | `CircleEndpointsTests.Label_IsNotRetranslatedForAViewerInAnotherLanguage` | Integration | ✅ |
| FR-016 name required | `CircleTests.Create_BlankName_IsRefused` | Unit | ✅ |
| FR-016 verified address required | `CircleEndpointsTests.Create_UnverifiedAccount_IsRefusedWithAResendOffer` | Integration | ✅ |
| FR-016 atomicity | `CircleRepositoryTests.FailedCreate_LeavesNeitherCircleNorMembership` | Repository | ✅ |
| FR-016 / FR-019 the mapping is faithful | `CircleRepositoryTests.Circle_SurvivesARoundTripWholeWithEveryPersonInvitationAndDecline` — the aggregate reloaded from Postgres in a second context, managed member and all | Repository | ✅ |
| FR-016 caps | `CircleTests.Create_BeyondTheOwnedCircleCap_IsRefused`; `RateLimitTests.CircleCreation_IsLimitedPerUserPerDay` | Unit / Integration | ✅ |
| **FR-017 identical response** | `InvitationTimingTests.Invite_NoAccount_WithAccount_AndAlreadyAMember_AreIndistinguishable` — three arms, interleaved, compared on the fastest sample each managed. *Renamed from the plan's `InvitationEnumerationTests`, and it does less than the plan implied:* it covers **timing** only. Body and status parity across the three arms is still owed — see gap 6 | Security | ⚠️ **partial** |
| FR-017 resend rather than duplicate | `CircleTests.Invite_SameAddressTwice_ResendsTheExistingInvitation` + `.Resend_IssuesAFreshSecretAndKillsThePreviousLink` | Unit | ✅ |
| FR-017 revoke | `InvitationEndpointsTests.Revoked_CannotBeAccepted_AndSaysSoPlainly` | Integration | ✅ |
| FR-017 expiry (14 days) | `CircleTests.Invitation_PastItsExpiry_LeavesThePendingListAndFreesTheAddress` (virtual clock) | Unit | ✅ |
| FR-017 role-only-member | `CircleTests.Invitation_NeverGrantsMoreThanMember` | Unit | ✅ |
| FR-017 rate limits | `InvitationRateLimiterTests` (Repository, real Postgres) — per inviter (20/h, 60/d), **per recipient address (3/24 h whatever the sender)**, the rolling window, that a refused attempt is not counted, and that the refusal says *when* they may continue; `InvitationRateLimitTests` (Integration) for the per-circle 50-pending cap and the endpoint's answer | Repository / Integration | ✅ |
| FR-017 delivery failure is not reported | `InvitationEndpointsTests.UndeliverableMail_LeavesTheInvitationPendingAndTellsTheInviterNothing` | Integration | ✅ |
| FR-017 mail language | `InvitationMailTests.Mail_UsesTheRecipientsAccountLanguage_OtherwiseTheInvitersUsingMailpit` | Integration | ✅ |
| FR-017 dispatch is queued | `InvitationEndpointsTests.Invite_QueuesTheMailRatherThanAwaitingIt` (the structural half of the timing property) | Integration | ✅ |
| FR-018 no-account path | `InvitationEndpointsTests.SignUpPath_CreatesTheMembershipOnceTheAddressIsVerified`; `InvitationPageTests.SignUp_PrefillsAndLocksTheInvitedAddress` | Integration / Component | ✅ |
| FR-018 account path | `InvitationEndpointsTests.JoinRequest_IsListedForTheMatchingVerifiedAddress_AndAcceptsWithoutAnyMailToken` | Integration | ✅ |
| **FR-018 the forwarded link** | `InvitationEndpointsTests.Accept_BySomeoneElsesVerifiedAddress_CreatesNothingAndRevealsNothing` — the single most important test in this plan | Integration | ✅ |
| FR-018 decline without an account | `InvitationEndpointsTests.Decline_WorksAnonymously_AndConsumesTheInvitation` | Integration | ✅ |
| FR-018 decline cooldown is silent | `CircleTests.ReInviteInsideTheCooldown_LooksIdenticalAndSendsNothing` + `InvitationMailTests.NoMailIsDeliveredInsideTheCooldown` | Unit / Integration | ✅ |
| FR-018 already a member | `InvitationEndpointsTests.Accept_WhenAlreadyAMember_SaysSo_AndCreatesNoSecondMembership` | Integration | ✅ |
| FR-018 invalid / used / deleted circle | `InvitationPageTests.ExpiredRevokedUsedAndDeletedCircle_EachGetTheirOwnPlainMessage` | Component | ✅ |
| FR-018 abandoned sign-up resumes | `InvitationEndpointsTests.AbandonedSignUp_LeavesTheInvitationPending` | Integration | ✅ |
| FR-018 pre-accept visibility | `InvitationEndpointsTests.Preview_ShowsOnlyCircleNameLabelAndInviterDisplayName` | Integration | ✅ |
| FR-019 add / edit / mark | `ManagedMemberTests` (added in the member role, marked managed, no address stored or returned, name and language editable, duplicate names allowed) | Unit / Integration | ✅ |
| FR-019 never elevated | `CircleTests.ManagedMember_CannotBeGivenAdminOrOwner` | Unit | ✅ |
| FR-019 promotion links | `CircleTests.Promotion_LinksTheExistingMembership_AndCreatesNoDuplicate` + `.Promotion_LetsTheAccountsDisplayNameWin` | Unit | ✅ |
| FR-019 survives its creator | `CircleRepositoryTests.PurgeAccount_RemovesTheMembershipAndTheirInvitations_ButKeepsTheirManagedMembers` (the persisted half); `AccountDeletionCleanupTests.DeletingAnAdmin_LeavesTheirManagedMembersInTheCircle` (through account deletion) | Repository / Integration | ✅ |
| FR-019 Secret-Santa copy unaffected | **not testable in M2** — nothing copies a circle into a group until M4. See the coverage notes. | — | n/a |
| FR-020 role change is owner-only | `CircleRoleMatrixTests` rows for `PUT …/role`; `CircleTests.AdminChangingARole_IsRefused` | Authorization / Unit | ✅ |
| FR-020 admin may not remove an admin | `CircleTests.Admin_CannotRemoveAnotherAdmin` | Unit | ✅ |
| FR-020 removal deletes only the membership | `CircleEndpointsTests.Removal_LeavesTheAccountAndSecretSantaDataUntouched` | Integration | ✅ |
| **FR-020 / NFR-006 effect is immediate** | `CircleAuthorizationTests.RemovedMember_IsRefusedOnTheNextRequest_WithTheSameAccessToken` and `.DemotedAdmin_IsAuthorizedUnderTheNewRoleWithoutSigningOut` | Authorization | ✅ |
| FR-020 owner cannot self-remove or demote | `CircleTests.Owner_CannotBeRemovedOrDemoted_EvenByThemselves` | Unit | ✅ |
| FR-020 transfer | `CircleTests.Transfer_MakesTheTargetOwnerAndThePreviousOwnerAdmin_Atomically` + `.Transfer_ToAManagedMember_IsRefused`; `CircleRepositoryTests.Transfer_WhenTheTargetLeftConcurrently_Fails_AndTheCircleKeepsItsOwner` + `.TwoConcurrentEdits_DoNotBothWin_EvenWhenOnlyChildrenChanged` — the second is the general case, and the one that fails if the concurrency token ever stops covering the whole aggregate | Unit / Repository | ✅ |
| **NFR-006 no addresses** | `CircleContractsTests.NoCircleDto_CarriesAnEmailField` — an assembly scan, not a per-endpoint assertion | Architecture | ✅ |
| **NFR-006 role matrix** | `CircleRoleMatrixTests` — every route × owner / admin / member / non-member / anonymous, permitted **and** refused outcomes | Authorization | ✅ |
| **NFR-006 non-member ≡ unknown** | `CircleAuthorizationTests.NonMemberAndUnknownCircle_AnswerIdentically` | Authorization | ✅ |
| **NFR-006 the next endpoint** | `CircleEndpointGuardTests.EveryCircleRoute_CarriesTheRoleFilter` — walks the endpoint data source, so an endpoint added later without a check fails the build rather than a review | Architecture | ✅ |
| NFR-006 UI hiding is not the control | `CircleRoleMatrixTests` covers it by construction — every refused row is a direct API call | Authorization | ✅ |
| FR-021 leave | `CircleEndpointsTests.Leave_DeletesTheMembership_AndTheCircleVanishesFromTheirList`; `.Leave_ThenRequestWithAStillValidToken_IsRefused` | Integration | ✅ |
| FR-021 the confirmation says what survives | `CirclePageTests.LeaveConfirmation_StatesAccountGroupsMembersAndDrawsAreUntouched` | Component | ✅ |
| FR-021 sole-owner branches | `CircleTests.Owner_WithOtherAccountBackedPeople_CannotLeave_AndIsOfferedTransfer` + `.Owner_WithOnlyManagedMembers_IsOfferedDeletion` | Unit | ✅ |
| FR-021 rejoin does not restore a role | `CircleTests.Rejoining_AfterLeaving_ComesBackAsMember` | Unit | ✅ |
| FR-021 already removed | `CircleEndpointsTests.Leave_WhenAlreadyRemoved_IsSilent` | Integration | ✅ |
| FR-022 deletion scope | `CircleDeletionTests.Delete_RemovesCircleMembershipsManagedMembersAndPendingInvitations_ButNoAccount` | Integration | ✅ |
| FR-022 the confirmation | `CirclePageTests.DeleteConfirmation_NamesTheCircle_CountsThePeople_AndNamesEveryManagedMember` + `.DeleteButton_StaysDisabledUntilTheNameIsTypedBack` | Component | ✅ |
| FR-022 admin cannot delete | `CircleRoleMatrixTests` row for `DELETE /circles/{id}` | Authorization | ✅ |
| FR-022 one transaction | `CircleRepositoryTests.FailedDelete_LeavesEverythingInPlace` — and it asserts the `DELETE` was actually issued before the failure, so it cannot pass vacuously; plus `.Delete_TakesEveryPersonInvitationAndDeclineWithIt` and `.Delete_LeavesOtherCirclesUntouched` for the cascade's scope | Repository | ✅ |
| FR-022 dangling circle id tolerated | `CircleDeletionTests.ADeletedCircleId_IsToleratedRatherThanTreatedAsCorruption` — asserted at the contract level in M2; the Secret-Santa half arrives with M4 | Integration | ⚠️ partial |
| FR-022 pending invitation dies with it | `InvitationEndpointsTests.AfterTheCircleIsDeleted_TheRecipientIsToldItNoLongerExists` | Integration | ✅ |
| **FR-015 sole-owner guard** | `SoleOwnerCircleGuardTests` (nothing owned passes; each owned circle is named and given the way out that fits it); `AccountDeletionCleanupTests` (`.Deletion_IsRefusedWhileTheAccountOwnsACircle`, `.OnceOwnershipIsHandedOver_TheAccountCanBeDeleted`, `.DeletingTheCircleFirst_UnblocksTheAccount`, `.Deletion_RemovesTheAccountFromOtherPeoplesCircles`, `.Deletion_LeavesTheManagedMembersTheAccountAdded`, `.Deletion_WithdrawsTheInvitationsTheAccountSent`) | Unit / Integration | ✅ |
| NFR-004 managed-member minimisation | `AppSuite.Circles.Repository.Test.PersistedModelReviewTests` — the same scan for birth-date/postal/phone/payment fragments the Main model has carried since M1, over a model describing people who did not enter their own data, plus `.AManagedMember_PersistsNothingBeyondANameAndALanguage` pinned as an exact column list. A sibling class rather than an extension of Main's: the two contexts have separate models, and coupling Main's test project to Circles to share one list would breach the layering this milestone is enforcing | Architecture | ✅ |
| NFR-004 invitation retention | `InvitationPurgeTests.ExpiredInvitations_ArePurged_AndPendingOnesAreNot`, `.ADeclineIsSwept_OnceItSuppressesNothing`, `.CountedInvitationAttempts_AreSweptOnceNoLimitCountsThem` (virtual clock, real Postgres) | Repository | ✅ |
| NFR-004 declines keep no address | `CircleTests.ADecline_StoresOnlyAHash`; `CircleRepositoryTests.ADeclinedInvitation_LeavesARowThatHoldsNoAddress` — the domain's intent, and then what is actually in the row | Unit / Repository | ✅ |
| NFR-004 the invitation secret is never stored | `PersistedModelReviewTests.AnInvitation_StoresTheHandleOfItsSecretAndNeverTheSecret` | Architecture | ✅ |
| **FR-020 / ADR-0003 the schema seam** | `PersistedModelReviewTests.EveryTable_LivesInTheCirclesSchema`; `CirclesLayeringTests.Repository_DoesNotDependOnWeb` | Architecture | ✅ |
| NFR-003 string parity | `LocalizationParityTests` (existing) covers the new keys | Localization | ✅ |
| NFR-003 invitation mail in three languages | `MailTemplateRendererTests.InvitationTemplate_RendersInAllThreeLanguages` | Unit | ✅ |
| Layering | `CirclesLayeringTests` — no ASP.NET/EF in `Circles.Core`; no Secret-Santa project references Circles; no Circles project references `AppSuite.Identity` | Architecture | ✅ |

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
6. **The invite form's body/status parity is not yet asserted.** `InvitationTimingTests` covers the
   timing arm — which is the one that was actually leaking — but nothing yet asserts that the three
   responses are byte-identical. The plan's single row implied both; splitting them here rather than
   leaving the row ticked is the point of reconciling against the code.

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

## Exit criteria

- [ ] All acceptance criteria covered by automated tests except the gaps recorded above
- [ ] The **role matrix** is complete — every route × every caller kind, and `CircleEndpointGuardTests`
      green, so the next endpoint cannot skip it
- [ ] Enumeration parity green on the invite form: body, status **and** timing
- [ ] `Accept_BySomeoneElsesVerifiedAddress_CreatesNothingAndRevealsNothing` green — the forwarded-link
      invariant is the security core of this milestone
- [ ] Aggregate invariants green: exactly one owner, atomic transfer, no elevated managed member
- [ ] FR-015's sole-owner guard green end to end, closing the criterion carried from M1
- [ ] Architecture fitness functions green, including the extended NFR-004 model review
- [ ] Localization parity green, including the invitation mail
- [ ] Browser walkthrough completed in DE/EN/FR with a clean console — **two accounts, two browser
      profiles**: create → invite an address with no account → sign up from the link → invite an
      address that has one → accept in-app → decline → managed member → promote → role change →
      transfer → leave → delete
- [ ] Matrix reconciled against the tests that actually exist, so every row names a real class
- [ ] Suite green locally; **"CI green" is carried to milestone 7**, not ticked
