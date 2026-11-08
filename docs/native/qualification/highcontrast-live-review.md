# Independent review: corrected HIGHCONTRAST verification preparation

**Disposition: preparation is reviewable; hosted execution remains BLOCKED / NOT_AUTHORISED.** This independent technical review binds the source and sealed artefacts below. It is not owner approval, supervisor qualification, a successful live HIGHCONTRAST observation or acceptance of PR #76. No collector, native fixture, Win32 getter, GUI or hosted workflow was executed by this reviewer during this review.

The remaining blocking observation is the retained local supervisor result: PID **37804**, exit **0**, Job **TotalProcesses=2 / ActiveProcesses=1**, `cleanupVerified=false`. The new accounting instrumentation has only mocked validation. The prepared configuration rejects execution before authority/network/native/process access even if an owner supplies an otherwise matching comment.

**Exact reviewed identities.** Collector/tooling source commit **S** is `2a9608f6af5f4847c9ed13a19ba3466a25da1f47`, tree `c8075f2a2fdac6fb60c77697bb741285931267a6`. Both were independently checked in Git. The final package commit **E** and an owner-authored execution approval are deliberately pending at this review. A later evidence/documentation commit does not substitute for S or change these bindings.

| Artefact | Independently calculated SHA-256 |
|---|---|
| [Configuration](highcontrast-live/config.json) | `c2cf91cbd5678752d028d7f64a6d87a90e183fb1f73c12de35b9cc967ef13d7a` |
| [Collector ZIP](../evidence/highcontrast-live-readiness/collector.zip), 113,417 bytes | `9c544090eacaa75170110201240f5a64d1476ea7e99de079a6b6e879daf10fdf` |
| [Build receipt](../evidence/highcontrast-live-readiness/build-receipt.json) | `9505857cacca58868fcd7321ed8997c79aa995ad0bc35fdadd20e3a6d392b6bd` |
| [Preparation evidence ZIP](../evidence/highcontrast-live-readiness/preparation-evidence.zip), 408,727 bytes | `09e7c8918b1e48fc8cd24f43b700e57dfcff423f31ce4f2cdc5cf562e65fb9aa` |
| [Original environment lock](../evidence/hosted-environment-lock/environment-lock.json) | `45a6aa515df05d93169521119b3bdab59db9512a439041e987495e373d5c466e` |
| Core 10.0.14 archive, 36,971,047 bytes | `d861ab80f232adf3bdce83293bc1fa92868400330c477bf854d8558eb91c7ca6` |

All **14/14 implementation hashes** in the configuration match canonical `git show S:path` bytes. All **5/5 collector source hashes** agree with those implementation entries, the build receipt and the source files retained inside the preparation ZIP. These are Git byte identities, not claims that every local checkout has the same line endings. The reviewed local workflow has SHA-256 `68dee2e7213899740e521d7248d2df20ab4ca6e1358e41c38e57855b711a8b7a`; its canonical Git hash is `08ec4169156e3aacad81127e4c29106fa279513a6516221e1b2ca32703b7cbb8`. Their only difference is CRLF versus LF. The new workflow checkout explicitly uses `core.autocrlf=false`.

The five binary members are unique, pass ZIP CRC validation and match the configuration, build receipt, retained build outputs and nested collector ZIP exactly:

| Member | SHA-256 |
|---|---|
| `DesktopProbe.exe` | `f8d0c064b37060f5890b9b7a731062a18b4bea8915c9835a7d4c0ebf95c320fe` |
| `DesktopProbe.dll` | `3ed0efdf2a9a841486da315888f761ab22ce4feb14260a92f0ecbec8eafdc10c` |
| `DesktopProbe.pdb` | `d71bfe61713956b4d43d81e044881dc9cc69f510820235e517926a2072037909` |
| `DesktopProbe.deps.json` | `9c2ef70d198253d937b961960c4de53cce477bb9e09a5990575207fd5e8aaff2` |
| `DesktopProbe.runtimeconfig.json` | `13a73aba9a05d93aab51d1edee476df13d89bf8705f28bc69bea1b31b816efd9` |

The preparation ZIP has **68 unique members** and valid CRCs. All **35 build-output entries** match the retained receipt, with no additional or missing entries in that build subdirectory. The retained compiler output reports exit 0, zero warnings and zero errors; stderr is empty. The source-version bytes `1.0.0+S` and system-DPI manifest text are present in the sealed DLL/apphost bytes. Runtime configuration specifies Core **10.0.14** with `rollForward=Disable`. This is static inspection of compiled files and recorded build evidence, not a second reproducible build or a loaded-runtime observation. The three recorded SDK/compiler file hashes also match the available local files; SDK 10.0.401 is a build identity, not the hosted runtime identity.

The downloaded Core archive independently matches its original SHA-256, SHA-512 and byte count. All **193/193 unique archive members** match both the prepared configuration and the corresponding entries in the original 480-file Core-plus-Desktop environment lock. The retained `runtime-files.json` is identical as a mapping and has SHA-256 `b7a2d4ae80853551ef84319330b82c0b69341e8aa18075bd9fa6e7a3f1ee0b5e`. No WindowsDesktop runtime closure is included. The original image/Windows record is unchanged: `win25-vs2026` / `20261103.298.1`, Windows build 26100, UBR 33674. This does not establish that a future hosted runner still has that image; drift must reject execution.

**Reviewed controls and their limits.** The [coordinator](../../../scripts/native/qualification_hosted/live_verification.py), [supervisor](../../../scripts/native/qualification_hosted/live_process.py), [sealer](../../../scripts/native/qualification_hosted/prepare_live_verification.py), collector identity additions and separate workflow job were inspected. The reviewed design retains one collector process, two fixed GETs, a 30-second supervision deadline, a separate five-second cleanup limit and zero retries, qualification or calibration processes. The borrowed [reader](../../../scripts/native/qualification_hosted/desktop_probe/HighContrastReader.cs) is byte-identical to source `4e2e37f2aa285cf318edd50d9cf2d0233b7eb4e6`; its previous [ownership review](highcontrast-buffer-review.md) remains applicable.

The source requires suspended creation and Job assignment before resume, an active-process limit of one, cumulative accounting, observed empty membership and raw accounting/limit/PID records. It retains process handles when post-create setup fails. The audit rejects quota terminations, missing records, changed identities, false cleanup, extra processes, crashes and deadline contradictions. These checks do not certify the real kernel path: synchronous Windows calls and thread scheduling are not hard real-time guarantees, and closing a kill-on-close Job is not an observed empty-membership result.

The workflow's new opt-in job excludes all six other jobs, uses exact checkout identity, has no hosted build or second diagnostic helper, and isolates the collector environment from GitHub tokens, profiling hooks and runtime overrides. The same collector PID would check its apphost, assembly, CoreLib and loaded runtime-native file identities before and after the fixed reads. File hashes are not relocated-memory measurements, and this preparation does not certify every loaded operating-system module. No such live identity record exists yet.

Authority checks bind an unedited owner comment to the exact E/tree, configuration, S/tree, archives and bounds. A proposal defaults to `approved=false` / `PROPOSED_NOT_AUTHORISED`. Run-attempt and history checks plus workflow concurrency reduce accidental reuse; API snapshots are not atomic consumption tokens. Later or parallel dispatch evidence can invalidate acceptance. The current configuration remains `NOT_AUTHORISED`, `campaignExecutionApproved=false`, `readOnlyVerificationApproved=false` and `supervisorQualification=BLOCKED_LOCAL_JOB_ACCOUNTING_UNRESOLVED`. No owner comment can bypass that last prerequisite for these bytes.

**Independent offline validation.** The reviewer ran the following established modules in separate Python processes immediately before S was committed. Their exact tested source bytes equal the S blobs; sealing did not alter them.

| Command | Independent result |
|---|---|
| `python -m unittest discover -s tests/unit -p test_native_live_verification.py -v` | 17 tests PASS, 1.128 s |
| `python -m unittest discover -s tests/unit -p test_native_hosted_execution.py -v` | 17 tests PASS, 6.247 s |
| Read-only Git/ZIP/hash/configuration verification described above | PASS for the sealed preparation bindings |

The new tests use fake kernels and synthetic evidence; the local CLI negative control rejects before native access. The blocked-configuration control proves no authority API or launch call occurs and retains `NOT_RUN` / `BLOCKED`. The default proposal was also checked offline to remain unapproved. The `audit_package` final-evidence entry point was source-reviewed; it was not run against a real live collection because none exists. No broad combined native test discovery, collector execution, fixture repeat, appearance/display/input change or hosted dispatch was performed by this reviewer.

Additional exact canonical bindings for the builder and independently run tests are:

| File | SHA-256 |
|---|---|
| `prepare_live_verification.py` | `99d2bc3b9f4f110c70ce56b0d5f91b4225406e3c8cfc2fade67a596a9e826ef0` |
| `test_native_live_verification.py` | `fc9be783068d7be2268eb0d81336f52044438628b7721c434392d05b6d01dd18` |
| `test_native_hosted_execution.py` | `35bc45047d0d60c9bda3062811d1a8bbd58baeefcf344a6e131292c244c9f56d` |

**Findings and disposition.** Source-file digests are now validated and must equal the corresponding implementation hashes; quota-termination evidence cannot pass; the blocked execution path and environment scrubbing have explicit negative controls. No additional material defect was found that prevents retaining this preparation package with its blocked verdict.

The original supervisor failure is preserved, not superseded. Its four raw result/output files in the preparation ZIP exactly match the original local files. The retained supervisor source has SHA-256 `787b01ba1a3abdd87b78feb5bd4b5615630e5949798ef11b4be2f2957fb1c78a`, agreeing with its source-binding record; it predates the new raw-accounting/PID instrumentation. Fixture SHA-256 is `311655cd165d4a82631efce4b827decea6a5199d2e723da67175b94a4539e549`. Its 21 fixture checks and exit 0 do not clear the Job 2/1 failure. The local inherited environment was not retained, so that control cannot establish equivalence to the proposed hosted environment allowlist. No ABI/query error, extra-process identity or safe-cleanup explanation has been demonstrated. Counter normalisation or a presumed console host would be unsupported.

The earlier hosted-unit-test FAIL is also retained. Its section delimiter included the newly added separate job and therefore saw that job's parent `GH_TOKEN`. The reviewed amendment stops at the new job boundary; it does not remove the original token-isolation assertion or weaken the workflow's child allowlist. The subsequent separate-process PASS does not erase the failed transcript.

The minimum remaining prerequisite is independently reviewable real evidence for the supervisor's single-member assignment, applied limits, raw accounting, termination and empty membership, obtained under separately agreed scope. This review authorises no new control. Resolving that prerequisite requires a revised configuration/source package as applicable, fresh hashes and independent review before any owner execution decision. The historical second-`LocalFree` heap-corruption origin, pointer aliasing and double-free attribution remain **INCONCLUSIVE**; the confirmed source ownership error is a separate finding.

PR #76 remains **NO-GO**. R01/R02, geometry recovery, appearance restoration, release certification and ADR operational activation remain blocked or unapproved under their existing records. This review changes no threshold, required check, historical result or human decision.
