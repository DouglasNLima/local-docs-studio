# XY Chart, Radar and Sankey engineering receipt

Work branch: `feature/native-mermaid-xy-radar-sankey-editors`. Baseline is clean
`develop` at `09c776495f6b2c4d7083d4919c5470f89727542e`, verified on 20 October 2026.
PR #41 is merged, with reviewed feature source `5d669485122df0aa3b548bbdcecbd2d9c7198947`.
Its .829 preview remains attributed to that feature source. Baseline native CI
[39409135264](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39409135264)
passed, with historical upgrade explicitly skipped. No advanced `develop` delta was present.

PR #38 remains open, Draft and paused at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`.
Stock Continuous is unchanged. The historical 250 ms goal remains incomplete and
.749 unresolved. Original checkout attachments and all other worktrees are preserved.

## Milestones and continuation

1. Initial local proof passed: pinned grammar inspection, representative compatibility fixtures,
   ordinary native adapter, SVG/PNG, document preview and HTML/DOCX/PDF export proof.
2. Implemented: shared source token helpers, stable inline keys and lossless XY, Radar and Sankey drafts. Initial 43 focused Core cases pass.
3. Implemented: native editors, canonical shape selection, normal routes, catalogue and protection guide. Initial 78 native fidelity/owner outcomes pass.
4. Pending: final source validation, independent normal/High Contrast UIA, fourteen
   installed-authoring suites on disposable runners, packages and provenance.
5. Pending: one reviewable Draft PR to `develop`; no merge or public release.

## Pinned source inspection

The installed NuGet `mermaider.nuspec` identifies version **0.15.1**, source
[`71b64845570d633a94d29e85579c0a02786fece8`](https://github.com/nullean/mermaider/tree/71b64845570d633a94d29e85579c0a02786fece8).
An independent upstream checkout at that exact detached revision was inspected:
`Parsing/{XyChart,Radar,Sankey}Parser.cs`, public `Models/{XyChart,RadarChart,SankeyDiagram}.cs`,
`Rendering/{XyChart,Radar,Sankey}SvgRenderer.cs`, `DiagramPreprocessor.cs`,
`DiagramDetector.cs`, `MermaidRenderer.PreprocessLines`, and matching parser/renderer tests.
No package update, private API, engine fork or substitute layout is introduced.

- XY has category quote toggling and doubled quotes. Invalid numeric tokens become
  zero, suffixes may be ignored, vectors may differ in length, values are clipped
  to the display range, and numeric X ranges do not drive continuous-X geometry.
  Bars paint before lines but `data-series` retains source declaration indices.
- Radar is positional, even with colon prefixes. Invalid values drop; missing
  final positions render as zero and radii clamp. Min/max only accept unsigned
  decimal directives. Duplicate labels do not establish occurrence identity.
- Sankey uses line-based quote toggling/doubled quotes and trimmed endpoint identity.
  Extra fields are ignored. Invalid/non-positive rows are omitted. Ribbons sort
  by actual source layer then endpoint names; parallel occurrences remain distinct.
  Self-links and feedback edges can be omitted. No topology editing is certified yet.

## Commands and evidence

Baseline commands: `git fetch origin`; `gh pr view 41`; `gh pr view 38`;
`gh run list --branch develop`; `dotnet build src/native/LensDocsStudio.Native.slnx
-c Release -p:RestoreLockedMode=true`: passed with zero warnings/errors at baseline.
Baseline Core: 1,784 passed, zero skipped.

Compatibility command after building: native validation executable
`--chart-compatibility artifacts/native/chart-compatibility .`.
Initial compatibility gate: **76 passed**, six family/appearance captures, including
ordinary native preview and actual HTML PNG / validated DOCX PNG / parsed PDF image
exports. Native editor fidelity: **78 passed**, complete pixels and owner histories.
These are implementation-stage working-tree results, not final committed-source
certification; the final-source gates must be rerun after the final code commit.

Retained failures: the first HTML assertion incorrectly expected inline SVG, whereas
the existing exporter embeds canonical PNG; corrected to compare actual image pixels.
The initial configuration regression exposed the pre-existing incomplete-arrow guard
matching YAML `---`; chart detection now leaves those inputs to pinned chart parsers.
The first fidelity harness compile called an internal application helper directly;
corrected to use the existing reflection-based application test approach. No private
engine API is used. Full Core after catalogue expansion initially found the exact
inventory assertion still at 33/25/11; it is updated to 36/28/14 with explicit checks
for all three new template/snippet entries. Assertions are retained or strengthened.
Raw local logs/TRX remain under the worktree's ignored `artifacts/` and `.log` files.

Local toolchain: Windows x64; .NET SDK 10.0.401, official runtime 10.0.12.
OS High Contrast mutation and installer execution remain restricted to disposable
Windows runners. Higher DPI, physical screen reader and SDK-free clean Windows
certification remain unavailable unless separately demonstrated.

## Initial committed-source acceptance and corrections

Implementation commit `22e1ffeba3c91a8137970aa0e8398e57717db2b8` is retained in
[Draft PR #42](https://github.com/DouglasNLima/local-docs-studio/pull/42). Hosted
[expanded-editor run 39418707445](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39418707445)
passed all eighteen family/appearance jobs, including all six new-family independent
UIA runs in normal/actual Windows High Contrast. This source also passed committed
local chart compatibility, 78 chart editor/owner checks and existing source-editor
fidelity. These results do not certify later commits.

[Native run 39418717299](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39418717299)
failed the exact help topic count (17 before, 18 after the new chapter); its package
and installed suites were correctly skipped. The count is updated with an additional
assertion covering the new chapter's alignment, positive-weight, local/global rename,
pending input and ownership guidance. No assertions or fixtures were removed.

Local full Core at `22e1ffe` additionally recorded two 100 ms regex safety timeouts
in ordinary short Sankey/Quadrant inputs during concurrent browser validation. The
TRX retains both failures. Hosted Core at that source had no regex timeout (1,826
passed, one help-count failure). Repeated chart numeric/geometry/header regexes now
reuse static instances rather than rebuilding the same patterns for every token;
the safety timeout is unchanged. Final Core must pass after these corrections.

Further review found that a byte-identical duplicate vector swap was incorrectly
treated as a no-op by the shared history check. Occurrence-key ordering now participates
in that check, with exact duplicate-key Undo/Redo regression coverage. Radar moves
across simple unambiguous declarations are now atomic across all curves; attached
comments or intervening source retain protection. Quoted XY categories and Radar
curve names have delimiter-specific evidence rather than a generic escaping rule.

A further pinned Sankey limitation is now explicit: integral node totals at or above
2^63 cannot be labelled correctly by the engine's Int64 formatter. The compatibility
gate records the actual `A,B,1e20` label and authoring protects this construct while
retaining exact source. No engine/layout replacement is introduced.

Initial raw hosted UIA artefacts are preserved locally under
`artifacts/native/22e1ffe-hosted-source-uia/`; the failure log is retained as
`artifacts/native/22e1ffe-hosted-native-failure.log`. Final-source screenshots, packages
and receipt identifiers remain pending.

## Refined committed-source Windows evidence

The six new-family independent UIA runs also passed after inspector refinement at
`1a4fcd5e8d948d7db4cf3a1dc4af298637e5d123`,
[run 39420457335](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39420457335).
The actual screenshots and source-labelled checks are checked in under
`docs/native/evidence/expansion-iii/`; the grammar guide links all eighteen captures.
Local web smoke passed **270/270** across Chromium and Edge at the original
implementation's unchanged static-web source. Final-source CI repeats the web gate.

Final review additionally proves read-only Radar dropped-token correspondence from
actual accepted numeric positions, without calling a dropped source token valid or
rewriting its vector. Safe neighbouring curves retain canonical proof and independent
editing. XY historical equal/reversed ranges keep exact lexemes during safe edits;
new ranges still require a finite positive span. The focused chart suite now has
55 cases. Final clean-source native build, complete Core, corpus, canonical gates,
UIA, installed-authoring and packages remain mandatory.

### Final delivery record

The [Draft PR #42 delivery receipt](https://github.com/DouglasNLima/local-docs-studio/pull/42)
records the final clean head, exact final-source CI/check counts, final screenshot
artefacts, installer/portable pairs, SHA-256, expiry and preserved local copies.
Package identity is taken from compiled source/version/resources and independent
payload inspection, never inferred from a later merge or documentation commit.
The final receipt is completed after those immutable outputs exist, without
changing their source attribution. No merge or public publication is authorised.

### Package inspection gate

The existing read-only PE/resource verifier now requires all fourteen editor families,
including the new XY/Radar/Sankey windows and Core drafts/token helpers. Its local
metadata-only proof over committed `2b1d53a` binaries finds 23 required native types
and 56 required Core types with no execution. Three additional negative controls
remove each new editor's metadata name while retaining binary version/resources and
recomputing the ZIP checksum; each must fail independently. Existing package, hash,
forbidden-payload, ER and Class resource controls are unchanged. Final downloaded
packages must pass this strengthened gate and all fourteen installed lifecycle suites.

Local committed-source `2b1d53a` native build passed with zero warnings/errors and
all **1,840 Core tests passed**, zero skipped. The full corpus and final CI results
are recorded in the final PR delivery receipt after completion.

### Complete native corpus correction

The local `2b1d53a` corpus rendered 142 ordinary diagram cases and generated validated
DOCX/PDF exports, but correctly failed two native catalogue/help inventory checks:
Mermaid snippets increased from six to nine and help topics from seventeen to
eighteen. The native-window assertions retain exact counts, now additionally checking
all three new snippet entries and the searchable chart chapter. New snippets are
contextual series/curve/link rows, with matching pinned-family render fixtures;
complete chart starters remain templates. The original failure summary and log are
retained under `artifacts/native/2b1d53a-corpus/` and `final-corpus-2b1d53a.log`.
A fresh complete corpus pass at the final source is still required.
