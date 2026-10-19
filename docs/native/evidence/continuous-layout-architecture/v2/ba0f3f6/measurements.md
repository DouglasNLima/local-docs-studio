# Controlled v2 measurements at ba0f3f6

[Complete raw run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39230058924). Every original observation is retained in the adjacent CSV and receipt archives. Layout clocks differ by adapter; end-to-end readiness is the shared comparative boundary. Min/max are the observed extrema, with no percentile inference.

## Replication 1

CPU available: 4; AMD64 Family 25 Model 17 Stepping 1, AuthenticAMD. Image win25-vs2026 20261007.264.1; .NET 10.0.13; 96 DPI, 640×300 DIP, 100% zoom, Light. Source ba0f3f6b6243275b158f0075e0040919a9493362; source dirty False. Order ['stock', 'candidate'].

Controlled candidate samples 66/66. Controlled layout misses 0; presentation misses 0. Additional UIA samples and their failures remain in the aggregate receipt.

| Fixture | Group | N | Candidate complete layout min–max ms | Candidate readiness min–max ms | Stock readiness min–max ms | Candidate timing Input max ms |
|---|---|---:|---:|---:|---:|---:|
| list | first-combined | 1 | 568.97–568.97 | 568.99–568.99 | 1640.50–1640.50 | 49.34 |
| list | cold-0 | 1 | 581.78–581.78 | 581.80–581.80 | 1239.68–1239.68 | 51.83 |
| list | cold-1 | 1 | 583.28–583.28 | 583.30–583.30 | 1208.08–1208.08 | 46.73 |
| list | cold-2 | 1 | 567.47–567.47 | 567.49–567.49 | 1203.59–1203.59 | 37.78 |
| list | repeated | 4 | 343.26–548.08 | 343.28–548.10 | 618.68–1314.36 | 43.18 |
| list | distant-edit | 3 | 562.19–681.44 | 562.22–681.46 | 674.11–1190.59 | 44.33 |
| table | first-combined | 1 | 293.92–293.92 | 293.94–293.94 | 460.34–460.34 | 45.32 |
| table | cold-0 | 1 | 371.30–371.30 | 371.31–371.31 | 633.69–633.69 | 57.22 |
| table | cold-1 | 1 | 361.17–361.17 | 361.18–361.18 | 617.73–617.73 | 51.21 |
| table | cold-2 | 1 | 357.32–357.32 | 357.33–357.33 | 605.11–605.11 | 32.44 |
| table | repeated | 4 | 179.44–192.46 | 179.46–192.47 | 187.60–268.46 | 40.93 |
| table | distant-edit | 3 | 249.00–258.18 | 249.02–258.20 | 200.91–214.88 | 42.71 |
| paragraphs | first-combined | 1 | 433.15–433.15 | 433.17–433.17 | 202.52–202.52 | 44.23 |
| paragraphs | cold-0 | 1 | 529.85–529.85 | 529.87–529.87 | 499.87–499.87 | 45.15 |
| paragraphs | cold-1 | 1 | 555.87–555.87 | 555.89–555.89 | 514.68–514.68 | 35.29 |
| paragraphs | cold-2 | 1 | 552.02–552.02 | 552.04–552.04 | 525.96–525.96 | 31.81 |
| paragraphs | repeated | 4 | 254.43–273.04 | 254.44–273.06 | 140.08–164.59 | 40.46 |
| paragraphs | distant-edit | 3 | 264.24–351.84 | 264.27–351.86 | 146.09–179.76 | 42.88 |
| quote | first-combined | 1 | 125.51–125.51 | 125.53–125.53 | 141.02–141.02 | 31.77 |
| quote | cold-0 | 1 | 248.71–248.71 | 248.72–248.72 | 350.10–350.10 | 32.02 |
| quote | cold-1 | 1 | 241.95–241.95 | 241.97–241.97 | 349.03–349.03 | 31.52 |
| quote | cold-2 | 1 | 249.83–249.83 | 249.85–249.85 | 348.80–348.80 | 38.17 |
| quote | repeated | 4 | 60.96–71.74 | 60.98–71.76 | 63.03–126.10 | 35.17 |
| quote | distant-edit | 3 | 103.94–115.56 | 103.96–115.58 | 70.28–107.06 | 34.16 |
| details | first-combined | 1 | 181.53–181.53 | 181.55–181.55 | 107.85–107.85 | 33.35 |
| details | cold-0 | 1 | 302.78–302.78 | 302.80–302.80 | 371.21–371.21 | 31.95 |
| details | cold-1 | 1 | 299.31–299.31 | 299.33–299.33 | 374.48–374.48 | 34.60 |
| details | cold-2 | 1 | 289.40–289.40 | 289.42–289.42 | 362.61–362.61 | 33.53 |
| details | repeated | 4 | 76.04–116.38 | 76.06–116.40 | 67.79–104.32 | 32.25 |
| details | distant-edit | 3 | 118.04–156.91 | 118.07–156.93 | 79.79–104.70 | 40.58 |
| mixed | first-combined | 1 | 834.88–834.88 | 834.93–834.93 | 830.71–830.71 | 37.60 |
| mixed | cold-0 | 1 | 995.13–995.13 | 995.15–995.15 | 1204.66–1204.66 | 47.67 |
| mixed | cold-1 | 1 | 973.69–973.69 | 973.71–973.71 | 1159.05–1159.05 | 39.51 |
| mixed | cold-2 | 1 | 974.57–974.57 | 974.59–974.59 | 1163.11–1163.11 | 45.27 |
| mixed | repeated | 4 | 239.01–717.22 | 239.03–717.24 | 321.85–769.69 | 44.11 |
| mixed | distant-edit | 3 | 227.14–684.19 | 227.16–684.21 | 275.34–705.39 | 45.14 |

| Separate shown-window scenario | Functional/status receipt | Actual in-process Input max ms |
|---|---|---:|
| parity-all | PASS | Not separately timed |
| lifecycle | PASS | 79.7326999999998 |
| appearance | FAIL | 446.25909999999976 |
| studio-checks | PASS | 87.35590000000002 |
| media-checks | FAIL | 562.3026 |

Allocation, natural GC, post-probe working/private/managed memory, request/cancellation/readiness, GUI/UIA and every installation observation remain in the original receipts. No forced collection is used. Functional PASS labels refer only to executed assertions, not full required parity.

## Replication 2

CPU available: 4; AMD64 Family 25 Model 1 Stepping 1, AuthenticAMD. Image win25-vs2026 20261007.264.1; .NET 10.0.13; 96 DPI, 640×300 DIP, 100% zoom, Light. Source ba0f3f6b6243275b158f0075e0040919a9493362; source dirty False. Order ['candidate', 'stock'].

Controlled candidate samples 66/66. Controlled layout misses 5; presentation misses 0. Additional UIA samples and their failures remain in the aggregate receipt.

| Fixture | Group | N | Candidate complete layout min–max ms | Candidate readiness min–max ms | Stock readiness min–max ms | Candidate timing Input max ms |
|---|---|---:|---:|---:|---:|---:|
| list | first-combined | 1 | 1176.23–1176.23 | 1176.32–1176.32 | 2158.36–2158.36 | 40.70 |
| list | cold-0 | 1 | 911.62–911.62 | 911.65–911.65 | 1939.20–1939.20 | 41.47 |
| list | cold-1 | 1 | 908.19–908.19 | 908.22–908.22 | 1920.41–1920.41 | 45.29 |
| list | cold-2 | 1 | 876.41–876.41 | 876.44–876.44 | 1972.53–1972.53 | 41.51 |
| list | repeated | 4 | 547.97–828.74 | 548.00–828.78 | 879.96–1867.75 | 49.53 |
| list | distant-edit | 3 | 701.23–970.98 | 701.24–971.02 | 861.55–1906.65 | 43.39 |
| table | first-combined | 1 | 503.68–503.68 | 503.70–503.70 | 578.76–578.76 | 48.55 |
| table | cold-0 | 1 | 511.95–511.95 | 511.98–511.98 | 859.22–859.22 | 36.90 |
| table | cold-1 | 1 | 516.22–516.22 | 516.24–516.24 | 870.47–870.47 | 44.22 |
| table | cold-2 | 1 | 554.73–554.73 | 554.75–554.75 | 851.30–851.30 | 48.89 |
| table | repeated | 4 | 238.02–304.79 | 238.04–304.81 | 278.88–312.00 | 45.42 |
| table | distant-edit | 3 | 295.48–342.37 | 295.50–342.39 | 291.39–328.18 | 35.90 |
| paragraphs | first-combined | 1 | 651.86–651.86 | 651.89–651.89 | 255.11–255.11 | 32.05 |
| paragraphs | cold-0 | 1 | 740.19–740.19 | 740.23–740.23 | 703.38–703.38 | 41.75 |
| paragraphs | cold-1 | 1 | 713.97–713.97 | 714.00–714.00 | 695.91–695.91 | 46.76 |
| paragraphs | cold-2 | 1 | 705.75–705.75 | 705.78–705.78 | 705.25–705.25 | 40.84 |
| paragraphs | repeated | 4 | 312.94–366.38 | 312.96–366.40 | 219.59–240.15 | 45.39 |
| paragraphs | distant-edit | 3 | 331.65–382.45 | 331.68–382.48 | 215.79–236.27 | 45.05 |
| quote | first-combined | 1 | 215.28–215.28 | 215.30–215.30 | 160.56–160.56 | 32.05 |
| quote | cold-0 | 1 | 344.54–344.54 | 344.56–344.56 | 471.93–471.93 | 31.81 |
| quote | cold-1 | 1 | 343.54–343.54 | 343.56–343.56 | 494.16–494.16 | 31.88 |
| quote | cold-2 | 1 | 346.56–346.56 | 346.59–346.59 | 478.47–478.47 | 32.08 |
| quote | repeated | 4 | 90.25–108.97 | 90.28–109.00 | 104.20–121.31 | 35.97 |
| quote | distant-edit | 3 | 140.42–156.20 | 140.44–156.22 | 107.25–154.93 | 43.89 |
| details | first-combined | 1 | 281.90–281.90 | 281.93–281.93 | 152.91–152.91 | 43.64 |
| details | cold-0 | 1 | 395.31–395.31 | 395.34–395.34 | 522.09–522.09 | 42.34 |
| details | cold-1 | 1 | 404.25–404.25 | 404.28–404.28 | 519.29–519.29 | 45.48 |
| details | cold-2 | 1 | 405.99–405.99 | 406.01–406.01 | 502.82–502.82 | 31.96 |
| details | repeated | 4 | 103.86–151.45 | 103.89–151.47 | 128.50–134.89 | 53.69 |
| details | distant-edit | 3 | 159.27–207.06 | 159.30–207.08 | 120.87–154.32 | 47.34 |
| mixed | first-combined | 1 | 1225.15–1225.15 | 1225.17–1225.17 | 980.94–980.94 | 43.40 |
| mixed | cold-0 | 1 | 1293.60–1293.60 | 1293.62–1293.62 | 1692.18–1692.18 | 41.35 |
| mixed | cold-1 | 1 | 1323.04–1323.04 | 1323.06–1323.06 | 1709.43–1709.43 | 43.68 |
| mixed | cold-2 | 1 | 1272.98–1272.98 | 1273.00–1273.00 | 1685.56–1685.56 | 43.32 |
| mixed | repeated | 4 | 322.28–874.70 | 322.30–874.72 | 398.17–940.48 | 51.70 |
| mixed | distant-edit | 3 | 292.93–847.00 | 292.95–847.01 | 442.65–931.50 | 41.56 |

| Separate shown-window scenario | Functional/status receipt | Actual in-process Input max ms |
|---|---|---:|
| media-checks | FAIL | 672.6623 |
| appearance | FAIL | 633.8218999999999 |
| studio-checks | PASS | 107.19099999999992 |
| parity-all | PASS | Not separately timed |
| lifecycle | PASS | 79.45910000000003 |

Allocation, natural GC, post-probe working/private/managed memory, request/cancellation/readiness, GUI/UIA and every installation observation remain in the original receipts. No forced collection is used. Functional PASS labels refer only to executed assertions, not full required parity.
