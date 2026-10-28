# Security policy

Proposed policy for the Goal #56 review. No production Stable release or support
period is certified. The current native distribution remains an unsigned internal
preview; the legacy shell and static web edition have separate exposure.

## Reporting privately

The intended channel is [GitHub private vulnerability reporting](https://github.com/DouglasNLima/local-docs-studio/security/advisories/new).
The repository API reported this feature **disabled on 28 October 2026**. It must
be enabled and verified by the owner before this channel is advertised as working.
No private email address is invented here. Until then, request a private contact
from the repository owner without including vulnerability details, credentials,
personal data or exploit material in a public issue. A working confidential route
is a launch blocker. Do not submit sensitive information to a disabled form.

Once the channel is enabled, provide the affected version, a minimal synthetic
reproduction, impact and relevant platform details. Maintainers should acknowledge,
triage and coordinate disclosure privately; response deadlines and an alternate
contact remain owner decisions. Never include secrets in public receipts.

## System and trust boundaries

Scope covers the buildless web/PWA, legacy Windows shell, native Core/WPF app,
installer/update tooling, dependencies and CI workflows. Local Markdown, Mermaid,
ZIP/Word imports, SVG/images, file names, templates, links and optional artefact
metadata are untrusted. Local user files and recovery/state are valuable assets.
Public release metadata, downloads and PR code are untrusted until independently
verified. Repository write access is not release/signing approval.

## Security invariants

- Imports and exports must stay within their authorised destinations, reject
  traversal/reparse escapes, bound resource consumption and avoid active content.
- Opening documents or metadata must not execute code, authorise remote requests
  or silently overwrite unsaved work. Network image download requires user action.
- Native updates must bind product, version/channel, repository/tag, asset, size
  and hash to a trusted RSA-PSS manifest, then validate Windows Authenticode,
  revocation and the exact approved subject/certificate SHA-256 pair. Empty trust
  permits a release-page link only. Final locked-file verification and user consent
  precede installer execution; no silent forced upgrade or elevation.
- PR/internal-preview jobs must not receive signing secrets or publication rights.
  Candidate source, checks, artefact identities and approval must remain auditable.
- Recovery, workspace content and preferences must survive cancelled operations,
  failed upgrades and uninstall unless the user explicitly removes them.

## Reportable findings and limitations

Report reachable code execution, trust bypass, arbitrary reads/writes, sensitive
data disclosure, dependency/build compromise and reproducible denial of service
with realistic inputs. Explain prerequisites and impact; severity is assessed
from exposure and evidence. No finding class is suppressed by this proposal and
no new accepted risk is declared.

Known limitations include empty production trust, no certified signed upgrade,
pending clean-machine/accessibility certification and the inherited Continuous
250 ms failure. These limitations do not excuse a security flaw. Passing mock
tests is not production-signing evidence. The legal audit's pattern scan is not
a complete vulnerability assessment. See [trust lifecycle](docs/open-source/trust-lifecycle.md)
and [certification gates](docs/open-source/release-gates.md).
