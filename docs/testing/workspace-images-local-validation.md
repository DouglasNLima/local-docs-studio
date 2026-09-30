# Workspace image local validation

This candidate requires explicit local owner approval before commit/push. It creates no release, version, installer, tag or distribution upload.

## Confirmed baseline causes

The inspected base is `main` at `78f7233`, with upstream `origin/main`. The repository is `DouglasNLima/local-docs-studio`. The existing untracked `.codex-remote-attachments/` directory is unrelated and remains untouched.

| Problem | Evidence and reproduction |
| --- | --- |
| Two competing image actions | The shell contained `image` and `imageFigure`. A Chromium reproduction counted two buttons. The first inserted a placeholder; the second independently serialised a figure. |
| Placeholder caption became content | `renderImageFigureFields` set the input value to `Optional caption`, and `buildImageFigure` serialised it. The reproduction read that value before any user input. |
| Local images resolved inconsistently | Preview and export each tried an asset's root path before the document-relative path and did not collapse parent segments. Folder collection only loaded text. The Windows bridge only read/wrote text documents. These are confirmed code causes, rather than a claim about every installed profile. |
| Pasted/dropped images were absent from disk | `createManagedImageAsset` only built session base64/object URLs and a map entry; no folder/native writer ran. Save messaging explicitly said it wrote image links. |
| Insertion jumped to the end | Shared `replaceEditorRange` assigned the whole textarea value, focused it and then restored selection. A baseline Chromium reproduction inserted at index 7390: `scrollTop` changed from 4000 to 10680, equal to its maximum. |
| Rename could corrupt unrelated content | Manage assets used `source.split(oldPath).join(newPath)` without Markdown/HTML structure and never moved a physical file. The replacement disables unsupported physical operations. |

The original installed Windows build/profile was not modified to reproduce these problems. Evidence comes from the inspected build-58 runtime, browser reproduction and subsequent isolated real-host smoke.

## Automated checks

Run from the repository root:

```powershell
npm test
dotnet build src/windows/LensDocsStudio.Windows.sln
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -KeepSmokeRoot -TimeoutSeconds 90
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsImagePortabilitySmoke.ps1
```

`tests/unit/image-references.test.mjs` covers paths, encoding, traversal, exact identities, figures/Markdown, reference/table images, untouched code/prose, optional captions and raster signature/MIME/size checks. `tests/browser/workspace-images.spec.mjs` covers all insertion sources, storage state, read-only protection, Escape, stale operations, viewport stability at the start/middle/end, both sync settings, Undo/Redo, pending migration, collisions, failed writes, missing bytes and fresh-context ZIP import. Browser capability simulations are labelled as such; they are not physical portability evidence.

`Run-WindowsImagePortabilitySmoke.ps1` runs the updated executable in generated profiles. It uses actual bridge capabilities and shared UI/service flows to inventory existing files, insert Markdown/figures, persist paste/drop/import images, save root/subfolder documents and verify bytes. It closes the app, copies only fixture files with `robocopy /E /XJ`, makes the source workspace unavailable by a fixture-only move, opens the copy with a new profile and round-trips its Markdown Bundle. It also checks junction/traversal/unauthorised paths and spoofed raster bytes. JSON evidence and hashes remain in its printed `artifacts/windows/image-portability-<id>/` folder. Clipboard/drop events in this automated harness are synthetic DOM events in the real host; the Windows bridge and physical writes are real.

The composite `Test-WindowsStaticAssets.ps1 -NoBuild` currently stops in its pre-existing local smoke ZIP helper: the broad `word-template-pack` exclusion matches the tracked runtime module `assets/scripts/exports/word-template-package.js`. Both the rule and the module are present at the inspected base. This candidate leaves that unrelated packaging rule unchanged. The build and real-host smoke passed; a separate SHA-256 comparison confirmed all 216 cache/vendor files in `StaticApp` match their source. The composite check is reported as blocked, rather than passed.

## Owner validation

1. Open the updated executable; use **Open folder** with a disposable workspace containing a root Markdown document, a `docs/` document and a small PNG under `assets/`.
2. Place the cursor in the middle of a long document. Use **Insert image > Workspace**, set alt text and leave caption empty. Confirm Markdown, preview and stable scrolling.
3. Insert the same image with a caption. Confirm the figure/caption without a placeholder caption.
4. Use **Import file > Choose image file**. In Windows this invokes the native picker. Choose an existing workspace image to confirm reuse, then an external image to confirm a copy under `assets/images/`.
5. Paste a real image from the Windows clipboard and drop a local image. Confirm physical files, a dirty Markdown indicator and useful state in **Manage assets**.
6. Repeat in `docs/`; check parent-relative paths. Undo/Redo a reference and cancel a dialogue with Escape; physical files must remain.
7. Save Markdown, close the app and copy the complete folder. Open the copy under a temporary profile with the source unavailable; verify images/captions. Never clear a real profile for isolation.
8. For temporary/ZIP images, select a destination and use **Save pending images to workspace**, then save Markdown or export a complete Markdown Bundle.

Native picker interaction and a real Windows clipboard producer require the owner's UI validation; automated DOM paste/drop does not certify those OS interactions. Optional owner-provided pack tests remain skipped unless `LENS_DOCS_REAL_PACK_ZIP`/`LENS_DOCS_REAL_PACK_FOLDER` supply authorised inputs.
