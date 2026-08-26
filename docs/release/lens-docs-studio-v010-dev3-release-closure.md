# Lens Docs Studio v0.1.0-dev.3 Release Closure

Closure date: 2026-06-12

## Summary

Phase 3BB closes the `v0.1.0-dev.3` prerelease evidence chain as documentation-only release closure and roadmap rebaseline.

`v0.1.0-dev.3` is the current verified prerelease/dev release for new validation. It is not a production release, stable-channel certification, go-live approval, stable/latest positioning, or a promotion to `main`.

No release assets, tags, GitHub Releases, packages, installers, or runtime code were edited, deleted, replaced, re-uploaded, rebuilt, republished, or changed during this closure. No merge to `main` was performed. `v0.1.0-dev.1` and `v0.1.0-dev.2` assets were not changed.

## Release Identity

| Field | Value |
| --- | --- |
| Release URL | `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.3` |
| Release tag | `v0.1.0-dev.3` |
| Release state | Prerelease/dev release |
| Tag target | `1e8f15bf177046333cdc86076487cee74f647170` |
| Frozen artefact source commit | `ea75e13bf72518eb417cf11e4f62d7730738c8cd` |
| Publication evidence commit | `1e8f15bf177046333cdc86076487cee74f647170` |
| Post-publication verification commit | `0bfd517e0fe116cad46839a3b4d7f806a84d3ccd` |
| Main merge performed | No |

## Published Assets

The published release contains the expected six assets only:

| Asset | SHA256 / status |
| --- | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | Published checksum file |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | Published installer report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.md` | Published release-candidate report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.json` | Published release-candidate metadata |

## Evidence Summary

Phase 3AW investigated the `v0.1.0-dev.2` packaged Open folder native picker failure. The exact no-dialog/no-return symptom was not reproduced in that retry, but the app was hardened so a non-returning native Open folder request now shows bounded operator guidance while keeping browser fallback inactive in packaged WebView2. Phase 3AW manual packaged verification passed against a freshly rebuilt package, including onboarding, diagnostics, visible native picker selection, cancellation, inactive browser fallback, and watcher changed-on-disk copy.

Phase 3BA post-publication verification for `v0.1.0-dev.3` passed for the tag, prerelease release state, expected six-asset inventory, downloaded ZIP SHA256, downloaded installer SHA256, downloaded ZIP smoke, and contained installer smoke.

Manual packaged sanity for Phase 3BA remained `SKIPPED_INTERACTIVE_NATIVE_UI_CONTROL_NOT_EXECUTED`. Phase 3AW remains the referenced manual packaged check for interactive native UI behaviours.

## Known Limitations

- `v0.1.0-dev.3` remains a prerelease/dev release.
- Production readiness, go-live approval, stable-channel certification, stable/latest positioning, signing, and `main` promotion remain separate future gates.
- Phase 3BA did not repeat real interactive packaged UI observation/control. Phase 3AW remains the referenced manual packaged sanity/remediation evidence.
- The exact Phase 3AV no-dialog/no-return condition was not reproduced in Phase 3AW; the dev.3 remediation improves the pending guidance path if that condition recurs.

## Roadmap Rebaseline

- Use `v0.1.0-dev.3` for new prerelease validation, support evidence, download guidance, and release monitoring.
- Treat `v0.1.0-dev.2` as superseded for new validation but preserved historically.
- Treat `v0.1.0-dev.1` as superseded for new validation but preserved historically.
- Treat `v0.1.0-dev` as older/stale.
- No further release asset action is required unless a future release is explicitly authorised.

## Post-v0.1.0-dev.3 Next Work

- Collect prerelease feedback against `v0.1.0-dev.3`.
- Monitor Open folder pending guidance feedback.
- Monitor installer, download, checksum, and runtime-prerequisite issues.
- Decide the next dev release scope from observed feedback and release-monitoring signals.
- Keep production readiness and go-live approval as a separate approval gate.
- Consider support bundle implementation only after explicit approval.

## Validation

Phase 3BB is documentation-only. The requested validation for this closure is:

| Command | Result |
| --- | --- |
| `npm run test:static` | PASS, static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS, build succeeded with 6 existing PRI qualifier warnings and 0 errors |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS, Debug Windows shell build succeeded with 0 warnings and 0 errors; packaged `StaticApp/` verified 53 service-worker assets and 150 vendor assets |

Browser tests, package rebuilds, installer rebuilds, release commands, tag commands, asset uploads, and manual smoke are skipped for Phase 3BB because this phase is documentation-only and does not change runtime code or release artefacts.

## Closure Verdict

`v0.1.0-dev.3` is closed as the current verified prerelease/dev release. The release evidence chain is complete for the dev-release boundary, with production readiness and go-live approval explicitly unclaimed.
