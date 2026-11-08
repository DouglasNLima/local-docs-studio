# Hosted qualification environment lock

Goal #77 / Draft PR #78 · **Discovery only; campaign NOT AUTHORISED**

Continues tooling source `4a340fb66ae410f5216310845848197518d4975c` and the
[bounded qualification plan](../native-acceptance-qualification-plan.md). This
discovery records a candidate configuration; it cannot execute R01, R02 or the
56-process calibration pilot. Existing acceptance and ADR activation remain pending.

## Discovery boundary

The existing registered `native-ux-performance.yml` gains one explicit Boolean
`environment_lock_only` dispatch input. When true, its three existing qualification
jobs are excluded and only the discovery job can run. Default dispatch and normal
PR behaviour remain unchanged. A separate `expected_tooling_sha` input must equal
both the dispatched and checked-out commit. Only the owner, including a re-run
actor, in this repository's PR #78 branch is allowed. The job uses read-only
contents permission, SHA-pinned checkout/upload actions and no retained checkout
credentials, secrets, signing, publishing or administrative changes.

This extension avoids requiring a default-branch merge for a new dispatch workflow.
The discovery is manually dispatched after a `[skip ci]` push; no existing broad
workflow is dispatched. [GitHub's dispatch documentation](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow)
describes the default-branch requirement for a new workflow.

`discover_hosted.py` collects ImageOS/ImageVersion, the full Windows build including
UBR/edition/build lab, runner architecture, actual SDK/compiler identities and
available runtime files. It resolves only the two required win-x64 runtime versions
from [Microsoft release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json),
checks the [selected immutable archive identities](runtime-downloads.json), verifies
the actual ZIP SHA-512 and records SHA-256. Separate installation roots contain each
version's complete Core/Desktop dependency closure, including native hostfxr,
hostpolicy, coreclr and WPF dependencies. Unsafe ZIP paths, duplicates, conflicting
overlap, missing versions or changed published digests block discovery.

Two distinct pure console processes load assemblies and report the actual runtime,
Core/WPF assemblies and native host modules. Their runtimeconfigs specify **both**
exact frameworks with roll-forward disabled. They never create an Application or
Window, access system colours, query/change High Contrast or inject input. They are
not any of the later R01/R02 processes and cannot prewarm their WPF first-read state.

The already reviewed offline preparation/build path compiles both pinned products
and all three harnesses, with distinct product/tooling revision metadata, complete
binary receipts and pure metadata/contrast self-tests. No qualification execution
flags are supplied. The original mixed fixture, seven anchors, journeys, frame
ceilings and observation/child limits are unchanged.

## Candidate and execution checks

The [configuration template](goal77-execution-config.template.json) now requires
`environmentLock` and `environmentLockSha256`. Discovery fills these and the existing
source/binary/runtime fields only from observed files. `status=NOT_AUTHORISED`,
`campaignExecutionApproved=false`, `ownerAuthorisationUrl=null` and calibration
disabled remain mandatory. The candidate validator shares the same source, closure
and environment checks as the execution validator but rejects execution approval;
it has no execution path.

Before any Windows execution APIs are imported, the launcher requires the exact
clean source, all generated/source inputs, exact binary file sets, the sealed full
runtime file sets and the discovered image/OS identity. An image-version, UBR,
architecture or runtime-file change **invalidates the configuration**. It must never
be repaired by updating a version string, substituting files or enabling roll-forward.
Repeat discovery and independent review for a changed environment.
The standard `windows-2025` label cannot reserve a historical image version;
availability is conditional on the fresh job passing this exact identity check.

The complete hosted artefact retains source snapshots, runtimes, binary bundles and
receipts at their observed absolute paths. Restore only into a fresh job at those
same paths; relocation is not silently supported. The manifest records the discovery
run/attempt. Downloads and temporary Git worktrees are omitted from the reusable
artefact; official archive identities and complete extracted file manifests remain.
Artefacts expire under the declared retention period. Missing reusable bytes require
fresh discovery/review, not a claimed match from a previous run.

## Owner execution-authorisation checklist

- Review the exact discovery run/attempt, tooling commit/tree, candidate-config
  SHA-256, complete runtime and binary receipts, and independent review.
- Confirm the current hosted ImageOS/ImageVersion and full Windows identity still
  match; reject drift before any input, GUI or appearance operation.
- Name only the previously approved bounded R01 four-process and/or R02
  eighteen-process protocol. No additional cells, repeats or calibration pilot.
- Explicitly authorise those protocols in Goal #77 or PR #78 on the exact reviewed
  configuration. Design approval and successful discovery are insufficient.
- Only then prepare a separately reviewed execution copy with the authorisation
  URL and approved status/Boolean. Preserve this original unauthorised candidate.
- Retain every failure, verify cleanup, and keep collection, correctness and
  presentation-order verdicts separate. Execution does not approve integration.

Until that decision, **no R01/R02 campaign is authorised**. Physical hardware,
consented real documents and statistical review remain pending for calibration and
release qualification. PRs #76/#78/#80 remain Draft; PR #76 remains **NO-GO**.
