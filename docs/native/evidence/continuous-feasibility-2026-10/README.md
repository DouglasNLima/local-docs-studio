# Goal X feasibility evidence — 26 October 2026

The [decision report](../../continuous-architecture-feasibility-2026-10.md) recommends **ARCHIVE / HOLD**, preserving PR #38 OPEN/DRAFT/PAUSED. Integration is NO-GO. Original stock 250 ms remains INCOMPLETE; strict experimental v2 Input remains FAIL. No new timing or product implementation is claimed.

**Goal X-B:** original report Native CI FAILED; its completed Web run PASSED. Root-cause attribution remains INCONCLUSIVE and report CI is NOT ACCEPTED / reviewer NO-GO. The appended `goalXB` manifest and [offline attribution section](#goal-x-b-offline-attribution-and-retention) supersede the original publication's prospective CI wording. Earlier Goal X data below remains historical and unchanged.

## What is retained here

`evidence-index.json` is a non-executable provenance and decision manifest. It contains:

- Full accepted, experimental, measured, visual and merged source pins/trees/parents.
- Current PR state and source-specific GitHub run/event/attempt/job conclusions, including skips.
- Artefact IDs, archive API digests, sizes, expiry dates and the fact that these hosted archives were not redownloaded in this study.
- The complete local 341-path merge-base diff, classified by role; direct endpoint comparison is separately identified as 680 paths.
- Immutable source URLs, Git blob IDs and SHA-256 of Git blob bytes for 66 primary source/evidence files. Hashes deliberately use repository bytes, avoiding checkout line-ending ambiguity.
- The two-host v2 acceptance rows, all original denominators/cohorts, exact maxima, loaded framework/application/harness identities and original phase statuses.
- The complete 19-entry v2 parity contract, including limitations.
- Independently inspected historical stock comparison receipts, each keeping its own source/harness/host population.

The manifest uses PASS/FAIL/BLOCKED/NOT RUN/INCOMPLETE/PROPOSED for study decisions. Embedded original source/API records retain their original vocabulary, including `success`, `failure`, `skipped`, `PENDING_VISUAL_REVIEW`, `MISSING` and `NOT_QUEUED`. Those original strings are not normalised into more favourable outcomes.

## Independent integrity audit

At immutable PR38 source `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`, the study opened the committed `full-eac5d7b/original-receipts.tar.gz` as data and checked **4,136 retained file members** against their original lengths/SHA-256 values. All matched. The original manifest has **5,000 entries**; **864 explicitly omitted members** were not claimed inspected or reconstructed. No archive executable was run and no archive was extracted over an existing directory.

Every one of the **292** CSV rows was matched to its original archived sample for complete layout, preparation/presentation, timing Input and interaction Input. Per host, 66 stock and 80 candidate samples remain; the candidate consists of 66 controlled samples plus 14 independent UIA cases. Each host retains all 66 pair-identity checks. Every repetition/edit zero remains in its original population. The source has 160 candidate layout/presentation passes, both strict Input gates FAIL and the original pending visual row.

The lifecycle receipts retain version 4 with a MISSING worker-cancellation observation. It is the deliberately injected non-cancellation worker failure, identified by `InjectedFailureVersion` and separately checked for stale rejection. The three natural cancellation observations per host are versions 2, 6 and 22. No missing field is converted to zero; the code's explicit scope is reported.

The later 9558abf visual review and final 31fa3ad executable provenance remain supplements with their actual source identity. This study inspected their records/metadata, not new pixels or a newly executed final-source timing sample. Omitted native ETLs, omitted geometry arrays and hosted binary archives remain linked evidence with finite retention, not independently verified bytes here.

## Reproducible read-only checks

Use the full pins in the manifest. These Git commands reproduce the principal lineage and inventory checks without changing any worktree:

```sh
git rev-parse 0df86ad2bdae005d49be68096c2f784b49de8109^{tree}
git rev-parse 31fa3ad12f3e0c87f5e45cea6f97801af53b67cb^{tree}
git merge-base 0df86ad2bdae005d49be68096c2f784b49de8109 31fa3ad12f3e0c87f5e45cea6f97801af53b67cb
git rev-list --left-right --count 0df86ad2bdae005d49be68096c2f784b49de8109...31fa3ad12f3e0c87f5e45cea6f97801af53b67cb
git diff --name-status a937399a3f822c4e911e1ebe5ec0e426b64c56ac 31fa3ad12f3e0c87f5e45cea6f97801af53b67cb
git diff a937399a3f822c4e911e1ebe5ec0e426b64c56ac 31fa3ad12f3e0c87f5e45cea6f97801af53b67cb -- src/native/LensDocsStudio.Native src/native/LensDocsStudio.Core src/native/LensDocsStudio.Native.slnx
git diff 509ce10819a07ca75c1d637e681eb8181f56fb0c 0df86ad2bdae005d49be68096c2f784b49de8109 -- src/native/LensDocsStudio.Native src/native/LensDocsStudio.Core
```

For each `sources` entry, hash the bytes from `git show <revision>:<path>` and compare with `sha256GitBytes`; `git rev-parse <revision>:<path>` must equal `gitBlob`. Compare tar-member bytes only for original manifest rows with `Archived: true`; count false rows separately. The receipt member paths and raw phase digests in `v2Audit.hosts` bind the summarised values. The complete CSV and raw archive stay at the immutable original source; they are not copied or rewritten here.

All 48 report reference definitions and source line anchors passed checks against their actual files. Of 48 unique URLs, 40 returned HTTP 200 directly and eight returned HTTP 504. The latter were verified through connected GitHub reads (seven immutable blobs and one run with jobs); those transport errors are not misreported as direct HTTP successes. Structured arithmetic/relative-link checks and `npm run test:static` passed. Run/comment URLs are checked against the corresponding connected GitHub resource. A live API artefact listing is not a download check. A historical run's SUCCESS is not a new acceptance result.

## Publication and checks

Only the report, this README and the JSON manifest are publication inputs. Temporary analysis outputs stay ignored. The source scripts/workflows/tests/package locks and all historical evidence are unchanged. Static integrity is checked with `npm run test:static`; structured checks verify JSON, arithmetic, archive digests, source refs, link targets and the documentation-only Git diff. No new test or executable analysis tool is committed.

The report commit uses `[skip ci]` to avoid automatic resumption of the stock controlled-performance PR workflow. Existing Native and Web workflows are explicitly dispatched at that exact new branch head, preserving every internal assertion. Their actual outcome and skips are recorded in the Draft PR delivery receipt. Other suites and all fresh performance work are NOT RUN in this documentation-only Goal. There is no automatic merge, Ready transition or failed-check bypass.

The commit/tree and live CI result cannot be embedded self-referentially in the commit itself. The Draft PR body is the final delivery receipt for those values, with immutable report links. The original source pins in this manifest never silently move when that receipt is completed.

## Goal X-B offline attribution and retention

The follow-up preserves the original three-file scope. `goalXB` is appended to the JSON without rewriting its original keys, source records, measurements or decisions. It retains the original PR body as received (which said PENDING), its digest, completed Native/Web job/step outcomes, the failed invocation and log lines, H1–H5 findings, and explicit missing observations. The final PR body/comment is a separate exact-commit delivery receipt. No historical comment is edited.

Two archives were downloaded into fresh ignored paths under `artifacts/goal-x-b/<full-sha>/run-<id>/attempt-1/windows-2025-vs2026-20261021.278.1/round-<original|comparison>/`. All downloads were data; no application, package or installer was executed. Selected member extraction checked resolved containment, created new files only and did not overwrite previous evidence. Archive-wide SHA-256 was matched to GitHub metadata; member counts are inventory counts, not a claim of reading every package payload.

| Original source/run | Artefact | Verified ZIP SHA-256 | Hosted expiry |
|---|---|---|---|
| `24616837b5289299466cf64e37952d61bc95df6b` / [40375067926](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40375067926) | [12675087255](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40375067926/artifacts/12675087255), 45,893,301 bytes, 3,147 members | `1a740a57c3df3bf6389c4704ab0c8e570b12121d9c7a334ab4e2d8c2eef17a54` | 2026-11-09 18:15:59 UTC |
| `0df86ad2bdae005d49be68096c2f784b49de8109` / [40361576134](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40361576134) | [12668699884](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40361576134/artifacts/12668699884), 342,455,972 bytes, 3,597 members | `7c9c93d3b43825995c94e95e5f75e95c82aadbcb9cd531f1dad76698c0fbec7d` | 2026-11-09 17:09:45 UTC |

For reproduction, retrieve the exact artefact IDs through the GitHub Actions API and write the binary response directly to a new file; avoid text-shell redirection which can transcode ZIP bytes. Hash the ZIP before opening it. Check the manifest's member hashes against the ZIP member bytes, not reserialised JSON or screenshots. Preserve expiry/availability failures as UNKNOWN. The complete failed job log has 740 source lines and a terminal newline, SHA-256 `abe7c6f54a09109b9f4bfa455e21d158681b0cd35d30f32ad86c55994a54c47d`; lines 572–573 contain the invocation and 586–591 the original terminating failure. ANSI escape sequences and line endings are retained in the local original. The develop log digest is `1c02f438b0559e9274a3cb41a59d2af4d9521e8971851b53f540e26a31e9eb98`.

The directly inspected failure members include `ui/native-ui.json`, `ui/native-ui-runtime.json`, `ui/native-ui-threading.json`, `ui/native-ui-performance.json`, `ui/native-refinement-workspace-closed.png`, all `ui/native-ui-failure*` captures/text/dump, the synthetic fixture/session, and refinement display/setup-restoration records. Develop comparison uses the same ordinary `ui/` members, `ui/native-refinement.json` and the post-reopen export capture. Installed passes are kept distinct from this ordinary initial process. No final-head native runtime or UI claim is inferred from either archive.

The ignored external reader first parsed the original minidump's module, memory, thread and process-information streams. Raw pointers on a stack were treated only as leads, not frames. It then performed a bounded native `StackWalk64` (at most 100 frames per each of 23 captured threads), using captured memory first and exact Microsoft PE images for unwind data. This is offline data analysis, with no window, input, registry, clipboard, process-attach or target-launch operation. Only the local system DbgHelp library executes; downloaded DLLs/PDBs are symbol data.

Microsoft symbol files were requested by original dump CodeView GUID/age and image timestamp/SizeOfImage. All thirteen PDB keys independently matched `SymSrvGetFileIndexInfoW`; exact-symbol matching was enabled for the single-threaded DbgHelp walk. See Microsoft's [symbol identity and exact-match rules](https://learn.microsoft.com/windows/win32/api/dbghelp/nf-dbghelp-symfindfileinpath) and [index information](https://learn.microsoft.com/windows/win32/api/dbghelp/ns-dbghelp-symsrv_index_infow). The manifest retains keys, PE/PDB hashes and the exact thread-5800 frames so a reviewer can repeat the unwind in an offline debugger with those files. Stop at the first absent/unregistered/JIT image; do not invent managed frames.

Two intermediate analysis limitations remain visible. A naive comparison against PDB information-stream age initially failed; it was not treated as identity success. The PDB GUIDs matched, DBI ages were 1, and the documented `SymSrvGetFileIndexInfoW` API then independently returned the exact expected GUID/age for all thirteen files. Also, two different comctl32 images share a filename: the second download was refused because that filename already existed, and the reader rejected the mismatched image instead of loading it. That image is outside the resolved thread-5800 chain. Neither is a Native CI result or a live UI negative-control PASS.

The resolved thread is in native common-dialogue initialisation and address-toolbar bitmap loading. This proves the captured native boundary only. The one dump does not establish the cause or duration of the stall, all-thread scheduling, command-event provenance, full HWND ownership, or a repair. The failure UIA inventory is process-filtered, and the outer failure capture follows display restoration; it is not a pre-action desktop inventory. Source assertions establish retained text/root/workspace clearance, but final Undo was not reached in the failed run.

No new hosted shortcut/menu/locator differential or live negative control was executed, and none is represented as successful. No proven precondition or selector defect justified a harness correction. Zero corrective source changes, native launches, GUI actions and CI reruns/dispatches occurred in this follow-up. The diagnostic is a read-only analysis of existing data, not final-source application validation. Static/document integrity checks apply only to the report update; final Native/Web are NOT RUN and the mandatory original failure continues to block formal report acceptance.

The original stock 250 ms result remains INCOMPLETE; v2 Input remains FAIL with every historical value preserved. PR #50 remains Draft/NO-GO for documentation merge acceptance, and PR #38 remains Open/Draft/Paused, ARCHIVE/HOLD with no product integration. No installation, release, signing, deployment or new beta/public-production approval is implied.
