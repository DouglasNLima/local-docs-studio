# SignPath eligibility assessment — 29 October 2026

**SIGNPATH_ELIGIBILITY_UNCONFIRMED.** This is an engineering assessment for
[Goal #62](https://github.com/DouglasNLima/local-docs-studio/issues/62), not an
application, legal clearance or provider acceptance. The Foundation terms identify
their code of conduct as **Draft**. Interpretation and project admission belong to
the Foundation.

## Verified baseline and source boundary

Read-only Git/API checks on 29 October 2026 matched main
`68f1dc955b27a47347a6fccc52a463472b601a1b` and develop
`8fdcb5eba90347a829e88e8f30e4ab489b437038`. PR #64 merged to develop; PR #65
merged to main. [Pages run 40935627548](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40935627548)
and its deployment job completed successfully on the main SHA. Public main now
contains scoped MIT, LICENSING.md and SECURITY.md. It has an older browser/legacy
shell tree: native WPF source has not been promoted.

Production rulesets [26127883](https://github.com/DouglasNLima/local-docs-studio/rules/26127883)
and [26127901](https://github.com/DouglasNLima/local-docs-studio/rules/26127901)
were active with empty bypass lists, PRs, zero mandatory human approvals, resolved
conversations, strict checks and deletion/force-push restrictions. Develop requires
30 contexts; main requires Test static app and OSS provenance, all app 15368.
Only @DouglasNLima appeared in the direct-collaborator readback, with admin access;
the recorded owner declaration confirms one owner/maintainer. Neither account MFA
nor external PVR delivery was tested. PVR returned enabled:true. Only github-pages
was listed as an environment and no repository Actions secret names were returned.
These checks and the source/owner record support the stated absence of a production
signing setup; they cannot independently inspect an unconnected provider account.

The five public v0.1.0-dev* releases are the separate legacy WinUI/WebView2 shell.
Native .NET 10/WPF outputs remain unsigned internal previews. There is no public
native WPF release, recorded SignPath application, acceptance, certificate or
signing request. No production ref, ruleset, tag, release or unrelated draft changed.

## Official sources and applicable conditions

Accessed **29 October 2026** using read-only page retrieval:

- [Foundation terms](https://signpath.org/terms) (canonical metadata /terms.html):
  Draft code of conduct; OSI licensing of all components without commercial
  dual-licensing; no proprietary components, with an express System Libraries
  exception referring to GPLv3 section 1. It also covers maintained/released/
  documented software, own binaries, upstream/fork conditions, privacy,
  installation, roles, MFA, policies, metadata and manual approval.
- [Application](https://signpath.org/apply): a subscription application with an
  embedded HubSpot form, not an established preliminary-enquiry facility.
- [Foundation home](https://signpath.org/): describes sponsored signing; this is
  not a promise that this project qualifies.
- [Documentation index](https://docs.signpath.io/): links project, certificate,
  signing and origin-verification documentation; technical facilities do not
  override the Foundation admission conditions.
- [Trusted GitHub builds](https://docs.signpath.io/trusted-build-systems/github):
  origin supplied by GitHub, workflow artefact stored before submission and
  GitHub-hosted preceding OSS jobs; configured provider integration is required.

The matrix paraphrases conditions; component-specific conclusions are deliberately
separate from permissive package declarations. VERIFIED means the cited project
fact is evidenced, never that the Foundation has accepted it.

| Requirement | Classification | Evidence and unresolved boundary |
|---|---|---|
| OSI-approved licensing of all eligible components | PARTIALLY VERIFIED | Original enumerated R1/R2 portions have MIT on both branches; [LICENSING.md](../../LICENSING.md). R3–R7 and mixed material have separate rights; no whole-payload clearance |
| No proprietary components; only express exception | BLOCKED | Excluded embedded icons, artwork and sample content have no established eligible grant here. No evidence that they or every runtime qualify as System Libraries. Separate permission alone may still fail the OSS condition |
| Active maintenance | VERIFIED | Current source, merged PRs #64/#65 and dated CI show activity; this is no reputation certification |
| Verifiable project reputation/control | PROVIDER CLARIFICATION REQUIRED | Public history/control evidence supplied; Foundation alone evaluates reputation and admission |
| Already publicly released in intended form | BLOCKED | No native WPF public release. Five legacy-shell releases and internal Actions artefacts do not satisfy this fact |
| Public functionality and download description | PARTIALLY VERIFIED | README/native guides describe functionality; public native download entry is missing; current download guidance must distinguish editions |
| Only project-owned signing targets | PROVIDER CLARIFICATION REQUIRED | Proposed apphost then final Inno installer; embedded SDK/icon/generated material means filename ownership alone is insufficient |
| Upstream OSS DLL inclusion/original signatures | PARTIALLY VERIFIED | Terms allow unsigned upstream OSS DLLs in packages, not signing upstream as this project. Preserve exact bytes and original signatures; final candidate still needs inspection |
| Modified upstream/fork conditions | PROVIDER CLARIFICATION REQUIRED | Pinned Mermaider, BoundedErLayout, Sugiyama/dagre attribution is retained; signed-upstream, visible-fork and signed-branch conditions are not established for compiled adaptations |
| Runtimes, fonts, embedded assets and installer | BLOCKED | Payload analysis below; package-level MIT and first-party exclusions do not clear constituent rights |
| Named Authors/Reviewers/Approvers | PARTIALLY VERIFIED | @DouglasNLima proposed for all three; external contributions reviewed by that human; self-review is not independent review |
| Same person in all roles | PROVIDER CLARIFICATION REQUIRED | Terms define responsibilities without an explicit different-person rule; they do not promise single-maintainer acceptance |
| Repository and provider MFA | UNKNOWN | GitHub status unverified; no SignPath account established; no account/security change authorised |
| Public Code signing policy | PARTIALLY VERIFIED | Reviewed draft plus separate main PR; heading/roles/privacy link/conditional credit prepared; merge/Pages publication require owner approval |
| Public Privacy policy and affected services | PARTIALLY VERIFIED | [Source audit](network-privacy-audit.md) and corrected draft; publication and installer presentation pending |
| Installation disclosure and disable option | BLOCKED | Native automatic checks default on; installer lacks privacy presentation and pre-first-launch disable control |
| Warn about system changes and provide uninstall | PARTIALLY VERIFIED | Native/legacy Inno scripts identify per-user installation, shortcuts, unchecked associations and uninstall; clean Windows/user comprehension certification outstanding |
| Trusted GitHub origin and immutable artefacts | PARTIALLY VERIFIED | Hosted preview provenance exists; [adapter design](signpath-adapter-plan.md) is not deployed provider configuration. Require exact run/attempt/artefact ID/digests and accepted origin policy |
| Metadata and common product/version restrictions | BLOCKED | Preview product labels differ; no provider-enforced metadata rules. Version JSON or passing preview checks is insufficient |
| Manual approval of every signing request | BLOCKED | Required in design; no signing policy/request/approver configured. Eventual each-request human decision is mandatory |
| Certificate identity and trust verification | UNKNOWN | Foundation would be publisher; subject, issuer, SHA-256 fingerprint and validity are unknown. Do not guess pins |
| Revocation, renewal and rollover | PROVIDER CLARIFICATION REQUIRED | Foundation may suspend/revoke; [trust lifecycle](trust-lifecycle.md) needs actual provider evidence, bridge and failure tests alongside independent RSA-PSS manifests |
| No malware/unwanted behaviour and investigate complaints | PARTIALLY VERIFIED | Documentation-studio purpose/source/tests available; no exhaustive safety certification or provider assessment claimed |
| Enquiry route and application boundary | UNKNOWN | [Contact review](signpath-owner-decisions.md#contact-route); official general questions channel exists, Foundation pre-application handling remains unverified |

## Actual proposed payload and rights decisions

Inspected current Native/Core project files, Directory.Build.props, locked package
references, [package builder](../../scripts/native/Build-NativePackage.ps1),
[native installer](../../installer/inno/LensDocsStudio.Native.iss), signing adapter,
[component closeout](third-party-closeout.md), original notices, provenance records
and [historical per-file SBOM](evidence/goal58/native-preview-957-sbom.json).

The .957 SBOM is source `22d434a923a03e86ebee237e71c55ef89dd996ed`,
tree `133cc2e59515aec1e44691c0e2333aa4f47d3ef4`, run 40759577875:
537 portable files, 522 exact registry-member matches across 20 archives, 21 notice
files. It is historical evidence, not the current develop candidate's clearance.
[Two installer container hashes](evidence/goal58/installer-containers.json) and
same-run installed parity do not establish independent container decompression.

The actual post-#64 baseline candidate was also downloaded from run 40932816467,
artefact 12925830225, version 0.1.0-preview.967. Every one of **540** portable
file lengths/hashes matched its supplied SBOM; 522 have upstream matches, 20
components and 22 notice paths. Portable SHA-256:
`c350c3306d4ea27aa4ac0c026bc3c658f17d4aa75d9934d636346876dd76aaef`;
installer SHA-256:
`e6664bb9019193f68f64eff90a74f1ad3af4b3281afae7f1f83f2ccc8fbbc649`.
[Inspection receipt](evidence/goal62/signpath-baseline-candidate-2026-10-29.json)
binds source/tree and limitations. Windows metadata inspection found ProductName
LensDocsStudio.Native, ProductVersion 0.1.0-preview.967 plus the source SHA,
FileVersion 0.1.0.0 and OriginalFilename LensDocsStudio.Native.dll; Authenticode
reported NotSigned. No payload was executed. The installer ProductName remains
Lens Docs Studio Native Preview. These are observed preview attributes, not an
accepted common product/version restriction or native release certification.

| Material which can reach signed output | Actual inclusion / needed disposition |
|---|---|
| SDK-generated LensDocsStudio.Native.exe | Stage 1 proposal. SDK apphost template plus project resource/icon; existing CI-output classification does not prove SDK origin/rights. Inspect exact SDK template and PE metadata on final source; ask provider about signing this generated boundary |
| Native and Core assemblies | Included alongside apphost; deliberately outside direct signing selectors. Original portions MIT, but Core embeds R3 catalogue, Docs Site templates and adapted rendering; Native carries visual resources. Exclusion from direct signing does not exempt installer contents |
| Self-contained .NET/WPF | Builder pins runtime 10.0.13, self-contained win-x64; .NETCore/WindowsDesktop DLLs, native runtime files and notices are shipped. Keep registry-byte matches and original signatures; evaluate constituent grants. No automatic System Libraries exception asserted |
| WpfMath/XamlMath fonts | DLL resources can include Computer Modern cmex10/cmmi10/cmr10/cmsy10 under Knuth copying/renaming conditions, and jlm_msam10.ttf under OFL-1.1/reserved names. Preserve font notices. Require provider interpretation or eligible replacement/exclusion; top-level package MIT is insufficient |
| Mermaider, Sugiyama, dagre | Pinned/adapted code compiled into Core plus upstream packages. Retain original MIT/adaptation and dagre notices. Determine compliance with provider modified-upstream conditions, or obtain explicit interpretation/restructure scope |
| Inno-generated installer/uninstaller | Stage 2 proposed versioned Setup.exe wraps verified signed apphost and all payload files. Inno compiler code, uninstaller and embedded icon are not project-authored MIT. Confirm generated-code arrangement and signing coverage with provider; no broad EXE/DLL selector |
| application.ico and CommandIcons.xaml/vector artwork | ApplicationIcon/Resource and SetupIconFile, WPF resource inclusion: potentially in both stages. R4 excluded. Obtain an eligible open grant with copyright/branding review, replace, or exclude before eligibility can be asserted |
| Built-in catalogue, embedded samples/templates/help | Core EmbeddedResource items and WPF/application literals can reach assemblies; Docs Site templates are resources, test-only fixtures are not automatically shipped. Review each actual sample/asset and grant; R3 exclusions are unresolved, not proof of public-domain or OSS rights |
| Other upstream DLLs | Preserve original package bytes/notices/signatures. Declared MIT/BSD/Apache expressions and registry matches are evidence, not full embedded closure or permission to re-sign upstream |
| Browser vendor libraries / legacy shell | Not copied by the native package builder; remain relevant to public web/legacy distribution. Do not misstate browser gaps as proof of native inclusion or erase them from project readiness |
| User/imported/customer content | Not an approved signing input. Keep R7 outside source/release artefacts; imported metadata never grants rights |

The outstanding original-notice gaps remain **BLOCKED**:
`dingbat-to-unicode@1.0.1`, Mammoth's nested JSZip/polyfill closure, and incomplete
embedded PDF.js worker notice closure. No missing notices were invented, no vendor
bytes changed and no complete redistribution clearance is claimed.

Stage 1 remains **only LensDocsStudio.Native.exe** from the approved publish root.
Stage 2 remains **only LensDocsStudio.Native-<approved-version>-win-x64-Setup.exe**
after rebuilding around and verifying the signed apphost. These are proposals,
not accepted configurations. Inno-generated uninstaller treatment remains open.
Native Stable packaging and real signed-install/upgrade certification are separate.

See the [owner decision matrix](signpath-owner-decisions.md), [unsent exact message](signpath-enquiry-draft.md)
and [engineering receipt](goal62-signpath-policy-review.md).
