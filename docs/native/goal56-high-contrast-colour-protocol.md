# Goal #56 — bounded High Contrast colour investigation

Protocol committed before hosted execution. Diagnostic work only; no product or
existing acceptance changes, no PR #73 update and no integration authorisation.

## Identities and retained failure

- Baseline: `ab21919a42ec8b3d17d9ba2e39b4e7e41459e206`, tree `f74345a61d0e9320a8826da8cb2ca3478ee3125e`.
- Candidate: `0e80946eaf15bfd5b6dcf02a3bd16b00d5306569`, tree `ea571be730c48b20a1c70b90f1bcb1883f4dde73`.
- Original run/job: `41345774785` / `124965136051`, attempt 1, FAILURE.
- Original archive `13099735465`: SHA-256 `5abffd2f92594d61856c9f9aea6d284ec69f6def3e5b1c268597b70bd2ee439e`.
- Original failure is prelaunch in the validation process. Requested/effective
  pressed foreground/background are `#263B50` / `#0078D4`, ratio
  `2.542596386238992`; Win32 reports `#263B50` / `#8EE3F0`, ratio
  `7.893273424942036`. Both flags true, bindings Active, Fluent.HC loaded.
  These are sequential observations, not an atomic snapshot or app verdict.
- Raw archive and decoded job log are preserved under
  `C:\Code\goal56-high-contrast-evidence` before any new native execution.

## Fixed execution budget and order

One new diagnostic workflow, one disposable Windows hosted runner, maximum 25
minutes. Baseline first, candidate second; each has isolated build/output/state
directories. No statistical equivalence or order independence is inferred.
At most one additional workflow is allowed solely to repair a tooling failure
that prevents controls from executing, with the failed attempt preserved. No
same-source rerun, complete Desktop UX rerun or passing-result search is allowed.

For each source:

1. Build the exact source with locked dependencies. Run the existing unchanged
   `--ux-screenshots ... --high-contrast` once in its own uninstrumented process.
   Preserve its exit code, diagnostics and every capture, including any failure.
   No observer, early WPF colour read, dispatcher pump or changed assertion is
   introduced in this process. This is new source-labelled evidence, never a
   replacement for the historical failed run.
2. Build equivalent validation-only overlay code. Start a separate normal WPF
   process, record A, then enable actual HC through the unchanged guarded helper.
   Record B immediately after its return. Apply System exactly once to a new
   dictionary and record C: requested slot, bound brush, WPF and Win32 colours,
   ratios, dictionary identity and recursively loaded Fluent variant.
3. Record notification identities/timestamps without reading colours from the
   notification callback. Observe one dispatcher ApplicationIdle completion,
   bounded by 20 seconds, then record D without reapplying System. This is a
   queue-processing boundary, not a promise that all future OS work has settled.
   Missing notifications are recorded as unsupported, never manufactured.
4. While the same helper keeps the actual scheme active, launch a fresh WPF
   child E, read initial colours and apply System once. Do not change the scheme.
5. Launch the actual unmodified product under that scheme using synthetic local
   state with automatic update checks disabled. A separate process-local startup
   observer records only after normal window loading and actual pointer input.
   Exercise a real command with a single mouse-down, capture while held, move
   outside and release to avoid executing it. Record actual template Surface,
   text/icon foreground, IsPressed, hover/focus, UIA identity/state and screenshot.
   Observer failure is a limitation, not a presentation pass. Do not bypass a
   failed prelaunch acceptance to relabel that acceptance; F is separate evidence.
6. Dispose the original HC helper, process notifications once, record restoration
   and verify original flag/scheme/Win32 colours. Terminate only owned test children.

## Observation and source rules

All records carry UTC and process-local Stopwatch ticks/frequency, PID/session,
exact product source/tree, overlay commit/tree (passed by orchestration), runner
image, OS/CPU/SDK and loaded framework/module paths, versions and SHA-256 values.
Snapshots are explicitly non-atomic; per-read ordering and before/after times
remain visible. Requested palette values are read from actual binding sources,
not reconstructed from the later WPF API. Capture attempts/completions, exceptions,
original FAILURE and unsupported observations remain explicit.

Initial normal reads deliberately prime caches in control A: this is a declared
transition control and cannot reconstruct the historical boundary. The unchanged
first process is the passive control. Creating a diagnostic HWND can alter WPF
notification registration; it is confined to the declared transition control.
No framework private cache mutation, notification injection, sleeps, blind retries,
theme loops, fixed colour substitution or alternate passing-pair selection.

The acceptance remains the actual effective foreground/background pair at 4.5:1.
Win32 is a separate observation, never a fallback. Compare official WPF source
with the loaded version; do not assume a tag or a different patch proves exact
historical binary behaviour. Preserve original .NET/WPF 10.0.14 versus package
10.0.13 as a difference, not a cause.

## Decisions and stop

Only a demonstrated source/lifecycle defect can justify a separate correction.
If mismatch does not reproduce, or UI relevance/cause remains unproved, classify
`HIGH_CONTRAST_ROOT_CAUSE_INCONCLUSIVE`, make no speculative fix, retain both
correction/integration NO-GO and publish the missing causal evidence under #56.
Independent agent review follows the investigation. No diagnostic PASS substitutes
for Desktop UX acceptance. Preserve `NATIVE_CONTINUOUS_250MS_FAIL`,
`SIGNPATH_ELIGIBILITY_UNCONFIRMED` and `NATIVE_STABLE_NO_GO`.
