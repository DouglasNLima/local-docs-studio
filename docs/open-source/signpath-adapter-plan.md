# Non-publishing SignPath CI adapter plan

Design only. No active signing workflow, credentials, provider account or signed
native release exists.
This prepares implementation after licence/provider/governance decisions without
installing a dormant secret-consuming workflow into PR execution.

The [official GitHub integration](https://docs.signpath.io/trusted-build-systems/github)
requires uploaded workflow artefacts and derives origin from GitHub. Its action
accepts an artefact ID and waits for a manually approved signing request. All
preceding OSS jobs must use GitHub-hosted runners. Pin reviewed action commits
when implementation is authorised; mutable examples in provider docs are not pins.

The current `NativeSigning.ps1` contract is a synchronous local PowerShell adapter
with a five-minute deadline. It is **not** a working SignPath connector: a manual
approval can exceed that limit and a local file path is not GitHub provenance.
Do not disguise SignPath submission inside that adapter or weaken its checks.

## Proposed stages

1. A separate `workflow_dispatch` workflow accepts an exact 40-character source
   commit and explicit version. Resolve it on the approved protected release
   branch, compare the expected tree, require a clean checkout, reviewed source
   checks, approved licensing and absent existing tag/release. Use no PR trigger,
   no arbitrary provider script input and no automatic publication.
2. Restore locked dependencies; build the first-party native app with fixed
   product/version metadata. Record SDK/runtime/compiler inputs, dependency and
   file SBOM, licences/notices, source/tree, run/attempt and unsigned file hashes.
   This is provenance; do not claim reproducibility without a measured rebuild.
3. Upload the unsigned apphost as an immutable Actions artefact. A protected
   environment releases only a submitter token after named human approval.
   Submit its returned artefact ID, approved project/policy/configuration and
   JSON-encoded version parameter using the provider action. Require SignPath's
   own manual approval for the request. No signing key enters Actions.
4. Download into a new staging directory. Verify expected file set, unchanged
   non-signature payload, valid timestamp/Windows trust and independently reviewed
   subject plus SHA-256 certificate. Keep upstream DLLs unchanged and unsigned by
   this project. Refuse stale, wrong-run or extra files. Build the installer using
   the verified signed apphost. Resolve Inno generated-uninstaller signing with
   the provider before claiming full coverage; the current installer does not
   configure `SignedUninstaller=yes`.
5. Upload that installer as a second immutable artefact and submit a second
   explicitly approved request. Verify the returned installer, extract/compare its
   payload, check publisher and timestamp, then construct the portable ZIP from
   the same verified payload. Do not rebuild the signed application afterwards.
6. Generate SHA-256/size from the **final signed installer**. Sign the update
   envelope with the separate approved RSA-PSS key, verify it using the public
   client policy and bind tag/version/channel/architecture/origin. Include notices,
   SBOM, release notes, provenance and certification receipt. Archive results as
   workflow candidate artefacts only; no tag or GitHub Release mutation. Treat
   those artefacts as publicly accessible and include no credentials or private data.
7. A separately authorised publication action reviews these exact immutable
   artefacts and current release absence. It alone gets `contents: write` and a
   protected publication environment. Never replace existing release assets, move
   tags or treat a workflow rerun as the original reviewed candidate.

Minimum token scope for candidate/signing jobs is `contents: read` and, where
needed, `actions: read`. No repository-wide write or PR secrets. Pin checkout,
runtime setup and upload/download actions; disable persisted checkout credentials;
bound timeouts; validate inputs before shell use; record approval/run/digest links.
Retain failed attempts. The environment must actually require reviewers and
disallow self-approval where available; naming an environment is not a gate.

## Artefact restrictions to review with SignPath

Use explicit `LensDocsStudio.Native.exe` and versioned installer selectors,
`pe-file` Authenticode directives and enforced product/version metadata per the
[artefact reference](https://docs.signpath.io/artifact-configuration/reference).
An Actions ZIP wrapper needs a ZIP root. Do not use `**/*.dll` or an unrestricted
`**/*.exe` selector. Inno is not listed as a composite container in that reference;
use the two-stage outer-PE approach and obtain explicit confirmation for generated
installer/uninstaller code. No unverified XML configuration is supplied as if it
were accepted by the provider.

## Stable packaging contract still to implement

Keep `Build-NativePackage.ps1` and `native.yml` unsigned-preview acceptance intact.
Add a distinct release builder only after the gates are agreed: `1.0.0-rc.N` maps
to Preview/prerelease and `1.0.0` maps to Stable/non-prerelease. Require both signing
layers, approved trust and all checks before constructing a distributable candidate.
Preserve native AppId `{3AF043CA-F7C1-4D1F-A8BC-847B4C44E23F}`, install/state paths,
mutex and ProgID. Review preview shortcut migration when changing the display
name to `Lens Docs Studio`; keep legacy shell identity separate. Test repair,
rollback, historical migration and retention in disposable Windows environments.
No new Stable-capable installer is claimed by this audit milestone.
