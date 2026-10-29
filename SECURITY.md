# Security policy

**@DouglasNLima is the sole security maintainer.** No second responder, independent
escalation service, acknowledgement/fix SLA or guaranteed support period is promised.

## Private reporting status

**PVR_ENABLED_EXTERNAL_FLOW_UNVERIFIED**. On 29 October 2026 GitHub's API confirmed
`enabled: true` following authorised activation. The
[execution receipt](https://github.com/DouglasNLima/local-docs-studio/issues/62#issuecomment-6501362732)
records the actual API results. [Security Advisories](https://github.com/DouglasNLima/local-docs-studio/security/advisories)
is the intended private reporting entry. Reporter submission, maintainer
notification, confidentiality and acknowledgement have **not been tested
end-to-end** with a genuine external reporter. No synthetic advisory was submitted.
An administrator's advisory form is not evidence of an external reporter's flow.

Until a confidential route is verified, you may request private contact from
@DouglasNLima through [GitHub issues](https://github.com/DouglasNLima/local-docs-studio/issues)
without vulnerability details, exploits, credentials or personal data. This public
request is not confidential intake. No alternative contact address is invented.
Do not disclose sensitive information publicly to test reporting. Any authorised
external test and account/notification change needs a separate owner decision.

## Scope and handling

This main tree contains the browser/PWA, legacy WinUI/WebView2 shell, packaging,
dependencies and CI. Native WPF development on develop remains a separate unsigned
internal candidate; there is no public native WPF release or SignPath acceptance.
Report security issues in any edition, including code execution, trust bypass,
arbitrary file access, disclosure, build compromise and reproducible denial of
service. Once a private route is established, include edition/version,
prerequisites, impact and a minimal reproduction without real user/customer data.

Documents, diagrams, ZIP/Word imports, images, paths, templates, links and optional
artefact metadata are untrusted. Imports/exports must respect authorised paths and
user consent, bound resource use, preserve user data and prevent active content.
The maintainer triages privately as availability permits and coordinates patches,
affected versions, mitigations, disclosure timing and reporter credit/consent.
Avoid leaking embargoed information through public branches, logs or artefacts.
Public disclosure is a separate explicit decision.

On suspected account, build or future signing compromise, suspend publication,
preserve evidence and use the affected platform's private recovery route. Do not
bypass trust, hash, consent or revocation checks. MFA status, real reporting
delivery and signed release certification remain unverified. Passing provenance
or documentation checks is not a comprehensive security audit or release approval.
The inherited Continuous 250 ms **FAIL** and historical UI Automation reliability
findings on native development remain unchanged; this policy does not waive them.
