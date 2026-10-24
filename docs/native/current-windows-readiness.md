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

## CURRENT — Goal VIII existing development-host evidence, 24 October 2026

This appended section supersedes only the present testing qualification; all
historical sections above retain their original source and scope. The accepted
integrated `develop` is now `9b1e73bbf564c53b48149e8c0dcd6fd7acbe308f`, tree
`1fb777317ad6e5c8531eebcb565fefae4227e354`, verified PR #47 merge. Exact-merge
[native 40108034236](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40108034236)
has 25 successful jobs and two historical skips; exact-merge
[web 40108034406](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40108034406)
has three successful jobs and Pages skipped. Both are push, attempt 1, at that SHA.

The user explicitly authorised isolated portable tests on the existing development
computer. Its SDKs do not block **DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION**.
The unchanged clean-machine preflight still reports BLOCKED. This is Windows 11
Home x64 `26300.9711`, normal active-console token, actual native window DPI 144
(150%). No scale/theme/default-app/security setting was changed.

| Current decision surface | Status and evidence boundary |
|---|---|
| Verified frozen portable `.871` | PASS: original archive/API digest, exact source/tree, unsigned self-contained Core/WPF 10.0.13, 528 independent payload hashes before execution |
| Local 24-family entry matrix | PASS: 27 exact-source/keyboard/private-validation/Cancel/reopen cases, including three C4 views and TreeView box mode; `s06` client `6075adbf54aae4eae69b912118b202f459cc715a` |
| Local appearance | NOT RUN as full appearance acceptance: actual 880×600/1280×800 logical bounds at 150%; inspected Venn pair. Ten incomplete large capture frames remain invalid visual evidence |
| Local shell/owner/restart | BLOCKED until a complete accepted receipt: exact Find/Enter selection failed in `s08`; separate foreground refusals prevented later keyboard input. Passed independent substeps remain attributed; later unperformed steps are NOT RUN |
| Local native export APIs | PASS: real DOCX/PDF/PNG/HTML/Docs Site plus canonical source/SVG, API source `1de34f87b92b101398e62922e8c7f6bc8e269130`. Independent Unicode, PDF colours/page, image bytes/dimensions and ZIP checks. Framework-dependent validation runtime is separate (actual 10.0.12 in `s15`); portable runtime remains bundled 10.0.13. GUI export dialogues NOT RUN |
| Host preservation | PASS in completed sessions: observed associations, registration/shortcuts and four existing private-state file hashes unchanged; bounded observation, not a system-wide write trace |
| HOST INSTALLER | NOT RUN — UNSAFE SHARED REGISTRATION. Fixed per-user AppId/uninstall/Start Menu/default-state surfaces cannot be isolated by choosing another install directory; original hosted-only installer guard retained |
| Clean Windows / 100% / 125% / multi-monitor | NOT RUN / NOT CERTIFIED; no feature activation, VM creation, scale change, log-off or restart |
| Physical reader / local OS High Contrast | NOT RUN. Hosted actual OS High Contrast and UIA remain separately labelled |
| INTERNAL BETA | BLOCKED while a critical local smoke or mandatory final feature gate lacks acceptance. Green hosted CI cannot resolve a local failure |
| PUBLIC PRODUCTION | BLOCKED pending separate release certification, historical migration, signing/trust and updater decisions |

The [Goal VIII host report/operator path](goal-viii-development-host-acceptance.md)
contains the exact baseline hashes, local scenario attribution, retained failures,
read-only DPI observer, synthetic state routing, interruption behaviour and safe
commands. The [later clean-Windows mini-checklist](goal-viii-clean-windows-mini-checklist.md)
is **NOT RUN now** and complements the unchanged Goal VII detailed runbooks.

[Draft PR #48](https://github.com/DouglasNLima/local-docs-studio/pull/48) targets
`develop` and stays Draft. Its post-commit engineering receipt names the distinct
final feature SHA/tree, exact-source push/PR runs and attempts, every job conclusion
and skip, preview hashes/expiry, independent 528-path inspection/full payload delta
and any later source-labelled local completion. That feature head is not silently
substituted for the frozen integrated source or earlier local client source.

Candidate `.876`, source `6a6d13505e3babe58b7a9c2e2ebdbdec6b883c3e`, failed the
hosted push lifecycle after 75 desktop checks at a whole-desktop UIA closure
observation (`E_UNEXPECTED`). The original archive/log/screenshot are retained; its
dependent installed-family checks were skipped and are not 72 successful phases.
The bounded harness correction uses native PID/real-HWND discovery and preserves
the action count, original deadline and exact persisted-draft/source checks. The
post-commit engineering receipt must record the final AFTER gates independently.
No product defect, historical RCA or successful draft retention is inferred from
the failed observation.

Production code, dependency pins, private state format, original fixtures and
protective contracts are unchanged. PR #38 remains OPEN / DRAFT / PAUSED at
`31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`; 250 ms remains INCOMPLETE. Historical
`.749`/`.853` PDF/Journey/intermediate ER causes remain INCONCLUSIVE, and the hosted
two-version install/upgrade/reinstall remains SYNTHETIC. No installer was invoked
on this host, and no merge, release, signing, update feed or production deployment
is authorised.
