---
id: FR-000            # FR-NNN (functional) or NFR-NNN (non-functional)
title: <short imperative title>
type: functional      # functional | non-functional
status: Draft         # Draft | Reviewed | Approved | Implemented | Dropped
priority: Must        # MoSCoW: Must | Should | Could | Won't
source: <use case / brainstorming / stakeholder>
verification: <test | review | demo | measurement>
related: []           # UC-…, FEAT-…, ADR-…, other FR/NFR
---

# {{id}} — {{title}}

## Description

A single, testable statement of what the system must do (functional) or a quality it must
exhibit (non-functional). One requirement per file.

## Rationale

Why this is required — the value or constraint behind it.

## Acceptance criteria

```gherkin
Given <context>
When <action>
Then <observable outcome>
```

<!-- For NON-FUNCTIONAL requirements, also fill in: -->
## Quality attribute *(NFR only)*

- **Attribute:** <performance | security | usability | reliability | privacy | i18n | …>
- **Metric & target:** <measurable target, e.g. "p95 draw completes < 200 ms on mid-range phone">
- **Measurement approach:** <how it is measured>

## Dependencies & related

- Derived from: <UC-…>
- Related: <FR-… / NFR-… / ADR-…>
