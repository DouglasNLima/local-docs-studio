# Licensing and provenance audit — Goal #56

Historical ownership questions below predate the owner's confirmed personal
development/no employer or customer restrictions. See the current
[R1–R7 decision packet](rights-decision.md), which also explicitly excludes the
Mermaider-adapted BoundedErLayout.cs outside the PinnedMermaider folder.
The exact public copyright notice/scope remains unapproved; this audit is preserved.

This historical audit is supplemented by the [Goal #58 closeout](third-party-closeout.md):
Highlight.js is now exactly reconstructed, and known web notices plus a file-level
native candidate SBOM are supplied. Historical findings below retain their date.

Recorded 28 October 2026. **Technical audit delivered with blocking findings;
not legal clearance, licence adoption or production certification.**

Baseline commit `5c61d17966befee665856a7993cb174252df5214`, tree
`d995527c96141dca398a0adcff6c9d007edacde3`, from freshly fetched `origin/develop`.
The complete [tracked-file inventory](evidence/tracked-files.csv) records all
2,300 paths, Git object IDs, modes, byte counts, SHA-256 and scope categories.
It records an immutable baseline, not the later audit files themselves.

## Scope and ownership

| Category | Files | Rights assessment |
|---|---:|---|
| Native source/tests | 716 | Project-authored claim requires owner confirmation; NuGet grants do not establish first-party ownership |
| Web/project source | 72 | Same first-party confirmation; no root licence |
| Build/test/distribution | 73 | Source scripts plus external toolchain/action terms; no licence inferred from CI |
| Legacy shell source | 18 | First-party code separated from Microsoft binary terms |
| Documentation | 151 | Owner must approve code/documentation scope and copied material |
| Recorded evidence | 898 | Includes machine-readable records and screenshots; rights/privacy review remains required for visible content |
| Fixtures | 131 | Synthetic intent does not prove provenance; copied examples require attribution |
| Artwork/fixtures | 6 | Original icons/SVGs and generated native ICO; confirm ownership and logo/trademark scope |
| Adapted upstream | 3 | Pinned Mermaider notice identifies adaptation and MIT attribution |
| Vendored web assets | 232 | Separate upstream terms; exact/modified-byte comparison below |

Only one Git author name was observed (`DouglasNLima`). This is not evidence of
employment rights, assignments, originality or permission from customers. No
contributor/asset rights are fabricated. The native icon generator identifies
its SVG input and generates seven sizes; generation alone cannot clear input rights.
No tracked executable/ZIP is approved for signing merely because it is public.

## Dependency evidence and compatibility

[Inventory JSON](evidence/inventory.json) records 42 unique NuGet package/version
entries across four native locks, the MarkerReuse experiment, Core/WPF runtime
packs and two legacy direct packages. Thirty-eight have restored licence metadata;
three absent-cache packages have [separate registry metadata](evidence/additional-nuget-metadata.json).
`xunit.abstractions` 2.0.3 uses a legacy licence URL rather than an expression and
needs explicit upstream-version confirmation. Native restore in locked mode passed.
Archive SHA-256, raw `.sha512` sidecar consistency and NuGet cache `contentHash`
matching are separate fields: signed nupkg raw hashes are not compared as if they
were the lockfile content hash. Cache matching is not independent registry or
ownership verification.

The native production lock contains 18 NuGet packages plus two self-contained
runtime packs. The recorded MIT/BSD/Apache/OFL grants do not show a necessary
conflict with a first-party MIT proposal when their conditions are met. **This is
conditional**, especially for embedded fonts and bundled upstream notices. WpfMath's
`FONT-LICENSES.md` also specifies Knuth copying/renaming terms for Computer Modern,
beyond the NuGet expression. Provider acceptance of that font boundary is unknown.
Sugiyama carries dagre attribution; preserve it even though its declared licence
is MIT. Existing adapter attribution is retained unchanged.

The Windows App SDK 2.1.3 **binary package** contains Microsoft Software License
Terms, verified from the [exact archive](evidence/legacy-package-evidence.json).
Its GitHub source licence cannot be substituted for that package's terms. The
WebView2 SDK package includes a BSD-style notice; the installed browser runtime is
a separate component. Legacy transitive payload licensing is not certified by
the two direct package declarations. Keep this legacy product out of the proposed
native SignPath scope until separately assessed.

The audit identified 56 npm package/version entries: top-level web distributions,
source-map embedded dependencies and three test packages (some overlap). All 56
archives passed registry integrity/shasum checks and yielded preserved notices.
`khroma` 2.1.0 lacks a registry licence field, but its archive contains the MIT
licence and original copyright. Do not treat a missing metadata field as either
permission or incompatibility.

The [archive evidence](evidence/npm-archive-evidence.json) proves 148 of 232 vendor
files byte-for-byte against upstream npm archives. Another 78 match after changing
only quoted `.mjs` module paths to `.js`; record these as local modifications.
Four more files match after the same extension replacement also covers paths in
licence comments/sourceMappingURL: three Mermaid chunks (`3SSMPTDK`, `67TQ5CYL`,
`7FYTHRHK`) and DOMPurify 3.4.5. These are identified local modifications, not
unexplained code changes. The project-generated manifest is separate.

Highlight.js 11.11.1 remains outside the reconstruction. Its header identifies
`highlight.js` (not the separate `@highlightjs/cdn-assets` package), jsDelivr,
Rollup 2.79.2 and Terser 5.39.0. The [observed versioned `+esm` response](evidence/highlight-cdn-comparison.json)
differs from the vendored bytes, so a live CDN fetch is not immutable build provenance. The
collector uses the actual `highlight.js` package for licence evidence. Recover the
exact bundling recipe and embedded notices before certifying this file.
Source maps alone do not prove a complete Mammoth/PDF.js/other bundle
transitive closure. No vendor bytes were replaced during this audit.

## Redistribution findings

The five public legacy ZIPs were downloaded and inspected without execution.
They contain 347/359/374/380/394 files and no standalone licence/notice-named paths.
Embedded headers may still exist; this is a missing-notice-pack finding, not a
claim that all copyright notices are absent. Do not replace historical assets.
Prepare a separately approved remediation for future distributions and any
historical correction notice.

Native baseline `.953` packages contain 529 files each, with 13 notice-related
paths. Their notice writer omitted `THIRD-PARTY-NOTICES` names and runtime packs
outside `project.assets.json` libraries. **Corrected in this proposal:** include
hyphenated/underscored names, explicit licence files and exact published Core/WPF
runtime packs. Missing declared licences stop packaging. Synthetic and actual
package-notice checks are recorded in the readiness receipt. This changes notice
collection only; installer identity, trust and unsigned-preview workflows stay
unchanged. Future candidate payloads need independent inspection after CI.

The preserved [upstream notice index](evidence/upstream-notices/index.json) contains
88 component/notice references and 59 distinct original texts. These are audit
evidence, not a certification that every distributable/export includes every notice.
Generate a file-level binary SBOM and verify all redistribution paths before Stable.

## Privacy and credential screening

The local all-ref scan covered 7,819 Git blobs (470,786,988 bytes). Five high-confidence
patterns found two blobs containing credential-shaped URLs: signing-adapter and
remote-image negative tests using example hosts. Their fixtures were inspected;
no live secret was identified. Findings retain only rule/object IDs, never values.
Unfetched refs, Git LFS, unknown credential formats and visual/OCR contents are not
covered. Public GitHub secret scanning/push protection were enabled; that is not
proof of absence. No history was rewritten.

All seven inspected ZIPs had zero hits for the distribution scanner's three
high-confidence patterns or sensitive key-file extensions. Members were hashed
without extraction/execution. Compressed executable internals, nested archives,
screenshots/customer provenance and arbitrary ignored local artefacts remain
uncertified. Do not describe this as a comprehensive secret or malware audit.
Historical records and synthetic fixtures are preserved, not silently sanitised.

## Decision gates

1. Owner confirms rights over first-party code/docs/examples/artwork and scope for
   logos/trademarks and user content; approve exact MIT holder/year text only then.
2. Close web transformation/embedded-notice gaps and legacy redistribution review;
   preserve upstream grants instead of applying MIT to everything.
3. Verify font/system-library and adapted-source boundaries with SignPath, including
   the non-OSI binary package distinction for the excluded legacy shell.
4. Approve a confidential reporting channel, roles/MFA and protected release controls.
5. Re-audit the exact final release source and binary SBOM after remediation.

See [MIT proposal and boundaries](licence-proposal.md), [third-party notices](../../THIRD_PARTY_NOTICES.md)
and [eligibility matrix](signpath-readiness.md). **NO-GO for a blanket licence or
public signed Stable claim; ready for owner review of this bounded proposal.**
