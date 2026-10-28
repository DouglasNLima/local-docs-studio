# Third-party notices and attribution review

This repository has no approved first-party licence yet. Third-party components
retain their own licences; this file neither relicenses them nor certifies a
complete distribution. See the [audit findings](docs/open-source/licence-audit.md).

Goal #58 adds an [exact Highlight.js reconstruction and payload review](docs/open-source/third-party-closeout.md),
a [cached known web notice pack](assets/vendor/THIRD-PARTY-NOTICES.txt), and a
[file-level native candidate SBOM](docs/open-source/evidence/goal58/native-preview-957-sbom.json).
The notice pack retains original upstream texts; complete embedded dependency
closure and first-party rights remain separately classified.

The [source-labelled inventory](docs/open-source/evidence/inventory.json) identifies
versions, lockfile references, authors/copyright fields, licence declarations,
upstream repositories and hashes. The [notice index](docs/open-source/evidence/upstream-notices/index.json)
maps each preserved, unmodified upstream notice to its component and source.
Those texts are evidence and grants for the upstream material they describe,
not a blanket grant over Lens Docs Studio. Retain all applicable notices when
redistributing binaries, sources, fonts or generated documentation runtimes.

| Distribution surface | Components and declared terms | Required treatment |
|---|---|---|
| Native direct dependencies | AngleSharp, Open XML, Mermaider, AvalonEdit, CommunityToolkit.Mvvm, PDFsharp/MigraDoc: MIT; Markdig: BSD-2-Clause; PdfPig: Apache-2.0; SharpVectors: BSD-3-Clause | Preserve package copyright/licence and bundled notices; keep Apache notices and change attribution |
| Native transitive dependencies and runtime | Sugiyama, XamlMath, Microsoft extensions/System packages and .NET Core/WPF runtime: mainly MIT with additional upstream notices | Include the exact restored component/runtime notices; Sugiyama includes dagre attribution |
| Mathematics fonts | WpfMath declares MIT AND OFL-1.1; its FONT-LICENSES also describes Computer Modern under Knuth terms | Preserve font attribution/reserved names and modification/renaming conditions; do not relabel fonts MIT |
| Adapted ER layout | Mermaider 0.15.1 at `71b64845570d633a94d29e85579c0a02786fece8` | Keep [adapter notice](src/native/LensDocsStudio.Core/Rendering/PinnedMermaider/NOTICE.md), upstream copyright and modification description |
| Web top-level runtime | Marked, Mermaid, KaTeX, fflate: MIT; Highlight.js: BSD-3-Clause; Mammoth and PDF.js: BSD-2-Clause / Apache-2.0 respectively; DOMPurify: MPL-2.0 OR Apache-2.0 | Preserve exact upstream notices and selected dual-licence terms; do not assume the top-level licence covers every embedded dependency |
| Web embedded packages | 46 versioned source-map packages, including D3, Cytoscape, dagre, KaTeX and DOMPurify; full records in inventory | Mostly MIT/ISC/BSD/Apache; preserve package-specific notices and document local module-path adaptations; remaining bundle provenance is a release gate |
| Legacy WinUI shell | Windows App SDK package has Microsoft Software License Terms; WebView2 SDK package has its own BSD-style notice; installed WebView2 Runtime has separate terms | No blanket MIT/OSI assertion for the legacy binary; audit full resolved payload separately from the native signing scope |
| Development/test tooling | Playwright: Apache-2.0; xUnit/coverlet/FlaUI and other NuGet tools retain their recorded terms | Tooling licence does not become the application's licence; audit experimental Mono.Cecil separately (MIT metadata) |
| Installer/toolchain | Inno Setup and generated installer code retain upstream terms | Preserve existing attribution and review the exact compiler/distribution terms; do not call the installer entirely MIT |

The native notice writer now collects hyphenated/underscored third-party notice
names, declared licence files and the exact published Core/WPF runtime packs.
Its tests preserve notice bytes and reject a missing declared licence. Existing
historical release assets remain untouched. Web/legacy packaging attribution and
binary SBOM completeness still need closure before a new public distribution.
