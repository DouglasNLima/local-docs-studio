# Versioned v2 raw evidence

Historical v1 evidence remains in its original source folders. These v2 records never reclassify a v1 failure. Full raw hosted runs retain geometry/style arrays and captures for 90 days; committed archives retain the original measurement/failure bytes and representative GUI evidence. Source/host/temperature groups remain separate.

| Source | Full hosted raw run | Durable measurements |
|---|---|---|
| a0336ff | [run 39214470766](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39214470766) | [all original observations](a0336ff/every-sample.json.gz), two receipt archives; 60/66 controlled samples per adapter |
| f86d6df | [run 39227279725](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39227279725) | [separate host/temperature tables](f86d6df/measurements.md), CSV, manifest and receipt archives |
| ba0f3f6 | [run 39230058924](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39230058924) | [separate host/temperature tables](ba0f3f6/measurements.md), CSV, manifest and receipt archives |

development-receipts.tar.gz preserves 299 local exploratory timing/failure receipts, including interrupted faulty diagnostic probes and focus-loss observations. Dirty local source/harness identities, temperatures and desktop activity are not controlled acceptance. No failure is promoted to a pass. These records contain generated test fixtures, not user workspace content.

Extract a receipt archive into a new folder with `tar -xzf <archive>`. Recreate aggregate CSV/group extrema with `python scripts/native/summarise-layout-architecture-v2.py <raw-replication-1> <raw-replication-2> --output <new-report-folder>`. This read-only helper has no measurement clocks, retries or percentile calculations.

Large JSON manifests/arrays are gzip-compressed to keep the incremental code diff reviewable. Decompression preserves the original JSON bytes; the adjacent CSV remains directly readable.

## Archive SHA-256

- `281d684890d9ebd46d2dc378d0dcca7145a48a1a6b11fa379ff51cdc67c2a854` — a0336ff/replication-1-receipts.tar.gz
- `92ca6e275e5ab38453a727b8d06c78699a9dca3d7c0fbef432a612057a6129c8` — a0336ff/replication-2-receipts.tar.gz
- `6e9f82dff1fbdf58d5b6fda388c1f8860103a9a88da2114e16dd75f3d181933a` — ba0f3f6/replication-1-receipts.tar.gz
- `b1d73323ff403f0d14267231196b8e65e84c572be08ac84b002df7adf76ffb21` — ba0f3f6/replication-2-receipts.tar.gz
- `1acaa25835b41185f4008747c0d3af09df86ff26d8459fa077527a7f8963e344` — development-receipts.tar.gz
- `0bbdb989fc5941159962275039805d9454096d0156794734f183d43078705c68` — f86d6df/replication-1-receipts.tar.gz
- `36386ce17ebfdb578a704f5460e77e618f3bbbb5610e83f97207afb05c390d4a` — f86d6df/replication-2-receipts.tar.gz
