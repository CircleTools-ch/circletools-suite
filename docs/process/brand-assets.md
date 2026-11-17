# Brand assets and rebranding a fork

Where every piece of the CircleTools brand lives in this repository: names, logos, colours, domains and
store identities. It serves two readers:

- **maintainers** — so a brand change touches every place at once;
- **forks that publish** an app or a public service — the trademark policy (`TRADEMARKS.md` in the
  repository root,
  [ADR-0094](../adr/0094-trademark-and-brand-policy.md)) asks them to rename and replace the logos, and this
  page is the checklist.

Running the official images for yourself does **not** need any of this.

> **Today this is an inventory, not a single switch.** The brand is spread over the files below; a fork
> changes each of them. Gathering them behind one setting is a backlog item (OSS-6b). When you add a new
> brand file or string, add it here in the same change.

## Brand at a glance

| | Hub (Main app) | Secret Santa |
|---|---|---|
| Name in the UI | CircleTools | Secret Santa (stores: "Secret Santa · CircleTools", [ADR-0057](../adr/0057-app-store-naming-and-localization.md)) |
| Brand colour (app bar, theme, mail) | `#03173d` on `#e7e9ee` text | `#7a1f2b` on `#f7ede1` text |
| Brand mark ([ADR-0059](../adr/0059-brand-mark-ring-and-central-glyph.md)) | ring around a trio of people | ring around a wrapped present |
| Web origin | `circletools.ch` | `secretsanta.circletools.ch` |
| App / bundle id | — | `ch.circletools.secretsanta` |

## 1. Logos and icons

| What | Where |
|---|---|
| App-bar marks (both apps) | `src/BuildingBlocks/CircleTools.Ui.Shared/wwwroot/img/app-main.svg`, `app-secretsanta.svg` |
| Mail logos (SVG source + PNG sent in mails) | `src/BuildingBlocks/CircleTools.Ui.Shared/wwwroot/img/email-logo-*.svg` / `.png` |
| Web favicons and PWA icons | `src/Apps/*/CircleTools.*.WebApp/wwwroot/favicon.svg`, `favicon.png`, `icon-192.png`, `icon-512.png` |
| Native app icon and splash | `src/Apps/SecretSanta/CircleTools.SecretSanta.App/Resources/AppIcon/appicon.svg`, `appiconfg.svg`, `Resources/Splash/splash.svg`, `Resources/Images/` |
| Play Store icon and feature graphics (DE/EN/FR) | `store/google-play/assets/` |
| Store screenshots | `store/google-play/screenshots/`, `store/app-store/screenshots/` |
| Docs site logo and favicon | `docfx/templates/circletools/public/logo.svg`, `favicon.svg` |

The store badges in `Ui.Shared/wwwroot/img/store-badges/` are Apple's and Google's artwork (see `NOTICE`),
not ours; a fork keeps or drops them under those companies' rules.

## 2. Colours

| Where | Values |
|---|---|
| App bar per app | `AppBranding` registration in `MainUiServiceCollectionExtensions.cs` and `SecretSantaUiServiceCollectionExtensions.cs` |
| Browser and PWA theme | `theme-color` in each WebApp's `wwwroot/index.html`; `theme_color` / `background_color` in `manifest.webmanifest` |
| Native icon background and splash | `Color` on `MauiIcon` / `MauiSplashScreen` in `CircleTools.SecretSanta.App.csproj`; the loading background in the App's `wwwroot/index.html` |
| Transactional mail | `Apps:*:BrandColor` in the API's `appsettings*.json`; fallback in `MailTemplateRenderer.cs` |

## 3. Names in the UI and in mails

| Where | What |
|---|---|
| Page titles and PWA names | `<title>` in each `wwwroot/index.html`; `name` / `short_name` in `manifest.webmanifest` |
| Launcher label | `ApplicationTitle` in `CircleTools.SecretSanta.App.csproj` |
| UI strings (DE / EN / FR, all four `.resx` per set) | Secret Santa `AppStrings` (`AppName`, `ActivityName`, share and reveal messages, …); Main `AppStrings` (`Greeting`); `AccountStrings` (`Connect*`); `SharedStrings` (`Legal_*` — privacy, imprint, deletion pages) |
| App switcher and per-app mail identity | `Apps` section in `CircleTools.Main.WebApp/wwwroot/appsettings*.json` and in the API's `appsettings*.json` (`Name`, `SenderName`, `LogoUrl`, `BaseUrl`) |
| Legal pages | The operator, contact and hosting facts in `SharedStrings` `Legal_*`; a self-host or fork must not publish ours as its own (self-host operator pages: backlog SH-8) |

Code identifiers — the `CircleTools.*` namespaces, project names, database names, container names — are not
shown to users and may stay as they are.

## 4. Domains, identities and store accounts

| Where | What |
|---|---|
| Hosted origins | `deploy/Caddyfile`, `deploy/compose.yaml`, `deploy/.env.example`, the API's `appsettings.Production.json` (CORS, passkey RP-ID and origins, app base URLs) |
| App defaults to the hosted service | `ApiBaseUrl` / `RevealBaseUrl` in `CircleTools.SecretSanta.App.csproj` |
| Android App Links | `DataHost` in `Platforms/Android/MainActivity.cs`; `.well-known/assetlinks.json` in both WebApps |
| iOS Universal Links and passkeys | `Platforms/iOS/Entitlements.plist` (and MacCatalyst); `.well-known/apple-app-site-association` in both WebApps |
| App and bundle id | `ApplicationId` in `CircleTools.SecretSanta.App.csproj` — a published fork **must** use its own |
| Store listings | `store/google-play/`, `store/app-store/` (texts, contact, demo account) |
| Mail sender | `Mail` section in the API settings and `deploy/.env.example` |

## Related

- Trademark policy: `TRADEMARKS.md` in the repository root · [ADR-0094](../adr/0094-trademark-and-brand-policy.md)
- [Naming](naming.md) — how the names were chosen
- [ADR-0057](../adr/0057-app-store-naming-and-localization.md) store names ·
  [ADR-0059](../adr/0059-brand-mark-ring-and-central-glyph.md) brand mark ·
  [ADR-0064](../adr/0064-transactional-email-html-and-per-app-branding.md) mail branding
