# Windows internal preview release notes

This candidate continues the verified native `9c7454e` implementation from Draft PR 21. The packaged `version.json` records this build's exact version, source revision, source kind and Actions run. Packages are unsigned and remain separate from the browser/PWA and legacy Windows shell.

- Empty drafts offer optional native workspace/document/help actions and brief saving, image and export guidance. Typing hides the strip without changing the document.
- Explicit Continuous displays quiet guidance for the same source-character/mapped-element characteristics that select Pages in Automatic. It remains the user's choice, preserves source and selection, and makes no performance prediction.
- Preview packaging requires clean committed source, excludes development symbols, includes this guide and notes, and records branch-source versus PR merge-checkout provenance.
- Independent package checks verify exact version/source, checksums, update metadata, bundled .NET/WPF components and payload hygiene. Hosted native and web gates include the readiness branch and PR target.

The supported editing, portable workspace/image, Mermaid, recovery, DOCX/PDF export, installer lifecycle and fail-closed update contracts remain in place. The experimental Continuous viewer in Draft PR 22 is **NO-GO** and is excluded.

Known limits remain: unsigned publisher/SmartScreen warnings, no trusted unsigned in-app updates, missing genuinely clean Windows certification, incomplete physical accessibility/compatibility, bounded Mermaid/import/export fidelity, Continuous delays above 250 ms and the unresolved 587.9933 ms details delay. Previous real Word acceptance has its original scope; Excel first-run settings and a disposable Azure DevOps destination remain blocked. No production signing, release, tag or automatic merge is authorised.

Read `PREVIEW-GUIDE.md` for evaluation instructions and `docs/native/windows-preview-readiness.md` in the source checkout for exact evidence and the readiness verdict. No byte-for-byte build reproducibility or production readiness is claimed.
