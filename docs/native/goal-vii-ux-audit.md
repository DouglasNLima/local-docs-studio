# Goal VII: native Windows UX audit

Recorded 24 October 2026. This is a bounded review/internal-preview audit, not
production certification. [Current readiness](current-windows-readiness.md) is the
current-status entry point. The [machine-readable matrix](evidence/goal-vii/ux-matrix.json)
contains exactly 24 families and distinguishes inspected implementation, existing
test scenarios, observed results and untested capabilities.

## Frozen precondition and provenance

Before any edits, `git fetch origin develop` returned
`33a751c60886a364102c45cb43e36af1e7f13517`, tree
`51912afe438f085bcfbebd33b52e90eb91a7ba33`, matching the reviewed merge of
[PR #46](https://github.com/DouglasNLima/local-docs-studio/pull/46).
There was no advancing delta to merge. The original `C:/Code/MarkdownReader`
checkout, its untracked attachment directory and other worktrees were not edited.
Work uses the isolated `feature/native-windows-ux-release-readiness` branch.

Independently retrieved run metadata and complete job lists establish:

| Source-labelled baseline | Event / attempt | Observed result |
|---|---|---|
| [Native 40040943523](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40040943523), exact develop SHA above | push / 1 | SUCCESS: 25 successful jobs; original .853 replay and historical upgrade deliberately skipped |
| [Web 40040943517](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40040943517), same SHA | push / 1 | SUCCESS: Chromium, Edge and aggregate; Pages skipped |
| [Baseline expanded captures 40096678650](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40096678650), same SHA | workflow_dispatch / 1 | SUCCESS: 38/38 normal/actual OS High Contrast appearances |
| [Baseline original-five UX 40096681728](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40096681728), same SHA | workflow_dispatch / 1 | SUCCESS: 14 jobs; historical consolidated-suite-before skipped. Shared-workspace receipts: 54 normal / 27 actual High Contrast checks |

The original-five suite also contains historical comparison jobs. Their old
checkout identities are not current-source captures. **38 appearances means
19 expanded families × 2**, with the original five covered separately; it never
means 24 × 2. Final-source results, attempts, skips, screenshots, hashes and expiry
are recorded after the final commit in the Draft PR's engineering receipt, avoiding
a self-referential commit/hash claim.

Mermaider remains 0.15.1, upstream
`71b64845570d633a94d29e85579c0a02786fece8`. No package lock, renderer, SVG/WPF
geometry, resource limit, protected grammar, private owner session or stock
Continuous path changes. The baseline unsigned pair is `0.1.0-preview.865` /
`0.1.1-preview.865`, source-labelled by its sidecars and native run. The baseline
portable payload contains 528 files. New packages retain their own versions and
source/tree; a source-identical two-version pair is a synthetic upgrade.

## Implementation and test inventory

The first five families have specialist XAML/code-behind windows. They share
`Controls/MermaidWorkspace.cs`, `Controls/MermaidInspectorPanel.cs` and
`Resources/MermaidEditorStyles.xaml`. Nineteen later families inherit
`SourceVisualEditorWindow`; chart and topology intermediate classes supply
specialised controls without replacing the family property models. C4's three
supported views remain one family.

The matrix records each concrete window, grammar guide and existing UIA harness.
Normal routes are preview **Visual Edit**, standalone `.mmd`/`.mermaid`, **Insert**,
and actionable source inspection/fallback. Lists expose source occurrences rather
than treating labels as identities. Packet's multiple native segments share one
occurrence; Venn overlap rejects guessed pointer ownership. Hidden TreeView icons,
zero-area Treemap tiles and Venn unions retain keyboard/source access.

Existing tests cover invalid pending fields, exact duplicate-fence ownership,
private history, one-step owner Undo/Redo, Cancel, stale owner conflicts,
save/restart/reopen, off-screen list navigation and protected-source inspection.
Existing 880×600 and 1280×800 captures and actual High Contrast assertions remain.
`NativeSuiteUiChecks` additionally observes selected-row keyboard focus, inspector
Tab, source visibility and pending-selection rollback for the original five.
These are existing tests rerun here, not newly invented coverage.

Source inspection confirms toolbar names bind to tooltips, occurrence Name and
HelpText bind to full descriptions, fields have native labels and the source
panel/inspector scroll independently. It does **not** prove every control has a
unique AutomationId/HelpText, exhaustive Tab/Shift+Tab order, all caret/selection
restoration combinations or a usable physical screen reader. Those remain
PARTIAL/NOT RUN in the matrix and manual protocol. Do not turn metadata into a
claim about real keyboard focus or audible announcements.

## Reproduced findings and bounded changes

| Priority | Finding / reproduction | Scope / correction | Evidence boundary |
|---|---|---|---|
| P1 readiness | Top of `implementation-progress.md` says Expansion VI remains blocked; `architecture-decisions.md` describes merged #46 as Draft with pending acceptance; readiness starts at eleven families | All readers of current summaries. Add one current entry point and explicit historical-record boundaries, retaining original dated text/packages | Direct source comparison with verified develop and post-merge API records |
| P1 validation | Independent package verifier accepts any `10.x` WPF runtime declaration and does not require the claimed exact Core version; mutate either included framework to 10.0.12 and recompute portable checksum | Read-only package checks only. Require exactly Core/WPF 10.0.13; independent controls mutate each framework separately | Preserve old-verifier acceptance and new rejection at the same package/source in raw candidate evidence; no product binary is launched |
| P2 provenance | Package metadata records commit/dirty state but omits its exact Git tree | Add tree to clean-source packaging metadata and independent optional tree pin/rejection control | Tree is read from Git before publish; source remains clean/committed; no synthetic source identity |

**No new product UX defect has been established by a before-failing/after-passing
actual UIA scenario. Product code therefore remains unchanged.** Diagnostic targets
such as focus recovery, ambiguity and small-window scrolling are not bugs merely
because the goal lists them. An uncertain or failing capture remains a failed
candidate with its original evidence; it is not reclassified as protected grammar.
There are no new P0 product findings. Existing manual certification gaps do not
become P0/P1 product defects without a reproducible scenario.

The added package checks preserve all 22 previous controls and add three focused
ones (wrong tree pin, wrong Core declaration, wrong WPF declaration). No deadline,
readiness budget, pixel comparison, mask, fixture, retry or product assertion is
weakened. Full payload deltas are generated from the independently inspected ZIP
manifests, including every changed/added/removed path and both hashes.

## Certification boundaries and retained history

The read-only host preflight is BLOCKED by installed development tools/SDKs;
its twelve execution steps are NOT RUN. No authorised SDK-free VM, actual
display-scaling path or physical screen-reader session has been identified.
`NativeTestDisplay` negotiates resolution; it never establishes real Windows
125%/150% scale. Offscreen images are not substituted for interactive captures.
Use the [certification runbooks](goal-vii-certification-runbooks.md).

The [accepted consolidation](accepted-native-consolidation.md),
[#44 receipt](expansion-v-engineering-receipt.md),
[#45 repair/RCA](native-853-postmerge-pdf-repair.md), and
[#46 receipt](expansion-vi-engineering-receipt.md) retain their source labels,
failed candidates and recording dates. Their old present-tense milestones are
historical, not current certification. `.749` and `.853` PDF/Journey/intermediate
ER root causes remain INCONCLUSIVE. The Continuous 250 ms objective remains
INCOMPLETE. PR #38 remains open/draft/paused at
`31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`; it is untouched. No controlled
250 ms comparison is run by this goal.
