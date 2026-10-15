# Native UX refinement and Word templates

Work in progress on `feature/native-ux-refinement`, based on verified native modernisation commit `5a373a412e86b93ea8af1b9e5b867250e3d23ee1`. This is an unsigned internal-preview implementation and certification effort. Existing PRs, releases, installed applications and the separate static web edition remain protected.

## Implementation and acceptance ledger

| Milestone | Status |
|---|---|
| A — attached-template compatibility | Implemented; 52 focused Word tests pass, including the new passive-reference and rejecting-resource cases |
| B — template library inspection and empty state | In progress |
| C — file icons and workspace closure | Pending |
| D — shared dialogue form layout | Pending |
| E — export completion actions and session history | Pending |
| F — adaptive application header | Pending |
| G — document location, Settings search and polish | Pending |
| H — complete regression, visual acceptance and packages | Pending |

## Word compatibility and security

The original service rejected every external resource relationship, including Word's passive attached-template metadata in the document settings part. It now accepts exactly one matching external `attachedTemplate` relationship belonging to the actual settings part and one corresponding direct settings reference. Neither target resolution nor target I/O is performed. The relationship and settings reference are excluded when reusable formatting is extracted into a fresh package. A non-blocking warning explains the omission without displaying the target.

Relationship XML is checked before SDK loading for duplicate/missing identifiers, malformed metadata and unsupported external types. Inconsistent, misplaced, internal and duplicated attached-template references are rejected. Existing safe ordinary hyperlinks retain their existing policy. Other external media, OLE, alternate content and unknown resource relationships remain blocked. ZIP path/entry/CRC/size, XML, macro, field, retained-resource and generated-package validations remain in force.

Formatting extraction retains supported styles, numbering, themes, the final section's supported page settings, inherited header/footer references and supported embedded static resources. It excludes the original body, unrelated personal metadata and unsupported structures. It does not reproduce cover designs or promise complete page fidelity.

The privately supplied original DOCX was independently exercised through an isolated copy: the baseline rejected it; the corrected importer accepted it; input bytes remained unchanged; no attached-template relationship remained. The private document, retained content and target paths are excluded from source control, hosted CI and public evidence. Further library/export acceptance is recorded as it is executed.

## Validation and remaining work

The unmodified base passed **1,086 Core tests**, with zero failures/skips. The first compatibility change passed **52 focused Word tests**, with zero failures/skips. Tests include file/UNC/HTTP/HTTPS targets, a listening loopback endpoint with no connection, an exclusively locked local target, byte preservation, retained formatting and generated DOCX schema checks, malformed references and unsafe external resources. Existing tests retain embedded image, macro, package-size and cancellation coverage.

Windows GUI checks and installers will run only on the disposable hosted Windows infrastructure. No newly built app or installer is launched on the ordinary desktop. Full native, browser, visual, installation and final package results are pending; this report does not yet certify preview readiness. Physical DPI/assistive technology and production signing remain outside the hosted evidence. Continuous retains the existing **250 ms** diagnostic target and its documented performance limitation.
