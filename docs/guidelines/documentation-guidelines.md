# Documentation Guidelines

> **Status: outline.**

## 1. Docs-as-code
- All docs are Markdown in `docs/`, reviewed in PRs, published via DocFX ([ADR-0002](../adr/0002-use-arc42-c4-docfx.md)).
- English only.

## 2. Structure
- Follow the folder conventions: every folder has `index.md` + `toc.yml`; templates prefixed `_template-`.
- Use the right artifact template (the artifact/template map is in the repository `CLAUDE.md`);
  keep the traceability chain by ID.

## 3. Diagrams
- **Mermaid** by default — fenced ` ```mermaid ` blocks, rendered natively by DocFX.
- **PlantUML / C4-PlantUML** for rigorous C4 diagrams: author `.puml` in `docs/diagrams/c4/`,
  **pre-render to SVG** into `docs/diagrams/exported/`, embed the SVG ([ADR-0004](../adr/0004-plantuml-prerender.md)).

### Pre-render command (PlantUML)
```bash
# Requires the PlantUML CLI (plantuml.jar) + Graphviz
java -jar plantuml.jar -tsvg -o ../exported docs/diagrams/c4/*.puml
```

## 4. Links & building
- Use relative links; build locally with `dotnet docfx docfx/docfx.json` and check the log for
  broken links/xrefs before opening a PR.

## 5. Style
- Short sentences, tables over prose where it helps, code fences with language hints, no marketing
  fluff. Write for a future contributor who wasn't in the room.
