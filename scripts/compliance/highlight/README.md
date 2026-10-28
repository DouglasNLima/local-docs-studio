# Reproduce the existing Highlight.js asset

This is a maintenance recipe for an existing third-party asset, not an application
build step. The web application still runs directly from its checked-in files.
No runtime dependency or vendor programme bytes are changed by Goal #58.

From the repository root, using Node 24.22.1 and Python 3.12:

```sh
npm ci --prefix scripts/compliance/highlight --ignore-scripts --no-audit --no-fund
python scripts/compliance/highlight/verify_upstream.py
node scripts/compliance/highlight/build.mjs
```

The lockfile pins all maintenance dependencies and npm archive integrity values.
The verifier independently downloads the Highlight.js 11.11.1 archive, checks
its pinned SHA-256 and SHA-512 integrity, compares all 1,559 installed files, then
compares all 194 upstream sources in the historical CDN map. The remaining map
source is the CDN's generated header. No downloaded package lifecycle scripts run.
Old reconstruction tools have deprecated transitive tooling dependencies; they
process the pinned source only and are not shipped in the application.

The first successful experiment used Rollup 2.79.2, CommonJS 22.0.2 with
`strictRequires: false`, node-resolve 13.3.0 and Terser 5.39.0. The initial default
CommonJS trial wrapped modules differently and did not match; it was never adopted.
Tool audit then identified [a Rollup path-traversal advisory](https://github.com/advisories/GHSA-mw96-cpmx-2vgc)
and [a deepmerge advisory](https://github.com/advisories/GHSA-gcqx-w48m-w2h4) through
node-resolve. The final recipe pins **Rollup 2.80.0**, removes node-resolve/deepmerge
entirely (CommonJS resolves these relative imports itself), and retains CommonJS
22.0.2 and Terser 5.39.0. This produces exactly the same unminified and minified
programme bytes. It is a maintenance-tool correction, not an application upgrade.
The recipe restores the historical header and map identifier, with LF line
endings and no final newline. Result: **955,998 bytes**, SHA-256
`980aa9a979a537d5f05c76d313c5f8b48f0bccba293a75e3c271975fce968aa0`.
This is a working reconstruction recipe, not a claim to have recovered jsDelivr's
private historical configuration or every original build-tool version.

Outputs go only to ignored `test-results/highlight-rebuild/`. The script compares
the result with the canonical Git blob because Windows `core.autocrlf` checkouts
can add seven CR bytes. No vendor asset is overwritten. `build-result.json`
records the complete and body hashes. A newly generated diagnostic map has 194
sources; it is not the historical 195-source CDN map and is not deployed.

The existing root-relative `/sm/...` map trailer does not resolve beneath this
project's GitHub Pages path. It is a pre-existing debugging limitation, not runtime
code or a new remote service dependency. The verified historical map remains
retrievable from the exact CDN URL recorded in the evidence. The source-map
comparison alone would not have authenticated the generated code; the separate
full-byte reconstruction now does.

The old bundle header contains build attribution but no BSD grant. The exact
Highlight.js BSD-3-Clause copyright and licence are retained in the upstream
notice evidence and the new cached `assets/vendor/THIRD-PARTY-NOTICES.txt` pack.
No MIT grant applies to this bundle. Runtime behaviour has no semantic changes;
the separately minified trial output is not distributed.
