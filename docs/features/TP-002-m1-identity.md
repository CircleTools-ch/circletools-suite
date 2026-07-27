---
id: TP-002
feature: FEAT-002
status: Draft
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
- **UI/component (bUnit)** — sign-up/sign-in validation, the verification banner, the deletion
  confirmation screen.
- **Localization** — key parity across `de-CH`/`en`/`fr-CH`, **including the four email templates**.
- **Exploratory** — the full browser walkthrough required by `CLAUDE.md`, in all three languages.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-007 sign-up + verification | `RegisterTests` (pending state created, verification mail delivered to Mailpit, link verifies, user signed in) | Integration | ✅ |
| FR-007 existing address | `RegisterTests.ExistingAddress_*` (identical response; "someone tried to sign up" mail sent instead; no second account) | Integration | ✅ |
| FR-007 password policy | `PasswordPolicyTests` (<12 chars rejected, breached-list hit rejected, no composition rule imposed) | Unit | ✅ |
| FR-007 pending account limits | `PendingAccountTests` (may sign in; sync/claim/circle surfaces unavailable) | Integration | ✅ |
| FR-007 / NFR-004 30-day purge | `RetentionJobTests` (29 days kept, 31 days purged, verified accounts untouched) | Integration | ✅ |
| FR-008 session issuance | `LoginTests` (15-min access token, 30-day refresh, access token never written to storage) | Integration | ✅ |
| FR-008 rotation | `RefreshRotationTests` (each refresh issues a new pair; the previous refresh token is invalidated) | Integration | ✅ |
| FR-008 **reuse = theft** | `RefreshRotationTests.ConsumedToken_Replayed_RevokesWholeChain` | Integration | ✅ |
| FR-008 **racing refresh** | `RefreshRotationTests.TwoConcurrentRefreshes_DoNotRevokeChain` (ADR-0021's explicit case) | Integration | ✅ |
| FR-008 reload → silent refresh | `AuthStateProviderTests` (in-memory token lost on reload, refresh recovers, failure signs out cleanly) | Component | ✅ |
| FR-009 reset flow | `PasswordResetTests` (single-use link, expiry, sessions revoked, pending email verified, passkeys retained) | Integration | ✅ |
| FR-009 reset does not lock the old password | `PasswordResetTests.RequestingReset_DoesNotInvalidateCurrentPassword` | Integration | ✅ |
| FR-010 passkey registration | `PasskeyTests` (public key + metadata only stored, label defaulting, duplicate authenticator rejected, removal leaves password usable) | Integration | ✅ |
| FR-010 no private/biometric data | `PasskeyTests.StoredCredential_ContainsNoPrivateKeyOrBiometricData` | Integration | ✅ |
| FR-011 passkey sign-in | `PasskeyLoginTests` (session identical in shape/lifetime to password login; removed credential rejected; cancel is not an error) | Integration | ✅ |
| FR-012 profile & language | `ProfileTests` (display name required, duplicates allowed, language applies immediately, email hidden from other members) | Integration / Component | ✅ |
| FR-013 email change | `EmailChangeTests` (verify-before-swap, old address still valid until confirmed, old address notified, expiry leaves account unchanged) | Integration | ✅ |
| FR-014 export contents | `ExportTests` (profile + memberships + owned groups + own wishlist + permitted assignments; empty account yields a valid archive) | Integration | ✅ |
| FR-014 **export leaks nothing** | `ExportAuthorizationTests` (non-organizer export carries only the caller's own assignment; nothing unreadable via UI appears) | Authorization | ✅ |
| FR-014 archive links | `ExportTests.ArchiveLink_IsSingleUseAndShortLived` | Integration | ✅ |
| FR-015 deletion | `DeleteAccountTests` (account/credentials/passkeys gone, all sessions revoked, no soft-deleted row remains) | Integration | ✅ |
| FR-015 re-auth required | `DeleteAccountTests.WithoutRecentReauthentication_IsRejected` | Integration | ✅ |
| FR-015 history survives | `DeleteAccountTests.CompletedDraw_Unchanged_And_RevealLinksStillResolve` | Integration | ✅ |
| FR-015 local data survives | manual browser check — deletion is server-side only | Exploratory | ❌ (manual) |
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
- **Accessibility:** form validation messages carry `role="alert"`; the deletion confirmation is
  keyboard-reachable and its destructive action is not the default focus.

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
- [ ] Manual browser walkthrough completed in DE/EN/FR with a clean console
- [ ] CI green
