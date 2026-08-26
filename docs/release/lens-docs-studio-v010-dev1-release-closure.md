# Lens Docs Studio v0.1.0-dev.1 Release Closure

Closure date: 2026-06-11

## Summary

`v0.1.0-dev.1` is published as a GitHub prerelease and has passed post-publication verification. This closure is documentation-only: it does not change runtime code, rebuild packages, republish artefacts, edit release assets, create tags, create releases, or merge to `main`.

No production readiness, go-live approval, or stable-channel certification is claimed.

## Release Identity

| Field | Value |
| --- | --- |
| Release URL | `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.1` |
| Release tag | `v0.1.0-dev.1` |
| Release state | Prerelease/dev release |
| Frozen source commit | `c776fffdc8364c2c978f8106869a2d6bf50477ff` |
| Publication evidence commit | `28f9dd2705e62ea8e74bea213c5c47e0b1cc64a4` |
| Post-publication verification commit | `256a7af04e4f8d6775c42da855d5732fa574a31b` |
| Main merge performed | No |

## Published Assets

| Asset | Purpose |
| --- | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | Portable Windows ZIP package |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | Unsigned Inno Setup installer |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | Installer checksum file |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | Installer build report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260611T174119Z.md` | Release-candidate report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260611T174119Z.json` | Release-candidate metadata |

Final published checksums:

| Artefact | SHA256 |
| --- | --- |
| ZIP | `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A` |
| Installer | `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745` |

## Evidence Chain

| Evidence | Result |
| --- | --- |
| Phase 3AA Stage A packaged diagnostics | PASS |
| Phase 3AA native picker selection | PASS, workspace selected through the real packaged picker |
| Phase 3AA Stage B watcher/conflict evidence | PASS |
| Phase 3AB release-candidate artefact rebaseline | PASS |
| Phase 3AC publication preflight | PASS as preflight; no publication performed in that phase |
| Phase 3AD publication bundle freeze | PASS |
| Publication evidence | PASS, prerelease created from the frozen Phase 3AD bundle |
| Phase 3AF release tag verification | PASS |
| Phase 3AF prerelease verification | PASS |
| Phase 3AF asset verification | PASS |
| Downloaded ZIP smoke | PASS |
| Installer smoke | PASS |

## Remaining Known Limitations

- `v0.1.0-dev.1` is a prerelease/dev release, not a production or stable release.
- The Windows installer remains unsigned and may trigger Windows SmartScreen or browser warnings.
- Runtime prerequisites remain external: .NET Desktop Runtime, Windows App SDK Runtime, and Evergreen WebView2 Runtime.
- WebView2 browser-local user data may remain after uninstall; cleanup remains documented-only to avoid silently deleting user/session state.
- ZIP remains the portable/fallback package. The installer is the easier Windows install path.
- The older `v0.1.0-dev` assets remain older/stale relative to `v0.1.0-dev.1`; they were not modified during this closure.

## Closure Verdict

`v0.1.0-dev.1` is closed as a verified prerelease/dev release. Post-publication verification passed, the published asset hashes match the frozen bundle, downloaded ZIP smoke passed, and installer smoke passed.

No release assets require further action unless a future release is explicitly authorised.

## Next Work

- Collect user feedback from prerelease usage.
- Monitor installer and download issues.
- Plan the next dev release scope from observed feedback and known limitations.
- Decide later whether any release should be promoted, superseded, or archived.
- Keep production readiness as a separate approval gate.
