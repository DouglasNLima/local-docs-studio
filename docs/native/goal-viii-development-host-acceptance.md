# Goal VIII — isolated Windows development-host acceptance

Recorded 24 October 2026. Local Markdown, Mermaid, and documentation studio.
This goal explicitly permits bounded portable testing on the existing development
computer. Development tools are metadata, rather than a reason to refuse that
qualification. This is **DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION**. It is not
clean Windows, an independent machine, SDK-free certification or production readiness.

## Frozen source and independent package provenance

Before editing, remote `develop` matched
`9b1e73bbf564c53b48149e8c0dcd6fd7acbe308f`, tree
`1fb777317ad6e5c8531eebcb565fefae4227e354`, the verified merge of
[PR #47](https://github.com/DouglasNLima/local-docs-studio/pull/47).
The original checkout remains at `0d100cc29dde2ba80755fb1e8af60ffd9d006b16`;
its untracked attachment directory and unrelated worktrees were preserved. Work is
on the separate `feature/native-windows-development-host-acceptance` worktree/branch.

| Frozen remote check | Result | Attribution |
|---|---|---|
| Native post-merge | PASS: 25 successful jobs, two deliberate historical skips | [40108034236](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40108034236), push, attempt 1, exact frozen SHA |
| Web post-merge | PASS: Chromium, Edge and aggregate; Pages skipped | [40108034406](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40108034406), push, attempt 1, same SHA |
| Original Actions archive | PASS: API digest independently matches all 342,267,159 downloaded bytes | Artefact `12566795323`, `b46c44916cb2ffb4c52c7345a72b3665e15cbc0af50342c7da016532c9f4514e` |
| `.871` portable ZIP | PASS: independent sidecar/source/tree/binary inspection; 528 paths, unsigned, exact Core/WPF 10.0.13, no forbidden payloads | SHA-256 `a56e95e450cf9ca36d50199ee8ad825c5e701c4cf5e35170a47da2d9b269babc` |
| `.871` installer bytes | PASS: read-only inspection only; never executed here | SHA-256 `ef2320c6efdfe344d7a39190e3cb4187b15377b1448b3452a0f9599266fbd2bb` |
| Original baseline gates inside archive | PASS: Core 2114/2114; 25/25 package rejection controls; corpus/export receipts retained | Original source-labelled TRX, JSON and images; HOSTED evidence |

The artefact expires **7 November 2026, 16:10:15 UTC**. Original archive, extracted
sidecars, immutable package bytes, binary inspection and full payload manifest are
retained under the ignored baseline evidence root. No hash is inferred from a name.
The executable SHA-256 is
`b34f52da1cb4d22c295b034d890229c950c51e1874c41c7d1e66aea621be5147`.

Final feature-source SHA/tree, native/web push and PR runs, attempts, conclusions,
skips, preview versions/hashes/expiry, independently inspected candidate payload and
complete baseline delta belong to the Draft PR's **post-commit engineering receipt**.
That head is distinct from integrated develop above. Keeping that receipt outside
the commit avoids pretending that a document contains its own final hash.

## Actual host qualification and isolation

| Observation | Result / boundary |
|---|---|
| OS | PASS: Windows 11 Home, x64, build `26300.9711`; recorded OS version `10.0.26300` |
| Execution token/session | PASS: non-elevated, interactive session 3, active console, Default input desktop accessible; physical human presence is not certified |
| Development tools | PASS as development-host metadata: .NET SDKs, Node, Python and Git present; local SDK command reports 10.0.401 |
| Unchanged clean-machine preflight | BLOCKED: development tools/SDKs detected. Original script/result retained; clean qualification NOT CERTIFIED |
| Existing product surface | PASS observation: no matching native/legacy uninstall registrations or Lens shortcuts detected; existing native private state contains four files and is preserved by hashes |
| Associations | PASS observation/preservation: `.md`, `.markdown`, `.mmd`, `.mermaid`, per-user/machine class keys, OpenWith/ProgIDs and UserChoice-related data retained locally; never written/restored |
| Local installer | NOT RUN — UNSAFE SHARED REGISTRATION: fixed AppId/uninstall key, Start Menu group and default private state; custom install directory does not isolate them |
| Real display | PASS observation: actual native HWND DPI `144`, effective window scale **150%**, window awareness 2. Observer awareness 0 virtualises monitor DPI to 96; DWM physical-frame bounds are recorded separately |
| 100% / 125% / multi-monitor | NOT RUN: no automatic scale, monitor, resolution, taskbar, log-off or restart changes authorised/performed |
| Local OS High Contrast | NOT RUN: personal OS theme/settings never toggled; retain separately labelled HOSTED actual High Contrast suites |
| Physical screen reader | NOT RUN: no human listening session/transcript; UIA metadata cannot substitute |
| Policy | PASS for observed portable execution without elevation/trust bypass; policy inventory is limited read-only metadata, not a complete policy audit |

The prepared sandbox is constrained to
`<worktree>/artifacts/native/development-host/<package-source-SHA>/<fresh-short-session>`.
The path must be absolute/local, canonical, strictly within this ignored task root,
with no existing destination, streams, traversal, junctions or symbolic-link escape.
Windows binary-resource inspection needs a short path; the harness reserves its
MAX_PATH budget before creating evidence. Installed/default-state/workspace paths
are never accepted as a sandbox. Every payload file is checked against the complete
independent manifest before launch. Wrong source/tree/hash, stale or incomplete
receipts, unsafe window identity and changed observed host state fail closed.

`App.xaml.cs` consumes `--state-directory` before constructing instance ownership,
`SessionStore`, recovery and workspace services. `StateDirectory` routes preferences,
session/history/UI state, snapshots, recovery, drafts, content library, snippets and
Word templates. The instance pipe is hashed from that explicit state root. There is
also a shared, transient installer-presence mutex; it is not an installed-product
registration. Synthetic preferences disable automatic update checks. No actual
recent-file links, private templates, personal Markdown or cloud documents are used.

Initial synthetic files cover LF/CRLF, Café😀🚀, duplicate/long headings, a local PNG,
relative document link, lists/tables/details and standalone valid/invalid Mermaid.
Their complete hashes precede app mutations; each UI scenario additionally retains
its source/preferences baseline. All ordinary product C#/XAML, Core, renderer,
dependencies, locks, installer, web assets and original test fixtures remain unchanged.
New C# belongs only to the existing independent validation project.

## Host-local observations and their limits

| Local scenario | Result and source attribution |
|---|---|
| First actual portable launch | PASS: `.871` at frozen SHA, PID 82472 / HWND 10816694, synthetic folder/file opened; real native image and Mermaid preview, Unicode and outline visible |
| About | PASS: actual dialog identifies `0.1.1-preview.871` and full frozen SHA; `manual/about.json` and unchanged screenshot retained |
| Quick Open | PASS: Ctrl+P, `relative` filtering, one real result and Enter opened exact `relative.md`; actual modal/tree receipts retained |
| Command Palette | PASS: Ctrl+Shift+P exposes 54 real commands; cancellation returns to owner; actual modal screenshot/tree retained |
| Close cancellation/save | PASS: actual synthetic dirty-close dialog cancelled once, retaining the edit; later Yes saved the synthetic buffer and closed normally. A manually delivered Return had inserted a leading LF in this synthetic buffer; its saved delta is retained, not attributed to a product defect |
| DPI and real sizes | PASS measurement: 144-DPI HWND; UIA bounds 1320×900 and 1920×1200 physical, corresponding to 880×600 and 1280×800 logical. Venn's actual pair was visually reviewed. Full visual matrix is NOT RUN: ten large captures have incomplete frames and cannot certify appearance |
| Complete local family run | PASS: `s06` has 27 completed entry cases across all 24 families, including C4's extra views and TreeView box mode; portable source is the frozen SHA, client `6075adbf54aae4eae69b912118b202f459cc715a`, tree `4c98136dfcf83da568410b8ad39c579a97d9cb25`. The later shell failure does not erase or promote these individual entry receipts |
| Shell acceptance | BLOCKED pending an accepted complete run: `s08` passed folder/image/Unicode, exact Quick Open, pointer relative link and palette, then FAILED exact Unicode selection after Find/Enter. `s07`/`s11` stopped at the foreground guard. Later runs must have their own receipts; no unperformed owner/viewport/restart step is counted |
| Independent owner/library checks | PASS in `s16`, client `d10ee0326c41da8575031164118c6e728ec3530c`: actual private Apply, one-step owner Undo/Redo, Ctrl+S and native template/snippet saves. Find remains FAIL. The run then stopped on a proved harness expectation error after explicit Cancel; remaining stale-owner/viewport/restart checks are NOT RUN in this session |
| Native export APIs | PASS in `s11`, API source `1de34f87b92b101398e62922e8c7f6bc8e269130`: real DOCX/PDF/HTML, canonical `.mmd`/SVG/PNG and four-page/two-diagram Docs Site; zero site warnings. Independent PDF text/colour/page, DOCX image, PNG dimensions and ZIP inspection retained. Portable export dialogues NOT RUN |
| Host preservation | PASS after completed sessions `s02`–`s09` and `s11`: observed associations, registrations, shortcuts and all four default private-state file hashes unchanged. Synthetic state/source changes retained. `s10` refused transient uncommitted preparation before creating its sandbox |
| Guard tests | PASS: 54 controls at `84fc659c8059a1c422edb6d33dab0950d099df95`, including fresh/expired/future timestamps, private-output refusal, wrapped registry-name round trips, real/dangling junction rejection, wrong source/tree/PID/HWND, changed defaults, specialist IDs, direct-client bindings and retained required failures. Final exact-source CI retains its own count |
| Local build/Core | PASS: committed harness build zero warnings/errors; sequential Core 2114/2114 at `da6e623a7fc592d121fa8928102e744a81a6d7e7`. Final-head CI remains independently required |
| Static web integrity | PASS: 50 modules, 57 shell assets, 150 vendor assets and 56 dependency scans; original service-worker/offline contract retained |
| Local Chromium / Edge | PASS: 135/135 each, 270 total in 25.2 minutes; `web-local-f508b82.log`, web assets unchanged from client source `f508b82b556e18dff4f0bc0dc2610b9293b608ec`. Final feature workflows remain separately required |

Local matrix cases are **24 families plus two extra C4 views and TreeView box mode**,
not 27 families. Original five specialist windows remain distinct from nineteen
shared-shell families. Each case uses a real standalone file, normal Visual Edit
entry, exact private source, actual keyboard occurrence selection, private validation,
Cancel, owner/source preservation and close/reopen, with actual-size captures. Packet
multi-row/+count, TreeView indentation/box syntax, zero-value Treemap keyboard targets,
Venn overlap/size/union source and C4 Context/Container/Component remain attributable.
This bounded entry acceptance does not relabel the hosted complete mutation suites.

The additional shell path tests real Find contexts/Enter, relative-link pointer
activation, private Apply, one-step owner Undo/Redo, dirty private Cancel, stale-owner
rejection, pointer pane resize, zoom/Fit/pan, invalid source, normal save/restart and
close workspace. A missing/failing completion means FAIL/BLOCKED, never an inferred
pass. Inspector drag persistence remains NOT RUN in this additional local shell.

Personal-host shell Open/Save dialogues remain **NOT RUN**: Windows recent-folder
history is shared outside the test state directory. No such dialogue is invoked by
the safe path. Real DOCX/PDF/HTML/PNG/Docs Site files are instead generated using the
native export services in the validation client, under the synthetic root. Its API
assembly source/version and Native/Core binary hashes are recorded separately from
the downloaded portable app's source. This is **HOST_LOCAL_NATIVE_EXPORT_API**, not
portable GUI export-dialog acceptance. Independent DOCX/PDF/PNG/HTML/ZIP inspection
must accompany it. Hosted GUI export/wizard/lifecycle gates remain mandatory.

The export API client is framework-dependent: `s15` records actual **.NET 10.0.12 /
WPF 10.0.12**, client source `68b20fe78ba913dbb2034d7f029049b2b29ebdf1`, PID 86112,
with Native/Core binary hashes. This is additional local API evidence, not a claim
that the portable runtime was used for those API calls. Actual portable processes
load the independently verified bundled Core/WPF **10.0.13**; `s06` module receipts
prove their paths and bytes. No SDK/runtime was installed or upgraded on this host.

The `s11` PDF is one real page, SHA-256
`8109f99b0f8e53f396d42a5338fc716bbf74a87bab9995270b35c697b32e5220`.
Independent parsers recover exact `Café😀🚀` and code text, with four character
colours. The saved diagram PNG is 471×149 pixels; DOCX relationships contain two
images, with exact canonical diagram bytes and matching fixture-image pixels.
The initial diagnostic looked only under `word/media/`; its zero count is preserved
and superseded by the actual package-root `media/` relationship inspection.

Inspected, privacy-safe local screenshots and the redacted receipt are in
[`evidence/goal-viii/`](evidence/goal-viii/host-summary.json). These are actual local
desktop/API observations. They are not hosted screenshots or offscreen UI renders.

## Retained failures and classification

No shipping product defect has been established and no shipping fix is included.

| Case | Classification / evidence / handling |
|---|---|
| Initial Core 2113/2114; one Requirement render deadline | UNPROVED / environment observation: existing three-second protection fired during overlapping build/test activity. Original TRX/log retained; sequential 2114/2114 passes. No timeout/resource-limit change; no historical RCA inferred |
| Overlapping build/test and client probe copy warnings | TEST HARNESS orchestration: retained MSB3026 logs. Subsequent build/check phases run sequentially; final build requires zero warnings |
| Windows version resource at 261 characters | TEST HARNESS / environment limit: identical verified DLL bytes return no version there and correct version at a shorter path. Preserve failed folder; reject excessive path length before preparation |
| Inherited StrictMode in existing verifier | TEST HARNESS: optional runtime JSON fields were rejected by inherited scope. Invoke the unchanged verifier in its supported fresh PowerShell process; all identity/runtime checks retained |
| PowerShell JSON UTC date conversion | TEST HARNESS: automatic DateTime conversion plus culture-dependent reparsing refused launch. Handle typed UTC dates/invariant strings; original marker unchanged |
| Association snapshot mismatch before launch | TEST HARNESS: wrapped registry names were canonicalised as objects; retained snapshots differ in representation, not registry values. Scalar canonicalisation and regression test added; never restore registry |
| Flowchart initial Apply disabled | TEST HARNESS: unchanged draft is correctly Ready with Apply disabled. New local check preserves that protective contract; existing suites untouched |
| Capture of controls without AutomationId | TEST HARNESS: optional diagnostic property unsupported. Record absent optional metadata without treating it as a product failure; required action/source controls still fail when absent |
| Exit metadata from an attached process | TEST HARNESS: UI completed, but exit code could not be obtained after the attached process closed. Use the existing separately owned started-process/Attach pattern |
| RPC failure observing closed editor | ENVIRONMENT / UIA observation, cause UNPROVED: raw COM error and partial captures retained. New host path enumerates real HWNDs by test PID and observes closure through captured HWND; no desktop-wide UIA search or uncertain mutation replay |
| Sequence navigation lookup | TEST HARNESS: the specialist uses `SequenceInteractions`, rather than the shared-shell `Elements` suffix. Retain the failed actual Sequence screenshot; explicitly map its existing contract and guard all five specialist navigation/source/validation IDs |
| Quick Open fuzzy matches | TEST HARNESS: `acceptance-lf.md` correctly also returns nearby fuzzy match `acceptance-crlf.md`. Retained actual two-result screenshot; require exactly one matching file identity, select it explicitly and still require exact opened source. No product search semantics change |
| Foreground focus refused | ENVIRONMENT LIMIT: `s07` stopped before keyboard input because the native window could not acquire foreground focus within the unchanged deadline. Retain failure; a fresh session may use explicit Computer Use activation of its verified real HWND. Never bypass policy or count unperformed shell checks |
| First image UIA lookup in `s12` | ENVIRONMENT / observation, cause UNPROVED: one direct read-only lookup timed out before the first accepted shell check. Source/exports and raw COM failure remain. Put this pure lookup inside the existing bounded observation routine, retaining its original deadline; no action is replayed |
| Find/Enter in `s08` | UNPROVED: exact selection assertion failed. Actual pointer Find Next selected `Café`; keyboard delivery/focus has not yet established the requested result. Preserve raw failure and manual observation. The new harness observes query/focus and avoids reactivating the owner while Find's separate focus scope is active; a later run must prove the outcome before acceptance |
| Find rechecks `s14` / `s15` | FAIL / cause UNPROVED: exact Find/Enter acceptance still failed; `s14` additionally retained a UIA TextPattern timeout. No shipping defect or fix is claimed. The source-preserving independent continuation retains this required failure and cannot report an overall shell PASS |
| `s15` diagnostic receipt collision | TEST HARNESS: the failure result and its screenshot metadata both requested `find-failure.json`; CreateNew refused the second write and stopped before independent owner scenarios. Preserve both the original failure and collision trace; use distinct result/capture names. No owner mutation occurred |
| Explicit Cancel in `s16` | TEST HARNESS: `MermaidVisualEditorWindow.Cancel` sets `discard = true` and closes intentionally, so no confirmation is expected. The retained window list has only the owner. The new harness's Yes-dialogue expectation was false (the separate window-close confirmation uses OK/Cancel). Correct only that expectation, retaining actual modal closure and exact owner/saved-source checks; no product change |
| Incomplete resized screenshots | TEST HARNESS / capture limit: ten `s06` 1280×800 frames contain large unpainted regions (Class, ER, Flowchart, Gantt, Journey, Mind map, Pie, Sequence, State, Timeline). Retain them unchanged. UIA entry/source checks remain attributable, but visual acceptance is NOT RUN for those frames. Observe controls before capture and label all new frames as requiring separate visual review |
| HTML export assertion in `s09` | TEST HARNESS: the existing `HtmlExporter` emits native diagram PNGs, not literal Mermaid source. Preserve real files/failure. Correct the new check to verify exact independently rendered canonical PNG bytes and separate `.mmd` source; no shipping exporter change |
| Intermediate C# compile errors | TEST HARNESS: retained exact candidate logs, including nullable diagnostics; corrected before accepted execution. No dropped warnings or relaxed compiler settings |
| Initial PR Continuous selector | GOVERNANCE: the existing exclusion list did not include the new Goal VIII branch, so run `40118136936` queued controlled comparisons. Cancelled while its jobs were observed queued; retained API receipts. Add only this branch to the established comparison exclusion; normal/High Contrast interaction checks remain required, and the 250 ms goal stays INCOMPLETE |

Evidence is append-only. Aborted preparation directories `baseline-console-01` and
`s01`, actual sessions `s02` onward, original archive/receipts, compile/test logs and
failed screenshots remain under ignored task evidence. Completed sessions cannot
be relaunched; choose a new source-labelled session. Raw paths/registry/state receipts
can contain personal identifiers and must **never be committed or uploaded**.
Only inspected privacy-safe screenshots and redacted summaries may be shared.
An occluded or incomplete frame does not prove the target app's appearance; exclude
unrelated surfaces from published evidence. The three shared images were inspected
individually and contain only the synthetic native scenario/export.

## Safe operator path and interruption

Requires PowerShell 7 on the development/validation station; never install tools into
a later clean recipient merely to obtain qualification. All scripts default to
observation/planning unless an explicit mode is selected. Examples use placeholders
for independently established feature pins; the baseline table supplies its own pins.

```powershell
$taskRepo = (Get-Location).Path
$taskSandbox = Join-Path $taskRepo 'artifacts/native/development-host/<40-character-package-source>/s01'
pwsh -NoProfile -File scripts/native/Test-NativeDevelopmentHost.ps1 -DryRun
pwsh -NoProfile -File scripts/native/Invoke-NativePortableHostSmoke.ps1 -SandboxRoot $taskSandbox -DryRun

# Read-only package verification, extraction and synthetic fixtures; no app launch.
pwsh -NoProfile -File scripts/native/Invoke-NativePortableHostSmoke.ps1 -Mode Prepare -SandboxRoot $taskSandbox -PackageDirectory '<verified-package-directory>' -ExpectedSourceRevision <source> -ExpectedSourceTree <tree> -ExpectedInstallerSha256 <installer-sha256> -ExpectedZipSha256 <portable-sha256>

# The client must be a Release build of the clean committed preparation source.
pwsh -NoProfile -File scripts/native/Invoke-NativePortableHostSmoke.ps1 -Mode RunUi -SandboxRoot $taskSandbox

# A fresh prepared sandbox may run only a targeted shell recheck after a proved
# harness correction. Its completion explicitly marks the entry matrix NOT RUN.
pwsh -NoProfile -File scripts/native/Invoke-NativePortableHostSmoke.ps1 -Mode RunUi -ScenarioGroup Shell -SandboxRoot '<separate-fresh-prepared-sandbox>'

# Optional manual companion: launch only, then use the actual native window.
# Use a separate fresh prepared session when RunUi has already completed.
pwsh -NoProfile -File scripts/native/Invoke-NativePortableHostSmoke.ps1 -Mode Launch -SandboxRoot $taskSandbox

# Against an already running, independently identified PID and real HWND only.
pwsh -NoProfile -File scripts/native/Get-NativeDevelopmentHostDisplayEvidence.ps1 -SandboxRoot $taskSandbox -NativeProcessId <pid> -WindowHandle <hwnd> -ExpectedSourceRevision <source> -ExpectedSourceTree <tree> -ExpectedExecutableSha256 <exe-sha256> -OutputPath '<sandbox>/display-unique.json'

# Close only the identified test app normally before completing preservation checks.
pwsh -NoProfile -File scripts/native/Invoke-NativePortableHostSmoke.ps1 -Mode Complete -SandboxRoot $taskSandbox
```

Before a launch, confirm source pins, fresh path, normal active console, no existing
native process and unchanged observed host snapshots. An existing native process,
locked desktop, stale receipt, unsupported policy or unsafe path is BLOCKED; do not
spoof GitHub runner markers, elevate, install certificates or bypass protections.
The original `Test-NativeInstaller.ps1` and VM-authorised display protocol remain intact.

If interrupted, stop input, retain the PID/start-time/receipt, and inspect the actual
test window. Close that app normally, saving only synthetic work if prompted.
No automatic kill, directory deletion, registry/default-app restore or personal-state
rollback is performed. A changed host surface requires investigation and a separate
explicit manual action, not speculative cleanup. Complete preserves before/after
receipts and synthetic hashes. Its PASS covers those observed surfaces only: it is
not a system-wide filesystem-write trace or a promise that Windows performs no
internal execution bookkeeping.

For manual companion checks, use only prepared files: verify About, folder/tree,
Quick Open/palette, editor/preview Find and Enter, panes/inspector, image/link,
zoom/Fit/pan, each family from the [matrix](evidence/goal-vii/ux-matrix.json),
private validation/Apply/Cancel, owner Undo/Redo, save/restart, invalid/protected
source and close workspace. Record exact pass/fail per step, real PID/HWND, focus/
selection, actual DPI/bounds and screenshots. Skip shared shell dialogues on the
personal host. A real Narrator/NVDA companion needs a human to record audible family,
occurrence, property/error/disabled-state announcements and focus return for all 24
families; absent that transcript, PHYSICAL SCREEN READER remains NOT RUN.

## Decision and separate gates

**HOST PORTABLE:** bounded actual interaction established; full accepted local matrix
and final feature gates require their completion receipts. **INTERNAL BETA: BLOCKED
until those required outcomes pass.** An unresolved critical local smoke failure
keeps this decision BLOCKED even with green hosted CI. Later exact-source results
are published in the Draft PR engineering receipt without rewriting historic evidence.

**HOST INSTALLER: NOT RUN — UNSAFE SHARED REGISTRATION.** The required hosted 24 × 3
installed phases remain 72 synthetic phases, not historical upgrade certification.
Original `.853` replay and historical upgrade remain skipped. The final head must
retain locked restore, zero-warning Release, Core ≥2114, corpus/full exports ≥176,
all original zero-tolerance fidelity, 38/38 expanded appearances plus original-five
suites, workspace/interaction/stock Continuous correctness, Chromium/Edge ≥135 each,
static/offline checks, ≥25 package negatives, real hosted wizard/lifecycle and an
independent complete package/delta inspection. Any failed required gate is BLOCKED.

**PUBLIC PRODUCTION: BLOCKED.** Later clean Windows, 125%/150% formal DPI and
multi-monitor, physical reader, original historical migration, signing/trust/updater
and release decisions remain separate. Follow the [later clean-system mini-checklist](goal-viii-clean-windows-mini-checklist.md),
preserving the detailed Goal VII runbooks. PR #38 stays OPEN / DRAFT / PAUSED at
`31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`; its original 250 ms goal is INCOMPLETE.
Historical `.749`/`.853` PDF/Journey/intermediate ER root causes remain INCONCLUSIVE.
No merge, signing, certificate install, tag, Release, updater publication or Pages
deployment is authorised by this goal.
