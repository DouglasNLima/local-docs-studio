# Native document statistics: isolated Reading Experience slice

Base: accepted develop `2dbc6d4e61db431001b42bb93b02d8e503eb8096`.
Historical reference: Draft PR #76 at `7b1334b09a02ad2fbfd4a0234798e6c387d3e634`.
This is a fresh branch, not a cherry-pick or an acceptance of the combined PR.

The native status bar shows words, characters and estimated reading time for the
current editor buffer. Tab to the statistics button and press Enter to open the
existing Document Review. Its accessible name and tooltip retain the complete
metrics when a narrow window truncates the visible label. Empty documents show
zero; no active document shows a disabled `No document`; pending edits show
`Counting…`; unsupported analysis shows `Statistics unavailable`.

Counts use readable Markdown text, including headings, table cells, link labels,
image descriptions, details summaries and captions. Formatting delimiters, front
matter, inline/fenced/indented code and Mermaid syntax do not contribute. Spaces
are collapsed and trimmed. Characters count Unicode scalars, not UTF-16 code
units or grapheme clusters. Words follow Unicode letters/numbers, attached
combining marks and internal apostrophes/hyphens, including a curly apostrophe.
This deliberately corrects supplementary-letter and decomposed-accent handling;
Document Review shares the same traversal so the two views agree. Reading time
retains the existing ceiling at 220 words per minute. No image or linked asset
is opened and no editor/source bytes are changed by statistics.

A 350 ms one-shot dispatcher debounce queues an immutable AvalonEdit snapshot.
Text materialisation, parsing and counting run on the thread pool. One semaphore
bounds calculation concurrency; cancellation, request generation, active tab,
source version, file identity and window lifetime guard publication. Old values
are cleared on edits and document changes. No polling, permanent worker or
background service is added. Markdig parsing remains synchronous within the
worker and cannot abort mid-parse; the existing 4 MiB source-character bound and
single worker limit that work, and cancellation is checked before parsing and
throughout traversal. Closure unsubscribes events and cancels queued work.

Focused reproduction (Windows, .NET 10; fresh output directories):

```powershell
dotnet test src/native/LensDocsStudio.Core.Tests -c Release --filter "FullyQualifiedName~DocumentStatistics|FullyQualifiedName~DocumentReview|FullyQualifiedName~WorkspaceDocumentReview|FullyQualifiedName~PreviewLoading"
dotnet run --project tests/native/document-statistics -c Release -- artifacts/statistics-light
dotnet run --project tests/native/document-statistics -c Release -- artifacts/statistics-dark --dark
```

For actual High Contrast, enable it in Windows Settings first and add
`--expect-high-contrast`. The control checks both Win32 and WPF flags and never
changes OS appearance or impersonates a hosted runner. These are shown owned
WPF/UIA controls and routed keyboard events, not physical display, desktop-input
or screen-reader certification. Results and retained development failures are
recorded in the final validation receipt alongside this document.

Only the statistics slice is proposed. Outline/TOC, slash commands, comparison,
Mermaid rendering and historical diagnostics are excluded. The [incremental
roadmap](reading-experience-incremental-roadmap.md) describes their boundaries.
All 30 mandatory develop contexts remain required after separate hosted CI
authorisation. Provenance substitutions remain inactive pending an exact owner
decision. No performance exception is inherited; Continuous 250 ms remains FAIL,
ADR 007 stays inactive, and public release/Native Stable remain NO-GO.
