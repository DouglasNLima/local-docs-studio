# Lens Docs Studio native guide

## Mermaid visual authoring

Choose **Edit visually** beside a supported Flowchart preview, or **Insert →
Flowchart visually** for a new diagram. The native editor offers nodes, connections,
subgraphs, properties, source inspection and private Undo/Redo. Update edited
properties before Apply; Reset property changes discards only those pending fields.
Apply updates the owning document as one ordinary Undo action. Cancel leaves it
unchanged; Save remains responsible for writing the file and checking disk changes.

Use F2 to edit a node label (Enter confirms; Escape cancels), named + ports to start
a connection, and the element list for keyboard selection. Middle-drag or
Space+drag pans, Ctrl+wheel zooms, F fits and Home resets. Manual positions last for
the editing session, with dashed temporary connection guides. Auto layout or any
source edit restores the canonical Mermaider drawing. No coordinates are saved.
Unsupported syntax opens the complete original diagram for protected visual inspection.
Zoom, PAN, Rendered preview and Edit source remain available; unsafe transformations
and Apply stay disabled. Resource limits still apply to very large or dense diagrams. Editing covers
`flowchart` and `graph`, five directions, common node shapes, separate connections
and named subgraphs. Keep connections outside groups when moving nodes between
them. Chained connections, edge IDs, interactions, configuration directives and
HTML label markup beyond line breaks stay protected.

The visual editor supports up to 300 nodes, 500 total elements, eight nested
subgraphs, 100,000 diagram characters and documents up to 4 million characters.
Source-only mode also protects graphs with more than 100 connections and more
than four connections per node. Other Mermaid families keep their existing
[rendering and export commands](#mermaid-diagrams-and-maths).

## Desktop workspace and synchronisation

The application menu and command toolbar share a row when their measured widths fit; resizing to a narrower window moves the toolbar below the menu and retains native overflow commands. The document location below the tabs shows a short workspace-relative path, with the full saved path in its tooltip.

Markdown files use a page with a Markdown cue; Mermaid files use connected diagram nodes; plain-text files use a lined page. Their shapes remain distinct without colour. **File → Close workspace**, the workspace toolbar and Command Palette detach the folder tree and stop its monitoring. Open tabs, unsaved text, recovery, document roots, relative images and recent-workspace history remain available. Open another folder whenever needed.

The command toolbars expose frequent file, navigation, formatting and viewing actions. Narrow windows use native toolbar overflow. Open documents occupy one horizontal tab row; scroll the strip when it fills.

Use **Ctrl+P** for Quick Open and **Ctrl+Shift+P** for the Command Palette. Both support text filtering, Up/Down, Enter and Escape. The palette runs the same actions as the application's menus and toolbars.

**Ctrl+Alt+B** toggles the workspace and **Ctrl+Alt+O** toggles the outline independently. Drag their dividers to resize them; visibility and width are private application preferences. The workspace header shows its name, with the full path in its tooltip.

**F11** enters Full Screen and restores the previous window placement on exit. **Ctrl+Alt+Z** enters Focus Mode, preserving the chosen editor/preview layout and permanent panel preferences. **Escape** dismisses an open menu or drop-down, then Find, before leaving Full Screen or Focus Mode. **Ctrl+Tab** and **Ctrl+Shift+Tab** switch open documents. The visible theme control offers System, Light and Dark.

Filter workspace files by name or relative path. Folder expansion and open documents survive filtering and refresh. Right-click a workspace item, or press **Shift+F10** on its selected row, for path, link, Explorer, reviewed rename/move and supported deletion actions. These actions use the clicked item. Deletion uses its existing confirmation and snapshots.

Deleting a workspace folder first reviews its files and subfolders. Confirmed deletion retains documentation snapshots and unsaved open text, then recycles the complete folder with its assets. Cancelling recycling restores it. Use the Windows Recycle Bin to restore the complete folder, or Document snapshots to recover text. Links in documents outside the deleted folder remain unchanged. Workspace roots and private application state cannot be deleted through this action.

Right-click a document tab for closing, copy/reveal and pin actions. **Pinned tabs must be unpinned before closing**; bulk close retains pins and stops if a save/close prompt is cancelled. Middle-click closes an unpinned tab. Tab order and pins remain private application state. The outline filters heading titles, indents their hierarchy and follows the current source section.

The preview toolbar exposes **Automatic**, **Pages** and **Continuous** reading modes, zoom, Find and rendered copy. Three separate opt-in controls offer **Follow caret**, **Synchronise scroll** and **Synchronise selection** in Split layout. They preserve the originating pane's keyboard focus. Literal selections use source offsets, including repeated words; generated or transformed content is labelled as a semantic block highlight when exact text correspondence is unavailable. Turning one behaviour off does not disable the others.

In the diagram window, hold **Space** and drag, or drag with the **middle mouse button**, to pan. **Ctrl+mouse wheel** zooms around the pointer. **F** fits the diagram, **Fit width** fills the available width, **Ctrl+0** selects Actual Size, **Home** resets the viewport and arrow keys pan. **Ctrl+C** copies the original drawing, independently of pan or zoom. SVG/PNG exports retain the original drawing.

## Getting started

Lens Docs Studio is a local Markdown, Mermaid, and documentation studio. This guide is bundled with the native Windows preview and works offline.

An empty draft offers optional workspace, document and native-guide actions. Start typing to hide the strip; your document stays unchanged by the guidance. **Help → About the native preview** and **Help → Updates** show the installed version. The package includes `PREVIEW-GUIDE.md` with installation, verification and evaluation restrictions.

Choose **File → Open document** to edit a Markdown or Mermaid file, or **Open folder** to browse a documentation workspace. Files open in separate tabs. **File → New document** creates an unsaved document; **Create from template** offers bundled examples and generators.

Use **Editor**, **Split** or **Preview** to choose your working layout. Drag the divider in Split to resize the editor and preview. The outline opens headings; double-click supported preview text to move to its source. **Alt+Left** and **Alt+Right** move through document navigation history.

Editor layout keeps the outline current and pauses hidden preview rendering. Returning to Split or Preview renders your latest edits. Changing between single-pane layouts also sets the appropriate Find context.

**Edit → Settings → Preview reading mode** offers **Automatic**, **Continuous** and **Pages**. Automatic uses native pages for long documents and a continuous view for smaller documents. In Pages, use the previous/next controls below the preview; Find, outline headings and document links can take you directly to a distant page. Images fit the page, and narrow diagram panels can scroll to their Zoom, export and source-copy controls. Reading mode and preview zoom never alter Markdown or export pagination. Continuous layout may take longer for very large documents.

The native preview is a separate development edition. Its state and installation are kept apart from the browser product and legacy Windows shell.

Continuous shows quiet guidance when the source has at least 262,144 characters or 1,000 mapped elements, the established Automatic-to-Pages characteristics. Pages is recommended for these cases; explicitly chosen Continuous remains available. This guidance is not a latency prediction or performance correction. Continuous's 250 ms objective and the intermittent details construction delay remain unresolved.

## Editing, finding and replacing

The editor keeps ordinary UTF-8 Markdown. **Ctrl+Z** and **Ctrl+Y** undo and redo changes. Use **Ctrl+B** for bold and **Ctrl+I** for italic, or the Edit and Insert menus for lists, tables, links, code and document helpers.

**Insert → Status badge** offers Draft, Ready, Blocked, Deprecated, Beta and In review. Enter the label to display; its text stays literal, including punctuation. To change a badge, place the caret inside its source and choose the new status. **Insert → Keyboard shortcut** displays keys such as `Ctrl + Shift + K`; separate keys with `+` or commas and use `Plus` for the plus key. Both editors retain invalid input with an explanation, and Cancel leaves the document unchanged. **Insert → Horizontal rule** adds a separate Markdown divider. Each insertion or edit is one undoable change.

**Insert → Table** opens a native grid. Place the caret inside an existing pipe table to edit it, or elsewhere to insert a new one; the editor context menu offers the same action. Cells contain Markdown, so links, emphasis, code and escaped characters remain editable. Use **F2** to edit a cell, **Tab** to move, and `<br>` for a displayed line break. Add, remove or move rows and columns with the labelled controls. The header stays first, and a column keeps its alignment when moved.

Choose **Apply table** or **Ctrl+Enter** to apply the complete grid in one undoable change. **Cancel** leaves the document unchanged; applying an untouched existing table also preserves its exact source spacing. Existing supported tables inside lists and quotes retain their enclosing structure. Tables with extra cells or an unsupported structure are left intact with an explanation. The grid supports up to 4,096 total rows, 128 columns, 65,536 cells and one million cell characters; larger tables remain editable directly in Markdown.

**Ctrl+F** searches the focused editor or preview. **Ctrl+H** opens editor replacement. Close the find bar with **Escape** to return to the previous view. Preview Find opens closed details sections when needed and brings the selected match into view.

**Ctrl+Shift+K** opens the workspace document and heading picker. Inserted links remain portable Markdown. **Ctrl+Space** offers Mermaid suggestions inside diagram source.

Wiki-style links use `[[document]]` or `[[folder/document#section|Label]]`. Type after `[[` or press **Ctrl+Space** there to choose a full workspace path. Completion is one undoable edit. Explicit root-relative paths take priority, then paths relative to the current document, then a unique filename. If a short name matches several documents, include its folder and extension.

Wiki links open existing workspace documents and supported sections. Missing or ambiguous targets leave the current text unchanged and explain the problem. Document review checks wiki targets against current drafts; reviewed file moves update targets while retaining labels. Docs Site resolves links among exported documents; standalone document exports keep readable labels.

Save as retains known wiki targets inside the destination workspace. When linked documents remain in another workspace, review the existing uncopied-link confirmation before saving the copy.

**View → Document review** checks the current text, including unsaved edits, for document structure and local link/image issues. Selecting a supported result moves to its source.

**View → Workspace map** shows the documents read from your open folder, with incoming and outgoing Markdown/wiki links from current saved-file drafts. Search titles or paths, filter documents without incoming links or with review notes, and use **Go to link source** to select the original reference. **Open document** participates in Back/Forward navigation. A changed source must be scanned again before using an old source position.

**Workspace diagram** and **Selected diagram** open native diagrams with zoom and SVG/PNG export. Diagrams show up to 60 documents and 240 distinct connections; the native lists retain the scanned results. **Copy Markdown report** supplies portable diagram source and passive document/link tables. The scan reads up to 1,000 documents, 2 million characters per document, 16 million characters in total and 10,000 links. Unreadable or limited scans are labelled partial, and unconfirmed targets remain unchecked. Close and reopen the map to refresh it. It excludes private application state and never writes workspace documents.

## Saving, recovery and external changes

**Ctrl+S** saves the current document. **Ctrl+Shift+S** saves a copy or chooses its first location. Local images and links are prepared for the destination before source is written.

Unsaved documents and edits are retained in separate recovery state. **File → Take snapshot** keeps a named point to review later under **Document snapshots**. Review restored drafts before saving them over an existing file.

Recovery has a 512 MiB encoded JSON limit across open documents, including saved baselines, current text and metadata. If recovery exceeds that limit, the previous session and backup remain unchanged and the application reports the failure. Close already-saved tabs to reduce the total before retrying. A document save waits for successful recovery before changing source. Unreadable or oversized old state is retained separately before a previous version is restored; check the recovery warning before continuing.

If another application changes a file, Lens Docs Studio flags the difference and keeps your editor text. Follow the conflict choices rather than assuming the version on disk is unchanged. **Reload from disk** asks about unsaved work and retains recovery where supported.

Use **Rename or move document** to review affected links before applying a move, including a filename case change such as `Guide.md` to `GUIDE.md`. Existing destination entries are never replaced, and private application state is excluded from link updates. **Delete document** uses recoverable Windows deletion and retains a snapshot.

Select a folder in the workspace tree and press **F2**, or choose **File → Rename or move selected folder**, to move the complete folder with assets and empty subfolders. Save any open workspace edits when prompted, then review the destination and document link changes. The destination's parent folder must already exist; existing folders are never merged. Clear the link-update option to keep document bytes unchanged. Raw HTML references and unresolved wiki targets need the manual review described in the warnings.

Folder moves check up to 5,000 entries, 256 MB of file content and 1,000 workspace documents, with bounded source/recovery text. New or changed files stop a reviewed move. Failed link writes attempt to restore completed writes and the original folder; if external changes prevent reversal, current files and recovery text are retained for review. Startup recovery opens original/planned text as drafts and never replays folder paths. Open tabs, recent paths and Back/Forward follow a successful move. Case-only names such as `docs` to `Docs` use the same review and update portable links when selected.

## Images and attachments

Choose **Insert → Local image**, paste image pixels, or drop a supported image into the editor. Images are validated and saved as local assets before the Markdown selection changes. An image inserted before the first save stays with the recovered draft and is copied into the destination workspace when saved.

**Insert → Image from URL** retrieves an image only after an explicit request. Preview does not automatically fetch remote image references.

To add or edit a caption, put the caret inside a standalone Markdown image and choose **Insert → Image figure**. Alternative text, caption, destination and title remain editable. Each application is one undoable change.

**File → Workspace images** lists local images and their references. Review the source locations before renaming or removing an asset.

## Mermaid diagrams and maths

Write Mermaid inside a fenced `mermaid` block or open a standalone `.mmd` or `.mermaid` file. Supported diagrams render through bundled .NET components. **Zoom**, **SVG**, **PNG** and **Copy source** actions appear beside a rendered diagram.

In **Mermaid Visual Editor**, the Elements tiles show the supported shape vocabulary;
click to insert or drag onto the canvas. The diagram list remains the keyboard and
multi-selection alternative. Use the grouped toolbar for history, direction, layout,
zoom and rendered preview; **More commands** holds commands that do not fit.

Properties are grouped by the selected element. **Update node** stays above the
scrolling fields and commits the pending node values together. Membership, class
and colour actions retain their existing explicit behaviour. **Reset changes**
appears while values are pending. Selection changes, node dragging, connection
completion, inline label editing and document Apply wait until those values are
updated or explicitly reset. Escape cancels connection mode while retaining pending
properties and selection. Colour swatches preview
six-digit hexadecimal values; the validated source value remains authoritative.

The tick in the **Mermaid source** header validates source changes; the adjacent copy
icon copies the draft. A star marks source needing validation or protected source;
its tooltip explains the state. Collapse the header to reclaim canvas space. The
**Mermaider layout** tooltip explains canonical positioning. Preview, Rendered preview
and the visual canvas share the same native drawing; selection outlines and ports are
editing adorners. At overlapping connection paths, choose the intended connection in
the element list rather than guessing from a pointer click. Apply and
Cancel remain in the fixed footer; hover a shortened status to read its full message.


Choose **View → Diagram theme** or use **Edit → Settings → Diagram theme**. Auto follows the application appearance; Lens, Default, Neutral, Forest and Dark stay fixed. The choice is saved on this device. Diagrams keep the selected palette in previews, workspace maps, SVG/PNG, Word/PDF/HTML, clipboard content and Docs Sites. A Docs Site can use a different page theme without changing its diagram images. These are native palette equivalents; browser Mermaid layout and colours are not reproduced pixel for pixel. Markdown source is unchanged.

The built-in catalogue includes examples for the supported diagram families. If a diagram cannot be rendered, its source is preserved and the preview explains the problem. **Copy source** copies the diagram code; **Edit source** opens the editor at its fence, or at the beginning of a standalone Mermaid file, and clears any previous selection. From Preview layout, this switches to Split so you can correct the code immediately. If the document has changed since the preview was drawn, wait for the updated preview before using Edit source. Full compatibility with every browser Mermaid version is still being evaluated.

Maths supports inline dollar delimiters, parenthesised LaTeX delimiters and display formulae. Fractions, roots, scripts, Greek symbols, sums, integrals and common matrix, cases and aligned expressions render offline. Right-click a formula to copy its original LaTeX.

Unsupported or oversized formulae remain readable source. Each formula is limited to 4,096 characters, with at most 256 formulae and 32,768 formula characters per document. HTML, Word, PDF and rich clipboard output use high-resolution formula images; Markdown keeps the editable source.

## Details, callouts and document helpers

**Insert → Details section** creates or edits a plain summary, Markdown body and initial expansion state. Complete passive details sections render as native expanders. Nested headings, links, tables, images and diagrams keep their source positions.

Expanding a section in preview does not edit Markdown. Word, PDF and rich clipboard exports retain the complete closed body. HTML and Docs Site exports keep interactive sections.

The Insert menu also offers callouts, progress indicators and explicit anchors. Unsupported embedded HTML remains inert source; it is never executed as application UI.

## Copying and pasting

**Edit → Copy rendered text** copies readable Unicode text, including original LaTeX for formulae. **Copy rendered content** supplies formatted HTML and text with embedded supported images and diagrams for applications such as Word.

Copy rendered text prepares the current document even when its preview is hidden. If the document or clipboard changes during preparation, the app leaves the clipboard unchanged and asks you to copy again.

**Copy Markdown with images** retains Markdown around local images and complete Mermaid blocks while supplying portable image representations. Attachment handling still depends on the destination application; live Azure DevOps workflows need separate validation.

**Paste Special** offers plain text, table, code, quote, HTML, list, checklist, numbered list and Mermaid modes. **Ctrl+Shift+V** pastes literal text. Formatted HTML and spreadsheet cells can become editable Markdown. Review any warnings about merged cells or linked images.

Native preview tables provide context actions for copying the selected table and exporting its cells as UTF-8 CSV. CSV preserves cell text; spreadsheet applications may infer their own value types when opening it.

## Templates, snippets and browser migration

Browser **View → Export appearance settings** saves theme, layout and outline choices. Open **Edit → Settings → Import settings** here to review those choices before Apply; Cancel keeps your saved settings. Missing editor and preview choices stay as currently selected. Native **Export settings** saves portable editor, preview, theme, layout and outline appearance choices for transfer or restoration, without desktop synchronisation preferences, documents, recent paths or update consent.

**File → Create from template** opens bundled examples and generators. Filter by category or name, edit the labelled fields and inspect the generated source. Creation opens a new unsaved tab. **Insert → Built-in snippet** inserts one undoable source change.

**File → Content library** manages your own templates, snippets and export profiles. Save a document as a template or selected source as a snippet from the File menu. Local raster images are embedded in the library copy; Mermaid remains editable. Referenced workspace documents still need their original files.

To migrate a browser library, use **Create → Export local library** in the browser edition. In the native Content library, choose **Import JSON** and select that file. Compatible version 1.0 entries merge without replacing existing content. Repeated imports are detected and conflicting identifiers receive new values.

Rename changes the library label. **Archive** hides an entry without deleting it; enable **Show archived** and choose **Restore** to bring it back. JSON export includes the entries in the selected active or archived collection. Browser profiles are not scanned, and browser settings or handles are not imported automatically.

## Word templates

**Export → Select Word template** applies supported formatting from a chosen DOCX to this session. The original file remains unchanged. Exports use fresh packages with validated styles, numbering, page layout and supported header/footer content.

Unrelated template body text and personal package metadata are excluded. Templates with active fields, external resources or unsupported retained objects are rejected with an explanation. Page, section and date/time fields are supported.

**Export → Word template library** keeps reusable templates in private native state. Import, rename, save a portable copy, archive and restore templates there. **Use default Word styles** clears the current selection.

The library shows retained styles, headings, numbering, page settings, header/footer resources and import notes. Its native typography sample is illustrative; it is not a complete page-fidelity preview and does not import the original body or cover design. Ordinary DOCX files with one consistent external attached-template reference are supported: the untrusted target is never opened, and both its relationship and settings reference are omitted with a warning. Other unsafe external resources remain blocked.

To move a browser template into this library, select it in the browser's **Export** menu and choose **Save template copy**. Import the downloaded DOCX here and choose **Use template**. The browser copy is the original source document, including its body and metadata; native import extracts supported reusable formatting. If the browser no longer has its original bytes, import your original DOCX there again or directly into the native library.

## Imports and exports

Successful file exports offer **Open file** and **Show in folder** in the status area. Files open only when you choose an action, using the registered Windows application. The app checks that the saved output remains present and unchanged before opening, revealing or copying its path. **Export → Recent exports** holds up to ten outputs for this session; newer status events dismiss the current actions. Missing, changed, failed and cancelled outputs never receive a new success action.

Word bold, italic, strikethrough and underline can come from document defaults, paragraph styles, character styles or direct formatting. Direct on/off values override inherited formatting. Double strikethrough and other underline variants are simplified to single lines with conversion notes. Missing or cyclic styles keep readable text and notes; chains longer than 64 styles are rejected before output is created. Headings retain their structural appearance and code stays literal. Conditional table/numbering styling, complex-script properties and exact Word layout may need manual correction.

Word import also limits generated text, because repeated links can expand a small DOCX substantially. If conversion exceeds the text limit, the app reports an error before creating the destination workspace. Your existing document and the original DOCX remain unchanged; split the Word document into smaller sections and import those separately.

Use **File → Import DOCX, HTML or PDF** to create editable content from supported input. Inspect the resulting text and warnings; document formats do not always map exactly to Markdown.

Conversion notes appear in the status bar after import. Hover over a shortened status message to read the full text; repeated notes are shown once.

Embedded Word pictures keep their image bytes and supported descriptions as Markdown alternative text. Explicitly empty descriptions remain empty; missing descriptions fall back to available picture metadata or a generic label. Each picture occurrence keeps its own description even when the image bytes are shared.

Word import preserves supported heading levels and lists supplied directly or through paragraph styles, including nesting, starting values and matching continuation paragraphs. Web/email links and paired internal bookmarks retain their destinations; linked pictures remain linked. Recognised code styles become literal code blocks, and consecutive quote paragraphs stay grouped, including supported placement inside lists. Code language and deeper quote structure may need manual correction; a generic spacing style is not treated as code. Unsupported number formats are simplified with a warning; lists and block styles inside table cells become text with explicit notes. Unsupported or missing link targets remain text, and Word fields keep their stored results without being evaluated. File/relative links and field-based hyperlinks may need manual correction. Imported diagram/formula pictures remain images rather than editable Mermaid or LaTeX source.

The Export menu offers **Word**, **PDF**, **HTML**, **Markdown bundle** and **Docs Site**. Exports are prepared separately from source. App-only search, outline and review controls are excluded from document output.

PDF uses supported Windows fonts to retain selectable characters, including simple emoji in monochrome. When no supported installed font contains a character, export identifies its Unicode code point and leaves the destination untouched; use another format or install the appropriate Windows language font. PDF complex-script shaping, bidirectional layout and joined/colour emoji remain limited. Markdown, HTML and Word retain portable Unicode source.

PDF retains `~~deleted text~~` as a visible strike-through and `==marked text==` with neutral highlighting. These remain selectable text across wrapped lines, pages, links and table cells; the Markdown source stays unchanged.

Word and PDF retain HTTP, HTTPS and email links, including angle-bracket URLs such as `<https://example.org>` and email addresses such as `<review@example.org>`. PDF links remain clickable when their labels wrap. Unsupported external schemes and relative file links remain readable text in standalone Word/PDF exports.

Word, PDF, HTML and rich-copy code blocks retain syntax colours for supported languages and preserve indentation as editable or selectable text. Word keeps tabs and line breaks without adding an extra final blank line. Unknown languages and blocks that exceed the colouring limit retain complete plain text. Exported Docs Sites adapt code colours to the chosen theme, printing and high-contrast preferences.

Word export also limits the combined formatting complexity of code blocks. If many tabs, line breaks or coloured sections exceed that limit, export stops before writing the destination and asks you to export fewer or smaller blocks. Your Markdown remains unchanged.

Markdown bundles keep portable source and local assets. Docs Site exports produce static HTML, navigation, section search and local resources for browsing or static hosting. Local preview and export do not execute imported scripts.

**File → Import Markdown bundle** extracts supported documents and assets into a new folder. An optional artefact manifest supplies display metadata for this import session. **View → Artefact bundle** filters imported items by group, title/path and supplied evidence label. Labels are displayed as supplied: candidate findings remain candidate findings. Invalid metadata produces an import note while valid documents remain available.

Use **Export → Artefact review pack** to include a rebuilt safe manifest with current editor text and local assets. Generic Markdown bundles exclude the manifest. Docs Site exports may use safe title/order/group hints, with Markdown front matter taking precedence. Switching workspace or restarting clears artefact context; import the ZIP again to use its metadata.

**Export → Export profiles** offers generic documentation, Azure DevOps Wiki Markdown and GitHub Pages Docs Site defaults. Applying a profile affects the current session. Save a reusable profile in the content library when needed. The Azure DevOps preset converts complete Mermaid fences in Markdown bundle entries while preserving other source and file encodings.

Optional artefact review packs, broader template fidelity and some advanced export semantics remain under development. Inspect important exported documents in their intended destination before distribution.

## Settings and keyboard shortcuts

Open **Edit → Settings** with **Ctrl+,**. Choose theme, layout, outline visibility, editor font and size, wrapping, line numbers, indentation, preview reading mode and zoom. **Apply** saves the choices on this device. **Restore defaults** can still be cancelled.

Use **Search settings** to find reading mode, zoom, line numbers, indentation, workspace panels or synchronisation. Filtering keeps staged choices intact. Workspace visibility and the three independent synchronisation controls use the existing desktop preferences; Apply saves them and Cancel keeps the previous values. Appearance-settings transfer continues to carry its established appearance fields, excluding private desktop navigation and synchronisation choices.

Indentation options affect new typing; existing tabs, spaces, text, selection and Undo/Redo remain intact. Preview zoom ranges from 50% to 200% and does not change export sizes. **System** theme follows Windows; Windows high-contrast colours take precedence.

| Action | Shortcut |
| --- | --- |
| New document | Ctrl+N |
| Open document / folder | Ctrl+O / Ctrl+Shift+O |
| Save / Save as | Ctrl+S / Ctrl+Shift+S |
| Close document | Ctrl+W |
| Find / Replace | Ctrl+F / Ctrl+H |
| Bold / Italic | Ctrl+B / Ctrl+I |
| Literal paste | Ctrl+Shift+V |
| Workspace document link | Ctrl+Shift+K |
| Mermaid suggestions | Ctrl+Space |
| Back / Forward | Alt+Left / Alt+Right |
| Rename or move document | F2 |
| Settings | Ctrl+, |
| Native guide | F1 |

## Updates and privacy

**Help → Updates** controls automatic checks and Stable or Preview channels. You can check manually, review release notes, postpone a version, or choose a verified download and installation when available. Checking public GitHub release metadata does not send local documents.

Downloading and installing are separate user actions. Installation saves recovery state, asks about unsaved edits and closes the workspace before opening the wizard. Updates require compatible signed metadata, integrity checks and a trusted Windows publisher.

Production signing identities are not yet configured for this internal preview, so untrusted downloaded installers cannot run through the app. Positive signed-upgrade and clean-machine certification remain outstanding.

Native preferences and recovery are stored under `%LOCALAPPDATA%/LensDocsStudio.Native`. Source documents remain ordinary local files. Automatic release checks are optional; image retrieval and opening external links require explicit actions. The installed editor and preview use no embedded browser, JavaScript runtime or local server.
