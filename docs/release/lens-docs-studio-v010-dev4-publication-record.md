# Lens Docs Studio v0.1.0-dev.4 Publication Record

Publication date: 2026-06-13

Publication verification time: 2026-06-13T11:25:13Z

## Summary

`v0.1.0-dev.4` was published as a GitHub prerelease/dev release from the frozen Phase 3BJ publication bundle.

This publication did not rebuild the package or installer, did not upload artefacts from live output paths or any other path, did not edit `v0.1.0-dev.1`, `v0.1.0-dev.2`, or `v0.1.0-dev.3` release assets, did not merge to `main`, and did not claim production readiness, go-live approval, stable-channel certification, or production release status.

## Release Identity

| Field | Value |
| --- | --- |
| Release URL | `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.4` |
| Release tag | `v0.1.0-dev.4` |
| Release state | Prerelease/dev release, not draft |
| Publication evidence commit before release | `2555a7554e9e625af7e5d9da93725f77f4997ded` |
| Frozen artefact source commit | `ce0d34bf19aaac4d67e91bef05343bc608cbd433` |
| Release tag target | `2555a7554e9e625af7e5d9da93725f77f4997ded` |
| Publication documentation commit | Recorded by the Git commit that introduced this document and reported in the publication hand-off |
| Branch | `develop` |
| Main merge performed | No |

## Frozen Publication Bundle

Bundle path:

```text
C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.4\
```

Only the frozen Phase 3BJ bundle assets were uploaded.

## Published Assets

| Asset | Size (bytes) | Purpose |
| --- | ---: | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | 34,115,305 | Portable Windows ZIP package |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | 25,936,701 | Unsigned Inno Setup installer |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | 110 | Installer checksum file |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | 64,593 | Installer build report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260613T105206Z.md` | 9,011 | Release-candidate report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260613T105206Z.json` | 5,534 | Release-candidate metadata |

No unexpected assets were attached.

## Published Checksums

| Artefact | SHA256 |
| --- | --- |
| ZIP | `06BFA3896E1CCA48F1DC87D2F49D2387A8BCB93078E9F070764F22B048DD860C` |
| Installer | `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |

The ZIP and installer SHA256 values were recomputed from the frozen local bundle before upload and matched the Phase 3BJ frozen values.

## Publication Verification

| Check | Result |
| --- | --- |
| Branch was `develop` before publication | PASS |
| Tracked worktree was clean before publication | PASS |
| Release `v0.1.0-dev.4` absent before creation | PASS |
| Remote tag `v0.1.0-dev.4` absent before creation | PASS |
| Frozen files existed before upload | PASS |
| ZIP SHA256 matched frozen Phase 3BJ value | PASS |
| Installer SHA256 matched frozen Phase 3BJ value | PASS |
| Tag exists after publication | PASS |
| Tag resolves to authorised evidence commit `2555a7554e9e625af7e5d9da93725f77f4997ded` | PASS |
| Frozen artefact source commit remains recorded in uploaded RC metadata and installer report | PASS |
| Release exists after publication | PASS |
| Release is marked prerelease | PASS |
| Release is not marked as a production release | PASS |
| Expected six assets are attached | PASS |
| No unexpected assets are attached | PASS |
| `v0.1.0-dev.1` assets were not changed | PASS |
| `v0.1.0-dev.2` assets were not changed | PASS |
| `v0.1.0-dev.3` assets were not changed | PASS |
| Main merge was not performed | PASS |
| Production readiness/go-live was not claimed | PASS |

Provenance note: the release tag points to publication evidence commit `2555a7554e9e625af7e5d9da93725f77f4997ded`, while the frozen Windows package and installer artefacts were built from source commit `ce0d34bf19aaac4d67e91bef05343bc608cbd433`. During publication, the local tag was initially created against the frozen source commit and was corrected before final verification so the final remote tag target matches the established prerelease evidence-commit model.

## Important Evidence Notes

- Phase 3BJ payload hygiene passed.
- Frozen ZIP had 0 blocked WebView2/runtime payload paths.
- Installer report had no blocked runtime payload markers.
- Clean install/uninstall passed with expected runtime WebView2 residue after app launch.
- True upgrade from published `v0.1.0-dev.3` remains blocked by the already-published baseline installer rollback.
- This release remains a prerelease/dev release only.

## Follow-up

- Treat `v0.1.0-dev.4` as the current prerelease/dev release for new validation.
- Keep `v0.1.0-dev.1`, `v0.1.0-dev.2`, and `v0.1.0-dev.3` assets intact as historical prerelease evidence.
- Keep production readiness, go-live approval, stable/latest positioning, signing, and `main` promotion as separate future gates.
