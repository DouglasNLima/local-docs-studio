# Word Export Templates

Lens Docs Studio supports reusable Word export templates. A user can import a `.docx` file as a browser-local template, select it during Word export, and produce a generated `.docx` that uses the source package as its presentation and page-master base.

The feature is generic. It is intended for corporate Word templates, personal templates, and other organisation-provided `.docx` sources. No TEKenable, HSI, or other customer-specific asset is hard-coded, bundled, or redistributed.

## Template model

The importer treats `.docx` files as Open XML packages and stores a browser-local template model with:

- manifest metadata held in IndexedDB;
- the original `template.docx` bytes used as the export base;
- detected semantic styles and heading-numbering metadata;
- the selected normal content section and its page-master properties;
- header/footer variants and their relationships;
- a representative content-table style or direct-formatting prototype;
- list numbering definitions and the package relationship graph.

The manifest records:

- stable template id, user-editable display name, original source filename, import/creation timestamps, and pack version;
- detected capabilities for styles, numbering, theme, settings, font table, sections, headers, footers, tables, and media;
- detected style ids and names where practical;
- semantic style mapping for document title, content headings, body, table, list, code, quote, and caption;
- whether each mapped content-heading style is automatically numbered.

## Local Storage

Imported packs are stored in the `local-docs-studio-word-templates` IndexedDB database for the current browser profile. They are not uploaded, shared, or added to Markdown Bundle exports.

The ignored `artifacts/word-templates/` path is reserved for optional local/debug copies when developers need to inspect packs. User-imported templates, logos, and generated packs must not be committed.

The Export menu's **Manage** action lists the default fallback and all imported packs. List rendering uses persisted manifest metadata and does not parse DOCX packages. A template can be selected, renamed, or deleted from the management dialog. Renaming updates only manifest display metadata; the stable id, original filename, and self-contained DOCX/model data remain unchanged. Deleting a pack removes its single IndexedDB record, including the preserved package and model data. If the deleted or otherwise unavailable pack was selected, the saved selection is cleared and Word export returns to the default path.

Existing records are migrated in place when they are listed. Missing ids are derived deterministically from available metadata and package bytes, existing ids are preserved, and missing provenance is not fabricated. Older records without the cached model remain exportable because the current package analyser can rebuild that model from the preserved DOCX bytes.

## Export application

When a template is selected, Word export clones the imported package and replaces the main document body while retaining the selected content section's `sectPr`. This preserves compatible package infrastructure such as styles, numbering, theme, font table, settings, headers, footers, DrawingML/VML, fields, relationships, and referenced media. Template body paragraphs, tables, placeholders, and obsolete sections are not copied into the generated body.

Generated document elements use semantic Word styles:

- Markdown H1 maps to the document-title style.
- Markdown H2 maps to the template's top-level content-heading style, H3 to the next level, and so on.
- Paragraphs map to body style ids.
- Tables reuse the detected table style and options, or clone a representative table's direct formatting when no useful style exists.
- Code blocks and block quotes map to code and quote style ids.
- Ordered and unordered lists use compatible template numbering definitions with independent concrete numbering instances.

If a mapped style is missing, export falls back to standard Word style names. Missing optional template parts do not block export.

If a mapped heading style is automatically numbered, a compatible literal counter at the start of the Markdown heading is omitted only from the generated Word text. The Markdown source is unchanged. Literal counters remain when the chosen Word style is not automatically numbered or when the numeric prefix does not match the heading depth.

The first Markdown H1 is also used as the document title. Export updates the core title and intended title text in the selected page master's header/footer while preserving the existing runs, formatting, fields, shapes, and layout. It does not perform an unrestricted package-wide text replacement.

## Sections, headers, footers, and media

The analyser scores the source sections to select the normal content page master rather than assuming a fixed section index. Its exact page size, margins, orientation, columns, title-page flag, and first/even/default header/footer references are retained. Because the complete package is cloned, header/footer relationships, media, DrawingML/VML, decorative shapes, and fields such as `PAGE` and `NUMPAGES` remain structurally intact. Export also requests field recalculation when the document opens in Word.

Generated image and Mermaid relationships receive collision-free ids and media names. Images are bounded by the selected section's usable content dimensions. Exceptionally tall rendered Mermaid diagrams are split into overlapping, page-width slices with page breaks so their labels remain readable without overflowing into headers or footers.

## Table prototype handling

The analyser prefers a representative content table and records its table, grid, row, cell, and paragraph properties. Generated tables retain Markdown cell content but reuse applicable style options, borders, cell margins, fills, typography, alignment, widths, banding, and header-row behaviour. Header rows are explicitly marked to repeat across pages. Detection is generic and does not contain template-specific filenames, colours, logos, or labels.

## Limitations

- No visual template designer is included.
- Templates cannot be edited in the app.
- Export selects one normal content page master; it does not reproduce arbitrary cover/front-matter body content or interleave Markdown across several source sections.
- Ambiguous title locations that cannot be tied to the source document title are left unchanged to avoid corrupting unrelated template text.
- Unsupported or malformed packages fail gracefully rather than emitting a knowingly corrupt document.
- Automated tests inspect package structure and relationships; release validation should also open and visually inspect representative output in Microsoft Word.
- Corporate branding and logos are user/organisation-provided assets and should not be redistributed without permission.

## Future Enhancements

- Optional explicit selection when a template contains several equally plausible content page masters.
- Richer template diagnostics and a future inspector for detected semantic mappings.
- More specialised handling for content controls whose displayed title is not linked to package metadata.
- Optional import diagnostics for unsupported package parts.
