# Geometry and appearance recovery receipt

8 November 2026 · Goal #77 / Draft PR #78 · **BLOCKED / NOT_AUTHORISED**

The original R01 attempt remains INVALID/FAIL, R02 and calibration remain NOT RUN,
and PR #76 remains **NO-GO**. No product, renderer, fixture, original qualification
bundle, requirement, historical evidence or branch protection was changed.

## Decision and evidence

- **Geometry:** the source clamps the window to the work area. Two partial records
  on the locked image show 1024 × 768 physical pixels, **1024 × 720 usable pixels
  at 96 DPI/100%**, and 1044 × 788 maximum tracking dimensions. This predicts the
  observed clamp exactly. Original-run attribution remains INCONCLUSIVE because
  its desktop/DPI facts were not recorded. No WPF window was created here.
- **Proposed host path:** 13 display modes were enumerated, including 1920 × 1080.
  A separately approved disposable-host mode experiment is plausible, but successful
  switching, usable area, stability and restoration are **not verified**. No display
  mode was changed. An alternative disposable VM needs explicit approval and a
  fresh identity/guard review.
- **Appearance:** this host returned flags 126 and a non-null empty scheme string.
  The first release completed; after the second release's begin marker the probe
  exited **0xC0000374 / STATUS_HEAP_CORRUPTION**. The fault origin and pointer aliasing
  are unproved. The full observation and loaded-Core/self-hash validation were not
  reached. Exact appearance restoration remains **UNVERIFIED**.
- **Fail-fast implementation:** spent campaign authority is rejected before V1.
  New read-only discovery is now also disabled before any build or child launch
  pending a reviewed buffer-ownership repair. Offline classifiers distinguish
  infrastructure, collection, original correctness and attribution. The supplemental
  per-cell gate still needs integration into a freshly reviewed launcher.

The [analysis and owner decisions](../../qualification/hosted-infrastructure-recovery.md)
and [readiness manifest](../../qualification/goal77-infrastructure-readiness.json)
record the precise boundaries. The minimum next technical work is safe, independently
verified repeated GET-buffer ownership. No speculative deallocation workaround,
registry edit, colour substitution, WPF prewarming or further hosted run is included.
Any live display or appearance experiment and any later R01/R02 campaign require
separate explicit owner authorisation after their prerequisites pass.

## Immutable execution identities

| Read-only discovery | Source / tree | Result / retained artefact |
|---|---|---|
| [42500058059, attempt 1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42500058059) | `a0e6fd05a930fbb1d483a9929bf2969277f0bdc6` / `5596e55012022921c9435306ad1bdaba510a678b` | FAIL; no desktop output or numeric child exit retained. [Receipt](run-42500058059/receipt.json), [original ZIP](run-42500058059/original-artefact.zip), [job log](run-42500058059/job.log). |
| [42500925642, attempt 1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42500925642) | `dcb1536a9299f65f94d140877042b844f03356e2` / `7b78528cd5c724faa325c1b78bdcf3ada3738123` | FAIL; PID 9612, 30 ordered journal records, native exit c0000374. [Receipt](run-42500925642/receipt.json), [original ZIP](run-42500925642/original-artefact.zip), [job log](run-42500925642/job.log). |

Both runs verified the original image `win25-vs2026/20261103.298.1`, Windows
Server Datacenter build 26100 / UBR 33674, X64 and SDK 10.0.403 identity files before
building the console probe. Both builds passed with zero warnings/errors. Both
workflows skipped all five other jobs, including R01, foreground controls and
calibration. **Zero qualification processes, appearance setters, display setters or
foreground input actions** were executed. The developer's desktop was not used.

Archive SHA-256: first `e1549ab66703379edb6abbb1c9a723ee67464e4b5eee03f31450fda9e5eab3b4`
(232,809 bytes, 36 members / 35 hash entries); follow-up
`0deafb2fd7daa16cc283be5941b763cd720d67d0dd5ab51d4653e75b50a99f21`
(242,013 bytes, 37 members / 36 hash entries). Every manifest entry and compiled
output was checked. ZIPs are the byte-exact authorities; readable text copies use
normal Git line endings. Neither failed result is replaced by a passing rerun.

Start source: `d2529cb10196589422650f35f647019965ff11be`, tree
`6c81db13d4f896d455174e3ccc97d4b40dccaefd`. Exact PR #76 remains
`7b1334b09a02ad2fbfd4a0234798e6c387d3e634`, develop remains
`99349d82eb06960eb1f9785cf384db2373aed4fa`, and PR #80 remains
`22c223c6f56fa41fda2a36102e1eed0af44974ec`. All three PRs remain Draft.
The final documentation/coordinator-lock commit and tree are recorded in the
Goal #77 / PR #78 delivery comment; no self-referential commit hash is invented here.

## Validation and review

The final [21 infrastructure controls](infrastructure-controls-blocked.txt),
[17 coordinator controls](hosted-controls.txt), [14 environment controls](environment-controls.txt)
and [43 original qualification controls](qualification-controls.txt) pass: **95 relevant
local tests**. These are offline synthetic controls and existing harmless console
supervision tests, not hosted qualification. Compilation was performed locally
without executing the probe. Static, documentation, provenance and governance
checks pass; full browser/native CI was not triggered for this validation-only work.

[Pre-discovery review](independent-pre-discovery.json) and
[journalled follow-up review](independent-journal-review.json) bound the executable
content before each discovery. The [final independent review](independent-final-review.json)
audits the actual failed artefacts and remaining restrictions. The native failure
demonstrates why offline acceptance alone is not infrastructure certification.
Original V1–V3 failures, 250 ms policy and 2.542596:1 HC failure against 4.5:1 are intact.
