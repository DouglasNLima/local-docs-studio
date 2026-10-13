# ADR: reject the current Continuous RichTextBox hybrid for integration

Status: **NO-GO**, feasibility code `8224057`, based on native `9c7454e`. Recorded 13 October 2026. Evidence: [report](continuous-viewer-feasibility.md), executable and draft [PR 22](https://github.com/DouglasNLima/local-docs-studio/pull/22).

## Context and experiment

Continuous FlowDocument performs synchronous bottomless formatting after cooperative construction. Earlier subdivision/incremental attachment did not resolve it. Replacement must bound native work while preserving meaning, selection, source navigation, clipboard, accessibility and exports.

Assess recycling item host, DrawingVisual/TextFormatter and hybrid text/table/media. Build the hybrid as a separate WPF executable. Retain authoritative source/syntax, grapheme selection and TextPattern independently of visuals. Split large structures/text instead of retaining a global FlowDocument.

The prototype demonstrates distant navigation, normal bounded container retention, cross-block Unicode/RTF/HTML/Markdown transfer, actual Windows keyboard/mouse/clipboard and offscreen TextPattern. Established exports retain distant content. Local cold list median improves 701.14 → 148.38 ms.

## Decision

**Reject integration.** Valid 65,537-character indivisible grapheme bypasses the 512-character budget. Two disposable Windows processes terminate `0x80000003` before post-layout evidence. Warm large/rich input exceeds 250 ms; history/font restoration fails its anchor expectation. Full rich clipboard/physical accessibility equivalence remain unestablished.

These are fundamental bounds/contracts. Faster first layout cannot justify replacement. Retain current renderer, solution, preferences, packages and PR 21. Experimental code stays reviewable/reproducible and excluded from production publishing.

## Consequences

Logical-document/selection/source/TextPattern separation is reusable evidence. A future native width-aware line/glyph surface needs height indexing and controlled shaping. Unicode-safe fragmentation, faithful hit/caret/clipboard geometry and reliable anchoring require proof before another implementation stage. TextFormatter's shared dependencies need an executable Unicode gate. No second complete renderer or automatic rollout starts here.
