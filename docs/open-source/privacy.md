# Privacy policy

**Public privacy disclosure — source reviewed 29 October 2026.**
The [source evidence matrix](public-policy-review.md) distinguishes the current
browser/PWA, legacy WinUI/WebView2 shell and proposed native .NET/WPF edition.

Lens Docs Studio works with local files. No account, subscription, advertising or
analytics service is required by the inspected application code. This is a source
review of the recorded baseline, not a guarantee about every third-party service
or every future version.

The native application's automatic checks default to enabled, with Stable channel.
Its first timer tick is about ten seconds after initialisation, then eligibility
is checked hourly; network attempts are limited to one per 24 hours, including
failed attempts. It requests the public GitHub Releases API and may fetch
`native-update.json` release metadata through GitHub download/asset hosts.
Manual checks bypass the interval and the automatic-check preference.
Disable automatic checks in **Help > Updates**. Requests include network/IP information,
requested URLs, timing
and `LensDocsStudio.Native/0.1` user agent. The update transport does not attach
workspace files, local paths or credentials. Download and installation are separate
user choices. Windows may contact certificate/revocation/timestamp infrastructure
when validating trust; unavailable trust evidence must not trigger a bypass.

The explicit **Download and insert** image command contacts the supplied HTTPS
server; native code disables cookies and default credentials but explicitly follows
up to five validated HTTPS redirects, potentially to another host. URLs and their
query strings are visible to those hosts. Native preview image loading itself is
workspace-local. Confirmed native external links open the default browser or mail
application. Browser/WebView2 link handling is subject to their platform behaviour;
the legacy shell has no verified equivalent external-window handler.
The static web edition is served
by its chosen host (normally GitHub Pages), so that host receives ordinary web
requests, including automatic cache filling and revalidation. Its vendor runtime
is bundled locally. Browser/WebView2 rendering of remote image references, main's
URL-image preview and exported HTML can contact referenced servers when displayed,
without a separate download confirmation. Browser headers/cookies/referrers follow
browser policy. Use local images or remove remote references to avoid these requests;
local processing does not make such content private from those servers.

GitHub's [privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement)
applies to GitHub-hosted pages, issues, releases and downloads. Windows/browser
platform services ([Microsoft privacy statement](https://privacy.microsoft.com/privacystatement))
and a user-selected image/link host have their own policies. The legacy shell
uses a local packaged virtual host; its prerequisite-dependent installer does not
download runtimes itself. Microsoft runtime installation/update, browser/PWA updates,
Windows trust and third-party viewers can generate their own traffic.
SignPath would receive build artefacts and source/provenance information for
signing after approval, not users' workspaces; its
[privacy policy](https://signpath.org/privacy-policy) and
[service privacy statement](https://signpath.io/privacy-policy) should be reviewed before
application. These service links are disclosures, not endorsements.

Browser profile localStorage/IndexedDB can retain drafts, document text, recent
handles, preferences and local template packs. Native ordinary JSON state under
`%LOCALAPPDATA%\LensDocsStudio.Native` can retain paths, sessions, drafts,
snapshots, recovery/backups and preferences; downloads use an update cache.
Local does not imply encrypted storage. OS backups, synchronised folders and
security software are outside the application's controls. Manage drafts/site
data or native state deliberately; deletion can remove recovery.
Uninstall does not remove user documents or private state. Support bundles and
screenshots require the user's review before sharing. The owner must approve the
public disclosure and installer presentation/disable option before signed release.
The current native installer does not display this policy or persist an update
opt-out before first launch. Those are missing implementation gates, including
upgrade/silent-install behaviour; the post-launch checkbox is insufficient.
Installers make per-user file/shortcut/optional association changes and provide
uninstall; the native package includes its runtime.

No automatic analytics, crash-report or workspace-upload endpoint was identified
in the bounded first-party source audit. This does not certify all third-party or
platform traffic, historical binaries or future versions. Exports, clipboard,
printing, support bundles and screenshots require deliberate user handling and
review before sharing; local diagnostics generation does not automatically upload.

See the [Code signing policy](code-signing-policy.md) and [download guidance](downloads.md).
