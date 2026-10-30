# SignPath Foundation owner decision packet — not submitted

Current evidence: [eligibility matrix](signpath-eligibility-2026-10-29.md),
[network audit](network-privacy-audit.md), [owner decision/contact review](signpath-owner-decisions.md).
Foundation-specific preliminary route: SIGNPATH_CONTACT_ROUTE_UNVERIFIED.

Target: `DouglasNLima/local-docs-studio`, native .NET/WPF edition, free official
distribution. Updated on 29 October 2026 after scoped MIT adoption on `develop`, production
protection activation and API-confirmed PVR enablement. The [current controls](governance-settings.md)
link the execution evidence; historical stage receipts retain their original state.
The technical preparation packet is **PREPARED FOR OWNER REVIEW**; provider eligibility is
**UNCONFIRMED**, and application submission remains **NOT AUTHORISED**. The official [terms](https://signpath.org/terms.html) (the page says
Draft) and [application page](https://signpath.org/apply) were checked on 29 October
2026. Their interpretation and acceptance remain with SignPath. The embedded form
did not expose its fields in the inspected page/browser; do not describe this as
a verified list of every current form field.

## Eligibility and evidence matrix

| Requirement | Evidence / missing decision | Owner decision |
|---|---|---|
| Permissive open-source project and component grants | MIT is operative for enumerated original R1/R2 portions on develop at `709564dda30a57cf1e98ba07566f510c1636e05c`; target-specific main promotion via PR #65 and Pages publication are verified. R3–R7 and mixed portions are excluded; upstream grants stay separate | ADOPTED on develop and main; component/release scope BLOCKED |
| Maintained, documented project | Current source, user guides and exact-source hosted validation are inspectable; provider assesses reputation | NOT APPLICABLE to an owner approval; technical evidence supplied |
| Already released in the form to be signed | Five historical legacy Windows releases are not the native WPF product. Actions previews are not a public native release | BLOCKED |
| Eligible included components | Native package bytes and original notices are mapped. Ask about Knuth/OFL fonts, apphost/runtime exception, adapted Mermaider/dagre and Inno-generated components | BLOCKED pending provider interpretation |
| Own binaries and verifiable build origin | Proposed selectors below; build source/tree, immutable artefact digest and file SBOM supplied | OWNER ACTION REQUIRED to approve future scope |
| Named Authors, Reviewers, Approvers and MFA | @DouglasNLima is proposed for all roles; MFA verification and provider acceptance of this allocation remain unknown; no signing identity/request exists | OWNER APPROVAL REQUIRED for future activation; provider acceptance BLOCKED |
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

### Solo-maintainer eligibility finding

The official terms were re-read on 29 October 2026. They define trusted Authors,
review of non-committer changes and human approval of each signing request, with
MFA on both GitHub and SignPath. They do **not explicitly require different
people** for these roles. This supports proposing the sole maintainer
@DouglasNLima for source author, external contribution reviewer, each signing
approval and final release authorisation; it is not proof that SignPath accepts
this project's allocation. The published terms do not give an explicit
single-person acceptance commitment. Provider interpretation/acceptance is still
UNCONFIRMED, not a reason to invent a second person.

Prepared question for a later owner-authorised enquiry: “Will the Foundation
accept @DouglasNLima as the sole trusted source author, external-contribution
reviewer and manual approver of each signing request for Lens Docs Studio, with
documented self-review, enforced CI and MFA? Please also confirm the proposed
native component/signing boundaries and how the existing-release prerequisite
applies to unsigned internal previews.” No enquiry/application was sent.

Future GitHub environment review uses this real owner with Prevent self-review
disabled and administrator bypass disabled. It is manual owner approval, not
independent approval. No signing environment, secret or workflow is activated.

### Binary selectors

First stage: only the native apphost at the root of an approved clean publish
directory, exact filename `LensDocsStudio.Native.exe`. It contains the SDK apphost
template; provider approval of that boundary is pending. Do not submit upstream
DLLs, Core/Native DLLs, test executables, updater fixtures or arbitrary `**/*.exe`.

Second stage: only `LensDocsStudio.Native-<approved-version>-win-x64-Setup.exe`,
where `<approved-version>` is one owner-approved immutable release value. The
historical Goal #58 baseline examples are `0.1.0-preview.957` and
`0.1.1-preview.957`, **not
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

Separate verdicts: scoped MIT **ADOPTED on develop and main**, technical packet
**PREPARED FOR OWNER REVIEW**, effective Solo Maintainer governance **ACTIVE AND VERIFIED**, provider eligibility
**UNCONFIRMED**, application **NOT SUBMITTED**, Stable publication **BLOCKED**.

## Prepared enquiry and application inventory

The [unsent enquiry](signpath-enquiry-draft.md) is the exact proposed message for
owner review, with four focused questions covering combined roles, released
native-product prerequisite, included/signable components and approved certificate
identity/rotation. It is a clarification route, not an application or request to
sign a candidate. No recipient/account/contact channel has been authorised.

| Application input | Prepared value / honest gap |
|---|---|
| Project | Lens Docs Studio — Local Markdown, Mermaid, and documentation studio |
| Repository | https://github.com/DouglasNLima/local-docs-studio |
| Source and first-party terms | Final PR SHA/tree and exact LICENSING.md manifest; scoped MIT adopted on develop and independently enumerated main via merged PR #65; Pages publication verified |
| Download of released native Windows edition | MISSING; only five legacy v0.1.0-dev* releases exist; internal previews are not released-native evidence |
| Responsible human | @DouglasNLima, all author/reviewer/approver roles; no independent human reviewer |
| MFA | UNVERIFIED on GitHub; no SignPath account established; owner-approved private status check required |
| Effective controls | Production rulesets 26127883/26127901 active, 30/two applicable checks, zero mandatory human approvals and no bypass; PVR_ENABLED_EXTERNAL_FLOW_UNVERIFIED |
| Public policies | Repository README, code-signing-policy.md and privacy.md; no provider-credit claim before acceptance |
| Component/source evidence | Current source ledger; preserved grants; historical .957 per-file binary SBOM explicitly labelled historical; final hosted candidate inspection recorded separately |
| Build and artefact proof | Exact final PR/check run links in the consolidated receipt; no production reproducibility claim |
| Signing selectors | Root LensDocsStudio.Native.exe, followed by one immutable versioned native Setup.exe; no recursive EXE/DLL allowlist |
| Certificate/issuer/subject/fingerprint/rollover | UNKNOWN until independently verified provider evidence; empty production trust remains unchanged |
| Embedded application form | Not verified: retrieved public apply page exposes no input fields; map prepared answers to the actual form only after authorised access |

PVR enablement is API-confirmed; external reporter submission, maintainer notification,
confidentiality and acknowledgement remain unverified. No synthetic report was
submitted. Native WPF previews remain unsigned internal candidates. There is no
public native WPF release, provider acceptance or authorised enquiry dispatch.

Missing values are blockers, not questions to answer by inference. Exclusions covering shipped examples/artwork and fonts can affect the
provider's all-component requirement; first-party MIT alone cannot establish
eligibility. The source manifest is not a licence for an entire assembly or
installer. Follow-up after explicit approval is a provider response and authorised
private MFA/control evidence, followed by separately scoped signing and
clean-Windows certification under Goal #56.
