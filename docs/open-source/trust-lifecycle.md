# Native update trust lifecycle

Inspected baseline: `5c61d17966befee665856a7993cb174252df5214`. Trust stays empty in
`src/native/LensDocsStudio.Core/Updates/update-trust.json`. No proposed signer name,
test key, token, private key or certificate is embedded by this work.

## Independent controls

`UpdatePolicy` validates semantic precedence, Stable/Preview correspondence,
published/non-draft status, native product, OS/architecture, exact official release
URL/tag/asset/size/hash and the RSA-PSS/SHA-256 envelope. An unknown key can produce
an untrusted release-page offer; `UpdateService` rejects its download/installation.
It revalidates before download and again with a locked installer, checks SHA-256
and invokes the Windows verifier. The latter requires WinVerifyTrust success with
chain revocation checking and an exact subject/certificate SHA-256 allowlist pair.
The launcher waits for the exact owner process and checks again before execution.
User consent, unsaved-work handling and recovery precede shutdown.

A valid Foundation signature alone is insufficient: other projects can have the
same publisher subject. Keep both the manifest key and exact certificate pin.
Actual provider certificate reuse, renewal cadence and overlap are UNKNOWN until
the project is accepted and independently verified. Do not replace the pin with
`any valid signature`, a subject-only rule or a proposed issuer string.

## Bootstrap and planned rollover

The first trusted version requires a verified manual install. Current unsigned
previews with empty trust cannot install it automatically; signing a new package
does not retroactively establish trust in those clients.

For a planned certificate change, obtain the next public certificate through an
independent approved provider channel, verify subject, DER SHA-256, validity,
chain/revocation and profile/repository assignment. Ship a bridge version under the
old trusted identity containing the reviewed old+new pins. Test old→bridge→new and
the offline/expired/revoked paths with the actual provider. Retire the old pin in a
later release. If the provider cannot supply overlap or the old signer is no longer
usable, automatic continuity is **BLOCKED**; use verified manual recovery.

Manifest keys are independent. Generate them in approved protected storage/HSM
where possible, record only public material and identifiers in the repository,
restrict signing to reviewed manifests, audit access and back up/recover under
owner-controlled procedures. The current script accepts an external PEM file; it
is not evidence of secure production key custody. A bridge trusted by old key K1
can carry K1+K2; subsequent manifests use K2, followed by removal of K1. Merely
publishing K2 on GitHub is not trusted rotation.

## Compromise and revocation

Suspend signing/publication, revoke affected credentials with the provider, retain
evidence and investigate source/CI compromise. Do not use a compromised key to
establish a new root of trust. The client has embedded trust and no independent
online key-revocation feed: removing an old manifest key affects only clients that
receive the new binary. Windows certificate revocation is a separate control.
If neither a previously trusted safe bridge nor an independently verified recovery
path exists, require manual reinstall and explain the break transparently.

Do not move tags, replace assets or downgrade user data to recover an installer.
Publish a newer corrected version only after authorisation. Preserve workspaces,
sidecars, settings, unsaved drafts and restart ownership; record installation
failure without silently executing another binary. No silent elevation or forced
closure is permitted. Keep unsigned/mock/disposable evidence distinct from real
signed A→B certification.
