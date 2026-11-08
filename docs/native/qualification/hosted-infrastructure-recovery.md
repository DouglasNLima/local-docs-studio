# Hosted geometry and appearance recovery — Goal #77

8 November 2026. **Execution remains BLOCKED / NOT_AUTHORISED.** This continuation
permits read-only hosted discovery and offline validation only. The spent authority
for [run 42495945111, attempt 1](../evidence/bounded-qualification/receipt.md) cannot
authorise another campaign. Its V1–V3 failures and all original bytes remain intact.

The [readiness manifest](goal77-infrastructure-readiness.json) separates infrastructure
eligibility, collection validity, original correctness and presentation attribution.
Discovery success cannot make any qualification result PASS. R02 and calibration
remain NOT RUN; PRs #76/#78/#80 remain Draft and PR #76 remains **NO-GO**.

The first [read-only discovery, run 42500058059](../evidence/infrastructure-recovery/run-42500058059/receipt.json),
failed after the exact image/SDK checks and successful console build. The child
returned non-zero with empty stdout/stderr; its initial wrapper omitted the numeric
exit code. No API boundary or desktop value can be inferred from that absence.
The entire first artefact and workflow log are retained. A narrowly changed-source
follow-up adds flushed read-boundary records and supervised PID/exit/timeout evidence;
it changes no interop call or product control and still creates no qualification
window. Partial boundary records are diagnostic facts, never a valid full discovery
or a substitute qualification result.

The [journalled follow-up, run 42500925642](../evidence/infrastructure-recovery/run-42500925642/receipt.json),
also failed. It retained useful geometry facts, then exited with `0xC0000374` at
the second HC-buffer release boundary. **Further discovery is disabled before
build or process launch**, pending a reviewed buffer-ownership repair. The
[delivery receipt](../evidence/infrastructure-recovery/receipt.md) links both
unchanged failed archives, exact identities, offline controls and independent review.

## Geometry: established mechanism and missing historical observation

The exact sealed R01 source creates its window with
`Width = Math.Min(1280, SystemParameters.WorkArea.Width)` and the equivalent height
clamp. The generated assertion then requires 1280 × 800 and 100% application zoom
before any completion observation. See the [original probe source](https://github.com/DouglasNLima/local-docs-studio/blob/77664fbeed39162e17c6d0d2ff24a65f8a7f9d13/src/native/LensDocsStudio.Native.UxProbe/Program.cs#L79)
and [qualification source transformation](https://github.com/DouglasNLima/local-docs-studio/blob/77664fbeed39162e17c6d0d2ff24a65f8a7f9d13/scripts/native/qualification/sources.py).
The candidate's [WindowStateService](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Native/Platform/WindowStateService.cs)
also fits initial dimensions to the work area. Initial saved bounds are null in
this journey. The recorded 1024 × 720 dimensions are consistent with that mechanism.

The original run did **not** retain the physical display mode, work area, DPI or
monitor identity. Therefore its exact desktop cause cannot be established solely
from the failed dimension assertion. A new observation can confirm the clamp's
prediction on another host with the same image; it cannot reconstruct the deleted
original VM or prove an unrecorded historical DPI value.

On the same image `win25-vs2026/20261103.298.1`, the follow-up's two flushed
geometry records reported one primary Generic PnP Monitor (`MSH062E`), a physical
mode of **1024 × 768**, a usable work area of **1024 × 720**, **96 DPI / 100% scaling**,
and maximum tracking dimensions **1044 × 788**. The active and process console
session were 2, with `WinSta0/Default`, and no remote session was reported. The
source clamp therefore predicts exactly 1024 × 720 on that observed configuration.
**The clamp mechanism and matching hosted limitation are confirmed; precise
attribution of the original run's unrecorded desktop state remains INCONCLUSIVE.**
The records are partial diagnostics from a subsequently failed process, not a
complete, valid eligibility collection or an actual WPF measurement.

The new console probe reads, twice at fixed boundaries, primary/monitor rectangles,
current and enumerated display modes, monitor device identity, system DPI, process
and thread awareness, scale percentage, window tracking limits, session, desktop
and window station. It creates no window and loads no WPF or Windows Forms assembly.
Its apphost declares System DPI awareness; it calls no DPI-setting API. The proposed
diagnostic mapping is deliberately limited to one primary monitor at 96 DPI/100%
Windows scaling, where physical pixels and WPF DIPs agree. Application zoom remains
100% independently. Other DPI/multi-monitor mappings are INCONCLUSIVE, not equivalent
viewports or a new product support limit. [SPI_GETWORKAREA](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow)
returns primary physical pixels; [GetDpiForSystem](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforsystem)
depends on caller awareness. Apphost declarations are not assumed to describe the
old `dotnet exec` qualification process.

A geometry-eligible disposable host must actually offer a stable primary work area
of at least 1280 × 800 DIPs and adequate window tracking limits. A single 1920 × 1080
display at 96 DPI is a proposed provisioning target, subject to the measured usable
area; it is **not** an observed or certified configuration. Enumerated larger modes
would support proposing a separate, owner-authorised display-mode experiment using
documented Windows display APIs, with original mode/appearance restoration verified.
Enumeration alone does not establish that a change succeeds or survives runner
session behaviour. No such setter is implemented or executed here.

The observed enumeration contained 13 modes, including **1920 × 1080, 32-bit, 64 Hz**.
That is the recommended next *experiment target*, conditional on repairing the
read-only collector. A 1280 × 800 display mode alone would not establish an
800-DIP usable height after desktop chrome. A separately approved disposable-host
experiment would use the documented [ChangeDisplaySettingsEx](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw)
test/apply path without registry persistence, verify the actual work area/DPI/session,
then restore and independently compare the original mode and appearance. There
would be no qualification window or product process. This is a proposal only;
1920 × 1080 mode availability is not verified switching, stability or restoration.

If this hosted image exposes no suitable mode, the alternative is an isolated,
disposable Windows VM with an actual interactive display adapter/session meeting
those requirements, never the developer's desktop. Its image, hardware/display,
runtime and binaries need a fresh lock and separate owner approval. A self-hosted
VM is currently rejected by the sealed runner guard; pretending that it is
GitHub-hosted is prohibited. A reviewed guard/configuration change and fresh
bundle receipts would be required before any qualification. This is not physical
display or native release certification.

## Appearance: enabled state is not scheme identity

The original record changed from `{flags:126, scheme:""}` to
`{flags:126, scheme:"High Contrast Black"}`. Bit 0 is clear in both, so High Contrast
ended disabled; the selected scheme identity did not round-trip. The exact
restoration assertion correctly failed. The original Python reader used
`value.scheme or ''`; the C# helper used `PtrToStringUni(...) ?? ""`. Those readers
lost the distinction between a null pointer and an allocated empty UTF-16 string.
They cannot now establish which original representation was returned. This is an
observation limitation, not permission to call either representation equivalent
to the named scheme or to close the failure.

The new host returned a **non-null pointer containing an empty string**, with raw
flags 126, in both recorded HC copies. That establishes EMPTY for this observation,
not retrospectively for the original VM. Microsoft's [ThemeSettings design](https://github.com/microsoft/WindowsAppSDK/blob/main/specs/themes/ThemeSettings.md)
also distinguishes a selected HC scheme from whether HC is enabled. Persisting
the selected named scheme after disabling HC is consistent with the original
failure; the precise reason the original empty restore request did not round-trip
is still unproved.

[HIGHCONTRASTW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-highcontrastw)
documents separate flags and default scheme fields. Its GET string is system
allocated and must be released with LocalFree. The new getter copies a bounded
string, preserves NULL/EMPTY/NAMED, releases the returned allocation, and retains
all raw flags, active theme identity and relevant Win32 system colours. It makes
no WPF system-colour read. HCF_HIGHCONTRASTON alone does not describe the scheme;
read-only/ignored flag bits are recorded without masking. HCF_OPTION_NOTHEMECHANGE
is not used to suppress notifications or implement a toggle.

The supported API-level restoration **proposal** is SPI_SETHIGHCONTRAST with the
captured raw flags and exact nullable scheme representation, ordinary broadcast
notifications, followed by independent GET/theme/palette comparison and held-input
and child-tree verification. Offline controls test the proposed data and strict
comparison only. There is no live setter in this recovery probe. The documentation
does not guarantee that passing NULL or an empty string clears a previously selected
scheme. Consequently this proposal is **not a guaranteed restoration method**, and
exact restoration remains UNVERIFIED even when a read-only snapshot is stable.

There is an additional **demonstrated collector failure**. In PID 9612, the first
GET/copy/LocalFree completed; the second GET again copied an empty string, then
the last marker was `highContrastFreeBegin`, without its completion marker. The
child exited `3221226356 / 0xC0000374`, [STATUS_HEAP_CORRUPTION](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/596a1078-e883-4972-9bbc-49e60bebca55),
without a timeout or retry. Pointer addresses/aliasing and a crash dump were not
collected. This localises the observed abort boundary; it does **not** prove double
free, identify the origin of earlier heap damage, or explain the first unjournalled
failure. The documented ownership recommendation does not establish that this
implementation is safe on the observed host. No deallocation is removed, no second
read is skipped, and no partial result is promoted to PASS. Safe repeated GET-buffer
ownership must be resolved and independently validated before appearance experiments.
Final loaded-Core/self-hash acceptance was not reached, although the journal reports
runtime 10.0.14 and the image/SDK/build identities were verified before launch.

A controlled live round trip on a disposable VM requires separate explicit owner
authorisation. It must retain the first mismatch and stop; no retries, registry
edits, WPF prewarming, colour substitution, cache clearing, synthetic theme messages
or assertion bypass. A named initial scheme could be a separately approved host
provisioning choice, established before the experiment baseline and independently
validated. It cannot silently normalise the original empty state or erase its failure.
If restoration cannot be demonstrated for the approved baseline, R01/R02 stay BLOCKED.

## Implemented fail-fast boundary and preservation

The hosted coordinator now rejects spent execution authority before preparation,
Windows imports, restoration or V1 launch. The read-only discovery mode excludes
all five existing workflow jobs, including foreground controls and calibration.
It requires exact reviewed source, clean checkout, the original image/build/SDK
identity and exact .NET 10.0.14 Core identity; image drift stops discovery. Only
the new no-UI console probe is built. Qualification bundles are never rebuilt or
launched by this mode. Two fixed reads replace neither a qualification cell nor
a presentation observation.

The pure eligibility classifier rejects missing/shared geometry, unknown units,
session/desktop problems, inconsistent snapshots and incomplete appearance evidence
before V1. Its current output is always BLOCKED because live restoration and new
authority are absent. The separately tested continuation rule stops additional
cells when scope/PID, supervision, empty process tree or cleanup cannot be verified;
an exit code alone cannot classify a candidate regression. That supplemental rule
is **not wired into the frozen launcher**. A future reviewed launcher must consume
both gates before fresh authority is issued; this is an explicit remaining dependency,
not a claim of campaign readiness.

The original sealed launcher, products, runtime closures, fixtures, 1280 × 800/100%
scope, four R01 cells, eighteen R02 processes, 30/240-second limits, memory limits,
seven boundaries, three frames per boundary, 84 full-frame/252-crop ceilings and
assertions remain unchanged. No WPF first-read boundary, trace-off behaviour,
Continuous 250 ms requirement or 4.5:1 contrast threshold is modified. The historical
2.542596:1 failure remains a failure. No WPF Render callback, Ready state or DwmFlush
is treated as evidence of displayed pixels.

## Owner decisions after discovery

| Decision | Recommendation ready for review | Prerequisite / excluded authority |
|---|---|---|
| Disposable-host geometry | Approve or reject a separate 1920 × 1080 mode experiment on the observed hosted image, with measured work area at least 1280 × 800 DIPs and exact restoration. Use an explicitly approved disposable VM if that path cannot qualify. | First repair the read-only collector. No mode change, new image or self-hosted guard substitution is approved here. |
| Original appearance baseline | Preserve the exact observed flags and EMPTY/NULL/NAMED distinction. Decide explicitly whether a separately provisioned named baseline is acceptable for a future experiment. | A named baseline cannot rewrite or waive the original empty-to-named failure. |
| Restoration experiment | Approve or reject a separately bounded HC round trip only after the getter's ownership is qualified; require independent exact readback and stop on the first mismatch. | No appearance change or claim of reliable empty-scheme restoration is currently authorised or established. |
| Fresh qualification | After infrastructure passes, review integration of the pre-V1/per-cell gates, exact new source/binary/configuration receipts, then issue fresh campaign authority if appropriate. | R01/R02 and calibration remain blocked; the earlier authority is spent. |

The minimum technical follow-up is the collector's returned-buffer ownership,
not a product or renderer change. No further hosted run is made in this continuation.

Hardware/corpus selection and statistical review remain governed by the existing
[selection sheet](hardware-corpus-selection.md); no specifications or user documents
are invented and no 56-process pilot is activated.
