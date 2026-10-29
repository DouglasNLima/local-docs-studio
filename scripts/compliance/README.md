# Reproducing the licensing evidence

These standard-library Python tools inspect data; they do not establish legal
rights, execute packages, sign code or publish releases. Run from the repository
root with Python 3.12+. Use a new output directory for every run so evidence is not
silently overwritten.

```powershell
dotnet restore src/native/LensDocsStudio.Native --locked-mode
python scripts/compliance/audit_open_source.py --revision <reviewed-commit> --output artifacts/compliance/source-audit --fetch-npm --history
python scripts/compliance/verify_npm_archives.py --inventory artifacts/compliance/source-audit/inventory.json --output artifacts/compliance/npm-audit
python scripts/compliance/inspect_distribution.py --output artifacts/compliance/distributions.json <downloaded-zip> <another-zip>
./scripts/native/Test-NativeNotices.ps1
```

`audit_open_source.py` reads the requested immutable Git tree and all locally
available refs for optional pattern screening. It records the ref inventory,
counts and redacted findings. It reads NuGet cache metadata/notices, distinguishes
raw archive SHA-512 from NuGet lock content hashes, and optionally retrieves exact
npm version metadata. Missing packages or legacy URL-only licences stay explicit.
The current top-level web/runtime versions are a reviewed list in the collector;
update that list when dependency pins change. It is not an automatic complete SBOM.

`verify_npm_archives.py` validates exact registry archives, reads tar members without
extracting or executing them, preserves notice bytes under digest filenames and
compares vendored Git blob hashes. Recorded transformations change quoted Mermaid
`.mjs` paths to `.js`, or all `.mjs` extensions including licence-comment/source-map
paths in Mermaid/DOMPurify. Unmatched files remain unresolved. Inspect the error
and coverage fields before interpreting the process exit code; a completed
inventory may contain missing metadata or failed inspections.

`inspect_distribution.py` reads ZIP members with size limits, hashes bytes and
records sensitive-extension/high-confidence-pattern hits without including match
text. It does not unpack nested archives, analyse executable internals, OCR images,
prove malware absence or verify copyright ownership. API digests and downloaded
asset source/version must be verified independently before certification.

Review all output before adding a source-labelled snapshot under
`docs/open-source/evidence/`. Keep downloads, expanded payloads, raw logs, private
data and keys under ignored local storage. No matching secret values belong in
reports, issues, PRs or tool output.

## Solo-maintainer decision packet

Run `python scripts/compliance/rights_ledger.py` after adding/removing source
paths, then `python scripts/compliance/check_foundation.py`,
`python scripts/compliance/check_governance.py` and
`python scripts/compliance/test_governance.py`. The current ledger is under
`evidence/goal62/`; the Goal #58/#60 ledgers remain unchanged historical evidence.
The explicit `configuration/mit-scope.json` allowlist records reviewed original
portions; classification alone never grants rights. Canonical Git hashes bind the
ledger to bytes, with its own row bound by the final tree. Add new paths only after
rights review, then regenerate the ledger. Governance checks require the approved
MIT text, scope/metadata, preserved notices/trust/performance bytes and observed
check contexts. They do not apply GitHub settings or establish enforcement.

Read-only settings reproduction (requires authenticated GitHub CLI):
`python scripts/compliance/observe_governance.py --output artifacts/compliance/governance --check-source <reviewed-SHA>`.
It sends only GET requests and retains no tokens, recovery codes or private reports.
