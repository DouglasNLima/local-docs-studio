# Lens Docs Studio Prerelease Guidance

Guidance date: 2026-06-13

## Current Recommended Prerelease

The current published prerelease/dev release for new validation is `v0.1.0-dev.4`.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.4`

Verified published checksums:

| Artefact | SHA256 |
| --- | --- |
| ZIP package | `06BFA3896E1CCA48F1DC87D2F49D2387A8BCB93078E9F070764F22B048DD860C` |
| Unsigned Inno Setup installer | `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |

`v0.1.0-dev.4` is a prerelease/dev release. It is not a production release, stable-channel certification, go-live approval, or a promotion to `main`.

`v0.1.0-dev.4` post-publication verification passed in Phase 3BL for the release tag, prerelease state, expected six-asset set, downloaded ZIP SHA256, downloaded installer SHA256, downloaded ZIP smoke, payload hygiene, and contained installer smoke.

Manual packaged sanity for Phase 3BL remained `SKIPPED_INTERACTIVE_NATIVE_UI_CONTROL_NOT_EXECUTED`. Phase 3AW remains the current referenced manual packaged sanity/remediation evidence for interactive native UI behaviours, including onboarding, diagnostics, visible native picker selection, cancellation, inactive browser fallback, and watcher changed-on-disk copy after the Open folder pending guidance remediation.

True upgrade from `v0.1.0-dev.3` remains blocked by the already-published baseline installer rollback recorded in Phase 3BH. No further release asset action is required unless a future release is authorised. `v0.1.0-dev.1`, `v0.1.0-dev.2`, and `v0.1.0-dev.3` assets were not changed by the `v0.1.0-dev.4` publication, verification, or closure evidence.

## Stale Older Prerelease

The older `v0.1.0-dev.3`, `v0.1.0-dev.2`, and `v0.1.0-dev.1` prerelease assets are superseded for new validation but preserved historically. The older `v0.1.0-dev` prerelease assets are older/stale relative to `v0.1.0-dev.4`.

In this context, stale means:

- A newer prerelease/dev release exists.
- New installation, validation, support evidence, and download guidance should use the published `v0.1.0-dev.4` release unless a later prerelease supersedes it.
- The older assets may still exist on GitHub for historical traceability.
- Stale does not mean the older release assets were edited, deleted, replaced, re-uploaded, rebuilt, or republished.
- Stale does not mean the older release is production-ready, unsafe by definition, or approved for any new support baseline.

Older `v0.1.0-dev.3`, `v0.1.0-dev.2`, `v0.1.0-dev.1`, and `v0.1.0-dev` assets may be referenced historically when reviewing earlier publication records, comparing prerelease behaviour, investigating a report that explicitly came from an older release, or explaining why later evidence moved to `v0.1.0-dev.4`.

Older `v0.1.0-dev.3`, `v0.1.0-dev.2`, `v0.1.0-dev.1`, and `v0.1.0-dev` assets should not be used for new installation, validation, support evidence, release-candidate certification, or user guidance unless the work is explicitly investigating historical behaviour from those prereleases.

## Post-v0.1.0-dev.4 Next Work

- Collect prerelease feedback against `v0.1.0-dev.4`.
- Monitor support bundle and Diagnostics copy/export feedback.
- Monitor payload hygiene, installer, download, checksum, and runtime-prerequisite issues.
- Decide whether to address upgrade path limitations in a future release.
- Decide the next dev release scope from observed feedback and release-monitoring signals.
- Keep production readiness and go-live approval as a separate approval gate.
- Consider manual packaged sanity helper implementation only after explicit approval.
- Use `docs/roadmap/lens-docs-studio-post-v010-dev3-backlog.md` for the post-release backlog.
- Use `docs/release/lens-docs-studio-v010-dev4-release-closure.md` for the `v0.1.0-dev.4` closure and roadmap rebaseline.

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
