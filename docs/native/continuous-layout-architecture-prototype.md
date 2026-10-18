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

**Decision: NOT VIABLE for this tested design under the unchanged acceptance
contract. No migration proposal is justified.** This is a bounded experimental
decision, not an impossibility claim for other WPF architectures or hardware.
The complete list alone fails timing after profiling and public-API corrections;
clipboard parity and wider semantics also remain incomplete. Expansion stops at
the first gate as specified above. #37's original goal remains incomplete.

The separate [Draft PR #38](https://github.com/DouglasNLima/local-docs-studio/pull/38)
contains the final source receipt, exact-head check outcomes and downloadable
experimental harness. The original default renderer is unchanged. No product
source outside the new experiment project is edited. The experiment is not in
the production solution or ordinary packaging path.

### Executable and repeatable commands

On Windows with the official .NET 10 SDK/Windows Desktop Runtime:

```powershell
dotnet build src/native/LensDocsStudio.LayoutExperiment -c Release -p:RestoreLockedMode=true
$experiment = './src/native/LensDocsStudio.LayoutExperiment/bin/Release/net10.0-windows/LensDocsStudio.LayoutExperiment.exe'
& $experiment --view
& $experiment --deterministic --output artifacts/layout-checks
& $experiment --adapter candidate --uia --output artifacts/layout-uia
& $experiment --adapter stock --output artifacts/layout-stock
& $experiment --lifecycle --output artifacts/layout-lifecycle
pwsh -NoProfile -File scripts/native/Test-ContinuousLayoutArchitecture.ps1 -Replication 1
pwsh -NoProfile -File scripts/native/Test-ContinuousLayoutArchitecture.ps1 -Replication 2 -OutputDirectory artifacts/layout-architecture-2
```

`--view` displays all 4,000 items. Use mouse drag, Shift+arrows, Home/End,
Ctrl+Home/End, Page Up/Down and Ctrl+A/C. `--source <path>` reads a supported
list-only source without writing it; unsupported structures fail explicitly.
The UIA route drives real Windows input/clipboard in its own shown window.
Run controlled comparisons on an isolated host. The acceptance script exits
unsuccessfully for timing failures **and** missing parity; a timing-only
observation is never a full acceptance pass. Do not rerun to select a green
sample. Final-head runs supplement the retained first comparison below; they
do not erase it.

The workflow `Experimental Continuous architecture gate` retains two independent
order-reversed comparisons and an `EXPERIMENT-ONLY-layout-harness-*` artefact.
The harness is framework-dependent, labels its window and README explicitly,
has no installer, updater or association changes, and records source cleanliness,
payload hashes and runtime provenance. Verify every listed payload hash against
`provenance.json` before execution. It contains no modified framework. Ordinary
application packages continue to use stock rendering.

### Implemented first slice and bounded corrections

The executable uses the original fixture factory by linked source, not copied
or reduced fixture content. Its unchanged list SHA-256 is
`A4A19DCD4319BA96D909C117F779EB5EEBC1733F73F831E16D44E63658490EBA`.
Every item is shaped and all glyph caret/range geometry is available before
installation. Later viewport painting does no required formatting. Native
markers come from public `TextSimpleMarkerProperties.TextSource`, not authored
bullet strings. The semantic list remains one list with 4,000 ordered items.

Initial eager formatting/geometry took 2,157.07 ms on the local Windows
10.0.26300/.NET 10.0.12/16-logical-processor host. Profiling then separated
FormatLine, drawing/freezing and full caret/range geometry. An observed
1,196.65 ms sample spent 505.88/309.22/311.03 ms in those exclusive stages;
its repeat took 1,040.43 ms. A per-snapshot native Disc marker drawing, reused
style properties and exact LTR bounds derived from WPF caret distances reduced
local observations to 669.74/588.04 ms. Bidi/mixed-size lines retain the general
TextFormatter bounds query. This correction preserves the complete geometry
definition; no work moves to a later viewport or UIA request.

Stock's MaxHeight strategy uses natural glyph metrics with a 23-DIP minimum
advance in WPF's 1/300-DIP text units. The initial candidate instead applied an
explicit TextFormatter line height and first top margin; that defect was
corrected. All 4,000 line tops, widths, natural heights and total extent now
match stock within 0.01 DIP (maximum observed top error 0.000000013 DIP).
Both extents are 132,032.6667 DIP. Every actual bold literal has an exact
parser-bounded source span; selecting the final item maps to its exact source
start and pre-delimiter end.

The first hosted rasters differ in 208 marker pixels only. Inspection found the
gap was based on font size rather than the List's inherited natural line height.
The final correction uses the public font-family line spacing, matching the
stock default of half that natural height. Its local raster matches the retained
hosted stock raster in every RGB pixel, a cross-host diagnostic rather than
paired final-head raster acceptance. The final PR receipt records same-host
final captures separately. Text geometry is not weakened to excuse raster
differences. There is no typography reduction to obtain a timing pass.

The first stock harness observation was invalid: ContentRendered re-entry
detached the viewer and produced zero extent. It is preserved as invalid, never
counted as a fast stock result. The corrected harness runs once per window and
requires all stock geometry. Early keyboard failures came from non-extended
injected navigation keys interacting with Shift/Num Lock. The client now sends
the proper extended-key flag; product key handling was not weakened. An RTF
copy loop was made linear after review exposed repeated full-run searches.
Original failed runs and successful corrections are retained together in
[the local raw archive](evidence/continuous-layout-architecture/f011402/local-exploratory-originals.tar.gz).
The later marker-correction local sample (2,309.45 ms) overlapped native/browser
checks and is explicitly exploratory/contended, not a controlled comparison.

### First controlled comparison — source f011402

[Run 39171840282](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39171840282),
source `f01140258d06fb669ee39d28c6892619529bc58b`, identical compiled harness
`7202C4B2B4E10EC7F821D2A4C7EBDF63068744A5B1FAA052D5381993992EE1A8`
for both adapters on both hosts. Official .NET 10.0.13, Windows 10.0.26100,
image `win25-vs2026` `20261007.264.1`, four available logical processors,
96 DPI, Light, 640 × 300, 100% zoom. CPU family/model differs between hosts;
their results are not pooled. No retry, sample suppression or forced GC.

Every number below is milliseconds. Each cell retains **all samples in execution
order**. Repeated zero and distant-edit zero are included explicitly. These
are candidate-before-required-layout versus stock-original-first-layout clocks,
not a claim that the candidate starts at the old stock attachment boundary.

| Host / temperature | Stock | Candidate |
|---|---|---|
| 1 first | 548.26 | 256.87 |
| 1 fresh-process cold (3) | 513.60, 453.29, 429.61 | 247.40, 250.43, 222.50 |
| 1 repeated (0–3) | 433.12, 519.76, 406.87, 469.82 | 246.02, 209.06, 257.16, 239.55 |
| 1 distant edits (0–2) | 460.44, 519.58, 482.83 | 286.46, 199.12, 271.97 |
| 2 first, reversed order | 1,764.58 | 850.67 |
| 2 fresh-process cold (3) | 1,546.31, 1,492.07, 1,442.93 | 752.07, 673.46, 670.71 |
| 2 repeated (0–3) | 1,393.39, 889.75, 690.70, 646.67 | 710.65, 639.02, 460.39, 541.59 |
| 2 distant edits (0–2) | 1,385.31, 855.04, 714.90 | 681.33, 647.65, 563.52 |

**5/11 and 11/11 candidate timing failures. Both acceptance jobs correctly fail.**
Both independent UIA clients, geometry comparisons and lifecycle checks pass
their stated list-only assertions. Raw readiness, source/runtime/harness identity,
allocation/natural collection/memory and every regression are in the original
[replication 1 receipt](evidence/continuous-layout-architecture/f011402/replication-1/acceptance.json)
and [replication 2 receipt](evidence/continuous-layout-architecture/f011402/replication-2/acceptance.json).
All original raw-file hashes are [retained](evidence/continuous-layout-architecture/f011402/original-file-hashes.json);
large raw geometry and the original captures remain in the run's 90-day artefacts.
Small original scalar/interaction receipts and representative captures are
committed alongside this report. They retain this measured source, not the later
marker/provenance correction. The final PR receipt owns final-source results.

First end-to-end readiness is 818.35 → 363.52 ms on host 1 and
2,283.31 → 1,010.09 ms on host 2. All other temperatures retain their separate
readiness arrays in raw receipts; parsing/model extraction is included there.
Process startup/file I/O are excluded and there is no physical first-frame claim.

Shown-window Input maxima across these timing workloads are stock/candidate
533.23/43.30 ms on host 1 and 1,737.95/45.80 ms on host 2. Separate replacement/
active-close/reopen scenarios report candidate maxima 43.41 and 48.39 ms, and
worker cancellation observations 0.91/0.64 ms with dispatcher observations
1.29/1.71 ms. These are in-process timestamps, not UIA transport. Whole lifecycle
durations 1,054.28/1,559.34 ms include more than winning-preview readiness and
are labelled accordingly. Product tab integration and appearance remain missing.

Candidate measured allocation is 63.77–68.08 MB on host 1 and 63.79–68.09 MB on
host 2, versus stock 115.81–117.91 MB and 114.30–117.94 MB. Natural GC arrays
remain per sample; both generations 0/1/2 occur. After four repeated candidate
snapshots, one weak reference remains on each host. Every surface is detached;
no forced collection is used. These bounded observations prove neither zero
retention nor a leak. Old UIA text ranges become unavailable after replacement
and use weak snapshot identity. Frozen drawings are released with snapshots;
there is no cross-document layout cache.

### Requirement status at the stopped gate

| Requirement | Status / evidence |
|---|---|
| Complete unchanged list; one semantic list, 4,000 items, bold text, markers and accurate extent | Implemented; all-item logical/style/source and stock geometry checks pass. Final marker gap corrected; see source-labelled raster outcomes |
| Real shown WPF window; visible and distant painting | Implemented; shown HWND and viewport PNGs retained; no browser/runtime fork |
| Mouse drag, keyboard extension across items, select all, focus retention, copying | Actual independent client passes on both hosted Windows machines |
| Exact source mapping, including final item | List literals and final selection pass; broader transformed-source/link/table cases missing |
| UIA complete text, one list/all items, selection, FindText, point geometry, off-screen final text and ScrollIntoView | Actual OS UIA client passes; physical screen-reader certification unavailable |
| UIA range movement/attributes/child relationship | Tested character endpoint movement, bold attribute and last-item RangeFromChild pass; complete TextUnit/attribute/relationship certification missing |
| Clipboard Unicode/Text/RTF | Implemented and actual client-tested; full parity **missing**: stock selected text serialises marker + tab and exposes Xaml; candidate excludes marker text and lacks Xaml. HTML is not supplied by this selection route |
| Selection across styled runs; wrapping; Unicode/surrogate/combining sequences; LF/CRLF source preservation | Deterministic small-fixture source/cluster/wrapped-geometry checks pass; complete real interaction/stock complex-text/bidi parity missing |
| Cancellation, queued coalescing, stale rejection, active closure and reopen | Instrumented isolated-window tests pass; one active + one latest pending request; every installed version checked; product tab/source-editor integration missing |
| Search highlighting, next/previous, source/preview navigation and stable edit anchors | UIA FindText/Select/ScrollIntoView and final-item mapping implemented; product search/outline/source navigation and stable edit-anchor parity missing |
| Nested ordered/unordered lists, continuation/numbering | Simple ordered marker code exists, unaccepted; nested/multi-paragraph items explicitly rejected; full parity missing |
| Tables/cells/alignment/wrapping/cell selection | Missing; explicitly rejected |
| Paragraphs/inline code/syntax colours, quotes, details expand/collapse | Only list-inline prototype surface; complete semantics missing |
| Headings/outline/anchors/internal and external links | Missing |
| Workspace images and Mermaid media | Missing from candidate; existing production implementations untouched |
| Light, Dark and actual OS High Contrast enable → current preview → disable → current preview | Light measured. Palette parameter exists; automatic candidate OS-transition/appearance ownership **not implemented or certified**. Stock's prior evidence stays source-labelled; final stock regression jobs are separate |
| All six complete-layout acceptance | **FAIL at list gate**; other five candidate workloads not implemented or timed |
| Pages/exports/five Mermaid editors/source/recovery/workspace/product identity | Existing code unchanged; exact-final-head regression outcomes in PR receipt |

| Original unchanged workload | New stock/candidate comparison | Candidate status |
|---|---|---|
| list | Both controlled hosts above; cold/repeated/edit separate | FAIL |
| table | Earlier #37 original observations preserved in its report; no new candidate pair | Missing; gate stopped |
| paragraphs | Same | Missing; gate stopped |
| quote | Same | Missing; gate stopped |
| details | Same | Missing; gate stopped |
| mixed | Same | Missing; gate stopped |

A fast first viewport or several sub-250 samples cannot establish viability.
Parallel formatting, alternative retained geometry representations or another
architecture remain possible future investigations, not measured fixes or a
migration recommendation. The present implementation does not satisfy the
authorised migration-proposal gate. The unrelated `.749` incident remains open;
no new reproduction was observed or historical launch search repeated here.

### Checks and final receipt

Local Release experiment and production solution builds pass with zero warnings/
errors; all 1,647 Core tests pass and the deterministic geometry/source checks
pass. The local native corpus/export run completes 134 diagram cases and export
generation/schema checks but its overall result **fails one existing details
expansion/collapse Input budget check** (297.1–724.6 ms failing observations,
complete content/formatting true). It ran concurrently with local browser checks;
contention is a limitation, not a proven cause or reason to discard it. The
`f011402` hosted UX baseline also fails workspace reopen dialogue discovery;
that is separate from the `.749` initial editor/outline incident. Both failures
and their original diagnostics are retained. Chromium/Edge/static checks were
started at this source; their final outcomes, five-editor/High Contrast/save/recovery regression
jobs and exact-source artefact verification are recorded in the final PR receipt.
Unfinished or skipped jobs must remain labelled as such. No earlier `.759`/`.760`
package is used as acceptance of this head. The final receipt records the clean
final source and newly built diagnostic artefact; this remains an experiment,
not an ordinary desktop installation, release, production signing or integration.
