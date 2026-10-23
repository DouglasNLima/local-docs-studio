# Post-merge .853 PDF lifecycle repair receipt

Status at source freeze: **INCONCLUSIVE historical RCA; independently proved CI
policy divergence corrected; targeted diagnostic instrumentation implemented.**
The Draft PR's final receipt owns the exact clean repair SHA/tree, executed gates,
attempts, package checksums and later replay results. A source-branch success cannot
replace the failed develop acceptance or prove an unobserved historical mechanism.

## Original evidence, preserved before source changes

Actual remote develop was `358147cd6da2265efb49e4922698796f9399fcf6`, tree
`d37a5701bd01ed52d07f7ab1a8e85eacff5d3f7b`. PR #44's reviewed source
`2cd7e41938e98a05dcf3f73e7934c224a06591f8` has the same tree. The repair branch
starts from that actual develop in an isolated managed worktree. The ordinary
checkout stays on main; existing attachments and all other worktrees are preserved.

- [Original failed native run 39856703884, attempt 1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39856703884),
  [job 120096656823](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39856703884/job/120096656823).
  No retry was requested and its failed source attribution is unchanged.
- [Original .853 archive 12460044301](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39856703884/artifacts/12460044301):
  339,027,263 bytes, SHA-256 `1249e817e676e3fba9fa996587fa8c6e4f49bfba05f371e5d1c092a7aa6be49f`;
  reported expiry **5 November 2026, 21:44:58 UTC**.
- [Accepted feature run 39836641315, attempt 1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39836641315)
  and [its .852 archive](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39836641315/artifacts/12453122541)
  remain separate evidence. The original archive checksum was independently verified.
- [Merged web push 39856703893](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39856703893)
  passed Chromium, Edge and aggregate; Pages was skipped.

Both compressed archives, raw decoded job logs, API run/job/step/artifact/PR
responses, complete member hash inventories, package comparison and the pre-change
RCA are retained in `C:/Code/lens-native-853-evidence/original-run-39856703884-attempt-1/`.
The [compact provenance inventory](native-853-original-evidence.json) records the
original bytes/hashes and capture paths. Inspection extracted diagnostic files only;
no original binary or installer was executed on the developer desktop.

## What the original failure proves

The failing call was the after-upgrade `NativeUiChecks.cs:805` combined predicate:
`File.Exists(codePdf) && ExportCompleted(window, codePdf)`, through the original
20-second `Wait` at line 1799. The exception establishes only that this combination
was not observed. The prior DOCX assertion does not prove PDF acceptance.

The complete after-upgrade `native-ui.json` records 45 earlier checks. Its
`native-save-dialogues.json` records the labelled filename field accepting
`coloured-code.pdf` with keyboard focus. It does not record the chosen directory,
resolved final path or dialogue closure. The failure screenshot below is copied
byte-for-byte from the original archive:

![Original .853 after-upgrade failure capture](images/native-853-after-upgrade-failure.png)

At capture time it visibly shows **Export completed — coloured-code.pdf**,
**Open file** and **Show in folder**. Both UIA control dumps contain only TitleBar
and SystemMenuBar; the window inventory contains one app window. No error or Save
modal is visible in that screenshot. A 33-thread x64 thread-information minidump
exists; its streams/stacks do not provide a full managed heap or export receipt.
Static dump string inspection found no PDF destination.

The installed-phase upload did not retain `coloured-code.pdf`, an output-directory
inventory or application export-stage logs. Absence from the archive is therefore
**not evidence of file absence on the runner**. Exact output path, size/hash/header,
UI state during the deadline, foreground ownership and process exit state were not
independently recorded. No crash, exporter exception, cancellation, corruption,
association causation or sole UIA cause is established by the available record.

## Product trace and comparison

`MainWindow.ExportDocument` collects the actual Save result, snapshots the source,
awaits `DocumentExportService`, then `ExportFileStore.WriteAsync`, then
`WorkspaceViewModel.CompleteExportAsync`. `SavedExport.FromSavedBytes` verifies the
selected file exists with the expected length before completion status/actions are
published. `RunAsync` retains cancellation feedback and displays real exceptions.
The captured completion text supports completion at some selected path at that
later instant. It does not establish that path, its content, or observations
throughout the preceding deadline. No exporter algorithm correction is justified.

Both jobs used `windows-2025-vs2026` image `20261018.275.1`. Each upgraded payload
contains 528 files; 523 hashes match, including renderer/runtime dependencies.
Five version/source-bearing entries differ: Core DLL, Native DLL/apphost/deps and
`version.json`. Source-tree equality must not be described as binary equality.
Feature lifecycle duration was 16m29s; the failing develop lifecycle was 8m35s and
stopped during after-upgrade. They did not complete equivalent work.

The original installer receipt verifies both installed payloads against their
portable packages and preserves 22 state files plus workspace bytes through
upgrade and restored-session validation. Feature .852 passed its complete cycle.
**Both .852 and .853 version-pair upgrades are SYNTHETIC same-source upgrades,
not historical preview migration.** Replacement pairs have the same limitation.

## Separate bounded corrections

1. `native.yml` now declares `internal-no-associations` for its internal acceptance
   job purpose. Central arguments supply both visible wizard and every lifecycle
   harness, including all twenty installed families and the separately guarded
   historical job. Develop pushes, feature PRs and manual acceptance use the same
   switches; branch allowlists no longer select optional tasks. Existing absence
   checks remain. The released installer defaults and association implementation
   are unchanged. A static contract check covers all seven command sites.
2. The coloured PDF check still invokes the real Export menu and Save dialogue
   once, and uses the original 20-second deadline. Independent disk/UI samples
   retain target existence, length/last-write, actual status, action visibility,
   enabled state and exact paths, plus observation errors. File presence alone,
   a wrong destination or a missing completion action cannot pass. Actual PDF
   header, selectable `Café😀🚀`/`keepIndent();`, editor/saved source and all other
   existing PDF assertions remain mandatory. Other export waits are unchanged.
3. Diagnostic evidence adds exact entered filename and native dialogue state on
   Save return, final process/exit/responsiveness, Win32 foreground/owner/windows,
   bounded output inventory and file hash. The opt-in, bounded local application
   journal records accepted destination, preparation, bytes written, completion,
   cancellation/error stages and model status without document text. It rethrows
   the original error to the unchanged user-facing handler. Generated PDF/DOCX,
   observation JSON and journal now survive installed-run uploads.

The product remains .NET 10/WPF with Mermaider 0.15.1 and stock Continuous. All
twenty authoring families, saved-source fidelity, fixture bytes, history/resource
limits and timing thresholds are unchanged. No new Mermaid family is implemented.

## Targeted controls and source-labelled validation

| Scope | Required evidence |
| --- | --- |
| Deterministic observation controls | Actual native PDF generation/file write/header/selectable Unicode; missing/wrong/empty output; incomplete/error/cancelled feedback; missing actions; absent/stale UIA; unrelated provider error propagation; real exporter exception; persistent failure at a bounded test-only deadline |
| Ordinary native acceptance | Locked build, 2,009 Core tests, 166 corpus/export cases, existing fidelity/compatibility, actual UI, package integrity and 18 existing negative controls |
| Installed repair-source acceptance | Visible excluded-association wizard; install/synthetic upgrade/after-upgrade PDF/uninstall/reinstall; all 20 families × three phases = 60 |
| Web | Static integrity plus Chromium/Edge smoke; Pages skipped |
| Original .853 replay | Manual `native.yml` input `replay_original_853=true`; fixed original package hashes, original product SHA and separate instrumented harness SHA; disposable included/excluded association matrix; no original package rebuild or republication |

The original replay is diagnostic and cannot stand in for repair-source acceptance.
Its association-enabled path is an explicit reproduction control; ordinary
acceptance always excludes associations. A successful replay cannot rewrite the
original failure or establish an unobserved historical cause.

Development locked build passed with zero warnings/errors and the new observation
controls passed. One development Core run retained two regex-budget failures
(Architecture node ordering and invariant chart numeric spelling): 2,007 passed,
two failed. Their Core sources/fixtures are unchanged; no budget, retry or expected
result was changed. Final-source gates must be recorded separately, with every
failure retained. Provider rejection controls are labelled injected observations;
they never substitute for genuine installed file/menu/Save/UIA evidence.

## Handoff and remaining certifications

[PR #38](https://github.com/DouglasNLima/local-docs-studio/pull/38) remains open,
Draft and PAUSED at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`. Its worktree and
earlier packages/evidence are untouched. The repair branch preserves the paused
benchmark skip while running ordinary/High Contrast correctness; the historical
250 ms deadline is unchanged. Earlier .852/.853 archives retain their hashes.

Higher-DPI interactive testing, a physical screen-reader session, SDK-free clean
machine installation and production-trust certification remain unavailable and
unclaimed. No signing, release, tag, updater publication, Pages deployment,
ordinary desktop install or merge is authorised by this repair.

**Post-merge verification STILL REQUIRED:** only after separate user authorisation
and merge must the new actual develop SHA pass both native and web push workflows,
including the full installed lifecycle and all family gates. Expansion VI stays
blocked until that evidence is inspected. The final decision must also preserve
the historical RCA's remaining unknowns; a green branch badge cannot resolve them.
