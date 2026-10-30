# Code signing policy

**Current signing status: no signing provider has accepted this project, no production certificate
or updater trust identity is configured, and no signed Stable release is offered.**
The proposed route is SignPath Foundation with GitHub Releases. Free signing is
conditional on external acceptance. See the [policy evidence review](public-policy-review.md).

The future signing scope is the native application apphost and its installer from
the approved source/run. Never submit arbitrary paths, upstream DLLs, the legacy
shell, user documents or development certificates. The team must agree the exact
artefact definition, compiler-generated apphost and embedded upstream boundaries
with the provider. Keep dependency notices, hashes and source evidence available.

The proposed stages are only `LensDocsStudio.Native.exe`, then
`LensDocsStudio.Native-<approved-version>-win-x64-Setup.exe` containing the verified
signed apphost. Neither selector is accepted by the provider. Embedded icons,
samples, modified adapters, runtimes, fonts and Inno-generated code require the
[component assessment](public-policy-review.md); scoped MIT alone does
not establish the no-proprietary-components condition.

Each signing request requires a named human approver. Publication requires a
separate explicit decision after signature, manifest, payload and upgrade checks.
The proposed trusted Author, reviewer of external contributions, human approver
of each signing request and final Release Authoriser is **@DouglasNLima**, the
sole maintainer. These are multiple responsibilities held by one person, not
independent human review or approval.
Provider acceptance of this allocation remains unconfirmed; no signing identity
is invented. Production branch controls are [active and verified](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/docs/open-source/governance-settings.md).
Rulesets 26127883/26127901 require PRs, resolved conversations, strict checks from
GitHub Actions app 15368, no bypass, and force-push/deletion restrictions. Develop
requires 30 contexts; main requires Test static app and OSS provenance. Neither
requires a second person's approval. Actual CI success must be checked for the
reviewed source; skipped checks and a green merge box are not release certification.
MFA remains unverified; a future signing environment needs separate approval and
effective-control verification. No signing environment or secret exists today.
A future owner-reviewed environment must leave Prevent self-review disabled,
or the only maintainer could not approve their own initiated deployment.

The Foundation route uses its publisher identity; do not substitute the project
owner's name or assume a stable certificate fingerprint. The required future
credit, conditional on provider approval and actual service, is:
"Free code signing provided by SignPath.io, certificate by SignPath Foundation".
This records future wording only; it is not an active benefit or endorsement.
Add actual issuer details only after independent verification.

The [privacy disclosure](privacy.md) describes update checks and user-directed
network activity. The project does not claim that all operation is offline or
that no network request happens automatically. Installation must disclose network
behaviour and the option to disable automatic update checks before release.

If provenance, approval, signing, timestamp, revocation, publisher identity or
manifest verification is missing, stop the release. Existing unsigned previews
cannot bootstrap automatic installation of the first trusted release. Users need
a verified manual installation. Preserve user data and allow cancellation.

Authoritative proposal: [adapter plan](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/docs/open-source/signpath-adapter-plan.md),
[trust lifecycle](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/docs/open-source/trust-lifecycle.md), [governance](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/docs/open-source/governance.md) and
[certification gates](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/docs/open-source/release-gates.md).

Current [download/release guidance](downloads.md) distinguishes legacy releases from native internal previews.
