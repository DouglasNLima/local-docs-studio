# Lens Docs Studio v0.1.0-dev Known Issues And Caveats

This document tracks known caveats for the public `v0.1.0-dev` prerelease. It is not a stable release readiness claim.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev`

## Current Known Issues / Caveats

- Installer is unsigned and may trigger Windows SmartScreen or browser download warnings.
- Runtime prerequisites are not bootstrapped by either asset. Users must install the .NET 8 Desktop Runtime, the matching Windows App SDK Runtime, and the Evergreen WebView2 Runtime separately.
- WebView2 user data may remain after uninstall under `%LOCALAPPDATA%\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe.WebView2`. This is documented-only for the next dev prerelease because deleting it silently could remove browser-local user/session state.
- File associations are optional/default-safe. The installer must not be treated as a mandatory file association path.
- ZIP remains available as the portable/fallback package for manual extraction, smoke validation, and environments that avoid installers. It does not create a Start Menu entry or uninstall entry unless the user creates shortcuts manually.
- The Inno installer is the easier Windows install path. It creates a Start Menu shortcut, offers an optional Desktop shortcut, keeps file associations opt-in/default-safe, remains unsigned, still requires runtime prerequisites, and supports uninstall with the WebView2 user data caveat above.
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

Phase 3R triage for the next targeted prerelease cycle is recorded in `docs/release/lens-docs-studio-v0.1.0-dev.1-planning.md`.

Phase 3T certification planning is recorded in `docs/release/lens-docs-studio-v0.1.0-dev.1-certification-plan.md`.

Recommended next-cycle path:

- Choose `v0.1.0-dev.1` for targeted prerelease fixes or documentation corrections.
- Choose `v0.1.0-rc.1` only after prerelease feedback is understood and no blocker remains.
- Choose `v0.1.0` stable later, after the release-candidate path is complete.
