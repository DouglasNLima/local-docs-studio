# Main provenance limitations

This target-specific account covers main baseline
`0d100cc29dde2ba80755fb1e8af60ffd9d006b16`. All existing files under `assets/vendor/`
except its first-party catalogue match canonical Git bytes at approved develop
baseline `709564dda30a57cf1e98ba07566f510c1636e05c`. The
[inventory](main-provenance.json) enumerates those files. No runtime dependency is
upgraded or replaced. Mermaid module-extension adaptations and DOMPurify's local
extension change retain their prior upstream attribution. Highlight.js retains
the historical bytes whose reconstruction was recorded in the
[earlier review](https://github.com/DouglasNLima/local-docs-studio/blob/709564dda30a57cf1e98ba07566f510c1636e05c/docs/open-source/third-party-closeout.md).
That earlier review is context; the current check independently verifies main's
file and notice bytes and does not claim a fresh upstream rebuild.

The known web pack is copied byte-for-byte, retaining its original source URLs and
grants. Indexed original notice ranges are hashed independently. Some package
notices cover optional resources not shipped here; inclusion is conservative and
does not assert that every upstream subcomponent is present.

Mammoth 1.12.0 supplier evidence identifies 14 embedded modules. The preserved
pack contains original notice references for 13; `dingbat-to-unicode@1.0.1`
declares BSD-2-Clause but an original notice was not found in the inspected archive
or version-tagged source. Nested JSZip/polyfill closure and full embedded PDF.js
worker closure remain incomplete. This is missing evidence, not a finding of
licence incompatibility. Complete distribution clearance is **BLOCKED** pending
review; the checker must not turn this into an all-components-cleared verdict.

Legacy WinUI/WebView2 packages and Inno outputs require their own complete resolved
payload review. Preserved SDK licence texts do not establish a whole-binary grant.
The native WPF candidate and its SBOM are absent from this main promotion. There
is no new signed/unsigned binary, release or retroactive change to existing assets.
