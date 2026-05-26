# Lens Docs Studio Identity Decision

## Decision

The app is renamed to Lens Docs Studio while remaining a generic local Markdown, Mermaid, and documentation studio. The product promise is local-first authoring, preview, review, import, and export for documentation files, not a tool exclusive to the Power Platform Lens family.

The reusable tagline is:

> Local Markdown, Mermaid, and documentation studio

## Visual Direction

Lens Docs Studio uses the Lens family accent `#FF883E` for restrained brand moments, primary actions, selected states, active indicators, and focus states. The main surfaces should stay neutral and compact, using `#FAFAFA`, `#F8F9FB`, `#F3F4F6`, `#EDEDED`, and high-contrast text.

## Compatibility Boundary

Lens Docs Studio supports optional Lens artefact bundle metadata during ZIP import, but that compatibility does not change the product identity. The main shell, tagline, manifest description, welcome state, and README introduction remain generic for local Markdown, Mermaid, and documentation workflows.

Artefact bundle metadata is passive, untrusted, session-only context. It must not add direct links or integrations to other Lens tools, call external services, persist new browser-local keys, infer evidence levels, or convert candidate findings into confirmed findings. Existing browser-local persistence keys stay unchanged so saved user preferences, drafts, snapshots, recent handles, and local library data remain available after the rename.
