# Native hardening and acceptance recovery

Goal #75 / Draft PR #76 continuation from `adc68a98d0570bc39858591d252da90a644553d2`, tree `00aac08a1c06821612bbb27b256057bf94a43217`. Exact develop comparison: `99349d82eb06960eb1f9785cf384db2373aed4fa`. The five functional workstreams remain intact.

## Protocol

Preserve original failures; separate native ownership, provider observations and renderer operations. Reproduce P0 arrangement/Timeline/workspace failures with bounded opt-in diagnostics and the same harness against baseline/candidate products. Investigate P1 selection/Packet without input retries or changed geometry criteria. Evaluate P2 Continuous 250 ms and High Contrast independently, with unchanged thresholds/fixtures. Commit minimal corrections separately and bind final regressions and verdicts to the final commit/tree.

## Timeline provider configuration

The retained installed receipt has no owned HWND, zero resize actions, unchanged fixture bytes, a 25,000 ms budget and 39,084 ms recorded acceptance before an IUIAutomation2.TransactionTimeout ArgumentException. It does not prove an application-startup or renderer cause.

Local read-only COM reproduction: TransactionTimeout and ConnectionTimeout reject 0, 1, 10 and 49 ms; 50, 99, 100 and 3,000 ms are accepted. FlaUI converts TimeSpan.TotalMilliseconds to an unsigned integer; a decreasing fractional budget can become unsupported. [Microsoft documents the provider timeout](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomation2-put_transactiontimeout); 50 ms is observed Windows behaviour, not a new acceptance threshold.

The correction floors the provider limit inside the original shared deadline, rejecting remaining budgets below 50 ms before UIA. Native process/HWND observation precedes provider configuration. The launch budget remains 25 seconds, the provider cap remains 3 seconds, Resize remains a single action and no input is retried. Actual UIA boundary controls accompany this correction; hosted application readiness remains required.

## Bounded controlled diagnostics

The opt-in `LENS_NATIVE_ACCEPTANCE_TRACE` harness records at most 2,048 phase events of 4,096 characters, including exact harness/product assembly revisions. It records command delivery, ownership and lifecycle phases without authored text. An external hosted-Windows supervisor retains partial evidence, bounded output and managed stacks/Mini dumps after 60 seconds without progress (eight minutes total). This is a separate diagnostic experiment, not an extension or replacement of the original acceptance deadlines. Uninstrumented suites remain required.

The controlled workflow runs the identical diagnostic harness against candidate and exact develop product assemblies, in both baseline-first and candidate-first order. Arrangement and normal lifecycle use real shown WPF; Packet and selection use actual Windows High Contrast; Timeline uses portable launch for comparison, with installed launch remaining a separate mandatory regression. Passing diagnostics cannot erase a failed original observation.

The independent recovery workflow runs the unchanged Continuous protocol twice with its existing `BaselineRevision` parameter set to exact develop, and the original High Contrast appearance assertion with identical harness binaries in both execution orders. The historical pinned Continuous workflow still runs independently. Neither fixture bytes nor the 250 ms/4.5:1 acceptance definitions change.

High Contrast also separates hosted runtime from the unchanged packaged Core/WPF 10.0.13 runtime. The latter is copied unmodified from the already pinned NuGet runtime packs into a private runner directory; no host installation, product upgrade, framework patch or package publication occurs. Both the harness and its launched application use that private runtime. This controls one observed difference from the historical failed image, without claiming to recreate that older Windows image.

The user's continuation excludes all signing. The disposable-certificate trust execution is therefore NOT RUN on this feature branch; its test and execution on other branches remain intact. Mock signing-policy checks do not sign artefacts. No trust or production-readiness claim follows from this omission.

## First controlled checkpoint, 847b8d1

Original Timeline MiniDumpThreadInfo evidence was analysed with pinned dotnet-dump 10.0.750501. At capture, the main thread is in WPF Dispatcher.GetMessage/Application.RunDispatcher, with no synchronous renderer stack. The retained desktop image shows the Timeline document and canonical preview after failure. Last native observation at 24,900.85 ms still has HWND zero; provider configuration fails at 26,660.50 ms, while the outer acceptance receipt records 39,084.11 ms. This proves neither a permanent startup deadlock nor readiness inside the original deadline. The provider correction does not establish the cause of late/unobserved ownership.

The first controlled diagnostic run exposed two defects in the newly added observer. Replacing `phase.json` failed with Windows access denied in baseline arrangement/lifecycle attempts; an append-only journal now avoids replacing an observed file. A real concurrent-reader control verifies 1,001 complete ordered records. Selection lacked intermediate phase events and could finish at the supervisor's no-progress boundary: both products produced 100 passing observations in candidate-first order, but its supervisor still failed. Selection now emits bounded progress records at existing assertion boundaries, and the supervisor rechecks exit before capture. These experiment failures are retained and are not attributed to product regressions.

Unaffected diagnostic observations include candidate arrangement passing in both orders, baseline arrangement passing in candidate-first order, and candidate normal lifecycle passing. Uninstrumented arrangement and selection gates also passed at this checkpoint. None clears the original ADC failures or establishes their cause. Clean repeated diagnostic comparisons and final-source regression results remain required.

Current appearance evidence reports runner image `20261103.298.1`, .NET 10.0.14 and matching WPF/Win32 highlight `#8EE3F0`, text `#263B50`, contrast 7.893273:1. Historical failure used image `20261021.278.1`, .NET 10.0.13 and divergent WPF highlight `#0078D4`, giving 2.542596:1. Source, runtime and image differences must not be collapsed into a causal claim; the original FAIL remains immutable.

## Interim verdicts

- Functional readiness: previous five-slice evidence retained; final hardening regression pending.
- Candidate regression acceptance: INCONCLUSIVE pending controlled P0/P1 runs.
- Independent performance: retained FAIL; unchanged final-source gate pending.
- High Contrast: historical appearance FAIL retained; separate candidate evidence pending.
- Overall integration: NO-GO; PR remains Draft.
