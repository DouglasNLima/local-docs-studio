# Licensing scope

The exact MIT text in [LICENSE](LICENSE), with **Copyright (c) 2026 DouglasNLima**,
applies only to original first-party R1/R2 portions explicitly enumerated in the
[main grant manifest](docs/open-source/main-scope.json). This is the owner's
approved personal-project declaration in [Goal #62](https://github.com/DouglasNLima/local-docs-studio/issues/62),
not independent legal verification of title. An automatic repository MIT badge
must not be read as a licence for every file or complete binary.

This manifest was reviewed against main baseline
`0d100cc29dde2ba80755fb1e8af60ffd9d006b16`, including the browser/PWA and legacy
WinUI/WebView2 shell actually present here. It is separate from develop's native
WPF scope. The [path ledger](docs/open-source/main-rights-ledger.json) records each
target path, classification and canonical Git SHA-256. See the
[review and publication boundary](docs/open-source/main-promotion.md).

| Boundary | Treatment |
|---|---|
| R1 — implementation | MIT for enumerated original application and legacy-shell code, test harnesses, tooling and configuration; embedded exclusions still apply |
| R2 — documentation | MIT for enumerated original documentation and policy prose, excluding quotations, upstream notices and uncleared examples |
| R3 — examples | Registries, templates, snippets, fixtures and embedded samples remain excluded by default; no individual example clearance is granted |
| R4 — visuals and branding | Names, logos, icons, SVG/PNG/ICO artwork, screenshots and other visuals remain excluded; copyright and trademark rights are separate; no endorsement is granted |
| R5 — evidence | Recorded audits, captures, upstream provenance and generated receipts retain their original rights and provenance |
| R6 — upstream | Vendored libraries, dependencies, adapted material, fonts, runtimes and installer components retain their own grants and original notices |
| R7 — user content | Imported/created workspaces, customer/private documents, templates, assets and exported user content remain their owners' property; using this app transfers no rights |

Mixed files grant only their original first-party portions. Test assertions and
harnesses are distinct from embedded Markdown/Mermaid, DOCX/XML samples and image
payloads. UI layout/logic is distinct from artwork. Word-template construction
code does not clear embedded templates. Dedicated registries, issue/release
templates and fixture resources are excluded as whole files. Path membership
does not relicense embedded third-party code, quotations, examples or artwork.

R6 includes `assets/vendor/**` apart from the project-authored `manifest.json`
catalogue, KaTeX fonts, Windows App SDK/WebView2, .NET runtime and Inno components.
See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the preserved grants it
links. Excluded does not mean unlicensed: an upstream grant can apply separately.
No whole-assembly, installer or historical release is labelled MIT here. Earlier
tags are not relicensed, and no native WPF source is promoted by this change.

New paths receive no automatic first-party grant. They remain **BLOCKED** until
reviewed and explicitly added to the manifest. The ledger generator records
decisions; it cannot establish ownership or clear an exclusion. Redistributors
must retain LICENSE, this disclosure and applicable upstream notices. Package
metadata uses `SEE LICENSE IN LICENSING.md`. MIT permits commercial reuse/resale;
free official distribution is project policy, not an extra restriction on MIT.
