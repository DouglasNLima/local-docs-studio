# Goal X feasibility evidence — 26 October 2026

The [decision report](../../continuous-architecture-feasibility-2026-10.md) recommends **ARCHIVE / HOLD**, preserving PR #38 OPEN/DRAFT/PAUSED. Integration is NO-GO. Original stock 250 ms remains INCOMPLETE; strict experimental v2 Input remains FAIL. No new timing or product implementation is claimed.

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
