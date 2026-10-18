# Continuous layout architecture experiment

## Authorised scope and baseline

This is a reversible diagnostic executable, stacked on open Draft PR #37 at
`a937399a3f822c4e911e1ebe5ec0e426b64c56ac`. Its open #36 dependency is
`9a64dfa5adf79dc447db1f671ea39c94ec42de40`; inspected `develop` is
`6a94dd3672f95a267402e2218935d064a6ee6401`. Neither dependency was merged.
The production renderer, Pages, exports, five Mermaid editors, persistence,
recovery, product identity and installation are outside the experiment.

The #37 receipt remains authoritative for its own source. Its four acceptance
failures (43/66, 36/66, 30/66 and 41/66), shown Input maxima of
1,566.37–2,870.51 ms, unsuccessful segmentation and untrusted private-runtime
experiments are preserved. They justify investigation, not impossibility.
Original `.749` first-launch causality remains unresolved; the existing
pre-cleanup diagnostics remain intact. `.759`/`.760` do not certify this source.

## Contract recorded before implementation

The first gate is the unchanged `ContinuousPreviewFixtures` 4,000-item list.
The simplest selected architecture is a custom WPF `FrameworkElement` inside a
native `ScrollViewer`, with complete eager public `TextFormatter` layout on a
dedicated STA worker. The formatter handles shaping, fallback, line breaking and
native `TextSimpleMarkerProperties` markers. It avoids FlowDocument/PTS in the
candidate. It must actually format all lines and markers, rather than estimate
off-screen extents. Only painting can be restricted to visible lines.

Markdig produces a semantic model with one list, ordered items, paragraph and
styled-run records. Strings and exact UTF-16 source spans are immutable inputs.
No WPF document is constructed to supply candidate text, selection or UIA.
The worker owns its formatter, text lines and line-break state and disposes them
on that thread. Before completion it freezes each line's drawing and extracts
all caret stops, cluster bounds, line positions and source mappings. The
installed snapshot contains only frozen drawings and privately owned immutable
scalar collections. The UI dispatcher owns the surface, scrolling, focus,
selection, automation peers, clipboard and installation/status decisions.

Typography is Segoe UI 15 DIP, ideal formatting, 23 DIP paragraph line height,
normal stretch, established bold runs, 28 DIP page padding, 24 DIP list inset,
and paragraph margins 4/10 DIP. Width 640 DIP and height 300 DIP, 100% zoom and
96 DPI match the established comparison. Native marker shaping is required;
literal bullet prefixes are ineligible. Any difference in wrapping, baseline,
margin collapse, marker placement or extent is a parity failure until explained
and corrected. Raster equality is reported separately from geometry/style
equivalence. No typography change is permitted to win timing.

Ranges are half-open logical UTF-16 offsets, excluding Markdown delimiters and
native markers, with CRLF paragraph separators. Each rendered character boundary
maps to a parser-bounded source position; separators have explicit surrounding
source boundaries. Mouse hit testing uses shaped caret geometry. Keyboard
extension uses formatter caret stops, not arbitrary surrogate halves. Selection
works across items, survives focus loss and supports Ctrl+A/C and Home/End.
Clipboard Unicode text, Text and RTF must be compared with stock; missing
formats are failures. Search uses logical text and the same range geometry;
source navigation and viewport anchors use the immutable mapping.

The actual automation peer implements `ITextProvider`/`ITextRangeProvider`, with
document text, single selection, text units, text search, attributes, point/range
geometry and ScrollIntoView. Its child tree expresses one List and its ListItems;
names derive from real content, never substitute for TextPattern. Ranges bind to
one snapshot identity and become unavailable after replacement/closure. An
independent client process must exercise the provider through Windows UIA.
Physical screen-reader certification remains unavailable.

Cancellation is checked during parsing/model extraction and before/after each
line/geometry unit. WPF FormatLine itself is atomic. At most one active and one
latest pending source are retained; a newer version cancels obsolete work.
Only the current source/version/width/DPI/zoom/appearance/lifetime may install.
Closure cancels pending work, rejects active completions, drops the snapshot
and invalidates old ranges. Appearance changes rebuild from a captured palette;
actual OS High Contrast enable/disable must be tested on disposable hosted
Windows, with every installation checked and automatic disable restoration.

## Measurement bridge and predeclared acceptance

Use one compiled executable for both adapters, original fixture bytes and one
host per pair. Stock retains the original first-layout clock: Input queued just
before complete document assignment, synchronous Measure/Arrange/UpdateLayout,
then Input delivery. Candidate complete layout starts **before its first
FormatLine or geometry work**, includes worker dispatch, all shaped geometry and
drawings, UI installation/layout and a subsequent Input callback. Its earlier
queued Input probe is responsiveness only and cannot terminate the layout clock.
These intervals have different starts and are labelled separately; candidate
preparation cannot hide layout. End-to-end readiness begins before parsing and
ends after complete content/layout/installation and ApplicationIdle; file I/O
and process startup are excluded, explicitly. Record actual start/end/worker
thread identities and any deferred work. Required deferred layout must be zero.

Keep the first execution, three genuinely fresh-process cold samples, four
repeated fresh constructions including zero and three genuine distant-source
revisions including zero. Independently repeat with initial order reversal.
Cold is process-cold, not a reboot or empty font/file cache. No prewarm, forced
GC, timing retry, outlier suppression, fixture reduction or percentile claim.
Record source/harness/fixture/runtime hashes, OS/image/CPU availability,
viewport/DPI/appearance, allocation/natural GC and memory. Correctness, captures
and source/range oracle checks run outside timing. Shown-window Input heartbeats
and cancellation timestamps are in-process; UIA/OS transport is not substituted.

The target remains <=250 ms complete layout for **every** required sample.
The existing construction/Input/cancellation 250 ms boundaries also remain;
latest-version correctness alone cannot pass them. A dedicated acceptance
command must exit unsuccessfully for timing, parity or missing required checks.
Deterministic tests have no wall-clock thresholds.

## Staged parity matrix and stopping rule

The first measured vertical slice must include the entire list, actual geometry,
markers, bold text, exact final-item source mapping, mouse/keyboard cross-item
selection, copy, scrolling/distant navigation, actual independent UIA and safe
stale/close handling. Assertions compare logical text/styles/list order/ranges
and geometry with stock, not CLR object counts. Profile demonstrated defects
and make bounded evidence-led corrections before a decision.

If this gate passes, extend to the other five unchanged fixtures and targeted
nested numbering, paragraphs/inline code, complex/bidirectional text, tables,
quotes/details, headings/links/anchors, images/Mermaid, search/navigation,
viewport edits and Light/Dark/actual High Contrast. Table row/cell geometry and
media dimensions must be complete before readiness. Unsupported nodes must fail
explicitly rather than silently degrade. Do not grow a broad incomplete renderer
after a demonstrated gate blocker. Record non-executed requirements as missing.

Only all six timing/parity/responsiveness passes permit **VIABLE FOR A MIGRATION
PROPOSAL**. A demonstrated design blocker permits **NOT VIABLE** for this design,
not all possible native engines. Missing execution/capabilities without a
demonstrated blocker mean **INSUFFICIENT EVIDENCE**. Neither outcome completes
#37 or authorises migration.

Public API references: [TextFormatter](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.textformatting.textformatter?view=windowsdesktop-10.0),
[native markers](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.textformatting.textmarkerproperties?view=windowsdesktop-10.0),
[TextPattern](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-textpattern-overview).

## Execution record

Implementation and measurements are pending. No parity or performance pass is
claimed by this pre-implementation contract.
