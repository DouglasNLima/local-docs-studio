# Hosted environment lock — delivery receipt

8 November 2026 · Goal #77 / Draft PR #78

**Technically verified candidate; ready for an explicit owner execution decision.**
R01, R02 and calibration were **NOT RUN**. The original
[candidate configuration](candidate-config.json) remains `NOT_AUTHORISED`, with
`campaignExecutionApproved=false`, no owner-authorisation URL and calibration disabled.

| Identity | Observed value |
|---|---|
| Discovery | [Run 42484885426, attempt 1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42484885426): SUCCESS; all three existing qualification jobs SKIPPED |
| Tooling commit | `77664fbeed39162e17c6d0d2ff24a65f8a7f9d13` |
| Tooling tree | `bcdf6ac20347ac8a81fcf417864657a5fd2e4a09` |
| Candidate configuration SHA-256 | `7d0d520d2045c9b547c968882e93e2c48c408effce32d602fde30f7546af1a2f` |
| Environment lock SHA-256 | `45a6aa515df05d93169521119b3bdab59db9512a439041e987495e373d5c466e` |
| Hosted image | `win25-vs2026`, `20261103.298.1`, X64 |
| Windows | Server 2025 Datacenter; `10.0.26100`, UBR `33674` |
| Actual SDK | `10.0.403`; compiler/host file hashes retained |
| Required diagnostic runtimes | Microsoft.NETCore.App **and** Microsoft.WindowsDesktop.App `10.0.13` and `10.0.14`, in separate roots; roll-forward disabled |
| Product sources | PR #76 `7b1334b09a02ad2fbfd4a0234798e6c387d3e634`; develop `99349d82eb06960eb1f9785cf384db2373aed4fa` |

The receipt-only delivery commit does not replace the tested tooling identity. Its
exact SHA/tree is recorded in the PR #78 and Goal #77 delivery comments. A future
execution checkout must use the **tooling commit above**, preserving its clean
source and the original configuration's observed paths.

## Retained evidence

- [Environment and all runtime SHA-256 manifests](environment-lock.json): **480
  files per version**, official archive URL/published and verified SHA-512, archive
  SHA-256, actual loaded Core/WPF/native-host files and separate probe PIDs.
- [Source receipt](source-receipt.json): **874 generated inputs**, product trees,
  seven unchanged fixture anchors and original harness/source hashes.
- Complete binary closures: [R01](r01-bundle-receipt.json),
  [R02 candidate](r02-candidate-bundle-receipt.json),
  [R02 baseline](r02-baseline-bundle-receipt.json), **52 files each**. The product/Core
  carry their exact product revision; harnesses carry the tooling revision.
- [Hosted validation result](validation.json), [terminal GitHub jobs and artefact
  identity](github-terminal.json), [independent byte verification](retained-bytes-verification.json),
  [independent configuration review](independent-configuration-review.md) and
  [machine-readable delivery receipt](delivery-receipt.json).
- [Actual SDK/OS information](dotnet-info.txt), [available runtimes](available-runtimes.txt)
  and [available runtime file hashes](available-runtime-files.json).

The complete reusable [validation artefact 13618494876](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42484885426/artifacts/13618494876)
is **185,677,817 bytes**, SHA-256
`8b3d50ff7b9b394867fdc9598fe14e47d980e865c7d74f389d7802be90c7f812`.
Its downloaded digest matches GitHub's digest. It retains full source/runtime/binary
bytes; the linked Git files are review copies of receipts, not a relocated runnable
bundle. Expiry is **8 December 2026**. Restore to the recorded paths in a fresh
matching hosted job and verify every file; absent/expired bytes require fresh
discovery and review.

## Validation and preserved failures

[Fourteen environment controls](environment-controls.txt),
[43 existing qualification controls](qualification-controls.txt), ten existing
auditor tests and 25 governance controls pass. Static, documentation, provenance,
inventory and whitespace checks pass. [Two product and three harness builds](bundle-build.txt)
plus the [pure runtime probe build](runtime-probe-build.txt) succeed with zero
warnings/errors. Hosted candidate verification and independent retained-byte
verification both pass. No GUI/input/HC or calibration process is executed.

[First discovery run 42484264490](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42484264490)
remains a **failed, incomplete discovery**. A full product checkout hit a Windows
path-length limit before product preparation/build; it produced no candidate config.
[Its original failure, log and partial environment/runtime observations](first-discovery-failure.json)
remain retained, bound to artefact SHA-256
`f3902b85b4823b40e45569a2c3dfc4b65eb0292e5a42f26b74e77bb612b9f4f0`.
The correction uses fresh sparse product worktrees, explicitly materialises their
indexes and verifies pinned HEAD/clean state. Compilation still reads exact Git
archives; historical source/evidence is neither modified nor deleted. The local
intermediate sparse-checkout control also rejected an unmaterialised index before
the correction passed. These are tooling observations, not native acceptance results.

Independent source review also retains a **failed raw-Git-blob comparison**: 831
exported text files differ by LF→CRLF conversion. All **833** product files then
match a full reconstruction with `git -c core.autocrlf=true archive` and the original
path selections exactly (426 candidate, 407 baseline; two already raw-byte exact).
There are no unknown byte changes, missing/extra files or source drift. The hosted
Git setting was not separately queried; the claim is exact reproduction of the
captured export profile. The raw comparison, successful reconstruction and captured
bytes are retained separately in the independent review evidence.

## Remaining prerequisites

1. **Separate owner authorisation** tied to the tooling commit, configuration hash
   and only the original R01 four-process and/or R02 eighteen-process protocol.
   Use the [owner checklist](../../qualification/hosted-environment-lock.md#owner-execution-authorisation-checklist).
2. A fresh hosted job must pass the same ImageOS/ImageVersion, full Windows identity,
   SDK/runtime files and complete source/binary checks. Standard runner labels cannot
   guarantee future image availability. **Any drift blocks execution** and requires
   new discovery/review, never edited version strings or roll-forward.
3. Restore the complete retained artefact at its observed paths while available.
   Preserve this unauthorised candidate; any authorised execution copy requires a
   separately recorded owner decision.

No runtime identity or binary prerequisite remains unresolved in this observed
environment. Actual foreground/visible completion, HC propagation/restoration and
accessibility remain unqualified. Physical H0/H1 hardware, three consented real
documents, resource limits and statistical review remain prerequisites for the
separate calibration/release work; this discovery does not certify them.

**PR #76 remains NO-GO; PRs #76/#78/#80 remain Draft.** All original 250 ms,
2.542596:1 High Contrast and correctness failures remain intact. No thresholds,
rulesets, required checks, production code, signing, release or ADR operational
authority changed. The unchanged High Contrast requirement is **4.5:1**.
