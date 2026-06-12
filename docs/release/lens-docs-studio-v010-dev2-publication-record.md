# Lens Docs Studio v0.1.0-dev.2 Publication Record

Publication date: 2026-06-12

## Summary

`v0.1.0-dev.2` was published as a GitHub prerelease/dev release from the frozen Phase 3AR publication bundle.

This publication did not rebuild the package or installer, did not upload artefacts from any other path, did not edit `v0.1.0-dev.1` release assets, did not merge to `main`, and did not claim production readiness, go-live approval, stable-channel certification, or production release status.

Manual packaged sanity remains blocked because the Phase 3AR session could not provide real interactive observation/control of the packaged WebView2 window, onboarding, native folder picker, or watcher/conflict scenario. It must not be described as passed.

## Release Identity

| Field | Value |
| --- | --- |
| Release URL | `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.2` |
| Release tag | `v0.1.0-dev.2` |
| Release state | Prerelease/dev release |
| Frozen artefact source commit | `5602358ef241c144fccae13e980c9eb2920126d8` |
| Publication preflight evidence commit | `64020477f4dfbf3b9cf17c77d8471808cb3bf227` |
| Publication documentation commit | Recorded by the Git commit that introduced this document and reported in the publication hand-off |
| Branch | `develop` |
| Main merge performed | No |

## Frozen Publication Bundle

Bundle path:

```text
C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.2\
```

Only the frozen Phase 3AR bundle assets were uploaded.

## Published Assets

| Asset | Size (bytes) | Purpose |
| --- | ---: | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | 34,031,627 | Portable Windows ZIP package |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | 29,689,316 | Unsigned Inno Setup installer |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | 110 | Installer checksum file |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | 154,014 | Installer build report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T105712Z.md` | 8,432 | Release-candidate report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T105712Z.json` | 4,940 | Release-candidate metadata |

No unexpected assets were attached.

## Published Checksums

| Artefact | SHA256 |
| --- | --- |
| ZIP | `C7C9E52322EDA140D9AB60F9D8D2BF257EED9EA898F50FC8EFA2D90A04A9BF0B` |
| Installer | `01C60DFAA57754EFFCCC763051D5EEDE0DB4E3284936C8BF7D1189D30DCA6C21` |

The ZIP and installer SHA256 values were recomputed from the frozen local bundle before upload and matched the Phase 3AR frozen values.

## Publication Verification

| Check | Result |
| --- | --- |
| Branch was `develop` before publication | PASS |
| Tracked worktree was clean before publication | PASS |
| Release `v0.1.0-dev.2` absent before creation | PASS |
| Remote tag `v0.1.0-dev.2` absent before creation | PASS |
| Frozen files existed before upload | PASS |
| ZIP SHA256 matched frozen Phase 3AR value | PASS |
| Installer SHA256 matched frozen Phase 3AR value | PASS |
| Tag exists after publication | PASS |
| Tag resolves to frozen artefact source commit `5602358ef241c144fccae13e980c9eb2920126d8` | PASS |
| Release exists after publication | PASS |
| Release is marked prerelease | PASS |
| Release is not marked as a production release | PASS |
| Expected six assets are attached | PASS |
| No unexpected assets are attached | PASS |
| `v0.1.0-dev.1` assets were not changed | PASS |
| Main merge was not performed | PASS |
| Production readiness/go-live was not claimed | PASS |
| Manual packaged sanity | BLOCKED_MANUAL_PACKAGED_SANITY_NOT_EXECUTED |

## Evidence Caveat

Phase 3AR manual packaged sanity remains blocked. The packaged WebView2 window, onboarding, native folder picker, and watcher/conflict scenario were not manually observed/controlled in a real interactive packaged session during Phase 3AR or this publication step.

## Follow-up

- Treat `v0.1.0-dev.2` as the current prerelease/dev release for new validation.
- Keep `v0.1.0-dev.1` assets intact as historical prerelease evidence.
- Keep production readiness, go-live approval, stable/latest positioning, signing, and `main` promotion as separate future gates.
