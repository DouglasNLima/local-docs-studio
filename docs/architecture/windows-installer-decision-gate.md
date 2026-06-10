# Windows Installer Decision Gate

## Status

Accepted for Phase 3F.

## Context

Lens Docs Studio has a Windows-first path on `develop` while the shared static app remains compatible with GitHub Pages and local static hosting. The Windows shell is a thin WinUI 3 and WebView2 host that loads packaged local static assets from `StaticApp/` through `https://lens-docs-studio.local/`.

The current Windows release candidate artefact is a framework-dependent folder/ZIP package built by `scripts/windows/Build-WindowsPackage.ps1` and certified by `scripts/windows/Test-WindowsPackageReleaseCandidate.ps1`. It includes the desktop executable, copied static runtime, local vendor assets, documentation, and manual per-user file association scripts. It does not include MSIX packaging, a classic installer, signing, certificate provisioning, installer prerequisite bootstrapping, auto-update, Store metadata, winget manifests, or machine-wide registration.

The current Windows project targets `net8.0-windows10.0.19041.0`, uses `Microsoft.WindowsAppSDK` `2.1.3`, uses `Microsoft.Web.WebView2` `1.0.3967.48`, and sets `WindowsPackageType` to `None`.

## Decision Drivers

- Keep the app offline-capable after extraction or installation.
- Preserve the buildless browser runtime and GitHub Pages compatibility.
- Avoid surprising users with unsigned or weakly explained installer behaviour.
- Keep the next implementation phase small enough to validate.
- Support Markdown, Mermaid, and text file associations without overwriting Windows `UserChoice`.
- Make release artefacts auditable through checksums and repeatable certification.
- Leave room for future auto-update without committing to an update channel too early.
- Minimise prerequisite friction for non-developer Windows users.

## Goals

- Compare feasible Windows distribution paths before implementing an installer.
- Choose the recommended short-term and next-phase distribution path.
- Record decisions for WebView2, .NET, Windows App SDK, signing, file associations, shortcuts, uninstall, auto-update, GitHub Releases, and winget suitability.
- Define validation expectations for future installer work.

## Non-Goals

- Implementing MSIX.
- Implementing WiX, Inno Setup, or another classic installer.
- Adding production installer scripts.
- Adding auto-update.
- Changing the Windows runtime, app shell, file association scripts, or package builder.
- Publishing to `main`, Microsoft Store, or winget.

## Options Considered

### 1. Keep Folder/ZIP Package Only

The current folder/ZIP package stays the only distributable. Users extract it and run `LensDocsStudio.Windows.exe`.

This keeps implementation and certification simple, preserves offline operation, and is easy to attach to GitHub Releases. It leaves prerequisite setup, trust prompts, shortcuts, file associations, uninstall, and updates mostly manual.

### 2. MSIX Package

MSIX would provide a modern Windows package format with clean install/uninstall semantics, identity, optional App Installer update flows, Start Menu integration, and declarative file associations.

MSIX also raises signing and certificate questions immediately. Sideloaded MSIX packages require users to trust the signing certificate or install through a trusted channel. The current unpackaged WinUI 3 project would need a separate packaging spike, runtime dependency decisions, manifest work, file association declarations, and validation on clean machines.

### 3. Classic Installer

A classic installer, such as WiX or Inno Setup, would install the current unpackaged app folder, create Start Menu/Desktop shortcuts, register uninstall metadata, optionally add per-user or machine-wide file associations, and detect or bootstrap prerequisites.

This path is flexible and familiar for non-Store distribution. It also introduces installer technology ownership, signing needs, prerequisite bootstrap decisions, uninstall clean-up rules, upgrade behaviour, and a larger test matrix.

### 4. winget-Friendly Distribution Path

A winget path would make installation discoverable once the release artefact is stable and signed. It is better treated as a publication layer over a stable MSIX or classic installer, rather than as the first implementation step.

winget generally expects a durable versioned release URL, installer metadata, silent install/uninstall behaviour where available, clear licensing metadata, and trustworthy signing. The current ZIP package can be useful as a release artefact but is not the preferred long-term winget payload.

### 5. Hybrid Path: ZIP Now, MSIX Or Classic Installer Later

The hybrid path keeps the current folder/ZIP package as the release candidate artefact while using Phase 3F to decide what evidence is required before building a production installer. A later spike can compare MSIX and a classic installer with real prototypes, signing, prerequisite handling, file association behaviour, uninstall, and update compatibility.

This path avoids overfitting the installer before the signing and update strategy is known, while still giving testers a repeatable ZIP artefact and GitHub Release publication route.

## Comparison Matrix

| Distribution option | User experience | Offline support | Ease of implementation | Code signing needs | WebView2 handling | .NET/Windows App SDK handling | File associations | Uninstall support | Auto-update compatibility | GitHub Release suitability | winget suitability | Risk | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Folder/ZIP only | Manual extract and run; no install wizard or shortcuts by default. | Good once prerequisites exist. | Already implemented. | Optional for ZIP integrity/trust, but unsigned executables may trigger reputation warnings. | Require Evergreen WebView2 Runtime; do not bundle Fixed Version Runtime. | Framework-dependent package requires .NET desktop runtime and Windows App SDK runtime. | Manual HKCU scripts only; user confirmation may still be required. | Manual delete of extracted folder; scripts can remove Lens-owned association keys. | Weak; users download new ZIPs manually. | Strong for RC artefacts with checksums and reports. | Weak to moderate; ZIP is not ideal for winget. | Low implementation risk, moderate user-friction risk. | Keep for short-term RC and GitHub Releases. |
| MSIX | Clean install, Start Menu entry, package identity, declarative capabilities. | Good if packaged assets are local; prerequisites still need policy. | Medium to high; needs packaging project or manifest work. | Required for sideloading and trust; certificate distribution matters. | Prefer Evergreen prerequisite; Fixed Version possible but larger and policy-heavy. | Can express dependencies, but runtime acquisition must be validated. | Declarative associations possible without manual scripts; default app still user-controlled. | Strong package uninstall semantics. | Compatible with App Installer update flows if chosen later. | Good as a versioned release artefact. | Good once signed and release URLs are stable. | Medium; certificate trust and MSIX friction need proof. | Spike next if package identity and App Installer updates are attractive. |
| Classic installer | Familiar setup flow, shortcuts, optional prerequisite checks. | Good if app assets are installed locally. | Medium; tooling and upgrade rules must be owned. | Strongly recommended for installer and executable reputation. | Can detect Evergreen, run bootstrapper, or include Fixed Version, depending on policy. | Can detect/bootstrap .NET and Windows App SDK, or install self-contained app if chosen. | Flexible per-user or machine-wide registration; must avoid writing `UserChoice`. | Strong when installer owns installed files and registry keys. | Compatible with external updater later, but must be designed. | Good as `.exe` or `.msi` release artefact. | Good if silent install/uninstall and metadata are clean. | Medium; bootstrap and clean-up complexity. | Spike alongside MSIX if non-Store installer UX is preferred. |
| winget-friendly path | Good once accepted; command-line install and upgrade. | Depends on underlying installer. | Low for metadata after installer exists; premature before that. | Expected for trust and acceptance. | Delegated to underlying installer/package. | Delegated to underlying installer/package. | Delegated to underlying installer/package. | Delegated to underlying installer/package. | Good if stable versioned releases exist. | Requires durable release URLs. | Strong after signed installer/MSIX exists. | Medium if attempted before installer maturity. | Prepare later, after signed installer choice. |
| Hybrid ZIP now, installer later | Testers use ZIP now; future users get proper installer. | Good for current ZIP and future installed app. | Low now, bounded spike later. | Decide before public installer release. | Keep Evergreen prerequisite now; validate bootstrapper/Fixed Version only in installer spike. | Keep framework-dependent now; compare bootstrap vs self-contained in spike. | Keep manual scripts now; installer/MSIX owns associations later. | Manual now; package uninstall later. | Manual now; decide update strategy before public release. | Strong for ZIP RCs now, installer artefacts later. | Deferred until installer exists. | Low short-term risk, controlled future risk. | Recommended. |

## Decision

Use the hybrid path.

Short term:

- Keep the folder/ZIP package as the working Windows release candidate artefact.
- Publish certified ZIP artefacts through GitHub Releases when the project is ready for external testers.
- Continue improving package certification, checksums, and documentation around prerequisites.
- Keep file association registration as manual per-user scripts and first-run guidance until the installer path is chosen.

Next:

- Run a separate installer spike comparing MSIX and one classic installer technology.
- The spike must prove signing, prerequisite handling, file associations, shortcuts, uninstall, upgrade, and clean-machine validation.
- Do not add production installer scripts until that spike has a clear outcome.

Later:

- Choose signing and certificate policy before public installer distribution.
- Choose auto-update strategy before winget or broad public release.
- Add winget metadata only after there is a stable signed installer or MSIX artefact with durable GitHub Release URLs.

## Runtime Prerequisite Decision

For the current ZIP path, stay framework-dependent and document prerequisites:

- Windows 10 version 2004 / build 19041 or newer.
- .NET desktop runtime matching the Windows target framework.
- Windows App SDK runtime matching the `Microsoft.WindowsAppSDK` package reference.
- Evergreen Microsoft Edge WebView2 Runtime.

The next installer spike should compare:

- Installer bootstrap of .NET desktop runtime and Windows App SDK runtime.
- Self-contained .NET publish for the app host, with the Windows App SDK runtime still handled explicitly.
- MSIX dependency declarations and clean-machine install behaviour.

No runtime prerequisite model changes are implemented in Phase 3F.

## WebView2 Runtime Decision

Use Evergreen WebView2 Runtime as the default policy for the current ZIP path and the first installer spike.

The WebView2 bootstrapper is a candidate for a classic installer if clean-machine testing shows Evergreen is missing often enough to justify installer-managed acquisition. WebView2 Fixed Version Runtime is deferred because it increases package size and servicing responsibility, and Lens Docs Studio does not currently require a pinned WebView2 engine for deterministic rendering.

No WebView2 bootstrapper or Fixed Version Runtime is implemented in Phase 3F.

## Signing And Certificate Implications

The ZIP RC path can continue unsigned for internal validation, but public distribution should not rely on unsigned executables or unsigned installers. Unsigned artefacts increase Windows SmartScreen and enterprise trust friction.

Before public installer release, choose one of:

- A code-signing certificate for classic installer and executable signing.
- An MSIX signing certificate and a trusted distribution story for sideloading or Store/App Installer use.
- A temporary internal/test certificate only for private validation, never positioned as public trust.

Release documentation should publish checksums for ZIPs and installers. Checksums do not replace code signing.

## File Association Strategy

Keep the current manual per-user HKCU scripts for development and ZIP package testing. They should continue to avoid `UserChoice`, avoid administrator requirements, and avoid removing unrelated defaults.

Installer/MSIX file associations should be implemented only after the installer path is chosen:

- MSIX can declare file type associations in the package manifest.
- A classic installer can register per-user associations by default, with machine-wide registration only if explicitly needed and documented.
- Both paths must leave final default-app choice under Windows user control.
- Single-instance forwarding should be considered before broad file association release so opening files does not create confusing duplicate windows.

## Release Artefact Strategy

For the current path, GitHub Releases should publish:

- The certified folder/ZIP package.
- The SHA256 checksum.
- The generated release-candidate Markdown report, or a summary linking to it.
- Runtime prerequisite notes.
- Manual file association guidance.
- Clear non-claims for signing, installers, auto-update, Store, and winget.

After an installer path is selected, GitHub Releases can add signed MSIX or classic installer artefacts while retaining the ZIP as an advanced/manual package if still useful.

## Consequences

- The project avoids committing to MSIX or a classic installer before signing, update, and prerequisite decisions are clear.
- Testers can continue validating the real Windows shell through a repeatable ZIP package.
- User friction remains higher until an installer is built.
- Public release readiness depends on a future signing and installer decision.
- The file association story remains intentionally manual and reversible for now.

## Recommended Implementation Plan

1. Phase 3F: publish this decision gate and update references in the existing Windows docs.
2. Phase 3G candidate: improve GitHub Release ZIP publication documentation, including checksum and RC report expectations.
3. Phase 3H candidate: run an installer spike with MSIX and one classic installer technology, using clean-machine validation.
4. Phase 3I candidate: implement the chosen installer MVP with signing, prerequisite handling, shortcuts, uninstall, and file associations.
5. Later phase: decide auto-update and winget publication after installer artefacts are stable and signed.

## Out Of Scope

Phase 3F does not implement:

- MSIX packaging.
- WiX, Inno Setup, or any other production installer.
- WebView2 bootstrapper integration.
- WebView2 Fixed Version Runtime bundling.
- Self-contained publish changes.
- Windows App SDK runtime bootstrapper changes.
- Code signing.
- Installer-owned file associations.
- Start Menu or Desktop shortcuts.
- Uninstall registration.
- Auto-update.
- winget manifests.
- GitHub Release publication automation.

## Validation Plan

For this documentation phase, validate that static browser assets, Windows build, packaged asset checks, and package RC certification still pass:

```powershell
npm run test:static
dotnet build src/windows/LensDocsStudio.Windows.sln
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1
```

`npm run test:browser` is optional for this phase because no runtime, UI, import/export, layout, clipboard, or browser workflow code changes are intended. If runtime or project files are changed in a later installer phase, run the full browser and Windows smoke/package suite.

Future installer validation should include:

- Clean Windows machine or VM install.
- Offline launch after installation.
- Prerequisite-present and prerequisite-missing scenarios.
- Start Menu shortcut launch.
- Optional Desktop shortcut behaviour.
- Open-with and direct file activation for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.
- Upgrade from an older version.
- Uninstall and registry clean-up.
- Signature and certificate trust checks.
- GitHub Release download and checksum verification.

## Open Questions

- Which classic installer technology should be prototyped first: WiX, Inno Setup, or another option?
- Is MSIX sideloading acceptable for the intended first external tester audience?
- Will public distribution use a commercial code-signing certificate before installer MVP?
- Should the app eventually support App Installer updates, an installer-owned updater, winget upgrades, or manual GitHub Release updates only?
- Should the ZIP artefact remain public after a signed installer exists?
- Should single-instance forwarding be implemented before installer-owned file associations?
