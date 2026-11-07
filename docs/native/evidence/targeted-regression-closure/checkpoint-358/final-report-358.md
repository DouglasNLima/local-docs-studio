**Final preview-completion evidence: NO-GO.** This continuation qualifies real pending cancellation in Pages, corrects a demonstrated observer synchronisation error, and retains a final-source visual failure. Continuous/Automatic pending interruption and the exact cause of the original High Contrast discrepancy remain **INCONCLUSIVE**. No product presentation repair is claimed.

Exact final HEAD: `358d2a6f4b8028461fb535f2f71a9b7c71636760`; tree: `81a5921ddc41780ae7aa644a9c2fb37aaef6e70b`. This continues `06073c0bf5a10da6b93c8a26d6d2a8c89b4ce41d` without changing the five functional workstreams or the integrated PR #80 source-position correction. The working tree and remote identity are verified separately at delivery.

**Independent verdicts**

| Question | Verdict | Evidence and limit |
|---|---|---|
| Combined functionality | **Bounded foreground PASS; full campaign not accepted** | 128/128 candidate foreground journeys and 387/388 controls pass. One control fails its 240-second bound; cause INCONCLUSIVE. Previous five-workstream acceptance is retained as historical evidence. |
| Foreground pending cancellation â€” Pages | **PASS within this bounded campaign** | 12/12 qualified observations: primary/secondary readers Ã— comparison closure/Ctrl+Tab/Find dismissal Ã— normal/actual OS High Contrast. |
| Foreground pending cancellation â€” Continuous/Automatic | **INCONCLUSIVE** | All 24 deliveries arrived after the designated restoration ended; none counts as a pending PASS. |
| Visible preview completion | **FAIL observed; exact cause INCONCLUSIVE** | One diagnostics-disabled final-source frame displays the start position and old page counter while WPF/UIA report the end position and Ready. |
| Regression acceptance | **FAIL / NO-GO** | One final candidate control times out and installed TreeView upgrade authoring fails to close the saved application. The earlier same-product selection failure also remains unresolved. |
| Continuous 250 ms | **FAIL** | Both unchanged-gate replications have 42/66 candidate observations over 250 ms; their separate correctness checks pass. |
| High Contrast | **Final gate PASS; overall acceptance INCONCLUSIVE** | Final actual OS/WPF contrast is 7.893:1 against the unchanged 4.5:1 requirement, with all four sizes captured. The earlier same-product 2.543:1 FAIL remains unexplained. |
| Overall | **NO-GO** | Mandatory FAIL/INCONCLUSIVE gates remain. |

**Foreground pending experiment**

The six predeclared cohorts ran all **66 fresh foreground processes and 24 controlled processes**, with 90/90 complete collections. They retain 414 full desktop frames and 660 label crops. There were no retried cells, trace overflows or recorded stale-generation Ready publications. Maximum trace size was 958 of the unchanged 4,000-record bound. All 66 functional/source-integrity summaries pass, and the exact mixed fixture remains SHA-256 `6b7cd92a7dcf1dd88ff31e68fb196bde2ab76655f99e9f9fee93d82bf57280a0`.

One real arrow key resizes the comparison divider. A worker observes the designated native restoration's start marker and sends real Windows pointer/keyboard input. Qualification requires both the native message and WPF delivery before that same operation's terminal marker, with its current uncancelled token and intended target. In all 12 Pages cells, delivery is followed by cancellation and the old operation's rejected outcome. Ownership, mapped text, UIA TextPattern geometry, source files/buffers/dirty state and remaining UI state pass. Controlled calls remain separate and do not qualify foreground timing.

For example, normal Pages primary closure records operation `56292202`, generation 34: start QPC `2063832665`; native pointer-up `2063927102` (+9.4437 ms); WPF delivery `2063928969` (+9.6304 ms, pending task/current uncancelled token/CloseComparison target); cancellation `2063942608` (+10.9943 ms); rejected completion `2064299881` (+46.7216 ms). The twelve complete per-operation records, including both readers and actual High Contrast, are preserved in the independent review receipt.

For Continuous/Automatic, actual restoration duration was 8.0487â€“34.1023 ms, and WPF interruption delivery was **23.4403â€“56.1276 ms after completion**. The experiment therefore did not qualify pending interruption in these modes. Sending input after a start notification is insufficient when delivery occurs after the task ends. No artificial product delay, larger fixture, timeout extension or forced layout loop was introduced to obtain a PASS.

**Visible-completion evidence and attribution**

Independent transcription of every distinct mismatching label crop (16 hashes, viewed without the expected labels) separates 188 OCR recognition errors from **one real final-source pixel difference**. The raw OCR verdicts and original images remain unchanged.

The real failure is in `completion-Pages-normal/foreground/trace-0/pixels-primary/completion-000-primary-find-frame-0.png`. This process has diagnostics disabled. The full frame visibly shows **ReadableStart / Page 1 of 234**, while both before/after state samples report **Ready, current owner/version/attachment, mapped source 4459, native/outer page 257 and Page 257 of 257**, with meaningful UIA TextPattern ranges and geometry. The visible status itself is `Preview updated in 384 ms`, matching WPF/UIA. Thus the failure includes the visible document position, not just a counter recognition error.

QPC frequency is 10,000,000 Hz. The first state sample begins at `2603424939`; DwmFlush returns at `2603742001`; BitBlt spans `2603742917â€“2603956645`; the after sample begins at `2604107054`. The next predeclared frame, `completion-001-primary-find-frame-1.png`, shows **ReadableEnd / Page 257 of 257**. Its BitBlt starts at `2604546011`, 80.3094 ms after the first BitBlt began. The capture routine issues no input between these frames; this uninstrumented process does not have a native-message/render trace. The empty ordinary samples journal is not used as proof of input timing. The first capture began 243.3707 ms after the recorded Find origin; this is not a replacement measurement for the unchanged Continuous 250 ms gate.

This is a retained visual **FAIL**, with a transient difference observed across fixed frames. Missing mutation/render-commit clocks in that diagnostics-disabled run prevent exact attribution to application publication, WPF propagation, compositor scheduling or capture synchronisation. A later correct frame does not erase the first frame or prove that all intermittent failures are eliminated.

The retained original `06073c0b` Pages/High Contrast screenshot lacks the necessary WPF-text, page and render-commit/BitBlt clocks. Its exact cause remains **INCONCLUSIVE**. A separately retained traced reproduction at `945c106b` demonstrates a frame-boundary difference: Ready/native page 259 precedes the outer page-label update; DwmFlush returns before the relevant WPF channel commit; BitBlt straddles that commit, capturing `Preview layout changed / Page 2 of 259`; the next fixed frame shows the current labels. That is evidence for that reproduction, not proof that the original High Contrast frame or final uninstrumented failure has the same cause.

[WPF's threading model](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/threading-model) separates UI and render processing. [DwmFlush](https://learn.microsoft.com/windows/win32/api/dwmapi/nf-dwmapi-dwmflush) waits for already queued DirectX work, not future WPF submissions. The published [WPF page-viewer implementation](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/documents/IFlowDocumentViewer.cs#L452-L483) asynchronously forwards page changes. Those references explain possible ordering; actual runtime identity is recorded, and the published v10.0.0 reference is not presented as a verified source checkout for the runner's 10.0.14 runtime. Desktop pixels are not physical monitor scan-out.

**Focused correction and review**

The demonstrated defect repaired in the final commit is in the **observer**. It previously included UIA-only ItemStatus changes in the render-commit wait. Twenty `945c106b` failures show the last natural visual commit followed by Ready, which requires no new visual work; the observer then waited unnecessarily until the unchanged deadline. The corrected observer tracks bound TextBlock.Text/page-label Content changes separately and accepts an already observed natural commit after those visual changes. It retains every semantic/ownership/Ready event and every pixel assertion. No rendering is requested. The old predicate fails a valid nonvisual ItemStatus control; the corrected predicate passes in all three modes. The first invalid page-label control and all earlier timeouts remain preserved.

The bound-text commit observation is not a guarantee of complete visual presentation or physical scan-out. Independent pixel evidence remains mandatory and is why the final stale frame fails despite passing model/UIA checks.

Affected diagnostic components are `CompletionTrace`, `CompletionJourney`, `ComparisonControl`, the bounded completion runner/auditor and opt-in content-free `PreviewRestorationEvents` markers in `PreviewViewportAnchor`. Product changes since the starting HEAD are those passive task markers only: no product cancellation, ownership, layout or rendering decision was changed. Product/Core/Validation/Web behaviour and existing workflows are byte-identical to the first diagnostic checkpoint. Local exact-source build: zero warnings/errors; three exact-source mode controls pass; 15 observer/UX report tests and 26 governance checks pass. Ordinary diagnostics do not contain authored document text.

**Regression and terminal workflows**

The broader final-source candidate campaign passes all **32 original mixed-document foreground journeys and 96 comparison foreground journeys**. It passes **387/388 controlled checks**. Baseline `75de42474514af0e7a6e521235a6517f4acad14b` (tree `6cb88161e24573649321bcb1d9f3418ba128194b`) passes 27/32 original foreground journeys and 48/96 comparison foreground journeys; its failures and reproduced source-loss controls remain intact. Both test orders, normal/actual OS High Contrast, all three reading modes, Editor/Editor, Editor/Preview and Preview/Preview, zoom/media, switching, Find, selection, cancellation and closure remain covered. The independent comparison audit records 504 visible Ready boundaries without ownership/source/geometry/UIA anomalies, and 96 source-integrity PASS reports. Its original 24 rapid-close observations still all report no pending operation; only the separate lifecycle-qualified experiment establishes pending timing.

The single final controlled failure is `comparison-Continuous-high-contrast-candidate-first/controls/resize-compact/candidate`: exit -1 at the unchanged 240-second child limit. Its retained trace has five startup-only records, no active document, generation 0 and no recorded render request, comparison or resize action. Both output streams are empty; no completed control/exception report exists. This is a **collection FAIL with cause INCONCLUSIVE**, not a demonstrated compact-resize product defect, and is not replaced by a passing rerun.

The final selection gate passes all four candidate cells (normal/actual High Contrast Ã— diagnostics enabled/disabled), each with 100 observations. The earlier `945c106b` diagnostics-disabled geometry FAIL remains unresolved because no product selection correction intervened. The unchanged desktop High Contrast gate now passes at 7.893273:1, including all four required screenshot sizes. Its earlier 2.542596:1 WPF contrast failure, despite Win32 reporting 7.893273:1, is retained and remains unexplained. Separate exact-develop recovery cohorts also pass their four High Contrast cells across both orders and hosted/packaged runtimes; they do not erase that earlier discrepancy. The independent exact-develop performance replications fail 15/66 and 41/66 candidate observations at the same 250 ms threshold, separately from the historical pinned gate's 42/66 failures in each replication.

The complete native workflow terminates with **31 successful jobs, one failed TreeView installed-authoring job and two declared skips**. Core/document safety, native rendering/export/workflow checks, reading productivity, normal/High Contrast Find and the main installation lifecycle complete successfully. Both Web workflows pass Chromium, Edge and static validation; both deployment jobs are skipped. Full job/step outcomes and logs are retained rather than summarised as an all-green suite.

The installed TreeView job fails during upgraded-application authoring with `Saved application did not close`. Initial installation authoring passed; both installation and upgrade matched all 540 portable payload files, and all 45 previously authored/source/state/evidence files were preserved through upgrade and cleanup. The failure occurred inside the workflow step named install/upgrade/reinstall parity, but its recorded failing assertion concerns application closure. The reinstall portion was not completed. Its original archive and UI reports/screenshots are retained. Cause is **INCONCLUSIVE**, and no unrelated repair or rerun was undertaken.

All **14 final-source workflows are terminal: nine success, five failure**. No workflow or failed cell was rerun or cancelled.

| Workflow | Result | Scope |
|---|---|---|
| [Native Continuous 250 ms acceptance](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694241) | **FAILURE** | Historical pinned 250 ms gate: 42/66 candidate failures in each replication. |
| [Native independent acceptance recovery](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694278) | **FAILURE** | Exact-develop 250 ms failures: 15/66 and 41/66; four High Contrast jobs pass. |
| [Native preview completion evidence](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391689023) | **FAILURE** | 90 complete collections; mandatory pending/pixel acceptance remains unmet. |
| [Native preview reflow integration acceptance](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838) | **FAILURE** | 128/128 candidate foreground; 387/388 controls; one candidate startup timeout plus retained baseline failures. |
| [Validate native Windows edition](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694305) | **FAILURE** | 31 jobs pass, TreeView upgrade authoring fails, two declared jobs skipped. |
| [Native editor selection reliability](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694226) | **SUCCESS** | Final candidate selection cells pass; prior same-product failure retained. |
| [Native first-layout attribution](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694200) | **SUCCESS** | First-layout attribution passes. |
| [Native hardening controlled diagnostics](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694297) | **SUCCESS** | Controlled hardening diagnostics pass. |
| [Publish Lens Docs Studio](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688870) | **SUCCESS** | Push: Chromium, Edge and static checks pass; deployment skipped. |
| [Publish Lens Docs Studio](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694338) | **SUCCESS** | PR: Chromium, Edge and static checks pass; deployment skipped. |
| [Validate OSS provenance](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694224) | **SUCCESS** | Provenance validation passes. |
| [Validate native Mermaid creation hub](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694197) | **SUCCESS** | Creation-hub regression passes. |
| [Validate native desktop UX](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694309) | **SUCCESS** | Final UX/actual High Contrast pass; earlier contrast discrepancy remains unresolved. |
| [Validate native workspace and Mermaid interactions](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694215) | **SUCCESS** | Workspace/Mermaid regression passes. |

**Evidence retention and recommendation**

The [first checkpoint receipt](https://github.com/DouglasNLima/local-docs-studio/blob/358d2a6f4b8028461fb535f2f71a9b7c71636760/docs/native/evidence/preview-completion/checkpoint-7bfd/receipt.json) and [second checkpoint receipt](https://github.com/DouglasNLima/local-docs-studio/blob/358d2a6f4b8028461fb535f2f71a9b7c71636760/docs/native/evidence/preview-completion/checkpoint-945/receipt.json) retain original failures, negative controls, terminal results and archive/full-log hashes. The second receipt hashes to `eb76528b148083873a505e51ba1948d7698a1261371a5f90d23316c243017577`; its 166 indexed raw files were verified byte-for-byte after committing. This includes the earlier diagnostics-disabled selection geometry failure, all 20 observer timeouts, original baseline failures and the original High Contrast screenshot through the first receipt. No failed run was cancelled or overwritten.

Final local receipt `artifacts/preview-completion-investigation/final-delivery-358.json`: SHA-256 `eb57db31fedb2bd47e978d4cbb2cedb89877a3ccde900c1a4f042a16f2b44a3c`. It contains the full terminal workflow results, full-log hashes, all **40 downloaded and SHA-256-verified archives**, exact product/harness identities, protected-file checks, independent reviews and every failure above. The original ZIPs, full logs and derived receipts remain in the shared worktree; hosted archive expiry dates remain recorded rather than being represented as permanent hosting.

Key independent receipt hashes:

| Receipt | SHA-256 |
|---|---|
| `hosted-358/independent-final-review.json` | `1ebb12f3e50ce25bb1fb5b14efdb714a10816677f321528fa6c21dfa95e5f5e7` |
| `hosted-358/pixel-frame-correlation.json` | `03d487991bd9552fcdf31db20910a976c2316cd2cbd8d6f61c5d06a4e72ee5e1` |
| `gate-review-358.json` | `93ab4da5958da158250a2da48723c46c6aa59a12ec2b0d4bd93629da1befee9e` |
| `control-timeout-review-358.json` | `7c28aec30602498aec5de87b73e612328fa754a2cc3f155b1afe00822a0c8311` |
| `native-failure-review-358.json` | `97c7403d8cb6a5fc15f8fa2517818a5412187835ccfa7dff49a73d0dbb94c964` |

<details>
<summary>All 40 immutable archive identities and SHA-256 hashes</summary>

| Group / archive | ID | SHA-256 |
|---|---|---|
| gates / [continuous-interactions-high-contrast](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694241/artifacts/13582455019) | 13582455019 | `2a59caae39b24e36acfff3a67b36ac3b5f5815d074295774863053c552d15bed` |
| gates / [continuous-interactions-normal](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694241/artifacts/13582465591) | 13582465591 | `8c802400de25aeab59d797d08545603d205a1db92d902699f9a32fb65fe66a97` |
| gates / [continuous-performance-1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694241/artifacts/13581804757) | 13581804757 | `33a90a42527c84f9e586da6cea9720422827a518cc7e1b80ed1dd1d1795bd5a5` |
| gates / [continuous-performance-2](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694241/artifacts/13582880110) | 13582880110 | `f924cf2d93bfacb8b5b7e3eba6ebe8ee4258a2b03addb33b3a57b264d5836e8c` |
| gates / [native-selection-high-contrast](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694226/artifacts/13581493372) | 13581493372 | `551ac06e9ef3c1ce3044e28f9bb1caf696ade8a5cf33f5f8a05406e3a1330ec1` |
| gates / [native-selection-normal](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694226/artifacts/13581634171) | 13581634171 | `469432c2d8f8544c3bfa78a9f6eb3d709d3aee7ffff63643d23302bf3b406524` |
| gates / [native-ux-visual-evidence](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694309/artifacts/13582975979) | 13582975979 | `e10cc01521ef9b16264dcd1f915b2e985842a02e5c9ee892715c05b649e1f30e` |
| hosted / [completion-Automatic-high-contrast](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391689023/artifacts/13582106217) | 13582106217 | `e12b24bddd6fc45c41d7b270a8fad3e092395fb769edca3dd5d6ef47bf096261` |
| hosted / [completion-Automatic-normal](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391689023/artifacts/13581887206) | 13581887206 | `0e4a33016b20803bfa8ae51c7511937d7e6da5230b8cd91799f61f13ce432e2e` |
| hosted / [completion-Continuous-high-contrast](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391689023/artifacts/13582285979) | 13582285979 | `99571b3d8d105331c1e201cb2208f2bfe985d416f794d3ef58c89cae4973c456` |
| hosted / [completion-Continuous-normal](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391689023/artifacts/13581762374) | 13581762374 | `fb2776c127954a06421bb1355c018a4742f36447ceecf971f9630af4d09be137` |
| hosted / [completion-Pages-high-contrast](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391689023/artifacts/13582190988) | 13582190988 | `97f01acd39eaa2458040b99a48e63b55741e2f31900bcfce3b6db0c80dfb29d2` |
| hosted / [completion-Pages-normal](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391689023/artifacts/13581403055) | 13581403055 | `ef189ca95d501d3f88034a99701fc0a397d621b1600eb3bc19da8917de023001` |
| integration / [comparison-Automatic-high-contrast-baseline-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13582692771) | 13582692771 | `9d1788b5540c7a08821d0e53d5dfd9abded710d2fd275144f27092a677f6371c` |
| integration / [comparison-Automatic-high-contrast-candidate-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13581942866) | 13581942866 | `b836b0d285120dfe6d0f4c96b3092e15e2c5b0f4981e8c14df3bba00e76163b9` |
| integration / [comparison-Automatic-normal-baseline-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13582003869) | 13582003869 | `3800595b0c027efb5d13da1e9e371e682ff7d6f2570e68121bfdfccf8b9f9895` |
| integration / [comparison-Automatic-normal-candidate-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13581779585) | 13581779585 | `aeb2e69ace1ec5d7e57f950dc36c9c90396b36e0bd6ea72d96439f64ef719403` |
| integration / [comparison-Continuous-high-contrast-baseline-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13582560475) | 13582560475 | `249b184b3f6ddb4c96b9867b72dfef40dde68f7d889703501f982d1900e9f0da` |
| integration / [comparison-Continuous-high-contrast-candidate-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13581469593) | 13581469593 | `d1366c3af3fad6954ca782a50fe05a231f49245130afbc03f9baec6c8325c055` |
| integration / [comparison-Continuous-normal-baseline-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13583017017) | 13583017017 | `a6c2e867b3d721f6f767ab4703f2eca3209afdfd385e895355d2087353375e45` |
| integration / [comparison-Continuous-normal-candidate-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13582716715) | 13582716715 | `13424bdc102bde6da4d7f4c07bc02b03daaab206b6c2363c26db32844b7fde5a` |
| integration / [comparison-Pages-high-contrast-baseline-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13583425455) | 13583425455 | `4f133e4b965005c80d9746a97bc1a8adfd417f69886cb896e60f20d37893c556` |
| integration / [comparison-Pages-high-contrast-candidate-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13581723680) | 13581723680 | `3accddf39cda3ed572c1b22b54017107ccb401524690ae102caf4335d692cb54` |
| integration / [comparison-Pages-normal-baseline-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13582462536) | 13582462536 | `0d68a2e960e7c7870ca1f118c654fabd34edb11cd3f8f6a0cefc2d5971a797eb` |
| integration / [comparison-Pages-normal-candidate-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13582455922) | 13582455922 | `2e259ef3e2d908f1009071ce7ab28da8b7625f4c8ac6b1415541a071fbab6b07` |
| integration / [original-mixed-high-contrast-baseline-first-trace-0](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13581383417) | 13581383417 | `c87b3cc88e452faa9db8f359a45de228bf5039e26b337f9f14cee4c644f7118c` |
| integration / [original-mixed-high-contrast-baseline-first-trace-1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13582596270) | 13582596270 | `58ca04236848f74ca14257ea1c18d4bb971703070761bf9a168d600281600581` |
| integration / [original-mixed-high-contrast-candidate-first-trace-0](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13581099244) | 13581099244 | `e916daf34f557cc4134fd8a1347870c248bef8a04a6c7ee731e2142f5b94d005` |
| integration / [original-mixed-high-contrast-candidate-first-trace-1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13582062528) | 13582062528 | `430a49dbca875b68758a6fb149a19840aff3700b5bf1bca4abc83217ead44180` |
| integration / [original-mixed-normal-baseline-first-trace-0](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13581513959) | 13581513959 | `8b5feef17b91cf820ef133f080df553f36c3af5e423ef98f052c628e73d20eea` |
| integration / [original-mixed-normal-baseline-first-trace-1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13581982708) | 13581982708 | `bc19157b459f9844a6c9db4ba31306b384b271a3325c7dda881b969db560bc6f` |
| integration / [original-mixed-normal-candidate-first-trace-0](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13581608413) | 13581608413 | `118fd30267baa3d8903194938634fe9baf5eef7b6aa90c41a68ac6f083b56548` |
| integration / [original-mixed-normal-candidate-first-trace-1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838/artifacts/13581169215) | 13581169215 | `3eba08ee800894d3ee9c97cbc82294a16c73481a7f854592d1fee8bf6f5b65de` |
| native-failures / [native-TreeView-installed-authoring](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694305/artifacts/13583242519) | 13583242519 | `8839e3e1fcd0670ce5ba6821e1123bc9cfaa9c65b243451b8a9cb12c1d5b558a` |
| recovery / [develop-high-contrast-hosted-baseline-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694278/artifacts/13582152658) | 13582152658 | `11405bbd6eac2b762ce2c9cdbdfd45582b45896f905c5176dae31938459ffd4b` |
| recovery / [develop-high-contrast-hosted-candidate-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694278/artifacts/13581968502) | 13581968502 | `baf36607d06eb81ab083208cfc21d6346e582accb5b43bd3168534878418e0e1` |
| recovery / [develop-high-contrast-packaged-10.0.13-baseline-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694278/artifacts/13581573513) | 13581573513 | `b1b7bf323d3fd541dbdea330dc34baed9e5da054afb03b2edc774953f2fb8e8e` |
| recovery / [develop-high-contrast-packaged-10.0.13-candidate-first](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694278/artifacts/13582302996) | 13582302996 | `853050878cc5faa3454633ed127d1adc1f432a0a43787f0650a37f854f15c3c5` |
| recovery / [develop-performance-1](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694278/artifacts/13581984465) | 13581984465 | `a51c0575f9e1a9facdf0546da3c7d478ab66acf5264b4041ebcf8b2606087db0` |
| recovery / [develop-performance-2](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694278/artifacts/13582147472) | 13582147472 | `e95d3c8dbb838702f7d4718080641b06c93338aa34bd20f564f0e40637fb517a` |

</details>

<details>
<summary>Full terminal workflow JSON and log hashes</summary>

| Run | JSON SHA-256 | Full log SHA-256 |
|---|---|---|
| [42391688838](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688838) | `a40bb6ca7088a66896a807a1a321fefb1c8991178b3faafcd123f8f9db947fd0` | `a5eb9159c55f2f009dafab7efb9ae3db13cad496f0581ee06608690e5ff1f8ed` |
| [42391688870](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391688870) | `8610fe13b18e7bbfba5d5e066aeaec98e8c5796c47dd957b5093de62747c43b8` | `2193fdb17c9e30f6077222b53c3bc079134991102be85ac3a1d11b6a449aee8f` |
| [42391689023](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391689023) | `655ac3e7252a786d347d0895c8b741481e2ef247e7a8c0aae879fc99788a8f72` | `b18c7b8d37b7d89408ec2a7002aabedfe92581ba72426b9f868cfff12885d767` |
| [42391694197](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694197) | `c6dc94999eb531fae31d5178a428c95343745078e1ac7e5d9f9628ece2a3d02d` | `ad2d71d68b39c966c1d9e70eff862416d061855cf76b74c0730bb25e74a3f1e2` |
| [42391694200](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694200) | `88616691ee7ffb25fde531f8d88677093c177947d0818202d9b9dae1818f15fb` | `a7b29c5c5aa401e8fab896d99e9fbf2d3075cd11ec87da6729f3688739cb4bb8` |
| [42391694215](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694215) | `3d3b4d2d2c8b1e1f15742b6a91966ec42be0c25d1e51319a9afbdbb0cc6258e8` | `29f913467a4969f784ee47f4124cfa1552bbe205fc663f94dbf1319beaf01dc2` |
| [42391694224](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694224) | `e4b9dbb990f62c905c4adec639e0c7c7a1cfd3d7e8ad6c483fe7dbdedcaf2094` | `e83a597c21b4d8f391c7c0c7efd6685238b6420aa616d8592b86d6151bc12ba0` |
| [42391694226](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694226) | `0702bd52745eea9e03660b92f4fa9bda2789f6e7a30ac33a8ed89e78faf0d7fe` | `f20ee0f53d733f5cad3bfed0c5c5cc7c0697f410d88b5d09ccabc363cf009b88` |
| [42391694241](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694241) | `e0fba554e216795226183a33ce3a4e68559d2942e916ce2c034686667bb3b152` | `5186fe98662ad0b6c24098513c4e6c81f2572deb7ad3add64317d0aa30cd0d46` |
| [42391694278](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694278) | `c952a78376657a9e4f03ed292beac7815ff1f714652af6e01ef2fb047ef61b33` | `7841fe6141e884065fc5f87a8af51d6bcbb5aa6a5a11829d1deba320938ff6cd` |
| [42391694297](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694297) | `0bff7e0fd088b8b32f5d214df44fb8e26ba823c658bd08f72c76ff494e93e48a` | `040098f45bb90f8564158b3bba3c175231cf082018d0937b63ba3995933afca1` |
| [42391694305](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694305) | `f05b24d2356d2e9698cbbdba3eb1bbd9fb6799b08da60ca252e3408d63d0809e` | `a8366033aaa18160759697b27b1da1d7cbe67c15a5d7761091651fc3bfdbfd78` |
| [42391694309](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694309) | `c25802bb8f76293900058cbdde7961790d35c86c2f16d7525101f1e596e27ec5` | `76a6962127e3701ae1ce0debc6fb10c84fb3c7d5209c316fd839d53257004b61` |
| [42391694338](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42391694338) | `8a1a537fb95f3e7231dddf5406b8640c8353ab10f5b892186b10e28a5545a71c` | `ba6713f81ba71611f314bf86847bf5d45c71f456836c4bafa3d842d80024efb9` |

</details>

Keep PR #76 in Draft and Goal #75 open. PRs #78 and #80 remain Draft. The bounded functional integration remains useful evidence, but visible-completion acceptance cannot be granted. Further product correction requires a causally qualified reproduction of the remaining display/publication discrepancy; Continuous/Automatic still need genuinely pending input delivery if that gate is pursued. The retained selection, performance and contrast failures require their own acceptance resolution. No broader renderer optimisation, policy activation or speculative product repair was undertaken. The original 250 ms threshold, High Contrast contrast assertion and proposed ADR 007 are unchanged. No merge, deployment, release, signing, tag or protection change was performed.
