# Code signing policy

**Draft: no signing provider has accepted this project, no production certificate
or updater trust identity is configured, and no signed Stable release is offered.**
The proposed route is SignPath Foundation with GitHub Releases. Free signing is
conditional on external acceptance. See the [eligibility review](signpath-readiness.md).

The future signing scope is the native application apphost and its installer from
the approved source/run. Never submit arbitrary paths, upstream DLLs, the legacy
shell, user documents or development certificates. The team must agree the exact
artefact definition, compiler-generated apphost and embedded upstream boundaries
with the provider. Keep dependency notices, hashes and source evidence available.

Each signing request requires a named human approver. Publication requires a
separate explicit decision after signature, manifest, payload and upgrade checks.
The proposed trusted Author, reviewer of external contributions, human approver
of each signing request and final Release Authoriser is **@DouglasNLima**, the
sole maintainer. These are multiple roles, not independent human approval.
Provider acceptance of this allocation remains unconfirmed; no signing identity
is invented. MFA and actual branch/environment controls need verification.
A future owner-reviewed environment must leave Prevent self-review disabled,
or the only maintainer could not approve their own initiated deployment.

The Foundation route uses its publisher identity; do not substitute the project
owner's name or assume a stable certificate fingerprint. Add provider credit and
actual issuer details only after approval, using the provider's then-current
required wording. Do not advertise sponsorship in advance.

The [privacy disclosure](privacy.md) describes update checks and user-directed
network activity. The project does not claim that all operation is offline or
that no network request happens automatically. Installation must disclose network
behaviour and the option to disable automatic update checks before release.

If provenance, approval, signing, timestamp, revocation, publisher identity or
manifest verification is missing, stop the release. Existing unsigned previews
cannot bootstrap automatic installation of the first trusted release. Users need
a verified manual installation. Preserve user data and allow cancellation.

Authoritative proposal: [adapter plan](signpath-adapter-plan.md),
[trust lifecycle](trust-lifecycle.md), [governance](governance.md) and
[certification gates](release-gates.md).
