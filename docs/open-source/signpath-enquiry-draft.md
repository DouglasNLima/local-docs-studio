# SignPath preliminary enquiries — exact unsent messages

Verified **30 October 2026** for [Goal #62](https://github.com/DouglasNLima/local-docs-studio/issues/62).
**SIGNPATH_PRELIMINARY_ENQUIRY_PREPARED_NOT_SENT**.
Owner approval of the exact message, recipient and email channel is required before
dispatch. Nothing has been sent, submitted or attached. See the
[current evidence and routing assessment](signpath-preliminary-review-2026-10-30.md).

Recommend **Option A first**. The official support address supports first
interactions, but Foundation eligibility reviewer access remains
**SIGNPATH_CONTACT_ROUTE_UNVERIFIED**. Option B is ready at the same published
recipient candidate; a different routed recipient needs new exact-recipient/channel
approval. Neither option authorises an application.

## Option A — routing enquiry

Recipient candidate: **support@signpath.io**
Channel: **email**, no attachments
Subject: **SignPath Foundation — preliminary eligibility enquiry routing**

Hello SignPath Support,

I maintain Lens Docs Studio, a free local Markdown, Mermaid and documentation
studio: https://github.com/DouglasNLima/local-docs-studio.

Before considering a SignPath Foundation application for its native Windows
edition, could you identify the appropriate contact or process for a short
preliminary eligibility enquiry? I would like to clarify the sole-maintainer,
existing-release, component-scope and certificate-lifecycle requirements.

This is a routing question, not an application or a request to create an account
or sign software.

Thank you,
DouglasNLima

## Option B — full preliminary eligibility enquiry

Recipient candidate: **support@signpath.io** (Foundation routing unconfirmed)
Channel: **email**, no attachments
Subject: **Lens Docs Studio — preliminary SignPath Foundation eligibility questions**

Hello SignPath Support,

Please route this preliminary enquiry to the appropriate SignPath Foundation
contact if necessary.

I am @DouglasNLima, the sole human owner and maintainer of Lens Docs Studio —
Local Markdown, Mermaid, and documentation studio:
https://github.com/DouglasNLima/local-docs-studio.

Official distribution is free, without subscriptions or paid feature tiers. MIT
covers only enumerated original first-party material; dependencies retain their
own terms and excluded or uncleared content is not covered by that grant. This
does not establish open-source eligibility of the whole executable or installer.
Scoped licensing and security guidance are public on main; Code signing policy,
Privacy policy and download guidance are now published through GitHub Pages.

GitHub controls require pull requests, applicable CI and resolved conversations,
with no bypass and zero mandatory human approvals. Recorded self-review is not
independent human review. MFA verification remains separate and unverified.

Could you clarify these four points before any formal application?

1. **Sole maintainer.** Would the Foundation accept one person, @DouglasNLima,
   performing Author responsibilities, review of external contributions, manual
   approval of every signing request and final release approval? There is no
   second maintainer or independent human reviewer. What additional controls or
   evidence would this model require, alongside the required MFA?

2. **Existing release.** Historical public Windows releases use a legacy
   WinUI/WebView2 shell. The separate .NET 10/WPF native implementation has only
   unsigned internal preview artefacts and has never been publicly released.
   Could a new native implementation of this already published open-source
   product qualify under the requirement to be released in the form to be signed?
   What evidence or process would you require? We do not assume the legacy
   releases satisfy that condition and do not propose an unsigned native release
   merely to manufacture eligibility evidence.

3. **Components and signing boundaries.** We propose first signing only
   `LensDocsStudio.Native.exe`, then packaging the verified signed apphost with
   approved payload and signing only the version-specific Inno Setup installer,
   `LensDocsStudio.Native-<approved-version>-win-x64-Setup.exe`.
   How do your own-binary, all-component licensing and modified-upstream rules
   apply to the SDK-generated apphost, self-contained .NET/WPF runtime,
   WpfMath/XamlMath with Computer Modern/Knuth and OFL fonts, adapted
   Mermaider/Sugiyama/dagre implementations, Inno-generated installer/uninstaller,
   and embedded resources, artwork, icons and samples? Upstream libraries would
   retain their original bytes, licences and existing signatures; we would not
   re-sign them as project code. Licence, notice and redistribution obligations
   remain unresolved, including excluded artwork/samples; we do not represent
   uncleared material as eligible or assume a System Libraries exception. Which
   boundaries need clearance, replacement, exclusion or additional signing?

4. **Certificate identity and lifecycle.** If accepted, how could we obtain and
   independently verify the approved publisher subject, issuer, SHA-256 certificate
   fingerprint, validity and renewal information? How are revocation and planned
   certificate rollover communicated and verified? Our updater requires both a
   separate RSA-PSS signed update manifest and independently approved Authenticode
   certificate identity. Production trust is empty; first installation needs an
   independently verified manual bootstrap, with a reviewed trust bridge for
   rollover and failure-closed verification throughout.

This is preliminary eligibility clarification, not a formal application, consent
to terms, account request or request to sign a build. We recognise that component,
installer privacy and native release-certification work remains. Please identify
any further evidence needed before applying; we understand acceptance is your
decision.

Thank you,
DouglasNLima

## Dispatch and subsequent decisions

No binaries, credentials, unpublished security information, account details or
private MFA evidence accompany either message. The general fallback is
**info@signpath.io**, requiring separate exact-recipient approval; do not copy both
addresses or submit the application form as a substitute for routing.

Refresh material repository/provider facts before actual dispatch if they change.
Record any later response under Goal #62 without rewriting historical evidence.
A refusal leaves this packet useful: retain the blockers and consider the already
documented alternative distribution assessment under Goal #56. An invitation to
apply does not authorise an account, terms consent, artefact upload or application.
Signing implementation and Stable certification remain separately authorised work.
