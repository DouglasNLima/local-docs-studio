# Independent configuration review

8 November 2026 · Technical subagent review; no owner execution approval.

**DISCOVERY_REVIEW_PASS.** The exact unauthorised configuration is ready for the
owner's decision. No unresolved findings remain within this review's scope.

Reviewed discovery run **42484885426**, attempt **1**, source
`77664fbeed39162e17c6d0d2ff24a65f8a7f9d13`, tree
`bcdf6ac20347ac8a81fcf417864657a5fd2e4a09`, artefact **13618494876**. The reviewer
independently fetched GitHub job/artefact metadata and verified the downloaded ZIP
digest against GitHub's recorded digest.

- All **2,019 extracted files** match the ZIP; no missing/extra or unsafe entries.
- Both **480-file runtime closures**, exact loaded assembly/native-host records and
  runtime versions match; roll-forward is disabled. Image/full OS and SDK records
  are internally consistent and bound to the source/run.
- All three **52-file binary closures**, **874 generated-input hashes**, product/
  harness revisions, source-receipt pointers and five successful builds match.
- Configuration remains `NOT_AUTHORISED`, approval false, owner URL null and
  calibration disabled. Process bounds remain **4 / 18 / 0**.
- Live GitHub confirms one successful discovery job and all three original jobs
  skipped. No execution validator or product/WPF process was invoked locally.

The [initial strict raw-source report](independent-raw-source-check.json) retains
**FAIL**: all 831 failed predicates are raw-Git-versus-retained CRLF text comparisons;
there are no other failures. [EOL analysis](independent-source-eol.json) and the
[complete Git export reconstruction](independent-git-export.json) account for every
byte: **833/833 product files** match the exact path selections and committed
attributes under `git -c core.autocrlf=true archive`. All 13 tool-source records
match pinned content with Windows checkout CRLF conversion. The hosted Git setting
was not queried; this establishes the reproduced export profile, not a measured
setting or raw-blob identity. No captured source or failed report was rewritten.

The [final machine-readable review](independent-final-review.json) has SHA-256
`98a1544f79f446c9127191759cf640eac9c22164b4bdd9df2c56b9dab26d5414`.
The [read-only verification script](independent-review-script.py) and
[independent GitHub observation](independent-github-observation.json) are retained
for inspection. The earlier Windows path-length failure remains separately retained.

This verdict covers the observed configuration and retained bytes. A future hosted
job must still match the exact image/OS and file identities and obtain separate
explicit owner authority. It does not qualify actual presentation, High Contrast,
physical hardware, statistical sampling, integration or release. **PR #76 remains
NO-GO; no ADR operational policy is activated.**
