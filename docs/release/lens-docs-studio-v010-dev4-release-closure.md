# Lens Docs Studio v0.1.0-dev.4 Release Closure

Closure date: 2026-06-13

## Summary

Phase 3BM closes the `v0.1.0-dev.4` prerelease evidence chain as documentation-only release closure and roadmap rebaseline.

`v0.1.0-dev.4` is the current verified prerelease/dev release for new validation. It is not a production release, stable-channel certification, go-live approval, or promotion to `main`.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.4`

This closure did not edit, delete, replace, re-upload, rebuild, or republish release assets. It did not create, move, or delete tags. It did not create, edit, publish, unpublish, or delete GitHub Releases. It did not merge to `main`.

## Release Identity

| Field | Value |
| --- | --- |
| Release tag | `v0.1.0-dev.4` |
| Release URL | `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.4` |
| Release type | Prerelease/dev release |
| Tag target | `2555a7554e9e625af7e5d9da93725f77f4997ded` |
| Frozen artefact source commit | `ce0d34bf19aaac4d67e91bef05343bc608cbd433` |
| Publication evidence commit | `2555a7554e9e625af7e5d9da93725f77f4997ded` |
| Publication documentation commit | `a8f478ed9fb3bfd8c3c9beba495fc1a7f5775df3` |
| Post-publication verification commit | `56ef9e29471adc4efb80294ea5de2b12c3853fd9` |
| Main merge performed | No |
| Production readiness/go-live claimed | No |

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Required Phase 3BL commit | PASS, `56ef9e29471adc4efb80294ea5de2b12c3853fd9` is in `HEAD` history |
| Starting tracked status | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |

`git status --short --ignored` emitted long-path warnings while scanning generated WebView2 cache folders under ignored Windows build output. Those paths are generated runtime/build output, not tracked source.

## Published Assets

The published release has the expected six assets only:

| Asset | Purpose |
| --- | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | Portable Windows ZIP package |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | Unsigned Inno Setup installer |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | Installer checksum file |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | Installer build report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260613T105206Z.md` | Release-candidate report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260613T105206Z.json` | Release-candidate metadata |

Published checksum evidence:

| Artefact | SHA256 |
| --- | --- |
| ZIP | `06BFA3896E1CCA48F1DC87D2F49D2387A8BCB93078E9F070764F22B048DD860C` |
| Installer | `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |

## Evidence Summary

Phase 3BE implemented explicit-user-action Diagnostics support bundle generation. The bundle is local, preview-first, allowlisted, bounded, redacted, and excludes document contents, private paths by default, secrets, tokens, raw stack traces, browser storage, WebView2 user data, screenshots, and untrusted ZIP metadata. It added copy/export only after the user creates a preview, with no upload, telemetry, account, ticket, or external service path.

Phase 3BG improved Diagnostics copy/export wording and local action feedback. The copied summary focuses on safe operational state, the exported JSON preserves schema version 1, copy/export remain preview-first and explicit, and private document contents remain excluded.

Phase 3BI added package and installer payload hygiene guards so runtime-generated WebView2 user data, service-worker cache paths, browser storage, caches, and logs cannot enter Windows ZIP or Inno installer payloads. The final frozen and published artefacts passed payload hygiene checks.

Phase 3BH recorded the upgrade limitation. A true upgrade from the already-published `v0.1.0-dev.3` installer remains blocked because that baseline installer rolled back while copying a deep runtime-generated WebView2 Service Worker cache path. The `v0.1.0-dev.4` publication did not alter the already-published baseline asset and does not claim true upgrade success from `v0.1.0-dev.3`.

## Post-Publication Verification

Phase 3BL post-publication verification passed:

| Check | Result |
| --- | --- |
| Tag verification | PASS |
| Prerelease verification | PASS |
| Asset verification | PASS, expected six assets only |
| Downloaded ZIP SHA256 | PASS, `06BFA3896E1CCA48F1DC87D2F49D2387A8BCB93078E9F070764F22B048DD860C` |
| Downloaded installer SHA256 | PASS, `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |
| Downloaded ZIP smoke | PASS |
| Payload hygiene | PASS |
| Installer smoke | PASS |
| Manual packaged sanity | `SKIPPED_INTERACTIVE_NATIVE_UI_CONTROL_NOT_EXECUTED` |

Phase 3AW remains the referenced manual packaged verification for interactive native UI behaviours, including onboarding, diagnostics, visible native picker selection, cancellation, inactive browser fallback, and watcher changed-on-disk copy after the Open folder pending guidance remediation. Phase 3BL did not complete real interactive packaged UI observation/control for those behaviours.

## Remaining Known Limitations

- `v0.1.0-dev.4` remains a prerelease/dev release, not a production release.
- Production readiness, go-live approval, stable-channel certification, stable/latest positioning, signing, and `main` promotion remain separate future gates.
- True upgrade from `v0.1.0-dev.3` remains blocked by the already-published baseline installer rollback.
- Phase 3BL skipped interactive native UI control; Phase 3AW remains the referenced manual packaged verification.
- The unsigned installer can trigger operating-system, browser, or SmartScreen friction.
- Runtime prerequisites remain external and are not bootstrapped by the installer.
- WebView2 user data may remain after uninstall so browser-local state is not silently deleted.
- Manual packaged sanity helper implementation remains unimplemented and should be considered only after explicit approval.

## Supersedence

`v0.1.0-dev.4` supersedes `v0.1.0-dev.3` for new validation. `v0.1.0-dev.3` is preserved historically, including its publication, verification, release closure, and Phase 3AW manual packaged sanity reference.

`v0.1.0-dev.2` and `v0.1.0-dev.1` are also superseded for new validation but preserved historically. The older `v0.1.0-dev` release remains older/stale.

`v0.1.0-dev.1`, `v0.1.0-dev.2`, and `v0.1.0-dev.3` assets were not changed by the `v0.1.0-dev.4` publication, verification, or closure evidence.

No further `v0.1.0-dev.4` release asset action is required unless a future release-maintenance decision is explicitly authorised.

## Post-v0.1.0-dev.4 Next Work

- Collect prerelease feedback against `v0.1.0-dev.4`.
- Monitor support bundle and Diagnostics copy/export feedback.
- Monitor payload hygiene, installer, download, checksum, and runtime-prerequisite issues.
- Decide whether to address upgrade path limitations in a future release.
- Decide the next dev release scope from feedback and release-monitoring signals.
- Keep production readiness and go-live approval as a separate approval gate.
- Consider manual packaged sanity helper implementation only after explicit approval.

## Validation

Validation for the Phase 3BM documentation-only closure and roadmap rebaseline:

| Command | Result |
| --- | --- |
| `npm run test:static` | PASS, static checks passed for 47 module files, 54 shell assets, 150 vendor assets, and 53 runtime external-dependency scans |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS, build succeeded with 0 warnings and 0 errors |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS, payload hygiene self-test passed and 54 service-worker assets plus 150 vendor assets were verified in packaged `StaticApp/` |

Browser tests, package rebuilds, installer rebuilds, release commands, release asset operations, tag operations, and manual smoke were intentionally skipped because Phase 3BM changed only documentation and evidence guidance.

## Evidence Boundary

- Runtime code did not change.
- Release assets were not edited, deleted, replaced, re-uploaded, rebuilt, republished, or published.
- No new tags or releases were created.
- No `main` merge was performed.
- `v0.1.0-dev.1`, `v0.1.0-dev.2`, and `v0.1.0-dev.3` assets were not changed.
- Downloaded/generated artefacts remain ignored and outside git.
- Production readiness, go-live approval, stable-channel certification, stable/latest positioning, and production release status are not claimed.
