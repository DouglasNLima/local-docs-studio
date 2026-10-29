# Downloads and signing status

**Draft current guidance — owner review required before publication.**

Lens Docs Studio is a free Local Markdown, Mermaid, and documentation studio.
The public [browser/PWA](../../index.html) works from GitHub Pages or a local
static server. See the [Privacy policy](privacy.md) for hosting, remote resources
and local storage, and the [Code signing policy](code-signing-policy.md).

The five historical [v0.1.0-dev* prereleases](https://github.com/DouglasNLima/local-docs-studio/releases)
belong to the **legacy WinUI 3/WebView2 Windows shell**. They are development
previews; this guidance does not relabel their signatures, licences or release
certification. Their immutable tags, descriptions and assets remain unchanged.
Existing notice/redistribution gaps remain recorded in
[third-party closeout](third-party-closeout.md).

The separate native .NET 10/WPF Windows edition has **no public release**.
Its Actions outputs are unsigned internal candidates. There is no accepted
production signing certificate, signed native Stable release or SignPath
acceptance. Do not use a legacy-shell release as native WPF distribution history.

Future native release guidance must identify the approved exact source, version,
asset hashes, signature verification and installer/uninstall/network disclosures.
Unsigned previews cannot silently establish the first trusted installation.
An independently verified manual installation and approved trust identity are
required before any trusted automatic upgrade route can exist.

Native Stable remains **NATIVE_STABLE_NO_GO**. Continuous 250 ms acceptance FAIL,
the post-PR #64 arrangement-responsiveness timeout, historical UI Automation limits,
clean Windows 10/11, physical accessibility/DPI and actual signed-install/upgrade
certification remain unresolved. A passing documentation PR is not release
certification. No native preview is published to satisfy provider eligibility.
