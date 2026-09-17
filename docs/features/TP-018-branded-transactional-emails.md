---
id: TP-018
feature: FEAT-018
status: Draft
related: [FR-047, FR-007, FR-009, FR-013, FR-017, FR-028, NFR-003, NFR-005, NFR-006, ADR-0064]
---

# TP-018 — Test plan for FEAT-018

## Scope & objectives

Verify that transactional mail renders as branded HTML with a plaintext fallback, is branded and linked
to the correct **source app**, keeps its security properties (no link in the enumeration notices, no
secret in the has-account invitation, user text encoded), preserves DE/EN/FR, and that account deletion
sends exactly one link-less farewell. See [FEAT-018](FEAT-018-branded-transactional-emails.md) /
[ADR-0064](../adr/0064-transactional-email-html-and-per-app-branding.md).

## Test levels

- **Unit** — `MailTemplateRenderer` + `MailLayout`: subjects/bodies per language, HTML CTA/href/fallback,
  per-`sourceApp` branding, default fallback, farewell, HTML-encoding.
- **Contract** — `OutboxInvitationMailer` payload still renders (adapter ↔ template JSON shape).
- **Integration** — sign-up / reset / delete against the API + Mailpit (Testcontainers), asserting a mail
  arrives with the expected subject and, for delete, that a farewell follows a successful deletion.
- **Exploratory / manual** — a live Mailpit and real-client (Gmail/Outlook/Apple Mail) visual pass across
  DE/EN/FR and both apps.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| ADR-0064 (HTML) | CTA `href` equals the exact link; fallback link + "Button not working?" present | Unit | ✅ |
| ADR-0064 (branding) | `sourceApp=secretsanta` → red `#7a1f2b`, "Secret Santa · CircleTools" sender, secretsanta origin | Unit | ✅ |
| ADR-0064 (branding) | `sourceApp=circletools` → navy `#03173d`, CircleTools sender, circletools origin | Unit | ✅ |
| ADR-0064 (fallback) | unknown `sourceApp` → default (CircleTools) branding + origin | Unit | ✅ |
| NFR-006 | user-supplied circle name with markup is HTML-encoded (no `<script>`) | Unit | ✅ |
| NFR-005 | sign-up-attempt / email-change-notice carry no link | Unit | ✅ |
| NFR-006 | has-account invitation carries no secret / `/invitation/` | Unit | ✅ |
| FR-016 | circle label rendered verbatim, not re-translated | Unit | ✅ |
| NFR-003 | verification/invitation subjects render in DE/EN/FR | Unit | ✅ |
| FR-047 | farewell renders per language with no link (`http`/`https` absent from body) | Unit | ✅ |
| FR-047 | delete → exactly one farewell queued after success; none on failure | Integration | ⛌ (owed) |
| FR-007/009/013 | live mail arrives with the expected subject (Mailpit) | Integration | ✅ (existing) |
| ADR-0064 | branded HTML reads correctly in a real client, both apps, DE/EN/FR | Exploratory | ⛌ (owed) |

## Test data & environments

- Unit: `MailOptions` with an `Apps` map for `secretsanta` + `circletools`; payloads built inline.
- Integration: `MailApiFactory` (Postgres + Mailpit via Testcontainers), as the existing account tests use.

## Localization & accessibility notes

- Verify DE/EN/FR subject + body + CTA + footer for each type; user data is never translated.
- HTML mail uses semantic-enough structure with adequate colour contrast (white on brand colour, dark on
  white); a plaintext part is always present for screen-reader/text clients.

## Exit criteria

- [x] Unit + contract tests green (`MailTemplateRendererTests`, `OutboxInvitationMailerTests`)
- [x] Live Mailpit **pipeline** pass (2026-09-17): 12 variants delivered through the real render + SMTP path;
      branding, CTA, localized fallback, plaintext part, DE/EN/FR, farewell and encoding verified from Mailpit
- [ ] Integration assertion for the farewell-on-delete added and green
- [ ] Human visual eyeball (Mailpit UI) + one real external client (Gmail/Outlook/Apple Mail)
- [ ] CI green
