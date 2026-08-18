---
status: Accepted
date: 2026-08-18
deciders: [adrian-moll]
tags: [testing, ci, webauthn, e2e]
---

# ADR-0045 — Browser-driving end-to-end test host (Playwright + CDP virtual authenticator)

## Context and problem statement

Two milestone-7 gaps in [arc42 ch. 11](../arc42/11-risks-and-technical-debt.md) — **D4** (the
WebAuthn ceremony is not automated) and **D8** (the sign-up-from-invitation-link journey is not
automated end to end) — both note the same missing thing: **a browser-driving test host**. Passkeys
run on ASP.NET Core Identity's native WebAuthn ([ADR-0009](0009-authentication-and-passkeys.md)); the
attestation and assertion ceremonies happen in `navigator.credentials`, which an in-process
`WebApplicationFactory` cannot perform — so every passkey test is negative or an authorization check,
none creates a real credential, and [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)'s
"no private key, no biometric data" has no stored credential to inspect.

**What test host drives a real browser against the running suite, and can create and verify a genuine
passkey, without a human at the keyboard?**

## Decision drivers

- **A real ceremony, no human.** WebAuthn needs an authenticator; a headless CI run has none. A
  *virtual* authenticator that answers presence and user-verification automatically is the only way.
- **The real topology.** The ceremony's challenge cookie is `SameSite=None; Secure` and the client is
  a separate origin ([ADR-0013](0013-wasm-api-dev-wiring.md)); the test must exercise that
  cross-origin flow, not a collapsed single-origin stand-in.
- **Fit the existing test story.** xUnit, run from `dotnet test`, gated in CI ([ADR-0038](0038-ci-cd-pipeline.md)).
- **Reusable.** D8 must reuse this host rather than grow a second one.
- **Keep the core signal fast.** The browser E2E must not slow or destabilise the main `build-test` job.

## Considered options

1. **Selenium WebDriver** (.NET) — first-class `AddVirtualAuthenticator`, but a second browser stack
   the project uses nowhere else.
2. **Playwright .NET + a CDP virtual authenticator** (`WebAuthn.addVirtualAuthenticator`), booting the
   app via **`Aspire.Hosting.Testing`**.
3. **Playwright .NET + a CDP virtual authenticator**, booting the app by launching the **Aspire
   AppHost as a child process** on its `https` launch profile (chosen).

## Decision outcome

**Chosen: Option 3.** A new `tests/E2E/CircleTools.WebAuthn.E2E.Test` project uses **Playwright .NET**
to drive real Chromium with a **CDP virtual authenticator** (RP-ID resolves to `localhost` in
Development, matching an internal, resident-key, auto-verified authenticator). A collection fixture
launches the **Aspire AppHost as a child process** (`--launch-profile https`), which brings up
Postgres, Mailpit, the Main API (`:7180`) and the Main web client (`:7080`) on the **fixed HTTPS
ports** the standalone WASM client's baked-in `ApiBaseUrl`/`SsoAuthorityUrl` require.

- **Playwright over Selenium:** it is the browser stack the project's manual walkthroughs already use;
  the CDP `WebAuthn` domain gives the virtual authenticator directly, so no second stack is added.
- **AppHost process over `Aspire.Hosting.Testing`:** the idiomatic testing builder assigns *dynamic*
  ports, which will not match the WASM client's static `appsettings.json` origins; making those
  injectable is a product change out of scope here. The `https` launch profile binds the fixed ports,
  so the process approach is the reliable route (and is exactly the manual procedure D4 asked to
  automate).
- **CI:** a **dedicated, gating `webauthn-e2e` job** on `ubuntu-latest` (which has the Docker runtime
  the stack needs). The project is in `CircleTools.slnx` but **not** `CircleTools.NoMaui.slnx`, so the
  core `build-test` job (which runs the NoMaui solution) never executes it — the browser E2E cannot
  slow or flake the main signal, yet is still a required check of its own.
- **NFR-004 inspection:** after registration the stored credential is read back (`GET
  /api/v1/account/passkeys`) and asserted to expose **public metadata only** — no key bytes — and a
  genuine assertion sign-in proves the stored public key is real.
- **Console gate:** real JS faults (uncaught exceptions, explicit `console.error`) fail the test. Two
  test-host artifacts are excluded, as neither is a product fault: Chromium's generic, detail-free
  "Failed to load resource" subresource noise; and the PWA service worker's refusal to register under
  the CI host's *untrusted* dev certificate (headless Chromium enforces a valid cert for the worker
  script even though `IgnoreHTTPSErrors` covers the ceremony's own fetches — a real or trusted-dev cert
  registers it fine, and the worker is irrelevant to the ceremony).

## Consequences

- **Positive:** D4 closed — a committed test creates and verifies a real passkey end to end, and gives
  NFR-004 a stored credential to inspect. The host is reusable for **D8**.
- **Positive:** one new dependency (`Microsoft.Playwright`, via `Directory.Packages.props`); no second
  browser stack.
- **Negative / accepted:** the E2E boots the whole stack (Docker + `dotnet run` the AppHost), so it is
  slower than an in-process test and needs Docker + an HTTPS dev cert on the runner. Isolated to its
  own job, it does not tax the core suite.
- **Follow-ups:** **D8** (invitation-link sign-up E2E) reuses this host; making the WASM `ApiBaseUrl`
  injectable would later allow the idiomatic `Aspire.Hosting.Testing` boot with dynamic ports.
- **New risks:** browser-download/flake surface in CI, contained to the dedicated job; the RP-ID is the
  dev `localhost`, so this does not exercise the production `circletools.ch` RP-ID (a deploy concern,
  R8/milestone 8).

## Pros and cons of the options

### Option 1 — Selenium WebDriver
- 👍 Purpose-built `AddVirtualAuthenticator` ergonomics for exactly this test.
- 👎 A second browser-automation stack the suite otherwise never uses; weaker WASM/networkidle waiting.

### Option 2 — Playwright + `Aspire.Hosting.Testing`
- 👍 The idiomatic way to boot an Aspire graph from a test.
- 👎 Dynamic ports do not match the WASM client's static origins; needs a product change first.

### Option 3 — Playwright + AppHost child process (chosen)
- 👍 Fixed ports match the client config; codifies the proven manual procedure; one browser stack.
- 👎 Launching the AppHost as a process is heavier and less idiomatic than an in-proc host.

## Links

- Related ADRs: [ADR-0038](0038-ci-cd-pipeline.md) (the CI gate this adds a job to),
  [ADR-0009](0009-authentication-and-passkeys.md) (native WebAuthn),
  [ADR-0013](0013-wasm-api-dev-wiring.md) (separate-origin client),
  [ADR-0021](0021-web-auth-token-model.md) (bearer session the passkey login issues)
- arc42 chapters: [11 Risks & technical debt](../arc42/11-risks-and-technical-debt.md) (D4, D8)
- Requirements / use cases: [FR-010](../requirements/functional/FR-010-manage-passkeys.md),
  [FR-011](../requirements/functional/FR-011-sign-in-with-passkey.md),
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md);
  test plan [TP-002](../features/TP-002-m1-identity.md) (gap 4)
