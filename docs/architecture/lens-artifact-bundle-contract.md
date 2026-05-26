# Lens Artefact Bundle Contract

## Purpose

`lens-artifact-bundle.json` is an optional ZIP manifest for Lens-family tools and other compatible generators. Lens Docs Studio reads it during ZIP import to improve navigation and reader context. ZIPs without this manifest remain ordinary Markdown, Mermaid, or Markdown Bundle imports.

The manifest is metadata, not authority. Lens Docs Studio displays supplied evidence labels and warnings, but does not validate, confirm, upgrade, downgrade, infer, or analyse findings.

## Version

Only `lens-artifact-bundle-1.0` is supported. Readers must be tolerant: unsupported versions, malformed manifests, oversized manifests, invalid fields, missing paths, and unsafe paths must not block safe Markdown/Mermaid import.

```json
{
  "formatVersion": "lens-artifact-bundle-1.0",
  "sourceTool": "Security Lens",
  "sourceToolVersion": "1.0.0",
  "generatedAtUtc": "2026-05-26T00:00:00.000Z",
  "entryDocument": "README.md",
  "title": "Security Lens Evidence Pack",
  "summary": "Optional short description.",
  "evidenceLevels": [
    "static evidence",
    "connected metadata evidence",
    "candidate finding",
    "confirmed finding",
    "blocked/unavailable evidence"
  ],
  "documents": [
    {
      "path": "README.md",
      "title": "Overview",
      "kind": "summary",
      "evidenceLevel": "static evidence",
      "order": 10
    }
  ],
  "diagrams": [
    {
      "path": "diagrams/security-overview.mmd",
      "title": "Security Overview",
      "kind": "mermaid",
      "evidenceLevel": "static evidence",
      "order": 20
    }
  ],
  "warnings": [
    {
      "code": "CONNECTED_METADATA_UNAVAILABLE",
      "message": "Connected metadata was unavailable during export.",
      "evidenceLevel": "blocked/unavailable evidence"
    }
  ]
}
```

## Fields

- `formatVersion`: required. Must be `lens-artifact-bundle-1.0`.
- `sourceTool`: optional display text from the generator. Treat as untrusted text.
- `sourceToolVersion`: optional display text from the generator. Treat as untrusted text.
- `generatedAtUtc`: optional ISO UTC timestamp. Invalid values are unavailable.
- `entryDocument`: optional ZIP-relative path. If safe and present in imported records, open it after import.
- `title`: optional display title.
- `summary`: optional display summary.
- `evidenceLevels`: optional list of labels used by the pack.
- `documents`: optional metadata for Markdown or Mermaid documents.
- `diagrams`: optional metadata for Mermaid diagrams.
- `warnings`: optional metadata-only warnings supplied by the generator.

Supported evidence labels are:

- `static evidence`
- `connected metadata evidence`
- `runtime evidence`
- `manual evidence`
- `candidate finding`
- `confirmed finding`
- `blocked/unavailable evidence`

Candidate findings remain candidate findings unless the manifest explicitly supplies `confirmed finding`. Lens Docs Studio must not claim runtime proof, environment analysis, or evidence confirmation from this metadata.

## Reader Panel

When a valid manifest is loaded, Lens Docs Studio may show a compact artefact reader panel. The panel may display safe title, source tool, source version, generated UTC, entry document, counts, evidence chips, warnings, and grouped navigation for imported records only.

Reader panel navigation must use validated metadata paths that correspond to imported Markdown or Mermaid records. Reader filters for evidence, kind, and search are session-only UI state. They must not affect generic file search, modify documents, write to localStorage, write to IndexedDB, or survive a workspace change.

The panel must include these notes when applicable:

- Evidence labels are displayed as supplied by the bundle.
- Candidate findings remain candidate findings.

Manifest-supplied text must be assigned with text-only DOM APIs or escaped before display. The reader must not create external links from manifest metadata.

## Export Profiles And Sites

Built-in export profiles are session-only presets. Applying them may adjust in-memory export defaults, but it must not persist artefact metadata or write the existing Azure DevOps Mermaid preference key. Saved local export profiles continue using the existing local library storage key.

Docs Site export may use safe artefact metadata as display-only hints when a bundle is loaded. Markdown front matter remains highest priority, followed by safe artefact metadata, then inferred title/path defaults. Safe bundle display metadata may be written to `site-manifest.json` under `artifactBundle`; rejected paths, unsafe URLs, malformed metadata, and untrusted raw manifest content must not be exported.

Generic Markdown Bundle export must not include `lens-artifact-bundle.json`. Artefact review bundle export is explicit through the artefact review pack action. That action may rebuild a safe normalised `lens-artifact-bundle.json` from current imported records and safe session metadata, including `reviewedWith: "Lens Docs Studio"`. It must not claim evidence validation, create findings, change candidate findings to confirmed findings, or persist metadata after export.

## Path And Privacy Rules

Manifest paths are ZIP-relative only. Readers must reject absolute paths, drive roots, path traversal, empty paths, control characters, and URL-like schemes. Metadata may only link to Markdown/Mermaid records actually imported from the ZIP, and raw local paths must not be exposed.

The manifest must never cause external network calls, service calls, account flows, telemetry, remote storage, or direct links to Lens tools. Manifest text must be escaped or assigned with text-only DOM APIs before display. Lens artefact metadata is session-only unless the user explicitly exports an artefact review pack, and it must not be written to localStorage or IndexedDB.
