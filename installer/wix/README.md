# WiX Installer Spike Notes

Status: Phase 3L spike only.

This folder intentionally contains notes, not a production WiX installer. Do not add generated `.msi`, `.wixpdb`, harvested package output, signing material, or release artefacts here during Phase 3L.

## Candidate Use

WiX is the stronger choice if Lens Docs Studio needs MSI-first enterprise deployment, repair semantics, transforms, detailed component ownership, or a more formal Windows Installer lifecycle.

## Current Decision

WiX is not the recommended Phase 3M path. It is powerful, but the next installer step is a small per-user installer around the existing folder package. Inno Setup should reach that MVP with less authoring and CI overhead.

## Future Prototype Questions

- Would MSI major-upgrade semantics materially improve the release path?
- Are enterprise deployment requirements expected soon?
- Can prerequisite detection remain simple without a bootstrapper chain?
- How much extra maintenance would component authoring add compared with Inno Setup?
- What signing and silent-install requirements would winget or enterprise use impose?

