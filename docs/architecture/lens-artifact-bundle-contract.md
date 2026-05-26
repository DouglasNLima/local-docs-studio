# Lens Artefact Bundle Contract

## Purpose

`lens-artifact-bundle.json` is an optional ZIP manifest for Lens-family tools and other compatible generators. Lens Docs Studio reads it only during ZIP import to improve navigation and reader context. ZIPs without this manifest remain ordinary Markdown, Mermaid, or Markdown Bundle imports.

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

## Path And Privacy Rules

Manifest paths are ZIP-relative only. Readers must reject absolute paths, drive roots, path traversal, empty paths, control characters, and URL-like schemes. Metadata may only link to Markdown/Mermaid records actually imported from the ZIP, and raw local paths must not be exposed.

The manifest must never cause external network calls, service calls, account flows, telemetry, remote storage, or direct links to Lens tools. Manifest text must be escaped or assigned with text-only DOM APIs before display. Lens artefact metadata is session-only in this phase and must not be written to localStorage or IndexedDB.
