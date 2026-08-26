# Phase 3BP Manual Packaged Sanity Helper

Date: 2026-06-13

## Summary

Phase 3BP implements the first approved `v0.1.0-dev.5` implementation slice: a Windows/manual evidence helper that creates safe generated fixtures and a local Markdown checklist for real human packaged sanity verification.

This is helper, test, and documentation work only. Runtime app code, package output, installer output, release assets, tags, GitHub Releases, and `main` were not changed. Production readiness, go-live approval, stable-channel certification, stable/latest positioning, and public rollout are not claimed.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Required Phase 3BO commit | PASS, `66699f9386ad09966cfbdce0544aead288c99b6f` is an ancestor of `HEAD` |
| Tracked status before edits | Clean, `## develop...origin/develop` |
| Ignored/generated paths before edits | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/`; Windows long-path warnings can appear while scanning ignored WebView2 cache output |

## Implemented Helper

Helper script:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/New-WindowsManualPackagedSanityChecklist.ps1
```

By default the helper:

- creates a neutral generated workspace under the local temp directory using the `LensDocsStudio-ManualSanity-YYYYMMDD-HHMMSS` naming pattern;
- writes safe synthetic fixtures only: `README.md`, `docs/overview.md`, `diagrams/sample.mmd`, and `notes/conflict.md`;
- writes a local Markdown checklist under ignored `artifacts/windows/manual-packaged-sanity/`;
- records package/version, executable, source commit, hash, environment, launch mode, runtime prerequisite, temporary workspace, scenario result, failure category, cleanup, and evidence-boundary fields;
- instructs the operator to record `TEMP_WORKSPACE_USED` or the neutral workspace basename rather than full private paths;
- leaves all scenario results blank for the human operator to mark as `PASS`, `FAIL`, or `BLOCKED`.

Optional executable launch is available only through explicit `-LaunchExecutable -ExecutablePath <path-to-exe>`. It launches the executable and does not automate onboarding, Diagnostics, native picker selection, cancellation, watcher decisions, or result marking.

## Checklist Coverage

The generated checklist covers:

- onboarding renders;
- Diagnostics opens;
- Retry bridge check passes;
- Create support bundle preview-first flow;
- Copy summary after preview;
- Export local JSON after preview;
- Open folder native picker visible;
- native picker select temporary workspace;
- native picker cancel;
- browser fallback inactive in packaged WebView2;
- clean external file change / changed-on-disk copy;
- dirty conflict keep app edits;
- dirty conflict use disk version;
- installer install/uninstall checks, where applicable;
- payload hygiene note for package/installer evidence;
- production readiness/go-live not claimed.

## Privacy And Safety Guardrails

The checklist explicitly tells the operator to:

- use only the generated temporary workspace;
- avoid private, customer, project, or personal documents;
- avoid pasting document contents into evidence;
- avoid screenshots by default;
- avoid tokens, secrets, API keys, passwords, certificates, cookies, connection strings, full private paths, usernames, emails, machine names, customer names, raw stack traces, browser storage, WebView2 user data, package payload listings, and PII;
- record only safe operational observations;
- mark unclear, unreachable, environment-limited, or partially observed scenarios as `BLOCKED`, not `PASS`;
- treat support bundle JSON as local evidence until separately reviewed;
- avoid changing release assets, tags, releases, or `main`.

## What The Helper Does Not Do

The helper does not:

- bypass, fake, or automate native folder picker decisions;
- inject workspaces through localStorage, command-line flags, hidden bridge calls, or test-only UI routes;
- collect document contents, private paths, secrets, raw stacks, screenshots, browser storage, WebView2 user data, support bundle uploads, or telemetry;
- mark manual sanity as passed;
- run package rebuilds, installer rebuilds, release publication, tag operations, or GitHub Release operations;
- claim production readiness, go-live approval, stable/latest positioning, or `main` promotion.

## Tests Added Or Updated

- `scripts/windows/Test-WindowsManualPackagedSanityChecklist.ps1` parses the helper, generates a checklist into a requested output directory, verifies safe fixture creation, required scenarios, required fields, privacy/safety guardrails, neutral temporary workspace naming, absence of unsafe bypass wording, absence of private document-content requests, absence of production/go-live approval claims, and absence of full temporary workspace paths in the checklist.
- `scripts/windows/Test-WindowsStaticAssets.ps1` now parses the helper and its self-test, then runs the helper self-test as part of Windows static validation.

## Validation

| Command | Result |
| --- | --- |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsManualPackagedSanityChecklist.ps1` | PASS, generated a checklist in a requested output directory and verified scenario, guardrail, field, naming, and evidence-boundary assertions |
| `npm run test:static` | PASS, static checks passed for 47 module files, 54 shell assets, 150 vendor assets, and 53 runtime external-dependency scans |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS, build succeeded with 6 existing PRI qualifier warnings and 0 errors |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS, parsed the helper scripts, ran the helper self-test, built the Debug Windows shell, and verified 54 service-worker assets plus 150 vendor assets in packaged `StaticApp/` |

`npm run test:browser` was not required because Phase 3BP did not change frontend runtime, layout, UI behaviour, rendering, import/export, clipboard, theme, or browser workflow files.

Package rebuilds, native smoke, installer rebuilds, release commands, tag commands, release asset operations, GitHub Release operations, and manual packaged smoke were not run because this slice changes only helper/checklist tooling, targeted tests, and documentation.

## Future Publication Boundary

Future `v0.1.0-dev.5` package or installer artefacts need to be built or rebuilt from the eventual authorised source commit before any publication decision, because Phase 3BP changes repository helper/test/documentation source after the already published `v0.1.0-dev.4` assets. Phase 3BP itself does not create, rebuild, publish, or validate release artefacts.

Existing prerelease assets, including `v0.1.0-dev.4` and older prereleases, were not changed.
