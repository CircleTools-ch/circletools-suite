---
date: 2026-09-13
facilitator: Adrian
participants: [Adrian]
topic: Storage & sharing tiers for Secret-Santa (local / account-free backend / account)
status: Concluded
---

# Brainstorming — Storage & sharing tiers for Secret-Santa (2026-09-13)

> **Concluded 2026-09-13.** Captures a **net-new** idea — a third **account-free, backend-stored,
> link-shared** storage tier — that **conflicts with several settled decisions** and therefore needs
> an **ADR before implementation** (CLAUDE.md rule 5). All pivotal product/UX decisions are resolved
> and recorded below (see **Decisions & selected ideas** and the ✅ marks). The **assignment-privacy
> stance changes a published promise** — [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)
> and the sync definition ([glossary/sync.md](../glossary/sync.md)) will be amended by the ADR.
> **Recorded as [ADR-0058](../adr/0058-account-free-link-shared-groups.md)** (Accepted 2026-09-13).
> Its doc amendments are applied (NFR-002, glossary/sync, arc42 §8, and five new glossary terms).
> **Next: the use-case → requirement → feature-spec pass.**

## Objective / framing question

Today the app has **two** storage modes, and the docs treat them as a hard binary — *no account ⇒
no server; account ⇒ server sync*
([glossary/sync.md](../glossary/sync.md), [arc42 §8](../arc42/08-crosscutting-concepts.md),
[ADR-0053](../adr/0053-backend-free-first-mobile-release.md)):

1. **Local-only** — offline, account-free; data lives on the device; sharing is via self-contained,
   client-encoded **reveal links** ([ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md),
   [ADR-0015](../adr/0015-mvp-client-offline-storage.md),
   [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).
3. **Synced with an account** — the whole group aggregate (draw + assignments included) lives in our
   Postgres and syncs across the signed-in user's devices
   ([ADR-0016](../adr/0016-sync-and-conflict-resolution.md),
   [ADR-0020](../adr/0020-server-side-assignment-privacy.md),
   [ADR-0033](../adr/0033-which-aggregates-sync.md)).

**The proposal:** insert a **middle tier** and present the mobile (Android/iOS) user with **three
explicit choices** for a group:

1. **Local only** — as today. The user can only share **reveal links**.
2. **Synced without an account** — the *group* is stored in our Postgres backend and can be **shared
   with other people via a shared group link**; anyone holding the link gets access to the group.
   No sign-up, no identity.
3. **Synced with an account** — as today.

> The single question this session answers: **is an account-free, backend-stored, link-shared group
> a mode we want — and if so, what does the shared link grant, and how do we hold that data without
> breaking the privacy promises tier 1 and tier 3 were built to keep?**

## Why this is being raised now

- **The market rewards exactly this.** The
  [competitor scan](2026-09-02-secret-santa-competitor-research.md) found DrawNames' "browser draw
  **joinable by link, no email / no app / no account required**" is a repeatedly-praised pattern, and
  reviewers punish "forced install/signup for guests." Tier 1's reveal links already give *recipients*
  a no-account experience — but there is no way for **co-organizers or participants to see and act on
  the shared group** without either the organizer's device or a full account.
- **It closes the gap between "the link is the secret" and "log in to collaborate."** Right now the
  jump from private-offline to shared-collaborative is also a jump to *sign up*. The middle tier lets
  a group become multi-device / multi-person **without** demanding identity.

## Divergent — raw ideas

Capture everything, no judging.

- Present the three modes as a **per-group** choice, not an app-wide setting — one family group can be
  local, another synced-by-link.
- The middle tier is essentially **"claim a group to an anonymous, link-addressed container"** instead
  of to a user account. Reuse the [claim](../glossary/claim.md) machinery
  ([ADR-0017](../adr/0017-anonymous-to-account-claim.md)) but key the server copy by an
  **unguessable share token** rather than a user id.
- The share link *is* the credential (bearer capability). Holding it = access. Same mental model as
  reveal links ("the link is the secret"), scaled up from one assignment to the whole group.
- **Two link grades?** A **read/participant** link (see your own assignment, the budget, wishlists you
  may edit) vs. an **organizer/edit** link (add members, run the draw). Or a single all-access link in v1.
- Later, a synced-by-link group can be **upgraded to an account** (someone signs in and claims it) —
  and maybe **downgraded** back to local-only (pull it off the server, keep the local copy), mirroring
  the existing claim/unclaim.
- Server stores the group aggregate the same way tier 3 does — reuse `IGroupRepository`'s syncing
  implementation and [ADR-0033](../adr/0033-which-aggregates-sync.md)'s "aggregate is the unit of
  sync"; the only change is **who/what authorizes** a pull/push.
- **Assignments on the server without an account.** This is the sharp edge:
  [ADR-0020](../adr/0020-server-side-assignment-privacy.md) stores plaintext assignments protected by
  **per-user authorization** (the API returns only the caller's own row). A link-holder has no user
  identity to authorize against — so either (a) the link-holder is the *organizer* who is allowed to
  see the whole mapping anyway, or (b) we need per-person sub-links / a different privacy model so a
  participant with the group link can't read everyone's assignments and spoil the draw.
- Consider **per-member personal links** off the group (like today's reveal links, but live/server-backed)
  so a participant sees only their own assignment while the group itself syncs.
- **Encryption option revisited.** ADR-0020 rejected per-member envelope encryption (P3) for the
  account tier; for an account-free tier where "the link is the key," client-side encryption keyed by a
  secret **in the link fragment** (never sent to the server) becomes newly attractive — the server holds
  ciphertext it cannot read, preserving something close to NFR-002 even while storing the group.
- **Retention / lifecycle.** Account-free server data has no owner to manage it — needs a TTL, an
  expiry, or an explicit "delete this shared group" any link-holder (or only an edit-link holder) can trigger.
- **Abuse / cost surface.** Anonymous writes to our Postgres = spam/storage-abuse vector. Rate limits,
  size caps, expiry, maybe a proof-of-work or lightweight anti-abuse token on creation.
- **Conflict resolution** across multiple anonymous editors of one group — last-write-wins per aggregate
  (as ADR-0016) still applies, but now the "writers" are several link-holders, not one user's devices.

## Clustering / themes

- **A · Product shape** — three explicit per-group choices; how they're presented; upgrade/downgrade
  paths between tiers.
- **B · Authorization model** — the share link as a bearer capability; one link vs. read/edit grades
  vs. per-member personal links; revocation.
- **C · Assignment privacy** — the hard one. How to keep the draw non-spoiling when the group (incl.
  assignments) lives on the server and is reachable by a link rather than an authenticated user.
  Options: organizer-only whole-mapping access + per-member sub-links; or link-fragment client-side
  encryption (server holds ciphertext).
- **D · Data lifecycle & anti-abuse** — retention/TTL, deletion without an owner, rate limiting, size
  caps, spam resistance for anonymous writes.
- **E · Reconciliation with settled docs** — this mode contradicts current statements and must
  supersede/amend them, not sit beside them (see below).
- **F · UI / UX & platform surface** — how the three modes appear in the app, how the user picks one,
  how a group is shared (with/without admin), and what differs Web vs Android/iOS. Detailed below.

## UI / UX — how the three modes appear in the app

Grounded in today's UI: a **per-group hub** (`GroupEditor.razor`, ADR-0055) whose header already has a
**⋮ overflow menu** (currently just *Rename*), a per-event **Share** screen (`Share.razor` — copy /
native share sheet / QR via `QrDialog`), a **Settings** page (`Settings.razor`), an **account menu** in
the top bar (`AccountMenu`, sign-in/out), and bottom-sheet + FAB mobile patterns
([FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md), ADR-0054). The design reuses
these seams rather than inventing chrome.

### Where the choice lives — the group's "Sharing & sync" screen

- **Per-group, not app-wide** (theme A). The natural home is the group hub's **⋮ menu** — which
  DGH-3 already flagged should graduate from *"Rename"* to **"Group settings"** *"if more settings
  arrive."* This is that trigger. So: ⋮ → **Group settings**, a screen (sheet on phone, ADR-0054 W4
  `TextInputSheet`/`ConfirmSheet` family) holding **rename + storage mode + sharing**.
- **A mode badge on the hub.** A small chip next to the group name shows the current mode at a glance:
  **On this device · Shared link · Account** (icons: phone / link / cloud-account). Tapping it opens
  Group settings. This is the whole "which mode am I in?" answer in one glance.

### Choosing the mode — the three-way control

A single grouped control (segmented buttons on wide, a radio list / `MudSelect` on phone), each option
one line of plain language, **not** jargon:

| Option (UI label, EN draft) | One-liner shown under it | Effect on pick |
|---|---|---|
| **On this device** *(default)* | "Private to this device. Share a reveal link per person." | Local only — today's behaviour ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)). |
| **Shared by link** | "Stored online so others can open the whole group with a link. No sign-up." | Uploads the group, mints a **share token**, shows the share sheet. |
| **My account** | "Synced across the devices you're signed in on." | Requires sign-in; runs the existing [claim](../glossary/claim.md) (ADR-0017). |

Interaction details:

- **The choice is offered at group creation** *(decided 2026-09-13)* — the create-group step shows the
  three options so a user who already knows they want an account can pick it up front, rather than
  starting local and migrating. **On this device is pre-selected** (the safe, zero-friction default,
  so anyone who just taps *Create* stays private and NFR-001 holds), but all three are visible with
  their one-liners. After creation the mode is changed the same way, from Group settings.
- **Upgrade/downgrade must be easy and reversible** *(decided 2026-09-13)* — switching mode is a
  first-class, low-anxiety action, not a hidden expert setting. Each switch shows a **short, plain
  consequence** (one line, ideally a before → after visual), never a wall of legal text: e.g.
  *"📱 → 🔗 Others with the link will be able to see this group."* / *"🔗 → 📱 The online copy is
  removed and shared links stop working. The group stays on this device."* Primary action is the safe
  confirm; the door back is always open.
- **Local → Shared by link:** a confirm step names what changes — *"This uploads the group to
  CircleTools so people with the link can see it. You can switch it back to this-device-only anytime."*
  On confirm: upload, mint token, drop straight into the **Share group** sheet.
- **Local/Shared → My account (claim admin):** if signed out, open the account menu's sign-in first,
  then **claim the group** into the account (ADR-0017 / ADR-0044). Per the model below, claiming makes
  that account the **sole admin** and **retires the admin link**; the **join link keeps working**.
- **My account → Shared by link (release-admin)** *(added 2026-09-13)*: the reverse of claim-admin, so
  ownership isn't a trap. The server copy is **re-keyed from the account to a fresh share token** and a
  **new admin link is minted**; assignments and existing join-link claims are preserved. Consistent with
  "the admin link works only while no account owns the group."
- **Downgrade** (Shared/Account → On this device): a `ConfirmSheet` warning — *"This removes the online
  copy and any shared links stop working. The group stays on this device."* Mirrors today's *unclaim*
  (the local copy is kept; the server copy is deleted, not tombstoned — per glossary/sync).
- **Account deletion** *(added 2026-09-13; extends [ADR-0022](../adr/0022-account-deletion-and-data-export.md))*:
  deleting an account routes **each account-owned group** through a per-group choice, mirroring
  ADR-0022's "sole circle owner must transfer or delete first" rule — **hand off as shared by link**
  (runs release-admin; **default when others have joined**, so nobody is stranded) / **keep on this
  device** (local copy survives; default when the owner is the only participant) / **delete the online
  copy**. The deletion confirmation enumerates the owned shared groups and their disposition.

### Sharing the group — the two link types *(refined 2026-09-13)*

There are **two distinct links**, answering two different needs:

**1 · Admin link** — grants the **admin capability** (members, exclusions, budget, run/redo the draw,
manage sharing). Key rules from the review:

- **Multiple people can use one admin link** — several co-organizers can hold admin at once while the
  group is account-free. Admin is a *shared capability*, not a single seat.
- **Account-free only.** The admin link works **only while no account owns the group.** The moment an
  account **claims admin** (someone signs in and takes the group), that account becomes the **sole
  admin**, the **admin link stops resolving**, and anyone opening it sees *"<name> has claimed the
  admin role for this group."* This locks **only** the admin capability — the join link is unaffected.
- **Admin can disable / rotate it.** An admin can **turn off the current admin link and mint a fresh
  one** (old token stops working) — the standard "someone I didn't mean to has it" escape.
- **Admin can release a claimed person.** A person, once claimed, is **locked** and cannot be claimed
  again — so a **new device**, a lost device, or a mis-claim is resolved by an **admin freeing the
  person**, after which the join link can claim it afresh *(decided 2026-09-13)*. This is the single
  recovery mechanism for all of "wrong person / lost access / changed device."

**2 · Join link** — lets a person **join the group as a participant**, and works **with *or* without an
account**. Opening it starts a short **"who are you in this group?"** flow with two independent choices:

- **Which person** — *claim an existing person* in the roster (e.g. "I'm Grandma"), or *add a new
  person* (yourself) to the group.
- **How it's remembered** — *create/sign-in to an account* and link it to that person (durable,
  cross-device — reuses member↔user linking, [ADR-0019](../adr/0019-member-user-linking.md), and
  invitee-chosen promotion, [ADR-0043](../adr/0043-invitee-chosen-managed-member-promotion.md)), **or**
  *just this device* (no account — a device-local claim, nothing synced beyond this device).

So the four combinations a joiner can land on: **existing person + account · existing person +
device-only · new person + account · new person + device-only.** A joined participant sees the group,
edits **their own** wishlist / gift note, and sees **their own** assignment once drawn — not others'.

UI (reuses the `Share.razor` seam — copy / native share sheet where `_canShare` / **QR** on every
platform):

- **Group settings → Share group** lists the **admin link** (admins only) and the **join link**, each
  with **Share** / **Copy** / **Show QR** — identical affordances to the reveal-link rows.
- Admin-only controls sit here too: **disable/regenerate the admin link**, and (when account-free)
  **claim admin to my account**.
- **Per-person reveal links still exist** underneath — a group can still hand one person just their
  reveal link (today's flow). The join link is *additive*, not a replacement.

**Privacy model — decided (2026-09-13): trust-based + first-claim-locks.** Because the join link is a
*single shared secret* and lets a holder **claim any not-yet-claimed person**, a nosy holder could
claim someone else first and see their assignment. We accept this under the **trust context** (a family
/ friend circle — the same trust the reveal links already assume), bounded by:

- **First claim wins, and it locks.** Once a person is claimed (by an account **or** a device), that
  person **cannot be claimed again**.
- **Only an admin can release a locked person** — the single recovery path for a new/lost device, a
  changed device, or a mis-claim. After release the person is claimable again via the join link.

Consequence for crypto: a **single per-group key in the link fragment cannot keep assignments
per-person-secret** (everyone shares the join link), so **per-person cryptographic secrecy** (per-person
keys ≈ server-backed per-member reveal links) is recorded as **future hardening**, not v1.

### Web vs Android / iOS — what's available where

The three modes are **shared feature code** ([ADR-0046](../adr/0046-suite-packaging-and-app-composition.md)),
so functionally **all three modes exist on all heads** — the Web WASM app already reaches the Main API
for account/sync ([ADR-0013](../adr/0013-wasm-api-dev-wiring.md)), and the MAUI head shares the same UI.
There is **no mode that is mobile-only or web-only.** The differences are in *mechanics*, not *presence*:

| Concern | Web (Blazor WASM PWA) | Android / iOS (MAUI Blazor Hybrid) |
|---|---|---|
| All three modes present | ✅ | ✅ (shared code) |
| Native **share sheet** for a link | Only where the browser exposes Web Share (mobile browsers); **desktop = Copy/QR only** (today's `_canShare`) | ✅ native OS share sheet |
| **Opening** a shared group link | Opens the WASM PWA in the browser | **Android:** App Links open the app directly (wired, ADR-0053). **iOS:** deep links **not yet wired** (ADR-0053, MNF-10 gated) → falls back to browser/PWA until done |
| **Show QR** of a link | ✅ (client-side) | ✅ |
| **Biometric app-lock** over a synced group | n/a (browser) | Relevant once a group syncs — MNF-9, parked |
| Storage substrate when *On this device* | `localStorage` | local JSON file (ADR-0015) — invisible to the user, same UX |

Practical implications to decide:

- **iOS shared-group links** land in the browser until MNF-10 wires universal links; acceptable for a
  first cut (Android is the lead platform, ADR-0053) but must be called out.
- **Desktop web** users share by **Copy or QR** (no OS share sheet) — already the norm here, no new work.
- Because the substrate differs (`localStorage` vs file) only for *On this device*, and the online modes
  are server-backed, **switching modes must go through the same `IGroupRepository` port** (ADR-0015 /
  ADR-0033) so both heads behave identically.

### UX questions — resolved & open

**Resolved (2026-09-13 review):**

- ✅ **Choice at creation, easy switching after.** Three options shown at group creation (local
  pre-selected); mode changed anytime from Group settings with easy, reversible, plain-language
  consequence prompts (see "Choosing the mode" above).
- ✅ **Two link types; claiming admin retires the *admin* link only.** *(refined 2026-09-13)*
  An **admin link** (shared capability, usable by multiple co-organizers, **account-free only**,
  disable/regenerate-able) and a **join link** (participant, works with or without an account). When
  an account **claims admin**, it becomes the **sole admin**, the **admin link stops working** and shows
  *"<name> has claimed the admin role for this group,"* while the **join link keeps working**. See
  "Sharing the group — the two link types" above for the full model and the join/identity flow.
- ✅ **Q3 — participant self-service *is* the point.** *(decided 2026-09-13)* The join link lets a person
  join and pick who they are (existing person / new person) and how they're remembered (account /
  device-only). This is effectively **Option C** below, with the impersonation risk accepted under the
  **trust context** and bounded by **first-claim-locks**. (See the privacy tradeoff note above.)
- ✅ **Q4 — hub row + chip.** *(decided 2026-09-13)* A **Sharing & sync hub row** (on-pattern,
  discoverable, room for status + the manage action) is the home, **plus a small mode chip** beside the
  group name that deep-links to it.

### Q3 — resolved: what the links expose

The naïve "one all-access link" **cannot work for Secret-Santa** (it would show the draw to everyone and
kill the surprise), which is why the model splits into the **admin link** and **join link** above. For
the record, the shapes considered:

| Option | Shape | Verdict |
|---|---|---|
| **A · Admin-link only** | group link shared only with co-organizers; participants keep per-person reveal links | Not chosen alone — misses participant self-service |
| **B · Two grades (participant view minus assignments)** | participant link shows group but not assignments; own assignment via reveal link | Partial — folded into the join model |
| **C · Full self-service** *(chosen)* | joiner identifies as a person via the **join link** and sees their own assignment; **admin link** separate | **Chosen** — matches the ask; impersonation accepted under trust + first-claim-locks |

**Implication:** v1 carries **both links** (admin + join) rather than the leaner admin-only cut. The
**assignment-privacy decision is made** — trust-based + first-claim-locks with admin release (theme C,
above); per-person cryptographic secrecy is future hardening.

## Convergent — selection

Scored as a **direction to explore**, not as built features. Impact/effort/fit on 1–5 (5 = best);
effort is "low = better."

| Idea | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| Middle tier exists at all (account-free backend-stored, link-shared group) | 4 | 2 | 3 | **Explore** — pursue via ADR |
| Per-group choice of the three modes | 4 | 3 | 4 | **Selected** (if the tier is adopted) |
| Share link as bearer capability ("the link is the secret", scaled up) | 4 | 3 | 5 | **Selected** |
| ~~Single all-access link in v1~~ | — | — | — | **Superseded** — Secret-Santa needs the admin/join split |
| Per-member live personal links (server-backed reveal) | 3 | 2 | 4 | **Explore** — one way to keep per-person secrecy |
| Link-fragment client-side encryption (server holds ciphertext) | 4 | 2 | 4 | **Weakened** — a *single group* key can't keep per-person secrecy; needs per-person keys |
| Upgrade account-free → account (claim) | 4 | 3 | 5 | **Selected** — reuses ADR-0017 |
| Downgrade back to local-only | 2 | 3 | 4 | **Lean yes** — mirrors unclaim |
| TTL / expiry + owner-less delete | 3 | 3 | 4 | **Required** if adopted |
| Anti-abuse on anonymous create/write | 3 | 3 | 4 | **Required** if adopted |
| Mode chosen per-group in ⋮ → **Group settings** (promote DGH-3) | 4 | 4 | 5 | **Selected** — reuses existing seam |
| Three-way control + mode **badge** on the hub | 4 | 4 | 5 | **Selected** |
| Mode **offered at group creation** (local pre-selected) | 4 | 4 | 5 | **Selected** (2026-09-13) — keeps NFR-001 default |
| Easy, reversible switching w/ plain consequence prompts | 5 | 4 | 5 | **Selected** (2026-09-13) |
| **Two link types: admin link + join link** | 4 | 3 | 5 | **Selected** (2026-09-13) |
| Admin link = **shared capability**, multi-holder, **account-free only**, disable/regenerate | 4 | 3 | 5 | **Selected** (2026-09-13) |
| **Claiming admin** → sole admin, admin link retired ("<name> claimed admin"), join link lives on | 4 | 3 | 5 | **Selected** (2026-09-13) |
| **Join flow:** pick person (existing/new) × persistence (account/device-only) | 5 | 2 | 5 | **Selected** (2026-09-13) — reuses ADR-0019/0043 |
| Assignment privacy: **trust-based + first-claim-locks** | 4 | 2 | 4 | **Selected** (2026-09-13) — per-person crypto = future hardening |
| **Claimed person is locked; only an admin can release it** to be re-claimed | 4 | 3 | 5 | **Selected** (2026-09-13) — covers lost device + mis-claim |
| Reuse `Share.razor` (share sheet / copy / QR) for the group link | 4 | 4 | 5 | **Selected** |
| All three modes on **all heads** (no web/mobile asymmetry) | 4 | 3 | 4 | **Selected** — mechanics differ, not presence |
| iOS shared-link deep-linking | 2 | 2 | 3 | **Park** on MNF-10 — browser fallback for v1 |

## Decisions & selected ideas

Nothing is decided yet — this is a Draft. The **emerging direction** to put in front of Adrian:

- The middle tier is worth an **ADR-level decision**, because it reopens settled privacy/architecture
  choices; it cannot be "just built."
- If adopted, model it as **per-group**, with the **share link as the credential**, reusing the
  existing sync aggregate machinery and the claim/unclaim shape (server copy keyed by a **share token**
  instead of a user id).
- **Two link types decided** (2026-09-13): an **admin link** (shared capability, account-free only,
  retired when an account claims admin, disable/regenerate-able) and a **join link** (participant;
  account or device-only; pick existing/new person). See the two-link-types section.
- **Assignment privacy decided** (2026-09-13): **trust-based + first-claim-locks.** A joiner claims a
  person and sees that person's assignment; the claim **locks** the person permanently, and **only an
  admin can release** it (the recovery path for lost/new device and mis-claims). Bounded by the
  family/friend **trust context** the reveal links already assume. **Future hardening (not v1):**
  per-person cryptographic secrecy (per-person keys ≈ server-backed per-member reveal links) — a single
  group-key link fragment can't achieve it since everyone shares the join link. Amends
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) (assignments now reach
  device-only joiners, not only signed-in users).

## Rejected / parked (for the record)

- **App-wide storage mode.** Rejected in favour of per-group — families mix private and shared groups.
- **Read/edit link grades in v1.** Parked; start with one link, add grades only if needed.
- **Merging anonymous edits field-by-field.** Rejected — keep last-write-wins per aggregate
  ([ADR-0016](../adr/0016-sync-and-conflict-resolution.md)); the unit of sync stays the aggregate
  ([ADR-0033](../adr/0033-which-aggregates-sync.md)).
- **Doing this without an ADR.** Rejected — it changes published promises (below).

## Settled decisions this would reopen (must be reconciled, not bypassed)

Per **CLAUDE.md rule 5**, this needs an ADR *before* implementation. That ADR would have to
supersede or amend:

- [glossary/sync.md](../glossary/sync.md) — "Without an account there is no server, no sync." Would
  need a new account-free-but-synced concept (and likely a new/《amended》 glossary term).
- [arc42 §8 crosscutting](../arc42/08-crosscutting-concepts.md) — same binary statement.
- [ADR-0053](../adr/0053-backend-free-first-mobile-release.md) — "the account API should become
  necessary only when someone logs in to sync."
- [ADR-0016](../adr/0016-sync-and-conflict-resolution.md) — sync currently presupposes a signed-in user.
- [ADR-0020](../adr/0020-server-side-assignment-privacy.md) /
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) — server-side assignment
  privacy is defined in terms of **per-user authorization**; a link-holder has no user identity.
- [ADR-0018](../adr/0018-circle-and-sharing-model.md) — the existing "sharing" is *inviting people into
  a Circle* and is account-bound; the new share-by-link-without-account model must be distinguished
  from it, and the **parked "invite by link/short code"** idea
  ([backlog](../roadmap/backlog.md), Later) reconciled with it.

## Follow-up actions

- [x] **All pivotal product/UX decisions taken** (2026-09-13): three modes chosen at creation w/ easy
      reversible switching; **admin link** (shared, account-free, retired on account admin-claim,
      disable/regenerate); **join link** (account or device-only; claim existing/new person);
      **trust-based + first-claim-locks with admin release**; hub row + chip. — Adrian
- [x] **ADR written & [ADR-0058](../adr/0058-account-free-link-shared-groups.md) Accepted**
      (2026-09-13), cross-referencing ADR-0014/0015/0016/0017/0018/0019/0020/0022/0033/0043/0053 and
      NFR-001/002. Next: apply its amendments to glossary/sync + arc42 §8 + NFR-002.
- [ ] Add/adjust **glossary term(s)** for the middle tier (e.g. *shared group* / *admin link* /
      *join link* / *account-free sync*) before naming them in code. — target: with the ADR
- [ ] Take through the process: **use case(s)** (choose storage mode at creation; switch mode;
      admin-share by admin link; join via join link incl. the identity/persistence flow; claim admin
      to an account; admin releases a claimed person) → **requirements** → **feature spec + test plan**.
- [ ] Capture **anti-abuse & retention** requirements (TTL, size caps, rate limits, owner-less delete)
      as NFRs.
- [ ] **UX pass** — wireframes for Group settings (mode + sharing), the join flow, and the mode
      chip/hub row, likely a `design` canvas, before the feature spec.
