# Copy Markdown with images: Azure DevOps Markdown fidelity

This note records the clipboard investigation and the manual acceptance gate for Azure DevOps Services.

## Clipboard investigation

The implementation in `assets/scripts/exports/export-service.js` writes one `ClipboardItem` when the asynchronous Clipboard API is available:

| Clipboard representation | Current and corrected payload |
| --- | --- |
| Plain text / Unicode text | The copied Markdown string. Mermaid blocks and managed local images are replaced only by ordered `data:image/png`, JPEG, GIF, or WebP image references so a rich destination can attach them. All other source characters remain unchanged. |
| HTML / CF_HTML | The browser `text/html` representation. On Windows, the browser may expose this to the operating system as CF_HTML; the app does not write CF_HTML directly. The carrier contains literal escaped Markdown text and only the ordered `<img>` elements for embedded data-image tokens. |
| RTF | Not published. |
| Bitmap / PNG | Not published by this feature. The images are carried as `<img src="data:image/...">` elements in the HTML item and as Markdown image references in plain text. |
| File drop / temporary files | Not published. No temporary image files or file-list clipboard entries are created. |
| Custom formats | Not published. No `text/markdown` or application-specific clipboard format is required. |

Multiple images stay associated by source offsets and document order. The Markdown image token is found in the copied source, the managed asset or rendered Mermaid PNG is substituted at that exact offset, and the rich carrier replaces that resulting data-image token with one `<img>` at the same offset. Duplicate image payloads are not deduplicated during composition; each occurrence keeps its own position, alt text, and sequence index.

The previous HTML path called `buildMarkdownHtml`, which rendered headings, lists, tables, code blocks, and rules as semantic HTML. The observed Azure DevOps Setext headings and rewritten list markers are the result of Azure selecting that `text/html` / CF_HTML path and applying its best-effort HTML-to-Markdown conversion. The corrected carrier deliberately has no semantic heading, list, table, code, emphasis, link, or rule elements. Azure can still see the data-backed `<img>` elements that trigger its existing automatic Work Item attachment handling, while the text it converts is the canonical Markdown source.

## Deterministic automated coverage

`tests/fixtures/azure-devops-markdown-fidelity.md` contains the HSI Marking Chart sample excerpt and the required Markdown fidelity cases: ATX headings, bold and italic syntax, inline code, a horizontal rule, nested unordered and ordered lists, a blockquote, a link, a table, a fenced code block, and two ordered image positions.

The browser regression coverage verifies that:

- the source-only `text/plain` result is unchanged;
- the HTML carrier has no semantic formatting elements that could cause HTML-to-Markdown re-serialisation;
- ATX headings, list markers, fences, and the HSI headings remain literal source text;
- two data-image tokens produce two ordered `<img>` elements with the correct alt text and payload;
- image-looking text inside fenced code is not treated as an attachment token.

## Manual Azure DevOps Services acceptance gate

This gate requires a current authenticated Azure DevOps Services session. It is intentionally not automated and requires no app credentials, PAT, REST API call, organisation configuration, project configuration, or Work Item mutation outside the normal user paste/save flow.

1. Open a User Story in Azure DevOps Services.
2. Open a large-text field such as Description that is already configured for Markdown.
3. Put the field in edit mode.
4. Open `tests/fixtures/azure-devops-markdown-fidelity.md` in Lens Docs Studio and choose **Edit > Copy Markdown with images**.
5. Paste once normally into the Azure DevOps field.
6. Confirm that every copied image is automatically uploaded as a Work Item attachment and that the resulting Markdown contains Azure DevOps attachment URLs rather than `data:image/...;base64,...` values.
7. Confirm image order, duplicate-image association, image positions, and permitted alt text.
8. Inspect the pasted Markdown and confirm these remain literal ATX headings and source list markers:

   ```markdown
   # HSI Marking Chart – Current-State Process and Component Architecture
   ## 1. Purpose
   ### Phase A – Source image discovery and ingestion
   - the components involved in the current process;
   - the responsibility of each component;
   ```

The only expected destination-controlled changes are the image references replaced by Azure DevOps attachment URLs and unavoidable line-ending normalisation.

## Compatibility checks

- Notepad or another plain-text editor receives canonical Markdown through `text/plain`.
- A normal Markdown editor receives canonical Markdown; Lens Docs Studio's automatic paste path treats Markdown-like `text/plain` as authoritative.
- Rich destinations still receive the embedded image elements in a single clipboard item, but the feature no longer asks them to interpret the whole document as rendered HTML.

The live Azure DevOps gate remains the final evidence for the target's attachment and persistence behaviour.
