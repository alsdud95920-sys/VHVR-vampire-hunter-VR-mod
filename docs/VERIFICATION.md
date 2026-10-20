## Latest automatic verification — 2026-10-09 (D-084)
- Full verifier actually completed: exit 0, 26/26 Passed=true, stderr empty; same active check identities as D-083. Evidence frozen under `<RepoRoot>\artifacts\host-stability-d084`, including per-check logs/receipts.
- 28 native-host fault fixtures execute actual Host.cpp with test-local XR/GPU responses: BUSY, pending/error/timeout, copy/release/layer order, empty/invalid frames, frame budget, STOPPING budget, failed cleanup, idempotency and retained-source retry. They are **not real runtime/driver/game tests**. Neither fixture executable links an OpenXR loader.
- Separate hardware GPU checks execute production Gpu.cpp: four exact copies, four reversed-row copies, four shape rejections. Native product/test builds /W4 /WX PASS; managed Release build zero warnings/errors. Seven product runtime exports, no NativeSelfTest export. Native EyePose/HostStatus ABI remains 44/136 bytes; managed failure-cleanup/watchdog checks are source/build only, not Unity callback execution.
- Native paired-eye math 29/29 retained. Independent stock exit 0: 121 files/8 directories/7,726,223,158 bytes match hashes and write times; all 16 original checked receipts match; x64/x86 VDXR unchanged. No game/runtime session or headset sitting.
- Preserved: vcvars64 PATH regression failure log and historical interrupted invocation marked INCOMPLETE. Fresh entire final verifier completed after compiler-child-only PATH repair. No retroactive PASS, TOOL-033/Q-047 solution, TOOL-034 full 600-second transaction repeat, real runtime recovery or M4b acceptance.

## Historical headset verification — 2026-10-08 (hd5 / D-075)
- **Full SP5 M2 COMPLETE:** headset sitting on build `9861b63`, VDXR Quest 3, D1 300/301 PASS and D2 60/61 PASS (images cleared, typeless runtime image -> explicit UNorm RTV), zero discarded/invalid-pose/frame/wait/locate failures. Required adapter matches; 2 views, 2428x2440, format 28. User: **"왼쪽 빨강, 오른쪽 눈 파랑 맞았는데, 너무 짧았긴 해, 다음부터 3초 더 늘려"**.
- Sitting evidence `artifacts/sp5-m2-vdxr-headset.hd5.json`, preserved `artifacts/openxr/probe/hd5-d1.json` and `hd5-d2.json`. Whole command 19.2 s measured; stock check PASS (121 files). No probe left after the sitting. Harness exitCode fields are null (not evidence of child exit 0); completed PASS receipts and the harness result provide frame evidence. TOOL-024 did not recur; TOOL-022 still warns on shutdown.
- Future C2/D2 visual tests use baseline 60 submissions **plus 3 actively submitted seconds**, recorded with `--hold-seconds 3`. D1/preflight unchanged. Debug and Release builds PASS, zero warnings/errors.
- New targeted regression `tests/openxr-hold.Tests.ps1 -StartSimulator -Tag hold4`: PASS marker for A-D real rung tables, negative/oversize duration validation, baseline unchanged 60/61 (hold=0), +3 s PASS 276/277 (216 extra submissions, 3.0001039 s measured), and budget-limited hold expected FAIL with an explicit incomplete-hold error. `artifacts/openxr-hold.hold4.json`; completed stdout PASS and empty test-runner stderr. Simulator clear has zero frame/wait failures/discards. Earlier wrapper attempts hold1..3 failed in Windows PowerShell process/log handling, repaired in the final runner; hold3's probe itself already measured 3.0004972 s correctly.
- D3D11 self-test `artifacts/openxr/probe/selftest-d075.json`: PASS, typed=ok, typeless=ok. AST parsing PASS for hold test, VDXR harness and existing simulator drill; git diff whitespace check PASS.
- These are targeted SP5 checks, **not** a new full `tests/verify.ps1` run, composed-eye image M3, game-camera M4b, gameplay stereo geometry or Phase 1 acceptance. No repeat headset sitting for the duration change.


﻿# Preparation verification — 2026-10-06
Repro command:
`powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File <RepoRoot>\tests\verify.ps1`

Actually executed; wrapper exit 0. Local machine-readable evidence:
`<RepoRoot>\artifacts\verification.json`
Each child process captured stdout/stderr independently, actual/expected exit codes recorded.

| Check | Actual / expected | Observation |
|---|---|---|
| Parser + PE fixture suite | 0 / 0 | 18 passed, zero failures |
| Recon build | 0 / 0 | Zero warnings/errors |
| Real installed-game recon | 0 / 0 | Unity 2022.3.37f1, Steam build 22267731, metadata v31 |
| CLI missing arguments | 2 / 2 | Usage error, not silent success |
| CLI output under game | 1 / 1 | Rejected before directory creation |
| Entry diagnostic build | 0 / 0 | net6.0 DLL, zero warnings/errors |
| Typed flag/default mirrors | pass | Two bool entries and inventory JSON parse |
| Re-read four game hashes | pass | EXE, UnityPlayer, GameAssembly, metadata unchanged |
| PowerShell ASTs | pass | verify.ps1 and verify-config.ps1 |

Metadata partial coverage: table at 633560, bytes 1770628; 97,424 nonempty null-delimited segments, 2,962 distinct matched names, 46 undecodable UTF-8 segments with offset/hex evidence. Includes framework/generated strings; NOT a class/member ownership dump. No import or UTF-16 scan claimed.
Report `<RepoRoot>\docs\playtests\reports\static-stage0\static-recon.json` actually exists; raw identifiers remain uncommitted and local.

BepInEx ZIP: 34,336,405 bytes, SHA256 F4CC496BD098A0DF4164B81E3737297707F13A47C2478DBA2F60EEFAB784817A, pinned build be.788+5b766a3. Download timeout resolved by verifying archive/extraction separately. Pinned commit LGPL-2.1-or-later license reviewed; no loader installed.

## Not validated / NOT passed
- Agent-observed headset test, exact native-title/runtime pairing and actual game XR session; policy clearance and update retention. Install/save backup/isolated restore subsequently verified D-019; policy research subsequently completed D-021 but permission/risk clearance remains. User confirmed Quest 3/bundled controllers/Virtual Desktop and other VR games/EDF6 VR mod working. Subsequent consented PC inventory observed hardware/software and VDXR default OpenXR registration/runtime DLL presence; user confirmed the presented PC/VR inventory with "네" on 2026-10-06. This does not authorize game launch or installation. Registry registration is not a session test; see TARGET_PROFILE and D-009.
- BepInEx IL2CPP interop generation or execution in this game's process.
- Visible on-screen diagnostic, actual camera hook, config hot-reload/invalid-config runtime behavior.
- Actual graphics API/URP usage, stereo, head or hand tracking, controller bindings, locomotion or weapon aiming.
- Restore after installation (no installation occurred), headset comfort, Phase 1 acceptance.
- Renderer framework selection/current-build compatibility; UUVR GPL code not incorporated.

Therefore **Stage 0 remains open**. Simulator/in-game assertions not applicable to pure static tooling yet; must be built with corresponding real features before later gates. Unit success does not substitute for any in-headset result.

## Fresh visible-evidence recheck after user confirmation
2026-10-06: original direct full-verifier tool call timed out at 30 seconds; do not claim an exit for that invocation. A separate captured process then completed and explicitly printed FULL_VERIFIER_PROCESS_EXIT=0 and the final VERIFIED line. Readback confirms 18/18 unit tests, both builds with zero warnings/errors, config checks, expected CLI exits 2/1, partial recon (46 undecodable segments) and four unchanged target hashes. Evidence: `<RepoRoot>\artifacts\confirmation-verifier.stdout.log`, `<RepoRoot>\artifacts\confirmation-verifier.stderr.log`, and regenerated verification.json. Wrapper stderr contains only CLIXML module-init progress, not a test error.
ZIP freshly hashed: 34,336,405 bytes and SHA256 match DEPENDENCIES.json. Direct verify-config invocation printed AFTER_CONFIG_CHILD_REACHED=True and CONFIG_CHILD_LASTEXITCODE=0; no invoking-script early exit reproduced. A scriptblock-only negative check changed SafeMode to a string in memory and propagated the expected `Invalid type: SafeMode` exception to the caller; actual config/source files untouched. Both verifier PowerShell ASTs pass. This is tooling failure propagation, not in-game invalid-config handling. Fresh consent-scoped PC/VR stdout confirms Windows 11 build 26200, 9800X3D, RTX 5080/AMD iGPU, RAM 32/31.16 GiB, Streamer 1.34.22.0, both VDXR runtime registrations and referenced DLLs. User confirmation resolves the earlier pending item; it is not an XR/session or Stage 0 pass.

## Latest mechanics recon verification — 2026-10-06
After Q-010/Q-016, fresh full verifier explicitly returned FULL_VERIFIER_PROCESS_EXIT=0. Evidence: `<RepoRoot>\artifacts\mechanics-verifier.stdout.log`, stderr counterpart, mechanics-verifier.exit.txt and verification.json. Initial asynchronous wrapper produced a blank exit, not exit evidence; direct captured rerun provides the actual 0 above.
- Tests **23 passed, 0 failed**: previous 18 plus first-occurrence offsets/UTF-8 byte length, mechanic keyword grouping, empty evidence, deterministic sample cap and undecodable-name nonreplacement.
- Recon and Stage 0 diagnostic builds: zero warnings/errors. CLI missing args exit 2; game-output rejection exit 1 before writes. Config two bool mirrors and JSON parse passed.
- Real installed metadata: 97,424 nonempty segments, **3,920** distinct matches with expanded filter, 46 undecodable; table offset 633560/length 1770628 unchanged. Original 18/2,962 entries above are historical, not latest counts.
- Local mechanics report: seven expected IDs, string offsets within table, source metadata hash alignment, sample count/truncation flags checked; candidate input not truncated, per-bucket cap 64. Names/groups overlap/include unrelated strings; counts are not game mechanic counts. No Q/E bindings/class ownership/function addresses recovered.
- Four core/metadata hashes unchanged; no BepInEx installed or forbidden-output folder created. No game/loader/XR execution, stock overwrite, PC/runtime settings change or private saves read. **Stage 0 NOT PASSED**; no camera hook/VR/playtest success claimed.

## Backup-session verification — 2026-10-06
Q-014/D-018 now authorize game-only save-folder backup reading/copy/hash, not unrelated data/payload parsing. `<RepoRoot>\artifacts\backup-result.json` and backup.exit.txt record verified operation/0: 121 install + 6 save-folder files, all source/snapshot/full isolated restore hashes/path sets match; originals/timestamps unchanged, no game launch/stock restore. Snapshot and local verification under `<RepoRoot>\backups\20261006T104307Z-cdd080e1e50a4fda845ffa7c01158173`; manifest hash matches D-019. Independent game/save rehash confirmed all three trees. Backups ignored; same-drive recovery only.
New backup fixture suite **12/12**, original recon suite **23/23**; final full verifier completed with actual marker **0** and seven child records at expected exits 0/0/0/0/2/1/0, builds zero warnings/errors and configuration/hash checks. Initial foreground 30-second timeout is not pass evidence; separate background runner's completed exit marker and final VERIFIED line are recorded in `<RepoRoot>\artifacts\backup-session-verifier.exit.txt` and stdout/stderr logs. Five PowerShell AST checks and Git diff check passed. Backup operation is independent of full recon verifier; generic full verifier runs fixtures, not actual new snapshots. **Stage 0 NOT PASSED**, stock recovery launch/runtime/config/stereo/input still untested.

## Entry-planning session — 2026-10-06 (historical)
Rechecked HEAD b3a8f3a and clean tree. Actually executed new read-only preflight and corrected entry fixtures, then full verifier. Local evidence:
- `<RepoRoot>\artifacts\entry-session-verifier.exit.txt`: **0**, final VERIFIED in stdout, stderr empty.
- `<RepoRoot>\artifacts\verification.json`: **9** child results, every actual equals expected and Passed=true.
- `<RepoRoot>\artifacts\entry-preflight.json`: **VERIFIED_PLAN_ONLY_NOT_DEPLOYABLE**, completion **2026-10-06T11:01:58.7938014Z**, GameWrites/GameLaunch NONE, InstallationAuthorized/Stage0Passed false, holds explicitly listed.

| Check | Actual / expected | Observation |
|---|---|---|
| Backup fixtures | 0 / 0 | 12/12, synthetic only; no new backup/save access |
| Entry planning fixtures | 0 / 0 | 21/21, exact app/build, changed size/hash, ambiguous manifest, empty/duplicate entries, path/ZIP traversal/case aliases/symlinks, extraction tamper/extras, root collisions and no-write planning |
| Recon fixtures | 0 / 0 | 23/23 |
| Recon build | 0 / 0 | Zero warnings/errors |
| Static recon | 0 / 0 | Same 97,424 segments/3,920 matches/46 undecodable, seven candidate groups/offset checks |
| CLI missing arguments | 2 / 2 | Expected usage failure |
| CLI game-output rejection | 1 / 1 | No forbidden directory created |
| Diagnostic build | 0 / 0 | Zero warnings/errors, no plugin source/VR changes |
| Read-only entry preflight | 0 / 0 | All **121** stock hashes/path/directory/timestamps match pinned existing backup; pinned **228** loader files match ZIP, DLL hashed, **229** proposed additions, no conflicting target roots |
| Config mirrors/AST/Git checks | pass | Two typed defaults/JSON; all **8** test PowerShell files parse; diff check clean; backups/artifacts ignored |

Automatic tooling feedback preserved, not concealed: first Entry.Tests expression issue fixed by parenthesizing concatenated strings; second missing ZipArchiveMode assembly fixed by explicit System.IO.Compression load. First background full verifier then stalled after recon 23/23 stdout with a persistent compiler descendant, no final pass. Cancelled only this session's matching verifier process, wrapper returns **-1**; its marker/partial logs retained as `.attempt1`. Added --disable-build-servers, UseSharedCompilation=false and MSBuildNodeReuse=false to run/build checks; fresh full run completes with marker 0/VERIFIED above. No unrelated compiler or Steam/game process stopped.
Policy sources/developer quotes and version-retention limits recorded in ENTRY_PLAN.md/D-021/D-022. Current public metadata matches build, but guard is not Steam version pinning. No permission/protection/score-isolation inferred from failed EULA or missing filenames. No game/loader/XR launch, original overwrite, Steam/Cloud setting change, save interpretation/new backup or Stage 0 pass/tag. Plan is not an executable installer; reversible transaction and visible diagnostic still need current-stage implementation/tests plus consent/runtime verification. Stage 1–4 implementation remains blocked.

## Coding continuation — 2026-10-06 (latest verified run)
Starting e8e1d70/clean. Actually implemented/tested Stage 0 transaction and settings behavior; nothing invoked on real game except existing read-only recon/preflight. Evidence `<RepoRoot>\artifacts\coding-session-verifier.exit.txt` **0**, stdout final VERIFIED, stderr empty, verification.json **11** named child checks. Verified names/order/expected exits **0/0/0/0/0/0/0/2/1/0/0**, and exact test-suite identity/count in stdout, not exit alone.

| Check | Actual / expected | Observation |
|---|---|---|
| Backup fixtures | 0 / 0 | 12/12 synthetic, existing actual backup reused |
| Entry planning fixtures | 0 / 0 | 21/21 |
| Transaction/recovery fixtures | 0 / 0 | **27/27**, staged publish/quarantine, changed/foreign content refusal, safe path/junction/source/stock checks, all root interruption boundaries, concurrent tool mutex, fixture-child termination before result journal and replay |
| Pure settings fixtures | 0 / 0 | **18/18**, true-default bool validation, no malformed-byte rewrite, bounded strict UTF8, locked-file defaults, same-size/time fingerprint, marker lifecycle |
| Recon fixtures | 0 / 0 | **23/23**, confirmed actual recon suite stdout, isolated from settings executable |
| Recon build / static recon | 0 / 0 each | Zero warnings/errors; same 97,424 segments/3,920 candidates/46 undecodable and seven group checks |
| Missing args / rejected game output | 2 / 2, 1 / 1 | Expected failures, no forbidden game directory |
| Diagnostic net6.0 build | 0 / 0 | Zero warnings/errors; settings integrated, no Unity/camera/XR work |
| Read-only entry preflight | 0 / 0 | **121** stock paths/hash/times match pinned backup, **228** ZIP/extracted files + DLL = **229** proposed files |
| Config mirrors / AST / diff | pass | Two bool defaults/JSON, **10** PowerShell ASTs, Git diff clean, generated evidence/backups ignored |

Final plan completion **2026-10-06T11:16:44.6616735Z**, status VERIFIED_PLAN_ONLY_NOT_DEPLOYABLE, InstallationAuthorized/Stage0Passed false, GameWrites/GameLaunch NONE. DLL SHA256 **7D705CED852B0093996BB11078607AF243EFB9A70674B7E6B32BAE0DEAD01695** aligns with plan.
Failures preserved: attempt1 wrapper exit1 after settings 15 passes (oversize InvalidDataException not caught); explicit catch added. Attempt2 wrapper0 genuine 25 transaction/18 settings/23 recon; historical only. Expanded attempt3 wrapper0 is **NOT valid full pass**: unit-tests actually ran settings18 due to shared apphost obj/bin collision. Added early Settings output/intermediate separation in Directory.Build.props and stdout suite assertions, rebuilt recon, fresh final run above correctly prints 12/21/27/18/23. Actual fixture-child termination tests do not terminate game/Steam/unrelated processes.


Limits: no real installer/preflight-consent wrapper, loader/timer/unload/constructor-invalid-config/screen diagnostic/stock relaunch verified. Journal trusted-local not authenticated; tool mutex doesn't exclude external launch or other Windows sessions; no power-loss guarantee. Generated loader roots preserved in quarantine after completed install, unexpected stock/external data or changed expected payload stop recovery. BasePlugin reads cfg before Plugin.Load; pure settings tests do not prove that startup. No new saves/snapshot/stock overwrite/settings/email/game/XR launch, Stage 0 pass/tag or later-stage implementation. Coding remains permitted before execution decisions; D-023/D-024.

## Stage 1 spike preparation — 2026-10-07 (D-046 → D-048, latest verified run)
Starting HEAD e2a49b4/clean. No headset and no user feedback were available, so only the two headset-free pre-conditions of
the bounded UUVR spike were worked on: B3 (dependency metadata) and B4 (reversible expected file set plus engine support).
Nothing was installed, launched or patched; the game folder stayed stock (121 files / 8 directories).

- **B3 closed (D-047)**: live GitHub API plus the release bytes showed the artefact D-038/D-040 named
  (`raicuparta.uuvr-modern`, 0.4.0) is the **Mono** package - v0.4.0 (2025-07-03) publishes only `uuvr-mono-modern.zip`
  (4,441,049 B). The newest release with an IL2CPP variant is **v0.3.1** (2024-06-23): `uuvr-il2cpp-legacy.zip`,
  4,555,958 B, SHA256 `282F5FB30F9FD9E918EA673A83BA319025E46B2703D828504E3479EAE817F036`, plugin id
  `raicuparta.uuvr-legacy`. `Directory.Build.props` at that tag defines legacy = Unity 2019 or earlier / modern = 2020+,
  so this Unity 2022.3.37f1 target is out of generation - a pre-registered risk, not a surprise. Read-only
  `Recon assembly` on `plugins/Uuvr.dll` (33,792 B) resolves all 13 assembly references against the loader-generated
  interop set for this build, and `Uuvr.deps.json` pins the loader line at be.692 (this project pins be.788).
- **B4 declarative (D-047)**: `config/uuvr-spike-payload.json` freezes **36 installed** + **23 generated** files with byte
  sizes, SHA256 and source path, under **10 narrow scopes** plus **1 declared stock mutation** (`globalgamemanagers`,
  pre-image 228,452 B, hash-verified against the pinned backup).
- **B4 engine (D-048)**: `src/core/EntryTransaction.ps1` now plans scoped roots, refuses any scope that would mask pinned
  stock, verifies declared generated files (present = exact bytes, absent = tolerated only post-install), records a rewrite
  of a declared stock mutation and restores its pre-image with its original write time during recovery.
- **Negative-result evidence**: a new engine fixture proves that an *undeclared* file appearing inside a scoped directory
  stops recovery with all evidence left in place; a scope widened to the whole `Vampire Hunters_Data` directory, a nested
  scope, and a malformed mutation record are all refused. This is the anti-downgrade guard, tested rather than asserted.

| Check | Actual / expected | Observation |
|---|---|---|
| Backup fixtures | 0 / 0 | 12/12, synthetic only |
| Entry planning fixtures | 0 / 0 | **28/28** (was 21), new scope/masking/mutation/canonicalisation cases |
| Transaction/recovery fixtures | 0 / 0 | **32/32** (was 27), existing loader-only cases unchanged plus 5 scoped/mutation cases |
| Pure settings fixtures | 0 / 0 | 18/18 unchanged |






































































































































Proven vs not proven (same honesty rules as D-052):
- **Proven (validation only):** the table really binds `dash` to the left stick press and `jump` to the left upper face
  button, and `two_hand_link` to the **right** upper face button; the validator throws if `jump` reappears on the right
  hand, if `aux_weapon_2` or `two_hand_link` reappears on the left hand, or if any pinned pair moves.
- **Not proven:** the feel. Whether a stick-press `dash` with the stick held behaves as expected, and how much the brief
  movement stop when pressing X for `jump` costs, are Stage 3 item 8 / Stage 4 questions for a headset session. The
  D-052↔D-053 flip is one line and both variants stay recorded; nothing is implemented or bound in either.















































































































































































































































## D-060 — the UUVR spike's install path exists (engine amendment + one combined transaction)

Three headset-free proofs, all read-only except the synthetic fixture:

1. `tests/Transaction.Tests.ps1` → **`RESULT 33 passed, 0 failed`** (32 before). The new check *"a top-level payload file
   may be its own scope, a nested one may not"* proves the amended rule end to end on a synthetic fixture: the plan
   accepts `winhttp.dll` as its own scope (5 roots) while still refusing the same file with no scope
   (*"not inside exactly one declared scope"*) and a **nested** payload file naming itself (*"Declare the parent scope"*),
   then installs, lets the simulated tool write, recovers, and ends with `ADDITIONS_QUARANTINED_STOCK_VERIFIED`,
   `winhttp.dll` in quarantine and the stock hashes plus write times equal to the pinned snapshot.
2. `tests/uuvr-spike-plan.ps1` (new; wired into `tests/verify.ps1` as the `uuvr-spike-plan` check) →
   `UUVR_SPIKE_PLAN_OK: files=265 (stage0=229, uuvr=36); roots=15; generated=23; mutations=1; stock=121. Nothing written.`
   It re-derives every UUVR record from the pinned release ZIP, cross-checks it against `config/uuvr-spike-payload.json`
   row by row, re-hashes every source, and plans the combined payload against the real (still stock) game folder.
3. `src/core/Install-UuvrSpike.ps1 -Action Plan` → the same line, exit 0 (`artifacts/spike-plan.d060.*`).

**What this does not prove.** A plan is not an install. The game folder is still stock (121 files / 8 directories,
untouched — nothing was written by any of the above), nothing has been installed, launched or run, UUVR has never
executed on this build, and SP1–SP9 remain open. The engine amendment is proven only on synthetic fixtures; the real
install's reversibility stays unproven until the spike's teardown returns `ADDITIONS_QUARANTINED_STOCK_VERIFIED` with
121/8 intact, so **SP8 is open**.

## D-061 — the Session 1 spike ran and failed (`FAIL_NO_STEREO`); what is proven, and what is only observed

Four launches, all through Steam (`steam://rungameid/2206270`), all with the same payload — logs kept under
`artifacts/uuvr-spike/run-flat-1/`, `run-flat-2/`, `run-vr-1-theater/`, `run-vr-2-nosteamvr/`:

1. **SP1 is proven.** `src/core/Install-UuvrSpike.ps1 -Action Install` returned `Installed` (exit 0,
   `artifacts/uuvr-install.d060.*`) for 265 files in one transaction; an **independent** re-hash (not the engine's own
   check) then confirmed **265/265 added files byte-identical to the plan** and **121/121 stock files identical in hash and
   write time** with all 8 stock directories present. An earlier "directory inventory differs" line was my own method error
   — an install legitimately adds directories — and was corrected to the stock-file comparison above.
2. **SP2 fails on its own wording, not on a crash.** Every launch logs
   `[Error :AssemblyPatcher] Failed to run initializer of com.raicuparta.uuvr: System.NullReferenceException` and then
   continues; UUVR's runtime loads (`Loading [UUVR 0.3.0]`, its types register) and our plugin reaches
   `[Stage0] ENTRY_EXECUTED` in the same session every time.
3. **SP3/SP4 fail on machine-checked evidence, not on a report.** The read-only probe logs
   `[Probe] SAMPLE scene='MainMenu_2' screen=1920x1200 xrLoadedDevice='' xrEnabled=False` — from `Intro` at t=0.2 s onward —
   in all three VR configurations, including the one where `vrserver.txt` had `Using existing HMD oculus.1PASH5D1P17365`
   and the user was wearing the headset. The user's own observation that Steam showed the game as a **VR Theater** window is
   the same fact seen from inside the headset.
4. **The declared mutation never happens.** `globalgamemanagers` still hashes `B9DF43EE…A87D1D9` (stock) after every run;
   the patcher dies before writing, so `globalgamemanagers.bak` is the only artifact.

**What this does not prove.** Nothing about UUVR being unworkable in principle: its runtime half is demonstrably alive and
the blocker is one identifiable step (the settings patch), so this is a **fix-or-drop** question (D-062), not a dead end.
It also proves nothing about our own Stage 2 design, and the D-049 `get_keys` prediction is now recorded as **not observed**
in any run.

### D-061 teardown — SP8 is now proven on the real install

`src/core/Install-UuvrSpike.ps1 -Action Uninstall` returned `ADDITIONS_QUARANTINED_STOCK_VERIFIED` with
`GameLaunched=false` (exit 0, stderr 0 bytes, `artifacts/uuvr-teardown.d061.stdout.log`), the journal's last event is
`Recovered`, and independent checks outside the engine confirm **121/121 stock files identical in hash and write time**,
8/8 stock directories present, **0 extra files**, and a stock flat relaunch whose `Player.log` has **0** matches for
`BepInEx|doorstop|winhttp|UUVR|Uuvr|Stage0` (only the single stock `[Subsystems] … UnitySubsystems` line, versus eight
`descriptors matched` lines while UUVR was installed). This closes the "real install's reversibility stays unproven" gap
that D-060 left open, so **SP8 PASS** and the spike's verdict stands as `FAIL_NO_STEREO` rather than "spike invalid".

Two process findings are recorded with it: the first teardown was killed by the 30-second command limit after writing
`QuarantineIntent` and before moving anything (quarantine empty, all files in place) and the re-run completed from that
journaled state; and the real teardown needs ~7 minutes because each of the 15 roots re-verifies the stock inventory, so it
must be run detached.













































































- install exit **0**; `XR_DEVICE_PATCH_APPLIED: … 228452B/B9DF43EE… -> 229764B/8DAFFBEF… devices=[OpenVR,OpenXR,Oculus]`;
  the journal ends `MutationObserved` and the live digest equals the patch report's `patched.sha256`.
- launch: `steam://rungameid/2206270`, game pid 49592, first probe sample after 44 s, closed with `CloseMainWindow`;
  `Player.log` rewritten (13,778 B -> 14,813 B), i.e. the launch is real and not a stale file.
- capture: probe reports at `t=0.3`, `9.8`, `19.8`, `29.8`; **all four** read `xrLoadedDevice='' xrEnabled=False`;
  `Player.log` carries the engine's subsystem discovery for **both** `UnitySubsystems/UnityOpenXR` and
  `UnitySubsystems/XRSDKOpenVR` plus UUVR's own `XRSettings.enabled` deprecation warning, and there is no device-load
  line naming `OpenXR` anywhere.
- teardown exit **0**; final snapshot = stock (`globalgamemanagers` `B9DF43EE…A87D1D9` / 228,452 B, 121 files, no loader
  and no launch-generated files).
- verdict **`STAGE_A_FAIL`**, PASS conditions 0/3. Exit code 0 means "the harness ran"; the verdict is the science, and
  a clean FAIL is a result.

The mechanism, measured separately and read-only (no game mutation): the build itself contains no XR loader code.
`Vampire Hunters_Data\il2cpp_data\Metadata\global-metadata.dat` (10,889,004 B) and `GameAssembly.dll` (47,464,960 B)
both contain `XRDisplaySubsystem` (8 / 26 hits) and `XRSettings` (4 / 13) but **zero** occurrences of
`XRGeneralSettings`, `XRManagerSettings`, `XRLoader`, `OpenXRLoader`, `OpenVRLoader`, `UnityEngine.XR.Management`,
`UnityEngine.XR.OpenXR`, `XRConfigurationData`, `XRDeviceSimulator`, `XRSDKOpenVR` or `UnityOpenXR`. Positive controls
in the same scan: `UniversalAdditionalCameraData` 4, `PlayerController` 8, `Weapon` 375,
`UnityEngine.Rendering.Universal` 387. Comparison on the same machine: `Arken Age` (Unity 2022.3.46f1, a shipping VR
game, same `UnitySubsystems`/native plugin layout) reports `enabledVRDevices(0) = []` and `ROUNDTRIP_OK blob=32168B`
through `XrDevicePatch inspect`, i.e. an empty device list does not stop a build that has the loaders. Recorded as
`docs/BUGS.md` LIM-003.

**The Q-038 follow-up (`sa3`).** `--devices OpenXR` alone, same preconditions and the same one-launch protocol
(17:23:07Z–17:34:30Z, `artifacts/stage-a-device-check.sa3.json`): install exit 0 with
`XR_DEVICE_PATCH_APPLIED … 228452B/B9DF43EE… -> 229740B/27F4C913… devices=[OpenXR]`, journal ends `MutationObserved`,
launch through Steam (pid 43584, first probe sample after 41 s, `CloseMainWindow`), teardown exit 0, final snapshot =
stock. Verdict **`STAGE_A_FAIL`** again, PASS conditions 0/3, and the same four reports read
`xrLoadedDevice='' xrEnabled=False`. Because there is no earlier list entry that could block a fall-through, this
rules the order/name hypothesis out: the list is not the gate (Q-038 answered).


**What this does not prove.** Nothing about stereo, head tracking or input: Stage B was not run, because the
pre-committed rule for a Stage A FAIL is to stop and ask the device-name question first. SP2 stays FAIL (UUVR's patcher
still throws on every launch) and SP3/SP4 stay unmeasured. What it *does* establish is that the device-list edit cannot
be the missing piece on this build, whatever names it holds - so the next question is not a name but whether this
target can load an XR device at all (Q-040).


**Post-run state, independently checked.** `artifacts/devtools/Check-Stock.ps1` (read-only, whole tree: every file
hashed and compared with the pinned backup, write times included) returned `STOCK_VERIFIED: 121 files / 8 directories
/ 7726223158 bytes; hashes and write times match the pinned backup`, stderr **0 bytes**
(`artifacts/stock-check.sa3.log`). The full verifier was then re-run on that state
(`artifacts/verify.d063.{stdout.log,stderr.log,pid.txt}`): final line **`VERIFIED`**, stderr **0 bytes**, and the three
checks that cover this work report `UUVR_PAYLOAD_OK: installed=36 generated=23 scopes=10 mutations=1 stock=121 …;
payload re-derived from the pinned ZIP, no scope masks pinned stock, PRE_IMAGE_VERIFIED`,
`XR_DEVICE_PATCH_OK: stock=228452B/B9DF43EE… patched=229764B/8DAFFBEF… devices=[OpenVR,OpenXR,Oculus] changedRanges=3
appended=1312; pinned pre-image verified, stock device list empty, deterministic, minimal diff, read-back exact` and
`UUVR_SPIKE_PLAN_OK: files=265 (stage0=229, uuvr=36); roots=15; generated=23; mutations=1; stock=121`. An install half
is not finished until removal has run **and** the verifier has been re-run after it (TOOL-006), which is why both runs
above end here rather than at their verdicts.
































for M2-M4.

## SP5 / M2 — our own OpenXR client: everything except the frame loop (2026-10-07)

Pre-committed rules: `docs/playtests/SPIKE-OPENXR-HOST.md`. Client: `src/render/OpenXrProbe` (first-party .NET 10
console app, Silk.NET.OpenXR 2.23.0 + Vortice.Direct3D11 3.8.3, loader vendored from the pinned Khronos zip).
Receipts: `artifacts/openxr/probe/m2.json`, `m2.stdout.log`, `m2.stderr.log`, and the runtime's own log
`%APPDATA%\MetaXR\MetaXrSimulator\logs\meta_xrsim_<pid>.log`.

**Measured working, in one run (all from the receipt and the runtime log):**
- loader `openxr_loader.dll` 1.1.63 (SHA256 `66946EA6E9CC2649…`) loaded by absolute path and pinned as Silk.NET's
  DllImport target; `XR_KHR_D3D11_enable` enabled at instance creation;
- `RuntimeName=Meta XR Simulator`, `RuntimeVersion` 207.0.0; system `Meta Quest 3`, vendorId 10291,
  positionTracking true, maxLayers 32;
- `xrEnumerateViewConfigurationViews` -> **2 views, 1680x1760** (max 8192x8192);
- `xrGetD3D11GraphicsRequirementsKHR` -> `adapterLuid=0x00000000000170C1`, minFeatureLevel 45056; four DXGI adapters
  enumerated and the **matching** one chosen (RTX 5080, `AdapterMatchesRequirement=true`);
- our own D3D11 device created on that adapter at `Level_11_1`, and `xrCreateSession` accepted with the D3D11 binding;
- `XR_REFERENCE_SPACE_TYPE_LOCAL` created; 9 swapchain formats enumerated; two color swapchains created, each with
  **3 images**;
- the session lifecycle reached `XR_SESSION_STATE_READY` and `xrBeginSession` returned Success.

**Verdict: FAIL (blocked), frames 0/300.** The first `xrWaitFrame` never returns; the session never leaves READY, so
`xrEndFrame` is never reached and the pre-committed M2 conditions (`frames>=300`, `endFrameOk==frames`, two eyes
cleared to different colours) are 0/3. Root cause and the six attempts to move it are recorded as `docs/BUGS.md`
TOOL-011. The decisive control: `hello_xr.exe -g D3D11` from the same pinned zip, same runtime, same GUI, same config,
advances READY -> SYNCHRONIZED -> VISIBLE -> FOCUSED in 50 ms and submits frames - so the test bed works and the
remaining difference is in our client's setup.

**What this does establish** (and it is more than M1): the pinned loader can be vendored and driven from our own code,
the runtime hands us the HMD system, the two eyes and the exact adapter it wants, a D3D11 device on that adapter is
accepted for a session, and runtime-owned swapchain images can be enumerated. M3 (photograph the composed eyes) and
M4 (the game's camera inside them) are unmeasured, and no headset time was used, as the pre-committed rules require.

























## SP5 / M2 — the probe's `--extensions` switch, and what VDXR actually offers (2026-10-07)

Added while preparing the headset sitting (D-066), so that headset time is never spent on a switch the runtime does not
support: `--extensions` enumerates instance extension properties and records them in the receipt. It needs no system,
so it runs with no headset attached.

Measured against VDXR 1.0.10 (`artifacts/openxr/probe/vdxr-extensions.json`), **31 instance extensions**:
`XR_KHR_D3D11_enable`, `XR_KHR_D3D12_enable`, `XR_KHR_vulkan_enable`, `XR_KHR_vulkan_enable2`,
`XR_KHR_opengl_enable`, `XR_KHR_composition_layer_depth`, `XR_KHR_win32_convert_performance_counter_time`,
`XR_KHR_convert_timespec_time`, `XR_EXT_win32_appcontainer_compatible`, `XR_KHR_visibility_mask`,
`XR_FB_display_refresh_rate`, `XR_EXT_palm_pose`, `XR_EXT_active_action_set_priority`, `XR_OCULUS_audio_device_guid`,
`XR_KHR_composition_layer_cylinder`, `XR_EXT_hand_tracking`, `XR_EXT_hand_tracking_data_source`,
`XR_FB_hand_tracking_aim`, `XR_EXT_eye_gaze_interaction`, `XR_FB_eye_tracking_social`, `XR_FB_face_tracking`,
`XR_FB_face_tracking2`, `XR_FB_body_tracking`, `XR_META_body_tracking_full_body`, `XR_META_body_tracking_fidelity`,
`XR_META_body_tracking_calibration`, `XR_HTCX_vive_tracker_interaction`, `XR_MND_headless`, `XR_EXT_uuid`,
`XR_META_headset_id`, `XR_EXT_debug_utils`.

Consequences, recorded before the sitting: `--user-presence` is off the ladder because `XR_EXT_user_presence` is
**absent** (requesting it would only fail instance creation); the depth extension **is** present, so `--parity` is
honourable on this runtime; and the hand, eye and body tracking extensions are present, which is a fact Stages 3+ will
need and which did not exist on the simulator.

**What this does establish** (and it is more than M1): the pinned loader can be vendored and driven from our own code,
the runtime hands us the HMD system, the two eyes and the exact adapter it wants, a D3D11 device on that adapter is
accepted for a session, and runtime-owned swapchain images can be enumerated. M3 (photograph the composed eyes) and
M4 (the game's camera inside them) are unmeasured, and no headset time was used, as the pre-committed rules require.









on a real runtime.** `xrWaitFrame` returned `Success` in 3 ms with `shouldRender=1`, `xrBeginFrame` succeeded, and the
loop then failed in our own D3D11 code - `CreateRenderTargetView failed hr=0x80070057` (E_INVALIDARG), `frames=0/5`,
five consecutive failures, `verdict=FAIL` on all three rungs (`artifacts/openxr/probe/m2-vdxr-a1..a3.run1.json`).

That failure is TOOL-012, found and fixed headset-free: the hand-rolled `D3D11Shim` took `CreateRenderTargetView`'s
vtable slot from the SDK header's C-style `ID3D11DeviceVtbl`, which omits the four inherited `ID3D11DeviceChild`
methods, so slot 9 is `CreateTexture2D` (measured: `0xC0000005` against a texture of our own). The shim is deleted; the
generated bindings now run on a `QueryInterface`-AddRef'd pointer (`ComInterop`), and `--selftest-d3d11` exercises the
whole render path headset-free: `verdict=PASS` (`artifacts/openxr/probe/selftest-d3d11.json`).

**Run 2 (tag `hd2`, ladder B1).** With that fixed the loop got further: `xrWaitFrame` Success, `xrBeginFrame` ok,
`xrLocateViews` returned 2 poses, both eyes were acquired/waited/released - and `xrEndFrame` answered
`ErrorPoseInvalid` (`frames=0/5`). The receipt also shows the session stuck at `Ready (2)`: it never reached
`Synchronized (3)` within 20 s. That is TOOL-013 - our probe submitted frames from a session that was not being
displayed, which the sample never does (it renders only in VISIBLE/FOCUSED). The sitting ended there, per the