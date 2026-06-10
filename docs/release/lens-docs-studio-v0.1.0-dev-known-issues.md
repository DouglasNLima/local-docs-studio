# Lens Docs Studio v0.1.0-dev Known Issues And Caveats

This document tracks known caveats for the public `v0.1.0-dev` prerelease. It is not a stable release readiness claim.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev`

## Current Known Issues / Caveats

- Installer is unsigned and may trigger Windows SmartScreen or browser download warnings.
- Runtime prerequisites are not bootstrapped by either asset. Users must install the .NET 8 Desktop Runtime, the matching Windows App SDK Runtime, and the Evergreen WebView2 Runtime separately.
- WebView2 user data may remain after uninstall.
- File associations are optional/default-safe. The installer must not be treated as a mandatory file association path.
- ZIP remains available as the portable/fallback package.
- ZIP and installer assets are prerelease assets only.

## Explicit Non-claims

- `v0.1.0-dev` is not stable.
- `v0.1.0-dev` is not the latest/stable GitHub release.
- Assets are not code signed.
- There is no auto-update.
- There is no runtime bootstrapper.
- There is no WebView2 bootstrapper.
- There is no MSIX package.
- There is no Store or winget publication.

## Feedback Handling

Use `docs/release/lens-docs-studio-v0.1.0-dev-feedback-intake.md` for structured feedback capture and triage.

Recommended next-cycle path:

- Choose `v0.1.0-dev.1` for targeted prerelease fixes or documentation corrections.
- Choose `v0.1.0-rc.1` only after prerelease feedback is understood and no blocker remains.
- Choose `v0.1.0` stable later, after the release-candidate path is complete.
