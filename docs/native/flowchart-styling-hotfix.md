# Isolated native flowchart styling hotfix

This is an unsigned internal Preview evaluation, independent of Goal #77. It does not qualify Native Stable, change release restrictions or resolve PR #76's NO-GO verdict. PRs #76, #78 and #80 remain Draft. No performance threshold, the 250 ms gate, the 4.5:1 contrast requirement, proposed ADR 007 or historical evidence changes here.

## Source and extraction

- Base: verified `develop` commit `99349d82eb06960eb1f9785cf384db2373aed4fa`, tree `e4d137a12cbdd827583fcb841612890dd88736ec`.
- Affected preview: `0.1.1-preview.1040`, source `f839182ceb16c20c4a8d58ddd39513aa81c267b5`.
- Reviewed correction: Draft PR #76 source `7b1334b09a02ad2fbfd4a0234798e6c387d3e634`; relevant commits `86caa102` and `3c1c100b`.
- Exact SPC fixture: `tests/fixtures/native-flowchart/spc-application-architecture.md`, Git blob `2890ecc52c27f063b76564781c2f53d3ea11b9a3`, copied without editing.
- Renderer remains pinned to Mermaider **0.15.1**. No dependency or lockfile changes.

Only the flowchart compatibility helpers and their necessary callers are extracted, without merging or cherry-picking PR #76. `SourceText` prepares renderer-private input for `DiagramService` and arrangement rendering. `MermaidSourceParser` and `MermaidSemanticValidation` use the same compatibility grammar; complete render correspondence remains required. `FlowchartSvgCompatibility` restores supported subgraph title colour and numeric dashes omitted by the pinned writer. `NativeMarkdownRenderer` exposes the typography limitation. Existing SVG and PNG exports use this same drawing.

Independent review reproduced three editing regressions in the original extraction: selecting a shared class, deleting a multiply-styled node and defining a class whose name collides with a generated effective class. The small additional remedy compares authored declarations during edit validation and removes only the selected node's previous explicit class assignments when selecting its replacement. Rendering still compares the complete effective graph. Unrelated targets, comments, definitions, Undo/Redo and source-patch checks remain intact.

The flowchart check also runs within the existing native validation corpus, whose evidence directory is already retained by CI. Workflows remain byte-identical to the base. The current rights ledger is refreshed through its existing generator without changing approval or licence scope; historical ledgers are preserved. Reading experience, comparison, performance, High Contrast implementation, versioning, signing and packaging infrastructure are unchanged.

## Reproducible scoped checks

Run from a clean checkout of the candidate commit:

```powershell
dotnet build src/native/LensDocsStudio.Native.slnx -c Release -p:RestoreLockedMode=true
dotnet test src/native/LensDocsStudio.Core.Tests -c Release --no-build --logger 'trx;LogFileName=core.trx' --results-directory artifacts/flowchart-hotfix/core
dotnet run --project src/native/LensDocsStudio.Native.Validation -c Release --no-build -- --flowchart-compatibility artifacts/flowchart-hotfix/native .
```

The Core tests reproduce the pinned upstream ten-node defect and assert nine corrected nodes, nine exact logical connections, eight declared classes, all nine assignments, node fill/stroke/text colours and subgraph styling. Negative cases cover malformed directives, quoted semicolons, comments, multiline labels, multiple targets, repeated classes, unknown classes and unsupported compound class syntax. Regression cases retain supported visual edits and history.

The compiled Windows check compares actual native preview, inspection and exported PNG pixels in Light and Dark, reopens the written SVG for identical native pixels, and verifies source strings and fixture bytes remain unchanged. Foreground application captures and package provenance are recorded separately in the PR validation receipt; this harness alone does not certify physical GUI interactions.

Use the existing `Build-NativePackage.ps1` and the native workflow run number for the preview version. Package only clean exact source, retain ZIP/installer SHA-256 hashes and `version.json`, and do not install onto the owner's desktop. The final PR receipt records candidate commit/tree, independent review, terminal hosted results and any limitations or failed checks; this document does not predeclare a pass.

## Supported semantics and limits

Complete `class`, `classDef` and `style` declarations accept optional semicolons. Multiple node targets and separate repeated class declarations are supported; later assignments win overlapping properties. Renderer-private effective class names never replace the original Markdown. Unknown class names add no properties and create no nodes.

Comma-separated class names within one assignment remain protected with a visible warning and default appearance; use separate class statements. Malformed or unsupported directives fail explicitly instead of rendering as graph nodes. Subgraph title colour supports bounded hexadecimal or named colours, and dashes support bounded numeric values. Multiline quoted style values and ambiguous subgraph title matching remain unsupported.

`<small>`, `<sub>`, `<sup>` and `<mark>` text and line breaks are retained, but their typography is unsupported. These labels remain protected in Visual Edit. The SPC fixture is inspected without rewriting or discarding its label content. Native and Web layout geometry can differ. Existing rendering and compatibility resource limits remain in force.
