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
   intermediate result, not clean-source certification. Final-source gates pending.
6. Packaging/handoff: pending final clean committed source and hosted validation.

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

To be completed against the immutable final source: Draft PR URL, clean SHA, full
check counts/skips, source-labelled Windows screenshots, Actions artefacts and expiry,
fresh unsigned self-contained installer/portable versions, SHA-256, preserved-copy
paths, exact compiled source/resources/runtime identity, independent complete payload
parity and negative controls. Earlier working candidates do not certify final source.
