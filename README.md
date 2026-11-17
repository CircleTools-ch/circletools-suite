# CircleTools

[![CI](https://github.com/CircleTools-ch/circletools-suite/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/CircleTools-ch/circletools-suite/actions/workflows/ci.yml)
[![Docs](https://github.com/CircleTools-ch/circletools-suite/actions/workflows/docs-pages.yml/badge.svg?branch=main)](https://circletools-ch.github.io/circletools-suite/)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

**Small apps for families and groups of friends.** CircleTools is a suite of apps under one brand
and one optional account. The first app is **Secret-Santa**: create a group, add members and
wishlists, set exclusions, run a fair draw, and share a private reveal link with each person.

Every app works **offline with no account**: data stays on your device. If you sign in, it **syncs
across devices** and you can reuse your *circles* (the standing groups of people you organize things
with) in every app.

**Try it:** [secretsanta.circletools.ch](https://secretsanta.circletools.ch) · account and circles
at [circletools.ch](https://circletools.ch) · documentation at
[circletools-ch.github.io/circletools-suite](https://circletools-ch.github.io/circletools-suite/)

## Features

- **Secret-Santa:** groups, members, wishlists, exclusions, a fair draw with history, and one or more
  events per group. Reveal links are self-contained, so each person sees only their own match.
- **Offline first:** the web app is an installable Blazor WebAssembly PWA, and the Android and iOS apps
  are built with .NET MAUI Blazor Hybrid from the same feature code.
- **Optional account:** email and password or **passkeys**, email verification, password reset, data
  export and account deletion. Sign in once and use every app in the suite (single sign-on).
- **Circles:** invite people by email, keep members for people who never sign up, and manage roles.
- **Swiss first:** German (`de-CH` by default), French and English.

## Tech stack

.NET 10 · .NET Aspire · Blazor WebAssembly · .NET MAUI Blazor Hybrid · ASP.NET Core · PostgreSQL ·
MudBlazor · DocFX. The code follows Domain-Driven Design with a strict inward-pointing layering
(Domain ← Application ← Infrastructure ← Hosts), checked by architecture tests.

## Getting started

**Prerequisites:** the .NET 10 SDK (the band is pinned in [`global.json`](global.json)) and Docker
(for the local Postgres and Mailpit, and for the integration tests). The Android and iOS heads also
need the MAUI workloads (`dotnet workload restore`).

```bash
git clone https://github.com/CircleTools-ch/circletools-suite.git
cd circletools-suite
dotnet tool restore
dotnet build CircleTools.slnx
dotnet test
```

**Run the whole stack:** Postgres, Mailpit, the API and both web apps, with the Aspire dashboard:

```bash
dotnet run --project src/Aspire/CircleTools.AppHost
```

**Run only the Secret-Santa web app:** offline, no backend needed:

```bash
dotnet run --project src/Apps/SecretSanta/CircleTools.SecretSanta.WebApp --launch-profile http
```

Then open <http://localhost:5081>.

**Build the docs site locally:**

```bash
dotnet docfx docfx/docfx.json --serve
```

## Repository layout

| Path | What |
|------|------|
| [`src/Apps/`](src/Apps/) | The apps: `Main` (account, circles, API) and `SecretSanta` (web, MAUI, core) |
| [`src/BuildingBlocks/`](src/BuildingBlocks/) | Shared kernel, identity, circles, localization, shared UI |
| [`src/Aspire/`](src/Aspire/) | The Aspire AppHost and service defaults for local development |
| [`tests/`](tests/) | Unit, integration, component, architecture and end-to-end tests |
| [`docs/`](docs/) | All documentation as Markdown: arc42, ADRs, requirements, guidelines, process |
| [`deploy/`](deploy/) | Docker Compose, Caddy and observability configuration for the hosted service |

## Documentation

Everything is documented in [`docs/`](docs/) and published at
**[circletools-ch.github.io/circletools-suite](https://circletools-ch.github.io/circletools-suite/)**:

- [Architecture (arc42)](docs/arc42/01-introduction-and-goals.md) and the
  [decision log (ADRs)](docs/adr/index.md): what we built and why
- [Roadmap](docs/roadmap/roadmap.md) and [backlog](docs/roadmap/backlog.md): where the project stands
- [Guidelines](docs/guidelines/coding-guidelines.md): coding, architecture, testing, localization,
  security
- [Glossary](docs/glossary/index.md): the domain terms the code uses

## Contributing

Contributions are welcome. Please read [`CONTRIBUTING.md`](CONTRIBUTING.md) first: the project follows
a documented process ([ways of working](docs/process/ways-of-working.md)), Conventional Commits and a
[Definition of Done](docs/process/definition-of-done.md). Significant design changes start as an ADR.
[`CLAUDE.md`](CLAUDE.md) is the operating manual for AI assistants and a quick orientation for people
too. Please follow the [code of conduct](CODE_OF_CONDUCT.md).

## Security

Please don't open public issues for vulnerabilities. Report them privately as described in
[`SECURITY.md`](SECURITY.md).

## License

Licensed under the [Apache License 2.0](LICENSE); third-party notices are in [`NOTICE`](NOTICE). The
CircleTools name and logos are not covered by the license; see the [trademark policy](TRADEMARKS.md).
