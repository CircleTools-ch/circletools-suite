---
status: Accepted
date: 2026-07-28
deciders: [Adrian]
tags: [security, identity, privacy, passwords]
---

# ADR-0027 — Breached-password check (bundled ≥12-character list)

## Context and problem statement

[FR-007](../requirements/functional/FR-007-create-suite-account.md) requires passwords to be
"minimum 12 characters, no composition rules, no forced rotation, **checked against a
breached-password list**". Only the length rule was implemented; the breached-password check was
recorded as intended (a note in `AddCircleToolsIdentityCore` says it "is a separate validator, not an
`IdentityOptions` setting") but never built, so an acceptance criterion of a Must-priority
requirement was silently open.

The rule follows NIST SP 800-63B, which pairs a length minimum with a check against known-compromised
secrets and drops composition rules. **Where does the list of known-compromised secrets come from?**

## Decision drivers

- **[NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md) timing.**
  Sign-up must not become measurably slower on any path. An outbound network call on the sign-up path
  is the single largest threat to that, and it lands on the *password* leg rather than the address
  leg, so it is not automatically symmetric.
- **[NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md) / privacy.**
  Sending anything derived from a user's password to a third party needs justification.
- Local-first ethos and hobby scale: no paid service, no new runtime dependency if avoidable.
- Testability without network access — the integration suite runs against containers, not the internet.
- **The length rule shrinks the problem.** Because anything under 12 characters is already rejected,
  only breached passwords of **12 characters or more** can ever reach this check. That is a small
  fraction of any breach corpus.

## Considered options

1. **Bundled offline list, filtered to ≥12 characters**, as an embedded resource behind a seam.
2. **Have I Been Pwned range API** (`api.pwnedpasswords.com/range/{5-char SHA-1 prefix}`, k-anonymity).
3. **Bundled list now, HIBP later** behind the same seam.

## Decision outcome

**Chosen: option 3 — a bundled ≥12-character list now, behind an `IBreachedPasswordList` seam that
keeps the HIBP option open.**

The list is the union of two public corpora, filtered to entries of 12–128 characters and
deduplicated: the **UK NCSC / HIBP "100k most used passwords"** and **`Pwdb_top-1000000`**, both from
[SecLists](https://github.com/danielmiessler/SecLists). Filtering is what makes this practical:

| Corpus | Entries | Entries ≥ 12 characters |
|--------|---------|-------------------------|
| NCSC 100k | 99 840 | 1 212 |
| Pwdb top-1M | 1 000 000 | 31 108 |
| **Union, deduplicated** | — | **31 560** (439 KB) |

Checking 1 000 000 candidate passwords would need megabytes to reject entries the length rule already
rejects. 452 KB, held in a `FrozenSet<string>` built once per process, gives an O(1) ordinal lookup
with no I/O at all — which is what keeps the NFR-005 timing rule intact for free rather than by
careful engineering.

Comparison is **ordinal and case-sensitive**. Breach corpora contain literal passwords; folding case
would reject passwords that were never breached, and the extra strictness is not worth surprising a
user whose passphrase merely resembles a leaked one.

### Rebuilding the list

Refreshing `src/BuildingBlocks/CircleTools.Identity/Passwords/BreachedPasswords.txt` means re-running
the filter over the two source files and committing the result. Three rules are not optional:

1. **Filter on characters, not bytes.** The check compares .NET `string` lengths. A shell filter
   (`awk 'length($0)>=12'`) counts *bytes*, so multi-byte entries slip under the 12-character rule.
   That mistake was made building this very list and was caught only by
   `TheBundledCorpus_HoldsNothingTheLengthRuleAlreadyRejects` — no functional test noticed.
2. **Drop lines carrying U+FFFD or control characters.** Both corpora hold a few dozen mojibake
   artifacts that are not passwords anyone ever typed.
3. **Drop lines with leading or trailing whitespace** — parsing artifacts of the source dumps.

Write UTF-8 without a BOM, LF endings, sorted and deduplicated; the loader reads it line by line.

## Consequences

- **Positive:** no network call, no third party, no key or quota, nothing about a password leaves the
  process. Deterministic and fully testable offline. The NFR-005 timing constraint is satisfied
  structurally rather than by measurement.
- **Negative:** **coverage is far below HIBP's** ~1 billion entries. This catches the passwords people
  actually reuse (`passwordpassword`, `qwertyuiopasdfgh`, `iloveyouiloveyou`) and misses the long
  tail. It is a meaningful improvement over no check at all, not a guarantee that a password is unbreached.
- **Negative:** the list ages. It is a static file with no refresh mechanism; refreshing it means a
  commit.
- **Follow-ups:**
  - Re-evaluate HIBP when the app is deployed and an outbound call is cheap and observable — the seam
    makes it an implementation swap, not a redesign. Revisit the timing question then, since the call
    sits on the password leg.
  - **Password-policy failures must be reported as codes, not prose.** `ApiErrorText.Details` records
    that localizing Identity's English messages "needs the API to return codes rather than prose — a
    change worth making when the policy grows past one rule". The policy now has two rules, so this
    ADR triggers that change: the API returns `PasswordTooShort` / `BreachedPassword`, and the client
    localizes them (NFR-003).
- **New risks:** a false sense of completeness. The check must be described as "known-common
  passwords", never as "your password has not been breached".

## Pros and cons of the options

### 1. Bundled offline list (≥12 chars)
- 👍 Zero I/O on the sign-up path; nothing derived from the password leaves the process; offline-testable.
- 👍 The length rule makes the useful subset small (452 KB rather than tens of MB).
- 👎 Limited coverage; ages without a refresh mechanism.

### 2. HIBP range API
- 👍 Vastly better coverage, continuously updated, no bundled data to maintain.
- 👎 An outbound call on the sign-up path, directly against the NFR-005 timing rule.
- 👎 A third party learns a 5-character SHA-1 prefix — k-anonymous, but not nothing.
- 👎 Needs a failure policy (fail-open weakens the rule; fail-closed makes sign-up depend on someone
  else's uptime), plus network access in tests.

### 3. Bundled now, HIBP behind the seam later
- 👍 Ships the acceptance criterion now at no architectural cost; defers the network question to when
  deployment makes it answerable.
- 👎 Two implementations to reason about eventually.

## Links

- Requirements: [FR-007](../requirements/functional/FR-007-create-suite-account.md),
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md),
  [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
- Related ADRs: [ADR-0009](0009-authentication-and-passkeys.md) (Identity owns credentials),
  [ADR-0010](0010-api-style-and-versioning.md) (failure shape),
  [ADR-0011](0011-localization-format-and-culture.md)
- Feature: [FEAT-002](../features/FEAT-002-m1-identity.md) · Test plan: [TP-002](../features/TP-002-m1-identity.md)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md)
- Guideline: [security-privacy-guidelines.md](../guidelines/security-privacy-guidelines.md)
