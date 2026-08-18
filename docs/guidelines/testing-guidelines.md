# Testing Guidelines

> **Status: outline.**

## 1. Philosophy
- Test behaviour, not implementation. Fast feedback. The domain is the most-tested layer.

## 2. Test levels
- **Unit** — domain & application logic, no I/O.
- **Integration** — repositories & API against real Postgres/SQLite via Aspire/Testcontainers.
- **Architecture** — dependency-direction & boundary rules (ArchUnitNET) as fitness functions.
- **Contract** — API DTOs / client compatibility.
- **Component (UI)** — Blazor components with bUnit.
- **End-to-end** — critical journeys across Web/native, driven in a **real browser** against the live
  Aspire stack. Tooling: **Playwright .NET** (Chromium), with a **CDP virtual authenticator** for the
  WebAuthn ceremony ([ADR-0045](../adr/0045-browser-driving-e2e-test-host.md)); the host is launched by
  running the AppHost on its `https` profile, so the fixed-port stack matches the WASM client's
  origins. These live under `tests/E2E/`, run in their **own gating CI job** (not the core suite), and
  need Docker + an HTTPS dev cert. First E2E: the passkey register/sign-in round-trip (arc42 D4).

## 3. Conventions
- Framework: xUnit (v3); assertions: FluentAssertions; mocks/fakes: NSubstitute.
- Naming: `MethodOrScenario_State_ExpectedResult`.
- Arrange-Act-Assert; one logical assertion group per test.
- Test projects: `<Subject>.Test`, co-located; `IsTestProject` conventions in `Directory.Build.props`.

## 4. Coverage & gates
- Target a meaningful coverage floor for domain/application (set concretely once code exists).
- CI must be green; architecture tests must pass.

## 5. Test data & determinism
- Inject clock/ids/randomness; no `DateTime.Now`/`Guid.NewGuid()` directly in logic.

## 6. Local-first & sync testing
- Verify offline flows with SQLite; verify reconciliation and conflict handling for sync.

## 7. Localization & accessibility
- Verify culture switching (DE/EN/FR) and formatting; basic a11y checks on components.
