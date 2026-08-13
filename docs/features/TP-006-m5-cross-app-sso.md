---
id: TP-006
feature: FEAT-006
status: Draft
related: [UC-MAIN-017, FR-031, FR-008, NFR-001, NFR-005, ADR-0039, ADR-0021, ADR-0013]
---

# TP-006 — Test plan for FEAT-006 (M5 — Cross-app SSO)

## Scope & objectives

Verify the cross-app single-sign-on hand-off ([FEAT-006](FEAT-006-m5-cross-app-sso.md),
[FR-031](../requirements/functional/FR-031-single-sign-on-across-apps.md)): a user signed in on the
authority origin can start an **independent** session on a sibling origin without re-entering
credentials, via the redirect-based authorization-code flow of
[ADR-0039](../adr/0039-cross-app-single-sign-on.md) — while the account-free product, the offline
product, and every per-origin token guarantee ([ADR-0021](../adr/0021-web-auth-token-model.md)) stay
**unchanged**.

As with M3's convergence plan and M4's authorization plan, **the risk concentrates in the parts that
fail silently**, and those get most of the testing:

- **The security envelope of the authorization code.** Each control is invisible when it regresses and
  load-bearing when it holds, so each is a first-class assertion, not a happy-path corollary:
  - **single-use** — a second `connect/token` with the same code is **denied**, and only one session is
    ever produced;
  - **short-lived** — a code presented after its TTL is **denied**;
  - **PKCE-bound** — a valid code with a wrong, absent, or mismatched `code_verifier` is **denied**
    (possession of the code alone must be useless);
  - **origin-bound** — `connect/authorize` with a `return_uri` **off the allow-list** issues **no code**,
    and the allow-list is asserted to be **the same configuration source** as `Cors:AllowedOrigins`
    ([ADR-0013](../adr/0013-wasm-api-dev-wiring.md)) rather than a second hand-maintained copy;
  - **at rest** — the code is stored **hashed**, never in plaintext (like the refresh secret).
- **The degrade path.** A broken hand-off must **never dead-end**: with the authority unreachable, the
  sibling shows its **own** sign-in and a normal password sign-in still succeeds; the account-free and
  offline experiences are untouched ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Session-shape parity.** The session produced by `connect/token` is **identical** to a password
  sign-in ([FR-008](../requirements/functional/FR-008-sign-in-with-password.md)): access token in
  memory, refresh token persisted and rotating, independent per origin — verified by driving a
  **refresh** on the bootstrapped session, not just by inspecting the first response.

Out of scope: a shared cookie/BFF (explicitly rejected, ADR-0039 option 3); a dedicated identity
origin; a **silent** (`prompt=none`) hand-off (deferred — first cut is interactive); **single
sign-out** across apps (a separate slice); any change to `login`/`refresh`/passkeys/sync/circles
(this slice is additive).

## Test levels

- **Unit** — pure logic, **no I/O**: PKCE verification (`SHA-256(verifier)` base64url == challenge;
  reject on mismatch/absent/wrong method); the `return_uri` allow-list check (exact-origin match,
  scheme/host/port, no suffix or substring escape); authorization-code creation binding
  `(userId, client, return_uri, code_challenge, expiresAt)`; the code-store single-use/expiry state
  machine with an injected `TimeProvider`. On the client: the PKCE verifier/challenge generation and the
  callback URL parse + **code-strip**.
- **Integration — the two-endpoint harness (`CircleTools.Main.Service.Test`)** — the **load-bearing
  level**, against **real Postgres** ([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)) and
  the real DI graph. A **bearer-authorized** `connect/authorize` mints a code; an **anonymous**
  `connect/token` redeems it. Asserts **status codes and end states**: happy-path mint→redeem yields a
  `SessionResponse`; second redeem of the same code is denied (single-use); expired code denied;
  wrong/absent `code_verifier` denied (PKCE); off-allow-list `return_uri` denied at authorize;
  unauthenticated `connect/authorize` is 401; the returned refresh token **actually refreshes** through
  the existing `/refresh` path (parity with FR-008).
- **Contract** — `ConnectAuthorizeRequest` / `ConnectAuthorizeResponse` / `ConnectTokenRequest` in
  `CircleTools.Main.Contracts`, and that `connect/token` **reuses `SessionResponse`** (no new response
  DTO); the new `AccountApiClients` route constants match the server routes.
- **Architecture** — the connect flow client code lives in **`CircleTools.Ui.Account`** (no app-UI
  coupling, `Ui.AccountLayeringTests`); the server endpoints reuse `SessionService` and do not
  introduce a second session path; no Blazor/JS-interop dependency leaks into the block.
- **UI/component (bUnit)** — the "Continue with your account" affordance appears only when there is **no
  local session**; choosing it invokes the redirect seam (mocked `NavigationManager`); the callback
  handler exchanges a `?code=…`, stores the session, and **strips the code**; the authority-unreachable
  path renders the **local sign-in fallback**, not a dead-end.
- **End-to-end / exploratory** — the **two-origin** browser walkthrough `CLAUDE.md` requires, in DE/EN/FR
  (see exit criteria). This is the only place the cross-origin redirect is seen whole and on screen — and
  the reveal-page precedent stands: real defects here (a code left in the URL, a blank screen on a failed
  exchange, a mistranslated fallback) are invisible to a green unit suite.

## Test matrix

Class/method names are **plan-time** and will be reconciled against the code at close (the M2 lesson: a
plan-time name is a promise, not evidence).

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| **FR-031 single-use: second redeem denied, one session only** | `ConnectTokenTests.ASecondRedemptionOfTheSameCode_IsDenied_AndOnlyOneSessionIsIssued` | Integration | ✅ |
| **FR-031 expired code denied** | `ConnectTokenTests.AnExpiredCode_IsDenied` | Integration | ✅ |
| **FR-031 PKCE: wrong/absent verifier denied** | `ConnectTokenTests.AValidCodeWithAWrongOrMissingVerifier_IsDenied` | Integration | ✅ |
| FR-031 PKCE verification is correct (S256) | `PkceTests.VerifierHashingMatchesChallenge_AndRejectsMismatch` | Unit | ✅ |
| **FR-031 off-allow-list return_uri issues no code** | `ConnectAuthorizeTests.AReturnUriOffTheAllowList_IssuesNoCode` | Integration | ✅ |
| **FR-031 allow-list source == CORS allow-list** | `ConnectOriginTests.TheReturnUriAllowList_IsSourcedFromTheSameConfigAsCors` | Integration / Architecture | ✅ |
| FR-031 authorize requires authentication | `ConnectAuthorizeTests.ConnectAuthorize_RequiresABearerToken_401WhenAnonymous` | Integration | ✅ |
| FR-031 happy path mint→redeem yields a session | `ConnectFlowTests.AMintedCode_RedeemsToASession_ForTheAuthorityUser` | Integration | ✅ |
| **FR-031 / FR-008 parity: bootstrapped session refreshes** | `ConnectFlowTests.TheBootstrappedRefreshToken_RefreshesThroughTheNormalPath` | Integration | ✅ |
| FR-031 code bound to user/client/return_uri | `ConnectAuthorizeTests.TheCode_IsBoundToTheCallerUser_Client_AndReturnUri` | Integration | ✅ |
| FR-031 code stored hashed, never plaintext | `ConnectCodeStoreTests.TheCode_IsPersistedHashed_NotInPlaintext` | Unit / Integration | ✅ |
| FR-031 code single-use/expiry state machine | `ConnectCodeStoreTests.ConsumeIsAtomic_AndExpiryIsHonoured` (injected clock) | Unit | ✅ |
| **FR-031 / NFR-001 authority unreachable → local sign-in fallback** | `ConnectCallbackTests.WhenTheHandoffFails_TheAppFallsBackToItsOwnSignIn_NotADeadEnd` | Component | ✅ |
| FR-031 account-free/offline unaffected by SSO | `ConnectFlowTests.TheAccountFreeAndOfflineExperience_IsUnchanged` (NFR-001) | Component / Integration | ✅ |
| FR-031 continue affordance only without a local session | `ContinueWithAccountTests.TheAffordanceAppears_OnlyWhenNoLocalSessionExists` | Component | ✅ |
| FR-031 callback strips the code from the URL | `ConnectCallbackTests.AfterExchange_TheCodeIsRemovedFromTheUrl` | Component | ✅ |
| FR-031 client PKCE verifier is generated and kept out of durable storage | `ClientPkceTests.TheVerifierIsGenerated_AndNotWrittenToDurableStorage` | Unit / Component | ✅ |
| FR-031 authority signed-out → its own login, then hand-off completes | `ConnectFlowTests.WhenTheAuthorityHasNoSession_ItSignsInFirst_ThenCompletesTheHandoff` | E2E | ⚠️ browser |
| FR-031 strings localized (DE/EN/FR) | `ConnectStringsTests.EveryConnectString_IsDefinedInEveryLanguage` + placeholder parity | Localization | ✅ |
| Contract: connect/token reuses SessionResponse (no new response DTO) | `ConnectContractTests.ConnectToken_ReturnsTheSameSessionResponse_AsLogin` | Contract | ✅ |
| Architecture: connect flow in Ui.Account, no app-UI coupling; one session path | `UiAccountLayeringTests.ConnectFlowLivesInTheBlock_AndTheServerReusesSessionService` | Architecture | ✅ |

## Test data & environments

- **Postgres** via Aspire/Testcontainers, fresh database per class; the connect-code store's migration
  (if a table backs it) applied with the suite.
- **One authenticated "authority" identity** for `connect/authorize`; the anonymous client for
  `connect/token`. A **distinct, non-suite origin** string for the off-allow-list case.
- **Time via injected `TimeProvider`** for code expiry and the single-use window — no wall-clock waits.
- **A captured code + verifier pair** reused across the negative cases (single-use, wrong verifier),
  proving each rejection independently.
- **Two isolated browser contexts as two origins** for the E2E walkthrough (the M3/M4 two-context
  precedent), one acting as the authority (`main-web`) and one as the sibling (`secretsanta-web`), both
  from the Aspire AppHost.

## Localization & accessibility notes

- **Localization:** parity fails if any `de-CH`/`en`/`fr-CH` key is missing or empty; placeholder parity
  fails if a translation drops a placeholder. No count-bearing strings here, so the M2/M3 singular trap
  does not apply — but the three user-facing strings (the **continue** affordance, the **signing you
  in…** status, the **authority-unreachable fallback**) are each verified on screen in all three
  languages, and the fallback must read as a neutral fact leading into sign-in, not an alarm.
- **Accessibility:** "Continue with your account" is a real keyboard-reachable control; the
  "signing you in…" state is `role="status"` (a fact), the fallback message is not an `alert` that traps
  focus; the flow never leaves the user on a blank screen after a failed exchange.

## Exit criteria

- [ ] All acceptance criteria of FR-031 covered by automated tests (gaps, if any, recorded here — not
      silently truncated)
- [ ] The **security envelope** is green: single-use (second redeem denied, one session only), expiry,
      **PKCE-bound**, **origin-bound** (off-allow-list issues no code), and the assertion that the
      allow-list **shares the CORS configuration source**
- [ ] The code is stored **hashed**; the client **PKCE verifier** is not written to durable storage
- [ ] **Session-shape parity** proven: the bootstrapped refresh token refreshes through the normal
      `/refresh` path, with the same lifetimes as FR-008
- [ ] The **degrade path** is green: authority unreachable → the sibling shows its own sign-in and a
      normal sign-in works; account-free/offline unchanged (NFR-001)
- [ ] `connect/token` reuses `SessionResponse` (no new response DTO); the new route constants match the
      server routes
- [ ] Architecture fitness green: the connect flow lives in `Ui.Account` with no app-UI coupling; the
      server reuses `SessionService` (one session path)
- [ ] Localization parity + placeholder parity green for the three new strings, verified on screen in
      DE/EN/FR
- [ ] **Two-origin browser walkthrough** completed in DE/EN/FR from the Aspire AppHost: sign in on
      `main-web` → open `secretsanta-web` → "Continue with your account" → land signed in with the
      `?code` **stripped** and a **clean console**; the reverse direction; the authority-**signed-out**
      case (authority login appears, then the hand-off completes); and the authority-**unreachable** case
      falling back to local sign-in
- [ ] Matrix reconciled against the tests that actually exist, so every row names a real class
- [ ] Suite green locally
- [ ] **"CI green" carried to milestone 7** if the pipeline gate is not yet wired for this slice
</content>
