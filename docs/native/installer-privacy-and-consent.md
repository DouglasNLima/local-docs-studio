# Installer privacy and first-launch update consent

Goal #56 starts from develop `86d60322f6f1f1dec06974bf5f9e1738a3bb80c7`, tree
`552158f43ebf2501c684d210dd9ebae80a3e6f8f`. This work is limited to native privacy
disclosure and update preferences. Native Stable remains **NATIVE_STABLE_NO_GO**;
**SIGNPATH_ELIGIBILITY_UNCONFIRMED** remains unchanged.

## Installer experience

The standard Inno wizard embeds the full local privacy policy, followed by a
keyboard-accessible Privacy and updates page. The scrollable disclosure covers
local documents, settings/recent paths/drafts/recovery, GitHub update metadata,
manual checks and selected downloads, external links/image imports, possible
Windows trust requests, per-user changes, optional associations and uninstall
retention. No browser/network access is needed. Both payloads include `PRIVACY.md`.
No SignPath acceptance, active signing or unbounded telemetry guarantee is claimed.

**Automatically check for updates** starts unchecked. If preference files exist,
**Keep my saved update preference (off if none is valid)** starts checked and
disables the new choice. Clear Keep to explicitly opt in or out. Silent setup
always chooses Preserve. The existing launch entry keeps `skipifsilent`.

## Precedence and persistence

1. Valid saved preferences win by default, including existing preview preferences
   which explicitly contain `AutomaticUpdateChecks: true`.
2. An explicit interactive choice replaces only the automatic-check field, only
   when the preferences SHA-256 still matches the bytes observed by Setup. A newer
   saved decision wins, including changes to other fields.
3. Missing preferences, an omitted automatic-check field and portable first run
   default to false. Recovered state cannot revive an older backup opt-in.

Setup never edits `preferences.json`. After Inno commits the actual installation
(`ssPostInstall`), it writes schema-1 `installer-update-choice.json` under native
private state via a temporary file and atomic Windows replacement/write-through.
Before this boundary cancellation or failed extraction writes no choice. Inno
cannot roll back after its commit; interruption after that boundary retains the
completed installation's choice. Transfer failure blocks optional launch, reports
the failure and returns a non-zero setup outcome. Abandoned `.tmp` files are ignored.

The instruction contains Preserve/Enable/Disable and the baseline fingerprint
(`missing` for new state), without paths or document content. The app validates
schema/action/hash and a 4 KiB limit. An **opt-in is consumed before persistence**,
so interruption can lose an opt-in but cannot replay it. An **opt-out stays pending
until false is durable**, so failed writes/restarts cannot revive saved true.
Malformed instructions remain blocking until a safe false setting is durable,
then are retained under a unique unreadable name. Read/consume failures cannot
authorise automatic requests. Settings merge through the same bounded, durable
atomic `SessionStore` write and backup/recovery policy. This instruction is never
a second effective settings store. The instruction's SHA-256 acknowledgement is
written atomically inside the same preferences record, so an interrupted deletion
cannot replay an old opt-out over a later user choice.

Startup applies preferences before constructing the updater. A readiness flag
guards both timer construction and request eligibility. Pending/failed preference
saves pause automatic checks. The UI choice and durable effective preference must
both allow a request, and recording the attempt must succeed before transport.
Manual Check now remains available after initialisation when automatic checks are
off. Later Help > Updates changes persist normally; failed saves are reported.
If preference initialisation fails, subsequent shutdown/settings writes are blocked
until a successful retry; default fields cannot replace unreadable saved settings.
The application continues restoring the workspace after a preference-specific
failure, with updates gated off. Session writes are blocked until restoration is
safe; an unedited failed-start shell can close without replacing unread recovery.

An isolated `--state-directory` never consumes installer instructions, including
matching files in a validation directory. Startup recognises the canonical default
directory even when an older updater helper passes it explicitly. The new updater
launcher omits that redundant override and preserves it for isolated validation
state. Normal portable execution
uses the safe default or valid native preferences. AppId, state location, shortcuts,
associations, uninstall retention and production trust remain unchanged.

## Validation boundaries

Core tests cover defaults, merges, retention, newer decisions, idempotence,
malformed/oversized/locked input, both directions of failed atomic writes/restart,
and unrelated state/recovery preservation. Synthetic updater interactions prove
zero requests before initialisation, disabled checks, permitted manual checks,
pending/failed saves, persisted enable and restart interval retention. No live
GitHub request is used as deterministic evidence.

Actual executable UI checks inspect the choice through the first timer interval
and verify persisted state on normal shutdown. Enabled installed UI fixtures use
an explicitly synthetic future attempt time to exclude live requests; synthetic
transports separately prove due requests. Wizard UIA captures local policy,
keyboard choices, cancellation, both successful choices and first launch. Hosted
lifecycle tests retain payload/source/version/state checks and add fresh silent
default, in-app changes, silent upgrade/reinstall retention, no unattended launch
and a real invalid-destination failure.

These are not packet capture, SDK-free certification or historical-preview
migration. Forced power/process interruption during physical extraction, clean
Windows 10/11, physical screen reader/DPI and denied ACLs as a distinct Windows
account remain **NOT TESTED** unless separately evidenced. Local installation is
**NOT TESTED**: the owner's shared registration/state cannot be isolated by `/DIR`.
Exact-source results, package identities, independent review and terminal hosted
outcomes are published in the engineering receipt under Goal #56 after commit.

The original [post-PR #68 run 41040108344](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41040108344)
contains `Actual candidate selection did not present a candidate.` Job 124010532372
and original artefact 12970156539 (SHA-256
`dacd7d1d0710dc4aaa37e69c1c8f4068f927b489c5edb2aad7f626a44704ee18`)
are retained locally without rerun. Root cause remains unproven. Terminal status
belongs to the engineering receipt. **NATIVE_CONTINUOUS_250MS_FAIL** and the prior
37/38 failing candidate samples remain separate. No performance fixture, threshold,
deadline, comparator or acceptance workflow changes.
