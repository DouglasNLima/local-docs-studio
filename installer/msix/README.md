# MSIX Installer Spike Notes

Status: Phase 3L spike only.

This folder intentionally contains notes, not a production MSIX package. Do not add generated `.msix`, `.msixbundle`, certificates, signed packages, Store metadata, or App Installer feeds here during Phase 3L.

## Candidate Use

MSIX should be revisited if Lens Docs Studio needs package identity, App Installer updates, Microsoft Store alignment, or stronger declarative Windows integration than the first classic installer MVP requires.

## Current Decision

MSIX is deferred for now because package signing is mandatory, sideload certificate trust would dominate the next internal release step, and the current unpackaged WinUI 3 shell already has a certified folder/ZIP route.

## Future Prototype Questions

- Can the current unpackaged WinUI 3 shell be packaged without disturbing `StaticApp/` loading?
- What certificate story is acceptable for internal testers and public users?
- Can file type associations be declared without creating confusing default-app behaviour?
- Does App Installer update flow matter enough to justify MSIX identity now?
- How does clean-machine install behave with .NET, Windows App SDK, and WebView2 prerequisites?

