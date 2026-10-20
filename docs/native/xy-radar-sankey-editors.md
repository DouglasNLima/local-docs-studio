# XY Chart, Radar and Sankey source authoring

Expansion III adds these three families to the eleven delivered native editors.
The [fourteen-family matrix](diagram-compatibility.md) describes implementation;
the [engineering receipt](expansion-iii-engineering-receipt.md) owns actual tested
coverage, source revisions, CI, previews and unavailable certification.

The renderer is unchanged: Mermaider **0.15.1**, package source
[`71b64845570d633a94d29e85579c0a02786fece8`](https://github.com/nullean/mermaider/tree/71b64845570d633a94d29e85579c0a02786fece8).
The compatibility gate inspects actual application output rather than inferring
support from the upstream README or current Mermaid website. Charts are ordinary
native SVG conversions; there is no browser, replacement layout or private engine API.

| Family | Supported first slice | Protected or unchanged source |
|---|---|---|
| XY Chart | `xychart` / `xychart-beta`; effective header/body title; horizontal/vertical; category labels and axis titles; increasing finite Y range or automatic; named/unnamed bar/line series; independent finite values; ordered series/value operations; atomic aligned-category operations | Box series; ignored point suffixes; coerced invalid numbers; numeric X ranges; malformed quoting/categories; ambiguous structural comment boundaries; arbitrary directives. Ragged vectors remain ragged and block category alignment changes |
| Radar | `radar-beta`; effective body title; ordered simple axis IDs/labels and curves; positional values; unsigned decimal min/max or automatic maximum; add/remove/reorder curves; atomic aligned axis operations with explicit initial zero | Keyed-looking prefixes, dropped/extra/ragged vectors, repeated IDs for structural changes, unsupported name separators, compound/ignored curve suffixes, legend/graticule/tick source. Cross-declaration axis moves are protected where attachment is unproved |
| Sankey | `sankey-beta`; ordered source/target/positive weight occurrences; local endpoint edits; add/remove/reorder links; explicit global rename across proved endpoints; real node/ribbon selection | Ignored/invalid rows; ambiguous CSV quoting; extra fields; all cyclic/self-link topology edits; rename collisions or unproved global endpoint coverage; invented title directives; new multiline fields |

## Exact source and identity

Shared `SourceText`, `SourceVector` and bounded `SourceDraft` history retain original
line endings, comments, Unicode, configuration, whitespace and numeric lexemes.
Inline occurrences have their own keys which travel with token moves and survive
Undo/Redo. Neither labels nor numeric values identify source occurrences. Changes
patch intended spans or explicitly selected row/vector groups; untouched diagrams
and neighbouring Markdown stay exact. Same-indent immediately preceding comments
attach to rows; ambiguous trailing comments and intervening directives protect moves.

The engine's quoted category/CSV fields toggle quotation and decode doubled quotes.
No unrelated escape convention is introduced. New numeric tokens must be finite
in invariant decimal/exponent syntax; representational underflow is rejected.
Radar min/max directives accept only unsigned decimals, unlike general curve values.
Invalid pending input remains visible, retains the last valid draft and prevents Apply.
Apply reparses and renders the exact draft, then uses the existing owner/version
session to commit atomically. Cancelling never applies private edits.

## Canonical correspondence and limitations

XY bars paint under lines; emitted `data-series` keeps declaration indices. Actual
grid extent, value range and mark geometry prove category/value positions, including
horizontal orientation, negative/zero values and clipping. The renderer clips values
to the display range and gives very short bars a minimum visible length; raw source
is unchanged. Numeric X ranges do not change category-centred mark positions, so this
slice does not offer continuous-X authoring. Earlier axis titles/ranges retain their
actual independent precedence. Selecting automatic Y range explicitly removes all
proved Y range spans, retaining titles and all other declarations.

Radar maps each value by position, even when a token looks like `axis: value`.
Invalid tokens can drop, missing final positions can display as zero and radii clamp.
Original malformed vectors are preserved and protected, rather than padded/repaired.
The actual SVG spoke origin/radius, curve order, polygons and point coordinates prove
supported positional correspondence. Duplicate labels and coincident curves remain
separate accessible list entries. Repeated IDs protect structural alignment changes.

Sankey node identity is trimmed decoded endpoint text. Nodes retain first-appearance
order; ribbon output sorts by actual source layer then endpoint names, with stable
parallel-link order. Correspondence checks real node rectangles, ribbon endpoints
and weight proportions. The engine can omit self-links/feedback ribbons: their bytes
stay accessible but visual topology changes are protected. A local endpoint edit
changes one row; global node rename is a separate selection and cannot merge identities.

## Executable evidence

After a zero-warning locked build of `src/native/LensDocsStudio.Native.slnx`:

```powershell
$validation = 'src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe'
& $validation --chart-compatibility artifacts/native/chart-compatibility .
& $validation --chart-editor-fidelity artifacts/native/chart-editor-fidelity .
```

Compatibility asserts real Light/Dark output, trusted SVG/PNG, ordinary document
preview and HTML/DOCX/PDF exports. Fidelity compares complete native preview/editor
pixels with zero tolerance and verifies exact owner patches and stale conflicts.
Independent UIA uses `--source-editor-ui <exe> <output> Xy|Radar|Sankey --normal`
or `--high-contrast` on disposable Windows runners, with actual OS High Contrast.
Installed-authoring acceptance uses the existing installer/payload harness. A synthetic
source-identical version-pair upgrade does not certify historical upgrades. UIA is
not physical screen-reader certification. Higher DPI and SDK-free clean Windows
remain unavailable unless the receipt records actual supporting evidence.
