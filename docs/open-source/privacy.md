# Privacy and network disclosure proposal

Lens Docs Studio works with local files. No account, subscription, advertising or
analytics service is required by the inspected application code. This is a source
review of the recorded baseline, not a guarantee about every third-party service
or every future version.

The native application automatically checks the public GitHub Releases API when
enabled, throttled to one attempt per 24 hours. Manual checks bypass that interval;
the preference can be disabled. Requests include the normal network/IP information
and `LensDocsStudio.Native/0.1` user agent. The update transport does not attach
workspace files, local paths or credentials. Download and installation are separate
user choices. Windows may contact certificate/revocation/timestamp infrastructure
when validating trust; unavailable trust evidence must not trigger a bypass.

The explicit **Download and insert** image command contacts the supplied HTTPS
server; native code disables cookies, default credentials and redirects. Opening
external links hands them to the user's browser. The static web edition is served
by its chosen host (normally GitHub Pages), so that host receives ordinary web
requests. Its vendor runtime is bundled locally. Referenced remote content in
documents and exported HTML may contact the referenced servers when displayed;
local processing does not make such content private from those servers.

GitHub's [privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement)
applies to GitHub-hosted pages, issues, releases and downloads. Windows/browser
platform services and a user-selected image/link host have their own policies.
SignPath would receive build artefacts and source/provenance information for
signing after approval, not users' workspaces; its
[privacy policy](https://signpath.org/privacy-policy) should be reviewed before
application. These service links are disclosures, not endorsements.

Local preferences, recent-workspace handles and recovery data remain on the device.
Uninstall does not remove user documents or private state. Support bundles and
screenshots require the user's review before sharing. The owner must approve the
public disclosure and installer presentation/disable option before signed release.
