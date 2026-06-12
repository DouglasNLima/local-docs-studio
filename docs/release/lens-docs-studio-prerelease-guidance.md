# Lens Docs Studio Prerelease Guidance

Guidance date: 2026-06-12

## Current Recommended Prerelease

The current recommended prerelease/dev release for new validation is `v0.1.0-dev.2`.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.2`

Verified published checksums:

| Artefact | SHA256 |
| --- | --- |
| ZIP package | `C7C9E52322EDA140D9AB60F9D8D2BF257EED9EA898F50FC8EFA2D90A04A9BF0B` |
| Unsigned Inno Setup installer | `01C60DFAA57754EFFCCC763051D5EEDE0DB4E3284936C8BF7D1189D30DCA6C21` |

`v0.1.0-dev.2` is a prerelease/dev release. It is not a production release, stable-channel certification, go-live approval, or a promotion to `main`.

Manual packaged sanity failed for `v0.1.0-dev.2` in Phase 3AV. First-run/onboarding and diagnostics were observed in the real packaged app, and diagnostics reported WebView2/native bridge routing as healthy. However, **Open folder** stayed pending at `Opening folder from Windows...`; no usable native folder picker appeared and no selected/cancelled result returned to the app. Watcher/conflict manual evidence remains blocked by that native picker failure.

Phase 3AT post-publication verification passed for the release tag, prerelease state, expected asset set, downloaded ZIP SHA256, downloaded installer SHA256, downloaded ZIP smoke, and contained installer smoke. Phase 3AV did not change release assets, tags, releases, packages, installers, or `main`. No further release asset action is required unless a future release is authorised, but new validation must treat the packaged native folder picker as a known failing manual sanity check for `v0.1.0-dev.2`.

## Stale Older Prerelease

The older `v0.1.0-dev.1` prerelease assets are superseded for new validation but preserved historically. The older `v0.1.0-dev` prerelease assets are older/stale relative to `v0.1.0-dev.2`.

In this context, stale means:

- A newer prerelease/dev release exists.
- New installation, validation, support evidence, and download guidance should use `v0.1.0-dev.2` unless a later prerelease supersedes it.
- The older assets may still exist on GitHub for historical traceability.
- Stale does not mean the older release assets were edited, deleted, replaced, re-uploaded, rebuilt, or republished.
- Stale does not mean the older release is production-ready, unsafe by definition, or approved for any new support baseline.

Older `v0.1.0-dev.1` and `v0.1.0-dev` assets may be referenced historically when reviewing earlier publication records, comparing prerelease behaviour, investigating a report that explicitly came from an older release, or explaining why later evidence moved to `v0.1.0-dev.2`.

Older `v0.1.0-dev.1` and `v0.1.0-dev` assets should not be used for new installation, validation, support evidence, release-candidate certification, or user guidance unless the work is explicitly investigating historical behaviour from those prereleases.

## Evidence Boundaries

This guidance changes documentation only.

It does not:

- Edit, delete, replace, re-upload, rebuild, or republish any release asset.
- Create, move, or delete tags.
- Create, edit, publish, unpublish, or delete GitHub Releases.
- Merge `develop` to `main`.
- Claim production readiness, go-live approval, stable-channel certification, or stable/latest positioning.

## Future Prerelease Maintenance

When a new prerelease is explicitly authorised and published:

1. Add or update this guidance with the new current recommended prerelease, release URL, published asset set, and verified checksums.
2. Mark the previous recommended prerelease as superseded in documentation, using precise wording that preserves its historical evidence.
3. Keep previous publication, verification, closure, and smoke evidence intact unless a separately authorised release-maintenance decision says otherwise.
4. Do not edit, delete, replace, re-upload, rebuild, or republish old assets as part of routine supersedence documentation.
5. State clearly whether the new prerelease is still prerelease/dev only, and avoid production readiness, go-live, stable/latest, or `main` promotion claims unless those gates are separately approved.
