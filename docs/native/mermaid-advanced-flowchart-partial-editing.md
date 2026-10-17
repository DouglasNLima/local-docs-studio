# Advanced Flowchart compatibility and safe partial editing

Status: engineering in progress; no new implementation or certification is claimed yet.

## Verified baseline

PR #30 remains open and Draft at `833e9db1f8643f7023143dd94349a2709f9ae9ce`.
This branch starts at that exact head and targets
`feature/native-workspace-mermaid-interaction-stability`. Its implementation is
inherited through Git ancestry. The rejected Continuous viewer is excluded.

The source adapter already records individual chain edges, but marks the complete
model protected. Transformation planning, private draft validation and canonical
identity mapping each require global `CanEdit`. An independent node therefore
cannot be changed beside a chain.

## Delivery requirements

- Core owns operation capabilities and bounded source dependency analysis.
- Expected-text patches preserve unrelated original source exactly.
- Every accepted operation requires complete pinned-parser semantic comparison and
  bounded canonical rendering. Unknown dependencies fail closed.
- The native inspector presents individual permissions and diagnostics.
- Private history and document identity/conflict guards remain transactional.
- Synthetic fixtures cover independent branches, chains, repeated declarations,
  styles, nested groups, Unicode, parallel edges and identical document blocks.
- Disposable hosted Windows supplies native execution, actual High Contrast,
  screenshots, regression evidence and unsigned package/lifecycle verification.

Grammar matrices, measured results, immutable CI links and package hashes will be
recorded here as verification completes. Earlier reports retain their original
revision scope. This Draft PR must not be merged or publicly released automatically.
