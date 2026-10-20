# Windows internal preview guide

## Fourteen native Mermaid editors � Expansion III

XY Chart, Radar and Sankey extend the eleven existing native families. Use Visual
Edit or Insert for category-based comparisons, positional profiles and weighted
flows. Source spans, duplicate occurrences, raw numeric spelling, private history
and the existing atomic owner Apply session are preserved. Unsupported coercion,
ragged alignment, ambiguous quoting and cyclic Sankey operations remain protected
with exact source access. Mermaider 0.15.1, the official .NET 10/WPF runtime and
stock Continuous are unchanged. This is an unsigned internal preview.

The [Draft PR #42 delivery record](https://github.com/DouglasNLima/local-docs-studio/pull/42)
and [engineering receipt](https://github.com/DouglasNLima/local-docs-studio/blob/feature/native-mermaid-xy-radar-sankey-editors/docs/native/expansion-iii-engineering-receipt.md)
own exact source, validation, package hashes and certification limits. Earlier
previews retain their original source attribution. No production release or merge
is implied. PR #38 remains paused; the historical 250 ms goal is incomplete and
.749 unresolved.


## Eleven native Mermaid editors — Expansion II

Timeline, User Journey and Quadrant join the existing eight normal editor families.
They reuse #40's source occurrences, source-group patching, private history, shared
native workspace and owner Apply session. No renderer/runtime upgrade, browser
fallback or LayoutExperiment is included. See the [syntax matrix](timeline-journey-quadrant-editors.md)
and [engineering receipt](expansion-ii-engineering-receipt.md) for source-labelled
verification, known protections and the Draft PR's final package/results record.
Stock Continuous and paused #38 retain their documented status. Internal packaging
and actual OS/installer acceptance are isolated; optional associations stay disabled.


Local Markdown, Mermaid, and documentation studio. This is an **unsigned internal preview for controlled evaluation**, not a production release.

## Obtain and verify the package

Use the exact successful branch-source Actions run, preview version and SHA-256 values in the [Draft PR #42 delivery record](https://github.com/DouglasNLima/local-docs-studio/pull/42). Its engineering receipt identifies all fourteen families, protected syntax, package provenance and the actual source-specific checks. Acceptance requires completed checks for the identified source; a queued, skipped or failed mandatory gate does not establish acceptance. These records are linked explicitly because the installed guide lives outside the source documentation folder. GitHub Actions artefacts require repository access and expire; they are not permanent download links. With the GitHub CLI:

```powershell
gh run download <verified-run-id> --repo DouglasNLima/local-docs-studio --name native-validation --dir <new-empty-directory>
Get-FileHash <directory>/packages/<version>/LensDocsStudio.Native-<version>-win-x64-Setup.exe -Algorithm SHA256
Get-FileHash <directory>/packages/<version>/LensDocsStudio.Native-<version>-win-x64.zip -Algorithm SHA256
```

Compare both hashes with the independent readiness report, not only the downloaded checksum file. Check `version.json` for the exact `sourceRevision`, `sourceKind`, version and `sourceDirty: false`. PR merge-checkout packages and earlier candidates have different identities. Do not substitute them. Retain the downloaded verification report and installer/ZIP together.

## Install or run portably

The package targets **x64 Windows**; installer minimum is Windows 10 build 17763. Available automated lifecycle coverage is GitHub-hosted Windows build 26100 with development tools. A supported, genuinely clean Windows 11 image without SDKs has **not been certified**. Windows on ARM, physical screen readers, real DPI changes and multi-monitor operation are not certified.

The self-contained package includes .NET 10 and WPF. Evaluation requires no .NET SDK, Visual Studio, Node.js, Git, WebView2 or local HTTP server. GitHub CLI is an optional retrieval tool, not an application dependency.

Run the verified installer through your organisation's approved evaluation procedure. It installs for your user under `%LOCALAPPDATA%\Programs\Lens Docs Studio Native` and creates a **Lens Docs Studio Native Preview** Start Menu entry. Desktop shortcut and Markdown/Mermaid **Open with** registration are optional and initially unchecked. It preserves your default applications and the legacy Windows edition. Close the native app normally before upgrading; the installer must not force-close unsaved work.

For the portable option, extract the verified ZIP to a new folder and launch `LensDocsStudio.Native.exe`. Keep all its neighbouring runtime files. Portable workspaces are ordinary folders of Markdown and images; private application state still uses `%LOCALAPPDATA%\LensDocsStudio.Native`, not the executable's folder.

The installer has no verified publisher. Windows may show publisher or SmartScreen warnings. A matching hash identifies the reviewed bytes; it does not establish publisher trust or make an unsigned executable safe. Do not disable Windows protections. If organisational policy blocks execution, ask the internal evaluation owner to provide an approved environment.

Uninstall through Windows **Installed apps**. Application binaries, shortcuts and optional native registrations are removed. Workspace documents, images and native settings, templates, snippets, snapshots and recovery state are retained. Retaining private state is intentional; uninstall is not a data-erasure operation.

## Start a synthetic workspace

For visual diagram evaluation, open the offline **Native guide → Mermaid visual authoring** topic. **Visual Edit** opens the dedicated Flowchart, Sequence, State or ER editor for a recognised diagram. The corresponding **Insert** command creates a supported starter; **Edit source** remains separate. Each editor's private draft has its own Undo/Redo. Apply changes the owning document in memory and ordinary Save writes it. The canvas and Rendered preview share the canonical native Mermaider drawing. Supported canvas drag positions and connection guides are temporary; Auto layout restores canonical positions. Protected content remains visible while unsafe operations explain their restrictions. Fit provides an overview. Evaluate with copies or synthetic files.

For ER evaluation, use `erDiagram`. Select an entity and an exact attribute row;
edit its type/name, supported PK/FK/UK markers or quoted comment in the inspector.
Add an attribute and relationship, change endpoint cardinalities and choose
identifying/non-identifying semantics. Validate, Apply, test document Undo/Redo,
Save and reopen. Unknown suffix tokens and unknown references protect affected
operations. Key markers describe documentation; this editor does not connect to
or modify a database. ER entity layout remains canonical.

For State evaluation, use `stateDiagram-v2`. Select a state on the canvas or in the element list, update its label, add a state and connect it through the endpoint selectors or canvas picking. Simple diagrams support scoped initial/final connections, self-transitions and cycles. Validate the draft, Apply, test document Undo/Redo, Save, close and reopen. Composite membership, special-state insertion, notes/styles and unknown dependencies remain protected; independently safe labels may still be editable. Saved source remains standard Mermaid without private coordinates.

1. Open the app. The empty-draft strip offers **Open workspace**, **Open document** and the offline **Native guide**. You can simply type; the strip disappears without changing your text.
2. Choose **File → Open folder** for a copied/synthetic workspace; double-click a nested Markdown document. **Ctrl+O** opens an individual file, **Ctrl+N** creates a draft.
3. Edit, use **Ctrl+Z / Ctrl+Y**, then **Ctrl+S**. First save chooses a location. A bullet on the tab indicates unsaved changes; close prompts protect edited documents.
4. Choose **Editor**, **Split** or **Preview**. In **Edit → Settings**, Automatic uses Pages for large sources or many mapped elements. Explicit Continuous remains available. Its guidance recommends Pages for these cases; it is not a performance fix or a precise time estimate.
5. Choose **Insert → Local image**, paste image pixels or drop a supported image. Save the document. Confirm its relative `assets/images/…` reference and copy the complete workspace to retain image files.
6. Follow supported relative Markdown/wiki links or heading links. **Alt+Left / Alt+Right** provide document history. Mermaid renders locally; unsupported syntax retains its source with a visible explanation.
7. Use **Export** to create Word (`.docx`) or PDF files. Open and inspect important outputs in the intended destination. Export is independent of the reading mode.
8. Close and reopen. Review restored drafts before saving over files. **File → Document snapshots** offers retained recovery points. External-change conflicts keep your editor text and require an explicit decision.

Press **F1** for the searchable offline guide. **Help → About the native preview** and **Help → Updates** identify the installed version; `version.json` identifies the source and packaging run. Stable/Preview selection controls discovery, not trust.

## Restrictions and support

For Class authoring, open a `classDiagram` and choose **Visual Edit**, or use
**Insert → Class diagram visually…**. Select class headers, verified member rows
or relationships on the canonical canvas; the element list provides keyboard
access to every exact occurrence. Add an attribute/method, update its properties,
add a class and select the relationship type/marker owner. Validate, Apply, test
document Undo/Redo, Save and reopen. Reverse keeps cardinalities with their actual
classes. Unknown grammar, method classifiers, namespace membership and notes
retain protected source inspection. Class placement remains automatic.

- **Updates:** production Authenticode identity and manifest pins are absent. Trusted in-app download/execution of unsigned installers is blocked. This candidate is distributed as a CI artefact, so release discovery does not itself make it available. Obtain later previews through the same reviewed internal procedure.
- **Performance:** Continuous's 250 ms input objective is not achieved. Large lists, tables, rich paragraphs and mixed documents have recorded delays; the 4.14 MB Continuous case has incomplete full-layout/navigation evidence. The intermittent 587.9933 ms details construction delay remains unresolved. Prefer Pages or Editor for those documents.
- **Rendering:** the production FlowDocument viewer is used. The rejected PR 22 viewer is excluded. Mermaid supports the documented eight families and bounded syntax, not every browser Mermaid construct. See the bundled offline guide and [source diagram compatibility](https://github.com/DouglasNLima/local-docs-studio/blob/feature/native-wpf-rebuild/docs/native/diagram-compatibility.md).
- **Fidelity:** PDF import extracts text only, without OCR or page reconstruction. DOCX/HTML conversion and template support are bounded. PDF complex-script shaping, bidirectional text, joined/colour emoji and tagged accessibility are unverified. Broader Word pagination/templates, live Excel paste and Azure DevOps destinations remain limited by prior acceptance scope.
- **Certification:** clean-machine, physical accessibility/displays, hardware failure and production-signed upgrade certification remain outstanding. Use synthetic/copied documents in an approved evaluation environment and retain originals.

Report the version, source revision, action sequence and a sanitised error message to the evaluation owner. No automatic support upload or remote telemetry is added. Review any screenshot or log before sharing: do not include document contents, personal paths, credentials, API keys, corporate templates or clipboard contents. Supply a minimal synthetic reproduction when useful. Refer to the readiness report for evidence rather than sending your private state folder.
