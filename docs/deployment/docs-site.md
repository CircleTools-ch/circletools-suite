# Documentation site (public GitHub Pages)

The DocFX site is published to **public** GitHub Pages ([ADR-0093](../adr/0093-publish-the-repository.md),
which superseded the private-Pages plan of [ADR-0076](../adr/0076-docs-site-to-private-github-pages.md) when the
repository went public). The CI workflow `docs-pages.yml` **builds** the site on every push to `main`; the
one-time setup below makes it **publish**.

> [!NOTE]
> The docs are public on purpose: design and security reasoning is meant to be reviewed, and security must
> not depend on it being hidden (ADR-0093). Server-specific operator detail does not belong in them — it lives
> in the VM's `.env` and the password manager ([ADR-0050](../adr/0050-secrets-management.md), backlog OSS-4).

## One-time enablement (after the repository is public)

1. **Enable Pages from Actions.** Repository **Settings → Pages → Build and deployment → Source:**
   *GitHub Actions*.

2. **Flip the deploy gate.** Repository **Settings → Secrets and variables → Actions → Variables**, add a
   repository variable:
   - `DOCS_PAGES_ENABLED` = `true`

The next push to `main` (or **Actions → Docs site → Run workflow**) builds the site and deploys it. The URL
is shown on the workflow's `github-pages` environment, typically `https://circletools-ch.github.io/circletools-suite/`.
A custom domain (for example `docs.circletools.ch`) can be added later under Settings → Pages plus a `CNAME`
record at the DNS provider.

## How it works

- `docs-pages.yml` has two jobs. **build** runs `dotnet docfx docfx/docfx.json` (the same link-checked
  build as CI, failing on broken links/xrefs) and uploads `docfx/_site` as the Pages artifact.
- **deploy** is gated on `DOCS_PAGES_ENABLED == 'true'` and runs `actions/deploy-pages`. Without the variable
  it is skipped and nothing is published — the safe default, also for forks.

## Troubleshooting

- **Asset/links broken under the `/<repo>/` subpath:** DocFX emits relative links, so this is usually fine;
  if not, set the site base URL in `docfx/docfx.json` (`globalMetadata._baseUrl`) or pass `--globalMetadata`
  on the build step.
- **Deploy job fails with a Pages error:** Pages is not enabled, or Source is not "GitHub Actions" — redo
  step 1.
