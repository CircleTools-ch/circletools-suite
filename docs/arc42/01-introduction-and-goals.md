# 1. Introduction and Goals

> Architecture documentation follows the [arc42](https://arc42.org) template. Chapters are
> living documents; sections marked _(proposed)_ are drafts awaiting ratification via
> [ADRs](09-architecture-decisions.md).

## 1.1 Requirements overview

`<Suite>` is an App-Suite for **families and groups of friends**: one brand, one login, shared
configuration, and a global dashboard over multiple bundled apps. The first app is a
**Secret-Santa** app.

Core capabilities:

- Each app is usable **standalone and offline**, with **no account and no backend** (data stored
  locally in SQLite).
- Users may optionally **create an account** (email + password, optional passkeys) to **sync**
  their data across devices via a Postgres-backed API.
- Every app ships as **native Android & iOS** (MAUI Blazor Hybrid, phone + tablet) and a
  responsive **Web** app (Blazor), sharing feature code.
- The product is **multilingual** (German, French, English; `de-CH` default). Code and
  documentation are **English**.

## 1.2 Quality goals

| # | Quality goal | Motivation |
|---|--------------|------------|
| Q1 | **Local-first reliability** | The app must fully work offline with no account; sync is additive, never required. |
| Q2 | **Cross-platform feature parity** | Web and native share the same features and code. |
| Q3 | **Privacy by design** | Swiss-first, family data; minimize and protect personal data. |
| Q4 | **Maintainability & evolvability** | DDD boundaries so apps can be added and services extracted later. |
| Q5 | **Localizability** | DE/EN/FR from the start, `de-CH` conventions. |

_See detailed scenarios in [chapter 10](10-quality-requirements.md)._

## 1.3 Stakeholders

| Role | Expectations |
|------|--------------|
| Product owner / developer (Adrian) | Professional, well-documented, evolvable codebase |
| End users (families & friends, Swiss-first) | Simple, delightful, private, works offline & in their language |
| Future contributors (human & AI) | Clear process, guidelines, and decision history |
| App stores / hosting provider | Compliant, deployable artifacts |
