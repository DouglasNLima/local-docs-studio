# Focused Outline and Copy TOC controls

Run on Windows with .NET 10, from the repository root. Each output directory must
be fresh; failed runs and their original evidence must be retained.

```powershell
dotnet run --project tests/native/outline-toc/OutlineTocControls.csproj -- artifacts/outline-light
dotnet run --project tests/native/outline-toc/OutlineTocControls.csproj -- artifacts/outline-dark --dark
dotnet run --project tests/native/outline-toc/OutlineTocControls.csproj -- artifacts/outline-dismissal --dismissal
```

These controls own isolated state, documents and shown WPF windows. They exercise
actual TreeView containers, UIA providers, routed keyboard events, native modal
options, clipboard writing/locking and source preservation. They do not inject
desktop input or certify a physical screen reader. Clipboard formats are saved
before the test writes and restored only while the test still owns the clipboard
sequence. They do not use the ordinary user's session or overwrite installation.

`--expect-high-contrast` requires actual Windows and WPF High Contrast already
enabled. It does not change OS appearance. The existing transition helper permits
mutation only on a disposable hosted desktop; do not forge those environment
flags. Light/Dark runs require actual High Contrast off and cannot qualify HC.

The thousand-section and 4 MiB controls check completeness, ownership and worker
responsiveness, not performance acceptance. No 250 ms exception or Goal #77
campaign authority is implied. The full historical PR #76 suite is not invoked.
