# Native blank preview — correctness investigation

Goal [#79](https://github.com/DouglasNLima/local-docs-studio/issues/79).
Status: **IN PROGRESS; root cause INCONCLUSIVE; no product fix claimed**.

This investigation is separate from Draft PR #76 and the proposed policy in
Draft PR #78. The original 250 ms gate, High Contrast requirements, 30-second
foreground action observation deadline and correctness assertions are unchanged.
ADR 007 and proposed performance budgets are neither imported nor activated.

## Frozen inputs

| Role | Commit | Tree |
| --- | --- | --- |
| Exact develop | `99349d82eb06960eb1f9785cf384db2373aed4fa` | `e4d137a12cbdd827583fcb841612890dd88736ec` |
| Exact PR #76 | `75de42474514af0e7a6e521235a6517f4acad14b` | `6cb88161e24573649321bcb1d9f3418ba128194b` |
| Historical | `0237b08390d5d1d946c0b19b81d671ccd54fb951` | `fa21b42ca71a4665c618a560679e9c835aad65f3` |
| Original final probe/evidence | `f839182ceb16c20c4a8d58ddd39513aa81c267b5` | `62c359f62ff9ca3ebd66fe1ac2246277ecf0653d` |

The original final campaign is
[42306706590](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42306706590).
Its six failed mixed-document observations (two develop, three PR #76, one
historical) are copied byte-for-byte under [original evidence](evidence/blank-preview/original/sha256.json).
Each retains its screenshot, failure, state, input journal, binary/source identity,
appearance and earlier successful distant Find capture. The original files remain
at their previous locations too. All six screenshots were independently inspected:
the Preview viewport is white while its status claims an updated preview.
None is reclassified or overwritten by a subsequent run.

The mixed fixture SHA-256 is
`6B7CD92A7DCF1DD88FF31E68FB196BDE2AB76655F99E9F9FEE93D82BF57280A0`.
The unchanged journey performs initial Split rendering and full-content checks,
distant preview Find, Editor typing/restoration, selection, editor Find, wheel,
document switch/return, Outline toggle, then Split → Preview → Editor.

## Diagnostic design

The exact PR #78 probe, fixed fixtures, report auditor and source-isolated launcher
are reused. Their collection PASS remains separate from performance acceptance.
An opt-in `LENS_NATIVE_TRANSITION_TRACE=1` test-only observer adds no product
behaviour. It records input/usable boundaries, existing request/cancellation markers,
model changes, document attachment, visibility, size and changed layout snapshots.
Snapshots include process-local object identities, document/source generations,
version ownership, template/visual parents and children, layout validity,
viewport/extents/offsets and presentation-source attachment. It does not log authored
text or file paths, force layout, retry a failed action, or repair the document.
Its declared bounds are 4,000 snapshots and 16 MiB per process. Instrumented and
uninstrumented evidence must be kept separate.

[Initial investigation run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42312451634)
starts with exact develop/PR #76 without transition tracing (four fresh processes
per product, three complete interaction cycles). Its following matrix traces
develop/PR #76 and historical/PR #76, both initial orders, normal/actual OS High
Contrast, four fresh processes per product and three cycles. Every failed child
is retained; no retries convert it into PASS. This bounded campaign is for causal
investigation, not a statistical certification claim.

Local validation before the first campaign: probe Release build passes with zero
warnings/errors; all ten inherited auditor controls pass. The first fixture-byte
check caught checkout line-ending conversion; restoring the original bytes after
the exact-byte attributes were installed resolved it without changing any hash.

A supplementary local foreground attempt could not proceed: Computer Use twice
reported that the returned native window no longer belonged to its reported app
identity. No input was sent. Only the newly launched process, verified by its
isolated executable path, was closed. This is NOT RUN, not product acceptance.

## Delivery boundaries

The native validation workflow skips its actual disposable-certificate signing
step on this investigation branch, matching the user's prohibition on signing.
Mock signing-policy tests and unsigned package validation remain. No signer,
release, deployment, branch-protection or performance-policy configuration changes
are made. Both related PRs remain Draft.

No integration recommendation can be positive until causality and independent
visible/UIA correctness are established. Any shared develop correction must be
incorporated into PR #76 and independently retested at its resulting exact head;
baseline success cannot grant PR #76 retrospective acceptance.
