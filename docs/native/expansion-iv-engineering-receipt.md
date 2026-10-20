# Kanban, Requirement and GitGraph engineering receipt

Work branch: `feature/native-mermaid-kanban-requirement-gitgraph-editors`.
Start time: 20 October 2026, approximately 15:36 Europe/Dublin. Clean isolated
baseline `develop` is `0776207a17f529e22b79fd64f32c55ce4df00281` (#42 merge),
whose tree matches the reviewed feature tree. No later baseline delta was present.
Post-merge [native CI](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39434550192)
and [web CI](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39434550159)
passed. The original main checkout, ignored attachments, previous preview .838,
receipts and #38 evidence remain untouched.

#38 remains open, Draft and PAUSED at
`31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`. Stock Continuous remains default;
the historical 250 ms goal remains incomplete and .749 unresolved. The controlled
timing job excludes this branch before its first push; ordinary correctness stays
enabled. No merge, public release, production signing, updater publication, tag
movement, production Pages deployment or ordinary desktop installation is authorised.

## Reviewable milestones

1. Compatibility: `22143a7` established the ordinary native pipeline gate;
   `90427f98f91c607f69684b01331dc431931f21cf` passed **94 checks**, six Light/Dark
   representative preview/SVG/PNG/HTML/DOCX/PDF captures plus empty/small and semantic
   controls. Raw output: `artifacts/native/expansion-iv/compatibility-2/`.
2. Kanban: `ecea6f7` introduced the lossless draft/shared inspector and **11** initial
   Core cases. Raw TRX: `artifacts/native/expansion-iv/tests/kanban-initial.trx`.
3. Requirement: `d55af80` introduced blocks/references and native correspondence;
   **28** initial Core cases. Raw TRX: `requirement-initial.trx` in the same folder.
4. GitGraph: `9ab896f` introduced ordered source authoring and actual native parent
   correspondence; **15** initial Core cases. Raw TRX: `gitgraph-candidate-4.trx`.
5. Integration/validation: ordinary routes, catalogue/help, independent six new UIA
   runs, original regressions and all seventeen installed lifecycle families are
   implemented. Working-tree fidelity passed **78 checks**; this is explicitly an
   intermediate result, not clean-source certification. Final-source gates are identified by the immutable PR delivery receipt.
6. Packaging/handoff: the PR delivery receipt records final clean committed source, hosted validation and independently inspected packages.

## Source inspection and retained failures

Inspected a detached upstream checkout at exact package source
`71b64845570d633a94d29e85579c0a02786fece8`: Kanban/Requirement/GitGraph parsers,
models, SVG renderers, preprocessor, detector, render dispatch, line preprocessing
and matching parser/renderer tests. The installed NuGet nuspec independently names
the same revision. The package/runtime references remain unchanged. The
[grammar/protection guide](kanban-requirement-gitgraph-editors.md) records actual
semantics and bounded limitations. No existing fixture bytes were edited.

The initial compatibility fixture mistakenly expected two tabs to make a card
under a two-space fixed threshold. Native output correctly made another column.
The failed output/receipt is retained under `compatibility-22143a7/`; the corrected
gate adds an explicit equal-threshold column assertion rather than weakening it.

Initial GitGraph compilation found a helper hiding the shared `Text` property;
the helper was renamed. The first executable candidate found that removing an ID
left trailing spaces which the scanner incorrectly treated as an unknown command;
the scanner now follows actual pinned line trimming while retaining source bytes.
The fixture's parent count was corrected from six to the five actual native links,
with additional explicit lane/fork/merge counts. An analyser rejection of filtered
`Assert.Single` was corrected to its predicate overload. All initial logs/TRX remain
under ignored `artifacts/expansion-iv-*.log` and `artifacts/native/expansion-iv/`.

Installer execution and actual Windows High Contrast/display mutation are confined
to disposable hosted Windows runners. Association tasks are disabled for this
branch with absence assertions preserved. A source-identical synthetic upgrade is
labelled synthetic. Historical migration skips remain explicit. Higher DPI, physical
screen-reader and SDK-free clean Windows certification remain unavailable.

## Final delivery record

The immutable PR delivery receipt records: Draft PR URL, clean SHA, full
check counts/skips, source-labelled Windows screenshots, Actions artefacts and expiry,
fresh unsigned self-contained installer/portable versions, SHA-256, preserved-copy
paths, exact compiled source/resources/runtime identity, independent complete payload
parity and negative controls. Earlier working candidates do not certify final source. Use the source SHA and completed run links in the PR delivery receipt.


## Initial hosted candidate and inspector correction

Draft PR #43 targets develop. Candidate `432ee1960030f411ea06b2019e67b8375fe8a20a`
passed all 1,896 Core tests locally (zero skipped), a zero-warning locked native
build, 94 compatibility and 78 canonical-fidelity checks. Its initial hosted new
UIA runs exposed a real shared inspector bug: empty ID/tag fields were hidden when
an ancestor was temporarily disabled during validation. Visibility now uses the
intended field capability rather than inherited effective IsEnabled, with a direct
native regression. The Requirement/Kanban harness also retained an old label when
selecting an occurrence after rename; it now follows the stable ordered occurrence
prefix. Assertions and source semantics are retained. Failed job logs, raw outputs
and screenshots are preserved under `artifacts/native/expansion-iv/432ee19-hosted/`
and `artifacts/expansion-iv-432ee19-job-*.log`.

Review additionally protects partial/unclosed Kanban metadata quotation, including
an empty unquoted value, while retaining independent safe labels. Requirement
literal property braces are distinguished from actual nested declaration lines.
The focused workflow suite now has 58 cases. Native GitGraph validation additionally
checks branch badge order and actual commit-lane centres; the compatibility gate
asserts all four Requirement directions against actual box coordinates. New UIA
cases exercise destination card movement, relation-type editing and explicit named
branch/commit/checkout/merge commands. Final-source outcomes remain pending.


## Inspected committed-source Windows milestone

Candidate `cc7c26793249838e4c8d824bccc1661f508d03fe` passed
[all 24 shared-source appearance jobs](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39446459035),
retaining the existing eighteen and adding all six new-family normal/actual Windows
High Contrast runs. Source-labelled summaries and nine inspected captures are
checked in under `docs/native/{evidence,images}/expansion-iv/cc7c267/` and linked by
the grammar guide. All 270 local Chromium/Edge browser tests passed at the earlier
unchanged web source `432ee19` (no retries); static/service-worker checks passed.

Clean `cc7c267` local native checks: zero-warning locked Release build; 1,900 Core
passed, zero skipped; workflow compatibility 98; workflow editor fidelity 82; prior
chart compatibility 82; chart editor fidelity 78; source editor fidelity 397.
These retain their actual source label and do not certify a later commit.

Final review additionally rejects ignored comment-like Kanban IDs before source is
committed; safely quotes legacy comment-like Requirement endpoints; and preserves
untouched case-insensitive relation/commit type lexemes during independent edits.
The final focused workflow suite has 62 cases. Final source, packages, all seventeen
installed lifecycle suites and fresh capture/CI links are recorded in the immutable
[Draft PR #43 delivery receipt](https://github.com/DouglasNLima/local-docs-studio/pull/43)
after its gates complete. That delivery record is authoritative for final status,
package SHA-256, runtime/provenance, artefact expiry, preserved paths and elapsed time.


## Complete actual-canvas fidelity gate

The strengthened gate invokes the application's actual asynchronous editor render,
reads the drawing published on its complete WPF canvas, and compares both that
image and the entire canvas (including every unselected hit overlay) against the
ordinary document preview. Source, appearance, dimensions, scale, layout rounding
and device-pixel snapping are identical. No pixels are masked and tolerance remains
zero. All six family/Light-Dark cases pass: **100 workflow fidelity checks** in the
working-tree proof. Fresh clean-source CI repeats this gate.

The first diagnostic call found an ambiguous inherited Render overload; reflection
now selects the application's Boolean overload explicitly. The first full-canvas
comparison then exposed mismatched test-surface pixel-snapping settings (true on
the editor, false on the unhosted comparison). Both now use the actual editor's
display options. Original mismatched pairs/options/logs are preserved under
`artifacts/native/expansion-iv/working-actual-canvas-{2,3,4}/`; the passing pair is
under `working-actual-canvas-5/`. These are labelled working-tree results, not
misattributed clean-source acceptance. No product rendering or comparison threshold
was changed for this additional gate.

Clean `e3984d0716392b40a0b4a615897bdae88ff36b2b` also passed 1,904 Core cases,
the full 148-case ordinary native corpus with zero failures, static/service-worker
checks, all compatibility/fidelity gates then present, and all 24 hosted shared-source
UIA appearance runs. The later actual-canvas change is validation-only; its final
compiled source is identified by the PR delivery record and fresh package metadata.
