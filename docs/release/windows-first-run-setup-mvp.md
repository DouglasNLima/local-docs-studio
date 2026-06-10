# Windows First-Run Setup MVP

Phase 3E adds a compact first-run setup wizard inside the Windows desktop shell. It is onboarding for the app runtime, not an installer wizard.

## When It Appears

The wizard appears only when Lens Docs Studio is running inside `LensDocsStudio.Windows`, the native bridge reports Windows workspace capability, and local browser storage has not recorded setup completion.

It does not auto-open in browser, GitHub Pages, local-server, or PWA mode. **Help > Open setup wizard** can reopen it manually after completion; browser mode shows a safe Windows-only readiness message.

## Steps

1. Welcome.
2. Runtime readiness.
3. Workspace.
4. File associations.
5. Starter document.
6. Done.

The wizard can be skipped at any point. Skipping records completion so it does not appear on the next Windows launch.

## Stored State

Setup state is stored in local browser storage:

- `lensDocs.windowsSetup.completed`
- `lensDocs.windowsSetup.completedAt`
- `lensDocs.windowsSetup.version`

Resetting WebView2/browser storage may show the wizard again, which is acceptable for this MVP.

## Runtime Readiness

The readiness step reports safe setup data only:

- Windows host detected.
- Native bridge available.
- Offline packaged assets loaded.
- App origin, normally `https://lens-docs-studio.local/`.
- WebView2 runtime availability when the host can report it safely.
- File and workspace bridge capability labels.

It does not expose usernames, machine names, environment variables, full executable paths, secrets, or arbitrary local paths.

## Workspace

The workspace step offers **Open a workspace folder** and **Continue without workspace**. Opening a workspace uses the existing native `workspace.openFolder` bridge only after the user clicks the button. The wizard does not create folders automatically.

## File Associations

The file association step is guidance-only. It lists `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`, and shows the per-user registration command for a packaged executable:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Register-WindowsFileAssociations.ps1 -ExecutablePath "<path-to-LensDocsStudio.Windows.exe>"
```

The app does not write registry keys, does not require administrator rights, does not write Windows `UserChoice`, and does not perform machine-wide setup.

## Starter Documents

The starter step can open:

- Markdown + Mermaid sample.
- Local feature guide.
- Unsaved blank Markdown document.

Users can also skip the starter step and continue with the current app state.

## Smoke Harness

The automated native bridge smoke is not required to complete setup. Smoke launches expose the smoke-only bridge capability, and the web runtime suppresses first-run setup when that capability is present. Normal Windows launches still show setup when storage is clean.

## Limitations

- Not an installer wizard.
- No MSIX.
- No signing or certificates.
- No administrator-elevated setup.
- No machine-wide registry writes.
- No WebView2 bootstrapper or fixed runtime bundling.
- No auto-update.
- No telemetry.
- No cloud sync or account login.
- No complex settings page.
