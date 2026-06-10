# Lens Docs Studio v0.1.0-dev Publication Record

## Release Summary

- Release: `Lens Docs Studio v0.1.0-dev`
- Tag: `v0.1.0-dev`
- Published as: public GitHub prerelease
- Stable/latest: no
- Target commit: `8b215d039188af94d32857239e6829916f4b74fc`
- Publication date: `2026-06-10`
- Repository: `DouglasNLima/local-docs-studio`
- Branch state at publication: `develop` at `0fb071a143684d67d3d0984ef74be311bb457903`, aligned with `origin/develop`
- Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev`

## Assets

| Asset | Purpose | SHA256 |
| --- | --- | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | Portable/fallback package | `8CD380C42C33AAB8FE02C82184FF0A6FFDE6FC0C6B077BE40E0CF6A51C7E6188` |
| `LensDocsStudio.Windows-0.1.0-dev.zip.sha256` | ZIP verification | n/a |
| `LensDocsStudio.Windows-0.1.0-dev-rc-report.md` | Certification evidence | n/a |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | Unsigned Inno Setup prerelease installer | `152051F7CDAB5A33F8D8E5C219F937A54777687B8980D750B486BE572883FA8E` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | Installer verification | n/a |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | Installer build/install evidence | n/a |

## Verification

- Pre-flight checks: the final internal RC review confirmed the ZIP and installer assets, release notes, checksums, ZIP smoke, installer install/uninstall smoke, and native bridge smoke before publication.
- Post-publish checks: the release is public, remains marked as a prerelease, and is not marked as stable/latest.
- Public download checksum verification: ZIP and installer checksums are recorded above and in the published checksum assets.
- Release notes review: release notes identify the ZIP fallback, unsigned Inno Setup installer, runtime prerequisites, optional file associations, uninstall caveat, and prerelease status.

## Runtime Prerequisites

- .NET 8 Desktop Runtime
- Windows App SDK Runtime matching the project package version
- Evergreen WebView2 Runtime

## Explicit Non-claims

- Not stable.
- Not latest.
- Not signed.
- No auto-update.
- No runtime bootstrapper.
- No WebView2 bootstrapper.
- No MSIX.
- No Store/winget publication.
- File associations are optional/default-safe.

## Known Caveats

- WebView2 user data may remain after uninstall.
- Installer is unsigned.
- Runtime prerequisites must be installed separately.
- ZIP and installer assets are prerelease assets only.

## Feedback Intake

Use `docs/release/lens-docs-studio-v0.1.0-dev-feedback-intake.md` to capture structured prerelease feedback.

Known issues and caveats are summarised in `docs/release/lens-docs-studio-v0.1.0-dev-known-issues.md`.

## Next Options

- `v0.1.0-dev.1`: preferred if public prerelease feedback finds targeted fixes or documentation clarifications before a release-candidate label.
- `v0.1.0-rc.1`: use when the prerelease feedback queue is clear enough to begin a stable-candidate pass.
- `v0.1.0` stable: defer until prerelease/RC feedback, prerequisite communication, installer confidence, and stable publication readiness are complete.

Phase 3R planning for the recommended `v0.1.0-dev.1` scope is recorded in `docs/release/lens-docs-studio-v0.1.0-dev.1-planning.md`.
