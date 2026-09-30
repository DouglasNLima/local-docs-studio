# Workspace images

Lens Docs Studio uses one **Insert image** action for workspace images, imported raster files and HTTPS URLs/local references. Alternative text is editable. A blank caption produces Markdown; a supplied caption produces the supported HTML figure. Paste and drop use the same acquisition and persistence service without opening the full dialogue.

## Paths and portability

New images use `<workspace-root>/assets/images/`. Existing workspace images retain their original location. The internal asset identity is a case-sensitive path from the workspace root. The reference stored in Markdown or an HTML `img` is relative to its document:

| Document | Physical image | Stored reference |
| --- | --- | --- |
| `overview.md` | `assets/capture.png` | `assets/capture.png` |
| `docs/details.md` | `assets/capture.png` | `../assets/capture.png` |
| `docs/details.md` | `assets/images/new-capture.png` | `../assets/images/new-capture.png` |

URI components encode spaces, Unicode, parentheses, hashes and literal percent signs once. Local Windows separators are accepted by the image dialogue and normalised; external URLs retain their external meaning. The resolver accepts `./` and `../` within the authorised root, with no basename search or general root fallback. A recognised older Markdown Bundle manifest may tag its session assets with legacy root semantics; saving pending assets and re-exporting a bundle explicitly canonicalise those image tokens. Ordinary ZIPs and disk images never receive this fallback.

Code fences, inline code, indented code, prose and unrelated links are excluded from reference rewriting by the vendored Markdown lexer. Markdown images, reference images, table-cell images and figure sources share the same path resolver. Missing or invalid local references produce a useful preview diagnosis and a locate/import action description.

## Host behaviour

In Windows, **Open folder** authorises a workspace capability. Specific bridge operations inventory, read, pick and create raster images. The host checks the WebView message origin and capability, limits payloads, rejects absolute/UNC/traversal paths except explicitly confirmed dialogue input inside the workspace, and validates extensions, signatures, MIME and image decoding. It pins directory handles, rejects reparse points, junctions and symlinks, verifies final physical paths, creates new files without replacement and reads the saved bytes back. The native image picker can prove that an existing selected file belongs to the workspace and reuse it; an external selected image is copied explicitly.

In supporting browsers, an authorised directory handle supplies folder reads and writes. Clipboard/drop operations query existing write permission; **Save pending images to workspace** can request permission through the user's action. Folder/file-input imports supply read-only bytes, without granting adjacent directory writes. A standalone Markdown file handle never authorises a neighbouring folder: **Select image destination folder** must authorise and confirm its location. Virtual documents retain a visible unsaved state and must be saved/exported to match the displayed document-relative location.

The browser uses an exclusive writable stream, a serial write queue and checks existing names/bytes before writing. File System Access has no native `CREATE_NEW` equivalent. External programs creating an empty file in the small interval between the existence check and handle creation cannot be excluded atomically. The Windows host supplies actual create-without-replacement semantics. No multi-file atomicity is claimed for either host.

Limits: PNG, JPEG, GIF and WebP; 20 MB per image; 40 million decoded pixels; 100 MB of loaded managed image bytes per session; inventory depth 12 and at most 500 images. Browser and Windows decoding may reject damaged or unsupported codec payloads. SVG file import remains blocked; Mermaid exports retain their existing separate sanitised SVG path.

## Saving and recovery

**Saved in workspace** means the image's physical bytes were available or were written and verified. It does not mean the Markdown is saved. Saving Markdown never silently saves its images or clears the document's dirty state on an image write.

**Available in session — not saved to folder** and **Save failed — bytes available in session** identify temporary assets. They remain exportable, but session memory is not a physical workspace or a promise of recovery after closing the app. External images remain external and require network access; they are never fetched into managed storage automatically.

**View > Manage assets > Save pending images to workspace** presents the destination, writes without replacing existing names, updates only relevant Markdown/HTML image references and marks those documents dirty. Repeating it leaves already saved images alone. Missing bytes require locating/importing the file. A write failure retains recoverable bytes and its cause. A late operation after a workspace change cannot insert into the next document; its bytes can be recovered/downloaded through Manage assets. Undo and cancellation do not delete physical files.

When pending bytes are referenced by a read-only document, migration leaves those bytes and references intact and explains that an editable copy or a Markdown Bundle is needed. It does not move the asset identity while leaving the read-only reference behind.

Physical rename, movement and removal are unavailable in Manage assets; the interface explains this and preserves files and references. Use the workspace's own file tools deliberately when changing its physical organisation.

Before saving a document with pending/missing local images, the app explains that Markdown writes references only and offers keeping the editor open for persistence or a portable export. It leaves intentional external links editable.

## Export and transfer

**Export Markdown Bundle** includes referenced local image bytes and documents with document-relative references, including figures and captions. A missing local dependency prevents the export from claiming a portable result. Legacy image references are canonicalised in the exported text. External dependencies are reported separately; no remote image download occurs.

HTML, Word, PDF print and Docs Site use the same resolved image bytes and retain their existing export contracts. Temporary preview URLs and machine paths are never new persisted image references. Word template packs, snapshots, DevOps preferences and unrelated content are unaffected.

To transfer a physical workspace, save pending images, save changed Markdown, close the app and copy the complete folder. Test the copy with a separate profile and the source unavailable. A working preview in the original profile alone does not establish portability.

## Editor context

The editor captures document identity, text version, selection and both scroll axes before asynchronous insertion. It rejects stale operations, edits a range with one history unit, focuses without automatic scrolling and restores the actual viewport. A hidden measurement mirror reveals only the minimum necessary part of the insertion. Selection/preview synchronisation remains enabled; image decoding completes before preview render restoration. Other insertion helpers use the shared editing/context mechanism.
