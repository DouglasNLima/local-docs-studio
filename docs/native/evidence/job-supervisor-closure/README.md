# Job supervisor closure: offline receipts

**No native fixture, HIGHCONTRAST collector, hosted workflow or desktop experiment
was executed.** Supervisor qualification remains BLOCKED; one local fixture control
is proposed and NOT_AUTHORISED. The historical total-2/active-1 observation is retained
as FAIL with INCONCLUSIVE cause.

- [Causal assessment and fixed protocol](../../qualification/job-supervisor-closure.md).
- [Independent technical review](../../qualification/job-supervisor-review.md).
- [Exact unapproved local plan](../../qualification/job-supervisor-control.json).
- [Source and runtime-file receipt](source-receipt.json).
- [Original offline transcripts and canonical source snapshots](offline-evidence.zip).
- [Exact local raw source snapshots](raw-source.zip) and [file hashes](files.json).

Source **S**: `331dc5ab220e5ca7dd145ff21a650d3d2578111f`, tree
`776ef245307e9e969e19352597662de46bd35907`. The final package **E** and tree, plus
the exact unapproved owner record, are published in Goal #77 after E exists. No
self-referential or invented commit identity is embedded in this packet.

| Artefact | SHA-256 |
|---|---|
| Local plan | `415e0ecb2e10328e3a6850da926ab1c9ef237072352a7a903fb1f46424081be3` |
| Source receipt | `ce52f5bb9d472f7d42e2e9f0305d2d916317f2ab14a0badd880e8fd3f5f1ec6f` |
| Offline ZIP: 72 members, 93,358 bytes | `36355fb0469dc7b0a4cd5ced4c051a3c48233730952775faaf1cfd06e7898c92` |
| Raw source ZIP: 16 members, 64,338 bytes | `6da92b5c88c1a3d511fd9b7aab742e29fc3c59d1dfc97137723099f58887602e` |
| Original fixture archive, unchanged | `d28cc4f433b02f93bdfac1604b270123a16419da0229387104ccc990f5a29c27` |
| Original harmless fixture EXE, 20,480 bytes | `311655cd165d4a82631efce4b827decea6a5199d2e723da67175b94a4539e549` |
| Original failed control preparation ZIP, unchanged | `09e7c8918b1e48fc8cd24f43b700e57dfcff423f31ce4f2cdc5cf562e65fb9aa` |

All 16 execution-source paths are bound twice: canonical Git S bytes and actual local
bytes. Both sets are retained; they must not be represented as identical where line
endings differ. The plan also pins the available local CPython 3.12.0 executable and
`python312.dll` by path/hash. The proposed driver requires `-I -S`, checks source/plan/
Python files before and after collection, and never loads the HIGHCONTRAST collector.
Reading and hashing archive members did not execute or rebuild the native fixture.

## Validation and retained failures

Paths below are members of `offline-evidence.zip`; raw transcripts retain their
original bytes. These are mocks/static checks, not live Windows Job observations.

| Evidence | Result and disposition |
|---|---|
| `sealed/test_native_job_supervisor.py.*` | **29 PASS** against committed S, isolated `python -I -S`, 0.991 s. Fake kernel/clock, including identity rejection cleanup, journal failure, native errors, fixed windows and contradictory evidence. |
| `sealed/test_native_live_verification.py.*` | **17 PASS** against committed S, isolated `python -I -S`, 3.010 s. Existing authority/collector controls retained and extended to the complete supervisor journal. |
| `offline-4/` | Supervisor/driver 28 PASS at that stage; existing hosted 17 PASS; infrastructure 21 PASS; live verification 17 PASS. |
| `final-source-checks/` | Supervisor/driver 29 PASS; live verification 17 PASS; foundation/governance PASS; 25 governance controls PASS; static PASS. Initial whitespace FAIL (one extra EOF blank line) retained; blank line removed before S and staged whitespace check passed. |
| `offline-1/` | Original mock-integration FAIL retained: the new fake supervisor created output files before the old evidence helper's exclusive writes. Resolved by using a separate mock log directory; no evidence assertion bypass. |
| `offline-2/` | Original 19/20 supervisor result retained: exact float subtraction in a mock assertion yielded 3.9999999999999996. Only that assertion uses approximate equality now; actual deadline/window checks remain exact and unchanged. Live verification 17 PASS. |
| `offline-3/` | 25 supervisor controls PASS with a retained test-harness ResourceWarning: its patched Path.open mishandled keyword-only mode. Corrected in the fixture; subsequent clean runs retain the deliberate journal failure without this unintended warning. Live verification 17 PASS. |
| `sealed/source/`, `sealed/plan.json`, `sealed/source-receipt.json` | Canonical 16-file source closure, exact plan, source/tree/runtime-file/fixture hash bindings. |

Independent review found and closed five flaws in the working proposal: identity
rejection orphaning an owned child, journal failures blocking cleanup, unbound
operation results permitting false PASS, per-stream membership reappearance and
bunched cleanup samples. These were reviewed before any native-control execution;
they are not asserted to explain the historical 2/1 observation. Independent test
results and the final exact binding check are in the linked review.

The earlier PID 37804 evidence, all R01 V1–V3 failures, appearance-restoration and
heap-corruption archives remain untouched. Product, renderer and workflow files are
unchanged. Browser/full-suite CI was unnecessary for this validation-only change;
no workflow was dispatched. The historical Continuous 250 ms and 4.5:1 requirements,
required checks, protections, Draft PR states and PR #76 NO-GO are preserved.
