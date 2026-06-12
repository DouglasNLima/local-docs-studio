# Lens Docs Studio v0.1.0-dev.2 Release Closure

Closure date: 2026-06-12

## Summary

`v0.1.0-dev.2` is published as a GitHub prerelease/dev release and has passed post-publication verification. This closure is documentation-only: it does not change runtime code, rebuild packages, republish artefacts, edit release assets, create tags, create releases, or merge to `main`.

No production readiness, go-live approval, stable-channel certification, or stable/latest positioning is claimed.

## Release Identity

| Field | Value |
| --- | --- |
| Release URL | `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.2` |
| Release tag | `v0.1.0-dev.2` |
| Release state | Prerelease/dev release |
| Tag target/source commit | `5602358ef241c144fccae13e980c9eb2920126d8` |
| Publication evidence commit | `f5104521f684de007e00593d5be3e1f370fcf5c8` |
| Post-publication verification commit | `9ebf2bd58dec324f27d93123af4b3ad8cc5e48b0` |
| Main merge performed | No |

## Published Assets

| Asset | Purpose |
| --- | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | Portable Windows ZIP package |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | Unsigned Inno Setup installer |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | Installer checksum file |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | Installer build report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T105712Z.md` | Release-candidate report |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T105712Z.json` | Release-candidate metadata |

Final published checksums:

| Artefact | SHA256 |
| --- | --- |
| ZIP | `C7C9E52322EDA140D9AB60F9D8D2BF257EED9EA898F50FC8EFA2D90A04A9BF0B` |
| Installer | `01C60DFAA57754EFFCCC763051D5EEDE0DB4E3284936C8BF7D1189D30DCA6C21` |

## Evidence Chain

| Evidence | Result |
| --- | --- |
| Phase 3AK diagnostics visibility evidence | PASS, bridge and folder-picker diagnostics visibility polish was implemented and validated before the release-candidate rebuild. |
| Phase 3AL onboarding copy evidence | PASS, first-run and onboarding copy polish was implemented and validated before the release-candidate rebuild. |
| Phase 3AM watcher/conflict copy evidence | PASS, watcher/conflict copy refinement was implemented and validated before the release-candidate rebuild. |
| Phase 3AN support bundle design | PASS as design-only; optional support bundle generation was not implemented or approved. |
| Phase 3AO installer evidence plan | PASS as planning-only; upgrade/uninstall evidence requirements were documented without rebuilding or publishing. |
| Phase 3AP prerelease guidance | PASS, stale older prerelease guidance was documented before publication. |
| Phase 3AQ release-candidate readiness | PASS as local readiness and artefact rebaseline; Phase 3AR later superseded its local artefact paths and hashes. |
| Phase 3AR publication bundle freeze | PASS, frozen bundle source commit `5602358ef241c144fccae13e980c9eb2920126d8`, ZIP SHA256 `C7C9E52322EDA140D9AB60F9D8D2BF257EED9EA898F50FC8EFA2D90A04A9BF0B`, installer SHA256 `01C60DFAA57754EFFCCC763051D5EEDE0DB4E3284936C8BF7D1189D30DCA6C21`. |
| Phase 3AS publication record | PASS, prerelease was published from the frozen Phase 3AR bundle with the expected six assets. |
| Phase 3AT post-publication verification | PASS, tag, prerelease state, asset inventory, downloaded checksums, downloaded ZIP smoke, and contained installer smoke passed. |
| Downloaded ZIP smoke | PASS |
| Installer smoke | PASS |
| Manual packaged sanity | `BLOCKED_MANUAL_PACKAGED_SANITY_NOT_EXECUTED` |

## Completed Slice Summary

- Diagnostics and folder-picker visibility were improved for packaged/native support reporting.
- First-run and empty-state onboarding copy was clarified around Open folder, Open file, Diagnostics, and local/offline expectations.
- Watcher/conflict wording was refined while preserving the existing non-destructive refresh behaviour.
- A support bundle contract was designed but not implemented.
- Installer upgrade/uninstall evidence requirements were documented for future use.
- Prerelease guidance now points new validation to `v0.1.0-dev.2` and preserves older releases as historical evidence.
- The final Phase 3AR bundle was published as `v0.1.0-dev.2` and post-publication verification passed.

## Remaining Known Limitations

- `v0.1.0-dev.2` is a prerelease/dev release, not a production or stable release.
- Production readiness and go-live approval are not claimed.
- No merge to `main` was performed.
- Manual packaged sanity remains blocked until real interactive packaged WebView2/onboarding/native picker/watcher observation is executed.
- The Windows installer remains unsigned and may trigger Windows SmartScreen or browser warnings.
- Runtime prerequisites remain external: .NET Desktop Runtime, Windows App SDK Runtime, and Evergreen WebView2 Runtime.
- WebView2 browser-local user data may remain after uninstall; cleanup remains documented-only to avoid silently deleting user/session state.
- ZIP remains the portable/fallback package. The installer is the easier Windows install path.
- `v0.1.0-dev.1` assets were not changed and remain historical prerelease evidence.
- The older `v0.1.0-dev` release remains older/stale relative to `v0.1.0-dev.2`.

## Closure Verdict

`v0.1.0-dev.2` is closed as the current verified prerelease/dev release for new validation. Post-publication verification passed, the published ZIP and installer hashes match the frozen Phase 3AR bundle, downloaded ZIP smoke passed, and installer smoke passed.

No further release asset action is required unless a future release is explicitly authorised.

## Next Work

- Collect prerelease feedback against `v0.1.0-dev.2`.
- Execute real manual packaged sanity when interactive packaged-app observation and control are available.
- Monitor installer, download, checksum, runtime prerequisite, and unsigned-installer issues.
- Decide the next dev release scope from observed feedback and release monitoring.
- Keep production readiness and go-live approval as separate approval gates.
- Consider implementing the optional support bundle only after explicit runtime approval.
