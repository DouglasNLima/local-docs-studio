# Phase 3BE Support Bundle Implementation

Date: 2026-06-12

## Summary

Phase 3BE implements the first approved `v0.1.0-dev.4` implementation slice: explicit-user-action Diagnostics support bundle generation.

Runtime code changed. This is not a release approval, production readiness claim, go-live approval, stable-channel certification, package publication, installer publication, tag operation, release operation, or `main` merge.

## Repository State Before Changes

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Required Phase 3BD commit | PASS, `94f2c0ecf8460ad84281494f8a1820774ad608ec` is an ancestor of `HEAD` |
| Tracked status before edits | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |

## Implemented Flow

The Windows shell Diagnostics dialog now includes **Create support bundle**.

The flow is:

1. The user opens **Help > Windows shell diagnostics**.
2. Diagnostics refresh bridge and Open folder routing state.
3. The user explicitly chooses **Create support bundle**.
4. The app creates a local JSON preview from allowlisted operational metadata already available in diagnostics and workspace state.
5. The user reviews the preview.
6. Only after the preview exists, **Copy summary** and **Export local JSON** are enabled.
7. The exported file uses a neutral filename: `lens-docs-studio-support-bundle-YYYYMMDDTHHMMSSZ.json`.

No network upload, telemetry, account flow, ticket creation, external service call, or automatic sharing path was added.

## Bundle Schema And Contents

The bundle is plain JSON with `schemaVersion: 1`.

Top-level fields:

- `schemaVersion`
- `generatedAtUtc`
- `app`
- `environment`
- `diagnostics`
- `openFolder`
- `watcher`
- `privacy`
- `redaction`

Included safe metadata:

- app name, generic tagline, version, build, and source commit when safely available;
- runtime mode category, packaged/native category, WebView2 shell detected state, origin category, platform category, and browser engine category;
- bridge message-handler state, ping state, safe host label, capability labels, last native request/response type labels, native error category, and bounded redacted native error message;
- `workspace.openFolder` capability state, Open folder route decision, browser fallback state, last attempt state/detail, selected workspace presence, workspace kind category, supported/skipped file count buckets, and active file state category;
- watcher event category, UTC timestamp when recorded, relative-path-only category, and dirty-conflict category;
- privacy and redaction declarations.

## Excluded Data

The support bundle does not include:

- Markdown, Mermaid, imported document, generated preview, Docs Site, Word, HTML, PDF, Markdown Bundle, or artefact bundle contents;
- full private file paths or folder paths by default;
- document titles or file names by default;
- secrets, tokens, API keys, passwords, connection strings, cookies, certificates, or environment variables;
- email addresses, usernames, machine names, private names, customer names, or raw PII;
- raw stack traces, raw logs, installer logs, WebView2 logs, browser storage, WebView2 user data, service-worker cache contents, screenshots, images, or binary attachments;
- `lens-artifact-bundle.json` content or other untrusted imported ZIP metadata.

## Redaction And Bounds

The support bundle builder uses allowlisted fields and category values. Free-text diagnostic detail is redacted and bounded before entering the JSON or copied summary.

Redaction covers:

- Windows, UNC, home, and Unix-like private path patterns;
- email-like text;
- token, access token, API key, secret, password, signature, and connection-string key/value fragments;
- URL query strings;
- stack trace line patterns.

Free-text diagnostic fields are limited to 160 characters. Count fields are converted to buckets.

## Tests Added Or Updated

Browser smoke coverage was added for:

- support bundle builder redaction for private paths, email addresses, secrets/tokens/connection strings, raw stack traces, long diagnostic strings, screenshot exclusion, document-content exclusion, and untrusted metadata exclusion;
- Diagnostics support bundle action visibility;
- explicit user action requirement before bundle preview/copy/export;
- generated JSON preview schema and safe field inclusion;
- exclusion of document content, full private paths, secrets, raw stack traces, and automatic upload/telemetry behaviour;
- neutral local JSON export filename.

Existing diagnostics, onboarding, native Open folder, fallback, and watcher/conflict browser tests remain in the same smoke suite.

## Validation

| Command | Result | Notes |
| --- | --- | --- |
| `npm run test:static` | PASS | Static checks passed for 47 module files, 54 shell assets, 150 vendor assets, and 53 runtime external-dependency scans. |
| `npm run test:browser` | PASS | 242 Playwright browser smoke tests passed across Chromium and Microsoft Edge. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 6 existing PRI qualifier warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Verified 54 service-worker assets and 150 vendor assets in packaged `StaticApp/`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Generated ignored local RC reports under `artifacts/windows/release-candidates/`; ZIP SHA256 `E0F21BB9B3B956C06464B1F17038A5B8369D0BF14BAAA24284885771DE6F6EFF`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development build native bridge smoke completed successfully. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Final ignored local Windows folder/ZIP package rebuilt; native smoke intentionally skipped by this package command flag. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` | PASS | Packaged native bridge smoke completed successfully against the final freshly rebuilt executable. |

## Package And Release Artefact Impact

Runtime static app code changed, including `assets/scripts/`, `assets/styles/app.css`, `index.html`, and `service-worker.js`.

Future `v0.1.0-dev.4` package artefacts must be rebuilt before any authorised publication because packaged static/runtime files changed. This phase does not build, upload, publish, replace, or approve release artefacts.

## Evidence Boundary

- Phase 3BE did not edit, delete, replace, re-upload, rebuild, or republish `v0.1.0-dev.1`, `v0.1.0-dev.2`, or `v0.1.0-dev.3` release assets.
- Phase 3BE did not create tags or GitHub Releases.
- Phase 3BE did not merge to `main`.
- Phase 3BE did not add telemetry or automatic upload.
- Phase 3BE did not collect document contents, raw stack traces, secrets, tokens, connection strings, emails, private names, or PII.
- Phase 3BE does not claim production readiness or go-live approval.
