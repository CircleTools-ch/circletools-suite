# Coding Guidelines (.NET 10 / C# + DDD tactical)

> **Status: outline.** Sections are filled in as the corresponding code is written. The
> `.editorconfig` and analyzers are the *enforceable* source of truth; this document explains
> intent and the parts tools can't check.

## 1. Purpose & scope
Applies to all C# in `src/` and `tests/`. Where this text and `.editorconfig` disagree, fix the
mismatch — they must agree.

## 2. Language & compiler settings
- `Nullable` enabled everywhere; no per-project opt-out.
- Implicit usings; `LangVersion=latest`; warnings-as-errors.
- File-scoped namespaces; `using` directives outside the namespace, `System.*` first.

## 3. Project & dependency conventions
- **Central Package Management** — add versions only in `Directory.Packages.props`.
- Folder-per-feature within a project; one public type per file (pragmatic exceptions allowed).

## 4. Naming (English)
- PascalCase types/members; `_camelCase` private fields; `I`-prefixed interfaces; `Async` suffix
  on async methods. Names match the [glossary](../glossary/index.md).

## 5. Modern C# & immutability
- Prefer `record`/`readonly struct`, primary constructors, `required` members, pattern matching,
  collection expressions, expression-bodied members where they read well.

## 6. Nullability & defensive coding
- Guard clauses; `ArgumentNullException.ThrowIfNull`; avoid returning null collections.

## 7. Error handling
- Domain exceptions for invariant violations; `Result`-style returns for expected failures; no
  control-flow-by-exception; API surfaces `ProblemDetails`.

## 8. Async
- `CancellationToken` on all I/O; no `async void`; `IAsyncEnumerable` for streaming; avoid sync-over-async.

## 9. Dependency injection
- Constructor injection; options pattern; no service locator; register in the host composition root.

## 10. Logging & observability
- Structured logging; OpenTelemetry via `ServiceDefaults`; no PII in logs.

## 11. DDD tactical patterns
- Entities, value objects, aggregates (roots enforce invariants), domain events, repositories,
  factories; **pure domain** (no EF/HTTP/ASP.NET/MAUI). DTO↔domain mapping in the application layer.

## 12. XML documentation
- Public APIs carry XML doc comments (feeds the DocFX API reference).

## 13. Testability
- No statics/singletons for logic; inject clock, ids, and randomness.

## 14. Security in code
- Validate inputs at boundaries; never commit secrets; minimize PII. See
  [security & privacy](security-privacy-guidelines.md).

## 15. Anti-patterns (avoid)
- Anemic domain leaking logic into services; primitive obsession; leaking infrastructure into the
  domain; packages added outside CPM.
