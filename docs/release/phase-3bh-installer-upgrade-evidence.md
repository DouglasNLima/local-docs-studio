# Phase 3BH Installer Upgrade Evidence

Date: 2026-06-13

## Summary

Phase 3BH is a no-publication installer evidence checkpoint from the published `v0.1.0-dev.3` installer to a local `v0.1.0-dev.4` candidate built from current `develop`.

Runtime code did not change. This phase added evidence documentation only. It did not publish `v0.1.0-dev.4`, edit release assets, create tags or releases, merge to `main`, or claim production readiness or go-live approval.

## Repository State Before Changes

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Source commit before evidence | `94eafbb856b6ad948552b6c805bfb9a283b99a69` |
| Phase 3BG commit in history | PASS, `94eafbb856b6ad948552b6c805bfb9a283b99a69` is `HEAD` at candidate build time |
| Tracked status before edits | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |

Generated Windows build, installer, download, log, and Playwright output paths remained ignored and were not staged.

## Inputs Reviewed

- `docs/release/phase-3be-support-bundle-implementation.md`
- `docs/release/phase-3bg-diagnostics-export-copy-improvements.md`
- `docs/release/lens-docs-studio-v010-dev3-release-closure.md`
- `docs/release/lens-docs-studio-installer-upgrade-uninstall-evidence-plan.md`
- `docs/roadmap/lens-docs-studio-v010-dev4-implementation-readiness.md`
- `scripts/windows/Build-WindowsPackage.ps1`
- `scripts/windows/Build-WindowsInnoInstaller.ps1`
- `scripts/windows/Test-WindowsPackageReleaseCandidate.ps1`
- `scripts/windows/Test-WindowsStaticAssets.ps1`
- `scripts/windows/Run-WindowsNativeBridgeSmoke.ps1`
- `installer/inno/LensDocsStudio.iss`

## Published v0.1.0-dev.3 Baseline

| Field | Value |
| --- | --- |
| Release tag | `v0.1.0-dev.3` |
| Release URL | `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.3` |
| Downloaded installer | `C:\Code\MarkdownReader\artifacts\verification\phase-3bh\LensDocsStudio.Windows-0.1.0-dev.3-Setup.exe` |
| Expected SHA256 | `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |
| Observed SHA256 | `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |
| Hash result | PASS |

The baseline installer was downloaded into an ignored verification folder. No release asset was edited, replaced, re-uploaded, rebuilt, or republished.

## Local v0.1.0-dev.4 Candidate

The local candidate was built from `develop` commit `94eafbb856b6ad948552b6c805bfb9a283b99a69`.

| Artefact | Path | SHA256 |
| --- | --- | --- |
| Windows ZIP package | `C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev.zip` | `32F350E5F7D08356C786A2F91467C5DEFCF22B9ABD3991394E4D073563F1A605` |
| Inno installer | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `DBC58C9EFB859C39921772EBD80688DC24AD45A377A70293D6948E80E83E747A` |
| Installer SHA256 file | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | Generated locally |
| Installer report | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | Generated locally |
| Release-candidate report | `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260613T074602Z.md` | Generated locally |
| Release-candidate metadata | `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260613T074602Z.json` | Generated locally |

The local candidate package does not include a `LensDocsStudio.Windows.exe.WebView2` payload folder. WebView2 data observed during installed-app smoke was runtime-generated local data.

## Upgrade Evidence

Result: **BLOCKED_BASELINE_INSTALL_ROLLBACK**.

The published `v0.1.0-dev.3` installer was run silently against a temporary install root:

```powershell
LensDocsStudio.Windows-0.1.0-dev.3-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS /TASKS= /CURRENTUSER /DIR="$env:TEMP\LensDocsStudio-Phase3BH\UpgradeInstall" /LOG="$env:TEMP\LensDocsStudio-Phase3BH\logs\upgrade-baseline-install.log"
```

The installer began copying files into the temporary root but rolled back before the baseline executable could be smoked. The log shows `Compatibility mode: Yes (DetectorsAppHealth)`, then a suppressed message box defaulting to Abort while copying a deep WebView2 Service Worker cache file from the published installer payload:

```text
An error occurred while trying to create a file in the destination directory:
The system cannot find the path specified.
```

The failed path was under:

```text
LensDocsStudio.Windows.exe.WebView2\EBWebView\Default\Service Worker\CacheStorage\...
```

Because the `v0.1.0-dev.3` baseline install rolled back, Phase 3BH could not honestly run a true in-place upgrade, same-identity reinstall, or post-upgrade native smoke. No upgrade success is claimed.

Observed upgrade scenario status:

| Step | Result |
| --- | --- |
| Install published `v0.1.0-dev.3` to temp path | BLOCKED, installer rollback before executable verification |
| Verify baseline executable exists | BLOCKED |
| Native smoke against baseline executable | BLOCKED |
| Install local candidate over baseline temp path | BLOCKED |
| Verify upgraded executable exists | BLOCKED |
| Native smoke against upgraded executable | BLOCKED |
| Verify support bundle UI after upgrade | BLOCKED |
| Verify diagnostics copy/export UI after upgrade | BLOCKED |
| Silent uninstall after upgrade | Not applicable; rollback removed the temp baseline install root |

This is an installer/package validation finding against the published baseline asset payload shape, not a runtime code change in Phase 3BH. The local candidate was still validated separately through clean install and packaged smoke.

## Clean Install Evidence

Result: **PASS_WITH_NOTED_SHORTCUT_BEHAVIOUR**.

The local candidate installer was run silently against a temporary install root:

```powershell
LensDocsStudio.Windows-0.1.0-dev-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS /TASKS= /CURRENTUSER /DIR="$env:TEMP\LensDocsStudio-Phase3BH\CleanInstall" /LOG="$env:TEMP\LensDocsStudio-Phase3BH\logs\clean-candidate-install.log"
```

| Check | Result |
| --- | --- |
| Install exit code | PASS, `0` |
| Installed executable exists | PASS |
| Native smoke against installed executable | PASS |
| Installed `StaticApp/` verification | PASS, 54 service-worker assets and 150 vendor assets |
| Support bundle UI present | PASS, installed static app contains `Create support bundle` |
| Diagnostics copy UI present | PASS, installed static app contains `Copy summary` |
| Diagnostics export UI present | PASS, installed static app contains `Export local JSON` |
| Uninstall exit code | PASS, `0` |
| HKCU uninstall entry after uninstall | PASS, absent |
| Lens-owned file association keys after uninstall | PASS, absent |
| Desktop shortcut after uninstall | PASS, absent |
| Start Menu shortcut after uninstall | PASS, absent |
| Install-root residue before manual cleanup | Expected WebView2 data folder: `LensDocsStudio.Windows.exe.WebView2` |
| Manual cleanup | PASS, temporary clean install root removed after evidence capture |

Note: even with `/NOICONS /TASKS=`, the Inno installer created the Start Menu group during clean install and removed it on uninstall. Desktop shortcut and optional file associations remained absent. This checkpoint records the behaviour; it does not change installer code.

## Validation

| Command | Result | Notes |
| --- | --- | --- |
| `npm run test:static` | PASS | Static checks passed for 47 module files, 54 shell assets, 150 vendor assets, and 53 runtime external-dependency scans. |
| `npm run test:browser` | NON-PASS, followed by targeted PASS | Full run reached the Edge project and timed out in `workspace folders can create, add, refresh, and detect changed files`; Chromium had passed that scenario. Targeted rerun with `npx playwright test tests/browser/app-smoke.spec.mjs --project=edge --grep "workspace folders can create, add, refresh, and detect changed files"` passed: 1 test in Edge. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 6 existing PRI qualifier warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Debug Windows shell build succeeded with 0 warnings and 0 errors; packaged `StaticApp/` verified 54 service-worker assets and 150 vendor assets. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Generated ignored RC report and JSON; ZIP SHA256 during RC validation was `734A660F650C3E4F641562A89D851D6DF79E1CC5A16EA19BB2B7AE23B9E8FCCE`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development build native bridge smoke completed successfully. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Final ignored local Windows folder/ZIP package rebuilt; native smoke intentionally skipped by this package command flag. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild` | PASS | Final ignored local Inno installer rebuilt; installer SHA256 `DBC58C9EFB859C39921772EBD80688DC24AD45A377A70293D6948E80E83E747A`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` | PASS | Packaged native bridge smoke completed successfully against the final local candidate executable. |

## Evidence Boundary

- Phase 3BH did not change runtime code.
- Phase 3BH did not publish `v0.1.0-dev.4`.
- Phase 3BH did not edit, delete, replace, re-upload, rebuild, or republish existing prerelease assets.
- Phase 3BH did not create tags or GitHub Releases.
- Phase 3BH did not merge to `main`.
- Phase 3BH used only temporary install paths and ignored local artefact/log folders for installer evidence.
- Phase 3BH did not overwrite user documents, private workspaces, customer data, or user-authored app content.
- Phase 3BH did not claim production readiness, go-live approval, stable-channel certification, or stable/latest positioning.

## Follow-Up Gate

Future `v0.1.0-dev.4` artefacts still require a separate freeze, approval, and publication gate before any upload or release action. The local candidate artefacts from this phase are validation outputs only.
