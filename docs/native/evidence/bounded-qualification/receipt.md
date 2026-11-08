# Bounded R01/R02 execution receipt

8 November 2026 · Goal #77 · **BLOCKED; PR #76 remains NO-GO**

The single [authorised R01 attempt](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42495945111)
failed before any visible-completion boundary was reached. The exact source,
runtime, SDK and image checks passed. All three launched processes then failed
the fixed 1280 × 800 / 100% scope assertion; their retained window geometry was
1024 × 720. The third process also failed exact Windows appearance restoration.
The launcher stopped before V4. **R02 and calibration were not executed.**

| Independent question | Result |
|---|---|
| Original artefact/source/runtime/environment identity | **VERIFIED**, before GUI; unchanged again after collection. |
| R01 collection validity | **INVALID**: 3/4 processes launched; zero qualifying frames/crops; required completion records absent. |
| Original correctness acceptance | Original auditor **FAIL** retained for the sealed startup assertions. The visible-completion journey itself was **NOT MEASURED**; this is no new proof of a preview regression or of candidate safety. |
| Presentation-order attribution | **INCONCLUSIVE**, with no qualifying frames or trace chronology. |
| Cleanup | Three child job trees verified empty and final held input empty. Exact appearance restoration **UNVERIFIED** after V3. |
| R02 | **BLOCKED / NOT RUN**: 0 preflight, 0 application and 0 stock-WPF processes. |
| PR #76 / policy / release | **NO-GO / NOT ACTIVATED / NOT AUTHORISED**. |

## Exact identities and retained evidence

- [Owner authorisation](https://github.com/DouglasNLima/local-docs-studio/issues/77#issuecomment-6723189720): supplied in the attached prompt; [original bytes](owner-attachment.json) and [recorded decision](owner-record.json) retained.
- Reviewed coordinator: `ffc8660100b19944905e2d699e8a27d866904b83`, tree `53f8aa3cde645f302d6ec9a804c87241ae555795`. [Independent pre-dispatch review](independent-pre-dispatch-review.json) resolved three findings and independently passed 17 offline controls.
- Dispatched evidence-only follow-up: `72794c53ea53d2ff13efd85a66d416f33edcc344`, tree `6b5a403b9de6c3d6e82efb150974b0bc5ecbd0f2`. Reviewed coordinator/workflow blobs were unchanged.
- Frozen execution implementation: `77664fbeed39162e17c6d0d2ff24a65f8a7f9d13`. PR #76 product: `7b1334b09a02ad2fbfd4a0234798e6c387d3e634`; develop: `99349d82eb06960eb1f9785cf384db2373aed4fa`.
- [Terminal run](run-42495945111/github-run.json): `42495945111`, attempt 1, job `128778220808`, **failure**. [All four other jobs](run-42495945111/github-jobs.json) were skipped, including discovery and the original calibration matrix.
- [Original complete artefact ZIP](run-42495945111/original-artefact.zip): `13623780293`, 148,276 bytes, SHA-256 `3bb3212f99811c44d31fef9fb0b7f6652ea0a906c29ae41e7bca1e70f62bbbea`. All 63 files, including every partial child log/state/asset, are preserved byte-for-byte. [Original file manifest](run-42495945111/original-file-manifest.json) and [complete decoded job log](run-42495945111/r01-job-log.txt) remain inspectable. Text extracts may have Git checkout line-ending conversion; the ZIP is the authority for original bytes.

The [preflight receipt](run-42495945111/preflight.json), [observed environment](run-42495945111/observed-environment.json)
and [complete restored-file manifest](run-42495945111/restored-files.json) bind all
2,019 original discovery files, three 52-file binary bundles and both 480-file
runtime closures. Image `win25-vs2026 / 20261103.298.1`, Windows Server 2025
Datacenter `10.0.26100.33674`, X64, SDK `10.0.403`, Core/Desktop `10.0.13` and
`10.0.14` matched the locked configuration. The three R01 processes also recorded
the loaded Core/WPF `10.0.14` hashes. No binary was rebuilt and no runtime rolled forward.

Original candidate configuration SHA-256 remains
`7d0d520d2045c9b547c968882e93e2c48c408effce32d602fde30f7546af1a2f`,
with `campaignExecutionApproved=false`, `status=NOT_AUTHORISED`. The separate
[execution copy](run-42495945111/execution-config.json), SHA-256
`0c93b3bc9a8924a59ff3b3fc9336bc94696d0648f01da43edaf82cd44e645e36`,
changes only the approval Boolean, status and owner-authorisation URL. It records
this spent attempt; it grants no permission to repeat it.

## Failed prerequisites and stop boundary

| Cell | PID | Appearance / tracing | Original exit | Job tree / appearance cleanup |
|---|---:|---|---:|---|
| V1 | 7420 | Normal / off | 1 | Empty / verified |
| V2 | 2008 | Normal / on | 1 | Empty / verified |
| V3 | 7372 | Actual OS High Contrast / off | 1 | Empty / **unverified** |
| V4 | — | Actual OS High Contrast / on | **NOT RUN** | Stopped after V3 |

Each retained failure is `Fixed 1280x800 / 100 percent completion scope unavailable.`,
at generated `build/r01/Program.cs:133`, before `RunCompletion`. Failure states
report Editor layout, 1024 × 720 actual geometry and the exact original mixed-buffer
hash. Preferences retain Pages/100% zoom. The sealed constructor caps dimensions
at `SystemParameters.WorkArea`; the observed geometry is consistent with inadequate
hosted work area. Raw work-area/DPI values were not captured, so that explanation
is an **inference**, not a certified display diagnosis. The narrower confirmed
fact is failure to establish the required window geometry before the journey.

There are **zero full observation frames, zero label crops and zero completed
observation boundaries**. The three 1 × 1 `pixel.png` files are fixture assets,
not screenshots. `failure-frame-scope.json` explicitly records no extra capture;
the frame ceiling was not bypassed. No pixel-review PASS can be written.

The [initial state](run-42495945111/initial-state.json) was flags `126`, scheme `""`;
the [final state](run-42495945111/final-state.json) was flags `126`, scheme
`"High Contrast Black"`. The HC-enabled bit was off and held input was empty at
the end, but exact scheme restoration was not verified. V3 retains
`OS appearance restoration/change is unverified`, `cleanupVerified=false`.
Neither empty process trees nor a successful GitHub post-job step clears that failure.

The frozen launcher treats non-zero child exits independently and continued after
V1/V2's scope assertions; its explicit safety/cleanup stop fired after V3. This
exposes a remaining classification limitation: a shared geometry precondition
failure was not distinguished early from an independently observed product failure.
It is not a basis for continuing to R02. The [original audit](run-42495945111/initial-audit.json),
[local audit](run-42495945111/local-r01-audit.json), [terminal record](run-42495945111/terminal.json)
and [R02 stop disposition](run-42495945111/r02-disposition.json) preserve separate outcomes.

## Risk disposition and minimum next work

R01 remains unresolved: this attempt adds no qualified visible-completion evidence.
R02 remains unresolved: its original `2.542596:1` failure against unchanged `4.5:1`
is preserved. V3's separate first-probe palette observation `7.893273:1` is explicitly
outside the original R02 preflight/application sequence and cannot qualify or clear it.
R03–R06 retain their existing controls; these deterministic geometry assertions do
not explain the historical pre-binding startup timeout, TreeView, selection or
pending-interruption observations.

The smallest next technical work is a separately reviewed validation-only proposal
that establishes the required hosted desktop geometry before starting any qualification
cell, distinguishes shared scope failures from product failures, and demonstrates
exact OS appearance restoration. It must preserve the original foreground sequence,
first-read boundaries, 1280 × 800 / 100% scope, deadlines, count ceilings and all
failed evidence. No reduced viewport, ignored scheme mismatch or changed assertion
is accepted here. Any amended tooling/environment must have fresh exact identities
and an explicit owner decision before another campaign attempt.

The [independent final review](run-42495945111/independent-final-review.json) confirms
**R01_FAILED_COLLECTION_INVALID_R02_NOT_RUN_BLOCKED**. Its separate offline audit
reproduces the original INVALID/FAIL result and verifies the complete archive,
all restored-file/binary/runtime identities, process boundaries and stop evidence.
It closes no product or accessibility risk and proposes no retry.

No repeat, R02 dispatch, 56-process pilot, physical-desktop interaction, production
change, threshold change, protection change, merge, signing or release followed
the stop. PRs #76, #78 and #80 remain Draft. The [risk register](../../native-acceptance-risk-register.md)
remains open, and the ADR operational transition remains pending.

Offline validation before dispatch: 17 coordinator, 43 qualification, 14 environment,
10 report and 25 governance controls passed; static, documentation and provenance
checks passed. [Receipts](pre-dispatch-validation.json) also retain development-time
test failures and their corrections. These offline passes do not override the hosted failure.
