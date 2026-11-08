# Hosted geometry and appearance recovery — Goal #77

8 November 2026. **Execution remains BLOCKED / NOT_AUTHORISED.** This continuation
permits read-only hosted discovery and offline validation only. The spent authority
for [run 42495945111, attempt 1](../evidence/bounded-qualification/receipt.md) cannot
authorise another campaign. Its V1–V3 failures and all original bytes remain intact.

The [readiness manifest](goal77-infrastructure-readiness.json) separates infrastructure
eligibility, collection validity, original correctness and presentation attribution.
Discovery success cannot make any qualification result PASS. R02 and calibration
remain NOT RUN; PRs #76/#78/#80 remain Draft and PR #76 remains **NO-GO**.

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

1. Select a proposed disposable host/display path after reviewing its observed
   capabilities; approve any display configuration experiment separately.
2. Decide whether to authorise one bounded appearance-restoration infrastructure
   experiment, with an exact initial-state contract and independent readback. A
   deliberately provisioned named baseline needs an explicit decision of its own.
3. After those prerequisites pass, commission the narrowly reviewed gate integration,
   new source/binary/configuration receipts and fresh execution authority. This
   continuation does not authorise that campaign or reuse the spent approval.

Hardware/corpus selection and statistical review remain governed by the existing
[selection sheet](hardware-corpus-selection.md); no specifications or user documents
are invented and no 56-process pilot is activated.
