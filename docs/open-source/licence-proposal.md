# First-party licence proposal — not an operative licence

**No licence is adopted by this file.** Do not rename it to `LICENSE`, set package
metadata to MIT, or add blanket SPDX headers until the owner explicitly approves
the audit findings, copyright holder and scope. The repository currently has no
root licence. Public visibility is not a copyright grant.

Proposed decision: use unmodified [MIT](https://opensource.org/license/mit) for
verified project-owned application code, tests, build scripts and accompanying
first-party documentation/examples. Preserve separately licensed dependencies,
adaptations, fonts and assets with their own notices. Confirm the year(s) and
copyright holder below; Git author names alone do not establish ownership.

MIT allows commercial reuse and resale by others. The project's promise of free
official distribution does not impose a non-commercial restriction on recipients.
If that freedom is unacceptable, MIT is not the agreed licence and the decision
must be revisited. Apache-2.0 is also permissive and adds an explicit patent grant
and termination provisions, but has more notice/change obligations. There is no
technical need to change third-party Apache licences to MIT. This is an engineering
inventory and proposal, not a legal ownership opinion.

## Candidate text for owner approval

```text
MIT License

Copyright (c) <approved years> <confirmed copyright holder(s)>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Boundaries and SPDX proposal

User-created/imported documentation, customer templates and workspace files keep
their owners' rights; exporting through this application does not relicense them.
Offer the same MIT terms for verified first-party guides/examples if the owner
approves. Treat historical screenshots and copied fixtures separately until their
contents and provenance are cleared.

Confirm permission for the original Lens artwork and generated native icon.
Propose licensing approved artwork files separately and state that copyright
permission grants no trademark endorsement. Names, logos and branding require an
explicit owner decision; do not infer trademark ownership or a trademark grant.

After approval, add `SPDX-License-Identifier: MIT` only to verified first-party
files where appropriate. Preserve existing upstream identifiers, dual-licence
choices, notices and copyright. Use `NOASSERTION` in an SBOM where rights are not
resolved; do not label an entire installer MIT. A package inventory is not a
complete binary SBOM or a licence-compatibility certification.
