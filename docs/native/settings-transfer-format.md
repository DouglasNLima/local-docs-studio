# Appearance settings transfer

In the browser, choose **View → Export appearance settings**. In the native edition, open **Edit → Settings → Import settings…**, review the populated choices and choose **Apply**. Cancel keeps saved preferences unchanged. **Export settings…** saves the choices currently shown in the native dialogue, including unapplied edits; import that file to restore them later.

The browser exports its theme preference, editor layout and outline visibility. An unset browser theme maps to `System`. Native exports also include editor and preview choices. Browser-specific pane widths, focus modes, diagram zoom/themes, local handles, document drafts, recent paths, libraries and update consent are outside this appearance contract. Templates and snippets use the separate library migration; Word templates use original DOCX copies.

Files are UTF-8 JSON, optionally with a UTF-8 BOM, limited to 64 KB and eight levels of nesting. The format version is exact:

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

At least one appearance choice is required. Omitted fields retain the current editable values, including within `editor` and `preview`. Empty nested objects, nulls, duplicate names, unknown fields, malformed JSON and unsupported values are rejected before changing the dialogue. Import does not write preferences until Apply. Apply merges appearance into the latest preferences, preserving unrelated state and the existing durable settings backup. Native file exports use guarded writes and retain recovery for interrupted replacement.
