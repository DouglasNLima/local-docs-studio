# Lens Docs Studio v0.1.0-dev.3 Publication Record

Publication date: 2026-06-12

Publication verification time: 2026-06-12T15:14:58Z

## Summary

`v0.1.0-dev.3` was published as a GitHub prerelease/dev release from the frozen Phase 3AY publication bundle.

This publication did not rebuild the package or installer, did not upload artefacts from live output paths or any other path, did not edit `v0.1.0-dev.1` or `v0.1.0-dev.2` release assets, did not merge to `main`, and did not claim production readiness, go-live approval, stable-channel certification, or production release status.

## Release Identity

| Field | Value |
| --- | --- |
| Release URL | `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.3` |
| Release tag | `v0.1.0-dev.3` |
| Release state | Prerelease/dev release, not draft |
| Publication evidence commit before release | `1e8f15bf177046333cdc86076487cee74f647170` |
| Frozen artefact source commit | `ea75e13bf72518eb417cf11e4f62d7730738c8cd` |
| Release tag target | `1e8f15bf177046333cdc86076487cee74f647170` |
| Publication documentation commit | Recorded by the Git commit that introduced this document and reported in the publication hand-off |
| Branch | `develop` |
| Main merge performed | No |

## Frozen Publication Bundle

Bundle path:

```text
C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\
```

Only the frozen Phase 3AY bundle assets were uploaded.

## Published Assets

| Asset | Size (bytes) | Purpose |
| --- | ---: | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | 34,054,203 | Portable Windows ZIP package |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | 29,689,155 | Unsigned Inno Setup installer |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | 110 | Installer checksum file |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | 155,006 | Installer build report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.md` | 8,432 | Release-candidate report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.json` | 4,940 | Release-candidate metadata |

No unexpected assets were attached.

## Published Checksums

| Artefact | SHA256 |
| --- | --- |
| ZIP | `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` |
| Installer | `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |

The ZIP and installer SHA256 values were recomputed from the frozen local bundle before upload and matched the Phase 3AY frozen values.

## Publication Verification

| Check | Result |
| --- | --- |
| Branch was `develop` before publication | PASS |
| Tracked worktree was clean before publication | PASS |
| Release `v0.1.0-dev.3` absent before creation | PASS |
| Remote tag `v0.1.0-dev.3` absent before creation | PASS |
| Frozen files existed before upload | PASS |
| ZIP SHA256 matched frozen Phase 3AY value | PASS |
| Installer SHA256 matched frozen Phase 3AY value | PASS |
| Tag exists after publication | PASS |
| Tag resolves to authorised evidence commit `1e8f15bf177046333cdc86076487cee74f647170` | PASS |
| Release exists after publication | PASS |
| Release is marked prerelease | PASS |
| Release is not marked as a production release | PASS |
| Expected six assets are attached | PASS |
| No unexpected assets are attached | PASS |
| `v0.1.0-dev.1` assets were not changed | PASS |
| `v0.1.0-dev.2` assets were not changed | PASS |
| Main merge was not performed | PASS |
| Production readiness/go-live was not claimed | PASS |

## Follow-up

- Treat `v0.1.0-dev.3` as the current prerelease/dev release for new validation.
- Keep `v0.1.0-dev.1` and `v0.1.0-dev.2` assets intact as historical prerelease evidence.
- Keep production readiness, go-live approval, stable/latest positioning, signing, and `main` promotion as separate future gates.
