# Device testing — what runs where

Which of our machines can **build** each head, which devices can **run** it, and what to type. This
is the practical companion to [mobile apps](mobile-apps.md): everything here is about testing before
a store account exists.

## Hardware

| Machine / device | Role |
|---|---|
| Windows PC (Visual Studio) | primary development machine; builds Web, Android and the Windows head |
| Android phone | real-device testing for Android and the web app |
| iPad | **test** device only — never a build device |
| Intel MacBook Pro | possible iOS build host, with conditions ([ADR-0031](../adr/0031-ios-build-host.md)) |

## Matrix

| Target | Build host | Test on | Status today |
|---|---|---|---|
| **Web** (Blazor WASM PWA) | Windows PC | any desktop browser, the Android phone, iPad Safari, the MacBook | ✅ works now |
| **Android** (MAUI head) | Windows PC | Android emulator + the phone over USB | ✅ works now |
| **Windows desktop** (MAUI head) | Windows PC | the PC itself | ✅ target framework already present |
| **iOS / iPadOS** (MAUI head) | **macOS only** | iOS Simulator on the Mac; iPad via deploy or TestFlight | ⛔ blocked on a Mac |

The asymmetry is the whole story: **everything except iOS can be developed and tested on hardware we
already have.** Only iOS needs something new.

## Web

The fastest loop, and the one that covers most feature work. Secret-Santa is a standalone Blazor
WASM app with no backend, so run its project directly:

```bash
dotnet run --project src/Apps/SecretSanta/CircleTools.SecretSanta.WebApp --launch-profile http
# → http://localhost:5081   (the http profile avoids dev-certificate prompts)
```

The Main app and its API run under Aspire instead:

```bash
dotnet run --project src/Aspire/CircleTools.AppHost
```

**Testing on the phone and iPad over the LAN.** Both are useful long before any native build exists —
the web app is the same UI the MAUI heads host. Bind to all interfaces and browse to the PC's LAN
address from the device:

```bash
dotnet run --project src/Apps/SecretSanta/CircleTools.SecretSanta.WebApp --launch-profile http --urls http://0.0.0.0:5081
```

Windows Firewall will ask to allow the port on a private network the first time. This gives real
touch targets, real mobile viewports and real iOS Safari behaviour on the iPad without a Mac.

Per the repository operating manual (`CLAUDE.md`), UI work is not done until it has been driven in a
browser: walk the whole flow, check the empty and error paths, read the browser console, and check all three locales.
State lives in `localStorage` — `localStorage.clear()` for a clean run.

## Android

Everything works from the Windows PC. Install the workload once:

```bash
dotnet workload restore
```

**Emulator** — create a device in the Android Device Manager (Visual Studio), then:

```bash
dotnet build src/Apps/SecretSanta/CircleTools.SecretSanta.App -f net10.0-android -t:Run
```

**Physical phone** — enable *Developer options* → *USB debugging*, plug it in, accept the RSA
prompt, and run the same command; `adb devices` confirms it is visible. A real phone is the better
test: the emulator hides performance and touch behaviour.

Nothing here needs a Google Play account. Play enrolment is only for *distribution*.

## Windows desktop head

The MAUI head also targets `net10.0-windows10.0.19041.0` (unpackaged). Useful as a fast sanity check
of the hybrid host without an emulator, though it is not a shipping target.

## iOS and iPadOS

**There is no way to build iOS on Windows.** Compiling, signing and packaging require Apple's
toolchain, which runs only on macOS. Visual Studio on Windows can drive a build via **Pair to Mac**,
but the Mac does the work.

> **Hot Restart** — the feature that deployed debug builds to an iPhone from Windows without a Mac —
> was **removed in Visual Studio 2026** and exists only in Visual Studio 2022 17.14. It never
> produced a store-shippable artifact. Do not plan around it.

### Version chain (check before installing anything)

| Layer | Requirement |
|---|---|
| .NET 10 iOS workload | Xcode **26.x** |
| Xcode 26.4+ | macOS **26.2 (Tahoe)** |
| macOS 26 on Intel MacBook Pro | **only** the 16-inch 2019 and the 13-inch 2020 **four**-Thunderbolt-port models |

Check the MacBook first: **Apple menu → About This Mac**. If it is not one of those two models, iOS
development on it is impossible today — not slow, impossible. Even if it qualifies, macOS 27 (autumn
2026) drops Intel entirely, so it is a bridge and not a plan. See
[ADR-0031](../adr/0031-ios-build-host.md).

### Once a Mac is available

- **Simulator** — free, no paid Apple account needed. Best for layout, localisation and flow checks,
  including the iPad form factor:
  ```bash
  dotnet build src/Apps/SecretSanta/CircleTools.SecretSanta.App -f net10.0-ios -t:Run
  ```
- **Physical iPad or iPhone** — a plain Apple ID gives *free provisioning*: the app installs but the
  signature expires after **7 days** and must be redeployed. Fine for occasional real-device checks.
- **TestFlight** — the practical way to get builds onto the iPad repeatedly, and onto other people's
  devices. Requires the paid Apple Developer Program and a signed build; see
  [mobile apps](mobile-apps.md).
- **Pair to Mac** — keeps day-to-day work on the Windows PC with the Mac acting as remote build host.
  Both machines need the same .NET and workload versions.

## Known gaps in the MAUI head

Worth knowing before judging what you see on a device — these are tracked, not surprises:

- The app icon and splash are still the **.NET template art** (`#512BD4` purple).
- `ApplicationId` is set to the final `ch.circletools.secretsanta`
  ([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)).
- `RevealBaseUrl` points at `https://localhost:7081`, so reveal links generated on a device will not
  resolve anywhere real yet.
- The head has **no language picker**, so only the default locale is reachable there. Test DE/EN/FR
  in the web app.

## Related

- [Mobile apps](mobile-apps.md) — store publishing runbook
- [ADR-0031](../adr/0031-ios-build-host.md) — iOS build host decision
- [Testing guidelines](../guidelines/testing-guidelines.md) — test levels and strategy
