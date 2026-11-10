# Advanced Outline and Copy TOC: engineering receipt

This independent Draft slice implements native H1–H6 document navigation and
reliable Markdown TOC copying from the unsaved buffer. It targets internal
personal use. Historical PR #76 is reference only; no whole-PR cherry-pick/merge
was performed, and no slash commands, comparison, new statistics, Mermaid
renderer, general preview architecture or performance campaign was introduced.

## Verified starting state

- Develop: `50bc2354e49662c9831338310d23c91cfe7a7b8a`.
- Complete develop tree: `7685cd6939275d3bef442b3ff1df579616a348aa`.
- PR #84 is merged into that develop state.
- PR #76 is open/Draft at `7b1334b09a02ad2fbfd4a0234798e6c387d3e634`.
- Goal #75 remains open.
- PR #78 remains open/Draft at `23bcbb58b65998af5089b8a4f4e3f4b69356df97`; Goal #77 remains open and unchanged.
- Feature branch: `feature/native-advanced-outline-toc`.

## Source and scope

Candidate `c10d673f9d4955e4fe51cf44850edc54bee708de`, complete tree
`b155fe4a2af0642cd293c0d40525146fd776b132`. Native tree
`15273162a09ed867f8af36c06ed5143fa114619c`; control tree
`d2fc5b47c4dedff87a220ef82bfadc4cba7eaa66`. Final proposal/review packaging may
change the complete tree; the final Draft PR records that exact identity while
the verifier requires these candidate source/document/evidence objects unchanged.

Product paths relative to `src/native/`:

- `LensDocsStudio.Core/Editing/TableOfContentsService.cs` (added).
- `LensDocsStudio.Native/MainWindow.Outline.cs` (added).
- `LensDocsStudio.Native/MainWindow.TableOfContents.cs` (added).
- `LensDocsStudio.Native/TableOfContentsWindow.xaml` and `.xaml.cs` (added).
- `LensDocsStudio.Native/ViewModels/OutlineHeadingViewModel.cs` (added).
- `LensDocsStudio.Native/MainWindow.WorkspaceChrome.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` (changed).

Test paths:

- `src/native/LensDocsStudio.Core.Tests/TableOfContentsTests.cs` (added).
- `tests/native/outline-toc/OutlineTocControls.csproj`, `Program.cs`, `README.md` (added).
- `src/native/LensDocsStudio.Native.Validation/NativePreviewSchedulingChecks.cs`,
  `NativeShellLayoutChecks.cs`, `NativeUiOutlineModernisationChecks.cs` (adapted).

The native TreeView retains existing parsed heading/source navigation, current
section, filtering and group commands. It adds real parent/child accessibility,
individual expansion, complete arrow/Home/End/Enter navigation and per-document
tracked collapse state. Copy TOC provides ranges, bullets/numbered/plain styles,
normalisation and details wrapping. Parser identifiers, duplicate disambiguation,
Unicode labels and exact export targets are preserved. Formatting escapes label
syntax and URL-encodes fragments. Source-version, identity, parser-mode and
cancellation guards prevent stale display/copy; the single worker gate and size
limits bound work. Clipboard errors are recoverable. No Save or linked-asset read
is part of copying. Existing Find, selection, zoom, reading mode and preview
ownership services remain in place.

## Local evidence

The [focused validation receipt](../../../native/native-outline-toc-validation.md)
and [original evidence hashes](../../../native/evidence/outline-toc/results.json)
record the full outcome and intermediate failures.

| Scope | Final observed result |
| --- | --- |
| Core | 116 PASS, zero FAIL/SKIP |
| Shown WPF/UIA feature controls | Light 60 PASS; Dark 60 PASS |
| Native modal Tab/Escape | 4 PASS |
| Loading / offline loading | 106 / 11 PASS |
| Continuous reflow / Pages | PASS / 31 PASS |
| Statistics / Find / selection | 48 / 48 / 32 PASS |
| Native Flowchart / scheduling / static | 19 PASS / PASS / PASS |
| Accepted baseline governance | 89 PASS; approved chain and original inventory preserved |
| Actual Windows High Contrast | SKIPPED / NOT RUN; existing transition guard requires a disposable hosted desktop |
| Hosted CI, physical input/screen reader, release certification | NOT RUN |

Product/control builds have zero warnings/errors after the recorded development
corrections. Tests were run from the candidate's working source before its commit;
the build source metadata therefore identifies the starting commit. Canonical
native/control source objects are pinned by this candidate; later changes were
limited to the disclosed scheduling observer and separate dismissal control,
both rebuilt and tested. No binary is published as part of this work.

Original failed observations are retained. In particular the intermediate
clipboard mismatch has **UNESTABLISHED cause**: the observer did not capture its
Copy status. Subsequent exact-copy/locked-clipboard passes do not retrospectively
explain it. No production retry or relaxed acceptance assertion was added.
Physical presentation/HC and exhaustive arbitrary-clipboard-format certification
are not claimed by these bounded local controls.

## Governance and next decision

Proposal `advanced-outline-toc-eleven-v1` remains INACTIVE. Its canonical committed
manifest SHA-256 is
`acff902eec465c6fb56c9dfca7943d0beb61f1029e55e9842d69d0efd4bbbcf2`.
It pins eleven exact protected substitutions and 88 added candidate files, while
preserving existing approved substitutions, MIT allowlist and R3–R7 exclusions.
The [proposal verifier](validate-proposal.py) and its negative controls make no
licensing or administrative change. Ordinary active source-identity governance
must remain failing until owner approval. See [validation results](validation-results.json)
and [independent read-only review](independent-review.md).

The next owner decision is solely whether to activate those exact provenance
substitutions and successor identities. Hosted qualification and merge require
separate authorisation; all 30 mandatory develop contexts still apply. Preparation
commits use `[skip ci]`. No workflow dispatch, merge, signing, tags, release,
deployment, updater publication or binary distribution is authorised. PR #76 and
Goal #75 remain open; PR #78 and Goal #77 are unchanged. Historical Continuous
250 ms remains FAIL; no exception transfers. ADR 007 is inactive. Public release
and Native Stable remain NO-GO.
