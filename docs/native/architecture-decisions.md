# Native architecture decisions

Baseline: develop 2cc3b5680958c9c553dad23856e6010c158fccd7, inspected 11 October 2026. The original checkout remains on main 0d100cc29dde2ba80755fb1e8af60ffd9d006b16 with an unrelated untracked attachment directory preserved.

1. Separate src/native solution: .NET 10, WPF, AvalonEdit, CommunityToolkit.Mvvm. Existing web and WinUI/WebView2 shell remain independently buildable.
2. Core owns portable documents, source-aware Markdig parsing, filesystem safety, imports/exports, preferences, recovery and update eligibility. Native owns WPF rendering and Windows integration. Dependency injection through constructors.
3. Mermaider is the first browser-free Mermaid candidate; native SVG compatibility is a gate, not assumed from valid XML. Reference: https://github.com/nullean/mermaider. No browser fallback.
4. Open XML SDK for DOCX; preserve template package parts and verify relationships. PDF generation must be local. No commercial dependency without licence review.
5. Native identity: LensDocsStudio.Native, isolated state and installation. Self-contained Inno Setup package; no static web assets in runtime.
6. Updates require explicit product/channel/architecture/OS/version metadata, integrity and publisher trust. Unsigned internal builds may link to releases but must not execute untrusted downloads.
7. develop omits newer main work on portable images, exact document navigation and full Word template packages. Both functional contracts are in scope; no merge of main into develop is implied.

Candidate library documentation and versions are verified against upstream repositories and NuGet. Pinned lock files will record restored dependencies.
