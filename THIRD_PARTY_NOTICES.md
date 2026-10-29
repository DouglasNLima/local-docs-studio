# Third-party notices

The original first-party grant is restricted by [LICENSING.md](LICENSING.md).
Dependencies retain their actual grants, copyright, attribution and modification
notices. This document does not certify complete redistribution clearance.

| Surface in this main tree | Terms and treatment |
|---|---|
| Marked 16.4.2, Mermaid 11.15.0, KaTeX 0.16.25 and fflate 0.8.2 | MIT; preserve their original notices and embedded-component grants |
| Highlight.js 11.11.1 | BSD-3-Clause; preserve its original grant, including the embedded language contributions |
| Mammoth 1.12.0 | BSD-2-Clause and embedded dependencies; incomplete nested notice closure remains a publication gap |
| PDF.js 5.7.284 | Apache-2.0 and upstream notices; optional cmaps, iccs, standard_fonts and wasm package directories are not shipped here |
| DOMPurify 3.4.5 | MPL-2.0 OR Apache-2.0; preserve the dual grant without asserting a blanket first-party licence |
| Mermaid embedded libraries and KaTeX fonts | Preserve the package/font-specific grants, including MIT, ISC, BSD and Apache notices; a top-level licence does not replace them |
| Legacy Windows App SDK 2.1.3 / WebView2 SDK 1.0.3967.48 | Microsoft Software License Terms / separate BSD-style SDK notice; installed WebView2 Runtime and .NET have their own terms; no whole-binary MIT assertion |
| Playwright 1.60.0 maintenance tooling | Apache-2.0, preserved below; development tooling is not the web application's licence |
| Inno Setup / generated installer code | Preserve upstream compiler and generated-code terms; review exact packaged bytes before any new binary publication |

The [known web notice pack](assets/vendor/THIRD-PARTY-NOTICES.txt) preserves the
existing 68 original texts/references for 65 identified runtime components. It is
byte-identical to the reviewed develop pack because every existing main vendor
runtime file is byte-identical to that approved source baseline. The
[target provenance inventory](docs/open-source/main-provenance.json) verifies the
actual main paths, hashes and each indexed original text range. It does not import
develop's native runtime or binary SBOM.

Additional preserved original grants:

- [Windows App SDK](docs/open-source/licences/windows-app-sdk.txt).
- [WebView2 SDK](docs/open-source/licences/webview2-sdk.txt).
- [Playwright licence](docs/open-source/licences/playwright.txt) and
  [attribution notice](docs/open-source/licences/playwright-notice.txt).

The [remaining provenance limitations](docs/open-source/third-party-closeout.md)
include Mammoth's `dingbat-to-unicode@1.0.1` missing original notice and nested
JSZip/polyfill closure. Do not invent missing copyright text. These issues remain
visible before a future owner publication decision. Historical releases are not
modified or retroactively certified. Artwork, branding and user-content ownership
remain separate; specific exports and complete legacy packages need their own
payload review and all applicable notices.
