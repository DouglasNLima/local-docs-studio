# Lens Docs Studio v0.1.0-dev Feedback Intake

Use this intake when collecting public prerelease feedback for `v0.1.0-dev`.

The release is public, but it is still a prerelease. It is not stable/latest, the installer is unsigned, and runtime prerequisites must be installed separately.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev`

## How To Report Feedback

Capture:

- Windows version
- Install method: ZIP portable package or Inno installer
- Runtime prerequisites installed
- Exact asset used
- SHA256 verified or not
- Steps to reproduce
- Expected behaviour
- Actual behaviour
- Screenshots/logs if available
- Whether the issue blocks prerelease adoption

## Feedback Categories

- Installation
- Runtime prerequisites
- Launch
- Native bridge
- File open/save
- Workspace open/save
- Watcher/conflict UX
- First-run setup wizard
- Help guide
- File associations
- Uninstall
- Documentation/release notes
- Other

## Severity

- Blocker
- High
- Medium
- Low
- Question/Feedback

## Decision Buckets

- Must fix before next prerelease
- Should fix before stable
- Documentation only
- Accepted caveat
- Won't fix for this cycle

## Current Known Issues / Caveats

- Unsigned installer.
- Runtime prerequisites are separate.
- WebView2 user data may remain after uninstall under `%LOCALAPPDATA%\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe.WebView2`.
- ZIP and installer are prerelease assets only.
- ZIP is the portable/fallback package for manual extraction and smoke validation. It does not create Start Menu or uninstall entries.
- The Inno installer is the easier Windows install path with a Start Menu shortcut, optional Desktop shortcut, optional/default-safe file associations, and uninstall support.
- File associations are optional/default-safe and should be treated as opt-in.

## Suggested Issue Body

```markdown
## Summary

## Environment

- Windows version:
- Install method: ZIP / installer:
- Exact asset:
- SHA256 verified: yes / no
- .NET 8 Desktop Runtime installed: yes / no / unsure
- Windows App SDK Runtime installed: yes / no / unsure
- Evergreen WebView2 Runtime installed: yes / no / unsure

## Category

Installation / Runtime prerequisites / Launch / Native bridge / File open-save / Workspace open-save / Watcher-conflict UX / First-run setup wizard / Help guide / File associations / Uninstall / Documentation-release notes / Other

## Severity

Blocker / High / Medium / Low / Question-Feedback

## Steps To Reproduce

1.
2.
3.

## Expected Behaviour

## Actual Behaviour

## Attachments

Screenshots, logs, or checksum output if available.

## Adoption Impact

Does this block prerelease adoption? yes / no / unsure
```

## Triage Notes

- Keep prerequisite failures separate from installer defects unless the installer performs an unexpected action.
- Treat checksum mismatches as blocker until the asset, local file, and command used for verification are understood.
- Do not infer stability from successful prerelease installs.
- Do not convert feedback about candidate findings, external documents, or imported artefact metadata into confirmed findings without user review.

## Next-Cycle Planning

Phase 3R triage and `v0.1.0-dev.1` scope planning are recorded in `docs/release/lens-docs-studio-v0.1.0-dev.1-planning.md`.
