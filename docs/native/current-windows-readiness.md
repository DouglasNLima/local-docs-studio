# Current native Windows readiness

Recorded 24 October 2026 for **Goal VII**, a review/internal-preview goal. This is
the authoritative current-status entry point. Dated chapters in other native docs
remain historical evidence at their own sources; old Draft/pending/family-count
statements do not describe this integrated baseline. Local Markdown, Mermaid, and
documentation studio.

## Integrated source and supported boundary

Verified develop: **`33a751c60886a364102c45cb43e36af1e7f13517`**, tree
**`51912afe438f085bcfbebd33b52e90eb91a7ba33`**, merged
[PR #46](https://github.com/DouglasNLima/local-docs-studio/pull/46).
[Native post-merge 40040943523](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40040943523)
and [web post-merge 40040943517](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40040943517)
are SUCCESS, push/attempt 1 at that SHA. Native has 25 successful jobs; original
.853 replay and historical upgrade skipped. Web has Chromium/Edge/aggregate
successful and Pages skipped. No later feature head is relabelled as develop.

There are **24 bounded editor families**: Flowchart; Sequence; State; ER; Class;
Gantt; Mindmap; Pie; Timeline; Journey; Quadrant; XY Chart; Radar; Sankey; Kanban;
Requirement; GitGraph; Architecture; C4; Block; Packet; TreeView; Treemap; Venn.
C4 Context/Container/Component are three supported views within one family.
[Grammar/protections](diagram-compatibility.md) and the
[24-family UX matrix](evidence/goal-vii/ux-matrix.json) define the actual boundary,
including source-only/ambiguous/unrendered occurrences. This is not universal
Mermaid grammar support.

The native edition remains .NET 10/WPF with pinned Mermaider **0.15.1**, upstream
**`71b64845570d633a94d29e85579c0a02786fece8`**, original native renderer and
canonical SVG/WPF geometry, self-contained official Core/WPF **10.0.13**,
stock Continuous and source-preserving private drafts/owner sessions. The generic
static web app and existing Windows shell remain separate.

## Decision matrix

| Capability / decision | Current status | Evidence and restriction | Decision owner / next prerequisite |
|---|---|---|---|
| INTERNAL CONTROLLED PREVIEW, accepted develop baseline | **READY WITH RESTRICTIONS** | Successful source-labelled native/web gates; unsigned software only, copied/synthetic workspaces in approved disposable x64 environments; inspect important exports | Product/review owner accepts bounded internal evaluation; no merge/release implied |
| Goal VII final feature source | **USE EXACT-SOURCE DELIVERY VERDICT** | The Draft PR's final engineering receipt is the authoritative post-commit result: exact SHA/tree, existing gates and independent package inspection. No result is inferred from this static source document | Implementation agent records evidence after commit; reviewer decides acceptance |
| PUBLIC PRODUCTION RELEASE | **BLOCKED / NO-GO** | Clean-machine, interactive DPI, physical reader, historical lineage and production trust remain uncertified | Release owner; separate explicit authorisation and certification |
| SDK-FREE CLEAN WINDOWS 11 | **BLOCKED / NOT RUN** | Read-only local preflight detects development SDKs/tools; hosted Windows is a developer-tools image | VM owner supplies authorised provenance-recorded clean x64 Windows 11 snapshot and standard user |
| ACTUAL HIGH DPI, 125%/150% | **BLOCKED / NOT RUN** | Existing actual display-resolution and offscreen geometry tests are not scale changes | VM/display owner supplies verifiable interactive scale path and effective window/monitor DPI |
| PHYSICAL SCREEN READER | **NOT RUN** | Named UIA trees/real keyboard/High Contrast are limited accessibility evidence, not audible Narrator/NVDA/JAWS acceptance | Accessibility operator conducts and records real sessions across the matrix |
| HISTORICAL UPGRADE | **NOT CERTIFIED / NO-GO** | Historical selector skipped; 24 × 3 same-source install/upgrade/reinstall phases are synthetic only | Release owner supplies pinned original immutable package plus original app/state and disposable session |
| SIGNING / UPDATER | **BLOCKED for production** | Unsigned internal packages; disposable test-cert rejection controls only; production trust remains empty and unsigned trusted update execution blocked | Trust owner supplies a separately authorised production process; no feed/key/publication here |
| CONTINUOUS 250 ms | **INCOMPLETE / NO-GO** | Historical controlled failures remain; successful lifecycle/correctness is not performance certification | Separate performance decision; no benchmark rerun in Goal VII |
| `.749` / `.853` PDF/Journey/intermediate ER RCA | **INCONCLUSIVE** | Original raw failures retained; later passing lifecycles establish no historical cause | Separate bounded investigation if authorised |

The final feature verdict must stay BLOCKED if any existing mandatory gate fails.
An unavailable **new optional certification** must be recorded NOT RUN/BLOCKED,
not counted as a successful mandatory job. These restrictions can coexist with
READY FOR REVIEW and a restricted internal-preview decision.

## Tested platforms, provenance and packages

Baseline CI uses GitHub-hosted x64 Windows (`windows-latest`, developer tools).
Exact image/build/version belongs to each job log and raw receipt, not to the
workflow label alone. Existing original-five normal/actual OS High Contrast UIA
and the **38 expanded appearances (19 × 2)** are separate suites. Retain 880×600
and 1280×800 checks, 25-second owner readiness and original action deadlines,
zero-tolerance canonical pixels, exact source/owner assertions and all existing
tests. A passing hosted install is not SDK-free Windows 11 certification.

The verified baseline package pair is `0.1.0-preview.865` /
`0.1.1-preview.865`, branch-source at the develop SHA above, from the native run's
`native-validation` artefact **12540566520**, expiring **7 November 2026,
00:10:01 UTC**. Download into a new empty directory using
`gh run download 40040943523 --repo DouglasNLima/local-docs-studio --name native-validation --dir <new-directory>`.
These are baseline bytes, not Goal VII packages. Actions retention is temporary,
not a permanent distribution channel.

The [Goal VII Draft PR #47](https://github.com/DouglasNLima/local-docs-studio/pull/47)
final engineering receipt identifies the **one final
committed feature SHA/tree**, run URLs/events/attempts/conclusions/skips, package
version pair, hashes, artefact IDs/expiry, failed candidates, independently checked
528-file payload manifests and the complete baseline delta. Results are attached
after the final commit so documentation cannot pretend to contain its own hash.
Use `Test-NativePreviewPackage.ps1` with independent installer/portable/source/tree
pins, then `Compare-NativePreviewPayload.ps1` for every actual path/digest change.
The added checks concern exact runtime declarations and tree provenance; they do
not change product dependencies or certify byte-for-byte reproducible builds.

No application/installer from downloaded packages is executed on the developer
desktop. Internal disposable installs use the established association-exclusion
policy; ordinary product installer defaults remain intact. No production signing,
updater feed/manifest publication, tag, Release, Pages deployment or merge is
authorised. A local unsigned `native-update.json` packaging fixture is not an
authorised updater publication.

## Required manual scenarios and retained history

Follow the [certification protocols and release candidate/rollback checklist](goal-vii-certification-runbooks.md)
for real 125%/150% scale, physical screen reader, clean Windows, backups, dirty
saved/untitled sessions, templates/images/content library, relative workspace paths,
safe cancellation and uninstall/reinstall. Preserve original and failed states;
do not overwrite personal documents or change default applications.

The [Goal VII audit](goal-vii-ux-audit.md) records inspected source and reproduced
defects. Original source-labelled receipts remain:
[accepted consolidation](accepted-native-consolidation.md),
[#44 / Expansion V](expansion-v-engineering-receipt.md),
[#45 / .853 repair](native-853-postmerge-pdf-repair.md), and
[#46 / Expansion VI](expansion-vi-engineering-receipt.md).
They are not silently rewritten or promoted into new-source certification.

[PR #38](https://github.com/DouglasNLima/local-docs-studio/pull/38) stays
**OPEN / DRAFT / PAUSED**, source
`31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`. It is not edited, resumed, merged,
cherry-picked or closed. Its experimental architecture and the historical 250 ms
comparison are outside this goal.
