# Appearance settings transfer

In the browser, choose **View → Export appearance settings**. In the native edition, open **Edit → Settings → Import settings…**, review the populated choices and choose **Apply**. Cancel keeps saved preferences unchanged. **Export settings…** saves the choices currently shown in the native dialogue, including unapplied edits; import that file to restore them later.

The browser exports its theme preference, editor layout and outline visibility. An unset browser theme maps to `System`. Native exports also include editor, preview and diagram-theme choices. Browser-specific pane widths, focus modes, diagram zoom/theme preferences, local handles, document drafts, recent paths, libraries and update consent are outside the browser export contract. Templates and snippets use the separate library migration; Word templates use original DOCX copies.

Files are UTF-8 JSON, optionally with a UTF-8 BOM, limited to 64 KB and eight levels of nesting. Native imports accept exact versions `lens-docs-studio-settings-1.0`, `lens-docs-studio-settings-1.1` and `lens-docs-studio-settings-1.2`. The unchanged browser exporter writes 1.0; native exports now write 1.2, adding diagram theme to the reading mode introduced in 1.1. Older native versions reject the newer format explicitly. A browser export remains:

```json
{
  "formatVersion": "lens-docs-studio-settings-1.0",
  "theme": "Dark",
  "layout": "Split",
  "showOutline": true
}
```

Supported fields:

| Field | Accepted values |
|---|---|
| `theme` | `System`, `Light`, `Dark` |
| `layout` | `Editor`, `Split`, `Preview` |
| `showOutline` | Boolean |
| `editor.fontFamily` | `Cascadia Code, Consolas`, `Consolas`, `Courier New` |
| `editor.fontSize` | Integer 10–36 |
| `editor.wordWrap`, `editor.showLineNumbers`, `editor.convertTabsToSpaces` | Boolean |
| `editor.indentationSize` | Integer 2–8 |
| `preview.zoom` | Integer 50–200 |
| `preview.readingMode` | `Automatic`, `Continuous`, `Pages`; requires format version 1.1 or later |
| `preview.diagramTheme` | `Auto`, `Lens`, `Default`, `Neutral`, `Forest`, `Dark`; requires format version 1.2 |

For example, a native 1.1 preview-only patch is `{"formatVersion":"lens-docs-studio-settings-1.1","preview":{"readingMode":"Pages"}}`. It retains the current zoom and other appearance choices. Legacy 1.0 imports retain the current reading mode. Existing saved native preferences without this field gain `Automatic` while retaining zoom and unrelated state.

A native 1.2 diagram-only patch is `{"formatVersion":"lens-docs-studio-settings-1.2","preview":{"diagramTheme":"Forest"}}`. Imports from 1.0/1.1 retain the current diagram choice; supplying this field under an older format is rejected. Earlier native preferences without a diagram theme use `Auto`. This does not change saved Markdown or update consent.

At least one appearance choice is required. Omitted fields retain the current editable values, including within `editor` and `preview`. Empty nested objects, nulls, duplicate names, unknown fields, malformed JSON and unsupported values are rejected before changing the dialogue. Import does not write preferences until Apply. Apply merges appearance into the latest preferences, preserving unrelated state and the existing durable settings backup. Native file exports use guarded writes and retain recovery for interrupted replacement.
