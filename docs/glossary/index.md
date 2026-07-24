# Glossary — Ubiquitous Language

This glossary is the **single source of truth** for domain terms. Names in code (aggregates,
entities, properties, DB tables, API DTOs) **must** match the canonical English term here. If a
term is missing, add it (copy [`_template-glossary-entry.md`](_template-glossary-entry.md))
before using it in code.

Terms may carry different meanings in different [bounded contexts](../arc42/05-building-block-view.md);
note that nuance in the entry. Product display translations (DE/EN/FR) are listed for reference
only — they are **not** code names.

## Terms

| Term | Context | Definition |
|------|---------|------------|
| [Assignment](assignment.md) | Secret-Santa | A single giver→recipient pairing produced by a draw. |
| [Budget](budget.md) | Secret-Santa | The agreed spending limit for gifts in a group. |
| [Draw](draw.md) | Secret-Santa | The act/result of randomly assigning gift-givers to recipients under the draw rules. |
| [Event details](event-details.md) | Secret-Santa | A group's occasion info: date, location, notes. |
| [Exclusion](exclusion.md) | Secret-Santa | A one-directional rule preventing a giver from being matched to a recipient. |
| [Group](group.md) | Secret-Santa | A set of participants who exchange gifts together. |
| [Member](member.md) | Secret-Santa | A person participating in a group. |
| [Reveal link](reveal-link.md) | Secret-Santa | A self-contained encoded link that reveals one giver's assignment client-side. |
| [Wishlist](wishlist.md) | Secret-Santa | A member's optional list of gift ideas. |

_Keep this table sorted alphabetically as terms are added._
