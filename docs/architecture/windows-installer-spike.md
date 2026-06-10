# Windows Installer Spike

## Status

Phase 3L spike decision.

## Current RC State

The Windows-first path is active on `develop`. A real GitHub draft prerelease exists for internal release-candidate review only:

- Tag: `v0.1.0-dev`
- Target commit: `8b215d039188af94d32857239e6829916f4b74fc`
- Draft: `true`
- Prerelease: `true`
- URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/untagged-79b1580fe9b2fc43270b`
- Phase 3K verdict: `INTERNAL_RC_MANUAL_SMOKE_PASSED_WITH_NOTES`

This spike does not publish the draft, create another release, create or move tags, merge to `main`, or change the uploaded RC artefacts.

## Installer Goals

- Keep Lens Docs Studio usable as a local Markdown, Mermaid, and documentation studio without a backend.
- Install the existing Windows shell and packaged static assets into a predictable per-user location.
- Preserve offline launch after installation when runtime prerequisites are present.
- Provide Start Menu integration, optional Desktop shortcut, clean uninstall, and a credible upgrade path.
- Keep file associations optional, reversible, per-user by default, and respectful of Windows default-app control.
- Keep GitHub Releases as the distribution channel for internal RCs and early external testers.
- Leave room for a later winget path after the installer artefact is stable and signed.

## Non-Goals

- Production MSIX package.
- Production WiX installer.
- Production Inno Setup installer.
- Code signing.
- Public release publication.
- Auto-update.
- Installer wizard UX beyond technology evaluation.
- Machine-wide registry changes.
- Elevation or administrator install.
- WebView2 bundling or bootstrapper integration.
- .NET, Windows App SDK, or WebView2 runtime bootstrapper integration.
- Merge to `main`.

## Evidence Sources

Local project evidence:

- `docs/architecture/windows-installer-decision-gate.md`
- `docs/architecture/windows-offline-distribution-roadmap.md`
- `docs/release/lens-docs-studio-draft-release-review.md`
- `scripts/windows/Build-WindowsPackage.ps1`
- `scripts/windows/Register-WindowsFileAssociations.ps1`
- `scripts/windows/Unregister-WindowsFileAssociations.ps1`
- `src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj`

External platform evidence checked for current policy:

- Microsoft Learn, MSIX package signing: <https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview>
- Microsoft Learn, WebView2 distribution: <https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution>
- Microsoft Learn, WebView2 Evergreen versus Fixed Version: <https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/evergreen-vs-fixed-version>
- Microsoft Learn, Windows App SDK deployment overview: <https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview>
- Microsoft Learn, unpackaged Windows App SDK deployment: <https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps>
- Microsoft Learn, winget manifest creation: <https://learn.microsoft.com/en-us/windows/package-manager/package/manifest>
- FireGiant WiX Toolset overview: <https://www.firegiant.com/wixtoolset/>
- WiX Toolset v4 and v5 tutorial: <https://docs.firegiant.com/wix/tutorial/>
- Inno Setup official information: <https://jrsoftware.org/isinfo.php>
- Inno Setup command line parameters: <https://jrsoftware.org/ishelp/index.php?topic=setupcmdline>

## Findings

- MSIX gives the cleanest Windows package identity story, strong uninstall semantics, declarative integration points, and future App Installer potential, but signing is not optional and sideload trust remains a real friction point for an early GitHub Releases audience.
- WiX is the strongest classic Windows Installer option when the project needs MSI control, enterprise deployment, transforms, repairs, and strict component ownership. That power is heavier than this app needs for the next small installer MVP.
- Inno Setup is a pragmatic classic installer path for a small unpackaged WinUI/WebView2 utility. It can install a prepared folder, create shortcuts, offer optional tasks, support silent install/uninstall, and integrate cleanly with GitHub Releases with less authoring overhead than WiX.
- Continuing ZIP plus GitHub Releases remains the safest fallback and internal RC route. It is already certified, auditable, and suitable for controlled testers, but it leaves too much manual setup for broader users.
- winget should be treated as a publication layer after a signed installer or MSIX exists. It should not drive the first installer implementation.

## Option Comparison

| Area | MSIX | WiX Toolset | Inno Setup | ZIP + GitHub Releases only | winget path |
| --- | --- | --- | --- | --- | --- |
| User installation experience | Clean package install with Windows identity, Start Menu entry, and controlled package lifecycle. Sideloading certificate trust can feel unfamiliar. | Familiar MSI experience, strong enterprise fit, but setup UX and authoring require more decisions. | Familiar lightweight setup wizard, easy per-user install story, good fit for early testers. | Manual extract and run. Clear but rough for non-developer users. | Excellent command-line UX after an accepted manifest exists; depends on the underlying installer. |
| Offline support | Good after install if static assets and runtime dependencies are present. | Good after install if installer lays down local assets and prerequisites are handled. | Good after install if installer lays down local assets and prerequisites are handled. | Good after extraction when prerequisites are present. | Depends on installer download and prerequisite strategy. |
| WebView2 Evergreen Runtime handling | Can require or declare policy, but distribution still needs validation. Fixed Version would increase package size and servicing duty. | Can detect Evergreen, launch bootstrapper, or document prerequisite. Bootstrapper adds online/runtime complexity. | Can detect Evergreen and show guidance first; bootstrapper can be added later if justified. | Documented prerequisite only. | Delegated to installer/MSIX. |
| .NET Desktop Runtime handling | Package/runtime dependency model needs a packaging prototype. | Can detect/bootstrap, or use self-contained publish. Bootstrap increases complexity. | Can detect and warn for MVP; bootstrap or self-contained publish can follow. | Documented prerequisite only. | Delegated to installer/MSIX. |
| Windows App SDK Runtime handling | MSIX has the strongest package/dependency story but needs proof for this unpackaged project path. | Can detect/bootstrap framework-dependent runtime or move toward self-contained strategy. | Can detect and warn for MVP; bootstrap can be deferred. | Documented prerequisite only. | Delegated to installer/MSIX. |
| File associations | Declarative file type associations. Windows still controls default-app choice. | Flexible registry authoring, but must avoid `UserChoice` and keep per-user default unless explicitly elevated later. | Optional task/checkbox can register per-user ProgIds, following the existing scripts. Must avoid `UserChoice`. | Manual HKCU scripts only. | Delegated to installer/MSIX. |
| Start Menu shortcut | Built-in package integration. | Supported. | Supported with simple script authoring. | Manual only. | Delegated to installer/MSIX. |
| Desktop shortcut | Possible but should remain optional. | Supported, optional feature/task. | Supported as optional task. | Manual only. | Delegated to installer/MSIX. |
| Uninstall behaviour | Strong package uninstall semantics. | Strong MSI uninstall and component tracking. | Good uninstall for installer-owned files and registry keys. | Delete extracted folder manually; run unregister script manually if used. | Delegated to installer/MSIX. |
| Update path | App Installer can be explored later, but it adds identity and signing commitments. | MSI major upgrades are robust but require careful product/component versioning. | Simple upgrade-over-install path is sufficient for MVP; auto-update remains deferred. | Manual download and replace. | Strong after stable signed installer URLs exist. |
| Code signing requirements | Required for deployment. Public sideload trust needs a real certificate and distribution story. | Strongly recommended for MSI/EXE reputation. | Strongly recommended for installer and app executable reputation. | Checksums help integrity, but signing is still missing for broad trust. | Strongly expected for community/package-manager trust. |
| CI/build automation complexity | Medium-high. Packaging manifest, signing, local/test certificates, and clean-machine install tests needed. | Medium-high. MSI authoring and upgrade validation add durable maintenance. | Low-medium. Can wrap the existing package output with a script and add checks incrementally. | Already implemented. | Low after installer exists, but manifest validation and release URL durability are required. |
| GitHub Releases compatibility | Good as an `.msix` or `.msixbundle` artefact after signing. | Good as `.msi` or bootstrap `.exe`. | Good as a signed setup `.exe`. | Already good for ZIP, SHA256, and RC report. | Uses GitHub Release URLs once stable. |
| winget compatibility | Good when signed and versioned. | Good when silent install/uninstall switches are clean. | Good when silent install/uninstall switches are documented and stable. | Possible but weaker than installer payloads. | Target state, not an installer option by itself. |
| Risk | Medium-high because certificate trust and MSIX packaging may dominate the next phase. | Medium because authoring power can outpace current needs. | Medium-low for an MVP because scope can stay small and reversible. | Low implementation risk, high user-friction risk. | Medium if attempted before installer maturity. |
| Recommended next step | Defer to a later MSIX prototype if package identity or App Installer becomes a priority. | Defer unless enterprise MSI needs appear. | Use for Phase 3M installer MVP. | Keep as fallback and internal RC channel. | Prepare after a signed installer exists. |

## Recommendation

Recommended next phase:

```text
Phase 3M - Classic Installer MVP with Inno Setup
```

The MVP should be a per-user Inno Setup installer that wraps the existing certified folder package output. It should install local static assets beside the Windows executable, create a Start Menu shortcut, offer an optional Desktop shortcut, optionally register per-user file associations, provide uninstall, and document runtime prerequisites.

The ZIP package should remain the fallback RC artefact and continue to be published through GitHub Releases when needed. MSIX should be deferred until there is a stronger need for package identity, App Installer update flow, Microsoft Store alignment, or enterprise package lifecycle behaviour.

## Proposed Phase 3M Scope

- Add prototype-to-MVP Inno Setup authoring under `installer/inno/`.
- Build from `scripts/windows/Build-WindowsPackage.ps1` output instead of republishing a separate app shape.
- Keep install per-user by default and avoid elevation.
- Create a Start Menu shortcut by default.
- Offer Desktop shortcut as an optional task.
- Offer file associations as an optional task for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.
- Register file associations per-user only, avoid Windows `UserChoice`, and leave default-app confirmation to Windows.
- Detect missing prerequisites and show clear guidance before launch. Do not bootstrap in the first MVP unless clean-machine evidence proves the warning-only path is too rough.
- Produce a signed-installer-ready artefact shape, but keep actual signing out until certificate policy is chosen.
- Add validation for install, launch, uninstall, silent install where feasible, and GitHub Release artefact naming.

## Runtime Prerequisite Recommendation

For Phase 3M, keep the app framework-dependent and treat runtime acquisition as documented prerequisites plus installer detection/warning:

- .NET 8 Desktop Runtime.
- Windows App SDK Runtime matching `Microsoft.WindowsAppSDK` `2.1.3`.
- Evergreen WebView2 Runtime.

Do not switch to self-contained publish in this spike. Consider self-contained .NET only if prerequisite friction remains high after installer detection is tested. Windows App SDK runtime handling still needs explicit validation even if .NET becomes self-contained later.

## WebView2 Recommendation

Keep Evergreen WebView2 Runtime as the default. The installer MVP should detect or clearly warn when WebView2 is missing, but should not bundle Fixed Version Runtime and should not run the bootstrapper in Phase 3M.

Reasons:

- Evergreen is the recommended default for most WebView2 apps and receives runtime updates outside the app release cadence.
- Fixed Version Runtime would increase package size and shift browser engine servicing onto this project.
- Bootstrapping adds online/offline branching, elevation questions, error handling, and test matrix growth.

## File Association Recommendation

The next installer should:

- Keep file associations optional.
- Register per-user associations only.
- Avoid writing `UserChoice`.
- Expose file associations as an installer task/checkbox.
- Keep the existing manual scripts for ZIP and development workflows.
- Consider single-instance forwarding before broad file association promotion, because shell activation currently may open another app instance.

## Signing Recommendation

Internal ZIP RCs can remain unsigned while checksums and draft-release review are used for controlled testing. A public installer should be signed before broad distribution.

For Phase 3M:

- Author the Inno installer so signing can be added cleanly.
- Do not block the MVP on acquiring a certificate.
- Document that unsigned installer builds are internal/test-only.
- Publish SHA256 checksums for every installer artefact. Checksums are integrity evidence, not a replacement for code signing.

## Risks

- Unsigned installer builds may trigger Windows SmartScreen or enterprise trust warnings.
- Runtime detection without bootstrapping may still leave too much first-run friction.
- File associations can surprise users if enabled by default, so they should remain opt-in.
- Per-user install paths and upgrade behaviour need careful validation to avoid stale assets.
- Inno Setup is simpler than WiX, but it is still a new release toolchain dependency.
- Deferring MSIX may delay App Installer update experiments if those become a priority.
- winget submission before signing and stable installer URLs would create avoidable review churn.

## Blockers

- Public installer distribution needs a code-signing policy.
- Clean-machine validation is required before making installer claims beyond internal MVP.
- Runtime prerequisite detection rules must be implemented and tested before a broader installer release.
- File association installer tasks should wait for explicit Phase 3M implementation and uninstall validation.

## Validation Performed

This spike is documentation and prototype guidance only. It does not change app runtime code, project files, release scripts, installer build output, registry behaviour, GitHub release state, or tags.

Phase 3L validation for this change passed on 2026-06-10:

```powershell
npm run test:static
dotnet build src/windows/LensDocsStudio.Windows.sln
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1
```

Results:

- Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans.
- `dotnet build src/windows/LensDocsStudio.Windows.sln` passed with 0 warnings and 0 errors.
- Windows static asset validation passed for 53 service-worker assets and 150 vendor assets in packaged `StaticApp/`.
- Windows package release-candidate certification passed and produced ZIP SHA256 `7B4EE1423B8ADC852BA672C97F541C824E21F2B6911A957FE2D5FB0850FD8814`.

Browser tests are not required for Phase 3L because no runtime, UI, import/export, layout, clipboard, or browser workflow files are changed.
