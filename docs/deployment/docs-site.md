# Documentation site (private GitHub Pages)

This is the one-time owner setup that turns the DocFX site into a live, **authenticated** internal wiki on
GitHub Pages. It implements [ADR-0076](../adr/0076-docs-site-to-private-github-pages.md). The CI workflow
`docs-pages.yml` already **builds** the site on every push to `main`; this makes it **publish**.

> [!IMPORTANT]
> The repository is private and the docs contain internal security/architecture detail (ADR-0020,
> ADR-0050, ADR-0075). The **visibility** setting below is load-bearing: it must be **Private**. If your
> GitHub plan does not offer private Pages, **do not enable Pages** — leave the gate off and read the site
> from the build artifact instead (see the fallback). Never publish it publicly.

> [!NOTE]
> **Current state (2026-10-13): Pages is not available on this repo** — it is private on a public-only
> plan, so repo **Settings → Pages** shows *"Upgrade or make this repo public to enable Pages"*. Private
> Pages access control needs **Team/Enterprise**. The **fallback below is therefore the operative path**;
> do not make the repo public. (Separately, the org-settings notice *"There are no verified domains…"* is
> about **custom-domain** ownership, **not** view access — ignore it for this purpose.)

## One-time enablement

1. **Confirm the plan supports private Pages.** Access control for Pages requires GitHub **Team** or
   **Enterprise**. (On Free/Pro, Pages is public-only — stop here and use the fallback.)

2. **Enable Pages from Actions, private.** Repository **Settings → Pages**:
   - **Build and deployment → Source:** *GitHub Actions*.
   - **Visibility / Access control:** *Private* (only members with repository access can view the site).

3. **Flip the deploy gate.** Repository **Settings → Secrets and variables → Actions → Variables**, add a
   repository variable:
   - `DOCS_PAGES_ENABLED` = `true`

That's it. The next push to `main` (or a manual **Actions → Docs site → Run workflow**) builds the site and
deploys it. The URL is shown on the workflow's `github-pages` environment, typically
`https://<org>.github.io/<repo>/`; viewing it requires a signed-in GitHub account with access to the repo.

## How it works

- `docs-pages.yml` has two jobs. **build** runs `dotnet docfx docfx/docfx.json` (the same link-checked
  build as CI, failing on broken links/xrefs) and uploads `docfx/_site` as the Pages artifact — this runs
  regardless of settings, so the build stays green before enablement.
- **deploy** is gated on `DOCS_PAGES_ENABLED == 'true'` and runs `actions/deploy-pages`. Skipped until you
  complete the steps above, so the default state is *no published site*.

## Fallback (no private Pages on the plan)

Leave `DOCS_PAGES_ENABLED` unset. The **build** job still produces the full site on every run; download it
from the workflow run's artifacts (`github-pages`) and open `index.html` locally, or build it yourself with
`dotnet docfx docfx/docfx.json --serve`. If a live internal site is wanted without a plan upgrade, host it
on the VPS behind Caddy auth (the ADR-0076 option-4 fallback) rather than switching Pages to public.

## Troubleshooting

- **Asset/links broken under the `/<repo>/` subpath:** DocFX emits relative links, so this is usually fine;
  if not, set the site base URL in `docfx/docfx.json` (`globalMetadata._baseUrl`) or pass `--globalMetadata`
  on the build step.
- **Deploy job fails with a Pages error:** Pages is not enabled, or Source is not "GitHub Actions" — redo
  step 2.
