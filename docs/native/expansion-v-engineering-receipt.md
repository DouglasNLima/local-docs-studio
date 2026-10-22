# Native Mermaid expansion V engineering receipt

Status: three bounded editor slices implemented. This source-controlled record retains implementation and candidate milestones. The source-freeze delivery receipt at `C:/Code/artifacts/LensDocsStudio-Expansion-V/final/DELIVERY.md`, delivered with [Draft PR #44](https://github.com/DouglasNLima/local-docs-studio/pull/44), owns the exact final source, gate results, installer/portable hashes, artefact links/expiry and preserved evidence. Candidate results below must not be substituted for that final-source record.

## Source and baseline

- Goal started on 22 October 2026. Work branch: `feature/native-mermaid-architecture-c4-block-editors`.
- Actual fetched `develop`: `06a7d868d1b58c82f29c7c655dcc972f1ca77282`, tree `3c3de58afee4fbb410c4b60a9ec051694d2df0c5`. No later develop changes were present at the initial audit.
- PR #43 reviewed source: `9d0f6f7c026a13bc11f080ca392b84b1674f0d40`, same tree. The `.847` previews identify that feature source, not the merge.
- Post-merge push CI: [native Windows edition](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39468973608) and [static web validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39468973709), both completed successfully at the actual merge SHA.
- Original checkout remains on `main` at `0d100cc29dde2ba80755fb1e8af60ffd9d006b16`; its untracked attachment directory is preserved. Isolated checkout: `C:/Code/MarkdownReader-expansion-v`.
- PR #38 remains open, Draft and PAUSED at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`. Its source, packages and evidence remain untouched. Stock Continuous remains the ordinary default; historical timing acceptance remains incomplete and `.749` unresolved.

## Pinned compatibility milestone

Inspected existing upstream checkout `artifacts/pinned-mermaider` in the expansion IV worktree; its actual HEAD is `71b64845570d633a94d29e85579c0a02786fece8`. Inspection includes Architecture/C4/Block parsers and public models, Architecture layout, C4/Block renderer-owned geometry, icon registry, preprocessing/header dispatch, resource checks and matching upstream tests. Mermaider 0.15.1 and official .NET 10/WPF remain unchanged.

Added five representative fixtures: Architecture, each of C4Context/C4Container/C4Component, and flat Block. The independent `--topology-compatibility` harness exercises the ordinary application renderer, trusted SVG conversion, ordinary document preview, PNG and HTML/DOCX/PDF exports in Light and Dark. Its raw receipt records the compiled source revision and per-input hashes, including failures.

The independent [hosted compatibility run 39818515472](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39818515472) passed **397 checks / 153 inputs** at clean `e94da769afae2e87b00fa6ba4a617e272585e4dc`, before editor UI implementation. Downloaded raw results are preserved under `artifacts/native/expansion-v/hosted-compatibility-e94da76`. This is compatibility-milestone evidence, not final-source acceptance.

## Editor milestone

Architecture, C4 and Block reuse `SourceDraft`, the shared inspector/source shell and the existing atomic `MermaidDocumentEditSession`. Shared `SourceCall` preserves exact C4 tokens through both pinned quote stages; shared token patching retains untouched source bytes and stable occurrences. Block token keys travel through intra-line/cross-line movement, deletion and private Undo/Redo. Topology hit priority uses actual shape area so enclosing group rectangles cannot intercept service/element selection.

All three families have Markdown/context, standalone Mermaid and Insert routes. C4 Dynamic/Deployment and unproved Architecture headers route to protected source access. The [grammar/protection guide](architecture-c4-block-editors.md) describes exact limits. The native catalogue names all three supported C4 levels explicitly; twenty editor families therefore have twenty-two Mermaid templates.

Clean editor milestone `ddb4d206137b7f4a62d85867210f750abfb9ead5` passed the locked native build with **zero warnings/errors**, **1,982 Core tests**, **447 compatibility checks / 162 inputs**, and **160 complete-canvas fidelity/owner checks**. These are candidate results, not final-source acceptance. Raw logs and TRX files remain under `artifacts/native/expansion-v/`. An initial test compilation recorded xUnit2031 for predicate assertions; the predicate overload corrected the analyser diagnostic while retaining both original conditions.

The [candidate appearance matrix 39825375272](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39825375272) completed all thirty jobs: twenty-five passed, including Block in normal and actual High Contrast. Architecture and C4 each failed both appearances when moving a newly added declaration across an existing connection row. The corrected common move transfers only the selected complete source group across proved independent connections, preserving their bytes and relative order; unknown directives and ambiguous comment attachments remain protected. A new ordering test first exposed an incorrect two-sided swap; its failure is retained and the implementation was corrected. Kanban High Contrast exposed a separate discard-confirmation lookup race; the harness now invokes the same element observed by its existing wait. No deadline or assertion was relaxed.

The [candidate ordinary native run 39825382922](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39825382922) rendered all **166 diagram cases successfully** but failed its catalogue assertion, which still expected twelve snippets after the three additions. The catalogue check now expects fifteen, verifies the new entries and inserts them under their actual native headers. All twelve earlier Mermaid snippet bodies are additionally pinned beside the seventeen template bodies and 108 unchanged fixture blobs. Candidate desktop UX, workspace/recovery, stock Continuous correctness and both web browser runs completed successfully at `ddb4d20`.

Correction development evidence: **1,993 Core tests passed**, locked build **zero warnings/errors**. Additional controls cover ambiguous trailing comments, exact configuration/Unicode/CRLF edits, unsafe Block shape conversion and quoted delimiter semantics. Raw failure and correction logs remain separate. Clean correction candidate `295dcb7a13660632b9ee59eff7591a5df721df17` independently passed **453 compatibility checks / 165 inputs** and **160 complete-canvas fidelity/owner checks** in [run 39829430040](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39829430040).

The corrected [candidate appearance run 39829430053](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39829430053) passed **all thirty normal/actual High Contrast jobs** at `295dcb7`, retaining all twenty-four earlier appearances. The three checked-in captures are unchanged PNGs from passing jobs at that source. Final review also adds the searchable bundled offline Help topic, updates the family/topic inventories to twenty, and checks the three C4 variants and source/ownership guidance in both Core and actual native Help rendering. The ten focused Help tests and locked zero-warning build passed before source freeze.

Pre-freeze local verification then passed the full **1,994 Core tests**, the native Help display/search checks, all 108 unchanged fixture blobs, catalogue synchronisation (**44 templates / 34 snippets**) and static/service-worker checks (**208 attempted cache assets, 50 modules, 57 shell assets, 150 vendor assets, 56 dependency scans**). These working-tree checks retain their own logs; the final clean committed source is independently rebuilt and accepted by the delivery runs.

The 24 existing shared-source appearance runs remain in the existing harness. Six Architecture/C4/Block normal/actual High Contrast runs extend it, including Light/Dark owner changes under actual High Contrast, explicit-ID pending validation, canonical pointer selection, keyboard spacers/junctions, off-screen navigation and source/inspector scrolling. The installed-authoring matrix extends from seventeen to twenty families (sixty install/upgrade/reinstall phases). Its final-source execution and independent downloaded hash checks belong to the delivery receipt. The fifteen existing package negative controls remain and three new editor-type controls extend them to eighteen. Final review also corrected the artefact upload list to retain all three new installed-authoring directories.

The pinned C4/Block pipeline omits upstream element checks. Bounded application guards now retain the existing 500-element budget and C4's existing default 64-level recursion bound. PNG scaled dimensions are checked before narrowing to integers, preserving the existing image budgets. No renderer, runtime, layout, deadline or dependency was changed.

Audit expectations to verify with actual output:

- Architecture uses explicit `in` membership, a shared ID namespace, actual port anchors and arrowheads. Brace group endpoints are stripped but absent from the native edge box lookup; preserve/protect them rather than inventing routing. Six bundled default pictograms emit native vector geometry. Provider artwork emits embedded SVG images, rejected by the existing native image safety policy; no policy relaxation or remote icon registration is proposed.
- C4 has distinct positional slots, removes unquoted empty arguments, filters named dollar tokens, reverses `Rel_Back`, ignores directional hints and flushes unfinished boundaries at EOF. Duplicate aliases coalesce to the final visible element. Context, Container and Component require separate proof; Dynamic and Deployment remain source-only.
- Block recognises several tokens per line, first-wins IDs, repeatable spacers, rectangle/rounded forms and positive spans. It stops at an unparseable suffix and clamps over-wide spans visually. These behaviours must be represented/protected without rewriting original bytes.

Reproduction (run from the isolated checkout):

```powershell
dotnet build src/native/LensDocsStudio.Native.slnx -c Release -p:RestoreLockedMode=true
src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe --topology-compatibility artifacts/native/expansion-v/compatibility .
```

## Retained infrastructure observations

Initial managed worktree creation failed with `No space left on device` while checking out tracked screenshots; C: had approximately 36 MB free. The tool removed its failed checkout. A separate sparse checkout allowed source inspection without altering earlier worktrees; sparse checkout was disabled once free space increased. No earlier artefacts or caches were deleted by this task.

Clean `16ffdeea3eda79220eaa1fcc7433bf69c78e3ccc` locked local build passed with zero warnings/errors in 4:09.84. Its first cold ordinary Architecture render failed the unchanged three-second resource deadline on this shared workstation. Raw failure remains at `artifacts/native/expansion-v/compatibility-16ffdee/`, including the exact source and compiled revision, and `logs/compatibility-16ffdee.log`. It is not a compatibility pass. No deadline, retry or fixture reduction was applied. A separately source-labelled disposable hosted compatibility workflow will establish the native result.

Before that hosted run, the explicit-membership geometry control connects its group junction to the member service: an unconnected junction could make the group bounding rectangle surround the unrelated service. The control now proves membership with unambiguous actual extents and retains both intended real connections; no rendered geometry is substituted.

The four preserved `.847` installer/portable hashes were independently rechecked against PR #43 and all match: `9e1dfe97dd87b8b3781189a4f516b09a5619365e3d4f91a2c872e6d309109adf`, `ca390641ceb1449754fd09c2a8e5140efba34b1a4069751f275041b66c5a430c`, `4be9b68cd27748fef6831ef786c8af068e2eb6097a529fcc97096f6eb7e4182a`, `528bd067820400c9fbbb2e0f70492eecfbeaea6e1baac8e21f1795b43b4fe79d`.

The [first PR native run 39818567910](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39818567910), source `e94da76`, passed build/Core/ordinary corpus/export/previous-fidelity gates but failed the visible installer-wizard gate: the wizard did not appear within its unchanged forty-second observation deadline. Raw failure is retained in `logs/native-e94da76-failed.log`. Its `.849` packages are historical compatibility-candidate packages, do not contain the new editors and are not final-source delivery or installed acceptance. No retry or observation deadline increase was applied.

## Source-freeze delivery gates

The separate delivery receipt records final clean-source all-family regressions, thirty normal/actual High Contrast appearance runs, the original five-editor UX coverage, owner/history/recovery, ordinary/export/fidelity/browser/static gates, sixty installed authoring phases, fresh unsigned packages and independent payload/type/control inspection. It identifies unavailable certifications and inherited skips explicitly. Twenty bounded families are implemented; broader grammar remains protected. The [actual candidate captures](architecture-c4-block-editors.md#actual-windows-captures) and their [hash receipt](evidence/expansion-v-captures.json) retain source `295dcb7`; they are not relabelled as final-source captures.

The final package comparison must examine all 528 portable payload paths against preserved `.847` bytes, retaining exact runtime and Mermaider identity and enumerating each changed application/guidance/metadata file. Source-identical `0.1.0` → `0.1.1` acceptance is synthetic and does not certify migration from historical previews. Original package hashes must remain unchanged after the eighteen negative controls. No installer or application binary is executed on the ordinary desktop during this independent inspection.

Installer execution and actual OS appearance mutation are restricted to disposable hosted Windows runners, with optional associations disabled. No merge, release, signing, updater publication, tag movement, Pages deployment or ordinary desktop installation is authorised.
