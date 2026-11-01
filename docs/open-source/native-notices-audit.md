# Goal #56 native notices and distribution audit

Assessment: **NATIVE_NOTICES_AUDIT_PARTIAL**. Independently demonstrated notice
omissions justify the bounded packaging correction. The eight PDFsharp sample
fonts and frozen R3/R4 exclusions prevent complete redistribution clearance.
No signing, merge, tag, public release or deployment is authorised.

## Reviewed baseline and prerequisite

Fresh protected develop: `ab21919a42ec8b3d17d9ba2e39b4e7e41459e206`, tree
`f74345a61d0e9320a8826da8cb2ca3478ee3125e`.
Its canonical `git archive --format=tar` SHA-256 is
`f465c93b3bfd07c599850912cd9efd6f8ef742749afd9e6c50964d388ca5a545`.
The [original terminal CI observation](evidence/native-notices/prerequisite-ci.json)
retains every job, including skips. All three runs completed on attempt 1:

| Run | Workflow | Terminal result | Jobs |
|---|---|---|---|
| 41334320896 | Web/static | SUCCESS | 3 success, 1 skipped |
| 41334320982 | OSS provenance | SUCCESS | 1 success |
| 41334321013 | Native Windows | SUCCESS | 30 success, 2 skipped |

The local baseline was built unsigned from that clean commit with
`Build-NativePackage.ps1 -Version 0.1.0-preview.1101`. It was neither installed
over the owner's installation nor published.

| Actual baseline package | SHA-256 |
|---|---|
| LensDocsStudio.Native-0.1.0-preview.1101-win-x64-Setup.exe | fe840cd43e843ecc3307e93fcf348a080c0148349c976d6990f68856dc8bc915 |
| LensDocsStudio.Native-0.1.0-preview.1101-win-x64.zip | d22fc7c5c93cdfe1b9d9b1e1d6e0f982a1faaf9a468428457a1c2799deacdc95 |

The [fresh baseline SBOM](evidence/native-notices/baseline-sbom.json) enumerates
all **540 files**, including **522 exact archive-member matches**, and independently
re-fetches all **20 official NuGet/runtime archives**. The [package verification](evidence/native-notices/baseline-package-verification.json)
binds source/tree, unsigned identity, scoped first-party licence, runtime, binary
versions, apphost/installer icon identity and all payload hashes. It performs no
installation or execution. The final reviewed commit/tree, freshly rebuilt package
identities, hosted lifecycle results and independent review are recorded in the
engineering receipt under [Goal #56](https://github.com/DouglasNLima/local-docs-studio/issues/56).
These immutable baseline files are not represented as final corrected packages.

## Complete component inventory and demonstrated defects

The [component findings](evidence/native-notices/component-findings.json) record
the baseline classification and exact archive hash for each component. The
[supplement manifest](../../docs/native/licences/native-notice-supplements.json)
binds the same 20 archives and 24 original source-text references. It contains 16
unique preserved texts, including one already retained in the historical notice
evidence. There is no dependency upgrade or binary removal.

| Redistributed package | Version | Notice treatment / remaining concern |
|---|---|---|
| AngleSharp | 1.8.4 | Exact primary MIT attribution added |
| AvalonEdit | 6.3.1.120 | Exact primary MIT notice adds AvalonEdit Contributors attribution |
| CommunityToolkit.Mvvm | 8.4.2 | Original package License.md and ThirdPartyNotices.txt retained |
| DocumentFormat.OpenXml / Framework | 3.5.1 | Both original source LICENSE and NOTICE added to both companion identities |
| Markdig | 1.4.0 | Original BSD notice restores Copyright (c) 2016-2026, Alexandre Mutel |
| Mermaider / Sugiyama | 0.15.1 | Primary MIT and dagre notice retained; existing adapted ER notice unchanged |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | Package licence and third-party notices retained |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | Package licence and third-party notices retained |
| Microsoft.Extensions.ObjectPool | 10.0.3 | Package third-party notice retained; exact source primary MIT notice added |
| PDFsharp-WPF / PDFsharp-MigraDoc-WPF | 6.2.4 | Exact version-tagged primary MIT and BigGustave Unlicense added; sample fonts remain under review |
| PdfPig | 0.1.16 | Full upstream LICENSE, NOTICES.txt, original Adobe glyph resources and AFM accompanying file added |
| SharpVectors.Wpf | 1.8.6 | Package lib/License.txt, including Brotli/MinIoC attribution, retained |
| System.IO.Hashing | 10.0.12 | Package third-party notice retained; exact source primary MIT notice added |
| WpfMath / XamlMath.Shared | 2.1.0 | Primary MIT notice added; original FONT-LICENSES.md and OFL reference retained |
| Microsoft.NETCore.App.Runtime.win-x64 | 10.0.13 | Exact published pack LICENSE.TXT and THIRD-PARTY-NOTICES.TXT retained |
| Microsoft.WindowsDesktop.App.Runtime.win-x64 | 10.0.13 | Exact published pack LICENSE retained |

`NOTICE_MISSING` is demonstrated for PdfPig's upstream notices. Its complete
LICENSE contains external PDFBox BSD conditions, the Adobe Core 14 AFM grant and
Adobe CMap conditions, absent from the generic Apache reference. The original
Adobe glyph-list files retain their full copyright/conditions/disclaimer.
The accompanying AFM HTML bytes are delivered as a `.txt` notice so that no active
HTML asset is introduced. `NOTICE_INCOMPLETE` covers the demonstrated primary
attribution differences; other exact primary licences are included conservatively.
This is not an opinion that every previous metadata-plus-reference combination
necessarily infringed a licence.

Original source notices are fetched during review and checked into the existing
byte-preserving evidence directory. Packaging makes no upstream requests. It
rejects missing/changed dependency identities, archive hashes or notice bytes,
unsupported runtimes, duplicate identities, invalid paths and repeated appending.
The original writer, its five negative/retention controls and standard SPDX texts
are unchanged. Nine separate supplement controls cover byte preservation and
failures, including BOM/mixed-line-ending retention. A separate read-only CI
workflow exercises both sets of controls; existing CI gates remain unchanged.

The source URLs are evidence, not runtime integrations. Supplemented notices are
linked from installed/portable `THIRD-PARTY-NOTICES.md` to `licences/` and the exact
manifest is delivered as `licences/native-notice-supplements.json`. Existing
hosted installer lifecycle tests enumerate and hash every installed file against
the ZIP after installation/upgrade; reading those files establishes access by the
installed test user. No new application command or installation choice is added.

## Embedded assets and unresolved rights

The independent metadata-only [resource inventory](evidence/native-notices/baseline-1101-resources.json)
enumerates 708 manifest resources across all managed payload assemblies;
the complete external file inventory additionally covers .NET/WPF assemblies and
satellites. Resource enumeration does not load or execute the distributed code.
Every non-runtime third-party DLL maps to an official archive member in the SBOM.

| Payload material | Supported classification | Evidence / limits |
|---|---|---|
| WpfMath five TTF resources | VERIFIED_COMPLETE for retained notices and package identity | Computer Modern cmex10/cmmi10/cmr10/cmsy10 retain Knuth copying/renaming conditions; jlm_msam10 retains AMS/OFL reserved-name terms. No project font modification. Provider eligibility interpretation remains separate |
| XamlMath mathematics XML datasets | VERIFIED_COMPLETE for primary attribution and package identity | Six embedded datasets; no additional font resource in this assembly |
| PdfPig AFMs, glyph lists and 92 CMaps | NOTICE_MISSING in baseline; supplied by correction | Full upstream external terms and original Adobe texts; exact assembly/resource hashes retained |
| PdfPig / PDFsharp sRGB2014.icc | VERIFIED_COMPLETE for supplier identity and retained embedded attribution | Copyright ICC 2015; profile terms remain distinct from Apache/MIT. [ICC profile terms](https://registry.color.org/profile-library/) and [sRGB2014 identification](https://registry.color.org/rgb-registry/srgbprofiles) require a separate interpretation for modified profiles |
| PDFsharp six Segoe WP fonts | REDISTRIBUTION_REVIEW_REQUIRED | Microsoft 2010 copyright; name-table terms refer to the originating product EULA. No general application redistribution grant established |
| PDFsharp Oblivious.ttf and xfiles.ttf | REDISTRIBUTION_REVIEW_REQUIRED | Oblivious contains PizzaDude/free-distribution wording; xfiles lacks a clear embedded grant. No invented author/grant |
| SharpVectors embedded schemas/DTD | VERIFIED_COMPLETE for retained notices and exact package bytes | W3C notice retained inside SVG DTD; no demonstrated stripping |
| Native application/installer icon and command artwork | REDISTRIBUTION_REVIEW_REQUIRED | Frozen R4 owner decision unchanged; original-looking source labels do not expand rights |
| Built-in catalogue/examples/templates/snippets | REDISTRIBUTION_REVIEW_REQUIRED | Frozen R3 exclusions unchanged; embedded in first-party Core assembly |
| First-party assemblies/apphost | REDISTRIBUTION_REVIEW_REQUIRED for their excluded resources | Existing original-portion MIT applies only within the frozen scope. Apphost includes upstream .NET code. No independent deterministic binary rebuild is claimed |
| Inno installer/uninstaller code | PROVENANCE_UNCONFIRMED for independent source-to-binary proof | Local build log observes Inno 6.7.3; hosted compiler identity is recorded separately. Upstream copyright/website requirements remain. Exact installer hash and actual hosted lifecycle/parity are distinct from compiler reproducibility or wholly first-party MIT |

The [font name-table evidence](evidence/native-notices/pdfsharp-font-names.json)
and [supplier asset comparison](evidence/native-notices/pdfsharp-asset-match.json)
identify all eight PDFsharp font resources. Every resource exactly matches the
supplier asset ZIP observed on 1 November 2026, SHA-256
`2e8c5049b8b8acfcc3e02687deddb3b8eb79b211d9cdf26366406f721778dcc6`.
The URL is mutable and the archive has no licence/notice/readme-named grant.
This improves provenance without supplying legal permission. No infringement
conclusion, dependency removal or font replacement is inferred.

The existing SBOM's 20 package/runtime identities omit the patched apphost's
template identity. The [companion apphost record](evidence/native-notices/apphost-origin.json)
independently verifies official `Microsoft.NETCore.App.Host.win-x64/10.0.13`,
archive SHA-256 `503712375103bd93aa6ab51d1b64c134ac131e002540ec818b447dfe72c52174`.
Both original host notice texts exactly match the runtime notices already shipped.
The SDK patches the template; no complete-file equality or independent upstream
rebuild is claimed. ASP.NET download dependencies are not claimed as shipped.

Mammoth JavaScript and `dingbat-to-unicode@1.0.1` are absent from this native
distribution. Native DOCX import uses Open XML and native diagrams use the pinned
Mermaider/Sugiyama path. The existing [Mammoth gap](evidence/goal58/mammoth-notice-gap.json),
web notice pack and all legacy release bytes remain unchanged and unresolved for
their own surfaces. A native notice correction does not close web bundle rights.

## Scope, verification and owner boundary

`LICENSE`, `mit-scope.json`, the original native licence texts, vendor notices,
adapted source, application/installer behaviour, update trust, signing controls
and the 250 ms benchmark are unchanged. The current rights ledger is regenerated
only for paths/bytes; all existing ownership decisions remain. New helper/test/CI
paths stay ungranted under the frozen allowlist, rather than silently extending
first-party MIT to new work. Upstream snapshots remain excluded evidence.

The independent reviewer re-downloaded all 17 unique source URLs and compared
them with the declared hashes and local bytes; [original comparison](evidence/native-notices/supplement-source-comparison.json).
The final issue receipt records the independent review of the final source,
actual corrected package contents, CI and engineering claims. This is technical
agent review, not human legal advice or GitHub approval.

The [preliminary local failures](evidence/native-notices/preliminary-checks.json)
remain explicit: initial ledger regeneration was pending, and a proposed new
`.gitattributes` rule violated protected bytes. That rule was reverted; the
existing evidence directory already provides exact byte retention. Neither
failure was suppressed by weakening a check or changing branch protection.

Required final validation: notice controls, unchanged licence/provenance/governance
checks, full fresh portable SBOM with official-registry downloads, package
verification/negative controls, actual hosted installed lifecycle and ZIP parity,
and terminal applicable hosted CI. Raw packages, logs and archive downloads are
retained outside source; their identities and evidence hashes belong in the final
receipt. Original failures, negative controls and expected skips remain visible.

SDK-free Windows 10/11, signed A-to-B upgrade, production Authenticode/revocation,
physical screen-reader/DPI and independent upstream reproducible-build
certification remain NOT TESTED/BLOCKED. Installer privacy work in other Drafts
is outside this audit. Public native distribution remains blocked by the recorded
font/R3/R4 questions and existing release gates.

Integration recommendation: review this notice-only correction for develop;
do not infer Stable acceptance. Preserve **NATIVE_CONTINUOUS_250MS_FAIL**,
**SIGNPATH_ELIGIBILITY_UNCONFIRMED** and **NATIVE_STABLE_NO_GO**. The owner must
decide the remaining rights/provider/release questions. No automatic merge or
other publication step follows this receipt.
