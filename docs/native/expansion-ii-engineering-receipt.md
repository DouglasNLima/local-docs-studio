# Eleven-editor engineering receipt

Timeline, User Journey and Quadrant extend the ordinary native application from eight
to eleven editor families. This branch starts from develop
`546b27adf2e0bc615e0f8cab23fe47f3e8f5cfc3`; the original main checkout's untracked
attachments and other feature worktrees remain intact. The Draft PR delivery record
owns the final clean source SHA, exact final-source results, package version, hashes,
CI links, retention and preserved copies. Earlier source-labelled evidence below does
not certify a later revision by itself.

| Milestone | Reviewable implementation |
| --- | --- |
| Pinned compatibility and ordinary Quadrant adapter/export slice | `294a4ab`: actual canonical panel/point geometry, Light/Dark SVG/PNG/HTML/DOCX/PDF; no adapter omission required a renderer change |
| Shared source helpers | `b6ea1c2`: existing #40 line keys, spans, group patching, bounded private history and owner Apply session retained |
| Quadrant | `6462523`: effective declarations, paired axis fields, point labels/coordinates and ordered occurrences |
| Timeline | `85481c2`: periods, individual events, continuation groups, sections and destination moves |
| User Journey | `9afef16`: tasks, raw/effective scores, task-local ordered actor occurrences and sections |
| Native/installed acceptance | `7a1348e`: six source-workspace families and all eleven installed families; original suites retained; optional association exclusions explicit |

The [supported/protected matrix](timeline-journey-quadrant-editors.md) and bundled
[guide](user-guide.md#timeline-user-journey-and-quadrant-visual-authoring) describe
the bounded syntax. Source stays authoritative: no whole-diagram regeneration,
date sorting, custom layout, guessed escaping or renderer normalisation. New fields
are checked against intended separators and canonical occurrence counts/order,
labels, scores, actors, axis precedence and emitted point coordinates. Untouched raw
numeric lexemes remain exact, including native clamping. The renderer's two-stage
point-position rounding affects SVG geometry only, never saved source precision.

At `7a1348e`, [all 12 independent UIA jobs pass](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39393043058):
Gantt/Mind map/Pie retained plus Timeline/Journey/Quadrant, normal and actual Windows
High Contrast. The new families have 17 normal / 18 High Contrast recorded assertions,
plus awaited native field, structural, history and ownership outcomes. Two UIA
connections agree on selection. Real canonical pointers, invalid pending fields,
private Undo/Redo, destination moves, complete sections, Apply/Cancel, owner Undo/Redo,
Save/restart/reopen, stale-owner rejection, standalone source, insertion and off-screen
navigation run at 880×600 and 1280×800. Subsequent probes add title/section and every
Quadrant axis/quadrant label field; their final-source results belong to the PR record.

Windowless fidelity separately compares complete editor and ordinary-preview pixels
with zero tolerance, verifies distinct canonical source geometry, compiled shared
selection resources and exact owner-session changes across multiple fence forms.
This does not substitute for independent native UIA acceptance.

Meaningful authored captures at `7a1348e` (later results remain source-labelled in the PR):

| Family | Light | Dark | Actual High Contrast |
| --- | --- | --- | --- |
| Timeline | [Capture](evidence/expansion-ii/7a1348e-timeline-light.png) | [Capture](evidence/expansion-ii/7a1348e-timeline-dark.png) | [Capture](evidence/expansion-ii/7a1348e-timeline-high-contrast.png) |
| User Journey | [Capture](evidence/expansion-ii/7a1348e-journey-light.png) | [Capture](evidence/expansion-ii/7a1348e-journey-dark.png) | [Capture](evidence/expansion-ii/7a1348e-journey-high-contrast.png) |
| Quadrant | [Capture](evidence/expansion-ii/7a1348e-quadrant-light.png) | [Capture](evidence/expansion-ii/7a1348e-quadrant-dark.png) | [Capture](evidence/expansion-ii/7a1348e-quadrant-high-contrast.png) |

[Inspect the compact source-labelled UIA results](evidence/expansion-ii/7a1348e-authoring-summary.json).
Focused family coverage now comprises 68 cases; the final full Core count is recorded
against the final clean source in the PR. Additional native duplicate/reordered checks
include Timeline continuation groups, Journey raw-score/repeated actors and Quadrant
coincident markers, comparing each source key with its actual SVG geometry.

Earlier failures remain recorded: the initial export probe passed lowercase format
names to an uppercase API; the harness was corrected before the milestone commit.
An orphan Timeline continuation was initially mistaken for an indented empty period;
the scanner was corrected and its regression retained. A non-aligned Quadrant precision
test rejected valid coordinates because correspondence overlooked the pinned SVG
formatter's second rounding step; exact emitted-position checks now include that step,
with raw source precision and comma-decimal UI culture coverage retained.
The inherited Continuous workflow initially excluded only #40's branch. It automatically
launched two out-of-scope controlled profiling jobs at `9525de2`; both failed in
[the retained run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39394954997).
The exclusion now also covers this expansion branch. Ordinary stock Continuous
interaction checks remain enabled; no performance target, fixture or renderer changed.

The preview uses the normal unsigned self-contained Windows x64 installer/portable
pipeline and official unmodified .NET 10/Mermaider 0.15.1. Isolated install/upgrade/
reinstall compares every installed payload file with portable bytes. The two newly
built preview versions are a **source-identical synthetic upgrade**, not historical
migration. Historical jobs retain their existing branch-conditioned skips. Optional
association tasks are explicitly disabled and native ProgID/OpenWith absence is checked
through the isolated runs; normal installer defaults and other optional-task coverage
remain intact. No installer or actual OS appearance mutation runs on the ordinary desktop.

Previews `.812`, `.823`, `.824` and prior artefacts remain preserved. No merge, public
release, tag movement, production signing, updater publication or ordinary installation
is part of this work. PR #38 remains open/draft/PAUSED at
`31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`; no LayoutExperiment code is included.
Stock Continuous and its 250 ms goal, `.749` incident, dense ER resource diagnostic,
mixed cold results and memory/certification limitations retain their documented status.
SDK-free clean Windows, higher-DPI and physical screen-reader certification remain
unavailable. Ambiguous attachments, orphan continuations, unknown directives and
unrepresentable period/event separators protect affected operations with source access.
