---
id: TP-002
feature: FEAT-002
status: In progress
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
| FR-007 sign-up + verification | `RegisterTests` (pending state created, verification mail delivered to Mailpit, link verifies, user signed in) | Integration | ✅ |
| FR-007 existing address | `RegisterTests.ExistingAddress_*` (identical response; "someone tried to sign up" mail sent instead; no second account) | Integration | ✅ |
| FR-007 password policy — length | `AccountEndpointsTests.Register_ShortPassword_IsRejected` (rejected, and reported as the `PasswordTooShort` code) | Integration | ✅ |
| FR-007 password policy — **breached list** | `BreachedPasswordValidatorTests` (corpus hits rejected with their own code; an unbreached passphrase accepted; comparison is case-sensitive; a missing password is left to Identity's own validators) | Unit | ✅ |
| FR-007 / ADR-0027 the corpus itself | `BreachedPasswordValidatorTests.TheBundledCorpus_*` (the embedded resource loads, holds ~31 500 entries, and contains nothing under 12 characters — a list that failed to load would make the check pass every password in silence) | Unit | ✅ |
| FR-007 breached password end to end | `AccountEndpointsTests.Register_BreachedPassword_IsRejected` + `.Register_BreachedPassword_CreatesNoAccount` (the address stays free for a second attempt) | Integration | ✅ |
| FR-009 policy holds on recovery too | `PasswordResetTests.Reset_BreachedPassword_IsRejectedAndTheOldPasswordStillWorks` | Integration | ✅ |
| NFR-003 policy failures are translatable | `AccountEndpointsTests.PasswordRejections_AreReportedAsCodesNotEnglishProse` — the API answers with codes, never Identity's English prose | Integration | ✅ |
| FR-007 pending account limits | `PendingAccountTests` (may sign in; sync/claim/circle surfaces unavailable) | Integration | ✅ |
| FR-007 / NFR-004 30-day purge | `RetentionJobTests` (29 days kept, 31 days purged, verified accounts untouched) | Integration | ✅ |
| FR-008 session issuance | `LoginTests` (15-min access token, 30-day refresh, access token never written to storage) | Integration | ✅ |
| FR-008 rotation | `RefreshRotationTests` (each refresh issues a new pair; the previous refresh token is invalidated) | Integration | ✅ |
| FR-008 **reuse = theft** | `RefreshRotationTests.ConsumedToken_Replayed_RevokesWholeChain` | Integration | ✅ |
| FR-008 **racing refresh** | `RefreshRotationTests.TwoConcurrentRefreshes_DoNotRevokeChain` (ADR-0021's explicit case) | Integration | ✅ |
| FR-008 reload → silent refresh | `TokenAuthStateProviderTests` (no token = anonymous; an unreadable token is treated as signed out rather than trusted; claims the UI needs are readable) | Component | ✅ |
| FR-008 client refresh discipline | `AuthorizedHttpMessageHandlerTests` (401 → one refresh → one retry; **N concurrent 401s trigger exactly one refresh**; a token near expiry is renewed before the request goes out; a failed refresh returns the 401 and signs out; the refresh call itself bypasses the handler) | Client HTTP | ✅ |
| FR-008 retry fidelity | `AuthorizedHttpMessageHandlerTests.Retry_ResendsTheOriginalBodyAndItsRequestOptions` — the retry keeps the body **and** the cross-origin credentials marker the ceremonies need | Client HTTP | ✅ |
| FR-008 / ADR-0026 "stay signed in" | `RefreshTokenStoreTests` (durable vs session scope; saving to one clears the other; rotation keeps the scope it found; sign-out clears both) | Client HTTP | ✅ |
| ADR-0010 failure mapping | `ApiResponseReaderTests` (status → failure; `ValidationProblem` field messages kept; the rate limiter's bare `{message}` recognised; a proxy's HTML page reported as the status alone) | Client HTTP | ✅ |
| FR-009 reset flow | `PasswordResetTests` (single-use link, expiry, sessions revoked, pending email verified, passkeys retained) | Integration | ✅ |
| FR-009 reset does not lock the old password | `PasswordResetTests.RequestingReset_DoesNotInvalidateCurrentPassword` | Integration | ✅ |
| FR-010 passkey registration | `PasskeyTests` (public key + metadata only stored, label defaulting, duplicate authenticator rejected, removal leaves password usable) | Integration | ✅ |
| FR-010 no private/biometric data | `PasskeyTests.StoredCredential_ContainsNoPrivateKeyOrBiometricData` | Integration | ✅ |
| FR-011 passkey sign-in | `PasskeyLoginTests` (session identical in shape/lifetime to password login; removed credential rejected; cancel is not an error) | Integration | ✅ |
| FR-012 profile & language | `ProfileTests` (display name required, duplicates allowed, language applies immediately, email hidden from other members) | Integration / Component | ✅ |
| FR-013 email change | `EmailChangeTests` (verify-before-swap, old address still valid until confirmed, old address notified, expiry leaves account unchanged) | Integration | ✅ |
| FR-014 export contents | `ExportTests` (profile + memberships + owned groups + own wishlist + permitted assignments; empty account yields a valid archive) | Integration | ✅ |
| FR-014 **export leaks nothing** | `ExportAuthorizationTests` (non-organizer export carries only the caller's own assignment; nothing unreadable via UI appears) | Authorization | ✅ |
| FR-014 archive delivery | **not applicable in M1** — the archive is a few hundred bytes and is returned inline, so there is no link to be single-use or short-lived. The [ADR-0022](../adr/0022-account-deletion-and-data-export.md) link arrives with the first export large enough to generate asynchronously (M2+). | — | n/a |
| FR-015 deletion | `DeleteAccountTests` (account/credentials/passkeys gone, all sessions revoked, no soft-deleted row remains) | Integration | ✅ |
| FR-015 re-auth required | `DeleteAccountTests.WithoutRecentReauthentication_IsRejected` | Integration | ✅ |
| FR-015 history survives | `DeleteAccountTests.CompletedDraw_Unchanged_And_RevealLinksStillResolve` | Integration | ✅ |
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
| FR-015 deletion is deliberate | `AccountPageTests.DeleteAccount_RequiresThePasswordBeforeTheButtonDoesAnything` (disabled without a password; not the primary button style) | Component | ✅ |
| Client layering | `MainUiLayeringTests` (`Main.Ui` depends on neither `AppSuite.Identity`, `Main.Service`, EF Core nor the WebAssembly host; `ApiClient` and `Ui.Abstractions` stay platform-neutral) | Architecture | ✅ |
| NFR-003 string parity | `LocalizationParityTests` extended to the **email templates** | Localization | ✅ |
| NFR-003 email language | `EmailLanguageTests` (reset mail uses the *account's* language, not the requester's browser) | Integration | ✅ |
| NFR-004 data minimisation | `PersistedModelReviewTests` (no DOB/postal/phone/payment columns; assignment columns limited to giver, recipient, group) | Architecture | ✅ |
| NFR-004 credential storage | covered by `PasswordPolicyTests` + `PasskeyTests` above (hash only; no private key) | Integration | ✅ |
| **NFR-005 identical responses** | `EnumerationTests` — parameterised over sign-up, sign-in, reset and email-change: body and status identical for existing vs non-existing addresses | Security | ✅ |
| **NFR-005 timing** | `EnumerationTimingTests` — median response times of the two paths compared over repeated runs; fails if they diverge beyond the harness noise floor | Security | ✅ |
| NFR-005 queued dispatch | `OutboxTests.MailIsEnqueuedNotAwaited` — the structural guarantee behind the timing test | Unit | ✅ |
| NFR-005 lockout is not an oracle | `EnumerationTests.RateLimitedResponses_DoNotRevealAccountExistence` | Security | ✅ |
| Layering | `IdentityLayeringTests` (no ASP.NET/EF in `Main.Service.Core`; no Secret-Santa project references `AppSuite.Identity`) | Architecture | ✅ |

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

Also not covered here: end-to-end journeys across Web **and** native, deferred to milestone 7; and
deliverability (SPF/DKIM/DMARC, bounces), which no local sink can exercise
([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)).

## Exit criteria

- [ ] All acceptance criteria covered by automated tests **except the three recorded above**
- [ ] Refresh rotation, reuse detection and the racing-refresh case all green
- [ ] Enumeration parity green on every address-accepting endpoint, body/status **and** timing
- [ ] Export authorization tests green (no assignment reachable that the UI would refuse)
- [ ] Architecture fitness functions green
- [ ] Localization parity green, including email templates
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
- [ ] CI green
