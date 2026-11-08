# R01/R02 qualification tooling

8 November 2026 · Goal #77 / Draft PR #78 · **Tooling preparation only; campaign NOT RUN**

Implements the [bounded qualification plan](../native-acceptance-qualification-plan.md)
from reviewed `45eccf7b3bd4c266b3a3223cbfe007248f03f39d`. Design approval remains
separate from execution authority and from ADR 007 operational activation.
PR #76 is **NO-GO**. The historical 250 ms and 2.542596:1 failures are unchanged;
the contrast requirement remains **4.5:1**.

## Components and immutable inputs

| Component | Purpose |
|---|---|
| [sources.py](../../../scripts/native/qualification/sources.py) | Verify the four source commits/trees and seven fixtures; export exact Git product snapshots; generate the bounded validation overlay and original preflight adapter. |
| [qualification.py](../../../scripts/native/qualification/qualification.py) | Prepare/build offline, enumerate the fixed cells, verify source/binary/runtime receipts and supervise separately authorised execution. |
| [windows.py](../../../scripts/native/qualification/windows.py) | Win32-only appearance snapshot/restore, input cleanup, job-tree termination and observed memory supervision. Loaded only after execution guards. |
| [R02 observer](../../../scripts/native/qualification/observer/Program.cs) | Distinct preflight, stock or actual compiled MainWindow observation in each fresh process. |
| [audit.py](../../../scripts/native/qualification/audit.py) | Independent offline evidence audit with separate collection, correctness and software-order verdicts. |
| [png.py](../../../scripts/native/qualification/png.py) | Bounded PNG structure/CRC/decompression and decoded BGRA-hash checks. |
| [offline controls](../../../tests/unit/test_native_qualification.py) | Mutation controls for identity, correctness, chronology, colours, evidence completeness, supervision and cleanup. |

Product pins: develop `99349d82eb06960eb1f9785cf384db2373aed4fa` and candidate
`7b1334b09a02ad2fbfd4a0234798e6c387d3e634`. PR #80 remains separately pinned at
`22c223c6f56fa41fda2a36102e1eed0af44974ec`. No branch is merged or rewritten.
Build inputs come from Git blobs in isolated directories, not mutable product
worktree globs. Source files, generated overlay/diff, fixtures, compiler identity
and complete binary output are hashed in new receipts. Input drift or extra source
files invalidates a build. Original product source and evidence remain untouched.

## R01: four processes, unchanged journey

The fixed order is V1 normal/trace off, V2 normal/trace on, V3 actual HC/trace off,
V4 actual HC/trace on. Each uses the original mixed fixture, Pages, 100% application
zoom and one iteration. The original `CompletionJourney.cs`, `CompletionTrace.cs`
and `Desktop.cs` are copied byte-for-byte from #76. The seven boundaries and three
frames per boundary remain unchanged: **84 full frames / 252 crops maximum**.

The generated `Program.cs` overlay changes validation plumbing only:

- Require hosted execution and recorded execution ID/configuration hash before any
  entry point, and restrict the entry to the fixed completion journey.
- Omit StageListener construction and generic dispatcher-duration subscriptions
  in **both** completion variants. Only traced cells construct CompletionTrace;
  trace-off has no completion/media/input/property/composition observer hooks.
- Record loaded core-runtime identity and a read-only 1280×800/100% scope check.
- Retain failure JSON/state and every fixed frame, but omit the ancillary
  `failure.png` outside the declared frame ceiling. This omission is recorded;
  it is never a pixel PASS.

The generated diff and source hashes expose these changes. New harness timings
must not replace historical unmodified-harness timings. The launcher removes ambient
diagnostic/loader flags and sets every journey/tracing selector explicitly. The
original 30-second observation and 240-second child bounds remain. Trace-on frame
1 still waits for the original natural WPF observer event; no timing/pixel retry,
forced render, new foreground action or compositor guarantee is introduced.

## R02: separate processes and first-read boundary

Four cells are fixed: 10.0.13 baseline-first/candidate-first, then 10.0.14
baseline-first/candidate-first. Each cell executes its original-sequence preflight
pair before its independent application pair, retaining the declared product order.
The two stock controls follow, one per runtime. Ceilings: **8 preflight + 8 app +
2 stock processes**. No repeat or automatic wider workflow is available.

The preflight adapter extracts the original first-read/resource/assertion sequence,
diagnostic and contrast function verbatim from #76. Entry OS inspection and passive
notification observation use Win32 only. The dedicated native observer creates an
**unshown native window**, receives actual broadcasts on its own native message
thread, and never accesses WPF resources, pumps a WPF dispatcher, applies a theme
or sends synthetic theme notifications. It is instrumentation of the validation
process, not evidence that WPF received/processed the same notification. Its source,
thread/PID, timestamps, overflow/error state and initial boundary are retained.

Original order: NativeTestHighContrast enables HC → first WPF HC query → Application
and ResourceDictionary → ThemeService.Apply → original diagnostic → unchanged
assertion. Failed preflight is retained. A later independent app observation cannot
turn it into PASS. Stock controls use the same first-read order with stock system
pairs and passive native notifications, without product resources.

The application observer starts with HC already active. It hosts **the exact
compiled MainWindow**, using isolated state and copied fixtures; this is not
standalone installed-app startup certification. At 1280×800 it identifies the
unique enabled Button named `New document (Ctrl+N)`, establishes a fixed real
keyboard selection, and captures a real pointer press during initial HC, normal,
and returned HC: at most **three pressed frames per app process**. It reads the
template `Surface.Background`, actual Button.Foreground and CommandIcon.Foreground
on the UI thread; Button.Background is transparent and is not substituted.

Each press is cancelled by moving outside before release. Source, dirty state,
document identity/count, caret, selection and editor focus must remain unchanged.
Wrong controls, unavailable opaque brushes, missing notifications, clipping or
unknown state produce failed/incomplete evidence. Actual OS HC→normal→HC changes
are observed through native messages; no reapplication or cache repair is performed.
Original 20-second UI bounds remain; the 240-second supervisor does not extend them.

The parent records original appearance before every process and restores it after
success, assertion failure or termination. Initial held input or HC-active state
blocks the normal-start protocol. Cleanup releases only leftover injected buttons/
modifiers and verifies appearance/input state. Unknown cleanup stops the remaining
cells. Job assignment failure also terminates the direct child. These mechanisms
verify an empty job tree within a separate ten-second cleanup bound, which cannot
extend the 240-second observation/child limit. Harmless console-child controls test
the job/memory API without WPF or appearance calls; actual HC cleanup remains unqualified.

## Execution manifest and runtime binding

The [hosted environment lock](hosted-environment-lock.md) adds discovery-only
configuration preparation and mandatory complete-runtime/image verification.
It preserves separate owner execution authority and all original protocol bounds.

[The execution inventory](goal77-execution.json) lists every cell, source and safety
bound. [The configuration template](goal77-execution-config.template.json) is
deliberately **NOT_AUTHORISED** and has no invented image, runtime or binary hashes.
It cannot run a campaign as delivered. The owner must separately authorise execution
on the exact tooling and configuration; a design-approval URL is not that authority.
The URL/acknowledgement guard records authority, not independent proof of its text.

Before any foreground process, the launcher verifies a clean exact tooling commit,
source/fixture/generated-source receipts, complete product/Core/harness closure,
exact OS image/build and host/runtime files. Only **10.0.13** and **10.0.14** are
diagnostic pins. Both Microsoft.NETCore.App and Microsoft.WindowsDesktop.App are
set to the exact version with roll-forward disabled. The loaded core/WPF hashes,
runtime version and product revision are checked again in the evidence audit.
Moving `10.0.x`, mismatched runtimes or missing hashes are rejected.

No hosted workflow or physical calibration pilot is dispatched by preparation.
The runtime versions on the development machine are not silently substituted.
All appearance/input entry points reject the developer desktop. Do not spoof
hosted flags to bypass the guard.

## Offline use

Run from the PR #78 worktree. Output roots must be fresh; partial results are retained.

```powershell
python scripts/native/qualification/qualification.py verify
python scripts/native/qualification/qualification.py plan --protocol R01 --output artifacts/goal77/r01-plan.json
python scripts/native/qualification/qualification.py plan --protocol R02 --output artifacts/goal77/r02-plan.json
python scripts/native/qualification/qualification.py prepare --candidate-root C:/Code/MarkdownReader-reading-productivity --baseline-root C:/Code/MarkdownReader-reading-experience --output artifacts/goal77/build
python scripts/native/qualification/qualification.py build --output artifacts/goal77/build
python -m unittest discover -s tests/unit -p test_native_qualification.py
```

`--self-test` on either generated R02 host performs only pure contrast/identity
checks: no window, input or OS change. Execution is a separate guarded `execute`
command requiring a reviewed configuration; it is **not authorised in this task**.
The audit reads retained files only and writes a fresh report:

```powershell
python scripts/native/qualification/audit.py artifacts/goal77/authorised-run --output artifacts/goal77/independent-audit.json
```

## Evidence schema and failure classifications

The [report schema](evidence.schema.json) defines the final audit envelope. Raw #76
completion formats remain unchanged. Each new execution has `execution.json`
(fixed ordered cells/config hash), `processes.json`, per-cell process journals,
fresh `evidence/`, stdout/stderr and a SHA-256 inventory of every retained file.
Identity carries actual PID, product/Core/harness/core-runtime/WPF hashes and QPC
frequency. Missing, extra, mismatched, duplicated or truncated records invalidate
collection; they are never zero-latency or empty passing samples.

R01 requires 21 exact frame records per process, complete trace metadata in traced
cells, no trace files/counters in trace-off cells, valid PNG/decoded pixel hashes,
crop geometry and consistent before/after ownership, source, UIA and labels.
`pixel-reviews/V1.json` through `V4.json` are independent review arrays. Each entry
names `file`, `pngSha256`, `processId`, `reviewer`, `requiredReaders`, and explicit
Boolean `currentSourceVisible`, `requiredLabelsCorrect`, `selectionCorrect`.
Review the full frame against its source/owner/map and raw crops. OCR alone cannot
fill these fields. Missing/ambiguous review remains INCONCLUSIVE; a confirmed stale,
blank or incorrect frame remains FAIL even when subsequent fixed frames pass.

R02 retains entry/identity/cleanup, original pre-assertion diagnostics or the stock
pair, native notification chronology, and per-phase actual-app records/captures.
Wrong PID, process kind, source, runtime, control or ratio invalidates collection.
Application and preflight outcomes remain separate rows, even if one fails.

| Verdict | Meaning / failure treatment |
|---|---|
| Collection VALID / INVALID | Identity, bounds, complete files and cleanup are independently verifiable. A validly collected application FAIL remains FAIL. |
| Existing correctness PASS / FAIL / INCONCLUSIVE | Original assertions remain; stronger missing-evidence checks cannot grant PASS. Known wrong ownership, content, selection, Ready or contrast always fails. |
| Presentation-order SUPPORTED_SOFTWARE_ORDER_ONLY / INCONCLUSIVE | Requires consistent recorded mutation, media submission and capture chronology for the same owner. Ready, RenderComplete or DwmFlush alone is insufficient. No physical pixel/scan-out claim. |
| Historical / policy verdict | Never reclassified; policy NOT_ACTIVATED and PR #76 NO-GO are fixed output fields. |

An invalid or partially collected cell does not erase an independently recorded
failure. Neither successful local tests nor later passing observations close R01–R06.

## Readiness boundary

Tooling validation covers offline controls, source extraction/overlay, compilation,
pure ratio checks, guards, bounded supervisor paths and evidence auditing. It does
not certify live foreground actions, actual HC notifications/restoration, 10.0.13/
10.0.14 behaviour, visible pixels, physical hardware, or statistical sampling.

**Execution remains BLOCKED** until exact runtime/image/binary configuration and
separate owner execution authorisation are recorded. Any outstanding tooling-review
finding also blocks execution. The [owner selection sheet](hardware-corpus-selection.md)
keeps H0/H1, three real documents, finite complexity/resource limits and the
56-process pilot pending; that pilot is not implemented as an executable mode.

The [delivery receipt](../evidence/qualification-tooling/receipt.md) binds the
tested implementation commit/tree, checks and independent review; the final
receipt-only commit/tree is recorded in the PR/Goal delivery comments.
PRs #76/#78/#80 remain Draft. No production code, thresholds, required checks,
rulesets, signing, distribution or ADR operational authority changes.
