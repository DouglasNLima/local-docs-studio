# Security policy

**@DouglasNLima is the sole security maintainer.** No native Stable release or
guaranteed support period is certified. Native candidates remain unsigned internal
previews; the legacy shell and web/PWA have separate exposure.

## Private reporting status

GitHub Private Vulnerability Reporting is proposed. The API returned **disabled
on 28 October 2026**; this goal has not enabled or tested it.
[Security Advisories](https://github.com/DouglasNLima/local-docs-studio/security/advisories)
is the intended entry after activation, not an operational reporting promise.

Until a confidential route is verified, request private contact from @DouglasNLima
through [GitHub issues](https://github.com/DouglasNLima/local-docs-studio/issues)
without vulnerability details, exploits, credentials or personal data. This public
request is not confidential intake. No email/alternative responder is invented.
A functioning private route remains a launch blocker. The
[activation/synthetic-test plan](docs/open-source/private-reporting-plan.md)
requires separate authorisation before settings or report submission.

## Intake, severity and responsible disclosure

After verification, supply affected edition/version, prerequisites, impact and a
minimal synthetic reproduction. Avoid secrets and real workspaces/customer data.
The maintainer handles reports as availability permits: no acknowledgement/fix
deadline, SLA, second responder or independent escalation is promised.

Record intake privately and reproduce safely. Assess severity using reachability,
attacker access, user interaction and confidentiality/integrity/availability impact.
Code execution, trust bypass, arbitrary file access, disclosure, build compromise
and reproducible denial of service are reportable. No finding class is suppressed.

Coordinate disclosure privately with the reporter. Prepare patches in a private
advisory workspace where appropriate, review dependency/licence/trust implications
and run affected regression/packaging tests. Avoid leaking embargoed information
through public branches, CI logs or artefacts. The owner records patch and release
decisions and coordinates advisory timing, affected versions, mitigations and
reporter credit/consent through GitHub Security Advisories. Public disclosure is
a separate explicit decision; never publish synthetic channel tests.

On suspected account, build, certificate or manifest-key compromise, the same
maintainer suspends signing/publication, limits access, preserves evidence and
contacts the affected platform/provider privately. Resumption needs a recorded
owner decision and real trust validation. Emergency handling cannot bypass
signature, hash, consent or revocation checks.

## Security invariants

Scope covers web/PWA, legacy shell, native Core/WPF, installers, updater,
dependencies and CI. Documents, Mermaid, ZIP/Word imports, SVG/images, paths,
templates, links and optional artefact metadata are untrusted.

- Imports/exports stay within authorised destinations, reject traversal/reparse
  escapes, bound resource use and prevent active content.
- Documents cannot execute code, authorise remote requests or silently overwrite
  edits. Network image downloads require user action.
- Native updates require an independent RSA-PSS/SHA-256 signed release manifest
  binding product, version/channel, repository/tag, asset, length and hash; approved
  Authenticode publisher/subject and certificate SHA-256 validation; Windows trust
  and revocation; final locked-installer verification and explicit user consent.
- Empty production trust permits a release-page link only. First trusted
  installation is manual. Preserve planned certificate/manifest-key rotation and
  retirement; no automatic unsigned-preview bootstrap is approved.
- PR/internal-preview jobs receive no signing secrets or release publication rights.
  A passing PR does not authorise signing or release.
- Preserve recovery, workspaces and preferences through cancellation, failed
  upgrades and uninstall unless the user explicitly removes them.

MFA is required for the sole GitHub maintainer and future SignPath access. Verify
status privately without passwords/recovery material; verification is currently
unknown. Empty trust, uncertified signed upgrades/clean-machine/accessibility
behaviour and the unchanged Continuous 250 ms **FAIL** remain known limitations,
not security waivers. Mock tests are not signing evidence; provenance checks are
not a comprehensive security audit. See [trust lifecycle](docs/open-source/trust-lifecycle.md)
and [release gates](docs/open-source/release-gates.md).
