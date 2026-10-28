# Third-party provenance and distribution closeout

This supplements the immutable Goal #56 audit. It records technical provenance,
not copyright clearance or a blanket licence-compatibility opinion.

## Highlight.js: exact reconstruction resolved

The [maintenance recipe](../../scripts/compliance/highlight/README.md) reconstructs
the original asset without replacing or upgrading it.
Its **955,998 canonical Git bytes** were reconstructed from Highlight.js 11.11.1,
Terser 5.39.0 and CommonJS 22.0.2 (`strictRequires: false`). The initial Rollup
2.79.2/node-resolve trial matched; the final recipe uses patched Rollup 2.80.0 and
no node-resolve/deepmerge, still with byte-identical output. The historical header
remains original metadata, not a claim that the final maintenance tools are the old
CDN toolchain. [Build evidence](evidence/goal58/highlight-reconstruction.json)
records complete SHA-256 `980aa9a979a537d5f05c76d313c5f8b48f0bccba293a75e3c271975fce968aa0`.

The [upstream verification](evidence/goal58/highlight-upstream.json) checks npm
SHA-512 integrity, archive SHA-256, all 1,559 installed package files, and all 194
upstream sources in the historical map. The extra map source is a CDN header.
The minified body matches exactly; no semantic differences or language/API changes
were introduced. The historical map reference remains a documented debugging-path
limitation. Embedded build attribution alone did not supply the BSD licence; the
original grant is now also in the cached web notice pack.

## Distribution evidence and remaining obligations

| Component / surface | Result and remaining obligation |
|---|---|
| Mermaid 11.15.0 and embedded packages | Prior archive evidence identifies exact bytes and `.mjs`→`.js` transformations. Preserve MIT/ISC/BSD/Apache component notices and modification descriptions; the new known-notice pack retains all indexed web grants |
| DOMPurify 3.4.5 | The extension-only change is identified. Preserve the upstream MPL-2.0 OR Apache-2.0 terms; no blanket first-party MIT or unsupported dual-licence choice is applied |
| Highlight.js 11.11.1 | Exact reconstruction above; BSD-3-Clause grant retained. No runtime bytes changed |
| Mammoth 1.12.0 | Vendored minified bytes match the official archive. Its unminified companion declares 14 exact modules/versions, and 14 original notice references were recovered for 13 of them (including isarray's README licence section). The minified file itself has no comments. Supplier declarations are useful attribution, not an independent complete rebuild. `dingbat-to-unicode@1.0.1` declares BSD-2-Clause but the inspected archive and version-tagged source contain no original notice text. Nested JSZip/polyfill closure also remains BLOCKED; do not invent a copyright holder or claim licence incompatibility from missing evidence |
| PDF.js 5.7.284 | Main and worker bytes match the archive. Preserve its Apache grant and indexed upstream subcomponent notices. The repository does not ship the package's optional `cmaps`, `iccs`, `standard_fonts` or `wasm` directories, so those must not be claimed as shipped binaries. The conservative notice pack retains their upstream texts without claiming inclusion. Full embedded-worker closure and any future optional resources require a payload-specific review |
| WpfMath 2.1.0 / XamlMath | Exact shipped DLL bytes map to NuGet. Retain `FONT-LICENSES.md`: `cmex10`, `cmmi10`, `cmr10`, `cmsy10` use Knuth copying/renaming conditions; `jlm_msam10.ttf` has OFL-1.1 and reserved-name conditions from the pinned JLaTeXMath source. No font modification or renaming occurred. Provider interpretation remains BLOCKED; do not call all fonts MIT |
| Mermaider / Sugiyama / dagre | Preserve the pinned adapter's upstream MIT notice and adaptation description, plus Sugiyama's dagre attribution. A package's MIT expression does not replace that embedded notice |
| .NET Core/WPF 10.0.13 | Self-contained shipped files are mapped by exact package member hashes. Preserve both runtime packs' original licences and third-party notices. Hosted SDK/runtime versions are distinct from packaged runtime 10.0.13 |
| Inno Setup 6.7.1 | Exact compiler version observed in CI. [Pinned licence](https://github.com/jrsoftware/issrc/blob/cfdf48923178df4b4f040e038b423aa555a61ffc/license.txt) requires preservation of source notices and embedded binary copyright/website addresses; documentation acknowledgement is appreciated, not mandatory. Do not label installer code MIT. Confirm the generated uninstaller/signing boundary and embedded notice presentation in the future disposable signing validation |
| Legacy Windows shell | Windows App SDK binary terms and separate WebView2 runtime terms remain distinct. Excluded from the native SignPath scope; no whole-binary OSI assertion |

The new [web notice pack](../../assets/vendor/THIRD-PARTY-NOTICES.txt) preserves
68 original texts/references for 65 known runtime components, with hashes in
[its manifest](evidence/goal58/web-notice-pack.json). It explicitly does **not**
claim complete Mammoth/embedded-bundle closure. The [supplier evidence](evidence/goal58/mammoth-supplier-evidence.json)
pins the npm archive, both browser bundle hashes, declared modules and their verified
package archives. The [remaining notice gap](evidence/goal58/mammoth-notice-gap.json)
records the exact `dingbat-to-unicode` tag/commit and inspected paths. Its upstream
source lock is not substituted for payload proof. The vendor cache manifest includes
it and the service-worker cache version changes to v71, so offline app copies
receive the notices. This does not change rendering code. Exported user Markdown
and documents retain their own rights; the web Docs Site export emits pre-rendered
content and its own navigation scripts, not copies of all these vendor libraries.
Font/image/captured-content obligations in specific exports remain payload-specific.

## File-level native binary SBOM

[Candidate .957 SBOM](evidence/goal58/native-preview-957-sbom.json) enumerates every
one of 537 portable files and its SHA-256. **522** match exact members of **20**
NuGet/runtime archives independently downloaded from the official registry.
The remaining 15 are three project binaries, metadata, guides and separate notices.
All 21 notice files are enumerated. Declared component licences are separate from
file-level `NOASSERTION`; exact package bytes do not prove independent upstream
source-to-binary reproducibility or first-party copyright title.

The candidate is the verified PR #57 post-merge source
`22d434a923a03e86ebee237e71c55ef89dd996ed`, tree
`133cc2e59515aec1e44691c0e2333aa4f47d3ef4`, run 40759577875, unsigned internal preview.
This baseline candidate must not be represented as a later Goal #58 binary.
[Installer container hashes](evidence/goal58/installer-containers.json) identify both
same-run installers; installed payload-parity CI complements, but does not replace,
the portable file table. No independent Inno container decompression is claimed.
Use `scripts/compliance/binary_sbom.py --fetch-registry` on a later candidate before
signing or publication. Reproduction instructions and final CI belong in the receipt.

Five historical legacy release ZIPs still lack standalone notice-named files.
Their tags/assets/bytes remain unchanged. OWNER ACTION REQUIRED: separately approve
a correction advisory/notice attachment strategy after checking the exact legacy
payload obligations; do not replace old installers or retroactively certify them.
