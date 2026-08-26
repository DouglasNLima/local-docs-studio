# Word Export Templates

Lens Docs Studio supports a small local MVP for reusable Word export templates. A user can import a `.docx` file as a Word template pack, select that pack during Word export, and produce a generated `.docx` that carries supported visual identity parts from the imported package.

The feature is generic. It is intended for corporate Word templates, personal templates, and other organisation-provided `.docx` sources. No TEKenable, HSI, or other customer-specific asset is hard-coded, bundled, or redistributed.

## Supported MVP

The importer treats `.docx` files as ZIP packages and stores a browser-local pack with:

- `manifest.json` metadata held in IndexedDB.
- The original `template.docx` bytes for local reference.
- Preserved WordprocessingML parts for styles, numbering, theme, safe settings, headers, footers, header/footer relationships, and referenced media.

The manifest records:

- template id, display name, source filename, creation timestamp, and pack version;
- detected capabilities for styles, numbering, theme, headers, footers, and media;
- detected style ids and names where practical;
- semantic style mapping for document title, heading 1, heading 2, heading 3, body, table, code, quote, and caption.

## Local Storage

Imported packs are stored in the `local-docs-studio-word-templates` IndexedDB database for the current browser profile. They are not uploaded, shared, or added to Markdown Bundle exports.

The ignored `artifacts/word-templates/` path is reserved for optional local/debug copies when developers need to inspect packs. User-imported templates, logos, and generated packs must not be committed.

## Export Application

When a template is selected, Word export copies supported parts into the generated `.docx`, adds required content type overrides, adds document relationships, and writes section header/footer references for the first detected header and footer.

Generated document elements use semantic Word styles:

- H1/H2/H3 map to heading style ids.
- Paragraphs map to body style ids.
- Tables map to a table style id.
- Code blocks and block quotes map to code and quote style ids.

If a mapped style is missing, export falls back to standard Word style names. Missing optional template parts do not block export.

## Header, Footer, And Media Handling

The MVP copies `word/header*.xml`, `word/footer*.xml`, `word/_rels/header*.xml.rels`, `word/_rels/footer*.xml.rels`, and media referenced by those relationships. External relationships are ignored. Header and footer relationship ids are preserved inside their own relationship files, while document-level relationship ids are generated with template-specific names to avoid collision with exported diagram and image relationships.

The selected template's first detected header and first detected footer are applied globally to the generated document section. Complex first/even/default section layouts are future work.

## Limitations

- No visual template designer is included.
- Templates cannot be edited in the app.
- Complex multi-section layouts are not merged.
- Unsupported or unresolved media is skipped rather than corrupting the generated package.
- Microsoft Word is not required for automated validation; tests inspect the `.docx` ZIP/package structure.
- Corporate branding and logos are user/organisation-provided assets and should not be redistributed without permission.

## Future Enhancements

- Template management actions such as rename, delete, and export pack.
- Full first/even/default header and footer mapping.
- Richer content type extraction from the source package.
- Deeper list/numbering mapping for generated ordered and unordered lists.
- Optional import diagnostics for unsupported package parts.
