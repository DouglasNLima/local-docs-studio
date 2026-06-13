# Phase 3BI Installer Payload Hygiene

Date: 2026-06-13

## Summary

Phase 3BI adds package and installer payload hygiene guards so runtime-generated WebView2 user data, caches, logs, and browser storage folders cannot enter Windows ZIP or Inno installer artefacts.

This phase changed packaging, validation, and evidence only. Runtime app behaviour did not change. No release assets were edited, deleted, replaced, re-uploaded, rebuilt, republished, or published. No tags or releases were created, no merge to `main` was made, and no production readiness or go-live approval is claimed.

## Repository State Before Changes

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Starting commit | `488d41fdf4ff372e8a4854f6f1645ea6b063f8fd` |
| Phase 3BH commit in history | PASS, `488d41fdf4ff372e8a4854f6f1645ea6b063f8fd` was `HEAD` at the start |
| Tracked status before edits | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | Existing ignored artefacts under `artifacts/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/`, `playwright-report/`, and prior WebView2 runtime data/cache output |

Generated package, installer, verification, downloaded, Playwright, and Windows build outputs remained ignored and were not staged.

## Phase 3BH Finding

Phase 3BH found that the published `v0.1.0-dev.3` installer rolled back while copying a deep runtime-generated WebView2 Service Worker cache path under:

```text
LensDocsStudio.Windows.exe.WebView2\EBWebView\Default\Service Worker\CacheStorage\...
```

The local `v0.1.0-dev.4` candidate package from Phase 3BH did not contain a `LensDocsStudio.Windows.exe.WebView2` payload folder. The most likely cause of the published baseline failure is that a prior package or installer source folder had been launched or smoked before installer compilation, leaving WebView2 user data beside `LensDocsStudio.Windows.exe`; the Inno script then recursively consumed the package source with `Source: "{#PackageSource}\*"`.

Phase 3BI does not alter the published baseline asset. The true upgrade scenario remains blocked by that already-published `v0.1.0-dev.3` installer rollback.

## Inputs Reviewed

- `docs/release/phase-3bh-installer-upgrade-evidence.md`
- `scripts/windows/Build-WindowsPackage.ps1`
- `scripts/windows/Build-WindowsInnoInstaller.ps1`
- `scripts/windows/Test-WindowsPackageReleaseCandidate.ps1`
- `scripts/windows/Test-WindowsStaticAssets.ps1`
- `installer/inno/LensDocsStudio.iss`
- `.gitignore`
- Existing ignored package output under `artifacts/windows/`

## Guard Implemented

Added shared payload hygiene helpers in `scripts/windows/WindowsPackagePayloadHygiene.ps1`.

The guard blocks package or installer-source paths containing:

- `LensDocsStudio.Windows.exe.WebView2`
- `EBWebView`
- `Service Worker`
- `CacheStorage`
- `GPUCache`
- `Code Cache`
- `DawnCache`
- `DawnGraphiteCache`
- `DawnWebGPUCache`
- `IndexedDB`
- `Local Storage`
- `Session Storage`
- `WebStorage`
- runtime `.log` files under blocked runtime/cache folders

The helper intentionally allows normal `StaticApp/` assets, including static files whose names may resemble browser storage concepts. `LensDocsStudio.Windows.exe.WebView2` and `EBWebView` remain blocked anywhere.

Enforcement points:

- `Build-WindowsPackage.ps1` removes blocked runtime payload directories from the package output before validation and ZIP creation.
- `Build-WindowsPackage.ps1` fails if the package folder or ZIP contains a blocked path.
- `Build-WindowsInnoInstaller.ps1` fails if the package source contains a blocked path before Inno compilation.
- `Test-WindowsPackageReleaseCandidate.ps1` fails if the package folder or ZIP contains a blocked path.
- Failure messages list bounded, focused relative paths.

## Tests And Script Coverage

Added `scripts/windows/Test-WindowsPackagePayloadHygiene.ps1`, covering:

- fake `LensDocsStudio.Windows.exe.WebView2` payload detection and assertion failure;
- fake nested `EBWebView/Default/Service Worker/CacheStorage` payload detection and assertion failure;
- fake ZIP assertion failure with the offending relative path;
- bounded path-focused failure messages;
- cleanup of blocked runtime payload directories;
- normal `StaticApp/` assets still passing.

`Test-WindowsStaticAssets.ps1` now parses the payload hygiene scripts and runs the hygiene self-test as part of Windows static validation.

## Local Candidate Artefacts

Final local artefacts generated during Phase 3BI validation:

| Artefact | Path | SHA256 |
| --- | --- | --- |
| Windows ZIP package | `C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev.zip` | `25F042ECC11F9E7FA6944DFF626AD009F6B4A2315C12234298ED581EA34B01B7` |
| Inno installer | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `1613B95E7CF25AA8CA76557002CD356646A98113BDE7647E5836AF84AE96598F` |

Additional release-candidate gate output:

| Artefact | Value |
| --- | --- |
| RC report | `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260613T081741Z.md` |
| RC metadata | `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260613T081741Z.json` |
| RC ZIP SHA256 during RC gate | `59370DAF256E0CB5BE6700ED907ED155334091830FD98B30EB8F0D4841B2F2EA` |

The final package folder and ZIP passed the payload hygiene assertions. The final installer report did not include blocked WebView2 runtime data/cache paths.

## Clean Install Evidence

Result: **PASS_WITH_EXPECTED_RUNTIME_WEBVIEW2_RESIDUE**.

The final local installer was run silently into:

```text
C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3BI\CleanInstall
```

| Check | Result |
| --- | --- |
| Install exit code | PASS, `0` |
| Installed executable exists | PASS |
| Native smoke against installed executable | PASS |
| Installed `StaticApp/` validation | PASS, 54 service-worker assets and 150 vendor assets |
| Uninstall exit code | PASS, `0` |
| Installed executable after uninstall | PASS, absent |
| Install-root residue after uninstall | Expected runtime-generated `LensDocsStudio.Windows.exe.WebView2` folder |

The residue was generated by running the installed app and is not present in the package or installer source payload.

## Validation

| Command | Result | Notes |
| --- | --- | --- |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackagePayloadHygiene.ps1` | PASS | Fake package and ZIP payload assertions failed as expected; `StaticApp/` fixture passed. |
| `npm run test:static` | PASS | Static checks passed for 47 module files, 54 shell assets, 150 vendor assets, and 53 runtime external-dependency scans. |
| `npm run test:browser` | NON-PASS, followed by targeted PASS | Full run recorded one failed Chromium test: `welcome Open folder uses the packaged native bridge route without browser fallback`. Targeted `npx playwright test --last-failed` rerun passed: 1 test. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 6 existing PRI qualifier warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Includes payload hygiene self-test; Debug Windows shell build succeeded with 0 warnings and 0 errors; packaged `StaticApp/` verified 54 service-worker assets and 150 vendor assets. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Payload guard passed for package folder and ZIP; RC ZIP SHA256 was `59370DAF256E0CB5BE6700ED907ED155334091830FD98B30EB8F0D4841B2F2EA`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development build native bridge smoke completed successfully. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Final ignored Windows folder/ZIP package rebuilt; native smoke intentionally skipped by this command flag. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild` | PASS | Final ignored Inno installer rebuilt from the guarded package source; installer SHA256 `1613B95E7CF25AA8CA76557002CD356646A98113BDE7647E5836AF84AE96598F`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` | PASS | Packaged native bridge smoke completed successfully against the final local candidate executable. |
| Clean install/uninstall of final local installer | PASS_WITH_EXPECTED_RUNTIME_WEBVIEW2_RESIDUE | Installer-owned files were removed; runtime-generated WebView2 user data remained in the temporary install root after app launch. |

## Evidence Boundary

- Existing prerelease assets were not changed.
- Release assets were not edited, deleted, replaced, re-uploaded, rebuilt, republished, or published.
- No new tags or releases were created.
- No merge to `main` was made.
- No production readiness, go-live approval, stable-channel certification, or stable/latest positioning is claimed.
- `v0.1.0-dev.4` artefacts still need a separate freeze, approval, and publication gate before any upload or release action.
- Future `v0.1.0-dev.4` artefacts must be rebuilt after this guard before publication consideration.
