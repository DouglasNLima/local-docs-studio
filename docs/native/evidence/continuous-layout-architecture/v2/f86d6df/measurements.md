# Controlled v2 measurements at f86d6df

[Complete raw run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39227279725). Every original observation is retained in the adjacent CSV and receipt archives. Layout clocks differ by adapter; end-to-end readiness is the shared comparative boundary. Min/max are the observed extrema, with no percentile inference.

## Replication 1

CPU available: 4; AMD64 Family 25 Model 1 Stepping 1, AuthenticAMD. Image win25-vs2026 20261007.264.1; .NET 10.0.13; 96 DPI, 640×300 DIP, 100% zoom, Light. Source f86d6df19b71449ac7dabe5700741448e9510c3f; source dirty False. Order ['stock', 'candidate'].

Controlled candidate samples 66/66. Controlled layout misses 5; presentation misses 0. Additional UIA samples and their failures remain in the aggregate receipt.

| Fixture | Group | N | Candidate complete layout min–max ms | Candidate readiness min–max ms | Stock readiness min–max ms | Candidate timing Input max ms |
|---|---|---:|---:|---:|---:|---:|
| list | first-combined | 1 | 822.34–822.34 | 822.37–822.37 | 2371.74–2371.74 | 35.58 |
| list | cold-0 | 1 | 907.16–907.16 | 907.19–907.19 | 1870.30–1870.30 | 43.65 |
| list | cold-1 | 1 | 878.42–878.42 | 878.46–878.46 | 1991.69–1991.69 | 47.94 |
| list | cold-2 | 1 | 1207.23–1207.23 | 1207.26–1207.26 | 2197.52–2197.52 | 58.40 |
| list | repeated | 4 | 550.27–851.88 | 550.28–851.92 | 934.65–1862.04 | 49.75 |
| list | distant-edit | 3 | 714.12–979.95 | 714.14–979.99 | 851.41–1931.03 | 44.74 |
| table | first-combined | 1 | 430.50–430.50 | 430.51–430.51 | 646.17–646.17 | 36.63 |
| table | cold-0 | 1 | 544.34–544.34 | 544.36–544.36 | 875.56–875.56 | 45.16 |
| table | cold-1 | 1 | 512.06–512.06 | 512.09–512.09 | 866.62–866.62 | 41.73 |
| table | cold-2 | 1 | 515.13–515.13 | 515.15–515.15 | 861.94–861.94 | 43.46 |
| table | repeated | 4 | 219.83–297.13 | 219.85–297.15 | 282.82–313.18 | 43.31 |
| table | distant-edit | 3 | 290.63–315.44 | 290.65–315.45 | 276.76–322.08 | 45.60 |
| paragraphs | first-combined | 1 | 591.30–591.30 | 591.33–591.33 | 276.73–276.73 | 35.04 |
| paragraphs | cold-0 | 1 | 712.65–712.65 | 712.68–712.68 | 692.72–692.72 | 46.00 |
| paragraphs | cold-1 | 1 | 713.31–713.31 | 713.34–713.34 | 712.53–712.53 | 43.31 |
| paragraphs | cold-2 | 1 | 723.98–723.98 | 724.02–724.02 | 721.18–721.18 | 41.00 |
| paragraphs | repeated | 4 | 294.72–360.85 | 294.74–360.88 | 215.45–240.36 | 43.32 |
| paragraphs | distant-edit | 3 | 319.39–383.98 | 319.41–384.00 | 208.94–239.74 | 35.76 |
| quote | first-combined | 1 | 192.32–192.32 | 192.34–192.34 | 124.15–124.15 | 31.83 |
| quote | cold-0 | 1 | 337.24–337.24 | 337.26–337.26 | 482.32–482.32 | 31.98 |
| quote | cold-1 | 1 | 348.19–348.19 | 348.22–348.22 | 493.40–493.40 | 33.35 |
| quote | cold-2 | 1 | 340.96–340.96 | 340.99–340.99 | 479.04–479.04 | 31.74 |
| quote | repeated | 4 | 84.72–114.00 | 84.75–114.03 | 97.82–114.15 | 37.76 |
| quote | distant-edit | 3 | 129.24–146.93 | 129.27–146.96 | 105.60–139.77 | 36.68 |
| details | first-combined | 1 | 216.94–216.94 | 216.97–216.97 | 163.96–163.96 | 32.70 |
| details | cold-0 | 1 | 361.62–361.62 | 361.65–361.65 | 510.16–510.16 | 31.85 |
| details | cold-1 | 1 | 365.14–365.14 | 365.17–365.17 | 534.53–534.53 | 33.12 |
| details | cold-2 | 1 | 376.24–376.24 | 376.26–376.26 | 522.79–522.79 | 32.11 |
| details | repeated | 4 | 86.72–118.32 | 86.74–118.34 | 105.41–142.19 | 39.46 |
| details | distant-edit | 3 | 134.37–151.37 | 134.39–151.39 | 107.53–145.92 | 40.62 |
| mixed | first-combined | 1 | 1010.51–1010.51 | 1010.52–1010.52 | 1117.40–1117.40 | 59.45 |
| mixed | cold-0 | 1 | 1255.39–1255.39 | 1255.41–1255.41 | 1696.77–1696.77 | 43.96 |
| mixed | cold-1 | 1 | 1297.21–1297.21 | 1297.24–1297.24 | 1859.83–1859.83 | 45.70 |
| mixed | cold-2 | 1 | 1245.19–1245.19 | 1245.21–1245.21 | 1586.35–1586.35 | 32.16 |
| mixed | repeated | 4 | 254.50–809.97 | 254.52–809.99 | 387.07–915.43 | 45.44 |
| mixed | distant-edit | 3 | 266.05–820.33 | 266.06–820.35 | 417.89–905.00 | 41.06 |

| Separate shown-window scenario | Functional/status receipt | Actual in-process Input max ms |
|---|---|---:|
| lifecycle | PASS | 75.2484999999997 |
| appearance | FAIL | 285.14130000000023 |
| studio-checks | PASS | 78.14739999999995 |
| parity-all | PASS | Not separately timed |

Allocation, natural GC, post-probe working/private/managed memory, request/cancellation/readiness, GUI/UIA and every installation observation remain in the original receipts. No forced collection is used. Functional PASS labels refer only to executed assertions, not full required parity.

## Replication 2

CPU available: 4; AMD64 Family 26 Model 2 Stepping 1, AuthenticAMD. Image win25-vs2026 20261007.264.1; .NET 10.0.13; 96 DPI, 640×300 DIP, 100% zoom, Light. Source f86d6df19b71449ac7dabe5700741448e9510c3f; source dirty False. Order ['candidate', 'stock'].

Controlled candidate samples 66/66. Controlled layout misses 0; presentation misses 0. Additional UIA samples and their failures remain in the aggregate receipt.

| Fixture | Group | N | Candidate complete layout min–max ms | Candidate readiness min–max ms | Stock readiness min–max ms | Candidate timing Input max ms |
|---|---|---:|---:|---:|---:|---:|
| list | first-combined | 1 | 502.53–502.53 | 502.55–502.55 | 743.17–743.17 | 44.43 |
| list | cold-0 | 1 | 313.62–313.62 | 313.64–313.64 | 653.36–653.36 | 44.93 |
| list | cold-1 | 1 | 320.17–320.17 | 320.19–320.19 | 651.04–651.04 | 36.27 |
| list | cold-2 | 1 | 306.93–306.93 | 306.95–306.95 | 639.75–639.75 | 42.00 |
| list | repeated | 4 | 171.02–323.29 | 171.03–323.31 | 446.16–655.61 | 43.83 |
| list | distant-edit | 3 | 262.91–333.24 | 262.92–333.26 | 407.46–675.93 | 39.98 |
| table | first-combined | 1 | 169.22–169.22 | 169.23–169.23 | 386.38–386.38 | 41.88 |
| table | cold-0 | 1 | 209.78–209.78 | 209.79–209.79 | 392.16–392.16 | 41.99 |
| table | cold-1 | 1 | 198.59–198.59 | 198.60–198.60 | 406.79–406.79 | 33.40 |
| table | cold-2 | 1 | 206.60–206.60 | 206.61–206.61 | 384.51–384.51 | 35.62 |
| table | repeated | 4 | 156.72–175.53 | 156.74–175.55 | 145.48–253.27 | 43.42 |
| table | distant-edit | 3 | 146.58–157.96 | 146.59–157.97 | 132.85–158.35 | 35.69 |
| paragraphs | first-combined | 1 | 273.65–273.65 | 273.67–273.67 | 163.96–163.96 | 31.37 |
| paragraphs | cold-0 | 1 | 331.50–331.50 | 331.51–331.51 | 345.28–345.28 | 47.69 |
| paragraphs | cold-1 | 1 | 327.83–327.83 | 327.84–327.84 | 329.14–329.14 | 32.18 |
| paragraphs | cold-2 | 1 | 332.08–332.08 | 332.09–332.09 | 319.67–319.67 | 35.38 |
| paragraphs | repeated | 4 | 163.28–287.38 | 163.29–287.40 | 91.39–102.00 | 44.47 |
| paragraphs | distant-edit | 3 | 259.92–314.84 | 259.93–314.85 | 97.34–110.44 | 32.01 |
| quote | first-combined | 1 | 51.00–51.00 | 51.01–51.01 | 120.06–120.06 | 26.41 |
| quote | cold-0 | 1 | 152.02–152.02 | 152.04–152.04 | 221.69–221.69 | 39.78 |
| quote | cold-1 | 1 | 174.33–174.33 | 174.34–174.34 | 229.26–229.26 | 42.85 |
| quote | cold-2 | 1 | 149.50–149.50 | 149.52–149.52 | 217.02–217.02 | 34.00 |
| quote | repeated | 4 | 63.64–69.22 | 63.66–69.23 | 41.97–49.12 | 33.98 |
| quote | distant-edit | 3 | 62.16–76.49 | 62.17–76.51 | 43.79–77.73 | 32.66 |
| details | first-combined | 1 | 74.69–74.69 | 74.70–74.70 | 98.84–98.84 | 38.29 |
| details | cold-0 | 1 | 166.48–166.48 | 166.50–166.50 | 290.82–290.82 | 44.18 |
| details | cold-1 | 1 | 161.24–161.24 | 161.25–161.25 | 235.76–235.76 | 31.80 |
| details | cold-2 | 1 | 159.17–159.17 | 159.19–159.19 | 245.28–245.28 | 34.26 |
| details | repeated | 4 | 59.74–70.75 | 59.75–70.76 | 49.26–74.49 | 39.42 |
| details | distant-edit | 3 | 57.16–70.19 | 57.17–70.21 | 45.94–69.82 | 42.62 |
| mixed | first-combined | 1 | 729.91–729.91 | 729.93–729.93 | 564.25–564.25 | 43.60 |
| mixed | cold-0 | 1 | 644.99–644.99 | 645.00–645.00 | 855.66–855.66 | 49.61 |
| mixed | cold-1 | 1 | 632.31–632.31 | 632.32–632.32 | 803.42–803.42 | 43.43 |
| mixed | cold-2 | 1 | 634.47–634.47 | 634.48–634.48 | 777.53–777.53 | 35.46 |
| mixed | repeated | 4 | 134.47–452.21 | 134.48–452.22 | 173.77–488.64 | 45.28 |
| mixed | distant-edit | 3 | 148.00–499.72 | 148.01–499.74 | 174.51–476.08 | 34.73 |

| Separate shown-window scenario | Functional/status receipt | Actual in-process Input max ms |
|---|---|---:|
| studio-checks | PASS | 56.859699999999975 |
| parity-all | PASS | Not separately timed |
| appearance | FAIL | 186.38210000000004 |
| lifecycle | PASS | 79.65110000000004 |

Allocation, natural GC, post-probe working/private/managed memory, request/cancellation/readiness, GUI/UIA and every installation observation remain in the original receipts. No forced collection is used. Functional PASS labels refer only to executed assertions, not full required parity.
