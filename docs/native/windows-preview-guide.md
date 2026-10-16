# Windows internal preview guide

Local Markdown, Mermaid, and documentation studio. This is an **unsigned internal preview for controlled evaluation**, not a production release.

## Obtain and verify the package

Use the exact successful branch-source Actions run, preview version and SHA-256 values in the [Mermaid editor delivery report](https://github.com/DouglasNLima/local-docs-studio/blob/feature/native-mermaid-visual-editor/docs/native/mermaid-visual-editor.md). This source report is linked explicitly because the installed guide lives outside the source documentation folder. GitHub Actions artefacts require repository access and expire; they are not permanent download links. With the GitHub CLI:

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

For visual diagram evaluation, open the offline **Native guide → Mermaid visual authoring** topic. Use **Visual Edit** on an existing recognised Flowchart, including protected source. **Edit source** remains separate; **Insert → Flowchart visually…**. The editor's private draft has its own Undo/Redo; Apply changes the owning document in memory and ordinary Save writes it. The canvas and Rendered preview share the original native Mermaider drawing. Canvas drag positions and dashed connection guides are temporary; Auto layout or a source edit restores canonical routing. Protected source remains visible for zoom and PAN while unsafe transformations and Apply are disabled. Wide diagrams open at Actual Size; Fit provides an overview. Evaluate with copies or synthetic files.

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

- **Updates:** production Authenticode identity and manifest pins are absent. Trusted in-app download/execution of unsigned installers is blocked. This candidate is distributed as a CI artefact, so release discovery does not itself make it available. Obtain later previews through the same reviewed internal procedure.
- **Performance:** Continuous's 250 ms input objective is not achieved. Large lists, tables, rich paragraphs and mixed documents have recorded delays; the 4.14 MB Continuous case has incomplete full-layout/navigation evidence. The intermittent 587.9933 ms details construction delay remains unresolved. Prefer Pages or Editor for those documents.
- **Rendering:** the production FlowDocument viewer is used. The rejected PR 22 viewer is excluded. Mermaid supports the documented eight families and bounded syntax, not every browser Mermaid construct. See the bundled offline guide and [source diagram compatibility](https://github.com/DouglasNLima/local-docs-studio/blob/feature/native-wpf-rebuild/docs/native/diagram-compatibility.md).
- **Fidelity:** PDF import extracts text only, without OCR or page reconstruction. DOCX/HTML conversion and template support are bounded. PDF complex-script shaping, bidirectional text, joined/colour emoji and tagged accessibility are unverified. Broader Word pagination/templates, live Excel paste and Azure DevOps destinations remain limited by prior acceptance scope.
- **Certification:** clean-machine, physical accessibility/displays, hardware failure and production-signed upgrade certification remain outstanding. Use synthetic/copied documents in an approved evaluation environment and retain originals.

Report the version, source revision, action sequence and a sanitised error message to the evaluation owner. No automatic support upload or remote telemetry is added. Review any screenshot or log before sharing: do not include document contents, personal paths, credentials, API keys, corporate templates or clipboard contents. Supply a minimal synthetic reproduction when useful. Refer to the readiness report for evidence rather than sending your private state folder.
