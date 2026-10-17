# Native Mermaid State Visual Editor

Status: implementation in progress; not certified.

The feature branch starts at consolidated `develop` commit
`77dff109d84c0730c5b6b3574c71b02b112b082f`, including advanced Flowchart
editing (PR #31, merge `91f541ce`) and the Sequence editor (PR #32).
The integrated web checks passed in run 39045995936. Native post-merge run
39045995957 was still running when work began; its final result must be
recorded before certification.

## Engineering scope

Add a dedicated State source model, expected-text transformations, private
history and native WPF editor. Preserve canonical Mermaider rendering and
existing document ownership/version guards. Verify the actual Mermaider
0.15.1 source at package repository commit
`71b64845570d633a94d29e85579c0a02786fece8` before exposing operations.

## Certification pending

Pinned syntax and operation matrices, source preservation, native UIA,
canonical fidelity, High Contrast, performance, existing editor regressions,
final-head CI and unsigned package provenance remain to be implemented and
verified. No completion or package certification is implied by this draft.
