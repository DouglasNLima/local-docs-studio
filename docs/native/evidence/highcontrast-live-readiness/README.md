# Corrected HIGHCONTRAST verification preparation receipts

**BLOCKED / NOT_AUTHORISED. No collector, hosted verification or qualification
campaign was executed.** This packet preserves an unresolved local supervisor
control failure alongside passing offline controls. It is not live native evidence.

- [Protocol and remaining decisions](../../qualification/highcontrast-live-readiness.md).
- [Independent review](../../qualification/highcontrast-live-review.md).
- [Candidate configuration](../../qualification/highcontrast-live/config.json), SHA-256
  `c2cf91cbd5678752d028d7f64a6d87a90e183fb1f73c12de35b9cc967ef13d7a`.
- [Build receipt](build-receipt.json) and [sealed collector](collector.zip).
- [Original preparation records](preparation-evidence.zip) and [file hashes](files.json).
- [Offline source, closure and preservation validation](validation-receipt.json).

## Exact source and binaries

Source/build commit **S** is `2a9608f6af5f4847c9ed13a19ba3466a25da1f47`, tree
`c8075f2a2fdac6fb60c77697bb741285931267a6`. The final package commit **E** and its
tree are recorded in the Goal #77 delivery comment after E exists. The unapproved
owner-record proposal binds E, S, configuration, binary, runtime and process bounds;
it grants no authority and cannot override the supervisor block.

The compile-only recipe exported five source files directly from Git S, used SDK
10.0.401 with roll-forward disabled and embedded S as the assembly revision. Build
completed with zero warnings/errors; **the resulting collector was not executed**.
The receipt includes compiler/SDK hashes, all 14 execution implementation hashes,
all five binary hashes and the complete local build output manifest.

| Identity | SHA-256 |
|---|---|
| Collector ZIP, 113,417 bytes | `9c544090eacaa75170110201240f5a64d1476ea7e99de079a6b6e879daf10fdf` |
| Build receipt | `9505857cacca58868fcd7321ed8997c79aa995ad0bc35fdadd20e3a6d392b6bd` |
| Raw preparation ZIP, 408,727 bytes | `09e7c8918b1e48fc8cd24f43b700e57dfcff423f31ce4f2cdc5cf562e65fb9aa` |
| Original Core 10.0.14 archive, 36,971,047 bytes | `d861ab80f232adf3bdce83293bc1fa92868400330c477bf854d8558eb91c7ca6` |

The downloaded Microsoft runtime archive was hashed without executing it. Its 193
file hashes match the Core subset of the original 480-file hosted environment lock.
The configuration contains the full closure, original download URL, byte length and
SHA-512; the 37 MB upstream archive is not duplicated in this repository. No current
runner identity has been newly observed. The existing image pin remains
`win25-vs2026 / 20261103.298.1`; drift invalidates the candidate configuration.

## Preserved validation outcomes

Paths below are members of `preparation-evidence.zip`; archive bytes preserve local
line endings independently of Git text normalisation.

| Evidence | Result and scope |
|---|---|
| `local-supervisor-control/` | **FAIL / UNRESOLVED**. One harmless native fixture, PID 37804, exit 0, 21 fixture checks passed; Job total 2 / active 1 and `cleanupVerified=false`. Includes original stdout, empty stderr, process record and exact supervisor source snapshot/hash used for that control. No User32/SPI/GUI use and no collector execution. |
| `controls/` | Live controls 16 PASS at that intermediate revision; infrastructure 21 PASS; environment lock 14 PASS; static PASS. First existing hosted-controls run **FAILED 1 of 17** because its source-section delimiter included the newly inserted job. Original failure retained. |
| `controls-amended/` | Final live controls 17 PASS; existing hosted controls 17 PASS after narrowing that delimiter. No acceptance assertion was removed. |
| `provenance-source/` | Foundation PASS; governance PASS; 25 governance controls PASS; `git diff --check` PASS. |
| `sealed-1/` | Canonical five-file source snapshot, raw compilation logs, generated output, five-file collector archive, configuration and complete build receipt. |
| `runtime-files.json` | All 193 Core archive file SHA-256 values. |

The passing fixture checks do not close the failed supervisor observation. New
accounting/PID-list instrumentation has only mocked validation. The precise cause
of the 2/1 result and proof of empty membership remain missing. Configuration status
is `NOT_AUTHORISED`, both approval flags are false, and
`supervisorQualification=BLOCKED_LOCAL_JOB_ACCOUNTING_UNRESOLVED` stops the coordinator
before authority-network, Windows-native or child-process access.

The independent reviewer repeated the 17 + 17 Python controls in separate
processes, reviewed the supervisor/authority/runtime/evidence boundaries and checked
the sealed hashes. No reviewer collector or native-fixture execution is requested.
No browser/full-suite CI was run: product behaviour is unchanged and hosted execution
is not authorised. Original R01 V1–V3, appearance restoration and heap-corruption
evidence are unchanged; PR #76 remains NO-GO.
