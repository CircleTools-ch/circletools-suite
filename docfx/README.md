# Documentation project (DocFX)

Builds the AppSuite documentation site from the Markdown in [`../docs`](../docs) and the C# API
reference (from XML doc comments in `../src`, once code exists). See
[ADR-0002](../docs/adr/0002-use-arc42-c4-docfx.md).

## Build & serve

```bash
dotnet tool restore                      # first time — installs DocFX (see ../.config/dotnet-tools.json)
dotnet docfx docfx/docfx.json            # build to docfx/_site
dotnet docfx docfx/docfx.json --serve    # build + serve at http://localhost:8080
```

## Layout

- `docfx.json` — build configuration (metadata → API, build → site).
- `filterConfig.yml` — which API members are included.
- `templates/appsuite/` — light branding over the `modern` theme.
- `_site/` — generated output (git-ignored).
- `api/` — generated API YAML (git-ignored; empty until `../src` has projects).

The site navigation (top bar) is driven by [`../docs/toc.yml`](../docs/toc.yml); each section has
its own nested `toc.yml`.
