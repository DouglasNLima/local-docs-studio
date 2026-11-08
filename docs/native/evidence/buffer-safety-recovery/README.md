# Offline HIGHCONTRAST buffer recovery evidence

This is **controlled simulation evidence only**. No actual HIGHCONTRAST GET,
collector execution, hosted discovery, WPF/GUI interaction, display/appearance
change, R01/R02 campaign or calibration was performed. The collector was compiled
only. Execution remains BLOCKED / NOT_AUTHORISED and PR #76 remains NO-GO.

- [Technical report and owner decisions](../../qualification/highcontrast-buffer-safety.md).
- [Independent review and raw source hashes](../../qualification/highcontrast-buffer-review.md).
- [Source identities and unchanged protected Git objects](source-identities.json).
- [Archive file/byte/hash manifest](archive-manifest.json).
- [Original offline output bytes](offline-original-bytes.zip).
- [Unsuccessful preparation results retained separately](preparation-failures.json).

Archive SHA-256:
`d28cc4f433b02f93bdfac1604b270123a16419da0229387104ccc990f5a29c27`
(1,229,220 bytes; 314 members). Each member has its own SHA-256 and byte count.
The ZIP preserves raw CRLF/LF bytes independently of later Git checkout conversion.
It contains all six local preparation/control attempts, their logs and receipts,
the final separate-process Python checks, static/governance controls, and the exact
reviewed source snapshots. It is an evidence archive, not a hosted execution bundle.

| Final evidence | Archive location | Result |
|---|---|---|
| Independent native implementation | `attempt-6/native-controls.stdout.txt` | 21 checks; 2 allocations/2 owner releases, no outstanding allocations |
| Actual managed reader with simulated GET | `attempt-6/managed-controls.stdout.txt` | 20 cases; 53 allocations/53 owner releases, no outstanding allocations |
| Compiler/runtime/source/binary manifest | `attempt-6/receipt.json` | 87 output hashes; eight source bindings; Core 10.0.12, SDK 10.0.401, MSVC 14.44.35207, Windows SDK 10.0.22621.0 |
| Pre-execution native dependency inspection | `attempt-6/imports-*.stdout.txt` | No User32, WPF or dynamic-loader imports in either control binary |
| Real collector, compile only | `attempt-6/collector-build-only.stdout.txt` | Build PASS, zero warnings/errors; not executed |
| Separate-process Python modules | `validation/unit-results.json` and individual logs | 101 PASS: 6 buffer evidence, 21 infrastructure, 14 environment lock, 17 hosted execution guards, 43 qualification controls |
| Static integrity | `validation/static.stdout.txt` | PASS |
| Governance controls | `validation/governance-controls-corrected.*` | 25 PASS |

The earlier combined Python discovery remains **100/101 FAIL**. Its original tool
result is transcribed and labelled as such in `preparation-failures.json`; it is
not represented as an archived raw console log. The unchanged parent-process import
assumption and independently verified diagnosis are explained in the review. The
separate-process passes do not overwrite that failure. The initial wrong governance
directory returned exit 5 / zero tests; that unsuccessful invocation is also retained.

The Microsoft implementation reference is pinned in the report and code. No external
Microsoft source is copied into the first-party test implementation. SDK header and
compiler identities are hashed in the receipt; no runtime certification is inferred.
The failing hosted archives and all previous acceptance evidence remain unchanged.
The final commit/tree and provenance checks are bound by the delivery receipt in
Goal #77 / Draft PR #78, not fabricated inside a self-referential Git commit.
