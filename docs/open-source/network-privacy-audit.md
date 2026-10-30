# Network and local-data evidence — 29 October 2026

## Native controls revision — 30 October 2026

The historical matrix retains its inspected revision. Goal #56 changes fresh
native automatic checks to **off**, embeds this policy in Setup and adds a
pre-launch choice with silent/upgrade handling. Preferences must initialise before
the timer; pending/failed consent saves prevent automatic requests. Manual checks
remain explicit. See the [current design and evidence limits](../native/installer-privacy-and-consent.md).
No destination, trust identity, signing provider or telemetry endpoint changes.
The historical missing-control gates below are implemented on this branch;
production certification and provider acceptance remain outstanding.

This audit supports the [Privacy policy](privacy.md) and [eligibility matrix](signpath-eligibility-2026-10-29.md).
It reviews develop `8fdcb5eba90347a829e88e8f30e4ab489b437038` and independently
main `68f1dc955b27a47347a6fccc52a463472b601a1b`. Main has a different browser
tree and the legacy shell; native source below exists on develop only. Paths
refer to these immutable trees, not an assertion about all historical releases.
The final main PR has its own source-bound synthetic/browser receipt.

## Triggers, destinations, data and controls

| Edition / code evidence | Trigger and destination | Data categories and control |
|---|---|---|
| Browser/PWA: service-worker.js install/fetch handlers; main.js; index.html | Opening the hosted app, registration, refresh/revalidation and offline-cache filling contact chosen host, normally GitHub Pages. Offline cache misses can attempt network | IP/ordinary browser request headers, URL and timing; static assets/docs/vendor bytes downloaded. Use local hosting or offline mode; browser caching/platform behaviour is separate |
| Browser rendering: assets/scripts/rendering/render-service.js; utils/security.js | Rendering Markdown remote images can issue image requests without a second download prompt; URL preview in main's editor/image-insertion-service.js can also set img.src | Remote host receives requested URL including query, IP and browser-controlled headers/referrer/cookies as applicable. Sanitising active HTML is not a remote-resource privacy block. Remove remote references, use local images or disconnect |
| Browser links: render-service.js | User activates external link, target=_blank with noopener noreferrer | Browser/recipient handles destination; no background navigation merely for link text. Main and develop share this principle, not identical source |
| Browser storage: editor/draft-service.js; state/config.js; utils/idb.js; files/file-service.js; exports/word-template-service.js | Editing/recovery/settings/recent handles/template packs stored in browser profile via localStorage/IndexedDB; session image/object URLs and selected files used locally | Document text, preferences, handles and user-selected templates/assets may persist locally; browser permission gates disk access. Clear site data/manage drafts explicitly; clearing data can destroy recovery. No remote workspace upload found in inspected first-party paths |
| Browser exports/support: exports/export-service.js; ui/support-bundle-service.js; ui/ui-service.js | Explicit save/copy/download/print/diagnostics export | Chosen document/asset bytes go to destination/clipboard/print. Support bundle is locally generated with allowlisted diagnostics and redaction; user reviews before sharing, redaction is not guaranteed for user-added material. Exported HTML/Docs Sites can load their referenced remote resources when opened |
| Legacy WinUI/WebView2: src/windows/LensDocsStudio.Windows/Services/WebViewBootstrapper.cs; MainWindow.xaml.cs; NativeBridge.cs | Packaged app uses virtual host https://lens-docs-studio.local backed by StaticApp files, not an internet service; remote document resources still use WebView2 networking | Local WebView2 profile holds web state; bridge reads/writes selected files/workspaces. No application GitHub Releases checker found. No custom external-window handler established; do not promise all links launch the default system browser |
| Legacy prerequisites: installer/inno/LensDocsStudio.iss; src/windows/LensDocsStudio.Windows/README.md | Framework/runtime availability checks; user may need .NET Desktop, Windows App SDK and Evergreen WebView2 from Microsoft | Inspected installer does not download/bootstrap prerequisites. Microsoft installation/update services have separate networking/privacy; no claim their traffic is disabled. Runtime availability and old frozen installer behaviour need version-specific checks |
| Native automatic updates: Core/State/SessionStore.cs; Native/ViewModels/UpdatesViewModel.cs; Core/Updates/UpdatePolicy.cs and UpdateService.cs | Default AutomaticUpdateChecks=true, Stable channel. Timer first 10 seconds, then hourly eligibility check; network attempt limited to once/24 h, attempt persisted even on failure. Manual Check now bypasses interval/disable preference | GET api.github.com/repos/DouglasNLima/local-docs-studio/releases?per_page=100; if candidate exists, native-update.json release asset may also be fetched. IP, URL, timing, LensDocsStudio.Native/0.1 user agent and ordinary transport data. No workspace body/paths/credentials attached by update code. Disable Help > Updates automatic checks; separate explicit manual action still works |
| Native update transport/download: Core/Updates/UpdateTransport.cs; UpdateService.cs; Native/Platform/NativeUpdateLauncher.cs | Metadata or user-selected installer requests to github.com repository releases/download, restricted redirects to release-assets.githubusercontent.com or objects.githubusercontent.com | Release metadata and installer bytes downloaded; download/install separate choices, local update-cache used. Production trust empty prevents in-app trusted install. Failed trust never authorises bypass |
| Native image download: Core/Workspace/RemoteImageService.cs; Native/MainWindow.xaml.cs | Explicit Download and insert to supplied HTTPS URL, follows at most five validated HTTPS redirects | URL/query, IP, timing, Accept headers; image client disables cookies/default credentials. Redirect target can be another HTTPS host. User chooses URL or local alternative; URL can itself contain sensitive query data |
| Native preview: Native/Rendering/PreviewImageService.cs | Reads workspace-local image after path validation; remote images are not automatically downloaded by this service | Local file/image bytes; explicit remote import above is separate. This local preview finding does not apply to browser/WebView2 or every exported document |
| Native external links/exports: MainWindow.xaml.cs; Platform/ExportShellService.cs | Confirmed http/https/mailto link opens default application. Opening exported content invokes associated viewer | Destination URL including query; browser/mail/viewer may contact servers. Explicit export saves/clipboard/print can disclose content to chosen destination; inspect references before opening/sharing |
| Native state/recovery: Core/State/SessionStore.cs; Native/App.xaml.cs; ViewModels/WorkspaceViewModel.cs | Local application-data state, preferences, recent paths, sessions/drafts, snapshots/recovery, logs/update cache | Local paths and document text can persist outside source workspace. Uninstall preserves user documents/private state; no automatic cloud sync found. OS backups/synchronised folders/security software are separate |
| Native installer: installer/inno/LensDocsStudio.Native.iss | Per-user local install, Start Menu shortcut, optional unchecked Desktop and Open with associations, installer logging/uninstall; optional launch after installation | Local files/registry/logs; self-contained runtime, no prerequisite download code in script. Privacy presentation and disable-before-first-launch control missing |
| Windows trust: Native/Platform/AuthenticodeVerifier.cs; Platform/NativeUpdateLauncher.cs; NativeSigning.ps1 | Actual signature/chain/revocation verification during future trusted install/signature checks | Windows/CA infrastructure may receive certificate, revocation, timestamp and ordinary connection data. Platform implementation/policies/caches decide requests; no live provider-certificate test performed |
| Telemetry/diagnostics boundary | Targeted first-party fetch/HttpClient/beacon/telemetry scans and diagnostics generation review | No automatic analytics/crash/workspace-upload endpoint identified in inspected application paths. This is a bounded source finding, not a packet capture or guarantee about browser, WebView2, Windows, dependencies, hosting or future editions |
| Future signing infrastructure | Only after separate owner/provider approval | SignPath would receive build artefacts/source-origin data, not a user-workspace integration. No account, upload, signing request or provider telemetry is activated by these drafts |

Native paths in this table are relative to src/native/LensDocsStudio.* as labelled.
The public main policy links immutable develop evidence rather than pretending its
older main source implements native behaviour.

## Required future gates and limitations

Before a signed native installer can meet the Foundation's end-user conditions,
display an owner-approved privacy/network notice during installation and provide
an option that persists AutomaticUpdateChecks=false **before any first-launch
timer**, including upgrade/existing-preference and silent-install semantics.
Explain automatic API/metadata requests, selected downloads, Windows trust checks,
system changes, retained state and uninstall. A README link or a checkbox available
only after launch does not implement this requirement. Test default, opt-out,
upgrade, cancellation and silent paths with synthetic destinations; ask the
provider about the final design. No runtime feature is implemented in Goal #62.

Bounded browser tests intercept synthetic remote image requests and observe a
clean session; they do not inspect encrypted platform traffic or certify every
third-party dependency. Existing native unit tests can exercise transport/redirect/
interval behaviour with fake handlers without contacting GitHub or image hosts.
Legacy smoke explicitly lacked network inspection; do not retroactively upgrade it.
Clean Windows 10/11, physical accessibility/DPI, external PVR delivery, MFA and
actual certificate/renewal/revocation remain unverified.

Affected service policies: [GitHub](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement),
[Microsoft](https://privacy.microsoft.com/privacystatement),
[SignPath Foundation](https://signpath.org/privacy-policy) and
[SignPath service](https://signpath.io/privacy-policy). User-selected hosts have
their own policies; a fixed list cannot cover arbitrary document URLs.
