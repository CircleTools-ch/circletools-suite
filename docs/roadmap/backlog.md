# Backlog

Idea stubs and work items, newest-first. Promote items into
[brainstorming](../brainstorming/index.md) → [use cases](../use-cases/index.md) as they mature.

## Store search — "Wichtel" and the App Store ranking (2026-11-17)

The owner found the app on Google Play for "Wichteln" but not for "Wichtel". Claude checked both stores for the
Swiss storefront. **Google Play:** the store's web search (`hl` = language, `gl=CH`), up to ~30 results.
**App Store:** Apple's iTunes Search API (`country=ch`), which approximates the App Store app's search but is
not identical to it. Ranks are positions in those lists; "n/a" means the term was not searched in that language.

| Term | Play EN | Play DE | Play FR | App Store |
|------|---------|---------|---------|-----------|
| SecretSanta | 5 | 4 | 7 | 8 |
| Secret Santa | 8 | 8 | 10 | 50 |
| secret santa app | 9 | n/a | n/a | 20 |
| gift exchange | 4 | n/a | n/a | 53 |
| name draw | not found | n/a | n/a | 46 |
| Wichtel | n/a | **29 of 29** | n/a | 41 |
| Wichteln | n/a | 6 | n/a | 56 |
| Wichtel App / wichteln app | n/a | 13 / 7 | n/a | 51 |
| Julklapp / Geschenktausch | n/a | not found / not found | n/a | 12 / 2 |
| père noël secret / pere noel secret | n/a | n/a | 6 / 4 | 16 / 16 |
| échange de cadeaux | n/a | n/a | 5 | 23 |
| tirage au sort noël | n/a | n/a | not found | 20 |
| gift / Geschenk / cadeau | not found | not found | not found | not found |

The App Store row uses the same results for every language; the API barely varies them.

**Reading.**
- **Google Play is fine for the main terms** (4–10). "Wichtel" alone is the gap: the listing has the word
  only in hyphenated compounds ("Wichtel-App", "Wichtel-Runde"), which apparently do not match the bare word.
- **The App Store is weak everywhere** (rank 41–56 for the core terms). This is partly the young app's few
  downloads and ratings, but the name spends 11 of its 30 characters on "CircleTools", which nobody searches
  for; the name is the strongest ranking field.
- **The one-word generic terms** (gift, Geschenk, cadeau) are not worth chasing.

**Release needed?**
- **Google Play: no.** The title and both descriptions are changed in Play Console → *Main store listing*. Each
  change goes through a listing review, usually within hours, and no new build is needed.
- **App Store: yes, for the parts that count.** The name, subtitle and keywords can only be changed with a new
  app version (a new build). The promotional text can be changed at any time, but it is not used for search.

**Items.**
- **ASO-1 · Play DE: the bare word "Wichtel" (P2, no release).** Use "Wichtel" as a word on its own in the
  short description and a few natural places in the full description (for example "wer dein Wichtel ist").
  Keep "Wichteln" in the title. Re-run the search a few days after the change.
  - ✅ 2026-11-17: written in `store/google-play/listing-de.md`
    (short description "Wichtel auslosen, private Links teilen: Wichteln offline und ohne Konto.", 72/80; two
    bare "Wichtel" in the full description). ✅ Same day: entered in Play Console (German – de-DE, default
    listing; the live text matched the previous repo version exactly) and **sent for review** — only these two
    changes, managed publishing off, so they go live on approval. ⏳ Re-run the "Wichtel" search a few days later.
    The App Store's German description stays as it is until the next iOS version (Apple does not search it).
- **ASO-2 · App Store name per language, with the next iOS version (P2).** Replace "CircleTools" in the name
  with the local search term, matching Play: "Secret Santa – Wichtel App" (DE), "Secret Santa: Gift Exchange"
  (EN), "Secret Santa: Père Noël secret" (FR). Then free up the keywords that this repeats, because Apple
  counts a word once across name, subtitle and keywords. This changes the identical-name rule of
  [ADR-0057](../adr/0057-app-store-naming-and-localization.md), so amend the ADR first. Owner decision.
  - ✅ 2026-11-17 (owner's go-ahead): ADR-0057 amended; `naming.md` updated. Those three names turned out to
    be taken by other apps, so the names are **`Secret Santa – Wichtel ziehen`** (DE),
    **`Secret Santa – Draw Names`** (EN, all four English locales) and **`Secret Santa – Tirage au sort`**
    (FR); no exact match in the App Store search API, fallbacks in the listing files. Subtitles and keywords
    rebalanced so no word repeats across name, subtitle and keywords (DE subtitle `Wichteln: auslosen und
    teilen`, EN `Gift exchange & wishlists`, FR unchanged). All in `store/app-store/listing-{de,en,fr}.md`.
  - ✅ 2026-11-19, reviewed again with the owner: **`Secret Santa – Wichteln`** (DE, subtitle `Wichtel
    auslosen und teilen`), **`Secret Santa – Gift Swap`** (EN, subtitle `Gift exchange & wishlists`; "Draw
    Names" dropped because the market leader's brand is "Drawnames") and **`Secret Santa – Père Noël`** (FR,
    subtitle `Tirage au sort, sans compte`). All three had no exact match on the App Store. **Owner decision:
    the same name on both stores** per language, so the Play titles follow (DE already matches; EN and FR
    change on Play, no release needed). ADR-0057 amendment, listing files and `naming.md` updated.
    **Timing (owner):** the Play titles change when the iOS version goes live, so both stores switch together
    (superseded on 2026-11-19: changed at once, see below).
  - **Release for ASO-2 (prepared 2026-11-19, owner decision "release now").** Contents since 1.0.14: the
    open-source section on About & legal (license + source link, ADR-0099), the Google Play trademark line,
    the .NET 10.0.14 servicing updates. No new feature; the in-app rating prompt follows in an early-December
    version. Release notes DE/EN/FR in the listing files (Play "Versionshinweise" + App Store "What's New").
    Steps: merge → *Release (mobile)* on `main` → approve `store-release` twice (Android, iOS; the first real
    run of the environment's secrets) → Play: promote the internal build to production → App Store Connect:
    new version, the build, What's New, name/subtitle (*App Information*) and keywords per language → submit.
    Then the Play EN/FR titles.
  - ✅ 2026-11-19, **released as 1.0.28** (not 1.0.29 as first counted: CI's nbgv height on `main` 8a6e40e
    gives `1.0.28`). Both store builds came from *Release (mobile)* run 44476907533.
    - **App Store:** version 1.0.28 with build 1.0.28, What's New and keywords in DE, FR and all four English
      locales; names and subtitles under *App Information* as above. App Store Connect accepted all three
      names, so no fallback was needed. Manual release. **Submitted for review** the same day. Creating the
      version first needed the owner to accept an updated Apple agreement.
    - **Google Play:** 1.0.28 promoted from internal testing to production at 100%, with the DE/EN/FR
      release notes. **Owner decision the same day: change the titles now**, not when iOS goes live (this
      replaces the timing note above). EN name `Secret Santa – Gift Swap` and the new EN short description,
      FR name `Secret Santa – Père Noël`; the DE name already matched. All four changes sent for review
      together; managed publishing is off, so they go live on approval.
  - ✅ 2026-11-20: both stores approved 1.0.28; the owner released iOS (manual release). The new App Store
    names show only from the release on, not in TestFlight and not on the home screen, which keeps
    "Secret Santa" ([ADR-0057](../adr/0057-app-store-naming-and-localization.md)).
  - ⏳ Re-run the store searches about 2026-11-27 (a week after release), together with the ASO-1 "Wichtel"
    check.
- **ASO-3 · Developer name on the App Store (owner, later).** It shows the owner's legal name because the
  Apple account is an individual account. Showing "CircleTools" needs an organization account (a legal entity
  and a D-U-N-S number). Google Play already shows "CircleTools".

## .NET 11 — evaluate in January (2026-11-17)

An update replaced the local .NET 10 SDK with **SDK 11.0.100** (installed: 9.0.320 and 11.0.100). Since
`global.json` pins `10.0.402` with `rollForward: latestFeature`, no `dotnet` command works in the repo (build,
tests, DocFX). Owner asked whether to move to the 11 SDK or to `net11.0`. **Decision 2026-11-17: stay on .NET 10
(LTS) through the Secret-Santa season; evaluate .NET 11 in January with an ADR.**

- **NET-1 · Reinstall the .NET 10 SDK now.** ✅ 2026-11-17: SDK 10.0.404 installed side by side with 11
  (winget `Microsoft.DotNet.SDK.10`); `global.json` selects it, MAUI workloads intact, DocFX builds again.
- **NET-2 · ADR: target framework after .NET 10 (January 2027).** Options: move to `net11.0` (STS) in the
  quiet season, or stay on `net10.0` until `net12.0` (next LTS, expected November 2027). Weigh:
  - **Support window** — check Microsoft's current policy; if STS releases get 24 months, .NET 11 ends about
    when .NET 10 does (≈ November 2028), so it buys features and performance, not time.
  - **Ecosystem** — Npgsql/EF Core provider, Aspire, MAUI 11 workloads, bUnit, Playwright, nbgv all available
    for 11; the `NU1608` AndroidX constraint warnings already present.
  - **Effort and risk** — every `Microsoft.*` pin and lock file, the MAUI Android and iOS heads (re-test on
    both phones, new store builds), new analyzers under `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild`,
    CI images (`setup-dotnet` from `global.json`), the Docker SDK and runtime images in `deploy/`.
  - **Timing** — not between November and early January (the app's season, store reviews).
  - **Alternative considered and rejected for now:** only the SDK to 11 (`rollForward: latestMajor`) while
    targeting `net10.0` — possible, but new analyzers and MAUI workload/SDK coupling make it a hidden
    upgrade; reinstalling SDK 10 is cheaper.
- **NET-3 · If NET-2 says upgrade:** a branch with the full bump, a green CI including `build-maui` and both
  E2E suites, the device pass on Android and iOS (CLAUDE.md "verify UI work"), then store releases.

## Open source & self-hosting (2026-11-13)

Owner, while the Play production application is in review: make the **whole repository open source** and
publish **container images** so anyone can self-host the suite, while the hosted service stays the
comfortable default. The official Android/iOS apps connect to the hosted backend by default; advanced users
can point them at their own server. Applies to Secret-Santa and every future CircleTools app.

Decisions are recorded as ADRs. Only an **Accepted** ADR is implemented:
[0099](../adr/0099-apache-2-license.md) license (Accepted; superseded [0092](../adr/0092-open-source-license-and-contributions.md)) ·
[0093](../adr/0093-publish-the-repository.md) publishing the repo (Accepted 2026-11-17) ·
[0094](../adr/0094-trademark-and-brand-policy.md) brand (Accepted 2026-11-17) ·
[0095](../adr/0095-self-host-deployment-topology.md) self-host topology ·
[0096](../adr/0096-client-server-selection-and-discovery.md) server choice in the apps ·
[0097](../adr/0097-passkeys-and-links-on-self-hosted-servers.md) passkeys & links ·
[0098](../adr/0098-container-image-distribution.md) images.

**Where it stands (2026-11-13):** OSS-1 (license) is done in the working tree, **not committed yet**. ADRs
0093–0098 are **Proposed** and waiting for the owner's review. **Next:** accept ADR-0093, then OSS-2/OSS-3
(history audit) — both must be finished before the repository is made public, which cannot be undone.
**2026-11-14:** OSS-2 done (pattern scan + gitleaks + trufflehog: no secrets). OSS-3 reviewed: one deleted
screenshot and the commit identity force a history rewrite — chosen; the rewrite is done and verified in a
scratch mirror and force-pushed; old PR refs deliberately not purged (owner decision). **OSS-2 and OSS-3
done.** Next: accept ADR-0093, then OSS-4 (operational docs) and OSS-5 (CI/repo hardening).
**2026-11-14 (later):** OSS-4 done; OSS-5 repository files committed and green in CI (`84b82ec`); OSS-1
committed (`90058e6`). **2026-11-17:** ADR-0093 and ADR-0094 **Accepted**; OSS-6 done (policy +
brand checklist; OSS-6b, the brand in one place, stays open). Later the same day: **OSS-5 done — the
repository is public** (flip day, see OSS-5). **Next:** OSS-6b. ADRs 0095–0098 stay Proposed until IMG/SH/CS.
**2026-11-20:** ADR-0098 **Accepted** (after a review against the code); IMG-1…IMG-4 unblocked. ADRs
0095–0097 stay Proposed.

**Choices to confirm when reviewing the Proposed ADRs.** Claude picked these as defaults during drafting;
each is a real trade-off, not a technical necessity:

- ~~ADR-0094 — a written trademark policy only, no registered trademark (cheaper, weaker in a dispute).~~
  Confirmed with the acceptance on 2026-11-17.
- ADR-0095 — self-host registration defaults to `invite` (not `open`), and the first account becomes the
  instance admin; mail is optional; single-origin mode is the self-host default.
- ADR-0096 — a custom server can only be chosen while signed out; data is **not** moved between servers
  automatically (export/import only); new apps must keep working with older servers.
- ADR-0097 — self-hosted servers trust the official Android app for passkeys **by default** (operators can
  turn it off); the official iOS app gets no passkeys on self-hosted servers (platform limit, not a choice).
- ADR-0098 — `:latest` points at the last **release**, not the last build; `:edge` is the last build.

**Order:** OSS (decide, audit, go public) → IMG + SH (the self-hosted web app already works with its own
server, so this is useful before any app change) → CS (custom server in the mobile apps).

**Phase 1 — Open-source the repository (owner decisions first)**

- **OSS-1 · License (ADR-0099, Accepted 2026-11-13 — Apache-2.0).** Done in the working tree 2026-11-13.
  First implemented as AGPL-3.0 + CLA (ADR-0092), then switched to Apache-2.0 the same day because the CLA
  (gist, bot, legal review) was too heavy; nothing had been committed.
  - ✅ `LICENSE` is the verbatim Apache-2.0 text (from apache.org, so GitHub detects it); `NOTICE` holds the
    copyright line and the third-party components (all MIT / Apache-2.0 / BSD / PostgreSQL, plus the
    SecLists-derived password list, Open Sans and Apple's badge artwork). Keep it in step with
    `Directory.Packages.props`.
  - ✅ SPDX header (`SPDX-FileCopyrightText: 2026 CircleTools contributors` + `SPDX-License-Identifier:
    Apache-2.0`) on every hand-written source file in `src/`, `tests/`, `deploy/`. Enforced by IDE0073 for C#
    (`.editorconfig` `file_header_template`, so IDEs also insert it) and by `SpdxHeaderTests` for Razor, JS,
    CSS, XAML, HTML, shell, SQL, Dockerfiles and the Caddyfile. Migrations and `*.Designer.cs` exempt.
  - ✅ License section in `CONTRIBUTING.md` (contributions under Apache-2.0 §5, nothing to sign) and `README.md`.
  - ✅ About & legal has an "Open source" section (DE/EN/FR) with a link to the license; the source link is
    behind `OpenSource.IsSourceCodePublic` (`Ui.Shared/OpenSource.cs`), off until the repo is public (OSS-5).
  - ✅ CLA Assistant gone (2026-11-17): no app installed in `CircleTools-ch`, no repository webhook, and the
    owner confirmed neither the organization's nor the personal app settings list it.
- **OSS-2 · Secret scan of the full history (ADR-0093).** `gitleaks` + `trufflehog` over all refs; rotate
  anything found, then remove it from history.
  - ✅ **First pass 2026-11-14 (Claude, pattern scan, no tools installed):** every blob reachable from any of
    the 241 commits on all local and remote refs, checked for private keys, cloud/GitHub/Google/Slack/Stripe/
    SendGrid tokens, JWTs, ntfy tokens, connection-string passwords, key/secret assignments and risky file
    names (`.env`, `.pem`, `.jks`, `.keystore`, `.p12`, `.p8`, `google-services.json`, …). **Nothing real
    found.** Every `deploy/.env.example` version holds only `change-me-…` / `your-…` / `tk_replace_with_…`
    placeholders; `appsettings.Production.json` (all versions) holds no secrets; `appsettings.Development.json`
    has the marked dev-only keys (`dev-only-signing-key-…`, the `AAECAwQ…` byte-sequence key) — fine to
    publish, and production must never use them. The Android `apk-key-hash` values are public anyway
    (`assetlinks.json`). No keystores, APK/AAB/IPA, database dumps or logs were ever committed.
  - ✅ **gitleaks 8.30.1, 2026-11-14** (`gitleaks git . --log-opts="--all" --redact`, 209 commits = every
    non-merge commit on all refs): 20 hits, **all false positives** — 9× the Android `apk-key-hash` passkey
    origins (public fingerprints, also in `assetlinks.json`) in `appsettings.Production.json` and ADR-0069;
    10× the dev-only `AAECAwQ…` byte-sequence key in `appsettings.Development.json` and four test
    factories; 1× a fake token literal in an old `AccountPageTests`. Nothing to rotate. Before publishing,
    add a `.gitleaksignore` (or allow-list in `.gitleaks.toml`) for these fingerprints so a CI gitleaks job
    (OSS-5) starts green.
  - ✅ **trufflehog 3.99.9, 2026-11-14** (official Docker image, `git file:///repo --no-verification`,
    container run with `--network none` so no candidate left the machine; ~29 MB scanned, the same volume as
    gitleaks): 11 unverified hits, **all false positives** — 8× `Password=postgres` in the local-dev
    connection string of the design-time `*DbContextFactory` classes and an old `appsettings.Development.json`,
    3× a one-letter dummy password in `PersistedModelReviewTests` / `MainDbContextModelTests`. 0 verified.
    **OSS-2 is done:** two scanners plus a pattern scan, nothing to rotate or remove.
- **OSS-3 · Personal-data review of the history.** Commit author emails (noreply for future commits;
  decide on a mailmap/rewrite for past ones), real names in device-test and brainstorming notes,
  screenshots, IPs and host names. **Found 2026-11-13:** all 241 commits so far carry the owner's personal
  Gmail address as author email, and the current tree contains it in this backlog (3 places) and in
  `docs/brainstorming/2026-10-20-secret-santa-release-device-test.md`. Switch git to the GitHub `noreply`
  address before the next commit, remove it from those docs, and decide whether to rewrite the history.
  - **Full-history review 2026-11-14 (Claude).** Findings, most serious first:
    1. **Deleted screenshot with the owner's email.** `Iphone_Passkey_error.png` (repo root, added in
       `381b6c4`, deleted in `a6de34a`, both 2026-10-02) shows the Security page with the account address in
       full. Deleting the file did not remove it from history → needs `git filter-repo --path
       Iphone_Passkey_error.png --invert-paths` (or the squash fallback of ADR-0093).
    2. **Commit identity.** All 452 author/committer entries use the owner's personal address (the other 30
       are GitHub's own `noreply` for web merges). Rewrite with a mailmap during the same `filter-repo` run,
       or accept it as public. Either way, switch `user.email` to the GitHub `noreply` address first.
    3. **Personal addresses in the current tree.** Both of the owner's personal addresses (accounts A and B
       of the device tests) appear in this backlog (Two-account and Sync sections) and in
       `docs/brainstorming/2026-10-20-secret-santa-release-device-test.md`. Replace with "account A" /
       "account B"; history keeps them unless rewritten (`filter-repo --replace-text`).
    4. **Server IP.** The VPS's public IPv4 is in this backlog (Go-live section). Low risk (DNS for
       `api.circletools.ch` reveals it), but it belongs in OSS-4's private notes.
    5. **Low / owner's call:** the owner's first name in ADR `deciders`, brainstorming front matter and
       arc42 (normal for a public project); the project mailbox `circletools.ch@…` and the role addresses
       `info@`, `appreview@`, `dev@`, `hello@`, `no-reply@` on the brand domain (intended to be public); a
       private LAN address (`192.168.100.x`) in the device-testing docs and the MAUI `.csproj` (harmless);
       the third-party email strings inside `BreachedPasswords.txt` are entries of the public SecLists source,
       not our data.
    - **Checked and clean:** every store screenshot (App Store and Play, all sizes and languages) uses
      made-up people (Anna, Ben, Clara, …); no family members' names in the device-test notes ("Bei Oma"
      is an example event location); no other real person's address in any version of any file.
    - **Owner decision 2026-11-14: rewrite the history.** Done by Claude in a scratch mirror (the working
      repository and GitHub untouched): mirror clone of the local repo, remote branches refreshed from
      GitHub, then `git filter-repo --invert-paths --path Iphone_Passkey_error.png --replace-text …
      --mailmap …`. Replacements: the two personal addresses → "account A" / "account B", the server IPv4
      → `<server IP>`; author/committer → the owner's GitHub `noreply` address. Hashes above are the
      pre-rewrite ones.
      - ✅ Verified: 240 commits (the "Delete screenshot" commit became empty and was dropped), 450
        identities on `noreply`, 30 GitHub web-merge identities unchanged; screenshot and all three strings
        absent from every blob and commit message; all 33 branch names present (filter-repo folds
        `origin/*` into local branches — only `main` differed, local one commit ahead, and
        `io-android-errors`, whose tip was the dropped commit); new `main` tree differs from the old one
        only in the two scrubbed docs; gitleaks on the rewrite: the same 20 known false positives.
      - ✅ Working tree: the same replacements applied to this backlog and the device-test note.
      - ✅ Working repo moved onto the rewritten history 2026-11-14 (owner's order): all 15 local branches
        repointed, `main` and the `claude/ecstatic-brahmagupta-674f54` worktree reset with `--mixed`, so
        their uncommitted changes are kept unchanged. A bundle of every pre-rewrite ref was kept outside the
        repo as a fallback. The `origin/*` remote-tracking refs still show the old history until the
        force-push and the next fetch.
      - ✅ `user.email` set to the GitHub `noreply` address in this repository's local git config (covers
        its worktrees; the global setting is unchanged, so a fresh clone elsewhere needs it again).
      - ✅ **Force-pushed 2026-11-14** (owner's order): GitHub had only `main` left (all feature branches
        were already deleted there), so only `main` was pushed, with `--force-with-lease` on the old tip.
        GitHub `main` = the rewritten local `main`; the last 100 commits all carry the `noreply` address.
        Local-only branches and the stale `origin/*` leftovers were **not** pushed (no deleted branch
        resurrected).
      - ✅ **Owner decision 2026-11-14: no purge.** The owner accepts that the closed PRs keep showing the
        old author address and the screenshot once the repo is public (exposure = one personal email address
        and a Security-page screenshot, no secrets). No GitHub Support request; OSS-3 is done.
      - (Context for that decision:) GitHub still serves the old commits by hash (checked: the
        screenshot commit is reachable) through the 30 closed PRs' `refs/pull/*` refs (PR #27,
        `io-android-errors`, holds the screenshot; every PR's commit list shows the old author address).
        Only **GitHub Support** can drop them: open a "remove sensitive data" request for
        `CircleTools-ch/circletools-suite` naming the PR refs and the screenshot path; the repo stays
        private until they confirm. ✅ Locally, the 31 stale `origin/*` refs were pruned 2026-11-14; only
        `origin/main` is left and `main` is level with it.
      - (Superseded plan text:) force-push all
        branches to GitHub (`--force --mirror` from the rewrite, or per branch). Other clones (CI caches,
        any other machine) must be re-cloned. GitHub keeps unreferenced old commits reachable by hash for a
        while and in cached PR refs — ask GitHub Support to purge them before the repo goes public
        (GitHub's "removing sensitive data" procedure).
- **OSS-4 · Operational docs review.** Move server-specific operator detail (admin hostnames, user names,
  backup target paths) to the password manager / private notes; keep design and security reasoning public.
  - ✅ **Reviewed 2026-11-14 (Claude)** — `docs/`, `deploy/`, `.github/workflows/`, `store/`, `README.md`.
    Very little was server-specific; the runbooks were already written with `<placeholder>`s.
    - **Generalized:** the exact Swiss Backup endpoint/bucket/path (in `backups.md` and the GL-5 entry
      here) now points at `RESTIC_REPOSITORY` in the VM's `.env`, which already holds it.
    - **Kept public, on purpose:** the public hostnames (`api.`, `grafana.`, `ntfy.` — in DNS and CT logs
      anyway); provider choices (Infomaniak, Swiss Backup, Brevo, healthchecks.io); `/opt/circletools` and the
      container paths; the SSH user names `ubuntu` / `deploy` (key-only SSH, `PermitRootLogin no`,
      fail2ban, ufw 22/80/443 only, Postgres never published — knowing the names gives nothing); the ntfy
      user and topic names in `.env.example` (templates, deny-all server); the Apple Team ID and Play
      fingerprints (served in `.well-known` anyway); the review account `appreview@` (its password is only in
      the password manager); the owner's name as App Store contact (public on the store listing).
    - **Not a doc question:** everything removed now is still in the public history (no further rewrite,
      OSS-3 decision), so anything that needs to stay unknown must be **changed on the server**, not
      deleted from docs. Nothing found needs that. Optional hardening, owner's call: give Grafana a
      non-default admin user (`GF_SECURITY_ADMIN_USER`; today it is Grafana's default on a public login
      page, protected by the strong password and Grafana's brute-force lockout) and, if the real ntfy admin
      is still called `owner` like the template, rename it.
    - **OSS-4 done.**
- **OSS-5 · CI and repository hardening.** GitHub Environments with required reviewer for release/deploy
  secrets; outside-contributor workflow approval; actions pinned to SHAs; branch protection; private
  vulnerability reporting; `SECURITY.md`; issue/PR templates; CODEOWNERS. Then flip to public, set `OpenSource.IsSourceCodePublic` to `true` (shows the source link on
  About & legal), and supersede ADR-0076 (public docs site).
  - **State found 2026-11-14:** the org is on GitHub **Free** and the repo is private, so **branch
    protection, rulesets, environments (and their reviewers) and private vulnerability reporting are not
    available until the repo is public** (API: "Upgrade to GitHub Pro or make this repository public"). All 15
    secrets are repository-level (`DEPLOY_*`, `ANDROID_*`, `PLAY_SERVICE_ACCOUNT_JSON`, `IOS_*`,
    `APPSTORE_*`). Good already: no `pull_request_target`; every workflow sets `permissions:` (read by
    default); the default workflow token is read-only; `ci.yml` (the only PR-triggered workflow) uses no
    secrets; deploy is `workflow_dispatch` only and store releases need a `v*` tag push (write access).
  - ✅ **Done in the working tree 2026-11-14 (Claude):**
    - All 32 `uses:` in the four workflows pinned to commit SHAs with the exact version as a comment.
    - `secret-scan` job in `ci.yml`: gitleaks 8.30.1 over the full history, binary checked against the
      release's SHA-256 (no third-party action — `gitleaks-action` needs a paid license for organizations).
      `.gitleaks.toml` allow-lists exactly the three reviewed false positives (OSS-2); the history scans clean.
    - `SECURITY.md` (private reporting, `dev@circletools.ch` fallback, scope, supported versions), linked from
      `CONTRIBUTING.md`; `.github/CODEOWNERS`; issue forms (bug, idea, security redirect, blank issues off)
      and a PR template (DoD, DE/EN/FR, no real personal data, Apache-2.0); `.github/dependabot.yml` for
      GitHub Actions only (NuGet bumps stay manual because they must refresh the lock files in the same change;
      vulnerable NuGet packages are caught by Dependabot alerts and the NuGet audit in the build).
    - **Found on the way:** `main` CI and the docs site were red since 2026-11-14 — two Microsoft advisories
      published 2026-11-10/12 against Aspire 13.6.2 (GHSA-4g86-54fm-h9h8 critical, GHSA-f7vh-h5g3-7jrh high)
      fail the restore (NuGet audit, warnings as errors). Bumped Aspire to **13.6.5** (AppHost SDK +
      `Aspire.Hosting.PostgreSQL`), which needs the **10.0.14** servicing band, so all 25 `10.0.13` pins moved
      to `10.0.14`; lock files refreshed (only 10.0.13 → 10.0.14 changes). Dev-time only (the AppHost is not
      deployed), but it blocked CI. **Verified locally:** full solution builds with 0 errors (the 45 warnings
      are the known AndroidX `NU1608` ones), 1,050 tests pass, both E2E suites pass against the new Aspire
      stack when run one at a time (9/9 and 2/2; run together they fight over the stack, CI runs them apart),
      DocFX builds (one known ADR-0035 source-link warning).
  - ✅ **2026-11-17 (owner's go-ahead):** actions must be pinned to a full-length commit SHA
    (`sha_pinning_required`); Dependabot alerts and security updates on. A CI re-run passed under the pinning
    rule; `deploy.yml` and `release-mobile.yml` are first exercised at the next deploy / store release.
  - ✅ **Flip day — done 2026-11-17.** Repo public; Pages publishing
    (`https://circletools-ch.github.io/circletools-suite/`); environments `production` and `store-release`
    hold the 4 + 11 secrets and the 15 repository-level copies are deleted; PR #31 (squash) merged and one
    approved deploy reached the VM. A new `DEPLOY_SSH_KEY` was made **on the server** — one made in
    PowerShell 7 with `-N '""'` got the literal passphrase `""` and was refused; the old and failed keys are
    removed from `authorized_keys`, and the old Play service-account key is deleted (the `store-release` copy
    is first exercised at the next store release). Ruleset *Main-Protection* (from 2026-08-13: PR required,
    squash only, no force push, no deletion, no bypass) now requires `build-test`, `docs`, `build-maui` and
    `secret-scan`; fork workflows need approval for all outside contributors; private vulnerability
    reporting, secret scanning and push protection on. Checked: the source link on About & legal of both
    `circletools.ch` and `secretsanta.circletools.ch` (new tab, console clean); issue forms and `SECURITY.md`
    on `main`. Still open: GHCR packages public (IMG-4) once ADR-0098 is accepted. The original plan follows.
  - **Flip day (plan as prepared).** Environments, rulesets and private vulnerability reporting only exist once the repo is
    public (Free plan), so going public comes **first** and the rest follows the same session. The short
    window is safe: only the owner has write access, fork PRs never receive secrets, and no workflow uses
    `pull_request_target`.
    - **Before — ✅ prepared 2026-11-17 in the working tree, uncommitted until the day** (committing earlier
      would show a dead source link after the next web deploy and put the deploy behind an environment that
      cannot exist yet):
      - `deploy.yml`: rollout job in `environment: production`; the `preflight` job that skipped the rollout
        while `DEPLOY_HOST` was unset is removed (an environment secret is invisible to other jobs, so it
        would have skipped every deploy silently); the job now fails with a clear message if the secrets are
        missing. `release-mobile.yml`: `android` and `ios` in `environment: store-release` (every run, also a
        manual test build, then waits for approval).
      - `OpenSource.IsSourceCodePublic = true`; `ImprintOpenSourceTests` now asserts the link (URL, new tab,
        `noopener`). Checked in the running web app: "Source code on GitHub" / "Quellcode auf GitHub" /
        "Code source sur GitHub" next to the license link, console clean.
      - ADR-0076 marked *Superseded by ADR-0093* (ADR-0093 already decided this — no new ADR);
        `docs/deployment/docs-site.md` rewritten for public Pages (Source: GitHub Actions + the
        `DOCS_PAGES_ENABLED` variable); `docs-pages.yml` comments; deployment index.
      - Runbooks: `server-setup.md`, `web-api-db.md`, `deploy/README.md` say *production environment*
        instead of repository secrets. README status: live hosted service, apps in store release, open source.
      - Verified: NoMaui solution builds with 0 warnings, Secret-Santa UI tests 232/232, architecture tests
        34/34, DocFX builds (the known ADR-0035 warning only).
    1. **Owner:** Settings → General → Change visibility → Public.
       Then Settings → Pages → Source: *GitHub Actions*, and the repository variable `DOCS_PAGES_ENABLED = true`.
    2. **Owner:** environments `production` (deployment branch `main`) and `store-release` (`main` + tags
       `v*`), required reviewer `adrian-moll`; re-enter the secrets there (`DEPLOY_*` → `production`;
       `ANDROID_*`, `PLAY_SERVICE_ACCOUNT_JSON`, `IOS_*`, `APPSTORE_*` → `store-release`). GitHub never shows
       secret values, so this needs the password manager.
    3. **Claude:** merge the prepared changes; owner runs `deploy.yml` once (approving the environment) to
       prove the rollout still reaches the VM; then delete the repository-level copies of the secrets.
    4. **Claude, with the owner's go-ahead:** ruleset on `main` (PR required, required checks `build-test`,
       `secret-scan`, `docs`, no force push, no deletion, owner bypass); Actions → approval required for all
       external contributors; private vulnerability reporting, secret scanning and push protection on;
       GHCR packages public (IMG-4) only when ADR-0098 is accepted.
    5. **Check:** the repo page, `SECURITY.md` link, issue forms, the source link on About & legal after the
       next web deploy.
- **OSS-6 · Trademark policy (ADR-0094).** `TRADEMARKS.md`; brand assets in one documented place so a fork
  can rebrand in one change; link from `README.md`, `CONTRIBUTING.md`, self-hosting guide.
  - ✅ **Done 2026-11-17 (Claude), ADR-0094 Accepted.** `TRADEMARKS.md` in the repository root: what the marks
    are (the name, domain, brand mark and logos; app names only together with CircleTools or our logos — a
    generic "Secret Santa" is not ours), what is fine without asking (run it, self-host with the official
    images, say "based on / compatible with CircleTools"), what needs a rename (store apps, public services,
    anything that looks official), keep `LICENSE`/`NOTICE`, ask at `info@circletools.ch`.
    [Brand assets](../process/brand-assets.md) lists every logo, colour, UI string, config value, domain and
    store identity a publishing fork must change. Linked from `README.md`, `CONTRIBUTING.md` and the process
    index; the self-hosting guide links it when SH-9 writes that guide.
- **OSS-6b · Brand in one place (open).** ADR-0094 asks that a rebrand be a single change; today it is the
  checklist above, spread over dozens of files: two hex colours repeated in eight places (app bar, `theme-color`,
  manifests, MAUI icon/splash, mail settings), names in four `.resx` sets × 4 cultures, icons in five
  folders, and domains in `Caddyfile`, app settings, `MainActivity.cs`, entitlements and `.well-known`. Needs a
  feature spec: one brand definition (names, colours, origins, ids) feeding the WASM/MAUI builds, manifests,
  mail settings and the resx placeholders, plus one asset folder. Overlaps SH-2/SH-3 (origins from
  `CIRCLETOOLS_PUBLIC_URL`) — do it with or after them. Until then: add any new brand file or string to the
  checklist in the same PR (stated in `CONTRIBUTING.md`).

**Phase 2 — Public images (ADR-0098)**

**Review 2026-11-20 (Claude, against the code; owner asked for both amendments):** ADR-0095 and ADR-0098
revised. **ADR-0098 Accepted 2026-11-20** (owner), so IMG-1…IMG-4 can be built; ADR-0095 stays
**Proposed**. Changes: server releases on `server-v*` tags
(`v*` starts the store release); no `:<major>` / `:<major>.<minor>` tags; the two service workers would
delete each other's caches on one origin; without mail nobody can create an account (FR-007), so the
no-mail rules and a setup link for the first admin; the instance admin and account invitations need a
feature spec (new SH-10). Items below updated to match.

- **IMG-1 · Multi-arch builds.** `linux/amd64` + `linux/arm64` with buildx; SDK stage on `$BUILDPLATFORM`
  (the API and WASM output don't depend on the CPU, so only the runtime stage differs).
- **IMG-2 · Tags and releases.** `deploy.yml` pushes `:<version>`, `:<commit-sha>`, `:edge` (no longer
  `:latest`); `release-server.yml` on a `server-v<version>` tag re-tags those images as `:stable` /
  `:latest` and writes the GitHub Release with notes (migrations, config changes, min app / min server
  versions). Production already pins `IMAGE_TAG=<version>`; the compose fallback changes from `latest` to
  `edge` in the same change.
- **IMG-3 · Supply chain.** cosign keyless signing, SLSA provenance, SBOM; base images pinned by digest
  with a Dependabot `docker` entry; non-root (`USER $APP_UID` in the API image, Caddy as non-root on 8080
  in plain-HTTP mode).
- **IMG-4 · Make the GHCR packages public** (after OSS-5).
- **Built 2026-11-20 on `feat/public-images` (Claude, owner's go-ahead), not merged:**
  - IMG-1 ✅ in code: both Dockerfiles build the SDK stage on `$BUILDPLATFORM` (`UseAppHost=false` for the
    API), `deploy.yml` builds `linux/amd64,linux/arm64` with QEMU + Buildx and a GHA layer cache.
  - IMG-2 ✅ in code: `:edge` replaces `:latest` in `deploy.yml`; compose fallback and `.env.example` →
    `edge`; new `release-server.yml` (on a published GitHub Release `server-v<version>`, not a pre-release)
    checks `:<version>` and `:<commit-sha>` are the same image and re-tags it `:stable` + `:latest`.
  - IMG-3 ✅ in code: base images pinned `tag@digest` (SDK/aspnet 10.0, Caddy 2.11.9-alpine), Dependabot
    `docker` entry for `/deploy`, provenance `mode=max` + SBOM, cosign keyless signing by digest
    (`id-token: write` on the build job only), OCI labels (`source` links the package to the repo), API as
    `USER $APP_UID`. **Web image still root, on purpose:** Caddy holds the TLS certificates in root-owned
    volumes on our server; non-root moves to SH-2 (plain-HTTP mode) with a one-time `chown` of
    `caddy_data` / `caddy_config`.
  - New page [Container images](../deployment/container-images.md) (tags, server release, `cosign verify`).
  - **Not yet proven:** signing, provenance and the GHA cache only run in CI — the first Deploy run after
    the merge is the test. Dependabot picking up `*.Dockerfile` names shows on its first weekly run.
  - IMG-4 open: owner makes both packages public (*Package settings → Change visibility*), ideally after
    the first signed build, then the first `server-v…` release.

**Phase 3 — Self-host edition of the server (ADR-0095)**

- **SH-1 · Single-origin mode.** Hub at `/`, apps under `/<app>/`, API under `/api/` (the API's routes
  already start with `/api/v…`, so only Caddy routes them); configurable base path in every WASM app
  (routing, static assets); same-origin SSO hand-off. **Service workers:** per-app cache names, each worker
  deletes only its own caches and answers navigations only under its own base path (today the hub's worker
  would wipe Secret-Santa's cache and serve the hub's page at `/secretsanta/`); review the shared storage
  keys `circletools.themeMode` and `circletools.sso.*`.
- **SH-2 · Runtime configuration in the web image.** Generate each app's `appsettings.json` and the CSP at
  container start from `CIRCLETOOLS_PUBLIC_URL` / `CIRCLETOOLS_MODE`; plain HTTP by default,
  `CIRCLETOOLS_TLS=auto` for stand-alone Caddy TLS.
- **SH-3 · API options from one public URL.** CORS, passkey RP-ID/origins, SSO return-uri allow-list, mail
  link origins derived from `CIRCLETOOLS_PUBLIC_URL`; forwarded headers only from the proxy network; clear
  log when the request origin does not match.
- **SH-4 · `deploy/selfhost/compose.yaml`.** `postgres` + `api` + `web`; opt-in
  `CIRCLETOOLS_MIGRATE_ON_START`; observability as an optional profile; no ntfy/cAdvisor/node-exporter.
- **SH-5 · `setup` script.** Generates JWT, share-token and email-HMAC keys into `.env`, and prints the
  one-time setup link for the first account (instance admin; 24 h, single use; `setup --admin-link` for a
  new one).
- **SH-6 · Optional mail.** Any SMTP; with none, invitation mails are switched off and the UI says so;
  link invitations keep working. Self-service sign-up needs mail (verification, FR-007): the API refuses
  `open` without SMTP; password reset becomes an admin-issued link.
- **SH-7 · Registration policy.** `CIRCLETOOLS_REGISTRATION=open|invite|closed`. Self-host default `closed`
  (first account via the setup link) until SH-10 ships, then `invite`.
- **SH-10 · Instance admin and account invitations (new feature, spec first).** No admin role and no
  account invitation exist today. Glossary entries (**instance admin**, **account invitation**,
  **operator**) → FEAT + TP from the templates → build: admin role, issue/revoke account invitation and
  password-reset links, the admin sees no one's groups.
- **SH-8 · Operator legal pages.** Operator-supplied imprint/privacy (mounted Markdown or URL); fallback
  text naming the self-hosted operator; CircleTools texts only on the hosted service.
- **SH-9 · Docs and CI.** Self-hosting guide + configuration reference + upgrade/backup notes in
  `docs/deployment/` (reverse-proxy examples: Traefik, Nginx Proxy Manager, Caddy, Synology); CI boots the
  self-host stack on amd64 and arm64 and runs an E2E smoke test.

**Phase 4 — Custom server in the apps (ADR-0096, ADR-0097)**

- **CS-1 · Discovery endpoint.** `GET /.well-known/circletools` on the API (name, versions, public/API/app
  URLs, auth capabilities, registration, features, operator).
- **CS-2 · Feature spec + test plan** for Settings → Advanced → Server (FEAT/TP from the templates), plus an
  NFR for self-hostability.
- **CS-3 · Runtime server setting.** `ApiBaseUrl` / `RevealBaseUrl` become "build-time default, runtime
  override"; change only while signed out; HTTPS only; server name/host shown on sign-in and account
  screens; version handshake with clear DE/EN/FR messages.
- **CS-4 · Passkeys on self-hosted servers.** Self-host defaults for `assetlinks.json` and the Android
  `apk-key-hash` origins (current + previous Play signing keys); passkey availability in the discovery
  document; passkey UI hidden where unsupported (iOS app on a self-hosted server). Release checklist gets
  "update self-host passkey defaults if the signing key rotates".
- **CS-5 · Links follow the server.** Reveal/join/event links use the chosen server's public URL; they open
  in the browser (no App Link for foreign hosts).
- **CS-6 · Compatibility both ways.** Document the minimum supported server version per app release;
  capability checks instead of assumptions; store privacy declarations mention user-chosen third-party
  servers.

## List order — people alphabetically, no sort buttons (2026-11-01)

Owner, using the app: no list could be ordered, and people appeared in the order they were added. Decision:
**sensible default orders per list, no generic sort control.** The lists are short (a group is ~4–15 people)
and a four-way sort button (A–Z / Z–A / added ↑ / added ↓) would clutter every phone screen. "Date added"
can't be offered honestly either, because a member has no creation time, only a roster position.

- **LO-1 · People alphabetically (done, next release).** Every list of people is sorted by name in the
  reader's language: case-insensitive, with "Ä" next to "A" in German and accents ignored for the order in
  French. This covers Members, the exclusion pickers, exclusion rules (by giver, then recipient), event
  wishlists, Share links (and so the hand-out dialog), the join-link roster and the "from a circle" people
  picker. The single comparer is `DisplayOrder` (`SecretSanta.Ui/DisplayOrder.cs`), covered by
  `DisplayOrderTests`. **Display only:** the stored roster order, sync and the draw are untouched, so no ADR
  or migration. Equal names keep their stored order.
- **LO-2 · Groups list: next event first (open).** Today: pinned first, then newest first (UX review U7, so
  a just-created group is on top). Better: pinned, then by the **next upcoming event date**, then newest
  without an upcoming event. Needs the next event date on `GroupSummaryDto` (local and online groups) and a
  rule for where a new group with no event goes, so U7 isn't undone. Taking-part groups should follow the
  same rule.
- **LO-3 · Wishlist items keep the owner's order (by design).** Items carry the owner's priority, so they
  are deliberately **not** sorted. They are edited as one text field, one item per line, so moving a line
  already reorders them. Revisit only if wishlists become structured items (then add up/down or drag).
- **LO-4 · Optional Groups sort toggle (parked).** If the Groups list ever grows long enough to need it,
  add a per-device "Upcoming / Name" toggle there only. Don't build it until someone asks.

## Legal-page text review — Privacy, About & legal, Delete your data (2026-10-31)

Owner read the three legal pages and asked for an accuracy and wording pass in DE/EN/FR. Decisions: fix the
code where a page promised more than it did, keep the personal voice but drop odd phrases, **no postal
address** in the imprint, add every missing disclosure, Swiss German **"Handy"**.

- **LR-1 · Sent mail kept forever (bug, fixed).** Outbox rows hold the recipient address in plain text and
  were only marked processed, never deleted — contradicting "invitation addresses are never stored in plain
  text" and "a deleted account is gone for good" (the farewell mail carries the address). `OutboxPurge`
  (`Main.Service.Repository/OutboxRetention.cs`) now deletes messages 7 days after sending, or after
  enqueueing if never sent; it runs hourly in `MainRetentionService` (renamed from
  `PendingAccountRetentionService`). [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)
  updated; covered by `OutboxPurgeTests`. The 7-day window is quoted on both pages — change them together.
- **LR-2 · False or stale claims corrected.** "Reminder emails" (reminders are device-local notifications,
  ADR-0060); the email list (nine mail types, not three); "every app works offline" (circles need an
  account); backup window "a few months" vs "about six months" (restic keeps 6 monthly); "for each group you
  choose" (one choice for all online groups); delete steps now name the real menu labels; "joining stores the
  group"; local storage holds groups too; sliding 30-day sign-in described correctly; invitation lifetimes
  (14 / 30 days) stated.
- **LR-3 · Disclosures added.** Technical logs (30 days, on-box Loki, IP only in-memory for rate limiting);
  signed-in device labels; app stores; backups on Swiss Backup; other people's data incl. children; reveal
  links seen by the messenger used to send them; what the JSON export contains. New keys
  `Legal_Privacy_Others*` and `Legal_Privacy_Logs*`.
- **LR-4 · Wording.** "Verwürfelung" / "brouillage" / "scramble" replaced; anglicisms and stiff phrasing
  removed in all three languages; DE role term `Betreiber:innen` (ADR-0032). Second pass: "Ein Konto brauchst
  du nur…", no comma before "und" in DE headings, law names DSG / FADP / LPD (no longer "revDSG"/"nLPD"),
  and no "collection of apps" — only Secret Santa exists so far.
- **LR-5 · "Remove" kept a member's data (bug, fixed).** Removing a member always archived them with their
  wishlists and gift note, although the deletion page said it deletes them. `Group.RemoveMember` now deletes
  wishlists, gift note and exclusions, deletes the member outright when no draw names them, and archives
  only the name otherwise ([ADR-0052](../adr/0052-member-archival-and-permanent-draw-history.md) amended).
  Remove-confirmation text updated in DE/EN/FR.
- **LR-6 · Expired Secret-Santa invitations never deleted (bug, fixed).** Lapsed `Invited` role assignments
  were ignored on read but kept forever. `GroupService.PruneExpiredInvitationsAsync` now runs in the daily
  `GroupRetentionService` sweep.
- **LR-7 · "Circle" as a product name in every language (done 2026-10-31, ships after 1.0.1).**
  [ADR-0089](../adr/0089-circle-as-a-product-name-in-every-language.md): the UI says Circle / Circles in DE, FR
  and EN — capitalised, masculine in DE/FR (*der/le Circle, die/les Circles*) — instead of "Kreis" / "cercle".
  All `.resx` strings (Main, Secret Santa, account, legal pages) and the circle invitation mail; glossary
  updated. "Freundeskreis" and each circle's own label are unchanged.
- **LR-8 · "I'll point out anything important in the app" dropped (owner decision 2026-10-31).** The app has no
  in-app notice for policy changes, so the privacy page now only says the date at the top shows the current version.

## Production release 1.0 — Secret-Santa (2026-10-20)

Adrian's pre-release list (update everything, full review, both store listings, Play tester gate) plus the
items missing from it, sequenced in the **[production-release plan](../deployment/production-release-plan.md)**.

- **PR-1 · Start the Play closed test now (P1, owner).** 12 testers × 14 days + up to 7 days review puts
  production access at mid-to-late November at best. ~~Recruit ~15 family/friends; no paid tester services.~~
  **Changed 2026-10-29 (owner decision):** use a **paid closed-testing service** instead — the owner does not
  want to involve family and friends. Google does not forbid paid testers but judges real engagement and
  asks for feedback in the production-access form, so pick a service with real people on real devices,
  ≥15 testers (dropout buffer), written feedback, and a refund/redo guarantee. Shortlist (own claims, not
  verified): Testers Community Starter €14 / 15 testers / "production access or refund" (preferred on
  price); TesterBee $18.74 / 15; PrimeTestLab $19.99 / 12 or $27.99 / 25.
  **Status 2026-10-29 (checked in Play Console):** still **not started** — track *Closed testing - Alpha*
  exists with no release, 0 testers opted in; only internal testing runs. Same day: release **0.1.131**
  (code 65667, built from `2724e11`) drafted on Alpha with DE/EN/FR release notes and **178
  countries/regions**. **Service bought:** Testers Community **Pro** (25 testers, €24, account
  `circletools.ch@gmail.com`). Its Google Group `testers-community@googlegroups.com` is set as the
  Alpha track's testers; store-listing graphics (icon + 3 feature graphics) labelled *AI-assisted* per
  owner decision. **Update 2026-10-29 (Play Console check): Google approved** — Publishing overview
  *Last published on 29 October 2026*, nothing pending; Alpha is **Active**, 0.1.131 *Available to selected
  testers* (released 29 Oct 07:11), 178 countries/regions, testers = the Testers Community Google Group,
  feedback address `info@circletools.ch`. **Submitted on Testers Community 2026-10-29** (Pro credit used,
  join link `https://play.google.com/apps/testing/ch.circletools.secretsanta`): dashboard shows *Private
  Testing Pro — Day 0 / 16, reports pending*; testers are assigned within ~6 h. The 14-day Google clock
  starts once ≥ 12 have opted in and installed — check *Closed testing → Testers*. **Next:** keep the
  track active, ship fix builds to Alpha, apply for production after day 14. Everything else in the console
  is ready (all 10 app-content declarations done, data-safety form matches `store/google-play/README.md`,
  DE/EN/FR listing with 7 phone screenshots each, store settings).
- **PR-2 · Apple App Store record, DSA trader status, privacy labels, review demo account (P1, owner).**
  ✅ **Done 2026-10-29** (checked in App Store Connect): DSA non-trader declared; privacy label published;
  App Information, pricing/availability, age rating 4+ and the 1.0 version page filled for DE, FR and
  EN (US/UK/CA/AU), incl. review contact, notes, demo sign-in and manual release; updated Developer
  Program License Agreement accepted. Demo account `appreview@circletools.ch` holds the fictitious
  *Familie Meier* group (Anna–Felix, *Weihnachten 2026*, drawn, online). **Left:** pick the build and
  upload iPhone 6.9" + iPad 13" screenshots after the on-device pass, then *Add for Review*. The App Store
  version is **1.0**, so the submitted build must come from the `version.json` → `1.0` bump (made
  2026-10-29 together with the ADR-0057 app-bar fix: `AppName` stays "Secret Santa" in every locale, new
  translated `ActivityName` for calendar title / share subject). Commit + push those, rerun
  `release-mobile.yml`, and use that `1.0.x` build for the weekend iPhone/iPad pass and screenshots
  (and as an update on the Play Alpha track).
- **PR-3 · Dependency & toolchain updates (P1).** 44 package updates found 2026-10-20; risky ones are MAUI
  10.0.20 → 10.0.110, MudBlazor 9.8 → 9.11, Plugin.LocalNotification 12 → 14. Table in the plan.
- **PR-4 · Full project review (P1)** — incl. open R1/R5 from the
  [2026-10-20 device test](../brainstorming/2026-10-20-secret-santa-release-device-test.md).
- **PR-5 · Email capacity for December (P1).** ✅ **Alerting implemented (2026-10-28,
  [ADR-0088](../adr/0088-mail-capacity-alerting.md)).** Brevo free tier is 300 emails/day (ADR-0037). Estimate:
  a Secret-Santa group sends at most one member-link mail per participant plus a few verification/invitation
  mails, so ~30 large groups drawing on the same day would be needed to hit the cap; the realistic risk is
  abuse of the endpoints that accept an email address (rate-limited, NFR-005). Passkey/password sign-in, group
  creation, draw, sync and link sharing send no mail. Two outbox-derived gauges (mails sent in a rolling 24 h,
  undeliverable mails) feed Grafana alert rules — warning above 200, critical above 270, critical on a new
  delivery failure — emailed to `ALERT_EMAIL_TO`, plus an **Email** row on the Business dashboard.
  **Still owner action:** set `ALERT_EMAIL_TO` in the VM's `.env` **before the next deploy** (compose now
  requires it), then check *Alerting → Alert rules* in Grafana shows the three rules. ✅ The ntfy push
  channel independent of Brevo is built (2026-10-31, [ADR-0091](../adr/0091-ntfy-push-alerts-and-watchdog.md)).
- **PR-6 · Store listings & screenshots for both stores, DE/EN/FR (P2, after the UI is stable).**
- **PR-7 · Support page/FAQ, alerting to the owner, rollback runbook, FR native read, legal review (P2).**
  **Alerting channel built (2026-10-31, [ADR-0091](../adr/0091-ntfy-push-alerts-and-watchdog.md)):** a
  self-hosted ntfy on the VPS (`ntfy.circletools.ch`, deny-all) gets every Grafana alert as a push next to the
  email, and the backup-failure alert through `ALERT_WEBHOOK_URL`; an always-firing watchdog rule pings
  healthchecks.io, which alerts when the VPS or its alerting stops. Verified locally (provisioning loads, a
  firing rule arrives in ntfy formatted by the `grafana` template, the watchdog heartbeat flows, anonymous and
  cross-topic access is denied). **Still owner action before the next deploy** (compose now requires the
  values): the `ntfy` A record, the healthchecks.io check, and the `NTFY_*` / `HEALTHCHECKS_PING_URL` values in
  the VM's `.env` — [server setup §14](../deployment/server-setup.md#14-push-alerts-ntfy-and-the-watchdog).
  Still open in PR-7: rules for API down and error rate (they can use the same `owner` contact point).

Found during the PR-3 dependency update (2026-10-20) — browser pass at 412×915 on MudBlazor 9.11, console
clean; none caused by the update unless stated:

- **PR-8 · Date-picker calendar clipped on phones (P2).** In the *Add event* dialog and on *Details*, the
  open calendar is cut off after the third week row (`.mud-picker-calendar-transition` 216 px tall, content
  432 px) — the 24th can't be tapped. Identical on MudBlazor 9.8.0, so pre-existing. Typing the date works.
  **Re-checked 2026-10-30:** web only. The MAUI app (Galaxy S24, local Debug build) uses the native OS date
  picker since ADR-0056 (`OperatingSystem.IsBrowser()` switch in `EventEditor` / `CarryForwardEventDialog`):
  full month, the 24th selectable in *Add event* and on *Details*. The web app at 412×915 still clips
  (transition 216 px, content 432 px — the calendar shows to the 17th) — so R2 fixes the web picker.
  **Fixed in the working tree 2026-10-30 (R2):** cause was our own phone touch-target rule (FEAT-008 W1,
  `circletools.css`), which raised every `.mud-icon-button` — the calendar's day cells included — to 48 px;
  MudBlazor positions the month grid absolutely in a box sized for 36 px days, so six 48 px rows overflowed
  and were clipped. The day cells are now exempt, like the adornment and switch buttons. Browser-verified at
  412×915: full month in *Add event* and on *Details*, 24.12.2026 selected and saved, console clean.
- **PR-9 · MudBlazor's own validation text is not localized (P3).** A date typed in the wrong format shows
  "Not a valid date time" in English in every UI language. Also: the accepted format follows the
  formatting region (ADR-0056), so `24.12.2026` is rejected on an en-GB browser — consider accepting
  `.`/`-` separators too.
  **Fixed in the working tree 2026-11-06 (R4):** the message comes through `SecretSantaMudLocalizer`
  (`Mud_Converter_InvalidDateTime`, also `…InvalidNumber`, DE/EN/FR). `TypedDateConverter` accepts `.`, `-` and
  `/`, one-digit days and months, two- or four-digit years, and ISO `yyyy-MM-dd`; the region still decides the
  order of day and month (ADR-0056), so `24.12.2026` stays refused on an en-US browser. **Web only:** the device
  heads use the native date input and never showed this. Browser-verified on en-GB, de-CH, fr-CH and en-US. A
  relaxed date stays as typed in the field until the page is reopened (`1-2-2027`, then `01.02.2027`).
- **PR-14 · Save deletes the date when the date field holds an invalid text (P2, found 2026-11-06, web only).**
  With a saved date, typing `hello` shows the error, but *Save* then reports success and the date is gone after
  a reload. MudBlazor sets `Date` to null on a failed conversion, whichever converter is used, so this predates
  R4. *Add event* has the same gap: the event is created without the date. Neither should go ahead while the
  field shows an error.
  **Fixed in the working tree 2026-11-06:** both forms use `TypedDatePicker`. A failed conversion no longer reaches
  the form, which keeps the last good date; *Save* and *Add event* refuse while the field shows the error; emptying
  the field still removes the date. Checking `ConversionError` alone was not enough: any later re-render of the
  form wiped the text and the error, and the next *Save* deleted the date, so the wrapper also renders the picker
  only when its inputs change. Browser-verified (EN flows, DE/FR error text). `TypedDatePickerTests` cover the
  wrapper; the refusal in the two forms has no automated test, because bUnit does not run the browser branch.
  Also verified in Chrome on a Galaxy S24 (2026-11-07, de-CH): both forms refuse `hello` with "Kein gültiges Datum", and
  the saved date survives a reload.
  If the error moves the button, a click made straight from the date field can miss it: the error shows, nothing is
  saved, and an earlier "Saved." stays until the next click.
- **PR-15 · The web apps' loading percentage goes over 100% (P3, reported by users 2026-11-06, web only).**
  Measured 101% on every fresh load and reload of the Secret-Santa web app (Debug build, en-GB, de-CH, fr-CH).
  Blazor's `blazor.webassembly.js` writes `loaded / total` to `--blazor-load-percentage-text` without a limit, and
  CSS cannot limit a text. The ring is unaffected. Both web apps use the same `app.css`.
  **Fixed in the working tree 2026-11-07:** both web apps show the ring alone, without the number. The count itself
  still reaches 101%; why it passes the total was not traced. Browser-verified on both apps (de-CH, Debug build).
- **PR-10 · Update banner stacks on a language switch (P3).** With "A new version is available" showing,
  switching language adds a second banner in the new language instead of replacing the first.
- **PR-11 · Three SecretSanta.Ui tests failed once under full-suite parallel load (P3).** Passed in four
  other runs (alone and in the full suite); names not captured. Likely the timing-based autosave tests
  (`PastThePause`); watch CI and capture names on the next failure.
  **2026-10-30:** a full local `dotnet test` (with the Aspire stack also running) failed one E2E test,
  `SecretSanta.E2E.Test.AccountGroupTests.A_group_organized_without_an_account_is_kept_in_the_account_and_handed_back`
  (52 s); it passed when re-run alone. Same pattern: load-dependent timing.
  **2026-11-13:** two more, each failed once in a full non-MAUI `dotnet test` run and passed on every re-run
  (alone and as a whole project): `Main.Ui.Test.AccountPageTests.ChangePassword_OnSuccess_ClearsTheFieldsWithoutFlaggingThemRequired`
  and `SecretSanta.Ui.Test.MembersPageParticipantTests.AGroupOnThisDevice_SaysHowToLetPeopleIn_AndOffersNoInvite`.
- **PR-16 · Timing tests failed on routes with nothing wrong; the budget has a real limit at saturation
  (test fixed 2026-11-07; P3 owner decision open).** `InvitationTimingTests` failed about 1 run in 5, and
  `EnumerationTimingTests` less often, always with arms about 2 ms apart and every arm at the budget.
  **Cause: the threshold, not the budget.** The handlers fit the budget at about 7x (invite: median
  15–17 ms against 120 ms), but on Windows `Task.Delay` lands a median of 3.9 ms late and up to 14 ms late
  at p95, so 2 ms between the fastest samples of two arms measures the timer. An A/A control proved it:
  identical requests sent as separate arms failed the old check as often as the real arms.

  | Old 2 ms check, invite form | Real arms failed | Identical requests failed |
  |---|---|---|
  | Machine at ~40 % CPU | 3/20 | 2/20 |
  | Beside a Main test-project run | 4/15 | 2/15 |

  **Fix (test code only; production behaviour and the budget are unchanged).** The tests now read each
  handler's cost off the wait the filter requests (`ConstantTimeClock`) and assert it directly: the
  cheapest run per arm must fit in half the budget. The floor check is unchanged and the caller-side
  spread check stays as a coarse 10 ms limit, for work outside the filter (`ConstantTimeEnvelope`).
  NFR-005 is not weakened: an arm costing 70 ms more now fails even though it stays under budget and the
  old check could not see it; +130 ms in the handler, +15 ms outside the filter and a bypassed filter all
  fail too (3/3 runs each on the invite form; the bypass 1 run, on all four constant-time routes).
  **Reliability after the fix, on Windows:** 20/20 passes for each of the two classes alone and beside a
  looping Main test-project run (19 loops, none failed). Not measured on the Ubuntu CI runners.
  **Open (owner, P3): the budget does stop covering the invite form on a saturated host.** With all 8
  logical CPUs busy, its handler has a median of 180–230 ms, about 100 % of requests overrun, and the arms
  separate again (member ~180–195 ms, others ~215–235 ms): a real leak under that load, not noise. Both
  classes therefore fail there (0/5 each), correctly for the invite form, and that is why the fix does not
  promise a pass at saturation. Even with room to spare 1.6 % of requests overran (5.6 % beside a test run),
  without favouring an arm. All of this was measured against a Postgres in Docker on a dev machine, so
  before changing anything: decide whether to raise `DefaultBudget`, ideally after measuring handler cost
  in production. These routes answer "we have queued a mail", so extra latency costs little.

- **PR-12 · Release cadence during the Play closed test (P1, decided 2026-10-29).** Testers Community advises
  shipping several updates during the test window so Google sees active development. **Decision (owner):**
  a new build on the Alpha track **every 2–3 days**, each one **small** (bug fixes and polish, no features,
  no structural change), with DE/EN/FR release notes. Before each release: re-check every planned item
  against the code (some backlog entries are already partly fixed) and run the browser pass from
  `CLAUDE.md`. Planned slices (testers' feedback may replace or add items):

  | # | Content | Status |
  |---|---------|--------|
  | R1 | `706dc24`: version 1.0 bump + "Secret Santa" in the app bar in every language (ADR-0057) | released as **1.0.1** (versionCode 16777217): internal → promoted to Alpha 2026-10-30, sent for Google review; notes in `store/google-play/listing-*.md` |
  | R2 | PR-8 (date-picker calendar clipped on phones — web only) · BTP-5 (second event suggests the same name — suggest the next year) | fixed in working tree 2026-10-30, browser-verified — **ships with R3** (owner, 2026-10-30: R2 alone was too small). Not built yet on 2026-11-05: goes out in the next build after 1.0.1, whose number comes from the git height, not "1.0.2" |
  | R3 | BTP-3 (forms show "Required" after a successful submit) · BTP-9 (COPIED label, member-row tap, stale back-link name after a rename) | fixed in working tree 2026-10-30, browser-verified (BTP-9 focus ring: not reproduced, open) — next build after 1.0.1, release notes drafted |
  | R4 | Localization: PR-9 (MudBlazor validation text; accept `.`/`-` date separators) · BTP-8 leftovers (FR "Réglages" → "Paramètres", FR colon spacing) · BTP-7 (`<html lang>` follows the UI language) | fixed in working tree 2026-11-06, browser-verified (DE/EN/FR); on the Galaxy S24 (2026-11-07) `<html lang>` follows the switch in the app's WebView (`fr-CH`, `en`, `de-CH`), but what TalkBack actually says has not been listened to — next build after 1.0.10, release notes drafted. Of BTP-8 the FR items only: its other copy points stay open |
  | R5 | BTP-6 (hide the wishlist count for a participant) · BTP-4 (Security page: stale device list, duplicate section title) · PR-13 (show-password toggle) | fixed in working tree 2026-11-07, browser-verified (DE/EN/FR) except as noted on BTP-6; device pass on the Galaxy S24 as noted on PR-13 — **ships with R4** in one build (owner, 2026-11-07), release notes drafted |
  | Spare | PR-10 (update banner stacks on a language switch, web) · SYNC-4 (old language behind the import prompt) · tester findings | — |

  Out of scope for this window: new features (CR-1…CR-4, MNF-6/7, IDEA-003). PR-11 (flaky tests) is watched
  in CI meanwhile. See the [production-release plan](../deployment/production-release-plan.md), track 1.

- **PR-13 · Password fields have no "show password" toggle (P3, reported by the owner 2026-10-31).** On a
  phone keyboard a typo in a masked password is invisible, so sign-in fails or a new account gets a
  password the user didn't mean. Add an eye icon at the end of every password field that switches it
  between masked and clear text (MudBlazor `Adornment.End` + `Visibility`/`VisibilityOff` icon, toggling
  `InputType.Password` ↔ `InputType.Text`), with a localized DE/EN/FR accessible label ("Show password" /
  "Hide password"). Masked stays the default, and each page resets to masked when it is opened again. Fields
  (all in `CircleTools.Ui.Account`): `Login.razor`, `Register.razor`, `ResetPassword.razor`, `Security.razor`
  (current / new / repeat), `AccountData.razor` (delete-account confirmation). Prefer one shared component
  over seven copies of the toggle. **Planned for R5** (it already touches `Security.razor` for BTP-4); it
  fits the PR-12 rule — small polish on existing fields, no new flow. Reaches iOS with the next App Store
  update after the build now in review. Browser-verify every field in DE/EN/FR plus one Android device pass.
  **Fixed in the working tree 2026-11-07 (R5):** `PasswordField` on all seven fields of the five pages. While the
  text shows, the input tells the keyboard not to correct or capitalize it; a successful password change masks the
  fields again. Browser-verified in the Secret-Santa web app (EN/DE/FR, phone width): the typed text survives the
  toggle, Enter still submits, the button is 48 px. `PasswordFieldTests` cover the component. The Main web app uses
  the same component but was not opened. **Galaxy S24, Android 14 (2026-11-07):** on the sign-in page of a Debug app
  build and in Chrome, the toggle reveals and hides the text and keeps it, and the input carries the no-correction
  attributes. Tapping the eye closes the keyboard, so the user taps the field again to go on typing. Not covered on the
  device: the Security, sign-up and delete-account pages in the app (no sign-in, to leave the phone's own group out of
  an account), how a real keyboard or password manager treats the revealed field, and a Release build.
- **PR-17 · Testers Community reports (P1, received 2026-11-10, day 12 / 71 %).** Feedback, ASO and
  production-access reports, filed with a per-point triage in
  `store/google-play/closed-test/testers-community/README.md` (outside the docs site, so not a link).
  No crashes or bugs found. **Proposed, owner to confirm:** rework the EN/DE/FR Play listing text for ASO
  (EN title `Secret Santa – Gift Exchange`, keyword in the short description, longer description with
  use cases; no build needed); captioned screenshots after production access (PR-6); show-password
  toggle already done (PR-13); **decline** Google Sign-in for 1.0 (needs an ADR, privacy/data-safety
  changes and an equivalent Apple option) and an exit confirmation on Back (against Android guidance,
  nothing is lost). The service's suggested production-access answers claim changes we did not make
  (Google Sign-in, exit dialog, family-and-friends testers): **answer from the honest draft** in that
  README on day 14. **Listing text rewritten 2026-11-10** (EN/DE/FR in `store/google-play/listing-*.md`);
  **Entered in Play Console 2026-11-10** (default listing en-US, de-DE, fr-FR; AI-asset label kept): 7 changes
  **sent for review 2026-11-10** (*Changes in review*). Same day, **Alpha 1.0.10** (approved but left
  unpublished since ~30 Oct) was **published** (owner go-ahead). Managed publishing is on, so once Google
  approves the listing it waits under *Changes ready to publish* until someone clicks *Publish*.
  **Approved the same day** and **published 2026-11-10** (all 7 changes: EN title, and short + full descriptions in EN/DE/FR). R4 + R5 built as
  **1.0.14** (`7a649d3`, Release (mobile) run 42798371946): on Play *Internal testing* and in TestFlight
  (*Ready to Submit*); web + backend deployed from the same commit. After the owner's device check on the
  Pixel 9 Pro, **promoted Internal → Closed testing – Alpha 2026-11-10** (100 %, DE/EN/FR release notes
  from `store/google-play/listing-*.md`; only the usual deobfuscation and native-symbols warnings) and
  **sent for review** the same day. Managed publishing: once approved, it still needs *Publish*.
  **App Store:** the same descriptions are prepared in `store/app-store/listing-*.md` for the **next iOS
  version after 1.0.10** (Apple only changes description, keywords, subtitle and name with a new version;
  1.0.10 is *Ready for Distribution*, no newer version exists yet). Name, subtitle, keywords and promotional
  text stay. When creating that version, also answer the new age-rating social-media questions in *App
  Information* (App Store Connect notice, 2026-11-10).
  **iOS 1.0.14 prepared 2026-11-10** (owner go-ahead): version created in App Store Connect with build
  1.0.14; description, What's New and promotional text entered for German, French and all four English
  locales (U.S., U.K., Canada, Australia) and checked after a reload. The new version did not carry over 1.0.10's promotional text, so it
  was re-entered from the listing files. The age-rating questionnaire was already complete (social media, messaging, user-generated
  content and the rest all *No*; 4+). After the owner's TestFlight check on an iPhone, **submitted for
  App Review 2026-11-10**. Release is set to *Manually release this version*, so once approved it waits
  for someone to click *Release*.
  **Production access applied 2026-11-13 15:53** (day 14; Dashboard ticked all three closed-test criteria;
  Alpha runs 1.0.14 since 11 Nov). Answered from the honest draft, not the service's suggestions:
  recruitment *Easy*, expected installs **0–10K**, no claim about Android vitals (not checked). The form
  had no "what did you do differently" question. Play shows *We have your application for production
  access* — review usually ≤ 7 days, result by email. **Next:** keep Alpha active; on approval, production
  release of 1.0.14 with a staged rollout (production-release plan, track 1).
- **PR-18 · Android code shrinking with R8 (P3, flagged by Play Console 2026-11-20).** The production track
  for 1.0.28 shows "DEX code optimisation is below our threshold" with obfuscation at 1%, and the release
  warns that there is no deobfuscation file. The Android build does not use R8 shrinking or obfuscation.
  Turning it on would make the app smaller and a little faster. It is not a blocker. **Risk:** R8 can strip
  code that MAUI, Blazor Hybrid and reflection-based serializers load at runtime, so it needs keep rules
  and a full device test pass (create → members → exclusions → draw → share → reveal, sign-in and sync,
  DE/EN/FR). **Steps:** check what MAUI on .NET 10 supports (`AndroidLinkTool=r8`, trimming), decide in an
  ADR, try it on a branch, compare the AAB size, test a Play internal build on the Pixel, then upload the
  mapping file with each release (the "no deobfuscation file" warning). Not before the Secret-Santa season
  is over; January together with NET-2.

Found during the Play screenshot pass on the Pixel 9 Pro (Play build `0.1.128`, 2026-10-28, DE/EN/FR, light
mode; [store submission guide](../deployment/store-submission-guide.md)) and reported by the owner the same day
(U-1…U-5, incl. iPad screenshots). **All fixed in the working tree 2026-10-28** and browser-verified against the
local stack (fresh profile, second origin as a second device, DE/EN/FR, console clean) unless marked
*device-verify*: those touch only the native heads' safe-area insets and need the next Play/TestFlight build.
Re-shoot the store screenshots that show SS-3/SS-4 (04 in all languages, FR 01/02) after that build. ✅ Done on
`0.1.129` (all phone and tablet shots, DE/EN/FR, uploaded to Play as drafts).

- **U-1 · Signing in on a device that never synced showed no online groups until a reload (P1).** ✅ Root cause:
  ADR-0086 removed the sign-in reconciler, which ended every sign-in with `AutoSync.RequestSync()`; since then
  nothing triggered the first cycle (only `POST /login` and `GET /circles` went out). `AutoSyncService` now
  watches the auth state and requests a cycle when an account signs in. Verified: cleared storage → sign in →
  the group is listed after ~2 s, no reload (before: still empty after 20 s).
- **U-2 · A long group name squeezed the "Online" chip (P2).** ✅ The name takes the free width and wraps
  (`ct-wrap-anywhere`); chip and menu keep theirs. Verified with "Moll-Schindelholz-Mauron-Gehri" at 412 px.
- **U-3 / SS-1 / SS-2 · Footer under the "Create group" FAB / behind the bottom navigation (P1).** ✅ One cause:
  `.ct-main-fill` sized the page `100dvh − app bar` but the native heads also push the content down by the
  status-bar inset (`app.css`), so a short page was taller than the screen by the inset and the footer slid
  under the FAB / the bottom bar. The inset now comes off the height (`circletools.css`). *Device-verify.*
- **U-4 · No direct way to edit one's own wishlist as an organizer (P1).** ✅ *My assignment*
  (`/groups/{id}/me`) now shows the wishlist and gift-note fields to the organizer too, saved into the
  document (`SetMemberWishlistCommand` / `SetMemberGiftNoteCommand`); the row says "…and your own wishlist".
  Verified: saved there → appears on the event's wishlists page and as the gift note in Members.
- **U-5 · A group joined (not organized) was missing from the drawer (P2).** ✅ The drawer's *Recently opened*
  now lists groups taken part in, linking to their page. Verified with a join on a second origin.
- **U-6 · iPad landscape: "All groups" missing from the persistent drawer (P1).** ✅ The clipped drawer started
  at MudBlazor's app-bar height, but the native app bar is taller by the safe-area inset, so the first entry sat
  behind it. `app.css` starts the drawer below the grown app bar. *Device-verify (iPad).*
- **SS-3 · Hyphen instead of a dash (P2).** ✅ `RevealSpoilerNote`, `RedrawWarning`, `ExclusionSelfNotAllowed` in
  all languages (— EN, – DE/FR); French copy now uses the spaced en dash throughout (18 em dashes replaced).
- **SS-4 · French: missing space before the colon (P2).** ✅ The colon was typed into the markup after the label in
  ~15 places; `LabelText.WithColon` (Ui.Shared, tested) adds the no-break space for French. Six French legal
  strings fixed by hand. Verified: « Pour : Ben », « Sa liste de souhaits : ».
- **SS-5 · Deleted group requested on every sync (P2).** ✅ Root cause: the sent removal was dropped from the
  queue by record equality against a freshly read copy whose permission list is another instance, so it never
  matched and the delete was re-sent every cycle (404 from the second time). Matched by group and kind now;
  `PendingRemovalTests` fails on the old code. Verified: delete → queue empty → reload, console clean.
- **SS-6 · Formal register in German (P3).** ✅ Footer link and all 26 German legal strings moved to *du*
  (ADR-0032 has no exemption for legal text); legal pages dated 2026-10-28. **Owed:** read them in the light legal
  review.
- **SS-7 · No active bottom-nav tab inside a group (P3).** ✅ `NavItem.AlsoActiveFor` (`groups/`) and
  `BottomNav.MoreActiveFor` (Settings, account). *Device-verify* (the bar is native-only).
- **SS-8 · Reveal page: back arrow at the far end, content under the gesture bar (P3).** ✅ Arrow at the leading
  edge (verified); pages without the bottom bar clear the home indicator (`ct-safe-bottom`, *device-verify*).
- **SS-9 · QR dialog edge to edge (P3).** ✅ `MaxWidth.False` → 32 px margin per side (verified at 412 px).
- **SS-10 · Draw page on a later visit said only "Assignments are hidden…" (P3).** ✅ Shows "Draw complete ·
  N assignments" from the event's current draw (verified after a reload).
- **Noted, not new:** `<html lang>` stays `en` in a French UI — BTP-7.

Found in the scenario pass on the Play build `0.1.129` (2026-10-28): Pixel 9 Pro app signed in as the review
account, production web in fresh browsers (organizer `adri3`, invitee `moll`, signed-out joiner and reveal-link
holder), DE/EN/FR. Covered and working: online group by an account, email invitation accepted on the web, join
link in the app (with the account) and in a signed-out browser (device key), reveal link only in browser and app,
wishlist edits propagating app ↔ web ↔ participant views at once, organizer view (*Connected*, read-only member
entries, *Wishlists 3/5*), sign-out / sign-in on the phone, group deletion (join link then says it no longer works).
Browser console stayed clean throughout. The store screenshots were re-shot on that build (phone + tablet, DE/EN/FR).

- **ST-1 · Switching accounts on one device hides a group both accounts are in (P1).** Sign out as A, sign in as
  B: a group B takes part in is missing from the list (survives a restart) when A also had it on this device. Root
  cause: sign-out keeps A's records on purpose (`AutoSyncService.OnAccountChangedAsync` — "the views hide the
  account's groups"), and `OnlineGroupSync.DiscoverAsync` adds a discovered group only when *no* record with that
  id exists, so A's hidden record blocks B's. Repro: review account joined "QA Oct 28" in the app → sign out →
  sign in as `moll` (also in it) → only "Test" listed; the web lists both, `GET /me/groups` returns both. ✅
  Pages and sync still find a group by its id, so one record per group is *in play*; the others now wait in
  `GroupRecordsDocument.Parked` instead of blocking or being dropped. Discovery gives the signed-in account its own
  record even when someone else's is there, and each sync cycle (now also requested on sign-out) arranges which one
  is in play: of the signed-in account's and the device's own (link or device key), the one that organizes, else the
  account's. So another account's record — unsent work included — waits for that account, and a group the device
  joined without an account gives way to the signed-in account's own role and comes back on sign-out
  (`AccountSwitchDiscoveryTests`; checked in the browser: device joined as Lou, Ivo signs in → "Als Moe", signs out
  → "Als Lou", signs in → "Als Moe").
- **ST-2 · A deleted group's reveal link still shows the pairing from the kept copy (P1).** After the organizer
  deletes the group, a device that opened the short link before shows the old pairing and wishlists with "Showing
  your saved copy — you may be offline", while online. `GET /reveal/{token}` answers 404, but `Reveal.razor` loads
  the kept copy before it looks at `_notFound`. Contradicts the delete dialog ("its links stop working"). ✅ On
  not-found the page drops the kept copy and shows *link is gone*; offline it still falls back to the copy
  (`RevealPage_ShortLink_WhoseGroupWasDeleted_SaysGone_AndDropsTheKeptCopy`).
- **ST-3 · Join page: an invited member is labelled "Already joined" (P3).** Invitees are reserved
  (`ConnectedMembers.IsTaken` counts `Invited`), which is right, but the chip says *Bereits beigetreten* / *Already
  joined* before the invitee accepted. ✅ `JoinableMember` gained an optional `Invited` (old clients ignore it);
  the server sets it for members held only by an open invitation, and the chip then reads *Eingeladen* /
  *Invited* / *Invité·e* (`MemberInvitedBadge`).
- **ST-4 · Participant page labels the location "Event:" (P3).** `groups/{id}/me` shows the date on its own line
  and then *Anlass: Zürich* / *Event: Zürich* / *Événement : Zürich*; the reveal page shows *Anlass: 24.12.2026,
  Zürich*. ✅ The line is labelled *Ort* / *Location* / *Lieu* (`LocationLabel`).
- **ST-5 · Join page: member radios flow inline (P3).** On a phone a name lands beside another member's
  *Already joined* chip (e.g. "Moll · Bereits beigetreten · ○ Pixel"). ✅ One member per line.
- **ST-6 · French join page: "Vous êtes invité à" next to "ajouté·e" (P3).** ✅ *Vous êtes invité·e à*, like the
  rest of the French copy.
- **ST-7 · Returning web visitors stay on the previous build — not a defect.** Two offline caches meant the new
  worker had installed and was waiting, which is ADR-0048's design: the app offers *Reload* rather than switching
  under the user. The prompt itself was not looked at during the pass; check it on the next deploy.

## Browser test pass before the Pixel test — Secret-Santa + Main (2026-10-17)

Every feature driven in a browser at phone width (412×915) against the local Aspire stack on the working
tree (build 0.1.118), with separate browser contexts as separate devices: local flow, carry-forward, backup,
DE/FR, sign-up/verify/sign-in + import prompt, two-device sync, shared group with a participant (join, own
assignment, own wishlist, leave), shared-by-link co-organizing, sessions/change password, circles. Console
clean apart from the expected 400/401 of deliberate wrong inputs.

- **BTP-1 · Wishlist edits lost behind "Saved ✓" (bug, P1).** ✅ **Fixed 2026-10-17.** Typing in a
  member's wishlist and moving to the next field within the 700 ms pause stored nothing: MudBlazor's
  `DebounceInterval` held the value back, so the blur saved the old text, showed "Saved ✓", and the pending
  debounce was cancelled. `EventWishlists.razor` now takes every keystroke into the model and runs its own
  pause timer; blur saves at once; saves are serialized (each rewrites the whole group in local storage);
  leaving the page saves what is unsaved. Same for the participant's own entry. Component tests:
  `EventWishlistsPageTests`. Browser-verified (field-to-field typing, reload).
- **BTP-2 · Wishlists page went stale after a background sync (bug, P2).** ✅ **Fixed 2026-10-17.** The one
  section page without the SBL-2 `ISyncSignals` refresh: it kept showing — and on the next edit saved over —
  a wishlist another device had changed. Now reloads on `SyncCompleted`, keeping text still being typed.
  Browser-verified across two devices without a reload.
- **BTP-3 · Forms show "Required" on every field after a successful submit (P2).** Change password, circle
  invite, add person: the form resets with validation still armed.
  **Fixed in the working tree 2026-10-30 (R3):** a touched MudBlazor field re-validates when its value is
  emptied, on the next render — so the page now renders the cleared values and then resets the fields'
  validation (`FieldValidation.ResetAsync`, `CircleTools.Ui.Shared`). Component tests for all three forms
  (each fails without the fix); browser-verified. Known edge: submitting with Enter leaves focus in the
  emptied field, and leaving it later shows "Required" — MudBlazor's normal blur validation; the button
  path (what phones use) is clean.
- **BTP-4 · Security page (P3).** The device list is stale after a password change signed the others out;
  two sections are titled "Signed-in devices" (the second is "sign out everywhere").
  **Fixed in the working tree 2026-11-07 (R5):** a successful change reloads the list (2 devices became 1 in the
  browser), and the second section is now "Sign out everywhere" / "Überall abmelden" / "Se déconnecter partout".
- **BTP-5 · Second event's suggested name repeats the first (P3).** "Christmas 2026" again; one tap creates a
  duplicate-named event. Suggest the next year, or block a duplicate name.
  **Fixed in the working tree 2026-10-30 (R2):** the suggestion (`EventNameSuggestion`) starts at the current
  year and skips every year the group already has an event of that localized name for (trimmed, ignoring
  case) — dialog and wide-screen placeholder alike. Unit-tested; browser-verified in EN/DE/FR (2026 → 2027
  → 2028), console clean. A duplicate name typed by hand is still allowed.
- **BTP-6 · Participant's group card shows "Wishlists 2/5" (P3).** Correct for the device (own + recipient),
  but reads as "3 missing". Hide the count for a participant.
  **Not reproduced on 2026-11-07, guarded in the working tree (R5):** since ADR-0086 a join-link participant lands on
  `/groups/{id}/me`, which shows no count, and has no document, so `/groups/{id}` answers "Group not found". The count
  remains on the group page for a read-only device that holds the document (`CanEditDocument` false), and `GroupEditor`
  now hides it there. That path was **not exercised**: no browser flow reached it and the page has no component test.
  The organizer still sees the count (browser-verified).
- **BTP-7 · `<html lang>` is always `en` (P2, a11y).** Hard-coded in `index.html`, never updated on a
  language switch: screen readers use the English voice; Chrome on Android may offer to translate DE/FR.
  **Fixed in the working tree 2026-11-06 (R4):** `CultureBoundary` sets it on start and on every switch, on all
  heads. `index.html` still says `en` until the runtime has started, while only the loading indicator shows.
- **BTP-8 · Copy (P3).** DE footer "Ihre Daten löschen" (rest uses *du*); FR colon spacing inconsistent
  ("Pour: Anna" vs "Distribués : 5"), hint says "Réglages" on the "Paramètres" page; EN 'There is already a
  “anna”'; reveal page "ready - no peeking" (hyphen); leave dialog promises "the organizer will see that you
  left" but nothing shows it; the reveal hint "tell the organizer what you would like" (see BTP-11).
  **FR items fixed in the working tree 2026-11-06 (R4):** the app's settings are "Paramètres" everywhere. "réglages"
  remains for the phone's own settings (`NotificationsBlocked`, `OpenSystemSettings`) and in the legal texts, whose
  wording the owner reviews. Every French resource colon now follows a no-break space, as `LabelText` does, guarded
  by a test for `AppStrings` of Secret Santa; the budget line of a calendar entry no longer hard-codes its colon.
- **BTP-9 · Small UI (P3).** Tapping a member row does nothing (only ⋮); a "COPIED!" label never resets; the
  back link keeps the old event name after a rename until navigation; tab title stays "Confirming your
  address" after confirming; focus ring left on the Exclusions card after creating a group.
  **Fixed in the working tree 2026-10-30 (R3), browser-verified (EN, plus DE/FR for the new string):**
  tapping a member's name opens them for editing (`EditNamed` accessible label, keyboard Enter/Space too);
  "Copied!" returns to "Copy" after 2 s (`CopiedFeedback`, share page, sharing & sync, hand-out dialog);
  the event back link re-reads the name on every local change (`ISyncSignals.LocalChanged`); the tab title
  follows the card title on verify-email, confirm-email-change and both connect pages. Component tests for
  the back link. **Still open:** the Exclusions focus ring did not reproduce in the browser (click or Enter
  leaves focus on the body) — likely touch-only in the Android WebView; check in the next device pass.
- **BTP-10 · Dev only: the update banner can't apply in development.** `wwwroot/service-worker.js` (dev) has no
  `SKIP_WAITING` handler, so "Reload" never activates a waiting worker and the banner returns. Production's
  worker handles it.
- **BTP-11 · Edit your own wishlist from the reveal link (P2).** ✅ **Built 2026-10-17** —
  [ADR-0084](../adr/0084-own-entry-from-the-reveal-link.md), [FR-050](../requirements/functional/FR-050-edit-own-entry-from-reveal-link.md),
  [FEAT-026](../features/FEAT-026-own-entry-from-the-reveal-link.md). Adrian chose: live-link groups only
  (Shared by link, My account · shared), wishlist + gift note, the person owns the entry (organizer's copy
  read-only; "Edit their wishlist yourself" hands it back). Browser-verified organizer → holder → giver.
  **Deploy server first** (migration `AddRevealSliceMember`), as SYNC-1.
- **BTP-12 · Undo for every text autosave (P2).** ✅ **Built 2026-10-17.** Adrian asked whether autosave
  leaves a way back from an edit made by mistake. Decision: keep autosave (the mobile norm for notes and
  lists; a missing save loses more than a wrong edit), and add undo — leaving a field whose text changed
  shows "Change saved · Undo" for 6 s, restoring the text from before that visit (`UndoLastEdit`). Applies
  to the organizer's wishlists, the participant's own entry, the reveal-page editor and My wishlists (there
  every saved change, incl. remove). While typing, the platform's own undo applies.
- **BTP-13 · Pages opened from a link are a page for one person (P2).** ✅ **Built 2026-10-17.** Adrian: the
  left menu looked odd on a reveal link, and changing the language meant Settings and losing the link. Now
  `/r` and `/join` use a link mode of the shell: no menu button, drawer, bottom bar, app switcher or account
  menu; a compact language switch (`CultureMenu`, "🌐 DE ▾") that changes the language in place; and, only on
  a device that already holds groups (organizer, joined participant), a way back ("All groups"; an arrow
  alone below 600 px, where FR wrapped at 360 px). The first-visit language still follows the phone; no
  `lang=` in links. The reveal page also shows the holder's own wishlist before the reveal (no spoiler).

## Two-account test follow-ups — Secret-Santa (2026-10-16)

From the [2026-10-16 two-account test](../brainstorming/2026-10-16-secret-santa-two-account-device-test.md)
(Pixel 9 Pro, build 0.1.118, + browser, production). Adrian selected every item; the session doc holds
the decision for each ID. Work packages in implementation order; each lists the documents to write
**before** the code. Server changes deploy before the apps (as SYNC-1).

- **WP-1 · Secrecy, server authorization, participants** — release blocker. Q1 (participant can't
  become organizer; "Leave group"), Q2/I2 (join read is redacted to the device's own pairing), Q3
  (organizer-only linking publish, 403 not 500), Q4 (client publishes only organized groups), Q5/I1
  (participant edits own wishlist and gift note; account join links the member), Q6 (taken members
  disabled on join), Q7 (account join preselected when signed in), Q20 (share counter counts only who
  needs a link), Q30 ("Verbunden" badge), Q19 (verify password fields). *First:* ADR for participant
  access and the redacted join view. **Done 2026-10-16** (ADR-0080, FEAT-024) — not yet committed or
  deployed; server first. Pixel check pending.
- **WP-2 · Storage model & onboarding.** Q31/I4 (a signed-in organizer's shared group stays owned by the
  account; *My account* → "Invite people"), Q15 (sharing wording), Q24/I6 (storage choice moves into
  *Create group*), Q35 (import prompt lists groups, no discard). *First:* ADR for Q31; ADR-0071 and
  ADR-0044 amendments. Also: an **owner** release of a joined person (today only an admin token can release),
  needed once signed-in organizers' shared groups are owned. **Implemented 2026-10-16** (ADR-0081, ADR-0071
  and ADR-0044 amendments, owner release): unit/component/store tests green, browser-verified (first create
  explains storage, import prompt, owned share, second device discovery, stop sharing, invite). Same-cycle
  rediscovery re-checked. FEAT-025/TP-025. Open: E2E suite not run/adapted.
- **WP-3 · Reminders.** T4 (lead time per kind, time of day, "Before the event", per-event override,
  after-draw offer), Q8 (blocked-permission state + open settings; web says "in the app"), Q9 (DST-safe
  delivery time). *First:* amend FR-046 and FEAT-016. **Implemented 2026-10-16** (FR-046 / FEAT-016 amended):
  unit-tested, Android head builds; the phone behaviour needs the device check.
- **WP-4 · Android chrome & theme.** Q11 (keyboard/scroll), Q12 (menu closes before sheet), Q17
  (investigate tap fall-through), Q33 (contrast, central theme), Q14 + T3 (sync status icon), T2
  (drawer "All groups" + "Recently opened"), Q18 (accessible tile names + check). Verified on the Pixel.
  **Implemented 2026-10-16** (ADR-0044 and ADR-0040 amendments): sync icon in the app bar (offline shown at
  once, detail + retry), drawer caption and initials, WCAG-checked theme (`ThemeContrastTests`), named hub
  tiles (`AccessibleNameTests`), menus close before their sheet, `ViewportBehavior` (bottom nav hides while
  typing, focused field below the app bar, new page at the top, Back restores). Browser-verified. Also: the
  *Your data* explainer no longer says "there is no account" when signed in. Open: Q11/Q17 on the Pixel.
- **WP-5 · Groups, events, members.** Q10 (duplicate name in the sheet), Q13 (exclusion warnings, draw
  vs. exclusions, failed re-draw state), Q21 (share rows), Q22/I5 (event card date/countdown/progress,
  *Details* summary, date in add dialog), Q23 (signed-in welcome card), T1 + Q36 (circles only inside
  *Create group*), Q28 (budget format), Q29 (wishlist autosave), Q34 (group menu incl. "Use as template").
  **Implemented 2026-10-16** (FR-001, FR-002, FR-003, FR-034, FR-036 amended): `DrawFeasibility` +
  exclusion warnings, "draw no longer matches" flag, draw page keeps the draw after a failed re-draw; event
  cards with date/countdown/wishlist progress, ordering, Details summary, date in *Add event*; welcome card
  signed in; circles only inside *Create group* (2+ people), web link instead of the "account app"; one
  budget format (`BudgetDisplayTests`); wishlist autosave; duplicate name kept in the sheet with a
  suggestion, sheet stays open between names; uniform share rows; group menu on list and group page with
  `CopyGroupCommand` ("Use as template") and a shared `GroupRemoval`. Tests green, browser-verified (DE).
  Open: phone check.
- **WP-6 · Account pages.** Q25 (native always stays signed in; FR-008), Q26/I7 (session list, sign out
  one device, e-mail in menu and profile; ADR-0079 amendment), Q27 (SSO option only when it can work),
  Q37 (account button shows the sign-in state: initial avatar / outlined icon or "Sign in" text).
  **Implemented 2026-10-16** (ADR-0079 amendment; FR-008, FR-031, FR-049 amended): device label on each
  session (`DeviceLabel`, migration `AddSessionDevice`), `POST /account/sessions` and
  `/sessions/{id}/revoke`, *Signed-in devices* on *Security*, the installed app sends a `CircleTools-App`
  User-Agent; no stay-signed-in box on the native heads; SSO button hidden; initial avatar / "Sign in"
  account button; e-mail in the menu and on *Profile*. Unit + API tests green, browser-verified (second
  browser signed out from the list). Open: phone check; production needs the migration.
- **WP-7 · Sharing.** Q32/I3 (share text with context; short reveal links for online groups). *First:*
  ADR for short links.
  **Implemented 2026-10-16** ([ADR-0082](../adr/0082-short-reveal-links-for-online-groups.md); FR-005, FR-006
  amended): `RevealShareMessage` (context message, DE/EN/FR); short `…/r#for=&rt=` links once the slices are
  published (`RevealSlicePublisher` now reports success), long links otherwise; the reveal page fetches a
  short link's slice, keeps it for offline reopening, and says when the first open needs a connection.
  Found on the way: each organizer device minted its own reveal tokens and replaced the other's on every
  sync (live links fell back to their snapshot without anyone noticing). Fixed with keyed slices — the
  server keeps the stored token per (event, giver) and returns it ([ADR-0068](../adr/0068-live-reveal-links-for-backend-backed-groups.md)
  amendment, migration `AddRevealSliceKey`). Old and new clients on one group still flip until the old
  ones update — **deploy the server and ship the app together.**
- **WP-8 · Wishlist items.** I8 (optional link and price, auto-linked URLs, over-budget hint,
  backward-compatible format). *First:* ADR for the wishlist format.
  **Implemented 2026-10-16** ([ADR-0083](../adr/0083-links-in-wishlist-items.md); FR-030 amended, glossary
  *Wishlist*): links are recognised anywhere in a wish and shown as "Open link (site)" on the reveal page and
  *My assignments*; no price. A first version with a "·" convention and prices was dropped on review as
  confusing. The save status now follows every change. Unit + component tests green.
- **WP-10 · Release & verification of WP-1…WP-8 (open).**
  - **Deploy order:** server first, then the apps. *Updated 2026-10-25 (FEAT-027):* the server's migrations
    are now `AddSessionDevice`, the `secretsanta` schema (`InitialGroupStore`), `RetireOldGroupStores` (drops the
    `linking` and `shared` schemas — test data only, decision D2) and `DropGroupSync`; old app builds stop working
    against it, so the Pixel gets the new build in the same step.
  - **Pixel check** of what only shows on a device: Q11 keyboard/scroll (`ViewportBehavior`), Q17 tap
    fall-through, reminders (WP-3), the app's `CircleTools-App` User-Agent in *Signed-in devices*, a second
    reveal link arriving while one is open. Needs a debug build on the phone (replaces the Play install).
  - **E2E suites** not run (they boot the AppHost on the ports and `bin` folders in use locally). Expected to
    need updates for WP-2's "signed-in sharing stays in the account": `ClaimAdminTests`,
    `AccountDeletionDispositionTests`, `MultiDeviceDeleteTests` (they claim admin after a signed-in shared
    create). `PasskeyCeremonyTests` was adapted to the new account button.
  - Also fixed on the way (2026-10-16): the device language from Settings no longer reverts on reload or
    sync (FR-012 amendment); the *Your data* texts no longer tell a signed-in person to sign in.
- **WP-9 · Test data.** On production: *QA-Familie* (Shared by link, organizer A, 3 exclusions) and its
  local copy in the Playwright browser profile (account B); *T2-Link* gained a member "ResumeTest". The
  phone is signed in as **A** (account A), in German.

## Sync & storage-mode test follow-ups — Secret-Santa (2026-10-15)

From the [2026-10-15 sync test](../brainstorming/2026-10-15-secret-santa-sync-device-test.md) (Pixel 9 Pro +
browser against production). All findings are fixed in `7bb52d1`. The decisions are in
[ADR-0073](../adr/0073-storage-mode-is-authoritative.md) Amendment 2,
[ADR-0078](../adr/0078-my-account-groups-record-their-account.md) Amendments 2–3,
[ADR-0044](../adr/0044-sign-in-reconciliation-and-account-scoped-visibility.md) (amendment 2026-10-15) and
[ADR-0079](../adr/0079-sign-out-this-device-and-change-password.md)
([FR-049](../requirements/functional/FR-049-change-password.md),
[FEAT-023](../features/FEAT-023-session-scope-and-change-password.md)). Open:

- **SYNC-1 · Deploy, server first.** The server must go out before the apps: ADR-0078 Amendment 2 (an
  older server answers 404 for a released group, which a new client reads as "deleted") and ADR-0079 (an
  older server ignores the logout body and ends every session, the old behaviour). Apps already installed
  keep "sign out everywhere" until they update. After the deploy, re-check S4 on the phone.
- **SYNC-2 · Device check on the Pixel.** *Done 2026-10-16* in the
  [two-account test](../brainstorming/2026-10-16-secret-santa-two-account-device-test.md): D5, S7 and S1
  pass on the device; D1 is only partly fixed (rest → Q11, WP-4); D8 seen once (→ Q17, WP-4). Original
  list — these fixes were built but not yet run on the phone:
  - D1: the keyboard no longer pushes the page or covers bottom sheets;
  - D5: closing the share sheet doesn't count as "handed out" (Android chooser callback);
  - S7: sync on resume and every 2 minutes in the foreground;
  - S1: the import prompt on Android;
  - D8: re-check the swallowed tap after "Draw again".

  Planned after the deploy (SYNC-1): test the released build from the Play Store against production, with
  the two test accounts, in a new session. Start from the
  [session doc](../brainstorming/2026-10-15-secret-santa-sync-device-test.md)'s status table. Only if an
  unreleased build is needed: a debug build against the local stack
  ([device testing](../deployment/device-testing.md)), which requires removing the Play Store install
  first and wipes the phone's local data.
- **SYNC-3 · Decide: deleting a Shared-by-link group as its admin.** ✅ **Settled by ADR-0086 (2026-10-22):** the
  owner's delete deletes the online copy for everyone, and the confirmation says so; a device that only organizes
  keeps or leaves its own copy. *Original note:* Today it is local-only. The online copy
  stays for join-link holders and co-admins until it expires (NFR-010), while "Make local" does delete it.
  Should one admin's delete remove the group for everyone, as for an admin-claimed group (ADR-0078
  Amendment 1)? Until decided, the delete confirmation for such a group keeps the plain wording. Owner:
  Adrian.
- **SYNC-4 · Cosmetic: the page behind the import prompt keeps the old language.** When a sign-in switches
  to the account's language, the prompt is correct but the page under its backdrop can stay in the previous
  language until the prompt is answered. Likely cause: the UI culture is async-local, and the culture
  boundary's rebuild renders in a flow that still holds the old culture. Low priority.
- **SYNC-5 · Two-browser E2E coverage.** Add E2E tests (`tests/E2E`) for the new behaviour:
  - signing out on one device leaves the other signed in;
  - a password change ends the other session;
  - a join-link participant gets the read-only view;
  - "My account → On this device" removes the group from the account's other browser.

  So far these are covered by unit and API tests plus a manual browser walkthrough only.
- **SYNC-6 · Clean up the test data in production.** On the account account A: **T2-Link** (an
  admin-claimed shared group) on the server. Also left over from testing:
  - **T3-Admin** and a phantom "Fritz" in the test browser profile;
  - **W1** (a local group with a draw) on the phone;
  - the orphaned "Testq" / "UI-Test Familie" rows from the 2026-10-14 review.

  The phone was left signed in as account B. *(Superseded 2026-10-16: the phone is now signed in
  as account A; new test data is listed under WP-9.)*

## First-run onboarding pass — Secret-Santa (2026-09-17)

A mobile-first review of the **first session** (empty app → names drawn → links shared), run as a
BA / UX-expert / designer council against the real 390px screens. Root cause it found: the primary
actions are present but **invisible or mislabelled on the first screens** — on a phone the empty-state
card's only visible button was **Sign in** (the optional account), while creating a group was a bare,
unlabelled "+" FAB; and the explainer said "…above" pointing at an inline field that is hidden on
phones. The account upsell was the loudest thing on the screen the app promises works "with no account".

**Done — SMALL items, built & browser-verified at 390px in DE/EN/FR (console clean):**

- **OB-1 · Primary "Create a group" button in the first-run empty card; Sign-in demoted to a text link.**
  `Groups.razor` empty state. New `EmptyStateCreateGroup` string (DE *Gruppe erstellen* / FR *Créer un
  groupe*). Shown only on narrow (the wide inline field already leads there, so no duplicate CTA on
  desktop). The very first tap now does the job, not account signup.
- **OB-2 · Fixed the empty-state explainer copy.** The **English** `EmptyStateExplainer` still said
  "Name a group above to start." (DGH-5's directional-copy fix had missed EN; neutral/DE/FR were already
  width-neutral). All four now read "…Create your first group to get started." (+ DE/FR equivalents).
- **OB-3 · Extended (labelled) FABs.** Groups → *Create group*, group hub → *Add occasion*, Members →
  *Add member*. The bare "+" read identically on every screen (it creates a group / an occasion / a
  member depending where you are); the label now says which, and doubles as the affordance a sighted
  non-screen-reader user was missing.
- **OB-4 · Draw is no longer a dead end.** `Draw.razor` under-two-members state was a bare sentence with
  no way forward; it now carries an **"Add members"** button linking to the Members page (new `GoToMembers`
  string). Verified: the draw page for a 0-member group shows the recovery button.
- **OB-5 · Occasion empty state copy + gating.** `NoEventsYet` dropped the "above"/"oben"/"ci-dessus"
  (same misdirection as OB-2) and now shows only once a group *has* members (the brand-new 0/0 case is
  covered by OB-6 instead of doubling up).
- **OB-6 · First-run hub hint.** On a brand-new group (0 members, 0 occasions) the hub — which leads
  with occasions (ADR-0055) — now shows a one-line **"Start by adding members, then add an occasion to
  draw names."** (new `HubFirstRunHint`), self-hiding as soon as either exists. Bridges the events-first
  IA for a first-timer with copy instead of restructuring.
- **OB-7 · "New version available" snackbar — not a real first-run defect (no change).** The council
  flagged it crowding the first paint; on inspection `UpdatePrompt` (ADR-0048) only fires from the
  service-worker *update* lifecycle, which does **not** trigger on a genuine first install (no prior SW).
  What shows in dev/repeat visits is the update mechanism working as designed. Left untouched (changing
  it risks ADR-0048); the on-screen crowding is a separate cosmetic snackbar-position question, parked.

**BIG — reviewed with the maintainer (2026-09-18) and implemented:**

- **OB-8 · Guided "first group" setup.** ✅ **Built & browser-verified (2026-09-18)** as a **progressive
  getting-started checklist** (the reviewed choice over a modal wizard) — [FEAT-019](../features/FEAT-019-getting-started-checklist.md)
  / [UC-SS-024](../use-cases/secret-santa/UC-SS-024-guided-first-group-setup.md). A non-modal card on the
  group hub sequences *name → add members → add an occasion → draw names*, ticks each off, disables "Draw"
  until an occasion exists, disappears once set up, and is **skippable** (device-local per group). Additive
  over [ADR-0055](../adr/0055-event-navigation-mobile-section-hub.md) (no ADR); supersedes the OB-6 hint.
  **Threshold finding:** "Add members" completes at **three** (not two — a two-person draw is forbidden by
  the no-reciprocal rule), so the checklist never reads done while the draw still fails. New `SecretSanta.Ui/Shared/GettingStartedChecklist.razor`;
  bUnit `GettingStartedChecklistTests` (5) — **Ui.Test 77/77**; full lifecycle verified in a real browser
  at 390px in EN + DE (empty → 3 members → occasion → draw → gone; skip + reload; draw locked until an
  occasion), console clean.
- **OB-9 · Per-screen FAB semantics.** ✅ **Codified (2026-09-18).** OB-3 already gave the FABs visible
  labels; the convention (extended labelled FABs; no dead ends; localize every string) is now written into
  [coding-guidelines §16](../guidelines/coding-guidelines.md) so new screens follow it. No further UI change.
- **OB-10 · "Reveal link" concept primer.** ✅ **Built & browser-verified (2026-09-18).** The Share page
  already reassures the *organizer* ("only the recipient can see who they drew"); added a **recipient-side**
  primer on the reveal page before the reveal tap — "This is your private Secret Santa link — only you can
  see who you got." (`RevealPrimer`, DE/EN/FR + neutral). Verified on the reveal page; RevealPageTests green.

## Mobile store-readiness & native passkeys — Secret-Santa (2026-09-17)

A store-requirements pass (Apple + Google 2026 rules, tablet/iPad priority) plus lifting the
mobile-passkey deferral now that the RP-ID `circletools.ch` is settled. Store-prep details also live in
the [mobile-apps runbook](../deployment/mobile-apps.md) "App preparation" section; the passkey decision
is [ADR-0066](../adr/0066-mobile-native-passkeys.md).

- **MSR-1 · iOS store-config parity (P1).** ✅ **Implemented 2026-09-17.** iOS `AppDelegate` now handles
  `ContinueUserActivity`/`OpenUrl` → `DeepLinkService` (reveal/event Universal Links open the app — was
  Android-only); new `Platforms/iOS/Entitlements.plist` (Associated Domains) wired via csproj
  `<CodesignEntitlements>`; `ITSAppUsesNonExemptEncryption=false`; privacy manifest gained
  `UserDefaults`/`CA92.1` + `NSPrivacyTracking=false`. iOS code doc-reviewed (can't compile on Windows,
  [ADR-0065](../adr/0065-hosted-macos-ios-builds.md)); device-verify on first TestFlight build. Android
  confirmed `targetSdkVersion=36` / `exported=true` in the merged manifest; tablet layout browser-verified
  at 13" iPad portrait+landscape (adaptive, no letterboxing). See GL-10.
- **MSR-2 · Native passkeys on the mobile heads (P1, [ADR-0066](../adr/0066-mobile-native-passkeys.md)).**
  ✅ **Android implemented & compiling; iOS implemented & doc-reviewed (2026-09-17).** `MauiPasskeyCeremony`
  is no longer a stub: **Android** via Credential Manager (`Xamarin.AndroidX.Credentials` 1.6.0.1, passes
  the server's WebAuthn JSON verbatim and returns the standard response JSON) — **builds 0 errors** (27
  NU1608 AndroidX-skew warnings, scoped to a warning via `WarningsNotAsErrors` in the App csproj; the
  PlayServicesAuth companion was dropped for pinning an incompatible older AndroidX graph, so pre-Android-14
  API 28–33 falls back to the FR-011 message — see the csproj note). **iOS** via AuthenticationServices
  (`ASAuthorizationPlatformPublicKeyCredentialProvider`, base64url↔`NSData`, iOS-16 gate) in
  `Platforms/iOS/MauiPasskeyCeremonyIos.cs`, reviewed against the Microsoft.iOS docs (fixed the generic
  `GetCredential<T>()` + `[SupportedOSPlatform("ios16.0")]` blockers) — **device-verify on TestFlight**.
  Seam (`IPasskeyCeremony`) and server unchanged. **Gated until go-live:** the passkey affordance keeps the
  FR-011 "not available here" fallback until the association files carry real ids (MSR-3) and the RP domain
  is live — password sign-in (FR-008) works throughout. Traceability: supersedes the FEAT-002 mobile-passkey
  deferral.
- **MSR-3 · Association files for deep links + passkeys (P2, go-live).** ✅ **Scaffolded 2026-09-17;**
  need real identifiers. Reveal-link **applinks** on `secretsanta.circletools.ch` (`assetlinks.json` +
  `apple-app-site-association`); passkey **`get_login_creds`/`webcredentials`** on the apex
  `circletools.ch` (Main WebApp `wwwroot/.well-known/*`) because the WebAuthn RP-ID is the apex
  (`Auth:Passkey:ServerDomain`). Placeholders: `REPLACE_WITH_PLAY_APP_SIGNING_SHA256` — ✅ **filled
  2026-09-18** (Android SHA-256 from Play App Signing, in both `assetlinks.json`, shared with GL-9) —
  and `REPLACE_WITH_APPLE_TEAM_ID` — ✅ **filled** (Team ID `U893QVXCU7`, both files). ✅ **Verified
  2026-10-29:** both `apple-app-site-association` files serve HTTP 200 with no redirect and are already in
  Apple's CDN (`app-site-association.cdn-apple.com/a/v1/<host>`); the distribution profile in build
  0.1.129 carries `com.apple.developer.associated-domains`. Only on-device verification is left (GL-10).
  Relates to GL-9, GL-10, [ADR-0066](../adr/0066-mobile-native-passkeys.md).
- **MSR-4 · Externally-blocked store gates (tracking only).** Not code: privacy-policy + account-deletion
  URLs (GL-6, hosting), DSA trader declaration (**non-trader** — free, no ads/IAP/income; reasoning in
  `store/app-store/README.md`), iPad-13"
  screenshots, App Privacy label / Data-safety form (must match the sync data), 16 KB-page-size verify on
  the release AAB (Play, 1 Feb 2027), Play closed-testing 12×14 days. Enumerated in the runbook checklist.

## Play Store submission prep — Secret-Santa (2026-09-17)

Found while preparing the Google Play Console submission (individual enrolment,
[ADR-0030](../adr/0030-mobile-store-publisher-identity.md)) and testing the signed-in app.

- **PSP-1 · Signed-in users have no in-app account-deletion path in the Secret-Santa app (bug, P1 — Play
  blocker).** Found 2026-09-17 by the maintainer testing the live signed-in app: groups can be deleted, but
  there is **no in-app way to delete the account**, despite the `/delete-account` legal page describing an
  "in-app" method. **Root cause (code-confirmed):** the working delete/export UI is `AccountData.razor`
  (route `/account/data`) and lives **only in `CircleTools.Main.Ui`** — the **Secret-Santa app has no
  account-data page at all** (no `Pages/Account/*` under SecretSanta). It is linked **only** from the Main
  app's drawer (`MainShell.razor` → "NavData"); the **shared `AccountMenu`** (top-right avatar, used by both
  apps, [ADR-0034](../adr/0034-shared-account-session-ui-block.md)) links to Profile + Security + Sign out
  only, **never to deletion**. The shared `LegalAccountDeletionContent` still renders an
  `Legal_Delete_InAppHeading/Body` section, so the Secret-Santa `/delete-account` page **promises an in-app
  path that does not exist there**. **Why it blocks submission:** Google Play's account-deletion policy
  requires an app that allows account creation to let users **request deletion from within the app**; the
  Secret-Santa head supports opt-in account/sync ([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)),
  so it needs this. Deletion functioning only on the Main origin (a different app a Secret-Santa user may
  never open) does not satisfy it. **Recommended fix (architectural, closes it for both apps at once):**
  move the delete/export page from `Main.Ui` into the shared **`CircleTools.Ui.Account`** building block
  (where `Profile`/`AccountMenu` already live) and add a **"Delete account / My data" entry to the shared
  `AccountMenu`** — then the legal page's "In-app" promise becomes true everywhere. Mind the app-specific
  dependencies the page currently pulls (`AppStrings`, `CircleErrorText`, `AccountApiClient`, `IFileSaver`,
  `ProblemAlert`) when relocating. Backend deletion itself is complete and tested
  ([ADR-0022](../adr/0022-account-deletion-and-data-export.md); `AccountDeletionDispositionTests` drives it
  against the Main origin) — this is a **UI-surface/routing gap in the Secret-Santa head**, not a
  missing feature. Relates to GL-6 (the `/delete-account` content) and
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md).
  ✅ **Implemented, tested & browser-verified — done (2026-09-17).** Moved
  `AccountData.razor` (route `/account/data`) and `CircleErrorText` from `Main.Ui` into the shared
  **`CircleTools.Ui.Account`** block (page → `Pages/`, class → `Api/`, `namespace
  CircleTools.Ui.Account.Api`); migrated its strings into `AccountStrings` (DE/EN/FR + neutral, incl. a new
  app-neutral `DeleteDoneHome` replacing the Main-only "Dashboard" link and an `AccountMenuData` label);
  added a **"Data & account" entry to the shared `AccountMenu`** (top-right avatar) so both apps expose the
  delete/export path the legal page describes. Registered `IFileSaver` in both Secret-Santa heads so Export
  works there too (the privacy page promises it): `BrowserFileSaver` + `wwwroot/js/download.js` (WASM) and a
  new `MauiFileSaver` over the device share sheet
  ([ADR-0028](../adr/0028-browser-platform-adapters-block.md)). Both Secret-Santa heads (WASM + MAUI Android)
  build 0/0; Main.Ui.Test 75/75 and Architecture.Test 28/28 green (the delete/export component + circle-error
  coverage survived the move). **Clean full-solution build 0/0**; full test suite green once run without
  Docker saturation (Main.Service.Test 245/245, SecretSanta.E2E 5/5, SecretSanta.Ui 72/72, all others).
  **DE/EN/FR real-browser walkthrough passed** (fresh Aspire stack, `secretsanta-web`): registered → signed
  in → the **account menu now shows "Data & account"** (was absent) → the page renders in all three locales →
  **Export downloaded** `circletools-export-*.json` (the new `IFileSaver`/`BrowserFileSaver` wiring) →
  password enabled the delete button → deletion succeeded and showed "account is gone" with the new
  **"Back to the app"** link (`DeleteDoneHome`); console clean throughout. Not yet on-device (MAUI Android
  head builds 0/0 but the share-sheet export + menu entry are unverified under a finger — fold into the next
  Pixel pass).
- **PSP-2 · Privacy-policy accuracy (2026-09-17).** Two claims examined during the PSP-1 text audit.
  - **(a) "unconfirmed accounts cleaned up after 30 days" — ✅ already true (audit finding was a false
    negative).** The purge is implemented, tested, and running: `PendingAccountPurge.PurgeAsync` deletes
    `!EmailConfirmed && CreatedAt < now − RetentionOptions.PendingAccountLifetime` (30 days) in
    `Main.Service.Repository/PendingAccountRetention.cs`, `PendingAccountRetentionService` (since renamed `MainRetentionService`) runs it hourly, and
    `AddCircleToolsRetention(...)` wires both at `Main.Service/Program.cs:120`. Covered by
    `PendingAccountPurgeTests` (4 tests, green in Repository.Test 58/58). The original "no job found" note was
    a too-narrow grep (searched `Main.Service`/`Identity`, but the job lives in `Main.Service.Repository` and
    is registered via a named extension). *Design note (not a defect):* the purge is a direct
    `context.Users.RemoveRange`, not the `IAccountDeletionCleanup` cascade — deliberate and safe because an
    unconfirmed account is gated out of circle ownership; if unconfirmed-but-signed-in accounts can ever
    accrue cross-schema (sync/linking) rows, revisit whether the FK cascade covers them.
  - **(b) "no hidden copy kept" vs backups — ✅ fixed (2026-09-17).** The nightly **restic backups** (GL-5)
    retain snapshots up to ~6 months, so deleted data survives in encrypted off-site backups. Reworded
    `Legal_Privacy_RetentionBody` (DE/EN/FR + neutral) to say the account is gone from the *live service* right
    away, with the only lingering trace in the encrypted disaster-recovery backups, which a deleted account
    drops out of within a few months as they roll over. `Legal_Delete_InAppBody`'s "no way back" left as-is
    (it speaks to user-side irreversibility, not backup absence). Ui.Shared builds 0/0. Still worth a glance in
    the **GL-6** legal review. Confirmed-true in the same audit: the "30-day sign-in-token window" (matches
    `RefreshTokenLifetime`), Infomaniak-Geneva hosting, Brevo/EU mail, and HMAC-scrambled invitation emails.

## Go-live — manual owner action (2026-09-15)

Tracked-so-it-is-not-rediscovered work items from the
[launch-readiness assessment](../deployment/launch-readiness.md): the suite is engineering-complete
(0/0 build, 762 tests, 60 Accepted ADRs, full deploy scaffolding) but not yet live. Each item below
**needs the project owner to act** — provisioning under a real account, publishing legal content, or a
manual fingerprint/Mac step — so none can be closed by code alone. Decisions are already recorded; these
are execution items against the milestone-8 [web-api-db](../deployment/web-api-db.md#checklist-milestone-8)
checklist.

- **GL-1 · Provision the Infomaniak VPS under a project-owned account (P1).** VPS Lite M, Geneva,
  Ubuntu LTS; Docker + Compose; not a personal login. [ADR-0036](../adr/0036-cloud-provider-and-hosting.md).
- **GL-2 · Generate secrets and load the two tiers (P1).** Generate into the vault, derive the VM
  `/opt/circletools/.env` and the GitHub Actions secrets; setting `DEPLOY_HOST` self-activates the CD
  job. [ADR-0050](../adr/0050-secrets-management.md).
- **GL-3 · Infomaniak DNS + Brevo account + SPF/DKIM/DMARC (P1, highest leverage).** Point the
  `circletools.ch` A records (`@`, `secretsanta`, `api`, `grafana`) at the VPS in the Infomaniak zone
  ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md) — no Cloudflare), and add the
  Brevo account + **SPF/DKIM/DMARC** records there too. Undeliverable mail silently breaks sign-up /
  verify / reset / invite — the whole onboarding path.
  [ADR-0037](../adr/0037-transactional-email-provider.md).
  ✅ **Verified (2026-09-17).** All four A records (`@`, `secretsanta`, `api`, `grafana`) resolve to the
  VPS (`<server IP>`); Brevo DKIM is delegated (`brevo1/brevo2._domainkey` → `b1/b2.circletools-ch.dkim.brevo.com`);
  DMARC is published (`v=DMARC1; p=none; rua=mailto:rua@dmarc.brevo.com`); SPF is
  `v=spf1 include:spf.infomaniak.ch -all`. End-to-end confirmed by a real account-creation whose
  verification mail was sent successfully — DMARC aligns via **DKIM** (Brevo's model), so the SPF record
  not including Brevo is fine. **Optional later hardening (non-blocking):** add `include:spf.brevo.com`
  for SPF alignment too, and tighten DMARC to `p=quarantine`→`p=reject` after reviewing `rua` reports.
- **GL-4 · Bring up TLS (P1).** ✅ **Resolved by dropping Cloudflare
  ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md), 2026-09-16).** The HTTP-01
  caveat only existed because of Cloudflare's proxy; with DNS staying at Infomaniak and **no proxy in
  front**, Caddy's default **HTTP-01** challenge reaches `:80` directly and issues Let's Encrypt certs for
  all origins on first boot. Reverted the scaffold to stock `caddy:2-alpine` + HTTP-01 (removed the
  `caddy-dns/cloudflare` build, the `acme_dns` directive, and `CLOUDFLARE_API_TOKEN` from `compose.yaml` /
  `.env.example`); dropped Cloudflare from the public **privacy/imprint** strings (DE/EN/FR) and swept the
  docs. **Owner action:** just open ports 80/443 and let the A records (GL-3) propagate before bringing the
  stack up — nothing else. See [server setup §9](../deployment/server-setup.md).
- **GL-5 · Nightly `pg_dump` → Swiss Backup + a tested restore (P1, biggest data-loss risk).**
  ✅ **Scripted (2026-09-16).** `deploy/backup.sh` streams a nightly `pg_dump` into an encrypted,
  deduplicated **restic** repo on Swiss Backup (S3), prunes (7 daily / 5 weekly / 6 monthly) and runs
  `restic check`; `deploy/systemd/circletools-backup.timer` schedules it 03:30 nightly; `deploy/restore.sh`
  does a **safe restore drill** into a scratch DB by default (`--target prod` for a real recovery). restic
  chosen as the tool (encryption + dedup + retention + verify in one binary; recorded in the runbook, no
  ADR — ADR-0036 fixes the policy). Runbook [Backups & restore](../deployment/backups.md); secrets templated
  in `deploy/.env.example`. ✅ **Closed on the VM (2026-09-17).** Swiss Backup S3 access + secrets
  provisioned in `/opt/circletools/.env`; the restic repo was initialised at
  the Swiss Backup bucket named in `RESTIC_REPOSITORY` (`.env` on the VM); a manual `backup.sh` ran green
  (snapshot saved, `restic check` reported *no errors*); the `restore.sh` drill **PASSED** — 33 tables
  restored across all five schemas (circles/linking/public/shared/sync) into a scratch DB and dropped,
  without touching live data; and `circletools-backup.timer` is installed + enabled (nightly ~03:30 UTC,
  confirmed via `systemctl list-timers`). The first dump was ~56 KiB (9 KiB stored after
  compression/dedup), so the 200 GB Swiss Backup quota is effectively unbounded for this workload.
  **Residual (non-blocking, tracked as GL-11):** no backup-failure alerting yet.
  [ADR-0036](../adr/0036-cloud-provider-and-hosting.md).
- **GL-6 · Legal review of the legal pages (P1).** Privacy policy + imprint (about & legal) +
  account-deletion page are **written and reachable** at `/privacy`, `/imprint`, `/delete-account` in
  both web apps, DE/EN/FR, browser-verified, in a warm first-person voice. Operator/controller named as
  Adrian Moll (circletools.ch), contact info@circletools.ch, no postal address (non-commercial choice).
  Remaining: a **light legal review** of the wording before go-live; and if the project is ever monetised
  or store-classified as a **trader**, add a postal address or a legal entity (e.g. a Swiss Verein).
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md).
- **GL-7 · Pin the CSP on the live stack (P2).** The policy is statically verified and boot-smoke'd;
  the final confirmation against the real cross-origin `connect-src` API fetch only happens live.
  [ADR-0021](../adr/0021-web-auth-token-model.md), [ADR-0040](../adr/0040-ui-component-library-mudblazor.md).
- **GL-8 · Live on-device sign-in / sync test (P2).** Drive account sign-in and sync from a real device
  against the running Main service — still owed since the parity work.
  [ADR-0039](../adr/0039-cross-app-single-sign-on.md).
- **GL-9 · Replace the `assetlinks.json` Play App Signing SHA-256 (P2, mobile).** ✅ **Done 2026-09-18** —
  the real fingerprint (`BB:EF:78:…:F1:EC`) from Play App Signing is in both `assetlinks.json` files, so
  reveal / calendar App Links auto-open silently instead of via the chooser. Residual: **redeploy** the
  web front so the updated `.well-known/assetlinks.json` serves.
  [ADR-0053](../adr/0053-backend-free-first-mobile-release.md).
- **GL-10 · Build the iOS head + wire iOS deep links (P2).** ~~Mac-gated~~ **No longer Mac-gated**
  ([ADR-0065](../adr/0065-hosted-macos-ios-builds.md): iOS builds on hosted macOS CI). ✅ **iOS deep
  links wired in code (2026-09-17):** `Platforms/iOS/AppDelegate.cs` now handles `ContinueUserActivity`
  (Universal Links) + `OpenUrl` and forwards to `DeepLinkService` (parity with Android's `MainActivity`);
  `Platforms/iOS/Entitlements.plist` declares `applinks:secretsanta.circletools.ch` (+
  `webcredentials:circletools.ch` for passkeys, [ADR-0066](../adr/0066-mobile-native-passkeys.md)), wired
  via csproj `<CodesignEntitlements>`; the `apple-app-site-association` files are scaffolded on both
  origins (see GL-9). ✅ Signed builds now reach TestFlight, and the Associated Domains capability is in
  the App Store profile (verified 2026-10-29 on the 0.1.129 `.ipa`). **Still owner:** run the build on an
  iPhone and iPad to verify deep links + passkeys on iOS/iPadOS.
  [ADR-0053](../adr/0053-backend-free-first-mobile-release.md).
- **GL-11 · Alert on backup failure (P2, non-blocking).** ✅ **Implemented (2026-09-17).**
  `circletools-backup.service` now carries `OnFailure=circletools-backup-failure@%n.service`, a templated unit
  that runs `deploy/notify-failure.sh` — it composes a message (host, time, last 20 journal lines) and sends it
  via **email** (reuses the Brevo SMTP login, `ALERT_EMAIL_TO`) and/or a **webhook** (`ALERT_WEBHOOK_URL` —
  ntfy/healthchecks/chat), always logging to the journal as a fallback; best-effort, always exits 0. See the
  backups runbook [Monitoring & failure alerting](../deployment/backups.md#monitoring--failure-alerting-gl-11).
  **Installed on the VM 2026-10-28:** `circletools-backup-failure@.service` + `ALERT_EMAIL_TO` in `.env`; the
  test alert email was sent. That install found that the deploy workflow never shipped
  `notify-failure.sh` to the VM (so no backup alert could have fired until then); `deploy.yml` now rsyncs it. A `restic snapshots` freshness "backup is fresh" signal into Grafana
  ([ADR-0051](../adr/0051-production-observability-backend.md)) remains a nice-to-have.
  [ADR-0036](../adr/0036-cloud-provider-and-hosting.md).
- **GL-12 · Confirm the live web apps actually boot in a real browser (verification, P2).** Raised
  2026-09-17. An initial `WebFetch` of `https://circletools.ch` *appeared* to show the Blazor *"An unhandled
  error has occurred."* bar — but that was a **false positive**: that banner is `display:none` boilerplate
  present in every Blazor WASM `index.html` (identical in the served shell and in-repo), and `WebFetch`
  cannot execute JavaScript, so it never booted the app and just rendered the hidden div's text.
  **Server-side checks are all healthy** (2026-09-17): both `circletools.ch` (Main) and
  `secretsanta.circletools.ch` return 200 with the correct shell + loading spinner, the strict CSP header is
  served by Caddy per [ADR-0021](../adr/0021-web-auth-token-model.md) (incl. `wasm-unsafe-eval`), HSTS +
  security headers present, and the API responds (structured ASP.NET `problem+json`; `/health` path is 404
  only). **What's still unverified:** that the WASM runtime *boots to a rendered UI* — curl proves the shell
  and assets are served, not a clean boot. **Do:** open both apps in a real browser (JS on), confirm they
  render and the console is clean — the CLAUDE.md "verify in a real browser" rule, and owed before store
  submission since the listing needs live `/privacy` `/imprint` `/delete-account` (GL-6,
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)). Not a Play *signup* blocker.
  ✅ **Verified & closed (2026-09-17).** The maintainer opened both `circletools.ch` (Main) and
  `secretsanta.circletools.ch` in a real browser: each shows the normal short WASM loading indicator (the
  `loading-progress` spinner while the .NET runtime + assemblies download) and then renders the app. No error
  bar, no defect — the original signal was purely the `WebFetch` false positive described above. Standing
  reminder unchanged: the substantive pre-submission need is that `/privacy` `/imprint` `/delete-account`
  render correctly in DE/EN/FR (GL-6), which is a separate content check, not a boot problem.

## Transactional email — branded HTML & per-app identity (2026-09-17)

Follow-ups from [FEAT-018](../features/FEAT-018-branded-transactional-emails.md) /
[ADR-0064](../adr/0064-transactional-email-html-and-per-app-branding.md) — branded HTML mail with per-app
co-branding and source-app link origins is built and unit-tested; these are the deferred bits:

- **TEM-1 · White-on-transparent PNG email logo per app (P2).** ✅ **Done (2026-09-17).** White-glyph
  source SVGs + rendered 200×200 transparent PNGs (`email-logo-main.png`, `email-logo-secretsanta.png`) live
  in `CircleTools.Ui.Shared/wwwroot/img/`, served by every host at `_content/CircleTools.Ui.Shared/img/`;
  `Mail:Apps:<app>:LogoUrl` is set in `appsettings.Production.json` to each app's own origin, so the header
  `<img>` now renders (white circle-of-people on navy / white gift on maroon), verified by
  `MailTemplateRendererTests`. Dev keeps the wordmark-only header (no absolute origin).
- **TEM-2 · Explain the reusable CircleTools login when app #2 ships (P3).** With only Secret-Santa live, an
  SSO explainer in mail would confuse more than help; the footer carries only a subtle "CircleTools account"
  seed for now. Add the real "one login across CircleTools apps" explainer when a second app exists — which
  is also when the per-app link origins start earning their keep.
- **TEM-3 · Real-client visual pass + farewell integration test (P2).** ✅ **Mailpit pipeline pass done
  (2026-09-17):** all 12 representative variants were delivered through the real renderer + SMTP into Mailpit
  and verified (per-app colour/sender/origin, CTA + localized fallback, plaintext alternative, DE/EN/FR,
  farewell, HTML-encoding). **Still owed:** a human visual eyeball in the Mailpit UI and one **real external
  client** (Gmail / Outlook / Apple Mail) across DE/EN/FR and both apps, per the CLAUDE.md "verify in a real
  client" rule; and an integration assertion that a farewell mail follows a successful deletion.

## Shared-by-link sync — Secret-Santa (2026-09-14)

Found while re-testing the account-free **link-shared groups** ([FEAT-013](../features/FEAT-013-account-free-link-shared-groups.md),
[ADR-0058](../adr/0058-account-free-link-shared-groups.md)) end-to-end in **two independent real browsers** against
the live Aspire stack — the admin↔admin path the earlier pass missed (it only drove the participant *join* link).

- **SBL-1 · Shared-by-link changes never pull on a plain refresh while signed out (bug, P1).** ✅ **Fixed,
  browser-verified.** A co-organizer who opened the **admin link** in a second browser and added members saw them
  pushed to the server correctly, but the **creator's browser never showed them on refresh** — not even on repeated
  refreshes. Same class as **DGH-6**: a sync path that only fires when signed in, applied to data that is
  account-free. `AutoSyncService.RunSharedCycleAsync` is explicitly built to reconcile shared-by-link groups
  **signed in or out**, but the **app-start trigger** in `InitializeAsync` only called `RequestSync()` when
  `CurrentAccountAsync() != Guid.Empty` — so a signed-out shared-by-link holder's cycle only ran after a *local*
  edit (which then clobbers the un-pulled change via last-write-wins), a reconnect, or (touch-only) pull-to-refresh.
  A plain desktop refresh triggered nothing. **Fix:** `InitializeAsync` now calls `RequestSync()` unconditionally on
  app start — the shared cycle runs first and always, the account cycle self-gates on sign-in, and the whole cycle is
  a cheap no-op when there is nothing to reconcile. **Diagnosis proof:** before the fix, adding any member on the
  creator's device (which *does* trigger `RequestSync`) immediately pulled the co-admin's members on the next refresh,
  isolating the bug to the missing trigger rather than the reconcile. **Browser-verified (two browsers, live stack):**
  B1 creates a shared group → B2 opens the admin link and adds Carol+Dan (server reaches v3) → **B1 pulls both on a
  pure refresh with no local edit** (was empty on every refresh before the fix); console clean.
- **SBL-2 · Section pages didn't refresh in place when a background sync landed (bug, P2).** ✅ **Fixed,
  unit- and two-browser-verified (2026-09-15).** Follow-on found by the maintainer editing the same group in two admin windows: after SBL-1 a
  co-admin's edit *did* sync, but the open page only showed it on the *next* refresh — the refresh that triggered the
  pull rendered the pre-pull state, so it felt like "several refreshes" or "only a force-refresh (Ctrl+Shift+R) works."
  Confirmed it was **not** the HTTP cache (verified: a default-cache fetch and a `no-store` fetch both returned the
  fresh version; the API sends no cache headers but Chrome was not serving stale) — the page simply never re-read the
  store after a background pull. **Fix:** a small `ISyncSignals` seam (just the `SyncCompleted` event, implemented by
  `AutoSyncService`, resolved to the same instance) that the group/event **read** pages — Members, group hub
  (`GroupEditor`), Exclusions, `EventHub`, Draw, Share — subscribe to and reload on, mirroring what the shell already
  does for the drawer. The two **form** pages (`EventEditor` details, `EventWishlists`) were deliberately left out: a
  live reload there would discard the editor's in-progress typing. A page depends only on the notification, so a
  component test supplies a trivial `FakeSyncSignals` instead of the whole sync graph. Covered by a new bUnit
  regression (`MembersPageSyncRefreshTests`: firing `SyncCompleted` swaps the roster in place); **37/37 Ui tests
  green**. **Two-browser re-verify done (2026-09-15, live Aspire stack, two isolated headless-Chrome devices over
  CDP):** device 1 created a shared-by-link group (member *Anna*) and opened Members; device 2 opened the **admin
  link** (group adopted with the *Shared by link* chip, *Anna* pulled) and added *Carol* → pushed. With device 1's
  Members page **open and idle** (Anna, Carol; no *Erin*), device 2 added *Erin*; a background cycle then landed on
  device 1 and **Erin appeared in place — no navigation, no manual refresh** (the `SyncCompleted` reload), the
  add-member field left undisturbed. Console clean throughout. This also re-confirmed **SBL-1** (device 1's fresh
  signed-out load pulled Carol without a hard refresh). Still a known limit (P3, parked): an **idle** open tab with no trigger doesn't pull at all — a co-admin's
  change needs a refresh (or reconnect / pull-to-refresh) to *start* a cycle; a focus/`visibilitychange` or periodic
  pull would make an idle tab live, but that nudges the deliberately-parked continuous-sync decision
  ([ADR-0016](../adr/0016-sync-and-conflict-resolution.md)).
  - **Also observed (expected, note only):** shared reconcile is last-write-wins per aggregate, so a local edit made
    on one device *before* it has pulled a newer server version is silently superseded on the next cycle. Inherent to
    the ADR-0058 whole-aggregate model; flagged so it is a known trade-off, not a surprise.

## Competitor-research candidates — Secret-Santa (2026-09-02)

Promoted from the [DE/EN/FR app-store competitor & review scan](../brainstorming/2026-09-02-secret-santa-competitor-research.md).
The scan's headline is that our **no-account / offline / link-or-QR / non-spoiling-reveal / draw-that-never-repeats**
spine is exactly what reviewers reward and incumbents get wrong — so these are the *on-strategy additions* it
surfaced, not a pivot. Each is a stub; take through the [process](../process/ways-of-working.md) (use case →
requirement → feature spec) if pursued. The QR work already in flight is **MNF-5** in the
[distribution cluster](#mobile-native-feel-pass--secret-santa-2026-09-02); reminders are **MNF-8** below (this scan
raises its motivation).

- **CR-1 · Recurring event carries wish lists + exclusions forward (P1).** Highest-value new candidate. Reviewers
  in all three markets complain that copying last year's event **loses the wish lists** (children's lists must be
  re-typed) and sometimes the exclusions too (DrawNames, Giftster). When creating a new occasion from a group that
  already has a past one, offer to **carry forward each member's wishlist and the exclusion set** (editable, not
  forced). Fits the events-as-first-class model ([ADR-0042](../adr/0042-events-as-first-class-entities.md)) and the
  member-archival/draw-history work ([ADR-0052](../adr/0052-member-archival-and-permanent-draw-history.md)). Design
  q: carry-forward as a copy at creation vs. a live link to the prior occasion.
  **✅ Promoted through the process (2026-09-02):** [UC-SS-014](../use-cases/secret-santa/UC-SS-014-carry-forward-new-occasion.md)
  → [FR-035](../requirements/functional/FR-035-carry-forward-new-occasion.md) →
  [FEAT-010](../features/FEAT-010-carry-forward-new-occasion.md) / [TP-010](../features/TP-010-carry-forward-new-occasion.md),
  new glossary term [Carry-forward](../glossary/carry-forward.md). Scope **sharpened against the model**: exclusions
  already carry (shared at group level, FR-034), so carry-forward copies **wishlists + budget** and seeds the new
  event's **draw-history baseline** with the source draw so the next draw won't repeat last year's pairing — which
  also answers the "same recipient every year" complaint. Copy-at-creation chosen (events stay independent, ADR-0042);
  no new ADR. Distinct from the account-only linked-member self-copy (`CopyFrom`, FR-030). ✅ **Built & browser-verified
  2026-09-02** ([FEAT-010](../features/FEAT-010-carry-forward-new-occasion.md) *Done*): `Group.AddEventFrom`
  + an `Event` baseline draw (separate from the draw history, so a carried occasion reads as undrawn but its
  first draw avoids last time's pairings), `CarryForwardEventDialog` on the group hub, and a "draw anyway"
  escape on the Draw screen for the rare over-constrained roster. Solution builds clean; 198 tests green
  (incl. 8 new); Playwright DE/EN/FR walkthrough clean-console, carried occasion draws.
- **CR-2 · Budget minimum *and* maximum (P2, cheap).** Every surveyed app offers only a **max** price; a recurring
  wish is a **minimum** too (so cheap-out gifts don't undercut the exchange). The event already has budget fields
  ([per-event editor](../features/FEAT-007-events-per-group.md)); add an optional lower bound and show the range on
  the reveal link. Small, closes a universal gap. Watch localization: CHF/EUR formatting per
  [ADR-0056](../adr/0056-decouple-ui-language-from-formatting-region.md).
  **✅ Promoted through the process (2026-09-03):** [UC-SS-015](../use-cases/secret-santa/UC-SS-015-budget-minimum-and-maximum.md)
  → [FR-036](../requirements/functional/FR-036-budget-minimum-and-maximum.md) →
  [FEAT-011](../features/FEAT-011-budget-minimum-and-maximum.md) / [TP-011](../features/TP-011-budget-minimum-and-maximum.md),
  glossary [Budget](../glossary/budget.md) amended to define the range. Design calls (recorded in the FEAT, **no ADR**):
  the existing single amount **is the maximum** and the new field is an optional floor (`0 ≤ min ≤ max`, one currency);
  min-only budgets are not offered. The minimum rides the reveal payload as an **additive optional field** (`bm`)
  **omitted when unset**, so max-only and pre-existing links are byte-for-byte unchanged and the payload version does
  not bump. ✅ **Built & browser-verified 2026-09-03** ([FEAT-011](../features/FEAT-011-budget-minimum-and-maximum.md)
  *Done*): `Budget.MinAmount` + invariant, a **Minimum** field on the event-details editor (old "Amount" relabelled
  **Maximum**), and a shared `BudgetDisplay.Format` rendering "10–30 CHF" (range) or "30 CHF" (bare max) on the reveal +
  my-assignments screens. Solution builds clean; **208 tests green** (incl. 10 new); Playwright DE/EN/FR walkthrough
  clean-console — range shown, max-only unchanged, min-exceeds-max rejected inline.
  **Gap found & closed (2026-09-03):** at ship time the range reached only the account-free **reveal link** — a linked
  member's *my-assignments* rebuilds from a server projection that stored only budget amount+currency, so it showed the
  bare max. **CR-3** carried the minimum through that projection (new `BudgetMinAmount` column + Linking migration,
  needed there anyway for the gift note), so my-assignments now shows the range too. FEAT-011 corrected accordingly.
- **CR-3 · "Do-not-gift" / dietary & lifestyle notes per member (P2).** Distinct from person-*exclusions* (who draws
  whom): a free-text/tagged **"please don't get me…" / allergies / dietary / lifestyle** note the giver sees on the
  reveal. Repeatedly wished-for across markets, done well by nobody. Rides on the existing wishlist plumbing and the
  self-contained reveal payload ([ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md)) — mind the encoded-link
  size budget. Glossary: may want a term (extends [wishlist](../glossary/wishlist.md)).
  **✅ Promoted through the process (2026-09-03):** [UC-SS-016](../use-cases/secret-santa/UC-SS-016-member-gift-note.md)
  → [FR-037](../requirements/functional/FR-037-member-gift-note.md) →
  [FEAT-012](../features/FEAT-012-member-gift-note.md) / [TP-012](../features/TP-012-member-gift-note.md),
  new glossary term [Gift note](../glossary/gift-note.md). **Design call (recorded, no ADR):** modelled **on the
  `Member`** (a stable per-person attribute — an allergy is the same every occasion), *not* per-event like a wishlist,
  so it is set once and shown on every draw; organizer-authored in v1 (linked-member self-authoring parked). Free text
  capped at **500 chars** to protect the encoded link; carried as an **additive optional** reveal-payload field (`gn`)
  omitted when unset (version unchanged). ✅ **Built & browser-verified 2026-09-03**
  ([FEAT-012](../features/FEAT-012-member-gift-note.md) *Done*): `Member.GiftNote` + `Group.SetMemberGiftNote`
  (independent of rename), a gift-note field on the members editor, and a **"Good to know"** line on the reveal +
  my-assignments (after the giftee is revealed). The linked-member path required a **Linking Postgres migration**
  (`AddAssignmentBudgetMinAndGiftNote`) adding `RecipientGiftNote` **and** `BudgetMinAmount` to the assignment
  projection — which **also closed the CR-2 gap** (the budget range now reaches my-assignments, not only the reveal
  link). Solution builds clean; **218 SecretSanta+arch tests green** (incl. 11 new) + a Docker-only Main projection
  test; Playwright DE/EN/FR walkthrough clean-console.
- **CR-4 · Schrottwichteln variant — each person draws 2 recipients (P3, DACH differentiator).** A culturally
  distinct DACH format (each participant is Wichtel for **two** people) that **native apps largely ignore** — only
  web tools (Wichtlify) model it. Sits naturally with the Swiss-first stance. Needs a draw-algorithm variant (the
  fair-draw/no-reciprocal/no-repeat invariants must extend to a 2-recipient assignment) and a reveal that shows both
  giftees. Bigger than CR-1..3; spec as its own feature if pursued. Ubiquitous-language check first — add a
  **Schrottwichteln** glossary entry before naming it in code.

## Drawer & group-hub refinements — Secret-Santa (2026-09-02)

Follow-ups from the maintainer's use, on top of the [chrome/navigation review](#mobile-chrome--navigation-review--secret-santa-2026-09-02).

- **DGH-6 · Deleted groups lingered in the drawer quick-access (bug, P1).** ✅ **Fixed, browser-verified.**
  A group deleted **while signed out** stayed in the drawer's quick-access until the next navigation/sync:
  the after-change trigger (`AutoSyncBehavior` → `RequestSync`) only reaches the drawer via `SyncCompleted`,
  which a signed-out cycle never fires. Added a **`LocalChanged`** event on `AutoSyncService` (raised by
  `RequestSync`, so on every mutation regardless of sign-in); the shell subscribes and re-reads
  quick-access, which filters by `VisibleGroupsService` so a deleted group drops immediately. Browser-verified
  (390px, signed out): create → group in drawer; delete (no navigation) → gone from the list **and** the
  drawer.

- **DGH-1 · Settings pinned to the drawer bottom + bigger nav targets.** ✅ **Built, browser-verified.**
  The drawer is now a full-height flex column: the main nav scrolls in the top region and **Settings sits
  pinned at the very bottom** (empty space between when there are few groups/events), so it reads as an
  app-level control. Drawer nav links get a **≥48px** target and a slightly larger font on narrow
  viewports. Browser-verified (390px): Groups link 48px tall; Settings link top at y≈796 of 844.
- **DGH-2 · Group hub (ADR-0055 amendment).** ✅ **Built, browser-verified; device pass pending.** The
  group landing is now a **hub** matching the event hub ([FEAT-009](../features/FEAT-009-event-section-hub.md),
  [ADR-0055 amendment](../adr/0055-event-navigation-mobile-section-hub.md)): the **occasions list** is the
  primary content, with **Members** and **Exclusions** as `ct-hub-row` rows (icon + count + chevron) below;
  `GroupNav` tabs retired, `GroupBackNav` added to the Members/Exclusions pages. Browser-verified (390px,
  en-US): no tab strip; Members/Exclusions rows with counts; adding an event lists it; row → `/members`
  with a back-to-group link; console clean.
- **DGH-3 · Group rename moved into the header overflow menu.** ✅ **Built, browser-verified.** The
  always-visible group-name text field is gone; rename lives behind the header **⋮ → "Rename"**, opening
  the W4 `TextInputSheet` (extended to pre-fill the current name). **Suggested label: "Rename"
  (Umbenennen)** while the name is the only group setting; promote to **"Group settings"** if more arrive.
  Browser-verified: sheet pre-fills "Familie"; renaming updates the hub title.
- **DGH-4 · Pinned Settings link clipped by the screen corner (P2).** ✅ **Built, browser-verified.** The
  bottom-pinned Settings link sat flush against the phone's rounded corner / gesture bar. Added
  `padding-bottom: calc(env(safe-area-inset-bottom) + 12px)` to `.ct-drawer-bottom` so it clears both.
  Browser-verified (no inset): 12px gap; on device the gesture-bar safe area adds more. **Confirm on the Pixel.**
- **DGH-5 · Create group / add event via a FAB (P1).** ✅ **Built, browser-verified; device pass pending.**
  Same pattern as adding members (FEAT-008 W4): on a phone the inline **create-group** field (Groups) and
  **add-event** field (group hub) are hidden and a **"+" FAB** opens a bottom sheet to enter the name — so
  each screen leads with the *existing* groups/events. The add-event sheet **pre-fills the default name**
  (e.g. "Christmas 2026"). Onboarding copy reworded to drop directional "above/oben/ci-dessus" (now width-
  neutral: "Add a group to get started."). Inline fields stay on wide. Browser-verified (390px, en-US):
  Groups FAB → sheet → group hub; events FAB → **pre-filled** sheet → event hub; inline fields hidden on
  narrow; console clean. **Confirm the FAB feel on the Pixel.**

## Mobile chrome & navigation review — Secret-Santa (2026-09-02)

A pass over app-chrome placement and navigation against mobile conventions, driven by the maintainer's
own use, on top of the [native-feel pass](#mobile-native-feel-pass--secret-santa-2026-09-02) below.

- **MCN-1 · First-run language follows the device (P1).** ✅ **Built 2026-09-02.** The app used to start
  in `de-CH` for everyone with no stored preference — it never looked at the device. Now
  `CultureProvider.InitializeAsync` uses the device/browser language on first run, and `AppCultures.Resolve`
  gained **language-prefix matching** (any `de-*` → de-CH, `fr-*` → fr-CH, `en-*` → en, else de-CH). A
  stored pick still wins; a signed-in account's language still overrides on sync
  ([ADR-0035](../adr/0035-profile-sync-seam.md)); nothing is persisted on first run, so the default keeps
  following the device until the user picks. **Browser-verified** (Playwright): browser `de-DE` → *Gruppen*,
  `en-US` → *Groups*, `fr-FR` → *Groupes*.
- **MCN-2 · Event section hub instead of router-tabs (P1).** ✅ **Built 2026-09-02, browser-verified;
  device pass pending.** [ADR-0055](../adr/0055-event-navigation-mobile-section-hub.md) **Accepted** and
  realised as [FEAT-009](../features/FEAT-009-event-section-hub.md): the event landing is now a **section
  hub** on all widths — a vertical list of Details / Wishlists / Draw / Share, each a tappable card with an
  icon, a one-line **status** ("Not drawn yet", "{x} of {y}", "Drawn"…) and a chevron — replacing the
  horizontally-scrolling tab strip (`EventNav` retired). Details gained its own `/details` route; each
  section page shows a "← Overview" back-link (`EventBackNav`). Refinement during build: the hub is the
  landing on **all** widths (not "hub-narrow / tabs-wide") because the responsive split forced the landing
  route to be two screens by width — see the ADR. Four new strings in all locales; parity green; Ui suite
  **29/29**. **Browser-verified** (Playwright, 390px, en-US): hub is the landing, no tab strip, four rows
  with status, row → route → back-to-Overview → hub, console clean. **Still to confirm on the Pixel:** the
  hub under a real finger in DE. Deferred: whether the **group level** (`GroupNav` tabs) gets a matching hub.
- **MCN-3 · App chrome → a Settings screen (P2).** ✅ **Built 2026-09-02, browser-verified; device pass
  pending.** New `/settings` page (`Pages/Settings.razor`) gathers **Appearance** (System/Light/Dark),
  **Language**, and the build **version** into one screen, composing the existing shared `ThemeState` /
  `CultureSelector` / `AppVersion`. The **theme switch is removed from the app bar** (native-mobile
  placement; declutters the bar on web too) and the drawer's language/version block is replaced with a
  single **Settings** nav link. One screen for web + mobile (no divergence). Two new strings
  (`SettingsTitle`, `SettingsAbout`) in all locales; parity green; Ui suite **29/29**. **Browser-verified**
  (Playwright, 390px): the drawer Settings link opens `/settings`; Appearance + Language dropdowns + a
  version label render; selecting **Dark** flips the app to dark (body background light→dark); app bar has
  no theme toggle; console clean. **Still to confirm on the Pixel** in DE. The signed-in language
  device-note + Profile link moved onto the Settings page unchanged.
- **MCN-4 · Account entry top-right — keep.** The account/avatar menu top-right is the convention
  (kept). Minor refinement to consider: make the **signed-out** state read clearly as "Sign in" rather
  than an ambiguous avatar. No structural change.

## Mobile native-feel pass — Secret-Santa (2026-09-02)

The MAUI head is at web parity and heading for a backend-free v1
([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)); the maintainer's priority is now
**mobile-first — the phone gets the most attention**. An on-device/on-screen review asked how to make
it feel more native. The governing decision — **one neutral Material design across web + both device
heads, with "native feel" earned through motion, touch ergonomics and OS integration rather than
per-platform (Cupertino) chrome** — is recorded as
[ADR-0054](../adr/0054-mobile-native-feel-strategy.md). The near-term implementable subset (levers 1–4)
is specified as [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md); the rest are stubs below,
prioritized. This extends the [R2-6 mobile touch pass](#ux-round-2--secret-santa-usability-2026-09-01)
and consumes the [mobile brainstorm (2026-09-01)](../brainstorming/2026-09-01-secret-santa-mobile.md).

**In [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md) (levers 1–4 — build first):**

- **MNF-1 · Touch-ergonomics completion (P1).** ✅ **Built 2026-09-02.** A shared **mobile density
  baseline** in `circletools.css` (narrow breakpoint) gives every `.mud-icon-button` a ≥48 px hit area
  (input-adornment icons excepted so dense fields don't grow) and every `.mud-menu-item` ≥48 px — so
  every screen, and every new one, inherits a phone-sized target instead of re-solving it. **Browser-verified**
  (Playwright, 390 px): the Groups kebab overflow button measures **48×48**. The transient
  `Size.Small` buttons in the confirm/edit clusters are left for **MNF-4**, which replaces those inline
  clusters with action sheets.
- **MNF-2 · Navigation clean-up (P1).** ✅ **Built 2026-09-02.** `SecretSantaShell` now shows the bottom
  bar **only when it carries more than the single "Groups" tab** (`_showBottomBar`), and the app-bar
  hamburger is hidden on narrow **only when a bottom bar owns the drawer** — so a signed-out / not-linked
  phone gets one menu entry (the hamburger + drawer) and no degenerate one-item bar, while a linked
  member gets the real tab bar and no redundant hamburger. Content bottom-padding (`ct-has-bottomnav`) is
  now conditional too, so there's no phantom strip without a bar. **Browser-verified** (Playwright,
  390 px, signed out): hamburger present, **no bottom bar**, hamburger opens the drawer, console clean.
  The **signed-in linked-member** path (bottom bar shown, hamburger hidden on narrow) needs the live
  Aspire stack + a linked member — deferred to the [TP-008](../features/TP-008-mobile-native-feel-polish.md)
  live-stack / on-device pass.
- **MNF-3a · Motion — haptics (P1).** ✅ **Built 2026-09-02.** New `IHapticFeedback` seam in
  `Ui.Abstractions` with a MAUI impl over `Microsoft.Maui.Devices.HapticFeedback` (`Click`) and a
  **web no-op** (`BrowserHapticFeedback`) — mirrors `IShareService`
  ([ADR-0028](../adr/0028-browser-platform-adapters-block.md)). A light tap fires on **draw success**
  (`Draw`), **giftee reveal** (`Reveal` + `MyAssignments`), and **pin/delete** (`Groups`). Android
  `VIBRATE` permission added to the manifest. Covered by a bUnit test (reveal taps once; the web impl is
  a no-op) — Ui suite **29/29**. **On-device: confirmed on the Pixel 9 Pro** — the buzz fires on **Auslosen (draw)**
  and **delete confirm** (the two offline-reachable points; pin and the signed-in *my-assignments*
  reveal are gated behind sign-in, and the public reveal-link buzz needs an opened `/r` link).
  **Root cause found on device:** the platform haptic silently no-ops unless `HapticFeedback.Perform`
  is marshalled onto the UI thread — a Blazor event handler is not guaranteed to be on the main looper.
  `MauiHapticFeedback` now wraps it in `MainThread.BeginInvokeOnMainThread`; that is what made the buzz
  fire. (A clean build with the temporary diagnostic logging removed is in the working tree; it is
  behaviourally identical to the confirmed build and ships with the next Android deploy.)
- **MNF-3b · Motion — pull-to-refresh (P1).** ✅ **Built & confirmed on the Pixel 2026-09-02.** A shared
  `PullToRefresh` component + `js/pullToRefresh.js` in `Ui.Shared` (works on both
  heads — both render in a web view): a top-of-page pull past ~72px shows a spinner and fires the existing
  `AutoSyncService.RequestSync()` (a no-op signed out; pulled data arrives via the ordinary
  `SyncCompleted` re-render). The gesture only engages at scroll-top while dragging down, so ordinary
  scrolling is never blocked; `overscroll-behavior-y: contain` suppresses the browser's own reload-on-pull.
  **Browser-verified** (Playwright, 390px touch): the indicator reaches full opacity while pulling,
  **triggers past the threshold**, **ignores a below-threshold pull**, console clean. **On-device (Pixel):**
  the maintainer confirmed the gesture feels good and **does not interfere with normal scrolling** — the
  on-device tuning [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md) flagged is satisfied; no
  threshold/resistance changes needed.
- **MNF-4 · Native interaction patterns (P2).** ✅ **Built 2026-09-02, browser-verified; device pass
  pending.** Two shared bottom-sheet dialogs in `SecretSanta.Ui/Shared` shown via `IDialogService` at
  `DialogPosition.BottomCenter`:
  - **`ConfirmSheet`** replaces the inline `MudAlert` confirmations for the three destructive actions —
    **delete group** (`Groups`), **remove member** (`Members`), **re-draw** (`Draw`) — with a bottom
    sheet carrying a red confirm, instead of a warning box that reflowed the card.
  - **`TextInputSheet` + a FAB** on the **Members** screen: on narrow the inline add-member card is
    hidden and a **`MudFab`** (bottom-right, cleared above the bottom nav / home indicator) opens the
    add-member sheet; the inline card stays on wide.

  **Browser-verified** (Playwright, 390px): the delete sheet and add sheet both open from the bottom, a
  member added via the sheet lands in the roster, the inline add-card is hidden on narrow, console clean.
  **Scope decisions (deliberate):** (1) the **Groups create field is left as-is** — it is the UX-003
  first-run funnel and the onboarding copy in DE/EN/FR literally points "above" at it, so converting it
  to a FAB is its own small UX/copy decision, not this polish pass; a Groups FAB is a candidate follow-up.
  (2) The **kebab overflow menus stay** (they already have 48px targets from W1) — only the destructive
  *confirmations* became sheets, keeping the change focused.
  - **On-device (Pixel) pass — mostly ✅, one bug found & fixed.** Delete/remove/re-draw sheets and the
    add-member sheet all work under a real finger. **Bug (device-only):** the FAB overlapped the last
    member row's kebab, and with too few members to scroll, that member's overflow menu was unreachable.
    **Fixed:** a `.ct-fab-spacer` reserves scrollable room below the list so the FAB always floats over
    empty space and any row can be scrolled clear; the FAB `z-index` was also lowered below MudBlazor's
    popover layer so an open kebab menu is never covered. Browser-verified (Playwright, 6+ members): the
    last row's kebab no longer overlaps the FAB and is clickable. **Confirmed fixed on the Pixel** — the
    bottom member's overflow menu is reachable for both short and long lists.

**Selected earlier, distribution cluster (from the [mobile brainstorm](../brainstorming/2026-09-01-secret-santa-mobile.md)) — build next:**

- **MNF-5 · QR code per reveal link (P2).** ✅ **Built 2026-09-02, browser-verified (DE/EN/FR); device
  pass pending.** Each reveal link on the Share page gained a **QR CODE** button beside Copy/Share that
  opens a centered dialog (`SecretSanta.Ui/Shared/QrDialog.razor` via `IDialogService`) titled with the
  recipient's name, showing a large scannable QR of *their* link plus a one-line hint — so the organizer
  holds up their phone and the recipient scans it, no messaging app needed. The code is drawn by a new
  reusable `QrImage.razor` component over **QRCoder 1.8.0** `PngByteQRCode`: pure managed, no
  System.Drawing and no JS interop, so it renders the same PNG data-URI **offline** in the WASM web head
  and the MAUI Blazor Hybrid webview with **no platform seam** (unlike `IShareService`). ECC level M keeps
  a 2 KB link (the `MaxRecommendedUrlLength` budget) within capacity while short links stay easy to scan;
  the PNG renders `image-rendering: pixelated` on a white ground so it is crisp and scannable in dark
  theme too. **Decision, no ADR:** QRCoder is a small, single-purpose, reversible dependency scoped to
  `SecretSanta.Ui` (kept out of the Main bundle); recorded here and in `Directory.Packages.props` rather
  than as an architecture decision — promote to `Ui.Shared` if a second app needs QR. Three new strings
  (`ShowQr`, `QrHint`, `Close`) in all four resx (de-CH "Schliessen"; FR "vous"); parity green; Ui suite
  **33/33** (added `QrImageTests` + a Share-page QR-button test). **Browser-verified** (Playwright,
  1280px + 390px, DE/EN/FR): create → event → 3 members → draw → Share shows three QR CODE buttons →
  the dialog renders a valid `data:image/png` QR titled per recipient with the localized hint and Close,
  console clean (only the pre-existing `<link rel=preload>` boot warnings). **Still to confirm on the
  Pixel:** the QR under a real camera scan, and the three-button (Copy/QR/Share) row on a phone where the
  Web Share API is present (the headless desktop context hides Share, so only the two-button row was
  captured on narrow). Deferred: a QR-decode assertion (the link → `QrImage.Value` wiring is unit-tested
  and the code renders correctly on screen).
- **MNF-6 · Add-to-calendar for an event (P2).** One tap from the event's date/location/description into
  the phone calendar.
  **✅ Promoted through the process (2026-09-15):** [UC-SS-021](../use-cases/secret-santa/UC-SS-021-add-occasion-to-calendar.md)
  → [FR-044](../requirements/functional/FR-044-add-event-to-device-calendar.md) →
  [FEAT-014](../features/FEAT-014-add-event-to-calendar.md) / [TP-014](../features/TP-014-add-event-to-calendar.md),
  with the calendar platform seam recorded as an
  [ADR-0028 amendment](../adr/0028-browser-platform-adapters-block.md) (no new ADR). **Design calls
  (locked, recorded in the FEAT):** a shared client-side **`.ics` VEVENT** backbone (works on the
  account-free reveal page) delivered per head — **web** opens the `.ics`, **Android** uses
  `Intent.ACTION_INSERT` (reliable; Android has no consistent `text/calendar` handler), **iOS** opens the
  `.ics` (deferred with the iOS head, [ADR-0031](../adr/0031-ios-build-host.md)); an **all-day** entry
  (the date is date-only), **gated on a date being set**; **system-UI only — no silent write, no calendar
  permission** (so no manifest change, unlike haptics' `VIBRATE`); the entry carries name + location +
  notes + **budget range** but **never an assignment** ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md));
  offered to the **organizer** (event Details + hub) **and every recipient** (reveal / my-assignments).
  The entry also carries a **deep link back into the app at the occasion** (in v1, all three storage modes):
  the app's own event route built from the reveal base URL, in the `.ics` `URL` property **and** the
  description, reusing the existing App Link / `DeepLinkService` machinery
  ([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)) — carries no secret (opaque GUIDs), and
  behaves exactly like reveal links today re: build-time origin and the Android chooser-until-real-
  `assetlinks.json`-fingerprint. **✅ Built & browser-verified 2026-09-15** ([FEAT-014](../features/FEAT-014-add-event-to-calendar.md)
  *Done*): the `ICalendarService`/`IcsBuilder` seam + per-head impls + the shared button on hub/reveal/
  my-assignments + the `/groups` App Link intent-filter. Solution builds clean (incl. the MAUI Android head
  0/0); Ui 72/72 + Architecture 28/28. Browser walk confirmed the button appears on the hub and reveal page
  once a date is set (absent without one), non-spoiling, console clean. **✅ On-device confirmed on the Pixel 9
  Pro 2026-09-15:** the button on a dated event hub → `ACTION_INSERT` → Google Calendar opened a **pre-filled
  all-day event** (correct title + date, no off-by-one). Residual: App Link **auto-open** still needs the real
  Play-signing `assetlinks.json` fingerprint (shared pre-go-live item with reveal links).
- **MNF-7 · Share all links for an event at once (P2).** Follow-on to the existing share seam.
  **✅ Promoted through the process (2026-09-15):** [UC-SS-022](../use-cases/secret-santa/UC-SS-022-hand-out-reveal-links.md)
  → [FR-045](../requirements/functional/FR-045-guided-hand-out-of-reveal-links.md) →
  [FEAT-015](../features/FEAT-015-guided-hand-out-of-reveal-links.md) /
  [TP-015](../features/TP-015-guided-hand-out-of-reveal-links.md), reusing the `IShareService` seam
  ([ADR-0028](../adr/0028-browser-platform-adapters-block.md), **no new ADR**). **Design calls (locked):**
  "share all" is a **guided hand-out** — the app walks the whole draw, firing the share sheet (or Copy on
  a desktop browser) for **one person's** link at a time and tracking who is done (**"X of Y done ·
  Z left"**), so nobody is forgotten. It **never** composes a payload with more than one reveal link — the
  naive "all links in one message" is a spoiler leak and is forbidden
  ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)). Handed-out progress is
  **device-local, per event, not synced** (via `IKeyValueStore`, like the recent-groups/culture prefs),
  survives an app reopen, and **resets on a re-draw** (the links change). Per-person Share/Copy/QR stay for
  one-offs; value is concentrated in the account-free / shared-by-link modes (linked members see their own
  assignment in-app). **✅ Built & browser-verified 2026-09-15** ([FEAT-015](../features/FEAT-015-guided-hand-out-of-reveal-links.md)
  *Done*): a **Hand out links** button + guided `HandoutDialog` + `HandoutProgressService` (device-local,
  keyed by a reveal-link hash so a re-draw resets). Builds clean; Ui 72/72. Browser walk confirmed
  "Handed out: 0 / 3" → dialog (one-at-a-time Share/Copy/Mark) → "1 / 3" + chip **persist across a full
  reload**, console clean. **Pending the Pixel:** the guided hand-out under a finger — its OS share sheet reuses
  the already-device-proven `IShareService` (reveal-link share confirmed on the Pixel earlier), so only the
  hand-out flow itself is unverified on device (not re-driven this pass; needs a draw set up on the phone).

**Parked — need their own design/ADR before building:**

- **MNF-8 · Local notifications (P1 value).** Draw-reminder before the event, "event is
  tomorrow", empty-wishlist nudge — the strongest reason to *install* rather than bookmark. Needs a
  scheduling + permission design (and, for "remind everyone", a reachable origin).
  **Motivation reinforced (2026-09-02):** the [competitor scan](../brainstorming/2026-09-02-secret-santa-competitor-research.md)
  found reviewers specifically asking to be **nudged to fill their wish list** — the empty-wishlist reminder is a
  named, cross-market wish, not just an install hook.
  **✅ Promoted through the process (2026-09-15):** [UC-SS-023](../use-cases/secret-santa/UC-SS-023-be-reminded-about-my-occasions.md)
  → [FR-046](../requirements/functional/FR-046-device-local-occasion-reminders.md) →
  [FEAT-016](../features/FEAT-016-device-local-occasion-reminders.md) /
  [TP-016](../features/TP-016-device-local-occasion-reminders.md), governed by a new ADR
  [ADR-0060](../adr/0060-local-notification-scheduling-and-permission.md). **Scope sharpened against the
  origin gate:** v1 ships the **device-local** subset — the three reminders scheduled and fired on the
  user's own device (offline, account-free), a new `INotificationScheduler` platform seam
  ([ADR-0028](../adr/0028-browser-platform-adapters-block.md)) over Plugin.LocalNotification on the device
  head with a **web no-op** (phone-first, like haptics), a **global per-kind opt-in** in Settings (off by
  default), OS permission requested **on first enable**, and **no assignment ever in a reminder body**
  ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)). **Cross-member push**
  ("nudge another member" / "remind everyone") **stays parked** on the reveal-origin/hosting decision
  ([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)) — a future ADR once an origin exists.
  **✅ Built 2026-09-15; on-device delivery pending the Pixel** ([FEAT-016](../features/FEAT-016-device-local-occasion-reminders.md)
  *Done*): the `INotificationScheduler` seam, the pure `ReminderPlanner`, `NotificationPreferences`, the
  `ReminderScheduler` coordinator, the web no-op (`BrowserNotificationScheduler`) and the MAUI impl over
  **Plugin.LocalNotification 12.0.0**, the Settings **Reminders** section, and `POST_NOTIFICATIONS` +
  `RECEIVE_BOOT_COMPLETED`. Solution builds clean incl. the MAUI Android head (0/0, the plugin restores for
  net10-android); Ui 72/72 + Architecture 28/28. Browser-verified the web-visible part (Settings shows the
  "needs the app" note, no switches on web). **✅ On-device confirmed on the Pixel 9 Pro 2026-09-15:** app
  launches (no crash), the plugin's `ScheduledAlarmReceiver` registers, the three switches render, and enabling
  one fired the Android **`POST_NOTIFICATIONS` prompt** → granting left it on (`granted=true`). Residual (needs
  elapsed time): each kind actually firing at its time, and cancel-on-draw/wishlist-filled/delete.
- **MNF-9 · Biometric app lock (P2, parked).** Face/fingerprint on open — assignments are the one secret
  the app keeps, and a phone left unlocked exposes who drew whom. Opt-in; warrants an
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md) privacy review first.
- **MNF-10 · iOS App Links + edge-swipe-back (P2, gated).** iOS deep links are **not wired at all** today
  (Android-only, [ADR-0053](../adr/0053-backend-free-first-mobile-release.md)); iOS also has no
  edge-swipe-back. Bring the iOS head to the Android head's deep-link parity when iOS becomes a target
  ([ADR-0031](../adr/0031-ios-build-host.md)).

**Explicitly declined** ([ADR-0054](../adr/0054-mobile-native-feel-strategy.md), do not reopen without a
new ADR): iOS Cupertino chrome / a platform-adaptive UI kit; Material You dynamic colour (conflicts with
the neutral chrome, [ADR-0040](../adr/0040-ui-component-library-mudblazor.md)); platform-divergent layouts.

## UX round 2 — Secret-Santa usability (2026-09-01)

A second UX pass on the Secret-Santa app, driven by the organizer's own use. Each item was built and
**verified in a real browser** (Playwright/Chromium, DE + EN, account-free flow), console clean.

- **R2-1/R2-2 · Member history: delete keeps past events; rename propagates (P1).** ✅ **Done —
  [ADR-0052](../adr/0052-member-archival-and-permanent-draw-history.md).** Removing a member used to
  delete them from the roster *and* clear every event's draw (and adding a member cleared past draws
  too), so a completed occasion lost both the person and the result. Now removal **archives** the member
  (kept in the aggregate for name resolution and rename propagation, off the active roster and out of
  future draws), and a roster change **flags** each event's draw stale instead of clearing it — draws
  are permanent history. A rename reaches past occasions because names resolve by id through
  `FindMember`. Reveal links for a kept draw still generate (the archived name resolves), with a "roster
  changed" warning on the draw/share screens; the remove confirmation says the person stays in occasions
  already drawn. Additive `MemberState.IsArchived`, opaque to sync (no server change). **Browser-verified
  end to end:** draw 4 people → remove one → the Share page still lists all 4 links (incl. the removed
  person) under the stale warning → rename another → the kept draw's link shows the new name.
- **R2-3 · A new group starts with no event (P2).** ✅ **Done — [ADR-0042 amendment](../adr/0042-events-as-first-class-entities.md).**
  A new group no longer auto-creates an unnamed event; the group editor opens on an empty occasions list
  with a hint, and the user creates the first occasion. `Group.RemoveEvent` may now go to zero;
  create-from-circle is eventless too. A pre-events snapshot (null `Events`) still migrates; an empty
  list is a real state.
- **R2-4 · Default event name "Christmas {year}" (P2).** ✅ **Done.** The add-event field is pre-filled
  with a localized default — *Weihnachten 2026* / *Christmas 2026* / *Noël 2026* (`DefaultEventNameFormat`
  + the current year) — which the user can accept or edit.
- **R2-5 · Nav quick-access submenu (groups → recent events) (P2).** ✅ **Done.** The drawer now shows,
  under *Groups*, the recently-opened (and still-visible) groups — each an expandable `MudNavGroup`
  listing its most recent occasions (dated newest-first, then undated by last-opened) plus a **Mehr…**
  link into the group — and, when more groups exist than are shown, an **Alle Gruppen** link to the full
  list. Recency is a device-local `RecentAccessService` over the plain `IKeyValueStore` (like the culture
  choice, not synced); `GroupNav`/`EventNav` record opens, and the shell refreshes the section on
  navigation, sign-in change and sync. The current group's submenu auto-expands. Event ordering needed a
  new `EventSummaryDto.Date`. **Browser-verified** (DE): the group and its two occasions appear nested in
  the drawer, current event highlighted.
- **R2-1b · A past occasion's stale draw is not flagged "re-draw needed" (refinement of R2-1/2).** ✅
  **Done.** After removing a member, a *past* occasion (its date has gone by) was still showing
  "re-draw needed", which is noise — a finished exchange is history, not something to re-draw. The read
  model ([ADR-0052](../adr/0052-member-archival-and-permanent-draw-history.md)) now suppresses the stale
  flag once the occasion's date is in the past (undated/upcoming still show it); the query handlers take a
  `TimeProvider`. Applies everywhere the flag surfaces (badges, draw/share notices). Browser-verified: a
  2024 occasion reads "Ausgelost", a 2027 occasion "Neu auslosen nötig".
- **R2-6 · Broader mobile/UX polish (P3, open).** Ongoing: make the app easy to understand and quick to
  reach the common things, on web and especially on phones. Candidates to be gathered as they surface.
  - **Mobile touch/layout pass — ✅ done 2026-09-01** (alongside making the Android head installable on a
    physical phone). From an on-screen inventory of every Secret-Santa screen: primary row-action buttons
    no longer use `Size.Small` (~30px → default, ~44px touch target); crowded per-row button clusters fold
    into an overflow **kebab menu** keeping one primary action visible (`Members` — was up to 5 inline
    buttons; `Groups`; `GroupEditor`); non-wrapping rows gained `flex-wrap` (`Share` card header,
    `Exclusions` list row); the app bar drops the cross-app switcher on narrow viewports
    (`SecretSantaShell`); the budget fields get explicit widths + hidden spinners (`EventEditor`); the
    from-circle person checkboxes lose `Dense` for a bigger hit area. A new **`MoreActions`** string in all
    four resx. Build + Ui tests green. **Still to verify on the Pixel:** the Android top safe-area /
    status-bar behaviour (currently tinted via `colorPrimaryDark`, neutral per ADR-0040 by design) and the
    kebab/tap targets under a real finger.
  - **Installable on a physical Android phone — ✅ done 2026-09-01.** `RevealBaseUrl` in `MauiProgram.cs`
    now points at the dev machine's **LAN address** (`http://192.168.100.122:5081`) so a link shared from
    the phone opens on any device on the home Wi-Fi (run the WebApp with `--urls http://0.0.0.0:5081`).
    USB-deploy runbook added to [device testing](../deployment/device-testing.md). **Known limitation:**
    account sign-in/sync over the LAN needs a trusted cert / real origin — deferred with hosting; the
    offline + share flow is unaffected.
  - **Backend-free v1 reveal path — ✅ implemented 2026-09-01 ([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)).**
    `RevealBaseUrl` (and `ApiBaseUrl`) are now **build-time settings** on the MAUI head (Release defaults to
    the production origins; `release-mobile.yml` passes them), so a store build no longer ships a dev URL.
    **Android App Links** for `secretsanta.circletools.ch/r` open the installed app and render the reveal
    **offline from the baked-in UI** (`MainActivity` intent-filter + `DeepLinkService` + `DeepLinkListener`,
    `LaunchMode=SingleTask`); a recipient without the app falls back to the static page. **Verified on the
    Pixel 9 Pro:** a reveal App Link opened the app and showed "Hallo Katy! … Du bist Wichtel für: Vreni",
    fully offline. `assetlinks.json` ships in the web app with a **placeholder** fingerprint —
    **still to do before go-live:** replace it with the real Play App Signing SHA-256 (see `deploy/README.md`)
    so links auto-open silently instead of via the chooser.
  - More mobile-feature candidates gathered in the
    [mobile brainstorm (2026-09-01)](../brainstorming/2026-09-01-secret-santa-mobile.md): QR codes,
    add-to-calendar, share-all, local notifications, biometric lock.

## UX & platform-parity review — 2026-08-12

A usability pass taken after M4 closed, to answer two questions directly: is the suite built on
**both** web and mobile, and is it **user-friendly**? The offline Secret-Santa journey was driven in a
**real browser** (headless Chromium via Playwright, DE/EN/FR: create group → budget/event → members +
wishlists → exclusion → draw → share → open a reveal link), per the `CLAUDE.md` "verify UI in a real
browser" rule. **The core offline flow is polished and correct** — all three languages render cleanly,
spoiler protection is thoughtful, the reveal page separates `Event: 12/24/2026, Zürich` correctly (the
old separator defect is gone), empty wishlists read "No wishlist provided", and the whole run produced
**zero console errors/warnings**. The findings below are the gaps that pass alongside that.

**Scope caveat:** only the **account-free** flow was browser-audited. The **signed-in v2 flows**
(sign-in, sync, claim, circles, linking) were **not** driven live — that needs the Main service +
Postgres + Mailpit up — so the signed-in items below are from a **code + docs review** and still want a
live two-account pass to confirm.

- **UX-001 · Mobile is a milestone behind — decide its scope (P1).** ✅ **Resolved 2026-08-13 —
  decision: bring the MAUI head to web parity.** The `CircleTools.SecretSanta.App` head now composes the
  same shared feature stack the browser WebApp does, with only the platform seams swapped for their
  device implementations (ADR-0025/0028): it references `CircleTools.ApiClient` and `CircleTools.Ui.Account`,
  and `MauiProgram.cs` registers the two keyed account `HttpClient`s (plain handler, no
  browser-credentials handler), the `M3` sync engine (`AddSecretSantaSyncEngine`), the `ISyncApi` /
  `ILinkingApi` / `ICircleDirectoryApi` ports over the authorized client, `AddAccountUi` under
  `secretsanta.refreshToken`, and `AddSecretSantaUi`. The device seams are `FileKeyValueStore` (already
  present) for storage and a new `MauiPasskeyCeremony`. The root `Routes` component now wraps
  `CascadingAuthenticationState` + `CultureBoundary` and, before the first page renders, runs the two
  startup steps the WebApp runs before `RunAsync` — `ICultureProvider.InitializeAsync` and
  `AccountSession.RestoreAsync` — which is where the culture/session init the old comment said "belongs
  with adding the picker" now lives. The `NavMenu` gained the account-aware section (sign-in / signed-in
  identity, Sync now, Claim, My links/assignments/wishlists, sign out) **and the language picker**
  (`CultureSelector`), so mobile is DE/EN/FR, not `de-CH`-only. **Verified:** both target frameworks
  build clean (`net10.0-windows`, `net10.0-android`, 0/0), and the Windows head was run natively — it
  boots with no composition/startup error and renders the parity nav (the **Anmelden** link and the
  **Deutsch** language picker) over the offline flow.
  - **What is still deferred, with reasons (all gated on the brand/domain, not on this work):**
    **cross-client passkeys** — the WebAuthn RP-ID is the registrable suite domain (FR-010/011), so
    `MauiPasskeyCeremony` reports "unsupported" and the account pages already show the right message;
    password sign-in works fully. **The production API origin** — `MauiProgram.ApiBaseUrl` is a dev
    default (`10.0.2.2:7080` for the Android emulator's host-loopback); the literal origin lands with
    the domain. The **`ApplicationId`** is now set to the final `ch.circletools.secretsanta` (2026-08-13,
    brand chosen); the **icon/splash** are still template art, to be replaced before first submission.
    There is still **no `Main` mobile head** — out of scope for the Secret-Santa app's parity.
  - Live sign-in/sync from a device against a running Main service was **not** driven this pass (it
    needs Main + Postgres + Mailpit reachable from the emulator/device); the account UI is the same
    shared `Ui.Account` components already browser-verified end-to-end on web across M1–M4.
- **UX-002 · Account-only actions are offered to account-free users (P1, browser-verified).** ✅
  **Fixed 2026-08-13.** On the members page **while signed out**, every member showed a **"Link…"**
  button; clicking it opened an email field + "Send" that calls the authorized server API — a dead end
  for someone with no account. `Members.razor` rendered the link/unlink controls with no `AuthorizeView`.
  The link/unlink cluster is now wrapped in `<AuthorizeView>` so it appears only when signed in; Edit
  and Remove (local, offline actions) stay for everyone. Verified in a real browser (signed out: no Link
  button, Edit/Remove present, console clean).
- **UX-003 · First-run / empty states are bare (P2, browser-verified).** ✅ **Fixed 2026-09-01.**
  Signed out, `/` was a title, an input, and the literal line "No groups yet." — no explanation of what
  the app does, no cue to sign in for cross-device sync. The empty Groups state is now an
  `AuthorizeView`-split onboarding block (`Groups.razor`): **signed out**, a gift-icon card with a
  headline (per app name — *Wichteln* / *Secret Santa* / *Père Noël secret*), a one-paragraph explainer
  of the whole flow that nudges to the create field above, a divider, and the **account sell at the
  point of choice** (sync across devices, see your own assignment, own your wishlist) with a **Sign in**
  button to `/account/login`; **signed in**, a shorter hint pointing at the create field and the
  from-circle shortcut. Five new strings (`EmptyState*`) added to all three cultures + neutral (the dead
  `NoGroups` key it replaces was removed); DE `du` / FR `vous` per ADR-0032; no counts, so no
  singular-form concern. Parity test green, project builds clean. **Verified in a real browser**
  (Playwright/Chromium, signed-out first-run in DE/EN/FR): the card renders correctly in all three
  languages, the create field stays the primary action above, and the change adds no console
  errors/warnings.
- **UX-004 · The share screen dumps raw encoded URLs (P2, browser-verified).** ✅ **Partly fixed
  2026-08-13.** Each person's reveal link was printed verbatim as a ~197-character base64 blob. The
  "Copy" button is the right primary action, but the raw string is noise and can alarm users. The raw
  URL is now collapsed behind a native `<details>` disclosure ("Show link" / "Link anzeigen" /
  "Afficher le lien"), so Copy is the clear primary action and the URL stays available for anyone who
  wants to inspect or paste it. Verified in a real browser (collapsed by default, expands on click,
  console clean).
  - **Native share sheet — ✅ done 2026-09-01.** Each link now has a **Share** button beside Copy that
    opens the OS share sheet (WhatsApp / Messages / e-mail), carrying the reveal link. It is a new
    platform seam, `IShareService` ([ADR-0028](../adr/0028-browser-platform-adapters-block.md) amendment):
    a browser impl over the Web Share API (`SecretSanta.WebApp/Interop/BrowserShareService.cs` +
    `wwwroot/js/share.js`) and a device impl over MAUI's `Share` (`App/Platform/MauiShareService.cs`).
    The button self-hides where the platform has no share sheet (most desktop browsers), so Copy stays
    the fallback. Covered by `SharePageTests` (button gates on support; click hands the link to the
    seam). WebApp + Android head build clean; Ui tests 28/28. **On-device (Pixel) confirmation pending**
    the USB-deploy pass.
  - **Still open (deferred):** a **QR code** per person (see the
    [mobile brainstorm](../brainstorming/2026-09-01-secret-santa-mobile.md)).
- **UX-005 · Signed-in navigation shows dead ends and a reverting language picker (P2).** ✅ **Fixed
  2026-09-01** (the **Claim** sub-finding was already closed earlier by ADR-0044, which retired the
  `/claim` screen for the silent `AutoSyncService` + `SignInReconciler` prompt). Two changes to
  `SecretSantaShell.razor`, both verified live:
  - **Contextual personal nav.** *My links / My assignments / My wishlists* were shown to **every**
    signed-in user, inert unless they were a linked member. They are now gated: **My links** appears
    only when a member-link request is actually waiting (`ILinkingApi.GetPendingAsync` > 0), and **My
    assignments / My wishlists** only for a linked member (`GetMyMembershipsAsync` > 0). The gate is
    **server-authoritative on purpose** — a member of *someone else's* group has no local group document
    to read `Member.UserId` from, so a local check would wrongly hide the screens from exactly the people
    they are for. The two gates recompute when the account changes and after each background sync (so
    they appear on their own once a create/self-link or an accepted link lands), and stay hidden when the
    server is unreachable, reappearing on the next sync. The mobile bottom bar gates its two personal
    tabs the same way.
  - **Language picker — warn (not unify).** The nav `CultureSelector` is device-local; for a signed-in
    user the account language wins and reverts the pick on the next reconcile
    ([ADR-0035](../adr/0035-profile-sync-seam.md)). Chosen fix: when signed in, a caption under the
    picker says so and links to the **Profile** page (the durable, synced path). Kept the shared
    `Ui.Shared/CultureSelector` device-local rather than wiring it to `IProfileWriter`, so the shared
    component's behavior is unchanged across apps; persisting-from-nav ("unify") is a recorded follow-up
    (would add an optional persistence callback to `CultureSelector`). Two new `NavLanguage*` strings in
    all three cultures + neutral.
  - **Verified against the live Aspire stack** (Playwright/Chromium, real register→verify→sign-in via
    the API + Mailpit): signed-in-but-not-linked hides all three personal links and shows the device
    note; after creating a group (FR-032 self-link) *My assignments* + *My wishlists* appear on their own
    via the post-create sync while *My links* stays hidden; the device note reads correctly in **DE / EN
    / FR** (the account language winning over the local pick was observed directly — a live demonstration
    of the very revert the note explains). Console clean; 8/8 assertions green.
  - **Follow-up (optional):** a bUnit regression test for the two nav gates (the shell needs MudBlazor
    providers + fakes for `ILinkingApi`/`AutoSyncService`/`ISender`, so it was left as a follow-up rather
    than built here); and the "unify" language-picker option if device-local-plus-warn proves too subtle
    in use.
- **UX-006 · Sync is manual (P2).** ✅ **Resolved by ADR-0044 (2026-08-17).** The manual "Sync now"
  button is gone; `AutoSyncService` runs a silent, debounced sync on four triggers — app start (restored
  session), reconnect, post-edit, and the sign-in reconciler — so device B converges without anyone
  pressing anything, and the shell shows only a passive last-outcome status line. The parked
  continuous-sync decision ([ADR-0016](../adr/0016-sync-and-conflict-resolution.md)) was **not**
  reopened. A pure *pull-on-tab-focus* refresh for a long-idle open tab is the only sliver left, and it
  is marginal; parked with the continuous-sync decision.
- **UX-007 · Group editor has three separate Save buttons and one shared status line (P3).** ✅
  **Obsolete after ADR-0042 (events as first-class).** The editor was restructured to **Name + Events**;
  budget and date/location moved into the **per-event** editor, so the "three saves under one ambiguous
  status line" arrangement no longer exists. `GroupEditor.razor` now has one name save plus per-event
  add/rename/remove, each with its own inline control. Nothing to do.

These are review findings, not yet promoted; the P1/P2 items are the candidates to turn into use
cases/requirements (or ADRs, for the mobile-scope decision) if taken forward.

## Ideas

### IDEA-003 — Wishlists can be switched off per event · _status: idea (2026-10-29), after the closed test_
- **Problem:** not every group uses [wishlists](../glossary/wishlist.md) — some draw for a surprise gift,
  a fixed theme or a Schrottwichteln-style exchange (CR-4). Today every UI still offers the wishlist editors and
  counts, which reads as a missing step ("Wishlists 2/5").
- **Idea (owner, 2026-10-29):** an [organizer](../glossary/organizer.md) setting **per
  [event](../glossary/event.md)** — *Wishlists on/off*, **on by default**. Off hides every wishlist edit part
  in all UIs (organizer views, participant page, reveal link, web and app) for that event.
- **Fits the model:** a wishlist already belongs to a member *within one event* (ADR-0042), so the switch
  is an event property, not a group one.
- **Not a closed-test slice:** it changes the event (domain + contracts + server + local store + sync) and
  every wishlist surface, so it is a feature, not a small fix (see PR-12).
- **Open questions:**
  - Switching off: keep existing entries hidden (and back on switch-on) or delete them? Leaning *keep*.
  - Old app versions ignore an unknown field and would keep showing the editors — does the server refuse
    wishlist writes for an event that has them off, and does that need an ADR (sync contract change)?
  - Also hide wishlist mentions in share texts, emails, reminders and the group card counts?
  - Does the setting carry forward to a recurring event (CR-1)?
- **Next:** brainstorming → use case + FR → ADR if the contract question needs one.

### IDEA-002 — Circle (shared people across the suite) · _status: built & closed (M2, 2026-07-30) — M4 consumes it_
- **Problem:** every app re-asks who your people are. A family or friend-group has no lasting
  existence in the suite — each Secret-Santa group is typed in from scratch, and nobody but the
  organizer can contribute.
- **Target users:** families and friend-groups who use more than one app, or the same app repeatedly.
- **Value:** one standing set of people, reusable across apps and years. A Secret-Santa group can be
  created **from** a circle instead of retyped; linked people see their own assignment on their own
  device and keep their own [wishlist](../glossary/wishlist.md).
- **Scope (built):** create a circle and label it; roles (owner/admin/member);
  invitations by email that never leak whether an address has an account; managed members without an
  account (children); a user in several circles.
- **Naming:** the canonical code name is **`Circle`**; the UI label is chosen per circle and defaults
  to "Family". `Group` remains the Secret-Santa term — see [Circle](../glossary/circle.md).
- **Traceability:** [brainstorming v2](../brainstorming/2026-07-24-accounts-families-sync.md)
  (decisions D2, D5, D6, D7) → glossary [Circle](../glossary/circle.md),
  [Circle membership](../glossary/circle-membership.md), [Invitation](../glossary/invitation.md),
  [Managed member](../glossary/managed-member.md).
- **Architecture decided (2026-07-27):** [ADR-0018](../adr/0018-circle-and-sharing-model.md) — the
  `Circle` aggregate, roles, enumeration-resistant invitations, managed members, and the
  snapshot-at-draw rule; [ADR-0019](../adr/0019-member-user-linking.md) for member linking.
- **Use cases & requirements written (2026-07-29):**
  [UC-MAIN-008..014](../use-cases/index.md) — create a circle · invite a person · accept/decline ·
  managed members · roles, removal & ownership transfer · leave · delete — and
  [FR-016..022](../requirements/index.md) plus
  [NFR-006](../requirements/non-functional/NFR-006-circle-authorization.md) (circle data
  authorization). Four points ADR-0018 left to this milestone are now decided in the specs, with the
  reasoning attached: **transfer of ownership is immediate, not an offer** (a pending transfer is the
  ownerless limbo the model forbids); **role changes are owner-only** and an admin may not remove
  another admin (or one admin can manufacture a majority); **membership is granted only to a verified
  owner of the invited address**, which closes the forwarded-invitation hole without a second secret;
  and **declining must be possible without an account**, because otherwise "declined" in the inviter's
  list would itself prove the address has one. Two M1 loose ends were closed on the way —
  [FR-015](../requirements/functional/FR-015-delete-account.md)'s sole-owner guard now has
  [FR-022](../requirements/functional/FR-022-delete-circle.md) behind it, and managed members are
  confirmed to be circle data that survives their creator.
- **Architecture follow-up closed (2026-07-29):** [arc42 ch. 5](../arc42/05-building-block-view.md)
  now describes the **Circles** context — `Circles.Contracts` / `Circles.Core` / `Circles.Repository`,
  hosted by the Main API host, with its own Postgres schema and no foreign key to Identity. It sits in
  `BuildingBlocks/` for the same reason Identity does (every app reuses it) but gets the full
  Core/Repository split, because unlike Identity it has invariants that must hold transactionally.
  [ch. 8 §8.10](../arc42/08-crosscutting-concepts.md) gained the authorization and invitation
  mechanics. This closes the last follow-up ADR-0018 recorded; **no new ADR was needed** — the
  packaging follows M1's precedent rather than deciding anything new.
- **Feature spec & test plan written (2026-07-29):** [FEAT-003](../features/FEAT-003-m2-circles.md) and
  [TP-003](../features/TP-003-m2-circles.md). The spec fixes every number the
  requirements deferred to it — **14-day invitations** (a resend replaces the live link rather than
  adding one), rate limits keyed on the **inviter**, the **recipient address** (the only one that
  protects the third party) and the **circle**, a **7-day silent** cooldown after a decline, and caps
  of 100 people per circle / 20 owned circles — and answers the four product questions: no acceptance
  or removal notifications (M2 has no notification channel and this is not the place to invent one),
  managed members *do* appear in a member's export because it mirrors in-app visibility, and circle
  deletion asks the owner to **type the circle's name** rather than their password, because the threat
  here is the misclick, not a stolen session. Two design points are worth knowing before implementing:
  memberships, invitations and decline records live **inside the `Circle` aggregate** (every invariant
  is circle-scoped), and there is **exactly one accept path** for both invitation branches, so the
  "verified address must match" rule lives in one place.
- **Domain & application built (2026-07-29):** `CircleTools.Circles.Contracts` and
  `CircleTools.Circles.Core` exist — the `Circle` aggregate with memberships, invitations and decline
  records inside it, every invariant and cap, and 23 command/query handlers, with **68 unit tests**
  and four architecture fitness functions green (355 tests across 11 projects overall). Four ports
  are declared and unimplemented until the host slice: `ICircleRepository`, `IAccountDirectory`,
  `IInvitationMailer`, `IInvitationRateLimiter`. Two spec corrections were made and recorded in
  [FEAT-003](../features/FEAT-003-m2-circles.md): the "no email field anywhere in Contracts" rule
  became "no DTO describing a **person** carries one", with a two-name exemption list under test
  (an invite request must carry an address, and a pending-invitation list is unusable without one);
  and the invitation mail leaves through an `IInvitationMailer` **port** rather than `Circles.Core`
  referencing `CircleTools.Infrastructure`, which would have pulled MailKit into the domain project.
- **Built and closed (2026-07-30):** the rest of the slice landed — `CircleTools.Circles.Repository`
  (the `circles` schema and its migration, `xmin` concurrency marked on the circle row so the token
  covers child changes, and `IInvitationRateLimiter` over rolling-window `invitation_attempts` rows),
  `/api/v1/circles` + `/api/v1/invitations` behind `CircleRoleFilter`, `OutboxInvitationMailer`, both
  account-deletion seams (`SoleOwnerCircleGuard` for M1's `IAccountDeletionGuard`, and the new
  `IAccountDeletionCleanup`), the DE/EN/FR invitation mail, and four `Main.Ui` pages. **537 tests
  green across 12 projects.** Two further spec corrections are recorded in
  [FEAT-003](../features/FEAT-003-m2-circles.md): `IInvitationRateLimiter` belongs to
  `Circles.Repository`, not the host (its counters are `circles` tables), and the in-app invitation
  path needed routes of its own, because `CircleRoleFilter` answers `404` to exactly the people those
  routes exist for.
- **What closing it found, and the lesson:** the four checks below were missing while everything
  around them looked finished, and a green suite said nothing about any of them.
  - the NFR-006 endpoint-guard fitness function — `CircleRoleRequirement` had been built as endpoint
    *metadata* specifically so a test could read it back, with a comment saying so, and no test did;
  - the "role matrix" was three spot checks, not 14 routes × 5 caller kinds;
  - revoking an invitation had no functional test, only its route filter;
  - **two acceptance criteria were unmet in the product**: FR-021's leave confirmation never said the
    account and the user's own Secret-Santa data survive, and FR-022's delete confirmation never
    named the circle, counted who loses access, or named the managed members. Both were found by
    reading the criteria against the screen rather than against the tests.

  All four were fixed at close. The transferable lesson for **M3**: a test-plan row is not evidence,
  and a fitness function that is designed but never written leaves its design comment behind as false
  reassurance.
- **Carried, with reasons:** deliverability of the invitation (FR-017) waits on the
  transactional-email provider — invitations make that decision onboarding-critical, not merely
  account-critical; FR-019's and FR-022's Secret-Santa halves wait on **M4**, when something first
  copies a circle into a group. The two confirmation screens changed at close still want a look in a
  real browser.

### IDEA-001 — Secret-Santa app (first app) · _status: MVP defined_
- **Problem:** Organizing a Secret-Santa (Wichteln) by hand is fiddly — drawing names fairly,
  honoring "who can't draw whom" (e.g. partners), and doing it across a group on different phones.
- **Target users:** families and friend-groups, Swiss-first.
- **Value:** a delightful, private, offline-capable way to run a gift exchange with no account or backend.
- **MVP scope (defined):** create & configure a group (name, budget, event details) → add members
  with wishlists → define exclusions → run a fair draw (honors exclusions, no reciprocal pairs, no
  repeat of the previous draw via draw history, graceful failure) → generate one **self-contained
  encoded reveal link** per member (recipient + wishlist + budget + event) → each member opens their
  link and the page decodes it fully client-side. Fully offline & account-free; privacy (the link is
  the secret); multilingual DE/EN/FR (`de-CH` default). Out of scope: accounts, sync, server-stored
  assignments, notifications, multiple simultaneous organizers.
- **Traceability:** [brainstorming](../brainstorming/2026-07-24-secret-santa-scope.md) →
  [use cases UC-SS-001..006](../use-cases/index.md) → [requirements FR-001..006, NFR-001..003](../requirements/index.md).
- **Post-MVP (v2 — designed 2026-07-27):** add **central login** in the CircleTools **Main** shell,
  **App↔backend sync**, and the ability to **link a Secret-Santa `Member` to a
  [User](../glossary/user.md) account** — so members receive their assignment on their own device,
  keep their own wishlist, and identity is shared across apps. Scope and the eight pivotal decisions
  are settled in [brainstorming v2](../brainstorming/2026-07-24-accounts-families-sync.md);
  sliced as **M1 Identity · M2 Circle · M3 Sync · M4 Linking**.
- **Privacy change to be aware of (D1):** in v2, a signed-in member's assignment **is stored
  server-side** and protected by authorization rather than by absence. The account-free flow is
  unchanged and still stores nothing.
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) has been amended, and
  [ADR-0020](../adr/0020-server-side-assignment-privacy.md) partly supersedes
  [ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md).
- **Architecture decided (2026-07-27):** [ADR-0016](../adr/0016-sync-and-conflict-resolution.md)…
  [ADR-0021](../adr/0021-web-auth-token-model.md) cover sync, the account claim, the circle model,
  Member↔User linking, assignment privacy and the web token model.
- **M3 specified (2026-07-30):** the sync and claim slice has its use cases and requirements —
  [UC-SS-007](../use-cases/secret-santa/UC-SS-007-claim-local-groups.md) take my existing groups into
  my account · [UC-SS-008](../use-cases/secret-santa/UC-SS-008-remove-group-from-account.md) take one
  back out · [UC-MAIN-015](../use-cases/main/UC-MAIN-015-sync-across-devices.md) ·
  [UC-MAIN-016](../use-cases/main/UC-MAIN-016-work-offline-and-reconcile.md) → FR-023..026 and
  [NFR-008](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md). The
  privacy consequence is worth stating where the Secret-Santa idea lives: **a claimed group carries
  its draw**, so M3 — not M4 — is when assignments first reach the server, and the claim screen has to
  say so before the upload rather than in a settings page afterwards.
- **M3 unblocked & designed (2026-08-03):** the one open decision that blocked the build is closed —
  [ADR-0033](../adr/0033-which-aggregates-sync.md) narrows [ADR-0016](../adr/0016-sync-and-conflict-resolution.md)
  so the Secret-Santa `Group` and profile preferences sync but the `Circle` does not (server-authoritative,
  online-only), and it parks an offline circle *read* cache as a separate, later decision. The slice now
  has its feature spec and test plan: [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md) (*Draft*) fixes
  the numbers the requirements deferred — the `POST /api/v1/sync` delta shape, the per-account cursor,
  tombstone/un-claim-marker retention below the slowest device's cursor, claim idempotency via
  client-minted ids, and profile preferences as a per-account document — and
  [TP-004](../features/TP-004-m3-sync-and-claim.md) (*Draft*) centres on a two-client harness for
  NFR-008's two silent-failure properties (convergence, data safety) and on proving the LWW collision
  hook actually emits telemetry rather than being a stub. The same deployment ADRs
  gate *shipping* but not *building* ([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)).
- **M3 built & closed (2026-08-12):** the whole slice is built and
  [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md) is *Done* / [TP-004](../features/TP-004-m3-sync-and-claim.md)
  *Reviewed*; FR-023..026 and NFR-008 are `Implemented`, and **FR-012's carried offline-language
  criterion closed with them**. The verification `CLAUDE.md` insists on — a **two-device, DE/EN/FR
  browser walkthrough against a live Main service** — was completed and is green (Playwright over real
  Chrome, two isolated contexts as two devices, 37 assertions): convergence, data safety, LWW with a
  reported collision, delete-without-resurrection, un-claim-as-marker, the FR-012 cross-device language
  change, the claim-list singular count in all three languages, and the offline circle notice, all
  confirmed on screen with a clean console. **No product defects were found.** Two behaviour questions
  were recorded, not fixed: first sign-in surfaces the claim screen via a nav link rather than an
  automatic prompt; and the **signed-in nav language picker is local-only** and is reverted to the
  profile preference on the next reconcile (the Profile page is the synced path). What is carried, not
  closed: the same-as-M1 **CI pipeline** ("CI green" → milestone 7), and a handful of automated tests
  the reconciliation records (`ClaimFlowTests`/`ClaimIdempotencyTests`, `SyncAuthTests`, the perf gate,
  account-deletion cascade, bUnit page renders).
- **M4 built and closed (2026-08-12):** the linking slice is built, verified, and browser-walked —
  [FEAT-005](../features/FEAT-005-m4-linking.md) is *Done* / [TP-005](../features/TP-005-m4-linking.md)
  *Reviewed*, FR-027..030 *Implemented*. Backend: the `Member` domain change (`UserId?` +
  `OriginCirclePersonId?`, and a link that does **not** invalidate a completed draw), create-from-circle
  composition with the duplicate-account guard, the new **`linking` Postgres schema** (member-links,
  the ADR-0020 assignment projection, self-wishlists, resolutions) with its migration, and the whole
  authorized surface on the Main host — reusing the M2 invitation machinery for the member-link, and
  the deny-not-filter rule realised as **403 for a stranger vs 404 for a known member with no draw**
  (unlink *closes* the member-link row rather than deleting it, so a former member degrades gracefully).
  The account-deletion cascade is a second `IAccountDeletionCleanup`. Client: the four Blazor flows
  (create-from-circle picker, organizer link/unlink on the members page, the anti-spoiler my-assignment
  view, the my-wishlist editor with the reveal-link caveat), the resolution applier and assignment
  publisher wired into the `SyncCoordinator`, and 60 DE/EN/FR strings. **Verified by automated tests:**
  17 two-account integration tests on real Postgres, 28 architecture fitness (incl. Core ⊥ Circles),
  16 Ui component/unit, localization parity, and the M3 suite still green (221) with no regression.
  **One decision recorded:** the circle read for the picker exposes an account-backed member's opaque
  account id to fellow circle members (never an address) so the group can link them — the circle's own
  trust boundary (ADR-0018); NFR-005's address-account oracle is untouched.
  - **The browser walkthrough was run and is green** (2026-08-12): two accounts against a live Main
    service (Postgres + Mailpit + the WASM app), Playwright over real Chromium, console clean in all
    three languages — create-from-circle (the *"3 people"* count checked as "3 Personen" / "3 personnes",
    no `"1 Personen"` bug), linked badges on the members page, draw + sync, the linked member's own
    assignment behind the anti-spoiler step, and a wishlist edit that **persisted across reload**.
  - **It found a real defect a green suite had missed** — create-from-circle set `Member.UserId`
    locally but never registered the **server-side** member-link, so a person linked that way could see
    their assignment but **could not own their wishlist**. Fixed by `POST /api/v1/member-links/circle`
    (the host **verifies shared-circle membership** before recording the link) plus the client call from
    `CreateFromCircleService`; now covered by `LinkingCircleTests`. The lesson for the next milestone,
    written down: **every test set up its links through the accept flow**, so the one path that did not
    was the one that broke — and only the browser showed it. One recorded decision came with the fix:
    `CirclePersonResponse` exposes an **opaque account id** (never an address) to fellow circle members
    so a group made from the circle can link them (ADR-0018 trust boundary; NFR-005 untouched).
- **Next:** M4 is the last v2 milestone; the carried cross-cutting items remain (the milestone-7 **CI
  pipeline** for "CI green", and *shipping* still waits on the transactional-email and hosting decisions,
  now sharpened again by the member-link mail — none gate the build).

### IDEA-000 — CircleTools shell (Main app-type) · _status: in progress_
- **Problem:** apps need shared branding, one login, shared config, and a dashboard.
- **Role:** hosts **central login / accounts**, shared config, and the **sync backbone** that
  connects the Secret-Santa app (and future apps) to the backend — including linking in-app "persons"
  (e.g. Secret-Santa `Member`s) to [User](../glossary/user.md) accounts.
- **Decided (2026-07-27):** identity stack per
  [ADR-0009](../adr/0009-authentication-and-passkeys.md) (ASP.NET Core Identity + passkeys), with
  **bearer tokens in the WASM client** (D8 — no BFF, so
  [ADR-0005](../adr/0005-web-hosting-render-model.md) stays intact); local-first
  [Sync](../glossary/sync.md) with per-aggregate last-write-wins; anonymous→account claim by
  **per-group choice** on first login (D4).
- **Traceability:** [brainstorming v2](../brainstorming/2026-07-24-accounts-families-sync.md) →
  glossary [User](../glossary/user.md), [Sync](../glossary/sync.md) →
  ADRs [0016](../adr/0016-sync-and-conflict-resolution.md),
  [0017](../adr/0017-anonymous-to-account-claim.md), [0021](../adr/0021-web-auth-token-model.md).
- **Use cases written (2026-07-27):** [UC-MAIN-001..007](../use-cases/index.md) cover the **M1 —
  Identity** slice — create an account, sign in with email+password, register a passkey, sign in with
  a passkey, profile & preferred language, delete account & export data, reset a forgotten password.
- **Open questions resolved (2026-07-27):** unverified accounts may sign in but get nothing
  account-scoped (purged after 30 days); password policy is NIST-style (12+ chars, no composition
  rules, breach-list check); sessions are 15-min access / 30-day rotating refresh, persistent, with no
  "remember me" tick; passkey **RP-ID is the registrable suite domain** so one passkey spans all app
  subdomains; display names are not unique; avatar deferred to M2. Deletion & export became
  [ADR-0022](../adr/0022-account-deletion-and-data-export.md) — immediate hard delete, export mirrors
  in-app visibility.
- **Requirements written (2026-07-27):** [FR-007..FR-015](../requirements/index.md) plus
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md) (data protection
  & retention) and [NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md)
  (account-enumeration resistance). Glossary gained [Passkey](../glossary/passkey.md).
- **Feature spec written (2026-07-27):** [FEAT-002](../features/FEAT-002-m1-identity.md). It creates
  two building blocks arc42 ch.5 already planned but that do not exist yet — `CircleTools.Identity`
  (suite-wide, per ADR-0009) and `CircleTools.Infrastructure` (outbox + SMTP sender). arc42 ch.5 §5.3 now
  fixes the four bounded contexts.
- **Test plan written (2026-07-27):** [TP-002](../features/TP-002-m1-identity.md), weighted toward the
  two disproportionate risks — refresh-token rotation (custom code, incl. the racing-refresh case) and
  enumeration resistance (whose failure mode is invisible to ordinary functional tests).
- **Server half built (2026-07-27):** `CircleTools.Identity` and `CircleTools.Infrastructure` exist, and
  the whole `/api/v1/account` surface with them — register, verify, login, refresh, logout,
  forgot/reset, profile, change-email, passkeys, export, delete — against real Postgres and real
  Mailpit.
- **Client half built (2026-07-28):** the Main Blazor client, plus the four UI building blocks arc42
  ch.5 planned — `Ui.Abstractions`, `Localization`, `ApiClient`, `Ui.Shared`
  ([ADR-0025](../adr/0025-shared-ui-building-blocks.md)). Three decisions that were being made
  silently in code became ADRs: styling ([0024](../adr/0024-ui-styling-and-theming.md)), the shared
  blocks ([0025](../adr/0025-shared-ui-building-blocks.md)), and where the refresh token is kept
  ([0026](../adr/0026-refresh-token-persistence.md), extending ADR-0021). FEAT-002 and TP-002 moved
  to *In progress*.
- **Secret-Santa migrated onto the shared blocks (2026-07-28):** ADR-0025's recorded follow-up is
  closed. Secret-Santa lost its own `AppCultures`, its own `CultureSelector` and its own
  `IKeyValueStore`; `secretsanta.culture` and `circletools.secretsanta.groups` are untouched, so no
  user loses a language or a group. Two amendments came out of it —
  [ADR-0028](../adr/0028-browser-platform-adapters-block.md) (a shared `Ui.Web` block for the browser
  adapters, now that there are two web hosts) and
  [ADR-0029](../adr/0029-storage-write-failure-contract.md) (`SetAsync` reports success, so a lost
  preference and a lost group stop being the same event). 281 tests green; full journey re-driven in
  a browser in DE/EN/FR.
- **M1 closed in the docs (2026-07-29):** [FEAT-002](../features/FEAT-002-m1-identity.md) is *Done*
  and [TP-002](../features/TP-002-m1-identity.md) *Reviewed*. The requirement and use-case registers
  were advanced with them — `Implemented` where every criterion is met, `Approved` where one is
  knowingly still open (FR-010/011/012/015, NFR-004), so the split marks where the slice stops
  instead of reading as neglect. The two exit criteria that could not be met are **carried to
  milestone 7**, not ticked; see the carry-overs below.
- **Carried to milestone 7 (from M1):**
  - **Automate the WebAuthn ceremony.** Every passkey test in the suite is negative or an
    authorization check; none creates a credential, because an in-process `WebApplicationFactory`
    has no authenticator. The ceremony works — it was driven by hand through a CDP virtual
    authenticator — but nothing re-checks it, and **NFR-004's "no private key, no biometric data"
    has no stored credential to inspect** until it is automated. Needs a browser-driving test host.
  - **A CI pipeline.** There is none, so the Definition of Done's "CI green" cannot be satisfied by
    any milestone until one exists. The CI/CD choice is still an open item in the
    [ADR register](../adr/index.md#open-decisions-backlog) and needs an ADR before the pipeline.
- **M2 written, built and closed (2026-07-29 → 2026-07-30):** the circle use cases and requirements,
  arc42 ch. 5's Circles context, [FEAT-003](../features/FEAT-003-m2-circles.md) (*Done*) and
  [TP-003](../features/TP-003-m2-circles.md) (*Reviewed*) — see `IDEA-002` above for what they decided,
  what was built, and what closing the milestone turned up.
- **M3 written (2026-07-30):** the sync and claim requirements —
  [FR-023](../requirements/functional/FR-023-sync-across-devices.md) sync across devices ·
  [FR-024](../requirements/functional/FR-024-offline-editing-and-reconciliation.md) offline editing
  and reconciliation · [FR-025](../requirements/functional/FR-025-claim-local-groups.md) claim ·
  [FR-026](../requirements/functional/FR-026-remove-group-from-account.md) un-claim ·
  [NFR-008](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md) convergence &
  data safety — plus glossary entries for [Claim](../glossary/claim.md) (whose naming hazard is
  recorded: ASP.NET Identity already has a `Claim`) and [Tombstone](../glossary/tombstone.md) (which
  is **not** a soft delete). **The one decision that blocked the build is now closed** (2026-08-03):
  the requirements exclude circle data from sync, narrowing
  [ADR-0016](../adr/0016-sync-and-conflict-resolution.md) — last-write-wins on data four admins share
  would discard other people's edits, and an offline circle write cannot make the server-side
  authorization decision NFR-006 requires — and that narrowing is recorded as
  [ADR-0033](../adr/0033-which-aggregates-sync.md). The M3 feature spec and test plan
  ([FEAT-004](../features/FEAT-004-m3-sync-and-claim.md) *Done* / [TP-004](../features/TP-004-m3-sync-and-claim.md)
  *Reviewed*) were written, built and closed 2026-08-12 — see the `IDEA-001` M3 note above for the
  build and the browser walkthrough. FR-023 also carried **FR-012**'s offline language change, now
  closed.
- **Of M1's three deferred criteria, one still stands**, recorded as deferred in FEAT-002: MAUI
  cross-client passkeys (needs the real domain). The *offline* language change **closed with M3**
  (FR-012, via FR-023/024 — verified cross-device in the M3 browser walkthrough). The remaining one —
  **the sole-circle-owner deletion guard — closed with M2** as `SoleOwnerCircleGuard`,
  verified end to end through real account deletion. It blocks on **every** circle the account owns
  rather than only the ones nobody else could own: deleting the owner's own membership would otherwise
  leave a circle with no owner, a state the domain does not define.
- **Blocked on deployment ADRs — M1 cannot ship without them:**
  - **Transactional email provider.** FR-007 (verification), FR-009 (reset) and FR-013 (email change)
    all depend on delivery. The ADR register currently justifies deferring this behind M2 invitations;
    it is in fact an **M1** dependency.
  - **Hosting / suite domain.** Needed for the passkey RP-ID (FR-010, FR-011), the CSP directive set
    (ADR-0021), and the NFR-004 residency target. The *strategy* for each is decided — only the
    literal domain is missing. See [naming](../process/naming.md).
- **Store distribution scoped (2026-07-29):** what it actually takes to put the MAUI heads in the two
  stores, worked out and written down ahead of milestone 8, because two parts of it have long lead
  times and one is irreversible.
  - **Decided:** publisher identity — enrol as a private individual on **project-owned** accounts,
    never the personal ones ([ADR-0030](../adr/0030-mobile-store-publisher-identity.md)); and the
    **iOS build host** — an own Apple-silicon Mac ([ADR-0031](../adr/0031-ios-build-host.md)).
  - **The finding that reorders the work:** iOS cannot be built on Windows at all, and the Intel
    MacBook Pro is at best a one-release bridge — .NET 10 needs Xcode 26 → macOS 26, which only the
    16-inch 2019 and four-port 13-inch 2020 models run, and macOS 27 drops Intel entirely. Recorded
    as risk **R6**.
  - **The other long pole is Google's, not ours:** a new personal Play account must run closed
    testing with **12 opted-in testers for 14 continuous days** before it may even apply for
    production access. Android is ≈4–6 weeks of mostly waiting; iOS is ≈1–2 weeks once a Mac exists.
    So the Play account and the tester list should be started first, well before code is ready.
  - **Obligations stated** as [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)
    (annual SDK / target-API floors, DSA trader status, account-deletion URL, privacy declarations,
    DE/EN/FR listings) and the procedure as the
    [mobile-apps runbook](../deployment/mobile-apps.md); what can be tested on today's hardware is in
    [device testing](../deployment/device-testing.md).
  - **Still blocked on the brand:** submission, not account setup. The bundle id is permanent once
    released and RP-IDs do not migrate, which promotes [naming](../process/naming.md) from a cosmetic
    milestone to a hard gate (risks **R8**, debt **D7**).

## Later / parked
- Additional apps (to be brainstormed).
- **Invite by link or short code** — parked; revisit if email delivery proves to be the friction
  point (see [Invitation](../glossary/invitation.md)).
- **Per-member envelope encryption of assignments** — the P3 option from D1, parked as future
  hardening if the operator-as-trusted-party assumption stops being acceptable.
