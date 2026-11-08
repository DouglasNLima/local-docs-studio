# Independent HIGHCONTRAST buffer-safety review

Reviewed on 2026-11-08 by the independent technical review agent, against base commit `eddd6b5268215182991cbe5d71a3fd52ce93c9b9`, tree `58616bf13a4f0610b4fa86dffbc3a25d5dbbad3f`, and the exact working-tree bytes below.

**Disposition: the ownership correction and controlled evidence are technically ready for a separate execution decision. Live execution remains BLOCKED and NOT_AUTHORISED.** This review is not owner approval, Windows runtime certification, restoration qualification, ADR operational activation or PR #76 integration acceptance.

The review covered the shared reader, collector adapter, evidence validator, native fixture, managed controls, local control runner and Python negative controls. The reviewer ran only the two focused Python modules in separate processes. No actual `SPI_GETHIGHCONTRAST`, collector, GUI, hosted job, R01, R02, calibration, display, appearance or input operation was executed by this reviewer.

The ownership contract has an explicit primary-source contradiction. The [Microsoft Learn HIGHCONTRASTW page](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-highcontrastw) instructs callers to release the returned scheme buffer. However, [Microsoft's pinned WinForms implementation, lines 50–57](https://github.com/dotnet/winforms/blob/295f067453524c1eef18fcbe85e14431bd8aef17/src/System.Private.Windows.Core/src/Windows/Win32/PInvokeCore.SystemParametersInfo.cs#L50) explicitly identifies that instruction as incorrect and does not release the returned pointer. The [pinned WPF implementation](https://github.com/dotnet/wpf/blob/49ae5cd6e0ba39eb9d54b93e35af6a0bfe0b7d51/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/SystemParameters.cs#L158) also consumes GET results without freeing the scheme pointer. The correction therefore treats GET storage as borrowed, copies the value immediately and never sends that pointer to a deallocator.

The old collector's ownership assumption and explicit release are confirmed source-level defects against that Microsoft implementation evidence. The precise origin of the historical heap corruption remains **INCONCLUSIVE**. Run 42500925642 recorded a second release-begin boundary followed by `STATUS_HEAP_CORRUPTION`, with no matching release-complete boundary; it did not record pointer addresses, aliasing, a faulting instruction or earlier heap damage. A confirmed double-free, a defect in `LocalFree` itself, or a cause for the earlier no-output run cannot be inferred. The [historical recovery receipt](../evidence/infrastructure-recovery/receipt.md) and failed archives remain authoritative and unchanged.

The new reader rejects unknown ownership before GET, validates x64 layout and offsets, captures the native error immediately on GET failure and ignores failed-call output fields. It copies raw flags and preserves NULL, EMPTY and NAMED distinctly. Only managed string data escapes the reader. Its 32,768-code-unit limit includes the terminator; malformed UTF-16 and missing terminators fail rather than silently changing the value. This cap limits work, not address readability. Actual pointer validity, lifetime and concurrent OS behaviour still depend on the native API contract and have not been established by simulation.

The collector no longer imports or calls `LocalFree` for GET storage. Its evidence declares `BORROWED_OS`, successful copying, no release attempt and the pinned ownership-contract identity. The validator rejects missing, legacy or contradictory released-buffer evidence. The existing unconditional hosted-discovery block and spent qualification authority remain in place.

The native fixture uses SDK declarations and compile-time ABI assertions: pointer/structure sizes 8/16, offsets 0/4/8, WCHAR size 2 and BOOL size 4. Its GET export is simulated. Fixture allocations come from `LocalAlloc` and only their explicit fixture owner releases them, once; attempted borrower or duplicate-owner releases are rejected before deallocation. Import inspection runs before control execution and excludes User32 and dynamic loader imports from the fixture DLL and native self-test. These controls establish behaviour of known fixture storage, not ownership of an observed Windows-returned address.

Both review findings are resolved:

| Finding | Final disposition |
| --- | --- |
| Positive managed controls previously replaced the actual pointer-read delegate. | The twentieth managed case now uses the default `Marshal.ReadInt16` path on known, owned, NUL-terminated empty and Unicode fixture buffers; the fake GET remains in use. |
| An empty loaded-assembly list was accepted as complete evidence. | Validation now requires unique, non-empty names containing both `System.Private.CoreLib` and `NativeBufferTests`; controls reject empty, missing and duplicate identities. |

Verification is bounded as follows:

| Evidence | Result and scope |
| --- | --- |
| Independently executed `python -m unittest discover -s tests/unit -p test_native_infrastructure.py -v` | PASS: 21 tests, 0.151 seconds; mocks and guarded entry only. |
| Independently executed `python -m unittest discover -s tests/unit -p test_native_buffer_evidence.py -v` | PASS: 6 tests, 0.012 seconds; pure report validation. |
| Root-executed local attempt 6, independently inspected | PASS_CONTROLLED_SIMULATION_ONLY: 21 native checks and 20 managed cases; 2/2 native and 53/53 managed fixture allocations/releases, no outstanding allocations. No real SPI or UI calls; the collector was compiled only. |
| Attempt-6 integrity, independently verified | All 87 retained file hashes and all eight source bindings in its receipt match. Complete reports pass the current validator. |
| Local simulation identity | .NET SDK 10.0.401, Core runtime 10.0.12, MSVC 14.44.35207, Windows SDK 10.0.22621.0. This is not certification of the hosted 10.0.14 runtime. |
| Historical archives, independently verified | Run 42500058059 SHA256 `e1549ab66703379edb6abbb1c9a723ee67464e4b5eee03f31450fda9e5eab3b4`; run 42500925642 SHA256 `0deafb2fd7daa16cc283be5941b763cd720d67d0dd5ab51d4653e75b50a99f21`. |

The root's combined `test_native*.py` discovery result remains **FAIL: 100/101**, not an aggregate PASS. Source inspection confirms the narrow import-isolation interaction: unchanged `test_native_qualification.py:24` imports `windows` during discovery, while unchanged `test_native_hosted_execution.py:48` asserts that `windows` is absent from the parent process's `sys.modules`. Discovery imports all selected modules before running their tests. The two files match the starting commit; the observed interaction does not demonstrate that the rejected child imported Windows or executed GUI code. Running established modules in separate processes preserves their intended isolation but does not erase the combined failure. This reviewer did not repeat the aggregate suite.

The following SHA256 values bind **raw working-tree bytes**, including line endings. Attempt 6 was reviewed at the local path `artifacts/goal77-buffer-safety/attempt-6`; its receipt and file manifest bind the build outputs and control reports. Subsequent Git checkout EOL conversion must not be silently described as an identical raw-byte hash.

| Reviewed file | Raw SHA256 |
| --- | --- |
| `scripts/native/qualification_hosted/desktop_probe/HighContrastReader.cs` | `bf59a42423bbce527a5e2f5851ff79494252d886c0dd85e1d8c2b1b1f5e77d59` |
| `scripts/native/qualification_hosted/desktop_probe/Program.cs` | `c5594b642795785b192c7c6c420bbb84a3c09df1314a4deb037ee2896d62b54f` |
| `scripts/native/qualification_hosted/discover_desktop.py` | `2763e87949f69bed89617c233fe664b7becdd21664f652e29d27ca240337552e` |
| `scripts/native/qualification_hosted/eligibility.py` | `18df748d3f4f5229903a2f867feb0518a37ad73ffefe57749c47ae7a317fc005` |
| `tests/unit/native_buffer/fixture.cpp` | `829614ae15dc468f2c8734891e8d48c61aa0616385aa8cf99e54ffe4978523d1` |
| `tests/unit/native_buffer/ManagedControls.cs` | `26045c726f1d78034b19d97743db17a3d7588f1872e5c1cfabe0669f2e9452b6` |
| `tests/unit/native_buffer/NativeBufferTests.csproj` | `308d94b0d0b6bf1a6d3099ef422cf1640013a227d96c18a7d8dd1f56eb71ef58` |
| `tests/unit/native_buffer/run_controls.py` | `a33f4b49d10752187f532fb949545cbf9fe24a9dbe434304e380dd4a912a0a37` |
| `tests/unit/test_native_infrastructure.py` | `dae47352899fd3a70f1e1caf43ec695aff989d3dcdb38d14c89e8eee578041cf` |
| `tests/unit/test_native_buffer_evidence.py` | `c752292ebbaeffc25c686356313fdae631fddf239d3770ce4a6e63a7d1ba986e` |
| `tests/unit/test_native_hosted_execution.py` | `2cfd6ee6a22c9677f6ffe9bb17548f1d6b0ed2ae180cf38628db4ebc8d3d5e84` |
| `tests/unit/test_native_qualification.py` | `56b9aa00fa36efceb5912098e6a00557e16bc7111c1ab9c0cb48e7ce02cbb28a` |
| `artifacts/goal77-buffer-safety/attempt-6/receipt.json` | `5209898f9a3b600169963011ade7839b699a7bd3b989d4a8715d0ed34db0c75c` |

No further source-level blocker was found within this focused ownership review. Passing simulated controls does not establish safe real GET calls, display eligibility, exact appearance restoration, visible completion, accessibility acceptance or production readiness. A future live validation needs its own explicit authority and reviewed execution boundary. R02 remains NOT_RUN, historical failures and required checks remain unchanged, and PR #76 remains NO-GO; PRs #76, #78 and #80 remain Draft.
