# Native update trust and packaging

## Current status - 24 October 2026

The authoritative [Current Windows readiness](current-windows-readiness.md) records
the accepted 24-family develop baseline, internal-preview restrictions and
[Goal VII Draft PR #47](https://github.com/DouglasNLima/local-docs-studio/pull/47).
PR #46 is merged and its post-merge native/web runs succeeded at
`33a751c60886a364102c45cb43e36af1e7f13517`. Higher-DPI, physical-reader, clean-Windows,
historical-upgrade and production-trust certifications remain separately blocked.

## Historical source-labelled records

The original chapters below retain their dates, revisions, package hashes, counts
and then-pending decisions. Their present-tense milestone wording is historical;
use the canonical summary and exact-source PR delivery receipt for current status.


## Internal Windows acceptance association policy

Every job in `native.yml` builds or consumes unsigned internal preview packages.
Its `internal-no-associations` policy therefore applies to pull requests, develop
pushes and manual runs alike. Workflow-level arguments pass
`--no-file-associations` to the visible wizard and `-NoFileAssociations` to each
install/upgrade/reinstall harness, including all twenty authoring families.
Branch names do not select installer tasks. The existing absence checks verify
that the excluded registrations remain absent throughout these hosted cycles.
The installer product's optional tasks and user defaults are unchanged.

The original develop [.853 failure](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39856703884)
used the association-enabled path. That coverage divergence is independently
proved; it is not established as the cause of its PDF observation failure.


The dedicated [Windows preview readiness report](windows-preview-readiness.md) records the subsequent release-readiness candidate, guide, package provenance and available acceptance. Historical evidence below retains its original revision and scope.

## Current delivery record

The [engineering acceptance report](engineering-completion-report.md) is the current 28-condition record. Delivered revision `70a2a9d0dc2c9fd1e4cebaf28c96015fde5d1a55` passes four verified hosted runs: native push/PR `38371057232`/`38371063213` (1,037 Core tests, 40 corpus cases), web push/PR `38371057253`/`38371063230` (264 cases each). Branch-source preview .287 and PR-merge .288 retain distinct source/hash labels. Earlier `7008751`, 1,035-test and .285/.286 evidence below remains historical.

The acceptance phase executed isolated native/Word Computer Use and representative persisted-workspace exports. Product corrections at `27d21b43` and final validation revision `f7d5d557a7e5601f4147af403dd4f1fe189bb65c` pass all four hosted pipelines. Final native push/PR `38419939879`/`38419949514` verify 1,055 Core/40 corpus; web push/PR `38419939724`/`38419949513` verify 268 cases. Thirteen observation and 13 persisted-workflow checks pass. Read the report for exact branch-source .299 pins, retained .291/earlier identities, original failures, real destination findings and the continuous-layout input defect. Clean Windows, live Excel/Azure DevOps, physical accessibility/hardware and production signing remain explicitly limited. Unsigned updates stay blocked; historical protected scans remain unchanged.

`Test-NativePreviewPreflight.ps1` supplies a read-only clean-image/package preflight. It requires independently verified installer SHA-256 and branch-source revision pins, rejects existing evidence destinations and wrong identity/hash, inventories development tools/SDKs/installation/elevation, and records all 12 lifecycle steps as NOT RUN. It never installs or launches the package, forges runner markers or marks a machine certified. It was executed against the exact .287, .291 and .299 installers on the developer desktop: package PASS, environment BLOCKED. A documented disposable Windows 11 snapshot remains required before actual clean-machine acceptance.

Product-correction branch preview .291 belongs to `27d21b43f214c6a2283ff68b2ff7f876e087283d`, native push run `38404441225`, artefact `11818883983`. Installer SHA-256 is `BA2EFD08636F8B7B673E0656982A449E246B01F6C8E8AFCF4C53AD0A96BD2647`; ZIP SHA-256 is `8DCE568B482FD6483800E91F644F2E6026230E40F5E8FBAC0BEF6817A683C14B`. It is self-contained x64, clean source, `NotSigned`, with empty update-envelope identity/signature and unchanged empty production trust. The original 70a example below retains its own pins; PR-merge .292 is a distinct source package. Subsequent documentation commits do not change these package identities.

Final-validation branch preview .299 belongs to `f7d5d557a7e5601f4147af403dd4f1fe189bb65c`, native push `38419939879`, artefact `11827121366`, digest `sha256:5cfce1e68497e2032c300cf9520e1437716bd145657fefec7877d0731ee0dd88`. Installer SHA-256 is `CBE70330FC099B57FBEBBFFE8BD2589C1536555A67907B7DAF95E7CC662A8E4E`; ZIP SHA-256 is `A373D9560A4187CCBC96F7AA2BEDC1019332840ABD2965DEFC770A4858820314`. Independently downloaded reports retain four 66-check desktop contexts, five wizard checks, 20 preserved state files and restored sessions. Package metadata is clean/self-contained/unsigned; envelope identity/signature and production trust remain empty. PR .300 is a distinct merge checkout. The following evidence-upload/docs head has its own package/check identities on PR 21; no earlier package is relabelled.

```powershell
./scripts/native/Test-NativePreviewPreflight.ps1 -OutputDirectory acceptance/preflight/new-287 `
  -Installer artifacts/native/ci-70a2a9d-push/packages/0.1.1-preview.287/LensDocsStudio.Native-0.1.1-preview.287-win-x64-Setup.exe `
  -ExpectedSourceRevision 70a2a9d0dc2c9fd1e4cebaf28c96015fde5d1a55 `
  -ExpectedSha256 3332AB0E708FDEE462B9039C8AC4E92EEB26BA0576DEF4335C7EB9A11A593A66
```

## Design and revision-specific evidence

The records below retain their recording revision and scope. Present-tense wording and pending outcomes in earlier milestone records are historical; the current report above supplies later verified outcomes without erasing the original failures.

Desktop/Office validation commands and Test-NativeInstaller.ps1 refuse execution outside disposable GitHub-hosted Windows before processing arguments or paths. These operational checks prevent accidental local side effects; they are not an authorisation boundary. Six local refusal checks pass without changing environment markers, the clipboard, installations or certificate stores. Positive execution belongs to hosted CI.

Status: internal preview implementation. Simulated preview upgrade validation passes. Production-signed updates, historical schema migration and SDK-free clean-machine certification remain outstanding.

Confirmed signing configuration (12 October 2026): no Authenticode certificate, signing service, thumbprint or verified publisher has been established. Builds remain unsigned internal previews. Signing support must remain pluggable; no identity may be inferred or invented. Unsigned update execution remains blocked. This is the user's confirmed configuration, not a pending request for credentials.

Changing channel cancels the current operation and clears its selected release/download, including a change back to the original channel. Downloads and final verification keep an immutable selection/environment snapshot and check cancellation again before presenting a result or requesting installation. The Core service also checks cancellation after external trust verification and removes failed download bytes. Six isolated interaction cases and 36 core update cases pass with mocked installer verification, alongside the existing disposable Windows trust checks. This adds no production identity and does not certify an actual production-signed upgrade.

Original combined trust evidence at 676523e: both native runs 38227300924/38227294457 pass 56 workflows in built/installed/upgraded/reinstalled applications, seven isolated signature checks and seven combined signed discovery/download/final-verification checks using the actual Windows verifier. The combined fixture uses in-memory GitHub responses, signed and unsigned disposable bytes and a fresh manifest key; it never launches downloaded content or exports the private key. Tampering, wrong publisher and failed-download cleanup are checked. Downloaded JSON confirms unchanged production trust and removal of both test certificates. This strengthens development evidence without certifying a production-signed installer upgrade.

The installer uses a distinct AppId, native state folder, installation path, mutex and Open with ProgID. Default file associations are preserved. No uninstall action removes workspaces or application state. Inno's files-in-use mutex requires the native app to close normally; forced application closure is disabled.

CI builds `0.1.0-preview.<run>` and `0.1.1-preview.<run>` from the same source revision and passes both packages to `Test-NativeInstaller.ps1`. Installation, upgrade and reinstallation must match the requested metadata, assembly version and uninstall registration. The initial UI run's existing private state files and a separate workspace document are hashed and checked through upgrade, uninstall and reinstall. Each installed version runs the desktop workflows. Five local file-observation checks and script parsing pass. Both 45b5d5d native CI runs 38217770147/38217763979 pass the expanded lifecycle and all 55 workflows in built/installed/upgraded/reinstalled contexts. This simulated upgrade does not establish historical schema migration, production signing or an SDK-free environment.

`Build-NativePackage.ps1` produces a self-contained x64 folder, installer, portable ZIP, version metadata, SHA-256 checksums and `native-update.json`. Output versions are immutable directories; a repeated version fails rather than deleting evidence. No publishing, tags or GitHub release mutations occur. The current packaging command accepts preview versions only.

Signing defaults to `Unsigned` and invokes no signing tool or provider. An explicitly configured signer may use either `SigningThumbprint` plus `SignToolPath` for an already provisioned certificate, or `SigningProviderPath` for a trusted local PowerShell adapter to a future service. Both modes also require the reviewed `ExpectedPublisherSubject` and `ExpectedCertificateSha256`; those values are currently unavailable. Incomplete or conflicting configuration fails before publishing files. No certificate or account is created or installed.

The provider contract is a `.ps1` script accepting `-FilePath` and `-TimestampUrl`. It signs the specified executable in place, uses credentials provisioned outside the repository, and exits non-zero on failure. It runs in a separate, non-interactive hidden PowerShell process with literal arguments and a five-minute deadline. Certificate-store signing uses SHA-256 and HTTPS timestamping. After either adapter returns, Windows must report a valid Authenticode signature, its signer must exactly match the reviewed subject and certificate SHA-256, and a timestamp certificate must be present. Both the apphost and installer pass this check before the portable ZIP and checksums are created. Package metadata records the adapter mode without credential or account details. Configuring a package signer does not alter the application's embedded update trust policy.

`Test-NativeSigning.ps1` passes 14 adapter/policy outcomes with mocked Windows signature results, including literal paths, unsigned defaults, missing/conflicting identities, failed provider exits, unsigned/invalid output, wrong subject/fingerprint and missing timestamp. It signs no executable and changes no certificate store. The separate isolated Windows verifier suite below provides actual Authenticode evidence; production signing and service interoperability remain unexecuted. All 34 update-filtered core tests also pass, including unsigned metadata being limited to a release-page link and rejection before executable download.

The application queries the repository's public GitHub Releases API after startup, then checks eligibility hourly while running. Persisted attempts limit automatic network checks to once per 24 hours, including failures. Manual checks bypass the interval. Stable is the default; Preview is explicit. Disabling checks persists. The app sends no local documents, paths or credentials. Release notes are plain text.

An eligible release must be published under `native-v<semantic-version>` and contain `native-update.json` plus exactly the named native installer. The envelope has schemaVersion 1, base64 UTF-8 payload, keyId and base64 RSA-PSS/SHA-256 signature. The signed payload declares schemaVersion, productId `LensDocsStudio.Native`, version, channel, architecture, minimumWindowsVersion, releaseTag, assetName, size and sha256. Product, version, channel, Windows compatibility, official repository asset URL and size must all match. Old shell releases and downgrades are excluded.

`New-NativeUpdateManifest.ps1 -PackageDirectory <folder> -SigningKeyPath <external-private-key-file> -KeyId <reviewed-key-id>` signs the existing installer metadata. It never prints the key or adds it to the repository. Provision the public RSA key and expected Authenticode subject/certificate SHA-256 through a reviewed change to the embedded `Updates/update-trust.json`. The current trust policy intentionally contains no production identities. Untrusted preview metadata permits a release-page link only; in-app download and execution remain disabled.

For a trusted update, the user separately chooses download and install. Size and SHA-256 are checked against signed metadata. Windows [WinVerifyTrust](https://learn.microsoft.com/windows/win32/api/wintrust/nf-wintrust-winverifytrust) validates Authenticode and revocation, and the verified signer must match the pinned publisher identity. A temporary native launcher copy waits for the exact parent process to exit, rechecks the installer while holding a read lock, and starts the interactive installer. It records the outcome and restarts the application with the previous state directory. Unsaved edits are prompted for and session recovery is persisted before shutdown. Cancellation, trust failure or download failure never executes the installer.

The launcher uses the current executable's embedded version and trust policy, not a version supplied by release notes. The temporary helper cache is retained for diagnosis; cache cleanup and a dedicated small helper are future refinements. There is no always-running update service. Positive production signature and signed upgrade validation must still be performed with authorised credentials; mock trust tests are labelled development evidence.

`Test-NativeAuthenticode.ps1` is a separate CI-only development check. On an elevated, disposable GitHub-hosted Windows runner it creates a one-day, non-exportable test certificate, signs an isolated copy of the built apphost with [SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool), and exercises the actual Windows verifier. Its seven cases cover a valid pinned signer, unsigned bytes, tampered bytes, wrong certificate fingerprint, wrong subject, empty policy and cancellation. The public certificate is temporarily added to LocalMachine\Root; its private key stays in CurrentUser\My. Removal of both is checked; only JSON evidence is uploaded. The application build and embedded production trust remain unchanged. Stage messages and a three-minute CI timeout bound diagnosis. Initial runs stalled without identifying a stage and were cancelled. A current-user root confirmation prompt is plausible, as illustrated by [Microsoft's certificate import guidance](https://learn.microsoft.com/en-us/troubleshoot/developer/visualstudio/installation/warnings-untrusted-certificate), but is not established by those logs. Local syntax/build/static checks and the local-host refusal guard pass. All seven actual-verifier cases and certificate/private-key cleanup pass in both complete 0045689 native CI runs 38215212276/38215207396; their logs are retained under artifacts/native. This does not certify a production issuer, revocation endpoint, timestamp service or signed upgrade.
