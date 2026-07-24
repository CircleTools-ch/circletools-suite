# 2. Architecture Constraints

## 2.1 Technical constraints

| Constraint | Notes |
|------------|-------|
| **.NET 10** | Fixed platform for all projects. |
| **.NET Aspire** | Orchestration and local composition. |
| **Domain-Driven Design** | Strategic + tactical; enforced by architecture tests. |
| **PostgreSQL** (server) + **SQLite** (on-device) | Backend and local storage respectively. |
| **Blazor** (Web) + **MAUI Blazor Hybrid** (native) | Shared feature code via Razor Class Libraries. |
| **Single solution** | Everything lives in one `.slnx`. |
| **Central Package Management** | All package versions in `Directory.Packages.props`. |

## 2.2 Organizational & process constraints

| Constraint | Notes |
|------------|-------|
| **Docs-as-code** | All documentation is Markdown in the repo, published via DocFX. |
| **Process-driven** | Work follows the [ways of working](../process/ways-of-working.md). |
| **English artifacts** | Code, comments, identifiers, DB tables, docs — all English. |
| **Decisions as ADRs** | Significant choices recorded before implementation. |

## 2.3 Conventions

- Coding & architecture per the [guidelines](../guidelines/coding-guidelines.md).
- Ubiquitous language per the [glossary](../glossary/index.md).
- `Walf` (We Are Like Family) is the suite brand and namespace/project prefix (see
  [naming](../process/naming.md); full contributor guidance is in the repository `CLAUDE.md`).
