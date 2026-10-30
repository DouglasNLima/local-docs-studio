# SignPath preliminary enquiry and routing review — 30 October 2026

Current decision packet for [Goal #62](https://github.com/DouglasNLima/local-docs-studio/issues/62),
within parent [Goal #56](https://github.com/DouglasNLima/local-docs-studio/issues/56).
This is preparation for owner approval, not provider contact or an application.

**SIGNPATH_ELIGIBILITY_UNCONFIRMED**
**SIGNPATH_PRELIMINARY_ENQUIRY_PREPARED_NOT_SENT**
**SIGNPATH_CONTACT_ROUTE_UNVERIFIED** (Foundation eligibility reviewers)
**PVR_ENABLED_EXTERNAL_FLOW_UNVERIFIED**
**NATIVE_STABLE_NO_GO**

## Current source and publication

Fresh fetch/API readback on **30 October 2026**:

| Source | Verified identity / outcome |
|---|---|
| develop | `327b35cbc016795edc35a2307c347434c09a3e9b`, tree `b62335d09043163bc6acc6128b46ac87f3effae1` |
| main | `d9b903a504ace622abe99c7f4c34f5d15d331b7b`, tree `44df7c520e14ee0c55194e2d77c4db48c68a3e58` |
| [PR #64](https://github.com/DouglasNLima/local-docs-studio/pull/64) | Merged to develop at `8fdcb5eba90347a829e88e8f30e4ab489b437038`; policy refresh after governance activation |
| [PR #65](https://github.com/DouglasNLima/local-docs-studio/pull/65) | Merged to main at `68f1dc955b27a47347a6fccc52a463472b601a1b`; target-specific scoped MIT/security publication |
| [PR #66](https://github.com/DouglasNLima/local-docs-studio/pull/66) | Merged to develop at the current baseline; dated eligibility/source evidence and unsent enquiry |
| [PR #67](https://github.com/DouglasNLima/local-docs-studio/pull/67) | Merged to main at the current baseline; final policy wording corrected before publication |
| [Pages 40978852428](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40978852428) | Attempt 1 FAILURE; attempt 2 SUCCESS on identical main SHA, including actual deployment. No new deployment in this task |
| Develop post-merge CI | [Web 40977112904](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40977112904), [provenance 40977112903](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40977112903), [native 40977113033](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40977113033): completed/success |

The [published signing policy](https://douglasnlima.github.io/local-docs-studio/docs/open-source/code-signing-policy.md),
[Privacy policy](https://douglasnlima.github.io/local-docs-studio/docs/open-source/privacy.md),
[Downloads](https://douglasnlima.github.io/local-docs-studio/docs/open-source/downloads.md),
LICENSING.md, SECURITY.md and service-worker.js returned HTTP 200. Each matched
current main after CRLF-to-LF normalisation; raw response hashes differ from Git
because publication has CRLF. This is a direct HTTP/source comparison, not a new
browser/offline or native certification. Main remains its older browser/legacy
shell tree; native WPF source is only on develop. Policies are public; future
signing, conditional provider credit and installer privacy implementation are not.

Effective rules were read from `/rules/branches/develop` and `/rules/branches/main`,
as well as rulesets [26127883](https://github.com/DouglasNLima/local-docs-studio/rules/26127883)
and [26127901](https://github.com/DouglasNLima/local-docs-studio/rules/26127901).
Both are active, have no bypass actors, require PRs/resolved conversations, strict
checks, prevent deletion/force pushes and require zero human approvals. Develop
requires 30 contexts; main requires Test static app and OSS provenance, all from
GitHub Actions app 15368. Merge commits alone are enabled; automatic/squash/rebase
merge and automatic branch deletion are disabled. The API also reports
`require_extra_approval_for_unattributed_changes: true`; this does not create an
independent human reviewer. Classic protection endpoint availability is not used
as a proxy for effective rules.

Only @DouglasNLima appeared as a direct collaborator (admin), consistent with the
recorded sole-owner declaration. PVR returned enabled; external delivery remains
unverified. Only github-pages was listed as an environment; no repository Actions
secret names were returned. This does not inspect all possible organisation,
environment or external account secrets. MFA and provider account state cannot be
independently established from these repository observations. Five legacy
`v0.1.0-dev*` releases remain; no native public release exists.

See the [bounded API/HTTP observation](evidence/goal62/preliminary-enquiry-baseline-2026-10-30.json).
The [29 October matrix](signpath-eligibility-2026-10-29.md),
[network source audit](network-privacy-audit.md), Stage C
[receipt](goal62-signpath-receipt.md), earlier PR descriptions and failed CI attempts
remain historical. Their old unprotected/draft/publication/form-retrieval statements
must not override this readback; they have not been rewritten.

## Official contact routes

Read-only official retrieval on **30 October 2026**, without filling forms,
registering, emailing or creating tickets:

| Route and official source | Organisation / supported purpose | Preliminary questions / application boundary | Authentication / recommendation |
|---|---|---|---|
| support@signpath.io — [Support](https://signpath.io/support) | General SignPath service, operated by GmbH; email for first interactions or individual requests | Supports an initial general request. Foundation eligibility handling is not promised. A routing email is not the Foundation application; provider-side ticket handling is unknown | No SignPath login stated for email. **Primary, Option A first** |
| info@signpath.io — [Contact](https://signpath.io/contact) | Explicit SignPath GmbH headquarters/general contact; page also invites questions via form | General routing fallback by inference, not a confirmed Foundation reviewer address. Neither ordinary question nor contact form is labelled the Foundation application | Public email/form page, no login gate observed; full form submission process not tested. **Fallback email**, separate approval |
| [Support portal](https://support.signpath.io/) linked by [Support](https://signpath.io/support) | SignPath support system | No Foundation preliminary eligibility purpose established; ticket creation outside scope | Support page explicitly requires sign-up. **Do not use** for this preparation |
| [Foundation application](https://signpath.org/apply) | Explicit Foundation programme / free SignPath.io subscription application | Formal eligibility submission, with proposed account/contact details and required Code of Conduct/personal-data consent. **Not adopted for preliminary routing** | Public fields readable without login; later account process unknown. Submission/consent NOT AUTHORISED |
| support@signpath.io — [Foundation terms](https://signpath.org/terms.html) | Explicit Foundation certificate-policy violation reporting | This narrower reference supports violation reports, not preliminary eligibility review; do not infer reviewer access | Published email, no login stated; rely on Support for first-contact purpose |
| [Foundation home](https://signpath.org/) and [About](https://signpath.org/about) | Foundation sponsored signing; About says currently operated by SignPath GmbH | Home links Apply; no separate preliminary eligibility address found. Common operator supports asking GmbH to route, not assuming a Foundation decision | Public pages; no account needed to read |
| [Open-source community](https://signpath.io/solutions/open-source-community) | GmbH community offering, links Foundation via Join the community | No separate eligibility contact or preliminary process established | Public page; demo/marketing actions are not an eligibility enquiry |

The Foundation's current operation by GmbH does not prove either mailbox reaches
eligibility reviewers. **SIGNPATH_CONTACT_ROUTE_UNVERIFIED** remains appropriate.
No more specific published preliminary route was found in these inspected pages
or the bounded official-domain search; this is not an exhaustive absence claim.
Recommend the short routing email, then seek new owner approval if the recipient
or message changes. No guarantee of response, acceptance or absence of automatic
support-ticket creation is made.

The application fields now visible include project/repository/homepage/download/
privacy/reputation details, maintainer type (including individuals), build system,
proposed account name/email and required consent. The individual-maintainer choice
is evidence about the form, not approval for one person to hold all signing roles.
No fields were completed and no consent accepted. Earlier failure to retrieve
fields remains an accurate historical tool limitation.

## Revalidated eligibility matrix

The [official terms](https://signpath.org/terms.html) still label the Code of Conduct
Draft. Admission and interpretation belong to the Foundation. Public policy
publication corrects a documentation gap; it is not eligibility or release clearance.

| Subject | Existing evidence | Provider question / classification |
|---|---|---|
| Q1: one human in all responsibilities | Sole owner @DouglasNLima; enforced PR/CI/conversation controls, zero mandatory human approvals; documented self-review | Accept combined Author, external-contribution reviewer, manual signing approver and release approver? Required extra controls/evidence? **UNCONFIRMED**; no independent human review; MFA **UNVERIFIED** |
| Q2: released in intended form | Five historical legacy WinUI/WebView2 public releases; separate .NET 10/WPF edition has unsigned internal previews only | Can a new implementation of the published open-source product qualify; what evidence/process is needed? **BLOCKED / clarification required**. Legacy releases are not treated as satisfying the condition; no manufactured unsigned native release |
| Q3: all-component licensing and own binaries | Scoped R1/R2 MIT on both branches; R3–R7/mixed exclusions and actual upstream grants preserved; proposed two-stage boundary below | SDK/generated/runtime/fonts/adapted/embedded/Inno treatment needs explicit interpretation and clearance. **BLOCKED**; neither complete executable nor installer is MIT-cleared |
| Q4: identity and lifecycle | Separate RSA-PSS update manifest, exact origin/version/channel/asset/hash binding, Windows Authenticode and approved subject/fingerprint required; production trust empty | How obtain/independently verify subject, issuer, SHA-256 fingerprint, validity/renewal, revocation and planned rollover? **UNKNOWN** until accepted provider evidence; no guessed pins |
| Public signing/privacy/download guidance | #67 public on main/Pages and directly read back | **PUBLISHED**; no active provider benefit claimed. Installer policy display and persisted pre-first-launch update opt-out remain **BLOCKED**, including upgrade/silent paths |
| Reputation, maintenance, documentation | Public repository history, guides and source-bound CI | Observable maintenance, not provider reputation approval; **PROVIDER DECISION** |
| Build origin/metadata/manual approval | Two-stage design only; preview CI, recorded candidate SBOMs; no active trusted provider integration | Actual integration, final run/artefact/digest, common enforced metadata and each-request manual approval remain **BLOCKED** |
| Native certification | Current post-merge preview CI succeeded; independent Continuous 250 ms FAIL and prior arrangement/UIA failures retained | **NATIVE_STABLE_NO_GO**; clean Windows 10/11, physical accessibility/DPI, approved signer and real signed install/upgrade/rollover evidence outstanding |
| Confidential security intake | PVR enabled by API | **PVR_ENABLED_EXTERNAL_FLOW_UNVERIFIED**; no synthetic submission or notification/privacy/acknowledgement claim |

## Verified component and signing scope

Reinspected Native/Core project references/resources, Directory.Build.props and
package locks, Build-NativePackage.ps1, native Inno script, preserved notices,
[native distribution disclosure](native-distribution-licensing.md) and
[component closeout](third-party-closeout.md). These source inputs are unchanged
from #66. The .957 and .967 payload/SBOM inspections remain bound to their own
historical source/run; no new package, reproducibility or whole-payload clearance
is claimed here.

1. Stage 1 proposes only the approved publish root's **LensDocsStudio.Native.exe**.
   It is SDK-generated and embeds the application icon; filename ownership does
   not settle SDK/resource rights. Native/Core DLLs are not direct selectors.
2. Stage 2 proposes only **LensDocsStudio.Native-<approved-version>-win-x64-Setup.exe**,
   after independently verifying the signed apphost and packaging approved payload.
   Inno's generated uninstaller needs an accepted arrangement; the current script
   does not configure SignedUninstaller=yes. No broad EXE/DLL allowlist.

| Included or relevant material | Remaining eligibility / redistribution boundary |
|---|---|
| Self-contained .NET/WPF | Packaged runtime 10.0.13; preserve constituent licences, notices and existing signatures. No automatic System Libraries exception |
| WpfMath 2.1.0 / XamlMath fonts | Computer Modern cmex10/cmmi10/cmr10/cmsy10 Knuth copying/renaming conditions and jlm_msam10.ttf OFL/reserved names; not all MIT |
| Mermaider 0.15.1 and compiled adaptations | Pinned Mermaider/BoundedErLayout, Sugiyama/dagre attribution and adapted portions retain upstream terms. Provider modified-upstream/fork/signed-build conditions unestablished |
| Inno installer/uninstaller | Generated compiler/uninstaller code and embedded icon have distinct terms; original copyright/website notices and provider-supported coverage required |
| Icons/artwork/resources/samples | application.ico, CommandIcons.xaml/vector artwork, embedded catalogue/help/Docs Site templates and sample literals can reach binaries. R3/R4 exclusions unresolved for release; retain grants or clear/replace/exclude |
| Other upstream libraries | Preserve exact package bytes, licences and original signatures; no re-signing as project source. Top-level permissive metadata is not embedded closure proof |
| Web/legacy distributions | Not copied by native builder, but dingbat-to-unicode notice, Mammoth nested JSZip/polyfills and PDF.js worker closure unresolved for affected distributions |
| User/customer content | R7 remains outside approved signing inputs; imported metadata grants no rights |

This new review document is not added to the MIT grant manifest; no grant path or
R3–R7 exclusion changes. The canonical rights ledger records its ungranted status.
A prepared enquiry remains useful if eligibility is declined: preserve the gaps
and separately documented [fallback assessment](signpath-readiness.md#fallback-decision).
Do not relax trust, publish an unsigned native release or relabel exclusions.

## Owner decision and application separation

The [exact recipient, subject and body for both options](signpath-enquiry-draft.md)
are complete. **Approve Option A to support@signpath.io by email** is the single
next owner action recommended. This task grants no dispatch permission. A route
reply may enable a later exact-message approval, not a formal application.

Future application separately requires provider answers, rights/notice closure,
qualifying-distribution interpretation, real private MFA evidence, approved
roles/terms and contact details. Do not attach binaries or private evidence to
either preliminary message. Signing integration, production identities, trust
bootstrap/rollover and Stable certification require separate authority under
Goal #56. No account, ticket, form, upload, signing, tag or release was made.

Final commit/tree, checks, independent review, hosted outcomes, retries and
preservation readback are published as an engineering receipt on Goal #62.
That receipt binds actual final source; this pre-commit packet does not claim
future CI success. The new Draft PR must remain unmerged.
