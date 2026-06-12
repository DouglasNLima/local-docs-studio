# Lens Docs Studio Prerelease Guidance

Guidance date: 2026-06-12

## Current Recommended Prerelease

The current recommended prerelease/dev release for new validation is `v0.1.0-dev.1`.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.1`

Verified published checksums:

| Artefact | SHA256 |
| --- | --- |
| ZIP package | `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A` |
| Unsigned Inno Setup installer | `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745` |

`v0.1.0-dev.1` is a verified prerelease/dev release. It is not a production release, stable-channel certification, go-live approval, or a promotion to `main`.

## Stale Older Prerelease

The older `v0.1.0-dev` prerelease assets are stale/superseded relative to `v0.1.0-dev.1`.

In this context, stale means:

- A newer verified prerelease/dev release exists.
- New installation, validation, support evidence, and download guidance should use `v0.1.0-dev.1` unless a later prerelease supersedes it.
- The older assets may still exist on GitHub for historical traceability.
- Stale does not mean the older release assets were edited, deleted, replaced, re-uploaded, rebuilt, or republished.
- Stale does not mean the older release is production-ready, unsafe by definition, or approved for any new support baseline.

Older `v0.1.0-dev` assets may be referenced historically when reviewing earlier publication records, comparing prerelease behaviour, investigating a report that explicitly came from that older release, or explaining why later evidence moved to `v0.1.0-dev.1`.

Older `v0.1.0-dev` assets should not be used for new installation, validation, support evidence, release-candidate certification, or user guidance unless the work is explicitly investigating historical behaviour from that prerelease.

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

