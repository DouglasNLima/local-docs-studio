# Native desktop UX refinements — engineering receipt

## Source and scope

Remote `develop` was fetched and independently verified with `ls-remote` on 27 October 2026 before branch creation. Base commit: `0df86ad2bdae005d49be68096c2f784b49de8109`; base tree: `05d534030761a7b68a8579bb33e4dfb50161ce90`. Branch: `feature/native-desktop-ux-refinements`. Isolated worktree; no changes taken from paused Draft PRs #38 or #50. The Draft PR records final source/tree identity and hosted outcomes.

## Interfaces and implementation slices

- Caption: `CaptionThemeService` registers native windows through `WindowStateService`. `ThemeService` supplies the effective application preference. DWM owns the caption buttons, Snap and accessibility. Explicit caption/text colours reset to system defaults during High Contrast. Startup reads the validated saved theme before showing the first window. Unsupported DWM attributes retain system behaviour. No registry writes or window recreation.
- Find: `FindMatches` indexes immutable source snapshots on a worker; `PreviewFindIndex` maps bounded chunks of rendered text across adjacent inline runs back to exact FlowDocument pointers. Formula source has dedicated mapped entries; Mermaid labels are excluded. The toolbar debounces queries for 180 ms, invalidates immediately on source/document changes, cancels stale work and selects the first occurrence without moving focus out of Find. Enter/F3 move forwards; Shift reverses; navigation wraps. Replacement recalculates at the next source anchor. Limits: 8 Mi characters, 100,000 matches, 1,024-character query and 200,000 preview segments. Limit failures show no partial count and explain the limitation.
- Pan: five-DIP movement threshold separates selection from left drag. Middle drag and Space+drag remain available; release, capture loss, deactivation and Escape cancel capture. Scrollbars and toolbar controls retain their input. No node movement or editing.
- Inspection: `DiagramInspection` accepts only verified canonical ER/Flowchart result geometry from the existing presentation services. Geometry is prepared once on an STA worker when opening the viewer. Drawing-only callers remain supported. Overlapping ambiguous hit targets fail closed; the accessible selector identifies exact occurrences, including parallel/self relationships. Direct neighbours are emphasised; unrelated canonical content remains unchanged. The overlay clips strokes around entities and labels. Persistent selection has a distinct outline, and survives viewport changes. Limits: 512 entities and 2,048 relationships; unsupported/unverified metadata disables inspection.
- Zoom: main preview percentage binds directly to the reader's zoom, resets to 100% on activation and persists via the existing preference store. Diagram zoom remains independent.

## Local validation and evidence

Implementation and local acceptance are complete for the available desktop. Hosted UIA, actual High Contrast and package/lifecycle outcomes are reported on the Draft PR; they are not implied by local passes.

- Release solution build: zero warnings/errors. Core: **2,222 passed**, none skipped.
- Full native corpus: **190 diagram and export checks, zero failures**. Includes unchanged Mermaid-family outputs, DOCX schema validation, PDF/HTML/Docs Site exports and shell layout at five sizes with 100/125/150/200% raster checks.
- Quadrant, chart, workflow, topology and Expansion VI compatibility/editor-fidelity gates, plus source-editor fidelity, all pass. The 108 previous source fixtures and 24-family UX audit remain unchanged; catalogue checks pass.
- Compiled Find routing and dynamic resource/owner checks pass. The unshown DWM readback reports unsupported; shown screenshots establish caption appearance independently.
- Focused [compiled regressions](evidence/desktop-ux/compiled-checks.json) cover search snapshots, formatted runs/formulae, cancellation, replacement invalidation, wraparound, zoom, exact ER and Flowchart correspondence, parallel/self relationships, unavailable metadata and unchanged canonical pixels.
- Web static integrity passes; **270 Chromium/Edge tests pass**. Native export browser checks: 11/12 initially passed; Chromium closed while taking a screenshot. Both disk/static variants of the affected test passed unchanged on a targeted rerun. The failed trace/log is retained under ignored local artefacts.
- The earlier shell fixture assumed the old Next caption and detached the XAML namescope. Its checks now use stable AutomationIds and retain the actual namescope; bounds and accessibility assertions remain. An initial details-latency outlier (278.8 ms against 250 ms) during concurrent work did not recur in the sequential complete run. No threshold was raised.

Actual native application observations used isolated state and synthetic fixtures. A byte-identical apphost with a distinct filename avoided a computer-use helper confusing the development process with the user's installed application. The installed application was neither changed nor closed.

| Runtime observation | Evidence |
|---|---|
| Baseline dark content with light system caption | [Baseline](evidence/desktop-ux/baseline-dark-caption.png) |
| Dark, Light and effective Windows theme | [Dark](evidence/desktop-ux/dark-caption.png), [Light](evidence/desktop-ux/light-caption.png), [Windows](evidence/desktop-ux/system-theme.png) |
| Editor Next/Previous; 25 source results | [Next](evidence/desktop-ux/find-editor-next.png) |
| Preview count 5; selected third occurrence spans formatted runs, query retains focus | [Preview Find](evidence/desktop-ux/find-preview-visible-selection.png) |
| Replacement reduces 25 to 24; Undo restores 25; new tab clears count | [Replacement](evidence/desktop-ux/find-replacement-24.png) |
| Main zoom 110%, reset to 100%, no document render for the label | [Zoom](evidence/desktop-ux/main-preview-zoom-110.png) |
| Hover identifies a verified ER relationship without dragging | [Hover](evidence/desktop-ux/er-relationship-hover.png) |
| Contact's direct connections in 22 entities/68 relationships; selection survives 720-DIP horizontal pan | [Selection](evidence/desktop-ux/er-22-68-contact-selected.png), [After pan](evidence/desktop-ux/er-22-68-after-pan.png) |
| Actual PNG/SVG save dialogues with and without highlighting produce identical bytes | [Manifest and hashes](evidence/desktop-ux/manifest.json) (canonical files retained under `artifacts/native/desktop-ux/runtime/`) |

The live run also exercised the small parallel/self ER fixture, 250% diagram zoom, 320-DIP vertical left drag, Escape, re-opening windows with saved preferences, preview regeneration after replacement and returning from an empty tab. A runtime finding (the inner WPF viewer hid selection while Find retained focus) was corrected and rechecked visually.

## Responsiveness and remaining certification limits

Final measured canonical ER rendering: **1,140 ms** (22/68) and **1,253 ms** (13/46). Opening the inspection metadata adds **1,078 ms / 866 ms** on its worker, once per viewer. Pointer geometry is flattened/cached once; 1,000 hit queries at 25% zoom took **14.6 ms / 8.6 ms**, with **32,064 allocated bytes** each. These are local observations, not hardware-independent performance guarantees. The original probe accidentally included whole-drawing bounds recomputation inside every sample; the final probe caches the drawing dimensions and measures hit testing separately.

The same four-diagram document rendered successfully on clean baseline and modified native builds, with **no rendering deadline warning** in either. Whole-document initial previews observed 7,388 ms and 7,577 ms respectively; these are not controlled per-diagram benchmarks. The three-second renderer deadline and pinned rendering dependencies are unchanged. The actual reported SPC Operations source/screenshot was not supplied, so its specific timeout remains unverified. Representative fixtures do not establish that it is fixed or inherited.

Physical screen-reader use, real higher-display-scaling transitions and local OS High Contrast transitions are not certified. Existing raster/layout checks cover multiple scales; the new actual High Contrast/UIA suite retains the repository's disposable hosted-Windows guard. Local installation/lifecycle testing is deliberately left to that prescribed pipeline because this development machine has an existing installation. No merge, signing or production publication is authorised.

## Reproduction

Run `dotnet build src/native/LensDocsStudio.Native.slnx -c Release`, the Core tests, and `dotnet run --project src/native/LensDocsStudio.Native.Validation -c Release --no-build -- --desktop-ux <fresh-output> .`. Existing `--find-routing`, `--theme-resources` and the complete native corpus remain independent gates. The workflow additionally runs `--desktop-ux-interactions <executable> <fresh-output> .` with normal and actual High Contrast desktops on disposable hosted Windows. It reuses the existing UIA/clipboard harness and does not relax host safeguards.

## References

[DWM theme integration](https://learn.microsoft.com/windows/apps/desktop/modernize/apply-windows-themes) and [documented caption attributes](https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute). Use native system caption defaults when attributes are unsupported.
