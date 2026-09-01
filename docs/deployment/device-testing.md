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

**Physical phone (e.g. the Pixel 9 Pro) — step by step:**

1. On the phone: **Settings → About phone**, tap **Build number** seven times to unlock
   *Developer options*; then **Settings → System → Developer options → USB debugging** on.
2. Plug the phone into the PC with a USB cable and **accept the RSA prompt** ("Allow USB debugging?").
3. Confirm it is visible: `adb devices` should list one device (`adb` ships with the Android SDK
   platform-tools, e.g. `C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe`).
4. Build, install and launch on the phone:
   ```bash
   dotnet build src/Apps/SecretSanta/CircleTools.SecretSanta.App -f net10.0-android -t:Run
   ```
   If MSBuild can't find the JDK/SDK, name them explicitly (VS-installed paths on this machine):
   `-p:JavaSdkDirectory="C:\Program Files\Android\openjdk\jdk-21.0.8"`
   `-p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"`.

A real phone is the better test: the emulator hides performance and touch behaviour.

**For reveal-link sharing to work end to end on the phone**, the reveal page must be reachable at the
link's origin. The MAUI head's `RevealBaseUrl` (`MauiProgram.cs`) is set to the dev machine's **LAN
address** for home testing, so run the WebApp bound to all interfaces and keep it running while you
test:

```bash
dotnet run --project src/Apps/SecretSanta/CircleTools.SecretSanta.WebApp --launch-profile http --urls http://0.0.0.0:5081
```

Then a link shared from the phone (via the new **Share** button → WhatsApp / Messages / e-mail) opens
on any device on the same Wi-Fi. Adjust the IP in `RevealBaseUrl` if the machine's LAN address differs.

> **Account sign-in / sync does not work over the LAN yet.** The Main API is served over HTTPS with
> the local dev certificate, which the phone does not trust. The offline product and reveal-link
> sharing need no API, so they are unaffected; a trusted origin lands with the suite domain.

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

- `ApplicationId` is set to the final `ch.circletools.secretsanta`
  ([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)).
- The app icon and splash are the **Secret-Santa art** (burgundy `#7a1f2b` gift ring), not the old
  .NET template purple.
- The head is at **web parity** (UX-001), including the **language picker** — DE/EN/FR are reachable
  on the device, not just in the web app.
- `RevealBaseUrl` is a **dev LAN default** (`http://192.168.100.122:5081`), not `localhost`, so links
  generated on a device resolve on the home Wi-Fi while the WebApp runs (see above). The literal
  production origin still lands with the suite domain.
- **Account sign-in / sync over the LAN** is the known gap — the dev HTTPS cert is untrusted by the
  phone (see the note above). Offline + sharing are unaffected.
- **Not yet verified on a physical phone:** the Android top safe-area / status-bar behaviour on a
  punch-hole, gesture-nav device (the status bar is tinted via `colorPrimaryDark`, neutral by design —
  [ADR-0040](../adr/0040-ui-component-library-mudblazor.md)).

## Related

- [Mobile apps](mobile-apps.md) — store publishing runbook
- [ADR-0031](../adr/0031-ios-build-host.md) — iOS build host decision
- [Testing guidelines](../guidelines/testing-guidelines.md) — test levels and strategy
