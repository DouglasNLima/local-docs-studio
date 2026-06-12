# Lens Docs Studio Manual Packaged Sanity Helper Plan

Planning date: 2026-06-12

## Status

Phase 3BF defines a design/planning-only helper approach for repeatable human verification of packaged onboarding, Diagnostics, native picker, support bundle, and watcher/conflict UX.

This plan does not implement runtime code, helper scripts, package rebuilds, installer rebuilds, release asset changes, tags, GitHub Releases, a merge to `main`, production readiness, or go-live approval. Existing prerelease assets remain untouched.

## Purpose

Provide a safe, repeatable checklist and future helper-script shape for real human observation of the packaged Windows app. The process should make evidence easier to collect while preserving the difference between:

- automated package/native smoke that proves the bridge and packaged static asset contracts; and
- manual packaged sanity that observes the actual app window, native Windows picker, support bundle flow, and external file change/conflict decisions.

## Non-Goals

- Do not automate, fake, bypass, or inject native Open folder selection.
- Do not add runtime product shortcuts, debug-only workspace injection, localStorage setup bypasses, command-line workspace bypasses, telemetry, upload, account, ticket, or support submission flows.
- Do not collect document contents, imported ZIP contents, screenshots by default, browser storage, WebView2 user data, raw logs, raw stack traces, secrets, tokens, connection strings, emails, usernames, machine names, or full private paths.
- Do not rebuild, edit, replace, re-upload, republish, delete, or retag release assets.
- Do not claim that manual packaged sanity has passed unless a human actually performs and records the checks.
- Do not claim production readiness, go-live approval, stable/latest positioning, signing, prerequisite bootstrapping, auto-update, Store publication, winget publication, or a `main` promotion.

## Target Scenarios

The manual packaged sanity helper should cover these scenarios:

| Scenario | Human-observed checks | Expected classification evidence |
| --- | --- | --- |
| First-run/onboarding copy | App launches from packaged ZIP or installed shortcut; first-run setup appears with generic local documentation wording; Open folder is the primary workspace action; Open file remains available; files-stay-local copy is visible; setup can be skipped and reopened. | `PASS` when all observed; `FAIL` when visible wording or setup flow is wrong; `BLOCKED` when packaged app cannot launch. |
| Diagnostics visibility and Retry bridge check | **Help > Windows shell diagnostics** opens; **Retry bridge check** updates diagnostics; bridge ping, WebView2 shell, capability labels, native route, and browser fallback state are visible without private values. | `PASS` when diagnostics are readable and safe; `FAIL` when diagnostics are missing, stale, or expose unsafe detail; `BLOCKED` when Diagnostics cannot be opened. |
| Native Open folder picker visible/select/cancel | **Open folder** uses the native bridge in packaged WebView2; a visible Windows `Select Folder` picker appears; selecting the temporary workspace loads safe relative workspace entries; a second attempt can be cancelled and returns a clean cancelled state. | `PASS` when both select and cancel are observed; `FAIL` when picker is hidden, non-returning, browser fallback appears, or selection/cancellation state is wrong; `BLOCKED` when the app/window cannot be operated. |
| Support bundle preview/create/copy/export | Diagnostics shows **Create support bundle**; copy/export remain disabled until a preview exists; the user explicitly creates a local preview; **Copy summary** and **Export local JSON** become available; exported filename is neutral. | `PASS` when explicit preview gating and local copy/export work; `FAIL` when copy/export is enabled early, upload/telemetry is suggested, or export fails; `BLOCKED` when Diagnostics cannot be operated. |
| Support bundle privacy boundaries | Preview/export includes only allowlisted operational metadata and privacy/redaction declarations; it excludes document contents, imported ZIP contents, full private paths by default, secrets, tokens, connection strings, emails, private names/PII, raw stack traces, screenshots, browser storage, and WebView2 user data. | `PASS` when excluded data is absent from preview/export; `FAIL` when unsafe data appears; `BLOCKED` when preview/export cannot be created. |
| Clean external file change | After opening a temporary workspace, externally edit the active file while the app has no unsaved edits; app shows changed-on-disk status; **Refresh active file** reloads the disk version only after explicit action. | `PASS` when clean external change and refresh are observed; `FAIL` when content changes silently or status is missing; `BLOCKED` when a workspace cannot be opened. |
| Dirty conflict keep app edits | Externally edit the active file, then make unsaved edits in the app before refreshing; conflict status appears; cancelling refresh preserves unsaved app edits and keeps the conflict visible. | `PASS` when app edits are preserved; `FAIL` when edits are discarded or conflict copy is unclear; `BLOCKED` when watcher/conflict state cannot be reached. |
| Dirty conflict use disk version | In the same dirty conflict state, confirm refresh and observe the disk version load only after confirmation; conflict marker clears appropriately. | `PASS` when disk version is loaded by explicit confirmation; `FAIL` when the wrong version remains or data is silently discarded; `BLOCKED` when confirmation cannot be completed. |
| Installer launch/install/uninstall, when relevant | For an authorised installer candidate only: install per-user, launch from Start Menu, optionally decline Desktop shortcut and file associations, run the packaged sanity subset, uninstall, and confirm installer-owned files/shortcuts are removed while WebView2 user data caveat is recorded. | `PASS` when install/launch/uninstall behaves as expected; `FAIL` when installer-owned state is wrong; `BLOCKED` when installer prerequisites or permissions prevent the check. |

## Required Environment

- Windows 10 version 2004 / build 19041 or newer, or Windows 11.
- .NET Desktop Runtime matching the Windows target framework.
- Windows App SDK Runtime matching the project package reference.
- Evergreen Microsoft Edge WebView2 Runtime.
- A packaged ZIP or installer candidate that was separately authorised for manual sanity.
- A clean temporary workspace with only synthetic Markdown/Mermaid/text fixtures.
- A text editor outside Lens Docs Studio for external file edits.
- Optional local hash/report references from automated package/native smoke, if already produced by approved validation.

## Temporary Workspace Convention

Use a temporary folder whose name is neutral and non-private. Preferred pattern:

```text
%TEMP%\LensDocsStudio-ManualSanity-YYYYMMDD-HHMMSS
```

Create only synthetic files, for example:

```text
README.md
docs\overview.md
diagrams\sample.mmd
notes\conflict.md
```

Fixtures should contain generic text such as:

```markdown
# Manual Sanity Fixture

Synthetic content for packaged verification.
```

Do not use customer files, personal notes, real project documents, private folder names, screenshots, downloaded release assets outside ignored artefact folders, or source-controlled fixture output generated by the helper.

## Exact Human-Observed Checks

The helper checklist should ask the operator to record the following observations:

1. Package or installer identity: artefact name, source commit when known, SHA256 if already available, and whether this is ZIP or installer evidence.
2. Launch mode: extracted ZIP executable, installed shortcut, or installed executable.
3. First-run state: setup appeared, safe onboarding copy was visible, setup skip/reopen worked.
4. Diagnostics: panel opened, retry completed, bridge ping/capabilities/route/fallback labels were visible and safe.
5. Native picker selection: visible Windows folder picker appeared and the selected temporary workspace loaded with relative paths.
6. Native picker cancellation: second picker attempt returned a clean cancelled state.
7. Support bundle preview: preview required explicit user action; copy/export were disabled before preview and enabled after preview.
8. Support bundle privacy: preview/export did not include document contents, imported ZIP contents, private paths, secrets, raw stacks, screenshots, browser storage, or WebView2 user data.
9. Clean external change: changed-on-disk status appeared and refresh loaded the disk version after explicit action.
10. Dirty conflict keep app edits: cancelling refresh preserved unsaved app edits.
11. Dirty conflict use disk version: confirming refresh loaded the disk version.
12. Installer-only checks, if relevant: install, Start Menu launch, optional shortcut/file-association choices, uninstall, and cleanup observations.
13. Cleanup: temporary workspace and extracted/installed test artefacts were removed or intentionally retained under an ignored evidence folder with a reason.

## Evidence Fields To Capture

Use concise evidence fields rather than raw logs:

| Field | Guidance |
| --- | --- |
| `phase` | Example: `Phase 3BF dry-run` or future approved phase name. |
| `operator` | Initials or role only; avoid full personal names. |
| `dateUtc` | ISO-like UTC date/time for the evidence note. |
| `windowsVersion` | High-level OS version/build only. |
| `runtimePrerequisites` | Present/missing/unknown for .NET Desktop Runtime, Windows App SDK Runtime, and WebView2 Runtime. |
| `appArtefact` | ZIP or installer file name only, plus SHA256 if already approved for capture. |
| `sourceCommit` | Commit SHA if known from the candidate report. |
| `launchMode` | ZIP executable, installed shortcut, or installed executable. |
| `temporaryWorkspace` | `TEMP_WORKSPACE_USED` or a redacted basename only; do not record full private paths. |
| `scenarioResults` | One `PASS`, `FAIL`, or `BLOCKED` result per scenario. |
| `failureCategory` | Bounded labels such as `APP_DID_NOT_LAUNCH`, `NATIVE_PICKER_NOT_VISIBLE`, `SUPPORT_BUNDLE_PRIVACY_LEAK`, or `WATCHER_CONFLICT_UNEXPECTED_RESULT`. |
| `notes` | Short, bounded operator notes with no document content, private paths, secrets, emails, raw stacks, or screenshots by default. |
| `cleanupResult` | Temporary workspace removed, extracted ZIP retained under ignored evidence path, installer uninstalled, or blocked with reason. |

## PASS/FAIL/BLOCKED Classifications

Use `PASS` only when the human observer completed the scenario and saw the expected behaviour.

Use `FAIL` when the scenario was reachable but product behaviour was wrong, unsafe, misleading, or privacy-breaking.

Use `BLOCKED` when the scenario could not be reached because of environment, prerequisite, launch, permission, tester-control, or prior-scenario failure. Do not convert a blocked scenario into a pass based on automated smoke, previous evidence, or expected behaviour.

Overall classification should be:

- `PACKAGED_MANUAL_SANITY_PASS` only when all required scenarios for the candidate pass.
- `PACKAGED_MANUAL_SANITY_FAIL` when any required scenario fails.
- `PACKAGED_MANUAL_SANITY_BLOCKED` when no required scenario fails but one or more required scenarios are blocked.
- `INSTALLER_MANUAL_SANITY_PASS`, `INSTALLER_MANUAL_SANITY_FAIL`, or `INSTALLER_MANUAL_SANITY_BLOCKED` when installer checks are in scope.

## Safety And Privacy Rules

- Use temporary synthetic documents only.
- Record relative paths or redacted basenames only.
- Do not paste document contents, screenshots, browser storage, WebView2 profile data, raw logs, raw stack traces, emails, usernames, machine names, secrets, tokens, API keys, passwords, certificates, cookies, connection strings, or customer/project names into evidence.
- Treat support bundle JSON as local evidence only. Do not upload or attach it unless a separate review confirms it is safe and the user explicitly chooses to share it.
- Keep generated helper evidence outside source unless an approved documentation summary is intentionally committed.
- Do not mutate release assets, tags, releases, or `main`.

## Cleanup Expectations

After the check:

- Close Lens Docs Studio.
- Delete the temporary workspace unless a blocked investigation requires short-term local retention.
- Remove extracted ZIP folders created only for the check, or keep them under ignored `artifacts/` with a clear local reason.
- If installer checks were in scope, uninstall the app and remove installer-owned shortcuts; record the WebView2 user data caveat rather than deleting browser-local state silently.
- Do not commit generated support bundles, downloaded packages, installer outputs, screenshots, native smoke roots, WebView2 user data, or local logs.

## Relationship To Automated Package/Native Smoke

Automated validation remains responsible for repeatable machine checks:

- `scripts/windows/Test-WindowsStaticAssets.ps1` verifies packaged `StaticApp/` completeness and offline static asset expectations.
- `scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` verifies stable native bridge operations against controlled smoke fixtures and does not automate the real Windows picker UI.
- `scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` builds/certifies a package candidate, validates static assets, runs packaged native bridge smoke, and records ZIP checksum/report metadata.
- `scripts/windows/Build-WindowsPackage.ps1` and `scripts/windows/Build-WindowsInnoInstaller.ps1` create local package/installer artefacts only when separately authorised.

Manual packaged sanity complements those checks. It verifies what automation deliberately does not claim: visible onboarding, actual native picker operation, human cancellation/selection, support bundle preview/privacy review, and real watcher/conflict decisions in the packaged UI.

## Future Implementation Options

A future approved helper could be one of:

- a Markdown checklist generator that writes a local evidence template under an ignored path;
- a PowerShell prompt that creates a temporary synthetic workspace and prints step-by-step manual instructions;
- a documentation-only checklist copied into release evidence notes;
- a script that collects operator-entered `PASS`/`FAIL`/`BLOCKED` classifications and bounded notes without reading workspace contents;
- a thin wrapper that references existing automated package/native smoke reports without rerunning package or installer builds.

Any helper script must:

- create temporary workspaces only;
- avoid reading or collecting document contents;
- avoid recording full private paths by default;
- avoid bypassing or controlling the native picker;
- avoid changing runtime product behaviour;
- avoid release asset edits/uploads;
- write local evidence templates under ignored output paths unless a human later writes a documentation summary;
- include docs and targeted validation if scripts are added.

## Future Implementation Prompt

Use this prompt only after manual packaged sanity helper implementation is explicitly approved:

```text
You are working in C:\Code\MarkdownReader on branch develop.

Objective:
Implement an approved manual packaged sanity helper/checklist generator for Lens Docs Studio packaged Windows verification.

Context:
- docs/testing/lens-docs-studio-manual-packaged-sanity-helper-plan.md defines the Phase 3BF design/planning-only helper approach.
- The helper supports human verification of packaged onboarding, Diagnostics, native Open folder picker selection/cancellation, support bundle preview/copy/export, support bundle privacy boundaries, watcher/conflict UX, and installer install/launch/uninstall checks when relevant.
- This is helper/checklist tooling only. It is not runtime product implementation and not release approval.

Constraints:
- Work directly on develop.
- Do not add runtime product shortcuts, debug-only workspace injection, localStorage setup bypasses, command-line workspace bypasses, or native picker bypasses.
- Do not fake, automate, or bypass the real Windows folder picker. The helper may instruct the human to use it, but must not select folders for the app or claim picker observation automatically.
- Do not collect document contents, imported ZIP contents, screenshots by default, browser storage, WebView2 user data, raw logs, raw stack traces, secrets, tokens, connection strings, emails, usernames, machine names, private names, raw PII, or full private paths.
- Create only temporary synthetic workspaces for checklist use.
- Output a local evidence template with PASS/FAIL/BLOCKED fields and bounded operator notes.
- Keep generated evidence, support bundles, package downloads, installer outputs, and temporary workspaces out of git.
- Do not edit, delete, replace, re-upload, rebuild, or republish existing release assets.
- Do not create tags or releases.
- Do not merge to main.
- Do not claim production readiness, go-live approval, stable-channel certification, stable/latest positioning, or public production rollout.
- Use British English for first-party docs, comments, logs, generated copy, examples, templates, and snippets.

Tasks:
1. Confirm branch, baseline history, tracked status, and ignored/generated paths before editing.
2. Inspect this helper plan, existing package/native smoke scripts, package RC checklist, installer evidence plan, support bundle implementation evidence, and dev.4 roadmap docs.
3. Implement the smallest approved helper shape:
   - create a temporary synthetic workspace only;
   - generate or print manual checklist steps;
   - output a local evidence template with scenario rows and PASS/FAIL/BLOCKED fields;
   - avoid reading workspace document contents;
   - avoid storing full private paths by default;
   - avoid native picker bypass or UI automation claims.
4. Add or update documentation for running the helper.
5. Add tests or static validation coverage if a script is added.
6. Run validation appropriate to the blast radius, at minimum:
   - npm run test:static
   - dotnet build src/windows/LensDocsStudio.Windows.sln
   - pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
7. Do not run browser tests, package rebuilds, installer rebuilds, release commands, or manual smoke unless the approved scope explicitly requires them.
8. Stage only intentional docs/script/test changes, commit, and push to origin/develop.

Suggested commit message:
docs: add manual packaged sanity helper

Final response must include:
- Branch
- Commit hash
- Push result
- Whether runtime code changed or helper/docs only
- Helper/checklist output path or command
- Docs/tests updated
- Validation results
- Skipped validation and reason
- Confirmation that no release assets, tags, releases, or main merges were changed
- Confirmation that existing prerelease assets were not changed
- Confirmation that production readiness/go-live was not claimed
```

## Phase 3BF Validation Boundary

For Phase 3BF itself, run:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Browser tests, package rebuilds, installer rebuilds, release commands, manual smoke, tag operations, uploads, and `main` merges are intentionally out of scope for this planning-only slice.
