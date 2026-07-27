---
date: 2026-07-24
facilitator: Adrian
participants: [Adrian]
topic: Accounts, families & sync (v2)
status: Concluded
---

# Brainstorming — Accounts, families & sync (v2) (2026-07-24)

> **Concluded 2026-07-27.** All eight pivotal decisions (D1–D8) are resolved and recorded below and
> under “Decisions & selected ideas”. The option tables are kept deliberately: the rejected
> alternatives are the record of *why*. One decision (**D1**) knowingly changes a published privacy
> promise — [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) has been amended
> accordingly, and [ADR-0020](../adr/0020-server-side-assignment-privacy.md) partly supersedes
> [ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md).
>
> **All eight decisions are now recorded as ADRs** —
> [0016](../adr/0016-sync-and-conflict-resolution.md) sync ·
> [0017](../adr/0017-anonymous-to-account-claim.md) claim ·
> [0018](../adr/0018-circle-and-sharing-model.md) circles ·
> [0019](../adr/0019-member-user-linking.md) linking ·
> [0020](../adr/0020-server-side-assignment-privacy.md) assignment privacy ·
> [0021](../adr/0021-web-auth-token-model.md) web tokens.

## Objective / framing question

The offline Secret-Santa MVP is done ([FEAT-001](../features/FEAT-001-secret-santa-mvp.md)). What is
the smallest coherent **v2** that gives the suite a **central account** in the Main shell, a
**Family** concept, and **cross-device sync** for Secret-Santa — and what exactly do we give up in
privacy and simplicity to get it?

Secondary framing: the MVP’s core promise is *“no account, no backend, the link is the secret.”* v2
must not delete that promise — an account has to stay **optional and additive**
([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
[ADR-0009](../adr/0009-authentication-and-passkeys.md)). Everything below is judged against that.

## Divergent — raw ideas

Capture everything, no judging.

**Identity / account**

- Sign up with name + email + password.
- Register a passkey after signup, for subsequent logins.
- Passkey-only signup (no password at all).
- Magic-link / passwordless email login.
- Social login (Google / Apple).
- Email verification required before an account can join a family.
- Profile: display name, avatar, preferred language (DE/EN/FR).
- Account deletion and data export (revDSG / GDPR).
- Merging two accounts a person accidentally created.
- One suite account across all apps (already the stance in ADR-0009).

**Family**

- Create a family and name it.
- Invite a **new** person by email → they receive an invitation → sign up → land in the family.
- Invite an **existing** account by email → they get a join request in-app and accept or decline.
- Invite by shareable link or short code — no email needed at all.
- Roles: owner / admin / member.
- **Managed members without an email** (young children, grandparents who won’t sign up) — a profile
  inside the family that a parent controls, with no account behind it.
- Leave a family; remove a member; transfer ownership.
- A user belongs to **several** families (own family, partner’s family, a friend group).
- Is “Family” even the right word for a friend-group? Alternatives: Circle, Household, Crew, Group.
- Family-level shared settings (default language, default budget…).

**Secret-Santa integration**

- Groups stay creatable standalone and offline — unchanged behaviour for account-free users.
- A group can be created **from a family**, auto-importing its members.
- A group can link **individual members** to accounts ad hoc, with no family involved.
- Mixed group: some members linked to accounts, some just names.
- A linked member logs in and sees **their own** assignment in-app — no reveal link needed.
- An unlinked member still gets a reveal link exactly as today.
- A linked member **owns and edits their own** [wishlist](../glossary/wishlist.md), instead of the
  organizer typing it for them.
- Notification when the draw is done (email now, push later).
- Family membership at draw time vs family membership that keeps changing afterwards.
- The organizer can still run everything alone — an account must never become mandatory.

**Sync**

- Local-first: the device is authoritative, the server is backup/relay.
- Server-authoritative when logged in, local storage as cache.
- Explicit “upload this group to my account” action, user-triggered.
- Automatic background sync.
- Anonymous→account **claim** on first login: merge all / choose per group / replace.
- Per-aggregate version token + tombstones for deletes; delta pull/push (the interim stance already
  recorded in the [ADR backlog](../adr/index.md)).
- Last-write-wins per aggregate, with an escalation hook for real conflicts.
- Conflict UI for the rare genuine collision.
- Sync only groups the user organizes vs also groups they participate in.

**Privacy of the draw once a server exists**

- Server stores assignments in plaintext; authorization keeps members apart.
- Server stores group/members/wishlists but **never** assignments — the draw result stays local and
  travels only in reveal links, as today.
- Server stores each assignment as a blob encrypted to that member; the operator cannot read it.
- Group-level key held by clients, server sees ciphertext only.

## Clustering / themes

- **Theme A — Identity in Main.** Signup, login, passkeys, profile, deletion/export. Stack already
  decided in [ADR-0009](../adr/0009-authentication-and-passkeys.md); the open piece is the **web
  token model**, explicitly deferred there.
- **Theme B — the shared circle as a new bounded context.** A genuinely new domain in the Main shell:
  circles, memberships, roles, invitations. (None of it existed in the
  [glossary](../glossary/index.md) at the time of the session; the terms were added when this session
  concluded — see D6.)
- **Theme C — Invitations & the email-existence problem.** Everything about “detect that this email
  already has an account” is really an account-enumeration question.
- **Theme D — Secret-Santa ↔ identity linking.** What a
  [Member](../glossary/member.md) *is* once accounts exist, and how a
  [Group](../glossary/group.md) relates to a family.
- **Theme E — Sync & the anonymous→account claim.** Mechanism, conflict policy, and what happens to
  data already sitting in `localStorage`.
- **Theme F — The privacy delta.** The one theme that isn’t additive: today we promise **zero**
  server-side assignments, and v2 breaks that unless we deliberately design around it.
- **Theme G — Infrastructure that suddenly becomes real.** Postgres, transactional email, hosting,
  data residency, CI/CD — all deferred until now precisely because there was no backend.

## Convergent — selection

Impact / effort / fit on a 1–5 scale (5 = best; effort 5 = cheapest).

| Idea | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| Email+password account in Main | 5 | 4 | 5 | **Selected (v2)** |
| Passkey registration after signup | 4 | 4 | 5 | **Selected (v2)** |
| Profile incl. preferred language | 3 | 5 | 5 | **Selected (v2)** |
| Account deletion & data export | 4 | 3 | 5 | **Selected (v2)** — legally non-optional once we store PII |
| Family: create, name, roles | 5 | 4 | 5 | **Selected (v2)** |
| Invite a new person by email | 5 | 3 | 5 | **Selected (v2)** |
| Invite an existing account (join request) | 5 | 3 | 5 | **Selected (v2)** |
| Managed members without email (children) | 4 | 3 | 5 | **Selected (v2)** — D7 |
| Invite by link/code instead of email | 3 | 4 | 4 | Parked — revisit if email delivery proves painful |
| Groups stay standalone & offline | 5 | 5 | 5 | **Selected (v2)** — non-negotiable |
| Group created from a family | 5 | 3 | 5 | **Selected (v2)** |
| Ad-hoc member↔account linking, no family | 4 | 3 | 5 | **Selected (v2)** |
| Linked member sees own assignment in-app | 5 | 2 | 4 | **Selected (v2)** — unblocked by D1 = P1 |
| Linked member owns their own wishlist | 4 | 3 | 5 | **Selected (v2)** |
| Sync mechanism (local-first, LWW per aggregate) | 5 | 2 | 4 | **Selected (v2)**, details → ADR |
| Anonymous→account claim | 5 | 3 | 5 | **Selected (v2)**, semantics → D4 |
| Assignments stored server-side in plaintext | 4 | 5 | 4 | **Selected (v2)** — D1 (option P1) |
| Assignments never stored server-side (hybrid) | 2 | 4 | 2 | **Rejected** — D1: an account that can't show you your assignment |
| Assignments encrypted per member | 5 | 1 | 3 | **Parked — future hardening** (D1, option P3) |
| Social login (Google/Apple) | 3 | 3 | 2 | Rejected for v2 — external dependency, conflicts with the single-suite-account stance |
| Magic-link login | 3 | 4 | 3 | Parked — passkeys cover the “no password” desire |
| Account merging | 2 | 1 | 3 | Parked — rare; handle manually if it happens |
| Push notifications | 4 | 2 | 4 | Parked — email first; push needs store/native work |
| Automatic background sync | 3 | 2 | 4 | Parked for v2 — sync on app start + on change is enough |
| Conflict-resolution UI | 2 | 2 | 3 | Parked — LWW first, add UI only if collisions actually occur |

### Pivotal decisions

**All resolved 2026-07-27.** Each unblocks an ADR sitting in the [ADR backlog](../adr/index.md).
The option tables are kept on purpose — what was rejected, and why, is the durable part.

#### D1 — What does the server know about a draw? ⚠️ the important one

This is the only part of v2 that **removes** a guarantee rather than adding one.
[NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) currently states a
*measurable* target: **“zero assignments stored server-side”**, verified by network capture. Any
option below except P2 makes that acceptance criterion false, so NFR-002 must be amended and
[ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md) partly superseded — deliberately, in
writing, not as a side effect.

Useful framing before choosing: **the organizer already holds the complete giver→recipient mapping
today**, in their own local storage. v2 does not expose anything new to *them*. The only new
observer is the **server operator** (i.e. us, plus anyone with database access or a backup). So D1
reduces to: *do we make the operator a trusted party?*

| Option | What it means | Cost |
|--------|---------------|------|
| **P1 — plaintext + authorization** | Assignments live in Postgres; the API returns only the caller’s own row. | Simplest; in-app reveal works everywhere; operator can read every pairing; NFR-002 rewritten around authorization instead of absence. |
| **P2 — hybrid, assignments never leave the client** | Groups, members, wishlists, exclusions sync; the draw result stays local and travels only in reveal links, exactly as today. | Keeps the promise literally intact; but a logged-in member on a new device **cannot** see their assignment without their link, which undercuts the main reason to log in. |
| **P3 — per-member envelope encryption** | Each assignment is encrypted to the member; the server stores ciphertext. | Strongest; substantial key-management work, and password reset means either key escrow or losing the assignment — a bad failure mode for a family app. |

*Recommendation:* **P1**, stated honestly — document that the operator can technically read
assignments, keep the data minimal, and note P3 as a future hardening step. P2 is intellectually
tidy but delivers an account that doesn’t do the thing people log in for.

> **Decided: P1 — plaintext + authorization.** Assignments live in Postgres; the API returns only the
> caller’s own row. The operator becomes a trusted party, and we say so in writing rather than
> implying otherwise. P2 was rejected because it produces an account that cannot show you your own
> assignment — the single reason people would log in. P3 is **parked as future hardening**: its key
> management is real work, and password reset would mean either key escrow (which dissolves the
> guarantee) or permanently losing an assignment, which is an unacceptable failure mode for a family
> app.
>
> **Consequences, deliberate:**
> [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) is **amended** — its
> “zero assignments stored server-side” target now applies to **account-free mode only**, and account
> mode is governed by an authorization target verified by tests instead of network capture.
> [ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md) is **partly superseded** by
> [ADR-0020](../adr/0020-server-side-assignment-privacy.md), which records this decision.
> [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)
> is untouched: not signing in preserves the original property in full.

#### D2 — Circle membership vs group snapshot

If the circle gains or loses a member in January, what happens to a group drawn in December?

- (a) **Snapshot at draw time** — the group freezes its member list when the draw runs. *Recommended:*
  a completed draw is history and must stay reproducible.
- (b) Live link — the group always mirrors the family. Breaks existing draws.
- (c) Hybrid — live until the draw, frozen after. Same as (a) plus explicit UI wording.

> **Decided: (a) snapshot at draw time.** A completed draw is history and must stay reproducible; a
> circle that changes in January must not silently rewrite December. In practice this is (c) — the
> group tracks the circle while it is still being set up, and freezes when the draw runs — so the UI
> must say plainly that running the draw fixes the participant list. This aligns with the existing
> draw-invalidation behaviour for membership changes.

#### D3 — What is a `Member` once accounts exist?

- (a) **`Member` keeps its identity and gains an optional `UserId`.** *Recommended:* smallest change,
  keeps unlinked members first-class, and the domain stays independent of the identity context.
- (b) `Member` becomes two subtypes (named-only vs account-backed).
- (c) Members are always users — rejected outright; it would kill the offline, account-free mode.

> **Decided: (a) optional `UserId` on `Member`.** Smallest change, keeps unlinked members
> first-class, and the Secret-Santa domain stays free of any dependency on the Identity context — it
> stores an identifier, not a user. (b) was rejected as premature polymorphism: the two “subtypes”
> differ in how the person is *served* (in-app reveal vs [Reveal link](../glossary/reveal-link.md),
> self-owned vs organizer-typed [Wishlist](../glossary/wishlist.md)), not in what they *are*.
> Recorded in the [Member](../glossary/member.md) glossary entry.

#### D4 — Anonymous → account claim semantics

- (a) **Choose per group** on first login (“you have 3 local groups — which do you want in your
  account?”). *Recommended:* explicit, reversible, no surprises.
- (b) Merge everything automatically. (c) Replace local with server.
- Related: what happens to local data **after** claiming — keep as cache, or clear.

> **Decided: (a) choose per group on first login.** Explicit, reversible, no surprises — a silent
> merge or replace is exactly the kind of irreversible surprise that destroys trust in a sync feature
> on day one. Local data is **kept as a cache** after claiming, not cleared, so nothing is lost if the
> claim was a mistake. Unclaimed groups stay local and keep working, unchanged and account-free.

#### D5 — Invitations & account enumeration

“Detect that an email is already used” is exactly the probe an attacker uses to harvest which
addresses have accounts.

- (a) **Never reveal existence to the inviter.** The UI always says *“invitation sent”*; the branch
  (sign-up vs join-request) is resolved on the *recipient’s* side, in the email and in their app.
  *Recommended*, and consistent with [security-privacy-guidelines](../guidelines/security-privacy-guidelines.md) §1.
- (b) Tell the inviter — better UX, leaks membership.
- Also decide: invitation expiry, resend/revoke, and rate limits per inviter.

> **Decided: (a) never reveal account existence to the inviter.** The invite form would otherwise be
> a free account-enumeration oracle. The UI always says *“invitation sent”*; whether the recipient
> signs up or gets an in-app join request is resolved entirely on their side. Invitations expire, can
> be resent and revoked by a circle admin, and issuing them is rate-limited per inviter. The
> resulting UX cost — the inviter cannot see whether their friend is already a user — is accepted.
> Recorded in the [Invitation](../glossary/invitation.md) glossary entry.

#### D6 — The term for the shared circle

The product serves “families **and groups of friends**”; calling a friend-group a *Family* is a
domain smell, and rule 7 of `CLAUDE.md` says the glossary decides before code exists.

- (a) `Family` — warm, matches the brand (**Walf** = *We Are Like Family*), slightly wrong for friends.
- (b) `Circle` — neutral, covers both, colder.
- (c) One concept, user-chosen label, canonical code name picked here regardless.

Decide **before** the first line of code — this name ends up in tables, DTOs and URLs.

> **Decided: (c), with `Circle` as the canonical code name.** One concept; the label is per-circle and
> chosen by its members, defaulting to “Family” (*Familie* / *Famille*).
>
> - **Code — `Circle`.** Aggregates, DB tables, DTOs and URLs all say `Circle`. It is the only option
>   that is never *wrong*: “Family” is literally inaccurate for a friend-group, and rows in a table
>   should not lie.
> - **UI — user-chosen label, default “Family”.** The warmth the brand wants lives in the label
>   layer, where it costs nothing and can differ per circle. Most users will never see the word
>   “Circle”.
> - **`Group` stays reserved** for the Secret-Santa aggregate. A circle is the standing set of
>   people; a group is one gift exchange. Conflating them is the most likely modelling mistake here,
>   so `group.md` now lists “circle” under *do-not-use* instead of as a synonym.
>
> Recorded in [Circle](../glossary/circle.md).

#### D7 — Managed members without an email

Young children have no email address, and a family Secret-Santa without the kids is pointless. A
family member that is a **profile without an account**, controlled by a parent, is cheap to design
in now and painful to retrofit once invitations and sync assume “member = account”.

*Recommendation:* include it in the v2 model, even if the UI ships later.

> **Decided: included in the v2 model.** A family Secret-Santa without the kids is pointless, and
> every invitation, sync and linking decision above would otherwise quietly assume “member =
> account” — an assumption that is very expensive to unpick later. The UI may ship after M2; the
> model must not. Managed members receive their assignment the account-free way, via a reveal link
> passed on by the organizer. Recorded in [Managed member](../glossary/managed-member.md).

#### D8 — Web auth token model (closes the ADR-0009 follow-up)

[ADR-0005](../adr/0005-web-hosting-render-model.md) chose a **standalone WASM PWA** — which means
there is no server-side host to run a BFF without introducing one. So:

- (a) **Bearer token in the WASM client** — matches ADR-0009 and ADR-0005. Accept the XSS exposure;
  mitigate with short-lived access tokens, refresh-token rotation, and a strict CSP. *Recommended.*
- (b) Introduce a BFF/cookie host — safer against XSS, but reopens ADR-0005 and adds a server the
  architecture deliberately avoided.

> **Decided: (a) bearer token in the WASM client.** Consistent with
> [ADR-0009](../adr/0009-authentication-and-passkeys.md) and
> [ADR-0005](../adr/0005-web-hosting-render-model.md); introducing a BFF would reopen a settled
> hosting decision to add exactly the server the architecture avoids. The XSS exposure is accepted
> and **mitigated**: short-lived access tokens, refresh-token rotation, and a strict CSP — all of
> which the web-token-model ADR must specify concretely rather than gesture at. This closes the
> follow-up left open in ADR-0009.

### Proposed slicing (each becomes a FEAT spec)

1. **M1 — Identity in Main:** signup, login, passkey registration, profile, deletion & export.
2. **M2 — Circle:** create, label, roles, invitations (D5), join/accept, managed members (D7).
3. **M3 — Secret-Santa sync:** account-scoped storage, sync mechanism, claim flow (D4).
4. **M4 — Linking:** group-from-circle (D2), member↔user (D3), in-app reveal (D1).

M1–M3 are independently shippable; M4 is where the interesting product value lands.

## Decisions & selected ideas

**Session concluded 2026-07-27.** The pivotal decisions, in one place:

| # | Question | Decision |
|---|----------|----------|
| **D1** | What the server knows about a draw | **P1 — plaintext + authorization.** Assignments in Postgres; the API returns only the caller’s own row. Operator is a trusted party, stated openly. P3 (envelope encryption) parked as future hardening. **Amends NFR-002; partly supersedes ADR-0014.** |
| **D2** | Circle membership vs group snapshot | **Snapshot at draw time.** A completed draw is history and stays reproducible; the UI must say that drawing fixes the participant list. |
| **D3** | What a `Member` is once accounts exist | **`Member` keeps its identity and gains an optional `UserId`.** Unlinked members stay first-class. |
| **D4** | Anonymous → account claim | **Choose per group** on first login. Local data kept as a cache afterwards, not cleared. |
| **D5** | Invitations & account enumeration | **Never reveal account existence to the inviter.** Always “invitation sent”; the branch resolves on the recipient’s side. Expiry, resend/revoke, per-inviter rate limits. |
| **D6** | Term for the shared circle | **One concept; canonical code name `Circle`; user-chosen UI label** defaulting to “Family”. `Group` stays reserved for Secret-Santa. |
| **D7** | Managed members without an email | **In the v2 model**, even if the UI ships later. |
| **D8** | Web auth token model | **Bearer token in the WASM client**, mitigated by short-lived access tokens, refresh rotation and a strict CSP. ADR-0005 not reopened; closes the ADR-0009 follow-up. |

What was already settled going in, and still stands:

- **v2 = central account + Circle + Secret-Santa sync**, sliced as M1–M4 above.
- **An account stays optional.** The offline, account-free flow keeps working unchanged, including
  reveal links for members with no account. NFR-001 is not weakened.
- **Identity stack is not reopened** — ASP.NET Core Identity + built-in passkeys + bearer tokens per
  [ADR-0009](../adr/0009-authentication-and-passkeys.md); the token model is now closed too (D8).
- **Sync starting policy** is the interim stance already recorded in the ADR backlog: aggregate =
  sync unit, per-aggregate version token + tombstones, delta pull/push, last-write-wins with an
  escalation hook.
- **Persistence:** Postgres via EF Core per [ADR-0007](../adr/0007-persistence-approach.md); the
  client keeps its `IGroupRepository` port from [ADR-0015](../adr/0015-mvp-client-offline-storage.md),
  with a syncing implementation behind it.

## Rejected / parked (for the record)

- **Social login (Google/Apple)** — rejected for v2: external dependency and data-residency
  questions, and it dilutes the single-suite-account model.
- **Magic-link login** — parked; passkeys already serve the passwordless case.
- **Account merging** — parked; too rare to design for now.
- **Push notifications** — parked; transactional email covers v2, push needs native/store work.
- **Automatic background sync** — parked; sync on start and on change is sufficient at this scale.
- **Conflict-resolution UI** — parked; ship last-write-wins and see whether collisions ever happen.
- **Invite by link/code** — parked; revisit if email delivery turns out to be the friction point.

## Follow-up actions

Glossary first — no code may use these terms before they exist (`CLAUDE.md` rule 7).

- [x] Resolve **D1–D8** and record the outcomes above; set `status: Concluded`.
- [x] Glossary entries added → [glossary](../glossary/index.md):
      [User](../glossary/user.md) (Account as synonym), [Circle](../glossary/circle.md) (D6 winner),
      [Circle membership](../glossary/circle-membership.md), [Invitation](../glossary/invitation.md),
      [Managed member](../glossary/managed-member.md), [Sync](../glossary/sync.md);
      [Member](../glossary/member.md) updated with the optional user link (D3), and
      [Group](../glossary/group.md) corrected — “circle” moved from *synonym* to *do-not-use*.
      **Bounded-context labels (`Identity`, `Circles`, `Platform`) are provisional** until arc42
      ch. 5 formalizes them.
- [x] Use cases (Main, **M1 — Identity**) written and registered (2026-07-27):
      [UC-MAIN-001](../use-cases/main/UC-MAIN-001-create-account.md) create a suite account ·
      [UC-MAIN-002](../use-cases/main/UC-MAIN-002-sign-in-with-password.md) sign in with email &
      password · [UC-MAIN-003](../use-cases/main/UC-MAIN-003-register-passkey.md) register a passkey ·
      [UC-MAIN-004](../use-cases/main/UC-MAIN-004-sign-in-with-passkey.md) sign in with a passkey ·
      [UC-MAIN-005](../use-cases/main/UC-MAIN-005-manage-profile.md) profile & preferred language ·
      [UC-MAIN-006](../use-cases/main/UC-MAIN-006-delete-account-and-export-data.md) delete account &
      export data · [UC-MAIN-007](../use-cases/main/UC-MAIN-007-reset-password.md) reset a forgotten
      password. Their open questions were resolved the same day; deletion & export became
      [ADR-0022](../adr/0022-account-deletion-and-data-export.md).
- [ ] Use cases (Main, **M2 — Circle**): create a circle; invite a new person; invite an existing
      account; accept/decline; leave/remove → `UC-MAIN-007…`.
- [ ] Use cases (Secret-Santa): create a group from a circle; link a member to an account; see my
      assignment while logged in; edit my own wishlist → `UC-SS-007…`.
- [x] **Amend [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)** per D1 —
      the “zero assignments server-side” target now scopes to account-free mode; account mode gets an
      authorization target with its own acceptance criteria.
- [ ] Requirements: FRs per use case; new NFRs for data protection & residency,
      account-enumeration resistance, and sync convergence → [requirements](../requirements/index.md).
- [x] ADRs written and **Accepted** (2026-07-27):
      [ADR-0016](../adr/0016-sync-and-conflict-resolution.md) sync & conflict resolution (incl. the
      client-storage sub-decision) · [ADR-0017](../adr/0017-anonymous-to-account-claim.md)
      anonymous→account claim (D4) · [ADR-0018](../adr/0018-circle-and-sharing-model.md)
      circle & sharing model (D2, D5, D6, D7) · [ADR-0019](../adr/0019-member-user-linking.md)
      Member↔User linking (D3) · [ADR-0020](../adr/0020-server-side-assignment-privacy.md)
      server-side assignment privacy (D1), partly superseding
      [ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md) ·
      [ADR-0021](../adr/0021-web-auth-token-model.md) web token model (D8).
- [x] arc42 **ch. 8** (§8.2 persistence, §8.3 sync, §8.4 identity/tokens, §8.8 privacy &
      enumeration resistance, new §8.10 circles & sharing) and **ch. 9** (register + an
      *Amended / partly superseded* table) updated.
- [ ] arc42 **ch. 5**: add the **Identity** and **Circles** bounded contexts to the
      [building-block view](../arc42/05-building-block-view.md), confirming the provisional context
      labels used in the glossary.
- [ ] Deployment-phase ADRs now genuinely due: cloud provider & hosting (Swiss data residency),
      transactional email provider, CI/CD → [deployment](../deployment/index.md).
- [x] Update [backlog.md](../roadmap/backlog.md): `IDEA-000` promoted to *in progress*, and the
      Circle idea stub (`IDEA-002`) added — it was not captured anywhere before.
