# Full controlled v2 comparison at 47dff1e

Run: [39291239503](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39291239503). Both strict gates fail. Every original 66 sample per adapter/host completes. Additional paragraph UIA fails on both hosts; its completed timed boundaries remain in the CSV. Candidate completed-observation counts are 79, plus that one boundary-only case per host. Additional cases never enter the original 66.

`summary.json.gz` retains complete original file hashes and per-source/host/temperature groups. `every-sample.csv` retains all 292 timed observations, including repetition/edit zero and boundary-only failures. HostRoot is the local downloaded evidence directory, not a measurement-host name. No percentile, retries, forced collection, cold prewarm or outlier suppression.

## Host 1

Machine `runnervm0xl3a`; 4 available logical processors; `AMD64 Family 25 Model 1 Stepping 1, AuthenticAMD`; `Microsoft Windows NT 10.0.26100.0`; image `win25-vs2026` / `20261007.264.1`; `.NET 10.0.13`; ReadyToRun; 640 x 300, DPI 96, zoom 100%, Light. Initial adapter order: stock then candidate.

Source `47dff1e77981fc0db03c6be9f58168201c5411b9`, clean; harness SHA-256 `E9903604EE10ED08243C7FEB62BAC100DCDD271868CB3F56EF462947617F5EDB`. Official runtime signatures pass and every 66 paired identity checks passes. Raw protocol, stages, thread identities, source/fixture/runtime hashes, natural GC and allocations are in the adjacent receipt archive.

### stock — original six workloads

Stock layout is the distinct original first-attachment clock. Candidate layout starts before required preparation, including native theme measurement, and ends after complete installation/layout/Input. Both end-to-end clocks exclude process/source I/O and empty shown-host setup, retained separately; stock viewer construction/attachment is inside its readiness clock. Candidate layout and readiness share the conservative pre-parsing start; the latter also includes the final idle/Input flush. Their near equality is disclosed, not hidden preparation.

| Fixture / group | n | Layout min–max ms | Preparation/presentation min–max ms | In-process Input maximum ms |
|---|---:|---:|---:|---:|
| details / distant-edit | 3 | 90.64–103.70 | 131.12–145.84 | 111.62 |
| list / distant-edit | 3 | 689.98–1470.18 | 819.09–1843.33 | 1446.90 |
| mixed / distant-edit | 3 | 322.34–386.04 | 460.96–645.57 | 402.25 |
| paragraphs / distant-edit | 3 | 163.17–183.82 | 230.46–241.80 | 196.27 |
| quote / distant-edit | 3 | 78.57–99.16 | 106.78–123.74 | 117.73 |
| table / distant-edit | 3 | 242.21–247.48 | 311.77–324.24 | 238.79 |
| details / first-combined | 1 | 97.31–97.31 | 147.02–147.02 | 86.09 |
| list / first-combined | 1 | 1560.34–1560.34 | 2041.64–2041.64 | 1537.60 |
| mixed / first-combined | 1 | 554.90–554.90 | 872.74–872.74 | 550.57 |
| paragraphs / first-combined | 1 | 159.58–159.58 | 232.83–232.83 | 162.26 |
| quote / first-combined | 1 | 91.84–91.84 | 118.10–118.10 | 111.83 |
| table / first-combined | 1 | 458.62–458.62 | 598.96–598.96 | 436.29 |
| details / repeated | 4 | 92.45–106.41 | 128.02–163.73 | 130.24 |
| list / repeated | 4 | 636.99–1447.08 | 850.46–1797.71 | 1419.92 |
| mixed / repeated | 4 | 330.15–371.01 | 415.10–649.05 | 390.18 |
| paragraphs / repeated | 4 | 168.39–177.78 | 231.89–241.56 | 188.55 |
| quote / repeated | 4 | 84.31–125.52 | 109.47–151.45 | 131.07 |
| table / repeated | 4 | 223.88–271.08 | 288.79–362.86 | 230.55 |
| details / cold-0 | 1 | 276.50–276.50 | 440.22–440.22 | 259.46 |
| details / cold-1 | 1 | 283.44–283.44 | 454.09–454.09 | 293.63 |
| details / cold-2 | 1 | 311.78–311.78 | 472.91–472.91 | 294.99 |
| list / cold-0 | 1 | 1460.46–1460.46 | 1870.29–1870.29 | 1437.57 |
| list / cold-1 | 1 | 1503.88–1503.88 | 1901.62–1901.62 | 1478.27 |
| list / cold-2 | 1 | 1442.03–1442.03 | 1887.66–1887.66 | 1416.20 |
| mixed / cold-0 | 1 | 642.18–642.18 | 1236.33–1236.33 | 619.85 |
| mixed / cold-1 | 1 | 618.54–618.54 | 1222.49–1222.49 | 599.77 |
| mixed / cold-2 | 1 | 769.23–769.23 | 1348.14–1348.14 | 771.32 |
| paragraphs / cold-0 | 1 | 460.75–460.75 | 704.52–704.52 | 462.82 |
| paragraphs / cold-1 | 1 | 416.88–416.88 | 668.21–668.21 | 398.00 |
| paragraphs / cold-2 | 1 | 428.72–428.72 | 665.28–665.28 | 416.03 |
| quote / cold-0 | 1 | 291.73–291.73 | 429.60–429.60 | 275.89 |
| quote / cold-1 | 1 | 293.12–293.12 | 443.75–443.75 | 304.76 |
| quote / cold-2 | 1 | 281.67–281.67 | 424.10–424.10 | 265.70 |
| table / cold-0 | 1 | 584.93–584.93 | 841.69–841.69 | 522.94 |
| table / cold-1 | 1 | 621.13–621.13 | 906.75–906.75 | 579.94 |
| table / cold-2 | 1 | 522.68–522.68 | 782.23–782.23 | 463.78 |

### candidate — original six workloads

Stock layout is the distinct original first-attachment clock. Candidate layout starts before required preparation, including native theme measurement, and ends after complete installation/layout/Input. Both end-to-end clocks exclude process/source I/O and empty shown-host setup, retained separately; stock viewer construction/attachment is inside its readiness clock. Candidate layout and readiness share the conservative pre-parsing start; the latter also includes the final idle/Input flush. Their near equality is disclosed, not hidden preparation.

| Fixture / group | n | Layout min–max ms | Preparation/presentation min–max ms | In-process Input maximum ms |
|---|---:|---:|---:|---:|
| details / distant-edit | 3 | 134.66–141.56 | 134.68–141.58 | 45.61 |
| list / distant-edit | 3 | 687.47–899.55 | 687.50–899.60 | 54.97 |
| mixed / distant-edit | 3 | 293.68–510.84 | 293.70–510.85 | 37.84 |
| paragraphs / distant-edit | 3 | 379.42–448.09 | 379.44–448.12 | 43.65 |
| quote / distant-edit | 3 | 126.90–129.89 | 126.93–129.92 | 42.74 |
| table / distant-edit | 3 | 244.48–312.15 | 244.50–312.17 | 44.92 |
| details / first-combined | 1 | 191.13–191.13 | 191.15–191.15 | 39.81 |
| list / first-combined | 1 | 832.35–832.35 | 832.38–832.38 | 40.13 |
| mixed / first-combined | 1 | 693.37–693.37 | 693.38–693.38 | 43.75 |
| paragraphs / first-combined | 1 | 560.54–560.54 | 560.56–560.56 | 40.10 |
| quote / first-combined | 1 | 190.40–190.40 | 190.44–190.44 | 36.95 |
| table / first-combined | 1 | 378.05–378.05 | 378.06–378.06 | 41.25 |
| details / repeated | 4 | 81.25–100.44 | 81.32–100.47 | 39.48 |
| list / repeated | 4 | 516.57–854.22 | 516.59–854.28 | 54.07 |
| mixed / repeated | 4 | 270.47–506.64 | 270.48–506.65 | 50.42 |
| paragraphs / repeated | 4 | 314.22–339.38 | 314.25–339.40 | 46.23 |
| quote / repeated | 4 | 108.68–119.17 | 108.70–119.20 | 41.17 |
| table / repeated | 4 | 248.87–268.19 | 248.89–268.20 | 43.34 |
| details / cold-0 | 1 | 326.95–326.95 | 326.98–326.98 | 40.65 |
| details / cold-1 | 1 | 357.29–357.29 | 357.33–357.33 | 46.03 |
| details / cold-2 | 1 | 321.45–321.45 | 321.49–321.49 | 44.06 |
| list / cold-0 | 1 | 811.61–811.61 | 811.65–811.65 | 52.72 |
| list / cold-1 | 1 | 898.26–898.26 | 898.30–898.30 | 42.24 |
| list / cold-2 | 1 | 840.18–840.18 | 840.24–840.24 | 53.90 |
| mixed / cold-0 | 1 | 863.63–863.63 | 863.66–863.66 | 49.19 |
| mixed / cold-1 | 1 | 839.57–839.57 | 839.60–839.60 | 38.94 |
| mixed / cold-2 | 1 | 865.50–865.50 | 865.53–865.53 | 40.06 |
| paragraphs / cold-0 | 1 | 683.55–683.55 | 683.60–683.60 | 39.70 |
| paragraphs / cold-1 | 1 | 652.63–652.63 | 652.66–652.66 | 45.35 |
| paragraphs / cold-2 | 1 | 644.87–644.87 | 644.92–644.92 | 46.77 |
| quote / cold-0 | 1 | 309.07–309.07 | 309.10–309.10 | 39.68 |
| quote / cold-1 | 1 | 418.57–418.57 | 418.62–418.62 | 50.57 |
| quote / cold-2 | 1 | 305.91–305.91 | 305.94–305.94 | 39.44 |
| table / cold-0 | 1 | 518.13–518.13 | 518.17–518.17 | 43.66 |
| table / cold-1 | 1 | 519.27–519.27 | 519.32–519.32 | 41.43 |
| table / cold-2 | 1 | 515.83–515.83 | 515.87–515.87 | 45.20 |

### Additional independent UIA cases — separately labelled

| Case / group | Receipt | Layout ms | Preparation/presentation ms | Timing / interaction Input ms |
|---|---|---:|---:|---:|
| collapsed-details / targeted-uia | PASS | 172.03 | 172.06 | 41.23 / 67.39 |
| details / independent-uia | PASS | 331.43 | 331.46 | 43.50 / 53.68 |
| list / independent-uia | PASS | 818.05 | 818.10 | 41.10 / 142.45 |
| mixed / independent-uia | PASS | 866.36 | 866.39 | 45.21 / 80.18 |
| nested-lists / targeted-uia | PASS | 156.66 | 156.70 | 42.01 / 61.28 |
| nested-numbering / extended-list-uia | PASS | 157.22 | 157.26 | 40.23 / 63.33 |
| paragraphs / independent-uia | FAIL / boundary only | 641.68 | 641.73 | 39.14 / 41.28 |
| quote / independent-uia | PASS | 313.05 | 313.09 | 41.49 / 66.48 |
| rich / targeted-uia | PASS | 1218.29 | 1218.32 | 43.61 / 57.73 |
| table / independent-uia | PASS | 444.64 | 444.67 | 45.52 / 188.22 |
| table-blocks / extended-table-uia | PASS | 161.96 | 162.00 | 38.95 / 69.07 |
| table-spans / extended-table-uia | PASS | 154.30 | 154.33 | 45.60 / 81.31 |
| table-wrap / targeted-uia | PASS | 173.11 | 173.15 | 40.60 / 63.78 |
| unicode-bidi / targeted-uia | PASS | 2317.04 | 2317.07 | 45.53 / 64.11 |

### Required special observations

| Scenario | Functional result | Input maximum ms |
|---|---|---:|
| lifecycle | PASS | 94.14 |
| appearance | PASS | 301.26 |
| studio-checks | PASS | 105.15 |
| media-checks | FAIL | 14490.12 |
| range-checks | PASS | 84.90 |
| link-checks | PASS | 46.00 |
| image-clipboard-checks | PASS | 52.74 |

Cancellation: PASS; UI-only observation max 0.69 ms; queued worker observation max 0.17 ms; dispatcher delivery max 29.08 ms. Injected non-cancellation failure remains separately labelled. Winning readiness PASS, maximum 953.13 ms; actual Input readiness is recorded separately from installation. All worker dispatchers released; no truncated required logs. Minimal automatic-appearance control Input 109.42 ms is comparative only and never exempts candidate Input.

Gate rows: complete layout FAIL (2 additional-case misses); preparation/presentation FAIL (1 additional-case misses); Input FAIL; functional INCOMPLETE_OR_FAIL; appearance/lifecycle correctness PASS; visual PASS. Missing implementation capabilities: none. Failed execution remains failed proof.

### Allocation and bounded retention observations

- stock AllocatedBytes: 20,398,560–117,121,760 bytes across original samples; per-temperature values and natural GC arrays remain raw.
- stock ManagedBytes: 33,028,432–194,829,168 bytes across original samples; per-temperature values and natural GC arrays remain raw.
- stock PrivateMemorySize64: 96,735,232–289,611,776 bytes across original samples; per-temperature values and natural GC arrays remain raw.
- candidate AllocatedBytes: 20,749,008–78,466,456 bytes across original samples; per-temperature values and natural GC arrays remain raw.
- candidate ManagedBytes: 20,141,912–287,001,920 bytes across original samples; per-temperature values and natural GC arrays remain raw.
- candidate PrivateMemorySize64: 67,342,336–328,724,480 bytes across original samples; per-temperature values and natural GC arrays remain raw.

Memory/weak-reference records are bounded natural-collection observations, neither zero-retention nor leak certification.

## Host 2

Machine `runnervm0xl3a`; 4 available logical processors; `AMD64 Family 25 Model 17 Stepping 1, AuthenticAMD`; `Microsoft Windows NT 10.0.26100.0`; image `win25-vs2026` / `20261007.264.1`; `.NET 10.0.13`; ReadyToRun; 640 x 300, DPI 96, zoom 100%, Light. Initial adapter order: candidate then stock.

Source `47dff1e77981fc0db03c6be9f58168201c5411b9`, clean; harness SHA-256 `E9903604EE10ED08243C7FEB62BAC100DCDD271868CB3F56EF462947617F5EDB`. Official runtime signatures pass and every 66 paired identity checks passes. Raw protocol, stages, thread identities, source/fixture/runtime hashes, natural GC and allocations are in the adjacent receipt archive.

### stock — original six workloads

Stock layout is the distinct original first-attachment clock. Candidate layout starts before required preparation, including native theme measurement, and ends after complete installation/layout/Input. Both end-to-end clocks exclude process/source I/O and empty shown-host setup, retained separately; stock viewer construction/attachment is inside its readiness clock. Candidate layout and readiness share the conservative pre-parsing start; the latter also includes the final idle/Input flush. Their near equality is disclosed, not hidden preparation.

| Fixture / group | n | Layout min–max ms | Preparation/presentation min–max ms | In-process Input maximum ms |
|---|---:|---:|---:|---:|
| details / distant-edit | 3 | 67.17–106.09 | 125.00–143.22 | 115.90 |
| list / distant-edit | 3 | 567.56–1234.92 | 792.37–1533.30 | 1205.58 |
| mixed / distant-edit | 3 | 288.91–352.21 | 435.04–646.50 | 367.88 |
| paragraphs / distant-edit | 3 | 148.55–176.15 | 214.55–232.52 | 179.11 |
| quote / distant-edit | 3 | 62.31–91.51 | 108.57–114.81 | 100.86 |
| table / distant-edit | 3 | 228.01–241.42 | 306.25–334.43 | 217.40 |
| details / first-combined | 1 | 77.34–77.34 | 139.10–139.10 | 75.75 |
| list / first-combined | 1 | 1171.46–1171.46 | 1452.66–1452.66 | 1146.92 |
| mixed / first-combined | 1 | 307.82–307.82 | 634.90–634.90 | 294.14 |
| paragraphs / first-combined | 1 | 130.07–130.07 | 224.74–224.74 | 133.58 |
| quote / first-combined | 1 | 70.37–70.37 | 109.91–109.91 | 69.39 |
| table / first-combined | 1 | 318.77–318.77 | 483.03–483.03 | 283.86 |
| details / repeated | 4 | 68.72–91.10 | 96.32–143.13 | 96.03 |
| list / repeated | 4 | 641.10–1306.27 | 853.28–1628.75 | 1276.65 |
| mixed / repeated | 4 | 320.33–351.13 | 401.08–664.63 | 361.73 |
| paragraphs / repeated | 4 | 138.42–165.76 | 192.90–221.89 | 185.73 |
| quote / repeated | 4 | 64.64–78.48 | 93.74–100.28 | 97.13 |
| table / repeated | 4 | 224.16–253.00 | 285.53–339.27 | 210.41 |
| details / cold-0 | 1 | 241.12–241.12 | 391.88–391.88 | 225.67 |
| details / cold-1 | 1 | 241.02–241.02 | 388.63–388.63 | 224.85 |
| details / cold-2 | 1 | 254.61–254.61 | 412.80–412.80 | 236.65 |
| list / cold-0 | 1 | 1156.00–1156.00 | 1484.63–1484.63 | 1130.39 |
| list / cold-1 | 1 | 1216.12–1216.12 | 1518.28–1518.28 | 1190.58 |
| list / cold-2 | 1 | 1142.27–1142.27 | 1464.84–1464.84 | 1114.20 |
| mixed / cold-0 | 1 | 567.43–567.43 | 1106.12–1106.12 | 542.80 |
| mixed / cold-1 | 1 | 547.35–547.35 | 1088.49–1088.49 | 526.63 |
| mixed / cold-2 | 1 | 580.62–580.62 | 1147.40–1147.40 | 579.07 |
| paragraphs / cold-0 | 1 | 386.45–386.45 | 597.84–597.84 | 366.37 |
| paragraphs / cold-1 | 1 | 379.90–379.90 | 583.32–583.32 | 360.61 |
| paragraphs / cold-2 | 1 | 371.43–371.43 | 585.88–585.88 | 349.97 |
| quote / cold-0 | 1 | 242.19–242.19 | 366.55–366.55 | 225.14 |
| quote / cold-1 | 1 | 250.88–250.88 | 377.99–377.99 | 231.94 |
| quote / cold-2 | 1 | 245.63–245.63 | 375.84–375.84 | 227.67 |
| table / cold-0 | 1 | 470.78–470.78 | 686.36–686.36 | 401.26 |
| table / cold-1 | 1 | 481.84–481.84 | 707.59–707.59 | 445.22 |
| table / cold-2 | 1 | 492.48–492.48 | 722.92–722.92 | 422.62 |

### candidate — original six workloads

Stock layout is the distinct original first-attachment clock. Candidate layout starts before required preparation, including native theme measurement, and ends after complete installation/layout/Input. Both end-to-end clocks exclude process/source I/O and empty shown-host setup, retained separately; stock viewer construction/attachment is inside its readiness clock. Candidate layout and readiness share the conservative pre-parsing start; the latter also includes the final idle/Input flush. Their near equality is disclosed, not hidden preparation.

| Fixture / group | n | Layout min–max ms | Preparation/presentation min–max ms | In-process Input maximum ms |
|---|---:|---:|---:|---:|
| details / distant-edit | 3 | 123.04–159.02 | 123.07–159.06 | 43.77 |
| list / distant-edit | 3 | 700.09–818.39 | 700.11–818.43 | 46.37 |
| mixed / distant-edit | 3 | 251.93–519.20 | 251.94–519.22 | 40.04 |
| paragraphs / distant-edit | 3 | 337.80–437.77 | 337.82–437.79 | 48.12 |
| quote / distant-edit | 3 | 133.14–142.23 | 133.16–142.25 | 38.06 |
| table / distant-edit | 3 | 283.30–293.73 | 283.32–293.75 | 47.88 |
| details / first-combined | 1 | 170.32–170.32 | 170.34–170.34 | 33.15 |
| list / first-combined | 1 | 902.19–902.19 | 902.24–902.24 | 64.84 |
| mixed / first-combined | 1 | 774.94–774.94 | 774.96–774.96 | 32.36 |
| paragraphs / first-combined | 1 | 508.04–508.04 | 508.06–508.06 | 39.33 |
| quote / first-combined | 1 | 156.72–156.72 | 156.74–156.74 | 32.66 |
| table / first-combined | 1 | 374.16–374.16 | 374.18–374.18 | 39.33 |
| details / repeated | 4 | 91.59–122.66 | 91.61–122.69 | 44.82 |
| list / repeated | 4 | 491.52–771.65 | 491.55–771.69 | 52.93 |
| mixed / repeated | 4 | 262.53–530.29 | 262.55–530.31 | 54.45 |
| paragraphs / repeated | 4 | 334.65–369.41 | 334.67–369.43 | 39.14 |
| quote / repeated | 4 | 84.21–87.95 | 84.23–87.98 | 32.45 |
| table / repeated | 4 | 212.73–332.11 | 212.75–332.14 | 45.97 |
| details / cold-0 | 1 | 291.86–291.86 | 291.89–291.89 | 41.20 |
| details / cold-1 | 1 | 292.96–292.96 | 292.99–292.99 | 42.23 |
| details / cold-2 | 1 | 304.74–304.74 | 304.77–304.77 | 47.23 |
| list / cold-0 | 1 | 673.38–673.38 | 673.42–673.42 | 45.21 |
| list / cold-1 | 1 | 694.17–694.17 | 694.21–694.21 | 44.17 |
| list / cold-2 | 1 | 710.10–710.10 | 710.14–710.14 | 44.55 |
| mixed / cold-0 | 1 | 793.65–793.65 | 793.68–793.68 | 57.16 |
| mixed / cold-1 | 1 | 826.28–826.28 | 826.31–826.31 | 43.79 |
| mixed / cold-2 | 1 | 828.73–828.73 | 828.76–828.76 | 44.03 |
| paragraphs / cold-0 | 1 | 659.61–659.61 | 659.64–659.64 | 48.76 |
| paragraphs / cold-1 | 1 | 608.17–608.17 | 608.20–608.20 | 45.49 |
| paragraphs / cold-2 | 1 | 627.94–627.94 | 627.98–627.98 | 49.94 |
| quote / cold-0 | 1 | 270.47–270.47 | 270.50–270.50 | 41.46 |
| quote / cold-1 | 1 | 277.85–277.85 | 277.88–277.88 | 42.29 |
| quote / cold-2 | 1 | 284.91–284.91 | 284.95–284.95 | 44.25 |
| table / cold-0 | 1 | 394.34–394.34 | 394.36–394.36 | 41.36 |
| table / cold-1 | 1 | 439.48–439.48 | 439.51–439.51 | 44.37 |
| table / cold-2 | 1 | 442.47–442.47 | 442.51–442.51 | 47.88 |

### Additional independent UIA cases — separately labelled

| Case / group | Receipt | Layout ms | Preparation/presentation ms | Timing / interaction Input ms |
|---|---|---:|---:|---:|
| collapsed-details / targeted-uia | PASS | 172.25 | 172.28 | 41.22 / 56.05 |
| details / independent-uia | PASS | 286.80 | 286.83 | 44.86 / 51.51 |
| list / independent-uia | PASS | 808.12 | 808.18 | 50.94 / 87.52 |
| mixed / independent-uia | PASS | 832.95 | 832.98 | 40.86 / 95.95 |
| nested-lists / targeted-uia | PASS | 153.10 | 153.13 | 41.13 / 63.61 |
| nested-numbering / extended-list-uia | PASS | 154.92 | 154.95 | 42.68 / 63.71 |
| paragraphs / independent-uia | FAIL / boundary only | 738.78 | 738.82 | 42.07 / 48.60 |
| quote / independent-uia | PASS | 302.64 | 302.68 | 46.35 / 67.36 |
| rich / targeted-uia | PASS | 231.66 | 231.69 | 48.12 / 55.03 |
| table / independent-uia | PASS | 503.31 | 503.35 | 44.34 / 180.51 |
| table-blocks / extended-table-uia | PASS | 179.72 | 179.75 | 45.34 / 79.19 |
| table-spans / extended-table-uia | PASS | 156.34 | 156.37 | 40.94 / 58.53 |
| table-wrap / targeted-uia | PASS | 171.58 | 171.62 | 43.10 / 66.63 |
| unicode-bidi / targeted-uia | PASS | 336.01 | 336.04 | 58.03 / 70.61 |

### Required special observations

| Scenario | Functional result | Input maximum ms |
|---|---|---:|
| lifecycle | PASS | 110.88 |
| appearance | PASS | 288.54 |
| studio-checks | PASS | 56.84 |
| media-checks | PASS | 639.12 |
| range-checks | PASS | 94.81 |
| link-checks | PASS | 53.19 |
| image-clipboard-checks | PASS | 41.64 |

Cancellation: PASS; UI-only observation max 0.80 ms; queued worker observation max 0.25 ms; dispatcher delivery max 15.65 ms. Injected non-cancellation failure remains separately labelled. Winning readiness PASS, maximum 686.50 ms; actual Input readiness is recorded separately from installation. All worker dispatchers released; no truncated required logs. Minimal automatic-appearance control Input 136.42 ms is comparative only and never exempts candidate Input.

Gate rows: complete layout PASS (0 additional-case misses); preparation/presentation PASS (0 additional-case misses); Input FAIL; functional INCOMPLETE_OR_FAIL; appearance/lifecycle correctness PASS; visual PASS. Missing implementation capabilities: none. Failed execution remains failed proof.

### Allocation and bounded retention observations

- stock AllocatedBytes: 20,398,288–117,125,888 bytes across original samples; per-temperature values and natural GC arrays remain raw.
- stock ManagedBytes: 33,030,728–194,699,416 bytes across original samples; per-temperature values and natural GC arrays remain raw.
- stock PrivateMemorySize64: 95,805,440–298,590,208 bytes across original samples; per-temperature values and natural GC arrays remain raw.
- candidate AllocatedBytes: 20,769,688–78,466,120 bytes across original samples; per-temperature values and natural GC arrays remain raw.
- candidate ManagedBytes: 38,776,688–287,018,240 bytes across original samples; per-temperature values and natural GC arrays remain raw.
- candidate PrivateMemorySize64: 91,594,752–329,342,976 bytes across original samples; per-temperature values and natural GC arrays remain raw.

Memory/weak-reference records are bounded natural-collection observations, neither zero-retention nor leak certification.
