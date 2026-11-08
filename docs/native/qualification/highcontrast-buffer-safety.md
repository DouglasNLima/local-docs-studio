# HIGHCONTRAST buffer safety recovery

The collector freed a borrowed OS pointer. That source-level ownership defect is
confirmed against Microsoft's explicit implementation guidance and corrected in
the discovery collector only. The precise origin of the historical heap corruption
remains **INCONCLUSIVE**. Offline controls support review of the correction; they
do not certify actual Windows HIGHCONTRAST behaviour.

**Execution remains BLOCKED / NOT_AUTHORISED.** No hosted discovery, real HIGHCONTRAST
call, appearance/display change, R01/R02 process or calibration pilot was executed
in this continuation. The actual collector was compiled, never run. PRs #76, #78
and #80 remain Draft; PR #76 remains **NO-GO**.

## Source and preserved observations

| Identity | Exact value |
|---|---|
| Starting PR #78 commit | `eddd6b5268215182991cbe5d71a3fd52ce93c9b9` |
| Starting tree | `58616bf13a4f0610b4fa86dffbc3a25d5dbbad3f` |
| PR #76 candidate | `7b1334b09a02ad2fbfd4a0234798e6c387d3e634` |
| develop | `99349d82eb06960eb1f9785cf384db2373aed4fa` |
| PR #80 correction | `22c223c6f56fa41fda2a36102e1eed0af44974ec` |
| Crashing discovery source | `dcb1536a9299f65f94d140877042b844f03356e2` |
| Crashing discovery tree | `7b78528cd5c724faa325c1b78bdcf3ada3738123` |

[Run 42500925642, attempt 1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42500925642)
retains PID 9612's `0xC0000374 / STATUS_HEAP_CORRUPTION`: the first GET/copy/LocalFree
completed; the second GET/copy completed; its final marker was
`highContrastFreeBegin`. Both copied flags 126 and a non-null empty UTF-16 string.
No recorded pointer addresses, alias proof, allocation provenance or crash dump
establishes a double free or the first corruption site. The detecting LocalFree
need not be the first damaging operation. The correction is consistent with the
observed failure, but is not retrospective proof of its exact mechanism.

The original archive SHA-256 remains
`0deafb2fd7daa16cc283be5941b763cd720d67d0dd5ab51d4653e75b50a99f21`.
All earlier evidence, including the incomplete discovery and original
R01 V1–V3 failures, remains byte-preserved. See the
[infrastructure report](hosted-infrastructure-recovery.md) and
[new offline receipts](../evidence/buffer-safety-recovery/README.md).

## Reviewed native contract

There is a material Microsoft documentation contradiction. The
[Learn HIGHCONTRASTW page](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-highcontrastw)
says to release the GET scheme with LocalFree. However, the official
[Microsoft WinForms wrapper at commit 295f067453524c1eef18fcbe85e14431bd8aef17](https://github.com/dotnet/winforms/blob/295f067453524c1eef18fcbe85e14431bd8aef17/src/System.Private.Windows.Core/src/Windows/Win32/PInvokeCore.SystemParametersInfo.cs#L45)
explicitly identifies that deallocation instruction as incorrect: Microsoft's
internal callers do not free this pointer. The collector follows this more specific
implementation guidance, records its exact contract identity and borrows the OS
storage only long enough to create a managed copy. It does not infer ownership
from a non-null address, empty contents, a successful first free or pointer equality.

| Boundary | Reviewed contract and implementation |
|---|---|
| Native structure | x64 HIGHCONTRASTW is 16 bytes; UINT fields at offsets 0/4, pointer at 8. WCHAR is 2 bytes and BOOL 4. Native SDK static assertions and the managed type agree. Other architectures/layouts fail before GET. |
| GET declaration | Exact `SystemParametersInfoW`, explicit Unicode and BOOL marshalling, `SetLastError=true`, `ref HighContrastNative`; both `cbSize` and `uiParam` are 16. No string marshaller owns the pointer. |
| GET failure | Capture the error immediately. Reject the observation without reading or releasing its output pointer, even if the failed output is non-null. |
| Successful GET | Raw flags and NULL/EMPTY/NAMED are preserved. Copy immediately, before logging/theme/palette work. No pointer is cached or returned in a snapshot. |
| Borrowed storage | **Zero collector releases**, on success or exception. This corrects an invalid deallocation; it does not omit a required release. No owning SafeHandle, LocalFree, FreeHGlobal or FreeCoTaskMem is attached to the OS pointer. Unknown ownership fails before GET. |
| UTF-16 copy | At most 32,768 code units including NUL; reject missing terminator and unpaired surrogates. No replacement characters, trimming or normalisation. The cap bounds work, not memory readability. Real access still relies on the API supplying a readable terminated string; a concurrent external change is not made safe by the cap. |
| Repeated GET | Two fixed reads remain. A reused address is permitted; contents are copied separately and earlier managed snapshots remain independent. No pointer identity or content stability is assumed. |
| LocalFree audit | The old signature and null-success/non-null-failure check matched the API. The defect was applying it to borrowed storage. The controlled fixture alone pairs known LocalAlloc allocations with exactly one owner release; unknown/duplicate releases are rejected before LocalFree. |
| Exceptions | Conversion, ABI and native failures propagate. Post-copy journalling failures remain failures. There is no borrowed-pointer cleanup to retry. The fixture's real owner still releases its own storage after conversion/observation exceptions. Native process faults remain invalid collections. |

The SDK signature/size checks follow
[SystemParametersInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow).
[LocalFree](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-localfree)
documents the allocator pairing and return semantics. The native helper includes
the installed Windows SDK and independently copies its simulated GET output; it
never calls SystemParametersInfoW. Simulations demonstrate the implemented
ownership discipline, not the Windows allocator's live behaviour.

## Minimal correction and evidence boundary

[HighContrastReader.cs](../../../scripts/native/qualification_hosted/desktop_probe/HighContrastReader.cs)
contains the shared ABI and immediate borrowed copy. The collector removes the
invalid release path, retains both reads, and journals `highContrastBorrowComplete`
after copying. Its output records `schemeOwnership=BORROWED_OS`,
`schemeCopyComplete=true`, `schemeReleaseAttempted=false` and the pinned ownership
contract. Those fields report the reviewed implementation; they alone cannot prove
native safety or authorise execution.

The offline eligibility validator rejects missing/contradictory ownership fields,
the old `getBufferReleased` claim, malformed/truncated scheme values and inconsistent
pointer kinds. Old failed records remain historical evidence, not migrated successes.
`discover_desktop.py` includes the additional source file but retains its unconditional
ownership/execution block before output, build or process launch. The spent campaign
authority guard and the frozen R01/R02 bundles are unchanged.

## Offline validation

The reproducible local recipe is
[run_controls.py](../../../tests/unit/native_buffer/run_controls.py), with a fresh
output directory under `artifacts`. It installs nothing and rejects hosted execution.
Observed local versions are SDK **10.0.401**, Core **10.0.12** with roll-forward disabled,
MSVC **14.44.35207**, Windows SDK **10.0.22621.0**, x64. These are local simulation
identities, not substitutions for the pinned hosted 10.0.14 environment. Logs and
SHA-256 receipts identify the compiler, SDK header, loaded Core, sources and binaries.

- **21 native checks**: SDK layout, NULL/empty/named results, repeated borrowed
  address, independent snapshots, failed GET, bounded truncation, wrong/duplicate
  release attempts and exactly-once fixture-owner cleanup.
- **20 managed cases**: the real shared reader, including its default
  `Marshal.ReadInt16` path on known valid fixture buffers; unknown ownership,
  malformed UTF-16, cap/terminator edges, failed GET with an invalid sentinel pointer,
  32 repeated allocation cycles, exceptions and snapshot lifetime. Final counts:
  **53 allocations, 53 owner releases, zero active allocations**.
- **6 evidence-control tests** reject missing/duplicate/truncated/non-finite records,
  contradictory ABI/release results, wrong runtime, missing/duplicate assembly
  identity, forbidden UI assemblies and missing/UI/dynamic native imports.
- **21 infrastructure controls** retain the no-side-effect guard, exact geometry,
  scheme distinctions, failure/timeout/PID handling and non-authorising verdicts.

Both native binaries' import tables are checked before execution. They contain
only the permitted kernel/CRT dependencies, no User32/WPF or dynamic loader imports.
The managed test project links the real reader but not the collector entry point.
The collector's compile-only check passed with zero warnings/errors. No actual
HIGHCONTRAST API was called by either control implementation.

Initial local recipe attempts 1–3 failed toolchain setup/lookup and remain recorded;
they did not execute either control binary. Attempts 4–5 passed the earlier 19-case
controls. Independent review required exercising the default reader and strengthening
assembly evidence. Attempt 6 passed the corrected 20-case controls. These are offline
tooling revisions, not retries of failed Windows observations.

An aggregate Python discovery invocation returned **100/101**, because the unchanged
hosted CLI test inspects its parent process's `sys.modules`, while the unchanged
qualification test imports `windows` during collection. Preserve that failed result.
The established test modules are qualified in separate Python processes; this does
not convert the aggregate invocation to PASS or remove its assertion. Detailed test,
static, provenance and source receipts are linked above. The
[independent review](highcontrast-buffer-review.md) records its exact scope and hashes.

## Remaining prerequisites and owner decisions

| Decision / prerequisite | Recommendation | Authority still required |
|---|---|---|
| Accept the ownership repair for separate execution review | The source-backed correction and offline controls are ready for review. The historical fault mechanism remains inconclusive. | Acceptance of this patch is not permission to execute the collector. |
| Bounded read-only native verification | Separately review one corrected, exact-source collector on a disposable pinned Windows host; two fixed GET/copies, original 30-second supervision, complete stdout/stderr/exit/PID/self/runtime hashes, independent native comparison, no retries. Stop on any mismatch/fault; if needed, agree dump/heap diagnostics in that separate plan before execution. | Explicit new owner authority naming source, image/runtime, process ceiling and permitted diagnostic collection. The current launcher remains blocked. No display/appearance/input changes or qualification process is included. |
| Geometry | Retain observed 1024 × 720 at 96 DPI as ineligible for 1280 × 800/100%. Enumerated 1920 × 1080 remains unactivated and unqualified. | A later, separately approved disposable-host configuration experiment or alternative runner; no spoofing or viewport reduction. |
| Exact appearance restoration | Raw flags 126 and EMPTY remain distinct from `High Contrast Black`; the earlier mismatch remains FAIL. Buffer-copy correctness cannot establish restoration. | Separate bounded live appearance experiment only after read-only native verification, with exact baseline/readback and cleanup. |
| R01/R02 execution | Review and integrate pre-V1 and per-cell shared-resource gates with fresh source/bundle/configuration receipts after infrastructure passes. | Fresh campaign authority; no automatic activation from this repair. R01 four-process/R02 eighteen-process ceilings and all original assertions remain. |

The 1280 × 800 window, 100% application zoom, Pages mode, first WPF colour-read
boundary, source/fixture/runtime identities, original timeouts/memory limits,
84-frame/252-crop bounds, 250 ms benchmark and 4.5:1 requirement are unchanged.
The historical 2.542596:1 failure remains FAIL. No R01–R06 risk is closed, no ADR
operational transition is activated, and the 56-process pilot remains unauthorised.
