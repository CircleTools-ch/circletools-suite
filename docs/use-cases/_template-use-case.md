---
id: UC-XX-000         # UC-<APP>-NNN, e.g. UC-SS-001
title: <goal-oriented title>
app: <app name>
actors: [<primary actor>, <supporting actors>]
status: Draft         # Draft | Reviewed | Approved
priority: Must        # MoSCoW
related: []           # FR-…, NFR-…, FEAT-…, ADR-…
---

# {{id}} — {{title}}

## Summary / goal

One or two sentences: what the actor wants to accomplish and why.

## Primary actor & stakeholders

- **Primary actor:** <who initiates>
- **Stakeholders & interests:** <who else cares and what they need>

## Preconditions

- <what must be true before the use case starts>

## Postconditions

- **Success:** <state of the world after success>
- **Failure:** <state after failure>

## Trigger

<what starts the use case>

## Main success scenario

1. <step>
2. <step>
3. <step>

## Extensions / alternate flows

- **1a.** <condition> → <handling>
- **3a.** <condition> → <handling>

## Exceptions

- **<error condition>** → <how the system responds>

## Non-functional notes

- **Offline/local-first:** <behaviour with no account/connection>
- **Localization:** <DE/EN/FR considerations>
- **Privacy:** <personal data touched>
- **Performance:** <any responsiveness expectations>

## Open questions

- <unresolved question — may become an ADR or brainstorming topic>

## Traceability

- Requirements: <FR-… / NFR-…>
- Feature spec: <FEAT-…>
- Glossary terms: <links to glossary entries used here>
