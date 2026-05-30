# Lens Docs Studio UI Definitions

This is the portable UI definition set for reusing the Lens Docs Studio look and feel in another browser app. The canonical source remains `assets/styles/app.css`; the copyable token export is `docs/architecture/lens-docs-studio-ui-tokens.css`.

Lens Docs Studio should continue to feel like a compact local documentation workbench:

- Generic product promise: Local Markdown, Mermaid, and documentation studio.
- Restrained Lens accent: `#FF883E` for primary actions, selected states, focus rings, active indicators, and small brand moments.
- Neutral working surfaces: light mode leans on `#FAFAFA`, `#F8F9FB`, `#F3F4F6`, and `#EDEDED`; dark mode uses charcoal surfaces rather than saturated colour.
- Dense, practical controls: toolbar groups, icon buttons, split panes, menus, sidebars, and status feedback are more important than marketing composition.
- British English in first-party UI and documentation.

## Token Export

Use `docs/architecture/lens-docs-studio-ui-tokens.css` when you want a direct starter stylesheet. It exports:

- Theme tokens for dark and light surfaces.
- Compatibility aliases for the current app variable names.
- Base recipes for the app shell, topbar, brand mark, buttons, icon toggles, panes, menus, editor, preview, toolbar groups, and statusbar.
- Responsive breakpoints matching the current app shell.

The token file is not loaded by Lens Docs Studio. It is a copyable reference for another app.

## Colour Tokens

| Role | Dark | Light | Use |
| --- | --- | --- | --- |
| Background | `#111318` | `#FAFAFA` | App canvas and outer shell. |
| Surface | `#181A20` | `#ffffff` | Menus, cards, dialogues, and panel bodies. |
| Strong surface | `#202329` | `#F8F9FB` | Pane headers, statusbar, structured chrome. |
| Soft surface | `#2B2F37` | `#F3F4F6` | Tabs, file rows, inline containers. |
| Softer surface | `#3A404A` | `#EDEDED` | Hovered soft surfaces. |
| Text | `#eef2f7` | `#0f172a` | Primary content. |
| Muted text | `#9da8b8` | `#64748b` | Secondary labels, helper copy, inactive metadata. |
| Strong muted text | `#c7d0dc` | `#334155` | Panel labels and high-priority metadata. |
| Accent | `#FF883E` | `#FF883E` | Primary, selected, focus, and brand moments. |
| Accent strong | `#E66F22` | `#D95F16` | Primary button gradient end, strong accent text. |
| Accent soft | `rgba(255, 136, 62, .12)` | `rgba(255, 136, 62, .10)` | Active rows, toggle state, quote backgrounds. |
| Success | `#4ade80` | `#16a34a` | Positive status. |
| Warning | `#fbbf24` | `#d97706` | Dirty state, warnings. |
| Danger | `#f87171` | `#dc2626` | Errors and destructive actions. |

Keep orange restrained. It should mark the user's current action or location, not flood the interface.

## Typography

The app names three font roles but does not require network fonts:

- Body: `Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`
- Display: `Outfit, Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`
- Monospace: `"JetBrains Mono", ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace`

Use display type for product names, pane titles, menu headings, and preview headings. Use body type for controls and dense UI. Use monospace for the editor, code, line numbers, diffs, and generated snippets.

Avoid negative letter spacing in compact controls. The live app has a few inherited negative values in historical refresh layers, but new UI should use `letter-spacing: 0` for normal text and only positive tracking for uppercase labels.

## Shape And Elevation

- Panel radius: `12px`
- Control radius: `8px`
- Small control radius: `6px`
- Pill radius: `999px`
- Panel shadow: `0 10px 30px rgba(0, 0, 0, .24)` in dark mode, `0 8px 24px rgba(22, 28, 39, .10)` in light mode.
- Floating menu shadow: `0 18px 44px rgba(0, 0, 0, .42)` plus a soft outline in dark mode.
- Focus ring: `0 0 0 3px rgba(255, 136, 62, .20)` in dark mode and `.18` in light mode.

Use cards only for individual repeated items, dialogues, and framed tools. The main work surface is panes and bands, not card stacks.

## Layout

The shell is a three-row grid:

- Topbar: compact app chrome, about `2.75rem` high on desktop.
- Main shell: sidebar, splitter, workspace.
- Statusbar: `1.65rem`, left-aligned, concise status copy.

Desktop shell:

- Sidebar: `292px`, collapsible to a `3rem` icon rail.
- Splitter: `8px`.
- Workspace: editor and preview panes, with the editor starting at `50%` width.
- Pane minimums: `18rem` per editor or preview pane.
- Gaps: `.5rem` between sidebar, splitter, and panes.

Responsive rules:

- At `1080px` and below, stack the sidebar and workspace as blocks.
- At `768px` and below, topbar menus become horizontally scrollable and menu panels use fixed mobile overlays.
- At `680px` and below, pane titles can stack controls vertically and preview padding drops to `1rem`.

## Components

### Brand

The brand mark is a compact document glyph inside an orange-accented square:

- Size: `1.8rem`.
- Radius: `8px`.
- Border: `var(--lens-accent-border)`.
- Background: a subtle vertical orange tint, not a solid orange tile.
- Title: display font, around `.92rem`, bold.
- Tagline: available in the DOM but hidden in the compact topbar.

### Buttons

Base buttons use neutral control backgrounds, `8px` to `12px` radii, and a soft orange focus ring. Primary buttons use an orange vertical gradient and white text. Disabled buttons keep their shape but drop to about `.46` opacity.

Icon buttons are square, usually `2rem`, and should use stroke icons with round line caps and joins. Active icon toggles use `accent-soft`, `accent-border`, and accent text.

### Menus

Menus are dense, scrollable panels:

- Default width: `22rem`.
- Wide create menu: `34rem`.
- Radius: `12px`.
- Padding: `.6rem`.
- Background: a subtle vertical surface gradient.
- Items: full-width, `8px` radius, about `2.2rem` minimum height.
- Headings: uppercase display font, small positive tracking.
- Notes: muted body text, `.78rem`, short and practical.

On mobile, menus become fixed overlays with `.55rem` side insets and a `.75rem` bottom inset.

### Panes

Panes are the app's main structural unit:

- Border: `1px solid var(--lens-border-soft)`.
- Radius: `12px`.
- Header: `2.55rem` minimum height, strong surface background.
- Body: editor or preview background.
- Keep pane headers compact. Put tools in grouped icon controls rather than long text buttons.

### Sidebar And File Rows

The sidebar uses a translucent surface with blur and a panel shadow. File rows are grid rows with:

- `8px` radius.
- Transparent idle border.
- Hover background from control hover.
- Active background: a subtle horizontal orange tint.
- File icon: `1.45rem`, `6px` radius, orange soft background.

Dirty and external-change indicators are small circular dots using warning or accent colour.

### Toolbars

Toolbars are grouped into small bordered sections. Each formatting control is usually a `2.1rem` square. Wide actions may use inline icon plus short glyph text, such as `MMD`.

Toolbars should wrap on normal desktop panes, then allow horizontal scrolling when space is constrained or the sidebar is collapsed.

### Editor

The editor uses the monospace font at `14px / 1.58`, with:

- Background: `--lens-editor-bg`.
- Caret: orange accent.
- Selection: soft orange highlight.
- Line number rail: about `3.5rem`, muted text, slightly darker than the editor.
- Syntax and find overlays mirror editor padding and font metrics.

Do not add decorative chrome around the text area. The pane and toolbar already provide the frame.

### Preview

The rendered Markdown preview uses body text at `line-height: 1.65` and `1.5rem` padding on desktop. Headings use display type. Links use the accent. Inline code uses a soft neutral pill, and blockquotes use a narrow orange left border plus a soft accent background.

Tables, code blocks, details blocks, progress bars, and diagram frames all use neutral borders, `12px` radius, and compact toolbar headers. Mermaid diagrams render on a light diagram canvas so diagrams stay legible in both app themes.

### Dialogues

Dialogues use the same surface, border, radius, and shadow system as panes:

- Modal card radius: `12px`.
- Backdrop: dark translucent overlay with blur.
- Form fields: editor background, `8px` radius, accent focus ring.
- Dialogue actions align to the end on desktop and stack on narrow mobile screens.

Use "dialogue" in user-facing copy.

### Status

The statusbar is deliberately quiet:

- Height: `1.65rem`.
- Font size: `.74rem`.
- Background: strong surface.
- Busy state: accent text plus a small spinner.
- Success, warning, and danger states use the semantic tokens.

Keep status messages short enough to truncate gracefully.

## Interaction Rules

- Hover and focus should change border, background, and text colour together.
- Focus always gets a visible orange ring.
- Active toggles use accent-soft and accent text.
- Pressed controls move down by `1px`.
- Disabled controls keep layout stable and reduce opacity.
- Drag targets use dashed orange outlines.
- Respect `prefers-reduced-motion` for animated status indicators.

## Copy Rules

- Keep first-party UI generic and local-first.
- Use British English: "maximise", "artefact", "dialogue", "colour", "behaviour".
- Prefer concise command labels: "Open file", "Export PDF", "Find in editor".
- Icon-only controls need clear `title`, `aria-label`, and hidden text where useful.
- Avoid describing obvious UI mechanics in the interface itself.

## Source Pointers

- `assets/styles/app.css`: live app CSS. The later "Phase 9" and "Phase 10" sections contain the effective theme and compact shell overrides.
- `index.html`: app shell structure, topbar menus, pane layout, toolbar groups, and statusbar.
- `docs/architecture/lens-docs-studio-identity.md`: product identity and Lens accent rules.
- `docs/architecture/lens-docs-studio-ui-tokens.css`: portable token and component starter export.
