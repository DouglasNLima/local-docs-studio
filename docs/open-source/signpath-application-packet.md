# SignPath Foundation owner decision packet — not submitted

Target: `DouglasNLima/local-docs-studio`, native .NET/WPF edition, free official
distribution. Objective: `OPEN_SOURCE_FOUNDATION_READY_FOR_OWNER_DECISION`.
This packet prepares evidence and unanswered decisions; **application readiness
is NO-GO**. The official [terms](https://signpath.org/terms.html) (the page says
Draft) and [application page](https://signpath.org/apply) were checked on 28 October
2026. Their interpretation and acceptance remain with SignPath. The embedded form
did not expose its fields in the inspected page/browser; do not describe this as
a verified list of every current form field.

## Eligibility and evidence matrix

| Requirement | Evidence / missing decision | Owner decision |
|---|---|---|
| Permissive open-source project and component grants | First-party MIT is not adopted. See the exact rights/path proposal; upstream grants stay separate | OWNER ACTION REQUIRED |
| Maintained, documented project | Current source, user guides and exact-source hosted validation are inspectable; provider assesses reputation | NOT APPLICABLE to an owner approval; technical evidence supplied |
| Already released in the form to be signed | Five historical legacy Windows releases are not the native WPF product. Actions previews are not a public native release | BLOCKED |
| Eligible included components | Native package bytes and original notices are mapped. Ask about Knuth/OFL fonts, apphost/runtime exception, adapted Mermaider/dagre and Inno-generated components | BLOCKED pending provider interpretation |
| Own binaries and verifiable build origin | Proposed selectors below; build source/tree, immutable artefact digest and file SBOM supplied | OWNER ACTION REQUIRED to approve future scope |
| Named Authors, Reviewers, Approvers and MFA | No team or signing identity is assigned or verified; no real manual signing approval has occurred | OWNER ACTION REQUIRED |
| Public signing/privacy policies | Drafts exist and disclose network activity; approve responsibilities and provider wording after acceptance | OWNER ACTION REQUIRED |
| Trusted build integration, product/version restrictions, manual release approval | Design only; no SignPath project, production secret or protected publication environment exists | BLOCKED |
| No harmful or unwanted behaviour | Documentation purpose and source are visible; tests do not certify absence of all harmful behaviour | BLOCKED pending the actual provider/security assessment |

The terms require project/component eligibility, maintained released software,
traceable builds, real responsible members with MFA and manual approval. The
Foundation is the certificate publisher; acceptance can be withdrawn. Upstream
libraries may be included under their grants without being signed as project code.
Do not infer that a proprietary SDK qualifies as a system library. The legacy
Windows App SDK/WebView2 scope is excluded from this native proposal.

## Proposed exact signing boundary

First stage: only the native apphost at the root of an approved clean publish
directory, exact filename `LensDocsStudio.Native.exe`. It contains the SDK apphost
template; provider approval of that boundary is pending. Do not submit upstream
DLLs, Core/Native DLLs, test executables, updater fixtures or arbitrary `**/*.exe`.

Second stage: only `LensDocsStudio.Native-<approved-version>-win-x64-Setup.exe`,
where `<approved-version>` is one owner-approved immutable release value. The
current actual candidates are `0.1.0-preview.957` and `0.1.1-preview.957`, **not
authorised signing requests**. Stage two follows independent verification of the
signed apphost and rebuilding the installer around it. Inno's generated uninstaller
and embedded compiler code require an explicit provider-supported arrangement;
they are not silently added to the allowlist. The current preview-only builder
does not implement Stable packaging. Confirm enforced PE product/name/version and
installer metadata with the provider before creating a production policy.

Inputs for an eventual application: repository/project URL; approved licence and
rights scope; user guides; native download URL (missing); exact source/build/run
and artefact digests; binary SBOM and notices; proposed selectors; signing/privacy
policies; approved real role membership and MFA evidence. Form-field verification,
all missing values and final submission require owner review. Do not fabricate a
native download URL, use a legacy release as a substitute, or publish a beta merely
to satisfy eligibility. A future owner-approved provider enquiry may clarify how
to resolve this prerequisite; none was sent.

## Trust, renewal and failure recovery

Keep the [existing adapter](signpath-adapter-plan.md) and
[trust lifecycle](trust-lifecycle.md) boundaries. An Authenticode publisher subject
shared across Foundation projects is not project-specific authorisation. Require
the approved certificate SHA-256/subject pair **and** the independent RSA-PSS
manifest binding product, version/channel, repository/tag, asset, length and hash.
Do not populate current empty production trust with guessed names or fingerprints.

For renewal, obtain actual certificate identities and validity windows through an
approved provider channel, validate them independently, review a bounded overlap
and ship the trust bridge through the currently trusted route before retirement.
Manifest-key rotation uses the same reviewed bridge/retirement sequence; no
downloaded manifest can appoint its own key. Record which approved versions trust
each identity. Test revocation, expiration, timestamp validation and rejected old
keys with the real provider before release, preserving failure evidence.

On signing outage, mismatch, revoked trust or missing approval, stop publication
and installation; never fall back to an unsigned installer or bypass checks.
If no trusted bridge remains, recover through an independently verified manual
installation, with owner-approved instructions and preserved user state. First
installation also needs that manual bootstrap: the existing unsigned preview has
no production identity from which to authenticate an automatic upgrade. A provider
suspension requires explicit recovery review, not an improvised replacement signer.

No application, certificate, secret, pin, signed installer, tag, beta, RC or Stable
release was created. Microsoft Store/MSIX remains the separately assessed fallback
in [the earlier review](signpath-readiness.md), with no packaging work here.

Separate verdicts: foundation review **CONDITIONAL GO**, MIT adoption **NO-GO**,
SignPath application **NO-GO**, Stable publication **NO-GO**.
