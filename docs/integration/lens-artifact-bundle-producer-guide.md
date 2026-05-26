# Lens Artefact Bundle Producer Guide

This guide is for future tools that want to generate ZIPs that Lens Docs Studio can import as ordinary editable Markdown and Mermaid files with optional reader metadata.

Lens Docs Studio displays metadata from `lens-artifact-bundle.json` as supplied. It does not validate evidence, confirm findings, upgrade candidate findings, downgrade confirmed findings, infer runtime evidence, call external services, or persist artefact metadata. The contract version remains `lens-artifact-bundle-1.0`.

## Purpose

`lens-artifact-bundle.json` helps a static reader understand which Markdown and Mermaid records are summaries, findings, diagrams, ADRs, release notes, runbooks, or warnings. The manifest is optional. A ZIP without it must still work as a generic Markdown/Mermaid ZIP.

The manifest is untrusted display metadata. Producers are responsible for the wording they supply, especially evidence labels and finding status.

## Minimal Manifest

```json
{
  "formatVersion": "lens-artifact-bundle-1.0",
  "entryDocument": "README.md",
  "title": "Example Review Pack",
  "documents": [
    {
      "path": "README.md",
      "title": "Overview",
      "kind": "summary",
      "evidenceLevel": "static evidence",
      "order": 10
    }
  ]
}
```

## Rich Manifest

```json
{
  "formatVersion": "lens-artifact-bundle-1.0",
  "sourceTool": "Security Lens",
  "sourceToolVersion": "4.2.0",
  "generatedAtUtc": "2026-05-26T12:34:56.000Z",
  "entryDocument": "README.md",
  "title": "Security Review Pack",
  "summary": "Static review notes for local documentation review.",
  "evidenceLevels": [
    "static evidence",
    "connected metadata evidence",
    "runtime evidence",
    "manual evidence",
    "candidate finding",
    "confirmed finding",
    "blocked/unavailable evidence"
  ],
  "documents": [
    {
      "path": "summary/executive-summary.md",
      "title": "Executive Summary",
      "kind": "summary",
      "evidenceLevel": "connected metadata evidence",
      "order": 20
    },
    {
      "path": "findings/candidate-risk.md",
      "title": "Candidate Risk",
      "kind": "finding",
      "evidenceLevel": "candidate finding",
      "order": 30
    },
    {
      "path": "findings/confirmed-control.md",
      "title": "Confirmed Control",
      "kind": "finding",
      "evidenceLevel": "confirmed finding",
      "order": 40
    },
    {
      "path": "adr/adr-001.md",
      "title": "ADR 001",
      "kind": "adr",
      "evidenceLevel": "static evidence",
      "order": 50
    },
    {
      "path": "release-notes/v1.md",
      "title": "Release Notes",
      "kind": "release notes",
      "evidenceLevel": "runtime evidence",
      "order": 60
    },
    {
      "path": "docs/runbook.md",
      "title": "Operations Runbook",
      "kind": "runbook",
      "evidenceLevel": "manual evidence",
      "order": 70
    }
  ],
  "diagrams": [
    {
      "path": "diagrams/trigger-timeline.mmd",
      "title": "Trigger Timeline",
      "kind": "mermaid",
      "evidenceLevel": "static evidence",
      "order": 45
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

- `formatVersion`: required. Use `lens-artifact-bundle-1.0`.
- `sourceTool`: optional producer display name.
- `sourceToolVersion`: optional producer version display text.
- `generatedAtUtc`: optional UTC timestamp ending in `Z`.
- `entryDocument`: optional ZIP-relative Markdown or Mermaid path to open first.
- `title`: optional pack title.
- `summary`: optional short pack summary.
- `evidenceLevels`: optional list of labels used in the pack.
- `documents`: optional metadata for Markdown documents.
- `diagrams`: optional metadata for Mermaid diagrams.
- `warnings`: optional metadata-only warnings supplied by the producer.

Document and diagram entries support:

- `path`: ZIP-relative path to an imported `.md`, `.markdown`, `.mmd`, or `.mermaid` file.
- `title`: display title.
- `kind`: display grouping hint, such as `summary`, `finding`, `mermaid`, `adr`, `release notes`, `runbook`, or `warning`.
- `evidenceLevel`: one supported label.
- `order`: numeric display order.

## Evidence Wording

Supported evidence labels are:

- `static evidence`: produced from static files or deterministic local inspection.
- `connected metadata evidence`: produced from metadata the producer already held.
- `runtime evidence`: produced from runtime information available to the producer.
- `manual evidence`: supplied or checked by a person outside Lens Docs Studio.
- `candidate finding`: a possible issue that has not been confirmed by the producer.
- `confirmed finding`: a finding explicitly confirmed by the producer before export.
- `blocked/unavailable evidence`: evidence the producer could not gather.

Do not label a candidate finding as `confirmed finding` unless the producing tool really confirmed it. Lens Docs Studio will display the label, not verify it.

## Path Safety

All manifest paths must be ZIP-relative paths to records included in the same ZIP. Use forward slashes.

Do not emit:

- Absolute paths such as `/absolute.md`.
- Drive-rooted paths such as `C:\secrets.md`.
- Traversal paths such as `../secrets.md`.
- URL-like paths such as `https://example.com/file.md`.
- Empty paths or paths with control characters.
- Paths to files that are not part of the ZIP.

Unsafe or missing metadata paths are ignored by Lens Docs Studio. Safe Markdown and Mermaid files still import.

## Recommended ZIP Structure

```text
README.md
summary/executive-summary.md
findings/candidate-risk.md
findings/confirmed-control.md
diagrams/trigger-timeline.mmd
adr/adr-001.md
release-notes/v1.md
docs/runbook.md
assets/tiny.png
lens-artifact-bundle.json
```

Use clear, stable file names. Prefer lower-case folders, hyphenated file names, and a top-level `README.md` as the entry document.

## Representing Common Records

- Summary pages: Markdown in `README.md` or `summary/`, `kind: "summary"`.
- Findings: Markdown in `findings/`, `kind: "finding"`, with explicit candidate or confirmed labels.
- Mermaid diagrams: `.mmd` or `.mermaid` files in `diagrams/`, listed in `diagrams`.
- ADRs: Markdown in `adr/` or `adrs/`, `kind: "adr"`.
- Release notes: Markdown in `release-notes/`, `kind: "release notes"`.
- Runbooks and documentation: Markdown in `docs/`, `kind: "runbook"` or `kind: "documentation"`.
- Warnings: use the `warnings` array for pack-level issues such as unavailable connected metadata.

Markdown front matter may provide title, order, and navigation group for Docs Site export. When front matter conflicts with artefact metadata, front matter wins.

## Producer Examples

These examples are documentation examples only; Lens Docs Studio does not link to or integrate with these tools.

- ArchViz could publish `docs/architecture-map.md`, `diagrams/dependency-map.mmd`, and ADR records with `static evidence`.
- Security Lens could publish candidate and confirmed findings with separate explicit labels.
- Trigger Timeline Explorer could publish Mermaid sequence or flow diagrams with runtime evidence only if the producer really gathered it.
- Release Lens could publish `release-notes/` records and warnings for blocked evidence.
- Automation Lens could publish runbooks, summary pages, and warnings where manual checks are required.

## Round-Trip Notes

Lens Docs Studio can explicitly export an artefact review pack. That export rebuilds a safe `lens-artifact-bundle.json` from currently imported records and safe session metadata, includes `reviewedWith: "Lens Docs Studio"`, and keeps candidate findings as candidate findings. Ordinary Markdown Bundle export remains generic and does not include `lens-artifact-bundle.json`.
