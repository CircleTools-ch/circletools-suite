---
status: Proposed
date: 2026-11-13
deciders: [Adrian]
tags: [authentication, passkeys, mobile, deep-links, self-hosting]
---

# ADR-0097 — Passkeys and app links on self-hosted servers

## Context and problem statement

Two platform features of the official apps are tied to `circletools.ch` **at build time**:

- **Passkeys** ([ADR-0066](0066-mobile-native-passkeys.md), [ADR-0069](0069-passkey-origin-allow-list-for-native-heads.md)).
  The RP-ID is the apex `circletools.ch`. On **iOS** the app may only create or use passkeys for domains
  in its `webcredentials:` entitlement, which is signed into the app. On **Android**, Credential Manager
  checks that the RP domain's `/.well-known/assetlinks.json` lists the calling app's package and signing
  certificate. Those certificate fingerprints are public.
- **App Links / Universal Links** for reveal, event and join links (`MainActivity` intent filters,
  `applinks:secretsanta.circletools.ch`). Both platforms only open the app for hosts declared in the app.

So the official app **cannot** be told at runtime to treat a self-hoster's domain like ours
([ADR-0096](0096-client-server-selection-and-discovery.md)).

**How do sign-in and shared links work when the official app is connected to a self-hosted server?**

## Decision drivers

- Signing in must always work on a self-hosted server, on both platforms.
- Use passkeys wherever the platform allows it; don't block Android because iOS can't.
- Links must reach the right server's data, and must work for recipients without the app.
- No security shortcut: never send a passkey ceremony for a domain the platform hasn't verified.

## Considered options

1. **Platform-honest: passwords everywhere, passkeys on Android when the server publishes our
   assetlinks; links open the self-hosted web app**
2. **Passwords only on self-hosted servers**
3. **Proxy passkeys through `circletools.ch`** (our domain as RP for every server)

## Decision outcome

**Chosen: Option 1.**

**Sign-in:**

- **Passwords** (with the existing breached-password check, [ADR-0027](0027-breached-password-check.md))
  are always available on self-hosted servers. The UI hides passkey buttons where they cannot work.
- **Android:** the self-host web image serves `/.well-known/assetlinks.json` declaring the official app
  (package name and the Play app-signing fingerprints, current and previous) by default, and the API
  adds the matching `android:apk-key-hash:` origins to its passkey allow-list by default. With that, the
  official Android app can use passkeys with the self-hosted RP-ID. The discovery document
  ([ADR-0096](0096-client-server-selection-and-discovery.md)) advertises it; the app checks it before
  offering passkeys. Operators can switch this off.
- **iOS:** no passkeys from the official app on a self-hosted server — the entitlement cannot be extended
  at runtime. The **web app** of the self-hosted instance (Safari) can still use passkeys for its own
  domain.

**Links:**

- Links created while connected to a self-hosted server point at **that server's** public URL (from the
  discovery document), because online groups' data lives there ([ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md),
  [ADR-0082](0082-short-reveal-links-for-online-groups.md)).
- Those links open in the **browser**, in the self-hosted web app — the official app is not opened
  automatically. Recipients without the app are unaffected, which is the common case anyway.
- Fully client-side reveal links for **offline** groups ([ADR-0014](0014-reveal-link-encoding-and-privacy.md))
  carry their data in the fragment and need no server. They keep using the configured reveal origin:
  the self-hosted web app if the user chose a server, otherwise ours.
- An "Open in app" action on the self-hosted web app (custom URL scheme) is a possible later
  improvement, not part of this decision.

Option 3 is rejected: every server's sign-ins would depend on, and be visible to, the CircleTools
service — the opposite of self-hosting.

## Consequences

- **Positive:** sign-in always works; Android keeps passkeys; no dependency on our infrastructure.
- **Negative:** iPhone users of a self-hosted server sign in with a password in the app; links from a
  self-hosted server open in the browser rather than the app.
- **Follow-ups:** self-host default for `assetlinks.json` and the Android passkey origins (kept in step
  with the values in the API's production settings); passkey availability in the discovery document; UI
  that hides passkeys where unsupported; link origin taken from the discovery document; self-hosting
  guide explains the iOS limitation.
- **New risks:** the Play app-signing key may rotate again (it has rotated once); the self-host defaults
  must be updated in the same release, which the release checklist gets as a step.

## Pros and cons of the options

### Platform-honest mix
- 👍 Best available experience per platform; no shortcuts.
- 👎 Behaviour differs between Android and iOS.

### Passwords only
- 👍 Simplest; same everywhere.
- 👎 Throws away Android passkeys that do work.

### Passkeys via our domain
- 👍 Passkeys on iOS too.
- 👎 Centralises every self-hosted sign-in on our service; complex and against the goal.

## Links

- Related ADRs: [ADR-0009](0009-authentication-and-passkeys.md), [ADR-0066](0066-mobile-native-passkeys.md),
  [ADR-0069](0069-passkey-origin-allow-list-for-native-heads.md), [ADR-0014](0014-reveal-link-encoding-and-privacy.md),
  [ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md), [ADR-0082](0082-short-reveal-links-for-online-groups.md),
  [ADR-0095](0095-self-host-deployment-topology.md), [ADR-0096](0096-client-server-selection-and-discovery.md)
- Backlog: [Open source & self-hosting](../roadmap/backlog.md#open-source--self-hosting-2026-11-13) (CS-4, CS-5)
