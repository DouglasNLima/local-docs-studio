# Public signing and privacy policy review

This narrow proposal targets main `68f1dc955b27a47347a6fccc52a463472b601a1b`.
Its older browser/PWA and legacy WinUI/WebView2 source was inspected independently;
it does not import native WPF source from develop. PR #65 already published scoped
MIT/licensing/security documents. This records the signing/privacy policy
proposal before a separately authorised merge or deployment.
Goal [#62](https://github.com/DouglasNLima/local-docs-studio/issues/62) carries the
final SHA/tree, independent agent review, terminal CI, merge simulation and owner
decision receipt. No policy publication is authorised by a passing test.

## Target-specific rights review

The newly authored original explanatory prose in code-signing-policy.md,
privacy.md, downloads.md and this review has been examined for this main tree.
It derives from the project's existing original develop policies, corrected
against actual source and current official requirements. Only original R2 portions
are proposed for explicit enumeration in main-scope.json under the owner's
existing restricted approval. The conditional provider credit, upstream names,
quotations and linked external terms are not relicensed by this enumeration.
No vendor licence, icon, sample, template, historical receipt or R3–R7 exclusion
changes. main-provenance.json retains its frozen comparison and incomplete notice
closure; main_provenance.py retains the historical BASELINE/APPROVED identities.
The canonical ledger is regenerated for the actual proposed tree, not relaxed.

The original regression harness changes are bounded R1 work: offline policy
retrieval plus synthetic network observation. Embedded synthetic image/test
payloads retain the existing mixed-content exclusions. No newly added SPDX or
whole-binary MIT claim is made.

## Source-backed policy evidence

Main paths below belong to the immutable baseline above. Native paths are linked
to develop `8fdcb5eba90347a829e88e8f30e4ab489b437038`.

| Behaviour | Source / boundary |
|---|---|
| Hosted requests and automatic cache/revalidation | [service worker](../../service-worker.js), [entry](../../assets/scripts/main.js); same-origin pages/assets. Browser hosting IP/headers differ from local file processing |
| Browser remote images and external links | [rendering](../../assets/scripts/rendering/render-service.js), [image dialog](../../assets/scripts/editor/image-insertion-service.js); image src can request remote URLs; external anchors use noopener noreferrer |
| Drafts, recents, templates | [draft service](../../assets/scripts/editor/draft-service.js), [file service](../../assets/scripts/files/file-service.js), [templates](../../assets/scripts/exports/word-template-service.js); profile storage/handles and text can persist locally |
| Local diagnostics/exports | [support bundles](../../assets/scripts/ui/support-bundle-service.js), [export service](../../assets/scripts/exports/export-service.js); explicit user output, no automatic upload identified |
| Legacy local host / dependencies | [WebView bootstrap](../../src/windows/LensDocsStudio.Windows/Services/WebViewBootstrapper.cs), [legacy installer](../../installer/inno/LensDocsStudio.iss); packaged virtual host, no app release checker or prerequisite downloader found; WebView2/Windows services separate |
| Native automatic check/default/interval | [UpdatesViewModel](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/src/native/LensDocsStudio.Native/ViewModels/UpdatesViewModel.cs), [SessionStore](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/src/native/LensDocsStudio.Core/State/SessionStore.cs); on by default, timer 10 seconds then hourly due check, one automatic attempt/24 h |
| Native request destinations | [UpdateService](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/src/native/LensDocsStudio.Core/Updates/UpdateService.cs), [transport](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/src/native/LensDocsStudio.Core/Updates/UpdateTransport.cs); api.github.com Releases, github.com metadata/downloads and restricted asset redirects |
| Native explicit image downloads | [RemoteImageService](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/src/native/LensDocsStudio.Core/Workspace/RemoteImageService.cs); user HTTPS URL, up to five validated redirects, cookies/default credentials disabled |
| Native installer disclosure gap | [Inno script](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/installer/inno/LensDocsStudio.Native.iss); no policy presentation or persisted update opt-out before launch. Future implementation must handle default/upgrade/silent/cancel paths |
| Windows trust and storage limits | [AuthenticodeVerifier](https://github.com/DouglasNLima/local-docs-studio/blob/8fdcb5eba90347a829e88e8f30e4ab489b437038/src/native/LensDocsStudio.Native/Platform/AuthenticodeVerifier.cs), native SessionStore above; OS certificate networking separate, ordinary JSON recovery is not encryption |

The full develop evidence packet is tracked under Goal #62. This review does not
claim all dependency/platform traffic was captured. Synthetic tests intercept
remote URLs; no real external image recipient receives user data. Physical/clean
Windows and provider trust tests remain separate, unexecuted here.

## Static delivery and approval

README links the exact **Code signing policy**, **Privacy policy** and current
download guidance. Existing Pages packaging recursively copies docs, so no workflow
or deployment change is needed. Existing legacy packaging also copies docs.
Service-worker cache v69 and the matching shell entry revision add these four
files. Focused Chromium/Edge tests verify obsolete-cache replacement and exact
offline policy bytes. The provenance validator checks their local links and
requires all four cache entries; mutation tests retain missing-path/grant/notice
failures and add a missing-policy-cache failure.

The owner must approve exact text and R2 scope additions, then separately approve
the actual merge/Pages publication with final source and checks. At the time of this review the PR was draft and unmerged; any subsequent merge
and Pages deployment require independently verified receipts. Enquiry dispatch,
provider application, MFA
verification, signing setup and native release decisions remain separate.
No tags/releases, historical descriptions, environments/secrets or rulesets change.
