# ADR: isolate a logical-document Continuous viewer experiment

Status: experimental, feasibility decision pending. Base: `9c7454e`.

The production Continuous FlowDocument renderer cannot bound synchronous bottomless formatting with application yields. Previous list subdivision and incremental attachment did not resolve it. An alternative must preserve full meaning, selection, source navigation, accessibility and exports before integration.

Prototype a recycling native WPF host with bounded text pieces and existing native media services. Keep original source and all semantic data in a portable complete logical model. Keep selection as logical grapheme offsets, expose complete logical UI Automation text ranges and derive realised visuals from that state. Split list/table/quote/details contents and oversized text rather than wrapping one unbounded FlowDocument in a virtualised container.

Candidate B, a TextFormatter/DrawingVisual surface, remains credible but requires substantially more glyph/hit-testing/interaction work. Native drawing speed is insufficient evidence for replacing a selectable accessible viewer. Candidate C reuses WPF shaping and media while testing the difficult document-level contracts directly.

The spike is a separate executable and workflow, excluded from production solution/package inputs. The current renderer, preference contracts and PR 21 are untouched. No merge, release or automatic presentation-engine switch occurs. Final acceptance requires equivalent cold/warm measured layout, bounded memory/container retention, actual distant navigation, cross-block copy and a declared accessibility scope. Integration, if justified, requires a separate review and regression stage.
