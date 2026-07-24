# Naming — Walf & Apps

## Decision (2026-07-24)

The suite brand name is **Walf** — a backronym for **W**e **A**re **L**ike **F**amily, chosen to
include *friends-as-family* alongside families. `Walf` is now the namespace/project prefix and the
product name in prose; the former `Suite` / `<Suite>` placeholder has been replaced repo-wide.

- **Domain:** `walf.ch` (confirmed available via RDAP + DNS; register at a Swiss registrar before
  relying on it). `walf.com` is owned by a third party and was not secured.
- **App domains:** apps are **subdomains** of the suite domain — e.g. `secretsanta.walf.ch`.
- **Accepted trade-offs:** the "W" reads as "V" in German/French (≈ *valf*), so pronunciation is
  not identical across DE/EN/FR; the name also echoes "Wolf".

## Decisions taken
- **Language:** English names.
- **TLD:** `.ch`-first (Swiss audience); matching `.com` is a nice-to-have, not required.
- **App domains:** apps are **subdomains** of the suite domain (e.g. `secretsanta.walf.ch`), so
  only **one** suite domain is needed. Per-app brand names are decided during each app's idea step.

## Suite name candidates (historical — from the harness-design search)

> Availability was checked via RDAP and reflects "not currently registered". **Confirm at a Swiss
> registrar** (e.g. Infomaniak, Hostpoint, cyon) before relying on it.

| Candidate | `.ch` | Feel |
|-----------|:-----:|------|
| FamNest | free | family + nest; very easy for German speakers |
| OurNest | free | our + nest; warm |
| FamHaus | free | family + Haus; most German-transparent |
| KinHaus | free | kin + Haus |
| WeTribe | free | echoes "We Are Like Family"; family + friends |
| OurTribe | free | your people (family + friends) |
| OurFam | free | our family |
| Kinvo | free | coined, clean, flexible |
| Famveo | free | coined |
| Famoji | free | playful |
| Hearthly | free | warm, hearth/home |
| KinHub | free | a hub for your kin |

_Taken (for the record): Famigos/Famigo, Tribely, Framily, Kinly, Nestly, Familio, and most short
single-word `.ch`/`.com` names._

## Done
Name chosen (**Walf**) and the `Suite` / `<Suite>` placeholders replaced repo-wide. Remaining
external action: register `walf.ch` at a Swiss registrar (and optionally `walf.app` / handles).
