# Lens Docs Studio Support Bundle Design

Design date: 2026-06-12

## Status

Phase 3AN is a design-only gate for a possible future troubleshooting/support bundle.

This document does not approve implementation, does not change runtime code, does not authorise package or installer rebuilds, does not change release assets, does not create tags or releases, does not merge to `main`, and does not claim production readiness or go-live approval.

## Purpose

The future support bundle should help a user report bridge, folder picker, watcher/conflict, package, and installer issues without exposing private document content, secrets, tokens, customer data, or unnecessary local data.

The bundle is for user-initiated troubleshooting. It should summarise operational state that already appears in safe diagnostics or validation evidence, package it into a small local artefact, and let the user inspect the contents before sharing it through their chosen support channel.

## Non-Goals

- No support bundle generation is implemented by this phase.
- No automatic upload, telemetry, background submission, account sign-in, ticket creation, or network call is approved.
- No document content, rendered preview content, imported ZIP content, browser storage dump, WebView2 user data folder, raw logs, raw stack traces, screenshots, secrets, tokens, connection strings, usernames, machine names, or full private local paths are included by default.
- No evidence level, finding state, artefact metadata, or user report is inferred or upgraded.
- No change is made to Markdown Bundle, Docs Site, Word, HTML, PDF, SVG, PNG, clipboard, or artefact review pack exports.
- No `v0.1.0-dev.1` release asset, tag, release, or publication record is edited, replaced, rebuilt, uploaded, deleted, or republished.

## Privacy And Safety Principles

- **Explicit action only**: the user must choose **Create support bundle** from Diagnostics or another approved troubleshooting entry point.
- **Local only**: the app creates a local copy/export artefact or copyable text. It never uploads automatically.
- **Show before export**: the app shows exactly what will be included before the user copies or saves the bundle.
- **Minimum useful metadata**: include state labels, booleans, safe versions, categories, timestamps, and bounded summaries rather than raw host data.
- **Deny by default**: new fields are excluded until they are classified and tested as safe.
- **Bounded text**: diagnostic detail strings must be short, category-like, and redacted before entering the bundle.
- **No private paths by default**: selected workspace presence can be recorded as yes/no; full paths require a separate explicit user opt-in and should still be redacted or truncated where practical.
- **Inspectable artefact**: use plain JSON and text files so the user can review the bundle outside the app.

## Threat Model

The future implementation must assume:

- A user may share the bundle publicly by mistake.
- A local folder path may reveal a private name, organisation, customer, project, or email address.
- Native errors, stack traces, and installer logs may include absolute paths, user names, machine names, registry keys, package paths, command-line arguments, or environment details.
- Imported ZIP metadata, including `lens-artifact-bundle.json`, is untrusted.
- Browser-local storage and WebView2 user data may contain document drafts, recent handles, session assets, preferences, or private file names.
- A malicious document or ZIP may try to influence diagnostics text through names or metadata.

The contract therefore permits only allowlisted fields, rejects raw logs and raw exceptions, and requires redaction tests before implementation.

## Data Classification

| Class | Examples | Bundle policy |
| --- | --- | --- |
| Safe operational metadata | App version, bundle schema version, app mode labels, boolean capability states, route decision labels, timestamp, validation command result summaries | Allowed by default after allowlist review |
| User-reviewable metadata | Manually attached validation summary, optional redacted path hint, optional issue notes typed by the user | Allowed only after preview and explicit user confirmation |
| Sensitive local data | Document contents, imported files, full paths, usernames, machine names, email addresses, screenshots, raw stack traces, browser storage, WebView2 user data, secrets, tokens, connection strings | Excluded by default and not collected by the app |
| Untrusted metadata | ZIP manifests, artefact bundle metadata, document front matter, user-authored Markdown links | Excluded unless converted to a safe category label through a future reviewed rule |

## Proposed Bundle Contents

Default safe contents may include:

- App version and product name.
- Bundle schema version.
- Bundle creation timestamp in UTC.
- Source commit or build commit if available from an existing safe build field.
- Runtime mode: browser/GitHub Pages, browser/local static server, Windows shell/development, Windows shell/packaged local assets, or unknown.
- Packaged/native mode yes/no/unknown.
- WebView2 shell detected yes/no/unknown.
- App origin category, such as packaged virtual host, local static server, GitHub Pages, file URL, or unknown. Do not include arbitrary full origin strings unless classified safe.
- Bridge message handler registered yes/no/unknown.
- Bridge ping state: pass/fail/unavailable/timeout/unknown.
- Safe host label when it exactly matches `LensDocsStudio.Windows`; otherwise unknown host.
- `workspace.openFolder` capability state: available/missing/failing/pending/unknown.
- Open folder route decision: native bridge, blocked while native bridge is present, browser directory picker, browser file input fallback, unavailable, or unknown.
- Browser fallback state: inactive in packaged WebView2, available in browser/PWA context, unavailable in this browser, or unknown.
- Last Open folder attempt state: not attempted, native picker opened, folder selected, user cancelled, native error, timeout, bridge unavailable, capability missing, browser picker opened, browser picker error, or unknown.
- Native error category, redacted and bounded to a short allowlisted reason such as timeout, unavailable, post-failed, unsupported-protocol, unsupported-message, invalid-source, invalid-timestamp, host-error, picker-cancelled, picker-failed, or unknown.
- Selected workspace presence yes/no, workspace kind, supported-file count bucket, skipped-file count bucket, and active-file state category.
- Watcher/conflict event category and timestamp where already tracked safely: changed, created, deleted, renamed, dirty conflict, refresh confirmed, refresh cancelled, or unknown.
- Watcher payload path safety result, such as relative path only yes/no/unknown. Do not include the relative path unless a future implementation explicitly decides it is safe and user-reviewable.
- Installer/package context category: ZIP package, unsigned Inno installer, development run, unknown.
- Runtime prerequisite status categories if available safely: .NET Desktop Runtime not checked/available/missing, Windows App SDK Runtime not checked/available/missing, WebView2 Runtime available/not reported/unknown.
- Validation or smoke summary manually attached by the user, limited to command names, pass/fail/blocked/skipped state, and brief bounded notes.

## Explicitly Excluded Data

The future implementation must not collect or include by default:

- Markdown, Mermaid, text, imported document, generated preview, Docs Site, Word, HTML, PDF, or artefact bundle contents.
- Full private file paths, folder paths, executable paths, install paths, temporary paths, registry paths, or command-line arguments.
- Usernames, machine names, email addresses, private customer names, private project names, or organisation names from paths or logs.
- Secrets, tokens, API keys, passwords, cookies, local storage values, browser permissions, recent handles, connection strings, certificates, SSH keys, or environment variables.
- Raw stack traces, exception dumps, event logs, installer logs, WebView2 logs, or unbounded console logs.
- Screenshots, screen recordings, clipboard contents, OCR, images, or binary attachments by default.
- WebView2 user data, cache, IndexedDB, localStorage, service-worker cache contents, or session assets.
- `lens-artifact-bundle.json` content or other imported ZIP metadata.
- Automatic telemetry, analytics, crash reporting, network upload, or support ticket submission.

## Proposed File Structure

A future generated bundle should be a ZIP with a neutral filename that avoids workspace names:

```text
lens-docs-studio-support-bundle-YYYYMMDD-HHMMSSZ.zip
```

Proposed contents:

```text
support-bundle.json
diagnostics-summary.txt
validation-summary.txt
README.txt
```

`support-bundle.json` should be the canonical machine-readable file:

```json
{
  "schemaVersion": 1,
  "createdAtUtc": "2026-06-12T09:30:00Z",
  "product": {
    "name": "Lens Docs Studio",
    "tagline": "Local Markdown, Mermaid, and documentation studio",
    "appVersion": "0.1.0-dev",
    "sourceCommit": "unknown"
  },
  "mode": {
    "runtimeMode": "windows-shell-packaged",
    "packagedNativeMode": "yes",
    "webView2ShellDetected": "yes",
    "appOriginCategory": "packaged-virtual-host"
  },
  "bridge": {
    "messageHandlerRegistered": "yes",
    "pingState": "pass",
    "host": "LensDocsStudio.Windows",
    "capabilities": {
      "diagnostics.ping": "available",
      "workspace.openFolder": "available",
      "workspace.watch": "available",
      "workspace.refreshFile": "available"
    }
  },
  "openFolder": {
    "routeDecision": "native-bridge",
    "browserFallbackState": "inactive-in-packaged-webview2",
    "lastAttemptState": "folder-selected",
    "nativeErrorCategory": "none",
    "selectedWorkspacePresent": "yes",
    "workspacePathIncluded": "no"
  },
  "watcher": {
    "lastEventCategory": "changed",
    "lastEventAtUtc": "2026-06-12T09:28:00Z",
    "relativePathOnly": "yes",
    "dirtyConflictState": "none"
  },
  "package": {
    "distributionKind": "unsigned-inno-installer",
    "webView2Runtime": "available",
    "dotnetDesktopRuntime": "not-checked",
    "windowsAppSdkRuntime": "not-checked"
  },
  "validation": {
    "manualAttachmentIncluded": "no",
    "summaries": []
  },
  "redaction": {
    "paths": "excluded-by-default",
    "documentContent": "excluded",
    "rawStackTraces": "excluded",
    "screenshots": "excluded-by-default"
  }
}
```

`diagnostics-summary.txt` should mirror the JSON in readable form with the same redacted fields. `validation-summary.txt` should be empty or contain only user-approved summaries. `README.txt` should explain that the bundle was created locally, contains no document content by design, and was not uploaded automatically.

## Redaction Rules

- Treat all strings from native errors, installer tools, file names, folder names, imported metadata, and user-authored documents as unsafe until proven otherwise.
- Prefer allowlisted enum values over sanitising arbitrary strings.
- Replace Windows paths, drive roots, UNC paths, home-relative paths, executable paths, and URL query strings with `[redacted-path]` or a category label.
- Replace email-like text with `[redacted-email]`.
- Replace token-like key/value fragments such as `token=`, `access_token=`, `api_key=`, `secret=`, `password=`, `sig=`, and `connectionString=` with `[redacted-secret]`.
- Bound every free-text field by length before writing it. Suggested limit: 160 characters per diagnostic detail and 2 KB per user-entered note.
- Never include raw exception `.stack`, `.toString()`, `Exception.ToString()`, command output, or log files. Map errors to short categories instead.
- Do not include relative workspace paths by default. If a future design approves optional relative paths, show the exact paths in the preview and allow the user to remove them.

## User Consent Model

1. The user opens Diagnostics.
2. The user chooses **Create support bundle**.
3. The app builds a preview from safe in-memory diagnostic state only.
4. The app shows every file and field that will be included.
5. Optional user-reviewable items start unchecked or require explicit confirmation.
6. The user chooses **Copy summary** or **Export local bundle**.
7. The user can cancel at any point.
8. Bundle generation errors use bounded safe copy and do not expose raw paths or stack traces.

No network upload is part of this flow. Sharing remains a separate user action outside the app.

## UX Entry Points

Future entry points may include:

- **Help > Windows shell diagnostics**: primary entry point because it already groups bridge, route, fallback, and last Open folder attempt state.
- First-run setup recovery copy: link users to Diagnostics when Open folder fails, then allow bundle creation only from Diagnostics.
- Feedback intake documentation: ask users to attach the local bundle only if they are comfortable with the previewed contents.

The UI should avoid implying production support, go-live approval, automated crash reporting, or guaranteed diagnosis.

## Diagnostics Integration

The design maps directly to current safe diagnostics fields:

- Mode: browser/PWA and WebView2 shell detected.
- Native bridge: ping result, protocol version category, safe host label, last native request type, last native response type, and last native error category.
- Capabilities: `diagnostics.ping`, `file.open`, `file.save`, `file.saveAs`, `workspace.openFolder`, `workspace.saveFile`, `workspace.createFile`, `workspace.watch`, and `workspace.refreshFile`.
- Workspace routing: Open folder route, `workspace.openFolder` capability, Open folder native-bridge decision, browser fallback state, directory picker availability, and folder input fallback availability.
- Last Open folder attempt: attempt state and bounded detail.

Future implementation should reuse these derived labels rather than adding lower-level host introspection.

## Package And Native Bridge Considerations

- The bundle should record whether the current app is running in packaged/native mode without including executable or install paths.
- WebView2 Runtime should be recorded only as available/not reported/unknown unless the host later exposes a reviewed safe version string.
- Native bridge errors should be categorised and bounded in the web app before bundle generation.
- Native smoke summaries can be manually attached by the user as pass/fail/blocked/skipped rows, not raw `smoke-result.json`.
- Smoke-only capabilities such as `smoke.nativeFixtures` must not appear in normal user bundles unless the app is explicitly launched in smoke mode and the user previews the field.

## Watcher And Conflict Troubleshooting Fields

Useful future fields:

- Selected workspace present yes/no.
- Workspace kind: browser folder, native folder, single file, imported ZIP, virtual document, or unknown.
- Last watcher event category and timestamp.
- Last dirty conflict state: none, changed on disk with app edits, deleted on disk with app content preserved, renamed on disk with review needed, or unknown.
- Last refresh decision category: not attempted, clean refresh, keep app edits, use disk version, deleted file preserved, refresh failed safely, or unknown.
- Watcher event path safety: relative-only yes/no/unknown.

Do not include full paths, file names, document titles, document content, or raw watcher payloads by default.

## Installer And Package Troubleshooting Fields

Useful future fields:

- Distribution kind: browser, portable ZIP, unsigned Inno installer, development run, unknown.
- Package/static app asset validation summary manually attached by the user.
- Installer report summary manually attached by the user, limited to pass/fail/blocked/skipped state and public artefact names.
- Runtime prerequisite state categories if available safely.
- File association state only as a category, such as not checked, guidance-only, user opted into installer association task, or unknown. Do not include registry paths or user-specific defaults.
- WebView2 user data cleanup state only as documented-retained/unknown; do not inspect or include the user data folder.

## Offline-Only Behaviour

The future support bundle must work without internet access. Creation, preview, copying, and exporting should use only local browser APIs and the existing static app dependencies. The feature must not depend on a backend, cloud endpoint, external script, CDN, environment variable, runtime build output, or production local server.

## Future Implementation Slices

1. **Design approval**: review this contract with privacy, support, and release boundaries.
2. **Data model**: add a pure support-bundle builder that accepts already-derived safe diagnostics and returns JSON/text strings.
3. **Redaction tests**: add unit-like static or browser tests for paths, emails, secrets, stack traces, long strings, and untrusted metadata.
4. **Diagnostics UI**: add **Create support bundle** to Diagnostics with a preview and cancel path.
5. **Local export/copy**: generate a local ZIP or copyable text only after explicit user action.
6. **Evidence update**: document validation results and any skipped package/browser/manual checks.

Each slice should stay small enough to review. Runtime implementation must update browser tests and static validation; package-static validation is required if packaged runtime files change.

## Validation And Evidence Expectations

For this Phase 3AN design-only gate:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Browser tests, package rebuilds, installer rebuilds, release preparation, release publication, and manual packaged smoke are intentionally skipped for this gate because no runtime code changes.

For a later implementation:

- Static tests must pass.
- Browser tests must cover the Diagnostics entry point, preview, cancel path, copy/export action, bounded error copy, and no automatic upload.
- Redaction tests must cover document content exclusion, private path exclusion, secrets, tokens, emails, raw stack traces, screenshots by default, and untrusted ZIP metadata.
- Package/static validation must run when runtime code changes affect packaged assets.
- Documentation/evidence must state whether package artefacts need rebuilding before any future publication.

## Release Boundaries

This design creates no release artefacts and changes no release assets.

A future implementation does not approve `v0.1.0-dev.2` publication by itself. Any release still needs a separately approved target commit, validation gate, artefact rebuild plan, checksums, release notes, and publication decision.

`v0.1.0-dev.1` release assets, tags, releases, and publication evidence remain unchanged by this phase. Production readiness, go-live approval, stable-channel certification, and promotion to `main` remain unclaimed.

## Future Implementation Prompt

Use this prompt only after the support bundle design is explicitly approved for implementation:

```text
You are working in C:\Code\MarkdownReader on branch develop.

Objective:
Implement the approved optional troubleshooting/support bundle feature from docs/architecture/lens-docs-studio-support-bundle-design.md.

Context:
- Phase 3AN approved the design contract only; generation was not implemented then.
- The feature must help users report bridge, folder picker, watcher/conflict, package, and installer issues without exposing private document content, secrets, tokens, raw stack traces, screenshots, or unnecessary local data.
- This implementation is not a release approval.

Constraints:
- Work directly on develop.
- Require explicit user action before generating a bundle.
- Do not automatically upload, submit telemetry, create tickets, call external services, or perform network submission.
- Do not include document contents, rendered previews, imported ZIP contents, session images, full private paths, secrets, tokens, connection strings, raw stack traces, email addresses, usernames, machine names, WebView2 user data, localStorage dumps, IndexedDB, service-worker cache contents, screenshots by default, or unbounded logs.
- Keep generated support bundles out of git.
- Preserve GitHub Pages/static hosting compatibility and the buildless app architecture.
- Keep product wording generic: Local Markdown, Mermaid, and documentation studio.
- Use British English for first-party UI, docs, comments, tests, logs, generated copy, examples, templates, and snippets.
- Do not edit, delete, replace, re-upload, rebuild, or republish v0.1.0-dev.1 release assets.
- Do not create tags or releases.
- Do not merge to main.
- Do not claim production readiness, go-live approval, stable-channel certification, or stable/latest positioning.

Tasks:
1. Confirm branch, baseline history, tracked status, and ignored/generated paths before editing.
2. Inspect current Diagnostics, native bridge, folder-picker, watcher/conflict, package/static validation, and installer documentation surfaces before editing.
3. Implement a pure allowlisted support bundle builder that accepts already-derived safe diagnostics and produces bounded JSON/text content.
4. Add redaction tests for private paths, usernames from paths, email addresses, secrets, tokens, connection strings, raw stack traces, long strings, screenshots-by-default exclusion, document-content exclusion, and untrusted ZIP metadata exclusion.
5. Add a Diagnostics UI flow:
   - user opens Diagnostics;
   - user chooses Create support bundle;
   - app shows exactly what will be included;
   - user can cancel;
   - user can copy or export a local bundle;
   - generated filename avoids private workspace names;
   - bundle generation errors use bounded safe copy.
6. Keep automatic upload out of scope and test that no upload/network submission control is introduced.
7. Update docs and evidence notes for the implemented feature, including package-affecting artefact expectations if runtime code changes.
8. Update service-worker.js if new cacheable static assets or modules are added.
9. Run validation:
   - npm run test:static
   - npm run test:browser
   - dotnet build src/windows/LensDocsStudio.Windows.sln
   - pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
10. Run package/static or native smoke validation if runtime changes affect packaged behaviour, and record any skipped validation with the reason.
11. Stage only intentional source/docs/test changes, commit, and push to origin/develop.

Suggested commit message:
add diagnostics support bundle export

Final response must include:
- Branch
- Commit hash
- Push result
- Runtime code changed or docs/tests only
- Files changed
- Support bundle UX and safety behaviours implemented
- Redaction and browser test coverage
- Static, browser, Windows build, and packaged static validation results
- Skipped validation and reason
- Confirmation that no release assets, tags, releases, or main merges were changed
- Confirmation that production readiness/go-live was not claimed
```
