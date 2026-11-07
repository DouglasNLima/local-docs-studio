# PR #76 source and evidence review index

Frozen review subject: `7b1334b09a02ad2fbfd4a0234798e6c387d3e634`, tree
`887856d293114442e97e0d06bef2928b340b639a`; base/merge-base develop
`99349d82eb06960eb1f9785cf384db2373aed4fa`. This is a navigational index, not a new
feature acceptance or source reorganisation. All five workstreams and integrated
preview correction are frozen as feature-complete within documented supported scope.
Overall integration remains **NO-GO** under the [decision package](native-acceptance-decision-package.md).

## Complete diff partition

The [727-path inventory](evidence/acceptance-decision/pr76-changed-files.tsv) records
one category per path, Git status and exact old/new blob IDs. Zero blob means absence.
The [source snapshot](evidence/acceptance-decision/source-identities.json) binds the
inventory hash, trees and current PR states. Paths identify review responsibility,
not a licence grant or an assertion that each entire file was newly written.

| Category | Paths | Review entry and boundary |
|---|---:|---|
| Core/domain production | 12 | `src/native/LensDocsStudio.Core/`: statistics/review, TOC/editing/slash, source parsing and flowchart semantic/SVG compatibility, shared diagram/arrangement integration. |
| Web production | 20 | `assets/`, `index.html`, `service-worker.js`: current-buffer services, comparison/Find/save/export routing, outline, slash, styles and static/offline module registration. |
| Native UI production | 26 | `src/native/LensDocsStudio.Native/`: MainWindow feature partials/XAML, view-model ownership, TOC dialogue, native slash service, renderer/comparison builder and source-position restoration. |
| Automated tests and validation | 69 | Core tests, browser smoke and fixtures; Native.Validation and UxProbe; native scripts and seven workflow changes except any separately classified governance path. Review observer correctness independently of product correctness. |
| Documentation and governance | 21 | User/engineering docs, `.gitattributes`, compliance code/config, current rights ledger and exact preserved-source amendments. These amendments are operative metadata; they are not immutable historical observations. |
| Immutable historical evidence | 579 | Remaining `docs/**/evidence/` files: failed/passing checkpoint captures, journals, identities, archive receipts and original source-labelled results. Review via manifests; do not edit or delete them. |
| **Total / production subtotal** | **727 / 58** | No path omitted or multiply counted. Large evidence line counts must not obscure the 58 production paths. |

Reproduce without checking out or rewriting source:

```powershell
git diff --raw --no-abbrev --no-renames 99349d82eb06960eb1f9785cf384db2373aed4fa...7b1334b09a02ad2fbfd4a0234798e6c387d3e634
git diff 99349d82eb06960eb1f9785cf384db2373aed4fa...7b1334b09a02ad2fbfd4a0234798e6c387d3e634 -- src/native/LensDocsStudio.Core src/native/LensDocsStudio.Native assets index.html service-worker.js
git diff --exit-code 358d2a6f4b8028461fb535f2f71a9b7c71636760 7b1334b09a02ad2fbfd4a0234798e6c387d3e634 -- src/native/LensDocsStudio.Core src/native/LensDocsStudio.Native
```

The last command succeeds: final targeted closure commits change observers and
collection prerequisites, not application/Core source. This is source identity,
not binary identity or performance non-regression. Review the changed production
files as a coherent final diff, then the tests and harnesses, then evidence. Do not
infer a production change from a validation-only `test(native)` commit title.

## Five workstreams and integrated correction

All links in this table resolve to the exact #76 subject. The
[engineering receipt](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/docs/engineering/reading-experience-receipt.md)
preserves implementation history; its early pending entries are checkpoints. Use
the [terminal receipt](https://github.com/DouglasNLima/local-docs-studio/pull/76#issuecomment-6713058791)
for the final verdict and limits.

| Workstream / bounded functional result | Production entry points | Specific coverage and limits |
|---|---|---|
| Intelligent status bar — PASS | [Web statistics](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/assets/scripts/document/document-statistics-service.js), worker and save-state routing; [Core DocumentStatistics](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Core/Review/DocumentStatistics.cs); Native `MainWindow.Statistics.cs`, document/workspace models. | [Browser tests lines 311–410](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/tests/browser/app-smoke.spec.mjs#L311), `DocumentStatisticsTests.cs`, native reading controls/input: unsaved buffer, Unicode, supersession, real persistence state, save-in-flight preservation, Review access. Not a physical accessibility certificate. |
| Advanced Outline / Copy TOC — PASS | [Web table-of-contents](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/assets/scripts/document/table-of-contents.js), document UX; Core `TableOfContentsService.cs`; [Native TOC](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Native/MainWindow.TableOfContents.cs), dialogue/XAML. | [Browser lines 249–310](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/tests/browser/app-smoke.spec.mjs#L249), `TableOfContentsTests.cs`, compiled and actual UIA/clipboard checks: H1–H6 hierarchy, renderer anchors, list/filter/wrapper options and no source mutation. |
| Contextual slash commands — PASS | [Web slash service](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/assets/scripts/editor/slash-command-service.js), existing insertion helpers; Core `SlashCommandService.cs` / `MarkdownEditService.cs`; Native `EditorSlashCommandService.cs` / `MainWindow.SlashCommands.cs`. | [Browser lines 163–248](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/tests/browser/app-smoke.spec.mjs#L163), `SlashCommandTests.cs`, actual AvalonEdit automatic/ongoing/cancelled IME events and foreground insertion/one-step Undo/Redo; URL/code/read-only guards. |
| Side-by-side comparison — PASS within supported layouts | [Web comparison](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/assets/scripts/document/comparison-service.js), editor/Find/draft/file/render/export routing; [Native comparison](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Native/MainWindow.Comparison.cs), ComparisonPreview/ComparisonReflow and `ComparisonPreviewBuilder.cs`. | [Browser lines 13–162](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/tests/browser/app-smoke.spec.mjs#L13), native reading checks and `ComparisonJourney.cs` / `ComparisonControl.cs`: independent buffers/history/Find, Save/Save As/conflicts/recovery, links/export ownership, closure/resizing/zoom. Pending Continuous/Automatic input and visible completion remain separate gaps. |
| Native Mermaid flowchart fidelity — PASS within disclosed semantics | [Core compatibility](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Core/Rendering/FlowchartCompatibility.cs), `FlowchartSvgCompatibility.cs`, parser/semantic validation/diagram/arrangement; native canonical rendering and review inspection. | `FlowchartCompatibilityTests.cs`, `MermaidVisualCorpusTests.cs`, [NativeFlowchartCompatibilityChecks](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Native.Validation/NativeFlowchartCompatibilityChecks.cs): SPC nine nodes/nine connections, canonical preview/export/inspection, unchanged Markdown. Unsupported small-label typography remains visibly disclosed; no broader Mermaid syntax claim. |
| Integrated preview reflow correction — PASS within bounded functional journey | [Integration report](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/docs/native/reading-preview-reflow-integration.md); `MainWindow.PreviewReflow.cs`, comparison lifecycle, zoom/Find/synchronisation; [PreviewViewportAnchor](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Native/Rendering/PreviewViewportAnchor.cs) / Pagination, navigation and restoration events. | Exact original mixed fixture, foreground/controlled source-position, mode/zoom/media/Find/pane ownership: 128 candidate journeys + 388 controls. Pages pending input qualifies separately. Required visible pixels still FAIL / INCONCLUSIVE; the correction does not certify compositor timing. |

Shared native coverage: [compiled reading checks](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Native.Validation/NativeReadingProductivityChecks.cs)
and [real-input reading checks](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Native.Validation/NativeReadingProductivityUiChecks.cs).
The earlier functional receipt records 47 compiled checks, ten foreground checks
per normal/HC appearance and 13 SPC checks. Final full suites pass 2,382 Core,
208 diagram/export and 12 exported-site tests, plus 157 per Web browser. Counts
describe their distinct scopes; do not add overlapping controls into a new total.

## PR dependencies and review order

1. **#78 / Goal #77** owns ADR 007, acceptance design and the original UX observer.
   It does not change product code. Its old campaign used #76 `75de4247`, not current
   `7b1334b0`. Current #76 copied/adapted observer infrastructure; it does not require
   merging #78 to execute those tests. Policy activation is a separate dependency.
2. **#80 / Goal #79** owns the shared old blank-preview source-position correction.
   Its three isolated product commits were already cherry-picked, with origins:
   `3f58591743abf81a5c96df5a4183c5b0b46fb995` → `13be8af7c97b93bfac8ed2c5e4e9bcdde3b6538e`;
   `4f133d1b110dd6b600d3e1ae06fe725a598feb4a` → `1332221463b5e457e67835995ba35f5719213808`;
   `a798eaf051baa14470a14b1af1825abe2284a9ec` → `570a6546a359e95843a2adccb57dc4e90ffd1d1f`.
   Standalone #80 lacks comparison features and cannot qualify integrated #76.
3. **#76 / Goal #75** adds comparison-specific restoration, pagination, zoom, Find,
   cancellation and ownership fixes on top of those commits. Review the final
   production diff and its retained failed checkpoints. Any future merge ordering
   must account for duplicated cherry-picks and overlapping harness/provenance files;
   no automatic second cherry-pick or assumption of clean semantic integration.

All three target the same develop commit and remain Draft. No history rewrite,
squash, deletion or source movement is proposed. Policy approval alone does not
make #76 mergeable by acceptance; fresh qualification and owner approval remain.

## Evidence reading route

Start with the [final terminal receipt](https://github.com/DouglasNLima/local-docs-studio/pull/76#issuecomment-6713058791),
then the [risk register](native-acceptance-risk-register.md), then inspect individual
failures through the receipt's run/archive links and frozen checkpoint manifests.
The final local receipt SHA-256 is
`32784dfb676e1bc896602c118ab12ff8acc5078940e50a73e18c4d2ce8ebb5a4`, verified again here.
Its local retention path is recorded in the source snapshot; hosted artefacts can
expire, so a missing download must be reported, not reconstructed or relabelled.
No claim is made that the 51 archives were redownloaded during this documentation task.

Review the unchanged historical performance fixtures/script/workflow separately
from the new UX, integration, completion and targeted-closure harnesses. Their
collection successes, expected negative-baseline failures and observer failures
are distinct from application acceptance. The repository's current rights ledger
may be regenerated for new documentation; immutable old failure logs/captures and
historical preservation snapshots must remain unchanged.
