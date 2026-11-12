# Focused Outline ownership controls

These local controls require the owner's explicit desktop-input authorisation.
Use fresh output directories and retain every failed or blocked execution. Run
desktop controls sequentially: they own test windows, keyboard/pointer input and
isolated document/session state. They never set hosted-runner flags, change OS
appearance, alter display/taskbar settings or use an installed user's session.

`LocalOriginalControls` accepts `--owner-authorised-local`, a mode, an absolute
native executable path, a fresh output directory and the repository root.

- `caret`: invokes the unchanged original desktop UX assertion body, including
  keyboard insertion at 2130/2142, UIA selection, passive Find, explicit Find,
  Undo/Redo and source/saved-byte checks. The original clipboard helper restores
  its captured clipboard only while it still owns the sequence.
- `viewports`: invokes the original viewport assertion body without its hosted
  display-mutation wrapper. At local 150% DPI its fixed pixel width cannot expose
  the reading-mode control; retain that result as blocked setup.
- `viewports-local-dpi`: the same assertion body, with only initial window resize
  scaled to preserve 1280 Ã— 800 logical pixels. `verify-control-parity.py` checks
  this exact difference. All four modes, top/restore, rapid A/B/C changes, anchor
  Back/Forward and saved-byte assertions retain their original conditions.
- `outline-pointer`: actual desktop pointer/UIA activation from a distinct editor
  caret to a child heading, checking collapsed selection, heading focus and exact
  source/saved bytes. No clipboard writes are needed by this control.

`OwnershipProbe` takes a fresh output directory, the generated original caret
fixture, and generated original viewport A/B fixtures. It shows an owned WPF
window and records a bounded event trace around real binding/layout operations.
It checks passive caret/selection/focus, independent reading positions, explicit
routed pointer/Enter activation and queued navigation superseded by document or
source changes. It uses dispatcher ordering, without sleeps or success retries.
Its text-composition and routed input controls complement the actual desktop
keyboard/UIA controls; they do not claim physical screen-reader coverage.

The `tests/native/outline-toc` controls retain their original sixty assertions
and add consistent clipboard observation, window ownership and cleanup evidence.
Light/Dark and actual Windows High Contrast have separately recorded qualification;
see `docs/native/evidence/pr85-autonomous-qualification/REPORT.md`. No hosted workflow
or performance campaign is launched by these projects.
