---
id: TP-002
feature: FEAT-002
status: Reviewed
related: [UC-MAIN-001, UC-MAIN-002, UC-MAIN-003, UC-MAIN-004, UC-MAIN-005, UC-MAIN-006, UC-MAIN-007, FR-007, FR-008, FR-009, FR-010, FR-011, FR-012, FR-013, FR-014, FR-015, NFR-003, NFR-004, NFR-005]
---

# TP-002 — Test plan for FEAT-002 (M1 — Identity)

## Scope & objectives

Verify the M1 identity slice ([FEAT-002](FEAT-002-m1-identity.md)): account creation and email
verification, password and [Passkey](../glossary/passkey.md) sign-in, session lifetimes with refresh
rotation, password reset, profile and language, email change, data export, and account deletion —
plus the two cross-cutting NFRs, **data protection & retention** and **account-enumeration
resistance**.

Two areas carry disproportionate risk and get disproportionate testing:

1. **Refresh-token rotation and reuse detection** — custom code, security-critical, with a concurrency
   case (racing refresh) that is easy to get subtly wrong.
2. **Enumeration resistance** — its failure mode is *invisible* to ordinary functional tests. A leaky
   endpoint passes every "does sign-up work" test.

Circles, sync, the claim flow and Secret-Santa linking are **out of scope** (M2–M4).

## Test levels

- **Unit** — export assembly, deletion policy and `IAccountDeletionGuard`, profile rules, token-chain
  state machine (no I/O; in-memory fakes, injected `TimeProvider`).
- **Integration** — endpoints against **real Postgres** (Aspire/Testcontainers) and **real SMTP**
  (Mailpit per [ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)), asserting on messages
  actually delivered to the sink rather than on a mocked sender.
- **Authorization** — the export boundary, reusing the
  [ADR-0020](../adr/0020-server-side-assignment-privacy.md) tests rather than writing a parallel set.
- **Security / timing** — paired enumeration tests per address-accepting endpoint.
- **Architecture** — ASP.NET Core and EF absent from `Main.Service.Core`; `AppSuite.Identity` not
  referenced by any Secret-Santa project ([ADR-0019](../adr/0019-member-user-linking.md)).
- **UI/component (bUnit)** — the account pages, composed over a stub transport rather than a fake API
  client, so the assertions are about how a page reacts to what the API actually says. Two themes
  dominate: the deliberately uninformative answers that keep NFR-005 intact, and the rule that a
  cancelled passkey prompt is not a failure.
- **Client HTTP (`AppSuite.ApiClient.Test`)** — the authorizing handler and the token stores in
  isolation. This is where "exactly one refresh" is pinned; getting it wrong is what makes a
  legitimate client look like a token thief to the server.
- **Localization** — key parity across `de-CH`/`en`/`fr-CH`, **including the four email templates**.
- **Password policy** — the length rule and the breached-list rule are verified at three levels: the
  validator and its corpus in isolation (unit), both rejections through the running API (integration),
  and the sentence that reaches the user (component + browser). The corpus test is the one that matters
  most: a list that failed to load would make the check accept every password with nothing else
  noticing, and building the list byte-wise instead of character-wise did in fact slip 98 too-short
  entries past every other level ([ADR-0027](../adr/0027-breached-password-check.md)).
- **Exploratory** — the full browser walkthrough required by `CLAUDE.md`, in all three languages.
  This is not a formality here: the walkthrough found six defects that every automated level above
  passed clean — a CSP violation from Blazor's own import map, a captive-dependency startup failure,
  a language picker that silently rendered the neutral strings, a language switch that did not reach
  the layout, a delete button whose first click was swallowed because its field bound on blur, and a
  post-deletion redirect that bounced the user to sign-in instead of a farewell. Where a defect could
  be pinned by a component test afterwards, it was.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-007 sign-up + verification | `AccountEndpointsTests.Register_CreatesPendingAccountAndQueuesVerificationMail` + `.VerifyEmail_MarksTheAccountVerified`; `MailDeliveryTests.VerificationMail_CarriesALinkThatActuallyVerifies` takes the link out of the *delivered* mail and uses it | Integration | ✅ |
| FR-007 existing address | `AccountEndpointsTests.Register_ExistingAddress_IsIndistinguishableButWarnsTheRealOwner`; `MailTemplateRendererTests.SignUpAttempt_TellsTheOwnerWithoutConfirmingAnythingToTheRequester` | Integration / Unit | ✅ |
| FR-007 resend verification | `ResendVerificationTests` (a pending account gets a fresh mail; an unknown address and an already-verified one answer identically and send nothing) | Integration | ✅ |
| FR-007 password policy — length | `AccountEndpointsTests.Register_ShortPassword_IsRejected` (rejected, and reported as the `PasswordTooShort` code) | Integration | ✅ |
| FR-007 password policy — **breached list** | `BreachedPasswordValidatorTests` (corpus hits rejected with their own code; an unbreached passphrase accepted; comparison is case-sensitive; a missing password is left to Identity's own validators) | Unit | ✅ |
| FR-007 / ADR-0027 the corpus itself | `BreachedPasswordValidatorTests.TheBundledCorpus_*` (the embedded resource loads, holds ~31 500 entries, and contains nothing under 12 characters — a list that failed to load would make the check pass every password in silence) | Unit | ✅ |
| FR-007 breached password end to end | `AccountEndpointsTests.Register_BreachedPassword_IsRejected` + `.Register_BreachedPassword_CreatesNoAccount` (the address stays free for a second attempt) | Integration | ✅ |
| FR-009 policy holds on recovery too | `PasswordResetTests.Reset_BreachedPassword_IsRejectedAndTheOldPasswordStillWorks` | Integration | ✅ |
| NFR-003 policy failures are translatable | `AccountEndpointsTests.PasswordRejections_AreReportedAsCodesNotEnglishProse` — the API answers with codes, never Identity's English prose | Integration | ✅ |
| FR-007 pending account may sign in | `AccountEndpointsTests.Login_BeforeVerification_SucceedsButReportsUnverified`; `AccessTokenIssuerTests.Issue_MarksAPendingAccountAsUnverified`; `TokenAuthStateProviderTests.PendingAccount_IsSignedInButNotVerified` | Integration / Unit / Component | ✅ |
| FR-007 pending account is *limited* | **not testable in M1** — the criterion says sync, claiming and circle join/invite stay unavailable, and none of those surfaces exists before M2/M3. The claim is carried by the unverified marker above; the restriction itself arrives with the first surface that has to honour it. | — | n/a |
| FR-007 / NFR-004 30-day purge | `PendingAccountPurgeTests` (inside the window kept, past it purged, verified accounts untouched however old, no-op when nothing is expired) — against real Postgres, clock injected | Integration | ✅ |
| FR-008 session issuance | `AccessTokenIssuerTests` (configured access-token lifetime, the claims endpoints authorise on, a distinct id per token, refuses to sign with a weak key); `SessionServiceTests.Start_IssuesBothTokensAndPersistsTheChain` | Unit | ✅ |
| FR-008 the refresh secret is never stored | `SessionServiceTests.Start_StoresOnlyTheHashOfTheRefreshSecret`; `RefreshTokenSecretTests` (the handle is not the secret, is unpredictable, is deterministic for lookup, and fits its column) | Unit | ✅ |
| FR-008 rotation | `RefreshTokenChainTests.Redeem_UnusedToken_RotatesAndInvalidatesPredecessor` + `.Redeem_SlidesTheWindow_OnEveryRotation`; `RefreshTokenChainStoreTests.Rotation_PersistsConsumptionAndSuccessor`; `AccountEndpointsTests.Refresh_RotatesTheTokenAndTheNewOneWorks` | Unit / Integration | ✅ |
| FR-008 **reuse = theft** | `RefreshTokenChainTests.Redeem_ConsumedTokenReplayedOutsideGrace_IsTheft_AndRevokesWholeChain` + `.Redeem_AfterTheftDetected_EvenTheValidSuccessorIsRefused`; `RefreshTokenChainStoreTests.ReuseAcrossRequests_IsDetectedAndRevokesTheChain`; `SessionServiceTests.Refresh_PersistsRevocationWhenReuseIsDetected` | Unit / Integration | ✅ |
| FR-008 **racing refresh** | `RefreshTokenChainTests.Redeem_TwoConcurrentRefreshes_DoNotRevokeChain` + `.Redeem_ReplayAtTheGraceBoundary_IsStillTreatedAsConcurrent` + `.Redeem_RetainsTheSuccessorSecretOnlyForTheGraceWindow`; `SessionServiceTests.Refresh_ConcurrentCallers_BothReceiveTheSameUsableSecret`; `RefreshTokenChainStoreTests.ConcurrentRefreshAcrossRequests_ReturnsTheSameUsableSecret`; `AccountEndpointsTests.Refresh_ConcurrentCallers_BothSucceedWithTheSameToken` (ADR-0021's explicit case, covered at all three levels) | Unit / Integration | ✅ |
| FR-008 reload → silent refresh | `TokenAuthStateProviderTests` (no token = anonymous; an unreadable token is treated as signed out rather than trusted; claims the UI needs are readable) | Component | ✅ |
| FR-008 client refresh discipline | `AuthorizedHttpMessageHandlerTests` (401 → one refresh → one retry; **N concurrent 401s trigger exactly one refresh**; a token near expiry is renewed before the request goes out; a failed refresh returns the 401 and signs out; the refresh call itself bypasses the handler) | Client HTTP | ✅ |
| FR-008 retry fidelity | `AuthorizedHttpMessageHandlerTests.Retry_ResendsTheOriginalBodyAndItsRequestOptions` — the retry keeps the body **and** the cross-origin credentials marker the ceremonies need | Client HTTP | ✅ |
| FR-008 / ADR-0026 "stay signed in" | `RefreshTokenStoreTests` (durable vs session scope; saving to one clears the other; rotation keeps the scope it found; sign-out clears both) | Client HTTP | ✅ |
| ADR-0010 failure mapping | `ApiResponseReaderTests` (status → failure; `ValidationProblem` field messages kept; the rate limiter's bare `{message}` recognised; a proxy's HTML page reported as the status alone) | Client HTTP | ✅ |
| FR-009 reset flow | `PasswordResetTests` (`Reset_ReplacesThePasswordAndRevokesEverySession`, `.Reset_LinkIsSingleUse`, `.Reset_AlsoVerifiesAPendingAddress`, `.Reset_ShortPassword_IsRejected`) | Integration | ✅ |
| FR-009 reset does not lock the old password | `PasswordResetTests.RequestingReset_DoesNotInvalidateTheCurrentPassword` | Integration | ✅ |
| FR-010 passkey management | `PasskeyTests` (credentials listed with their labels, scoped to their owner, another user's cannot be removed, every endpoint requires authentication, removal leaves password sign-in working, creation options issued for a signed-in user) | Integration | ✅ |
| FR-010 no private/biometric data | **not covered** — no test inspects a stored credential, because no test can register one: creating a credential needs a real WebAuthn authenticator. See the coverage notes. | — | ❌ |
| FR-011 passkey sign-in — negative paths only | `PasskeyTests.RequestOptions_NameNoUser_SoTheyRevealNothing` and `.PasskeyLogin_WithGarbage_IsRefused`; `AccountPageTests.CancelledPasskeySignIn_*` / `.BrokenPasskeyCeremony_*`. **A successful ceremony is not automated** — see the coverage notes. | Integration / Component | ⚠️ partial |
| FR-012 profile & language | `ProfileAndEmailChangeTests.UpdateProfile_*` (name and language change, empty display name rejected, duplicate display names allowed, authentication required) | Integration | ✅ |
| FR-013 email change | `ProfileAndEmailChangeTests.ChangeEmail_KeepsTheOldAddressWorkingUntilConfirmed`, `.ConfirmEmailChange_MovesTheAccountToTheNewAddress`, `.ChangeEmail_ToATakenAddress_IsIndistinguishableAndSendsNothing`, `.ConfirmEmailChange_ForAnotherUser_IsRefused` | Integration | ✅ |
| FR-014 export contents | `ExportAndDeletionTests.Export_ContainsTheCallersOwnProfile`, `.Export_ExplainsWhatIsDeliberatelyAbsent`, `.Export_RequiresAuthentication` | Integration | ✅ |
| FR-014 **export leaks nothing** | `ExportAndDeletionTests.Export_NeverReturnsAnotherUsersData`. The ADR-0020 assignment-authorization suite this row once promised to reuse does not apply yet: **no assignment is stored server-side in M1**, so there is nothing for an export to over-share. It becomes real in M4. | Authorization | ⚠️ partial |
| FR-014 archive delivery | **not applicable in M1** — the archive is a few hundred bytes and is returned inline, so there is no link to be single-use or short-lived. The [ADR-0022](../adr/0022-account-deletion-and-data-export.md) link arrives with the first export large enough to generate asynchronously (M2+). | — | n/a |
| FR-015 deletion | `ExportAndDeletionTests.Delete_RemovesTheAccountAndEndsEverySession` + `.Delete_FreesTheAddressForAFreshSignup` (no soft-deleted row can remain, or the address would still be taken); `SessionServiceTests.Refresh_AfterTheAccountIsDeleted_RevokesRatherThanIssuing` | Integration / Unit | ✅ |
| FR-015 re-auth required | `ExportAndDeletionTests.Delete_WithoutTheCorrectPassword_IsRefused` + `.Delete_RequiresAuthentication` | Integration | ✅ |
| FR-015 history survives | **not applicable in M1** — the criterion is about a completed draw outliving its organizer's account, and Secret-Santa data is not stored server-side until M4. Nothing on the server can be affected by deletion because nothing is there. | — | n/a |
| FR-015 local data survives | manual browser check — deletion is server-side only | Exploratory | ❌ (manual) |
| FR-007 sign-up screen | `AccountPageTests.SignUp_ShowsTheSameConfirmationWhateverTheAddress` | Component | ✅ |
| FR-007 / NFR-003 the rule is explained | `AccountPageTests.SignUp_SaysWhyThePasswordWasRefused_InTheUsersLanguage` and `.SignUp_ExplainsABreachedPassword_RatherThanJustRefusingIt` — the code becomes a localized sentence, not a code on screen; `.SignUp_PassesThroughAMessageItHasNoTranslationFor` pins the fallback the mixed contract needs | Component | ✅ |
| FR-007 verification banner | `AccountPageTests.VerificationBanner_*` (shown only while unverified; "send again" reports sent) | Component | ✅ |
| FR-007 verification link | `AccountPageTests.VerifyEmail_*` (the token is passed through exactly as it arrived; an incomplete link explains instead of calling the API) | Component | ✅ |
| FR-009 forgot screen | `AccountPageTests.ForgotPassword_ShowsTheSameConfirmationForAnyAddress` | Component | ✅ |
| FR-011 **cancel is not an error** | `AccountPageTests.CancelledPasskeySignIn_LeavesThePageWithNoError` — nothing with `role="alert"` appears, and no assertion is posted | Component | ✅ |
| FR-011 broken ceremony does report | `AccountPageTests.BrokenPasskeyCeremony_DoesSayWhatWentWrong` | Component | ✅ |
| FR-013 confirm-change link states | `AccountPageTests.ConfirmEmailChange_*` (arriving signed out, and arriving as a different account, each get their own message rather than a bare 401) | Component | ✅ |
| FR-014 export reaches the user | `AccountPageTests.Export_HandsTheArchiveToThePlatformToSave` (file name, content type, contents) | Component | ✅ |
| FR-015 deletion is deliberate | `AccountPageTests.DeleteAccount_RequiresThePasswordBeforeTheButtonDoesAnything` + `.DeleteAccount_OnSuccess_SaysTheAccountIsGoneAndDropsTheSession` | Component | ✅ |
| Client layering | `MainUiLayeringTests` (`Main.Ui` depends on neither `AppSuite.Identity`, `Main.Service`, EF Core nor the WebAssembly host; `ApiClient` and `Ui.Abstractions` stay platform-neutral) | Architecture | ✅ |
| NFR-003 string parity | `LocalizationParityTests` — one in `Main.Ui.Test`, one in `SecretSanta.Ui.Test`: every neutral key is present and non-empty in `de-CH`, `en` and `fr-CH` | Localization | ✅ |
| NFR-003 mail templates | **not** the parity test — the templates are code in `MailTemplateRenderer`, not RESX, so they are covered by `MailTemplateRendererTests` (each template renders in all three languages; `Normalize_ReducesCultureNamesToASupportedLanguage`). The row that claimed the parity test was "extended to the email templates" described a design that was not built. | Unit | ✅ |
| NFR-003 email language | `PasswordResetTests.ResetMail_UsesTheAccountsLanguage` (the *account's* language, not the requester's browser) and `MailDeliveryTests.Registering_DeliversTheVerificationMailInTheChosenLanguage`, both asserting on messages delivered to Mailpit | Integration | ✅ |
| NFR-004 data minimisation | `PersistedModelReviewTests.NoPersistedProperty_HoldsDataTheSuitePromisedNotToKeep` — scans every persisted property of the whole model for date-of-birth, postal, phone and payment fragments. **Found a real breach when written:** `IdentityUser` ships `PhoneNumber`/`PhoneNumberConfirmed` and the first migration created both columns; they are now mapped away and dropped. The assignment half of the criterion is M4 (nothing server-side yet). | Architecture | ✅ |
| NFR-004 credential storage | `PersistedModelReviewTests.ThePassword_IsPersistedOnlyAsAHash` and `.TheRefreshTokenTable_StoresHandlesNotSecrets`. The passkey half (no private key, no biometric data) is **not covered** — see the coverage notes. | Architecture | ⚠️ partial |
| **NFR-005 identical responses** | Asserted per surface next to that surface's own tests, not in one parameterised class: `AccountEndpointsTests.Register_ExistingAddress_*` and `.Login_UnknownAddressAndWrongPassword_AreIndistinguishable`, `PasswordResetTests.ForgotPassword_UnknownAddress_LooksIdenticalAndSendsNothing`, `ProfileAndEmailChangeTests.ChangeEmail_ToATakenAddress_IsIndistinguishableAndSendsNothing`, `ResendVerificationTests.UnknownAddress_*` and `.AlreadyVerifiedAccount_*` | Security | ✅ |
| **NFR-005 timing** | `EnumerationTimingTests` — interleaved samples, medians compared, per surface. **Found two real leaks when written:** sign-in answered 20x faster for an unknown address (no password hash to compute) and sign-up 18x faster for a *taken* one (no account to create). Both are closed by `DecoyPasswordVerifier`. | Security | ✅ |
| NFR-005 queued dispatch | `OutboxTests.MailIsEnqueuedNotAwaited` — one of the two structural guarantees behind the timing test | Unit | ✅ |
| NFR-005 equal password work | `DecoyPasswordVerifier` — the other one: a branch with no password to check spends the hash anyway, so it cannot be told apart by time | Unit / Security | ✅ |
| NFR-005 lockout is not an oracle | `RateLimitingTests.RateLimitRejection_DoesNotRevealWhetherTheAccountExists`, plus `.SensitiveEndpoints_AreRateLimitedPerSource` and `.Refresh_IsNotRateLimited` | Security | ✅ |
| Layering | `LayeringTests` (no EF Core, Npgsql or ASP.NET in `SharedKernel` or `Main.Service.Core`; `Service.Core` does not reach into `Service.Repository`) and `SecretSantaLayeringTests.SecretSanta_DoesNotDependOnTheIdentityModel` (ADR-0019 — the rule exists before M4 gives anyone a reason to break it) | Architecture | ✅ |

## Test data & environments

- **Postgres** via Aspire/Testcontainers; each test class gets a fresh database, migrations applied on
  start.
- **Mailpit** as the SMTP sink, asserted through its HTTP API — mails are verified as *delivered
  messages*, not as mock invocations. This is the payoff of the ADR-0023 seam.
- Time via injected `TimeProvider` throughout; **no wall-clock waits** for the 15-minute/30-day
  boundaries or the 30-day purge — those are advanced virtually.
- The timing test is the one exception: it measures real elapsed time and therefore runs repeated
  samples and compares distributions, never a single pair.
- Passkey ceremonies use a **virtual authenticator** (WebDriver/CDP) rather than real hardware.

## Localization & accessibility notes

- **Localization:** parity fails if any `de-CH`/`en`/`fr-CH` key is missing or empty, now including
  the four transactional email templates. A separate assertion covers that the mail body language
  follows the **account**, per [UC-MAIN-007](../use-cases/main/UC-MAIN-007-reset-password.md).
- **Accessibility:** failures render through `ProblemAlert`, which always carries `role="alert"`, so a
  screen reader announces them instead of leaving the user waiting on a form that silently refused;
  successes use `role="status"`. Every input has a `<label for>`. The deletion confirmation is
  keyboard-reachable, its button is not the primary style and is disabled until the password is
  entered, so the destructive action is never the default focus.

## Coverage notes / known gaps (no silent truncation)

Three acceptance criteria **cannot be closed in M1**, per [FEAT-002](FEAT-002-m1-identity.md). They are
listed here so the matrix is not mistaken for full coverage:

1. **FR-010/011 — MAUI cross-client passkeys.** Requires `apple-app-site-association` / Digital Asset
   Links served from the real domain. **No test in this plan covers it**; it moves to the M1 deployment
   check once hosting lands.
2. **FR-012 — offline language change.** Requires M3 sync. M1 tests the online path only; the "applies
   locally, pushed on next sync" criterion is untested because the behaviour is not implemented.
3. **FR-015 — sole-circle-owner guard.** Requires M2 circles. `IAccountDeletionGuard` is tested as an
   extension point (a registered failing guard blocks deletion); the *circle* guard itself arrives with
   M2.

Reconciling this plan against the tests that actually exist (2026-07-28) turned up **four more gaps**
that the matrix had been reporting as covered:

4. **Passkey registration and a successful passkey sign-in are not automated.** Every passkey test in
   the suite is a negative or an authorization check — listing, scoping, removal, "garbage is
   refused", "options reveal no user". None of them creates a credential, because creating one needs
   a real WebAuthn authenticator, which an in-process `WebApplicationFactory` does not have. The
   consequence is that **NFR-004's "no private key, no biometric data" has nothing to inspect**: there
   is no stored credential in any test. Both were verified by hand through a CDP virtual authenticator
   during the browser walkthrough; neither runs in CI. Closing this means driving the ceremony from a
   browser in the test suite, which belongs with the milestone-7 end-to-end work.
5. **FR-015 "history survives" and the export authorization suite have nothing to act on in M1.**
   Both are about Secret-Santa data on the server, and there is none until M4. They are marked `n/a`
   above rather than ✅.
6. **FR-007's pending-account restrictions cannot be tested**, because sync, claiming and circles do
   not exist yet. Only the "may sign in, and is marked unverified" half is real in M1.
7. **The matrix named ~17 test classes that were never written under those names.** `RegisterTests`,
   `LoginTests`, `RefreshRotationTests`, `DeleteAccountTests`, `EnumerationTests` and others were
   plan-time names; the behaviour is largely covered, but by differently-named classes. Every row
   above now names a class that exists, so the traceability chain points at something real.

Also not covered here: end-to-end journeys across Web **and** native, deferred to milestone 7; and
deliverability (SPF/DKIM/DMARC, bounces), which no local sink can exercise
([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)).

## Exit criteria

- [x] All acceptance criteria covered by automated tests **except those recorded above**. One
      exception is **deferred, not outstanding**: the passkey ceremony (gap 4), which also blocks
      NFR-004's stored-credential check. Automating it needs a browser-driving test host, which is
      milestone 7's work — it is carried there explicitly rather than left as an unticked box here.
      The behaviour itself was verified by hand through a CDP virtual authenticator (see the browser
      walkthrough below).
- [x] Refresh rotation, reuse detection and the racing-refresh case all green — at unit, store and
      endpoint level (2026-07-28)
- [x] Enumeration parity green on every address-accepting endpoint, body/status **and** timing
      (2026-07-28) — the timing half found and closed two 20x leaks on the way
- [x] Export authorization green *for what M1 stores* — `Export_NeverReturnsAnotherUsersData`. The
      assignment-level checks this criterion anticipates have nothing to guard until M4
- [x] Architecture fitness functions green (2026-07-29) — 14 rules, including the new
      Secret-Santa ↛ Identity rule and the NFR-004 model review
- [x] Localization parity green, including the mail templates (via `MailTemplateRendererTests` —
      the templates are code, not RESX)
- [x] Browser walkthrough completed (2026-07-28) — register → verification mail → verify → sign in →
      reload/silent refresh → profile + DE/EN/FR switch with no reload → passkey register, sign in and
      cancel → forgot/reset → change email + confirm → export download → delete. Console clean, **zero
      CSP violations**. Passkey ceremonies driven through a CDP virtual authenticator, per the
      "Test data & environments" note above.
- [x] Password-policy walkthrough completed (2026-07-28) — sign-up refused a 16-character breached
      password and a short one, and accepted an unbreached passphrase, in **DE, EN and FR**, driven
      through the running Aspire stack (Postgres + Mailpit + API + WASM). The rejection reads as a
      sentence in each language rather than an error code, and the console stayed clean apart from
      the deliberate 400s.
- [x] Suite green — **locally, because there is no CI pipeline to be green on.** 281 tests across
      10 projects, 0 failures, on 2026-07-29. "CI green" as the
      [Definition of Done](../process/definition-of-done.md)
      words it is **not satisfiable in M1** and is not being claimed: the pipeline needs a CI/CD
      decision that is still an open item in the
      [ADR backlog](../adr/index.md#open-decisions-backlog), and building it is milestone 7's work.
      Carried there rather than ticked.
