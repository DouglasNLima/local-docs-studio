# Windows internal preview release notes

This candidate adds native Mermaid visual authoring to verified refinement baseline `42c3fc6` in Draft PR 26. The packaged `version.json` records this build's exact version, source revision, source kind and Actions run. Packages are unsigned and remain separate from the browser/PWA and legacy Windows shell.

- Supported Flowcharts offer **Edit visually** beside their native preview. **Insert → Flowchart visually…** creates a new diagram. Nodes, connections, nested subgraphs and basic styles use native WPF controls and keyboard alternatives.
- The editor keeps a private source draft with Undo/Redo, source validation, zoom/PAN and temporary positioning. Apply replaces only the tracked diagram in one document Undo transaction; Cancel retains the original. Conflicting source, unsafe wrappers and unsupported grammar are protected.
- Standalone `.mmd`/`.mermaid` files retain raw source. Existing Mermaid rendering/export services validate every accepted visual operation. See the offline Native guide's **Mermaid visual authoring** topic for supported bounds and commands.

- Empty drafts offer optional native workspace/document/help actions and brief saving, image and export guidance. Typing hides the strip without changing the document.
- Explicit Continuous displays quiet guidance for the same source-character/mapped-element characteristics that select Pages in Automatic. It remains the user's choice, preserves source and selection, and makes no performance prediction.
- Preview packaging requires clean committed source, excludes development symbols, includes this guide and notes, and records branch-source versus PR merge-checkout provenance.
- Independent package checks verify exact version/source, checksums, update metadata, bundled .NET/WPF components and payload hygiene. Hosted native and web gates include the readiness branch and PR target.

The supported editing, portable workspace/image, Mermaid, recovery, DOCX/PDF export, installer lifecycle and fail-closed update contracts remain in place. The experimental Continuous viewer in Draft PR 22 is **NO-GO** and is excluded.

Known limits remain: unsigned publisher/SmartScreen warnings, no trusted unsigned in-app updates, missing genuinely clean Windows certification, incomplete physical accessibility/compatibility, bounded Mermaid/import/export fidelity, Continuous delays above 250 ms and the unresolved 587.9933 ms details delay. Previous real Word acceptance has its original scope; Excel first-run settings and a disposable Azure DevOps destination remain blocked. No production signing, release, tag or automatic merge is authorised.

Read `PREVIEW-GUIDE.md` for evaluation instructions and `docs/native/mermaid-visual-editor.md` in the source checkout for exact evidence and the readiness verdict. Earlier readiness and performance records retain their original scope. No byte-for-byte build reproducibility or production readiness is claimed.
