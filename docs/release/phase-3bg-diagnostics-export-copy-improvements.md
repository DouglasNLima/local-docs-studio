# Phase 3BG Diagnostics Export Copy Improvements

Date: 2026-06-13

## Summary

Phase 3BG implements the next `v0.1.0-dev.4` slice: Diagnostics copy/export usability improvements while preserving the Phase 3BE support-bundle privacy model.

Runtime static app code changed. This is not a release approval, production readiness claim, go-live approval, stable-channel certification, package publication, installer publication, tag operation, release operation, or `main` merge.

## Repository State Before Changes

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Required Phase 3BF commit | PASS, `9685b502035559a575a34c3fae086efddc0e47ff` is an ancestor of `HEAD` |
| Tracked status before edits | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |

## What Changed

- Diagnostics now explains the copy/export choices more directly:
  - **Copy summary** creates concise local diagnostics text.
  - **Export local JSON** saves the reviewed support bundle preview as a local JSON file with a neutral filename.
  - No automatic upload occurs.
  - Private document contents are excluded.
- The copied summary is now labelled **Lens Docs Studio diagnostics summary** and focuses on operational state rather than the full JSON.
- Diagnostics shows bounded local action messages after preview creation, copy success/failure, and export success.
- The exported JSON keeps `schemaVersion: 1`; the schema did not change.

## User-Visible Flow

1. The user opens **Help > Windows shell diagnostics**.
2. Diagnostics refreshes bridge, route, browser fallback, and Open folder state.
3. The user explicitly chooses **Create support bundle**.
4. The app creates a local preview from allowlisted operational metadata.
5. **Copy summary** and **Export local JSON** remain disabled until the preview exists.
6. The user explicitly chooses either copy or export.
7. Diagnostics shows a bounded success/failure message for the chosen action.

No upload, telemetry, account flow, ticket creation, external service call, or automatic sharing path was added.

## Safe Summary Contents

The copied diagnostics summary may include:

- app version and app build;
- support bundle schema version;
- generated UTC timestamp;
- runtime mode;
- packaged/native mode;
- WebView2 shell detected state;
- bridge ping state;
- `workspace.openFolder` capability state;
- Open folder route decision;
- browser fallback state;
- last Open folder attempt state;
- watcher event category;
- watcher timestamp bucket;
- privacy declaration.

## Excluded Data

The copied summary and exported JSON continue to exclude:

- Markdown, Mermaid, imported document, generated preview, Docs Site, Word, HTML, PDF, Markdown Bundle, or artefact bundle contents;
- full private file paths and folder paths by default;
- document titles or file names by default;
- imported ZIP contents;
- secrets, tokens, API keys, passwords, connection strings, cookies, certificates, or environment variables;
- email addresses, private names, usernames, machine names, customer names, or raw PII;
- raw stack traces, raw logs, installer logs, WebView2 logs, browser storage, WebView2 user data, service-worker cache contents, screenshots, images, or binary attachments;
- `lens-artifact-bundle.json` content or other untrusted imported ZIP metadata.

## Privacy And Redaction Boundaries

The Phase 3BE allowlist remains in force. Free-text diagnostic detail is redacted and bounded before entering the JSON or copied summary.

Redaction still covers Windows paths, UNC paths, home and Unix-like paths, email-like text, token/secret/password/signature/connection-string fragments, URL query strings, and stack-trace line patterns.

Copy/export remains preview-first and explicit-user-action only. The export filename remains neutral:

```text
lens-docs-studio-support-bundle-YYYYMMDDTHHMMSSZ.json
```

## Tests Added Or Updated

Browser smoke coverage was updated for:

- copied diagnostics summary fields, including app version/build, packaged/native mode, browser fallback state, route state, bridge state, Open folder attempt state, watcher category, timestamp bucket, and schema version;
- copied summary exclusion of private paths, document content, tokens, secrets, emails, and raw stack-trace lines;
- copy requiring explicit user action after preview creation;
- export requiring explicit user action after preview creation;
- neutral export filename avoiding workspace/private names;
- bounded copy/export success and failure messages;
- local-only/no-upload wording and behaviour;
- existing support bundle preview/export privacy checks.

## Validation

| Command | Result | Notes |
| --- | --- | --- |
| `npm run test:static` | PASS | Static checks passed for 47 module files, 54 shell assets, 150 vendor assets, and 53 runtime external-dependency scans. |
| `npm run test:browser` | PASS | 244 Playwright browser smoke tests passed across Chromium and Microsoft Edge. First run timed out at 10 minutes, then exposed two new expectation mismatches; after correcting the assertions, the targeted Diagnostics tests and full suite passed. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 0 warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Verified 54 service-worker assets and 150 vendor assets in packaged `StaticApp/`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Generated ignored local RC reports under `artifacts/windows/release-candidates/`; ZIP SHA256 `465A8BAC90E6AC546B58BF8BD4A71AB82B5593C7564630A3A2C64094C98F1711`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development build native bridge smoke completed successfully. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Ignored local Windows folder/ZIP package rebuilt; static asset validation passed; package-command native smoke intentionally skipped by `-NoSmoke`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` | PASS | Packaged native bridge smoke completed successfully against the freshly rebuilt executable. |

## Package And Release Artefact Impact

Runtime static app code changed in `assets/scripts/`, `assets/styles/app.css`, and browser tests.

Future `v0.1.0-dev.4` package artefacts must be rebuilt before any authorised publication because packaged static/runtime files changed. This phase created only ignored local validation artefacts and did not upload or publish anything.

## Evidence Boundary

- Phase 3BG did not edit, delete, replace, re-upload, rebuild, or republish existing `v0.1.0-dev.1`, `v0.1.0-dev.2`, or `v0.1.0-dev.3` release assets.
- Phase 3BG did not create tags or GitHub Releases.
- Phase 3BG did not merge to `main`.
- Phase 3BG did not add telemetry or automatic upload.
- Phase 3BG did not collect document contents, raw stack traces, secrets, tokens, connection strings, emails, private names, browser storage, WebView2 user data, or PII.
- Phase 3BG does not claim production readiness or go-live approval.
