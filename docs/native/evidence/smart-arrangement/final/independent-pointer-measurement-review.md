# Independent pointer-measurement harness review

Scope: final corrected `NativeArrangementLatencyChecks.cs` supplied for pre-commit review, 28 October 2026. This is a measurement review only; the independently passing product UI/export corrections are unchanged. No source was edited, shared build started or OS pointer input injected by this reviewer.

## Verified improvements

- A pending paced sample records an input-sequence baseline, injection timestamp, requested pointer coordinate and expected ghost coordinate. `Frame` requires a newer Move arrival, arrival time after the recorded injection time, matching pointer target and matching ghost before acknowledgement. This removes the previously possible acknowledgement from unchanged geometry alone.
- Raw frame observations now record only the first WPF opportunity after a new input sequence; they no longer repeatedly increase the age of the same old input.
- Screen probes perform one bounded `BitBlt` into a compatible memory bitmap and then read that bitmap. The receipt separates before-input capture cost, after-input capture cost and compositor flush cost. Positive pixel samples are labelled observed upper bounds and null observations are retained.
- The four-minute whole-workload harness budget and individual 25-second waits are labelled completion bounds. The receipt does not claim actual render-thread execution duration or physical scanout timing, and actual DPI is distinguished from WPF zoom.
- The initial failed hosted receipt is retained. Its repeated old-input ages are not reusable as latency measurements.

## Measurement defects to correct before certification

### H-1 — P2: expensive-work counter omits the arrangement phases it needs to count

`NativeArrangementLatencyChecks.cs:165-167` rejects `renderer.*`, `svg.*`, `arrangement.routing` and `arrangement.native-preparation`. It omits `arrangement.svg-generation` and `arrangement.geometry-validation`, which are the actual phase names in `ArrangementEngine.cs:127` and `:142`. The result at line 206 then hardcodes `PerMoveExpensiveRenderingCalls=0`.

Direct arranged SVG generation or validation during pointer handling could therefore run without failing this guard and still be reported as zero. Derive the count from the observed phase set, include the actual expensive arrangement phases, and report the measured phase counts. This is a harness coverage/counting defect, not evidence that current product pointer handling actually performs those operations.

### H-2 — P2: drop-coordinate assertion uses requested input despite admitted physical rounding

At lines 168 and 177-178, the expected layout delta is `planned.X / dpi` and `planned.Y / dpi`, with a 0.01 layout-unit tolerance. However, the same harness explicitly admits a one-physical-pixel difference between requested and delivered absolute SendInput coordinates at lines 394-396 and 449.

If down and up endpoints incur different rounding, the product correctly moves by the delivered delta but the harness compares it with the requested delta and fails. For example, at 150%, one pixel of endpoint difference is about 0.667 layout units, far above the 0.01 tolerance. Record actual delivered down/up screen positions, use their delta for the expected drawing movement, and retain the requested positions separately. This preserves a strict product geometry check without imposing contradictory input assumptions. No physical-DPI failure is claimed as locally reproduced; the contradiction is source-confirmed.

### H-3 — P2: fixed 80 ms stream drain censors slow tail observations

Lines 156-159 sleep for 80 ms after the last of the 200 producer-paced injections, snapshot the arrival/frame lists, then clear the coordinate-to-injection map. An input or first corresponding presentation later than that cutoff is absent from the reported samples and can no longer be matched after the map is cleared. The receipt labels omitted samples as possible coalescing but does not disclose that the tail is censored by this 80 ms cutoff.

This can bias the slow end of the measured distribution downward, independently of any explicit pass/fail threshold. Wait for the final injection's matching arrival and frame using the existing bounded harness wait, then snapshot and clear. Alternatively, retain and explicitly report censored samples and the observation boundary. Waiting for final acknowledgement is not a new product latency gate.

## Other limits

`ObservedInputEvents` is a count of matched routed WPF events, not necessarily the number of distinct injected inputs observed: repeated events at a mapped coordinate may share an injection timestamp. Keep that name literal; add a distinct-injection count if inferring coalescing. The producer frame list has an injection timestamp suitable for such deduplication. This is an interpretation limit, separate from H-3's explicit censoring defect.

The pixel distribution is conditional on successful pixel observations. `PixelObservedSamples` and all nulls must remain visible; 120 WPF acknowledgements must not be represented as 120 observed pixel updates if fewer changed probes were found. No physical scanout or large-DPI certification is granted by this source review.

**Recommendation:** correct H-1 through H-3 before treating a hosted run as latency certification. The explicit timing/visibility caveats and original evidence retention should remain. Product UI/export assessment remains unchanged.
Reviewed source SHA-256: `75FC5B8323F31A03B14DB3974E012D4AF8370544A901F45618B2D1A6CAB82309`.


## Final source re-review — 28 October 2026

Reviewed stable source SHA-256: `5124F368525E121459D7BA607D0E302E556A94B71870A3728C6F4B43A83025DB`.

**The harness source is ready for publication and a fresh hosted measurement run.** This is a source-review conclusion, not a claim that physical pointer/DPI or latency acceptance has passed. The reviewer performed no shared build, desktop injection, product edit or environment-marker substitution.

| Review item | Final assessment |
| --- | --- |
| H-1 omitted expensive arrangement phases | **RESOLVED.** Observed phases now include `arrangement.svg-generation` and `arrangement.geometry-validation`; the receipt derives total and per-phase counts and preserves them before rejecting unexpected work. |
| H-2 requested versus delivered drop geometry | **RESOLVED.** Actual down/up routed events retain timestamp, sequence and physical point. The expected layout position is derived from delivered physical delta divided by actual DPI; requested positions/delta remain separate. The strict geometry comparison is retained. |
| H-3 fixed tail cutoff | **RESOLVED.** The producer waits for final-injection arrival and presentation under the existing 25-second harness bound. Evidence is snapshotted in `finally`, including final acknowledgement, observation end, distinct/missing counts and right-censoring on failure. The 80 ms cutoff is removed. |
| Correlation ambiguity from adjacent targets | **RESOLVED.** Requested target spacing now exceeds twice the one-pixel matching radius. Independent enumeration of the producer trajectory finds 200 unique targets and minimum Chebyshev separation of four pixels; no pair has overlapping ±1-pixel matching neighbourhoods. The trajectory direction is chosen inside the actual viewport/desktop, or the harness fails explicitly when it cannot fit. |
| Duplicate routed events versus injected samples | **PASS with explicit meaning.** Raw `ObservedInputEvents` and `PresentedSamples` remain separate from `DistinctArrivedInjections` / `DistinctPresentedInjections` and missing counts. Raw arrival statistics must continue to be interpreted as routed-event observations rather than a claim of one event per injected sample. |
| Null pixel coverage | **PASS.** All 120 paced sample records retain nullable pixel observations. The pixel distribution filters positive observations and reports their count separately from requested/WPF samples. Screen capture and compositor overhead remain visible; no null becomes zero latency or a fabricated pixel acknowledgement. |
| Automatic-search failure classification | **RESOLVED.** Cancellation attempt and full search preserve raw status/count evidence before throwing on failure/unclassified outcomes. Such errors can no longer return into an overall passing case. Completed no-improvement and cancelled/resource-deadline outcomes remain explicitly distinct; stale choices are not fresh candidates. |
| Timing boundary / hidden threshold | **PASS.** No new product latency threshold was introduced. Four-minute workload / 25-second feedback waits are labelled harness completion bounds; final-tail timeout fails and retains censoring evidence. WPF opportunities, screen observation upper bounds and actual scanout remain distinct. |

No remaining concrete source-level measurement defect was found in this batch. The new hosted receipt still needs inspection for actual distinct sample counts, null-pixel coverage, delivered coordinates, phase/drop records, search outcome classification and terminal status. Earlier failed measurements and this report's original findings remain preserved; they must not be relabelled as passes.

## Hosted close-test failure — revision `4461591`, 28 October 2026

Preserved evidence: `artifacts/native/final-hosted-input-4461591/responsiveness/pointer-acceptance.json` and `small-er/pointer.json`. The case remains **FAIL**. No receipt was changed or reclassified, and no product build/input was performed by this reviewer.

Independent inspection finds a harness ordering defect in `CloseDuringDrag` (lines 397–399 at the failing revision): it queues Alt+F4, releases Alt and immediately releases the mouse before observing `Closed`. The real mouse-up can therefore reach product `PointerUp` and commit the provisional movement before the queued close completes.

The retained timeline supports that diagnosis directly:

- Closure-test down arrival: `265013.3258 ms`.
- Closure ghost move arrival: `265015.5043 ms`; WPF opportunity: `265015.5971 ms`.
- A **new** `arrangement.apply` starts at `265021.521 ms`, after the close fixture was saved. Its routing, SVG generation, geometry validation, native preparation and presentation installation are all present; apply completes at `265032.8028 ms`.
- The harness subsequently times out waiting for the supposedly clean arrangement to close.

This demonstrates an unintended drop during the closure test. The product's unsaved-layout confirmation is consistent with the timeout, but the exact modal/WM_CLOSE ordering was not captured and is not claimed as directly observed. The evidence does not establish a new product close regression.

Minimal harness correction: retain the held mouse button until `Closed` is observed, release mouse and Alt in `finally`, record pre-close saved/dirty/ghost/capture state, and compare the accepted state after closure with the saved baseline. Checking only ghost disappearance/capture release would not detect an unintended committed drop.

The completed paced/producer/drop/search measurements remain useful **failed-run observations**, not a complete acceptance pass. The producer receipt records 200 distinct arrivals and 200 distinct presentations, final input acknowledged, no tail censoring and zero observed expensive drag phases. A fresh hosted run is required after the close-sequence correction; the first measurements must remain preserved.

### Independent review of the close-test correction

At source SHA-256 `ACDCC5C19625F6C61614CC66325027E5034AFC8EC4D1621F37CD396F408B4945`, `CloseDuringDrag` now retains the mouse button until the actual `Closed` state is observed. It verifies capture/ghost cleanup **and** unchanged, clean accepted state against the baseline captured before the close request. Closure-start, Closing-event and observation-end entries retain state and any post-start apply phases. This addresses the demonstrated test-ordering defect without changing product behaviour or relaxing a timing threshold.

One minor cleanup adjustment was requested: also send Alt-up from `finally`, so a `SendInput` failure after Alt-down cannot leave that injected key held. The success path already releases Alt before waiting. The core regression correction passes independent source review; actual closure acceptance still requires a new hosted run.

The failed `4461591` small-ER receipt contains 120 paced WPF samples, 120 positive pixel observations, zero null pixel observations and 200 distinct producer arrivals/presentations. These remain explicitly failed-run observations. They were read, not modified.

### Final close-test source readiness

Reviewed source SHA-256: `71F01E17879F7603BDCBD1B7E11B2694911D7E623D3469BCDF55AF858A16CE72`.

**Ready for publication and a fresh hosted closure run.** The final source keeps the mouse held until closure is observed, verifies unchanged/clean accepted layout and released capture/ghost, and preserves closure diagnostics. Alt-up now executes in `finally`; a nested `finally` still attempts mouse-up if Alt-up injection throws. No product behaviour or acceptance deadline was changed by this correction.

No remaining concrete defect was found in this close-sequence correction. This review is source-only: the author/coordinator owns the combined build result, and hosted `4461591` remains a preserved failure. The next hosted terminal result must establish actual closure acceptance. The separate test-only preview-request correction is outside this particular follow-up review.
