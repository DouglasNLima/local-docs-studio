# Native architecture decisions

Baseline: develop 2cc3b5680958c9c553dad23856e6010c158fccd7, inspected 11 October 2026. The original checkout remains on main 0d100cc29dde2ba80755fb1e8af60ffd9d006b16 with an unrelated untracked attachment directory preserved.

1. Separate src/native solution: .NET 10, WPF, AvalonEdit, CommunityToolkit.Mvvm. Existing web and WinUI/WebView2 shell remain independently buildable.
2. Core owns portable documents, source-aware Markdig parsing, filesystem safety, imports/exports, preferences, recovery and update eligibility. Native owns WPF rendering and Windows integration. Dependency injection through constructors.
3. Mermaider is the first browser-free Mermaid candidate; native SVG compatibility is a gate, not assumed from valid XML. Reference: https://github.com/nullean/mermaider. No browser fallback.
4. Open XML SDK for DOCX; preserve template package parts and verify relationships. PDFsharp/MigraDoc provides local WPF-based PDF generation, PdfPig extracts PDF text, and AngleSharp parses HTML without a network browsing context. These are open-source packages pinned in lock files. Full template fidelity and tagged PDF remain validation work.
5. Native identity: LensDocsStudio.Native, isolated state and installation. Self-contained Inno Setup package; no static web assets in runtime.
6. Updates require explicit product/channel/architecture/OS/version metadata, integrity and publisher trust. Unsigned internal builds may link to releases but must not execute untrusted downloads.
7. develop omits newer main work on portable images, exact document navigation and full Word template packages. Both functional contracts are in scope; no merge of main into develop is implied.

Candidate library documentation and versions are verified against upstream repositories and NuGet. Pinned lock files will record restored dependencies.

Native SVG parsing runs on an STA worker; frozen drawings can safely cross to the UI thread. Bounded caches retain unchanged diagrams without retaining full document trees. SVG input is checked for active elements, external resource references and CSS escapes before SharpVectors receives it. Local fragment paint definitions remain supported. FlowDocument construction is still on the UI thread and large-document rendering remains an open performance item.

Use .NET 10's built-in WPF Fluent resource dictionaries rather than introducing a separate UI framework. Apply Windows high-contrast system colours, expose native automation names and retain per-monitor DPI metadata. Window placement uses the Windows placement API so removed monitors can be reconciled by the operating system.

Installer files-in-use behaviour uses an application mutex and disables forced closure/restart. Per-user Open with registration does not take over default extensions. See [Inno AppMutex](https://jrsoftware.org/ishelp/topic_setup_appmutex.htm) and [non-administrative installation](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm). No uninstaller action removes application state or workspaces.
