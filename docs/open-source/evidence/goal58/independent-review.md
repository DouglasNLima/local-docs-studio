# Independent engineering review

The first review was read-only, by a separate agent with a fresh review context.
It reviewed commit `fd9ab97ea00e7a98cff889b245f4e3065151ff0f`, tree
`a22cce0aee9ac5edd218132bcd84e3c59b1a2f0b`, against baseline
`22d434a923a03e86ebee237e71c55ef89dd996ed`. **No actionable material findings.**

Independent checks included exact Highlight.js reconstruction, 1,559 installed
source files/194 historical map sources, 20 NuGet archive hashes, 534 individual
package-member claims, all 537 original portable file hashes and lengths, source
metadata, outer artefact and both installer hashes, notice/rights checks, workflow
placement and permissions, and the licence/governance/SignPath boundaries.

That review covered the original 55-reference web notice pack and 2,417-path
rights ledger. The Mammoth follow-up was independently reviewed at commit
`5f35772be88fafe78364fddb51baa03746f6f13d`, tree
`36f54f50a6658d1d2264920f985f60115561c6ae`. Fresh supplier/archive downloads
reproduced all 12 unique original notice files and both evidence records, including
isarray's README grant. In-memory assembly reproduced the complete 68-reference,
65-component web pack; the checker covered all 2,434 paths. Programme bytes remain
unchanged and the stated attribution gaps remain visible.

One concrete evidence finding was identified: `mammoth-notice-gap.json` incorrectly
put the upstream commit SHA in `sourceTree`. The actual tree was independently
verified through the [Git commit endpoint](https://api.github.com/repos/mwilliamson/dingbat-to-unicode/git/commits/b27f259b49907f99b1b9097abba5a9668106b779)
as `a6835fcdb31ca22965128795cf2d7184426f6e26`, with exactly the recorded 14 paths,
and the field was corrected before publication. No other actionable material
finding was identified. The final correction SHA/tree and its review confirmation
are recorded in the PR receipt to avoid a self-referential commit.

This is independent agent engineering review, not human GitHub approval, legal
ownership clearance or evidence that repository controls have been enabled.
