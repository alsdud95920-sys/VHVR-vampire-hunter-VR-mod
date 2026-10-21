# TARGET PROFILE — Vampire Hunters
Date: 2026-10-06 | Mode: agentic

## 0.9.6 public package (built + published, 2026-10-22)
- `dist\VampireHuntersVR-0.9.6.zip` — 35,666,676 B, SHA256 `25FC057BB9B20FB7C1E23AF8E80C59ED9B39733A997DC5896E38E680EA8344E7`; sidecar `VampireHuntersVR-0.9.6.sha256.txt`.
- Assembled from the published 0.9.5 ZIP by `scripts\pack-release.ps1` (0.9.5 payload kept byte-identical): Stage0 440,320 B (`129361927619F42C3A625D99EB85EB04B781DF981690C104D17FB4F35BE85F2F`, = the headset-tested D-211 DLL), NativeHost 339,456 B (`E6C9BFD41E1E536087C945563F4751FC3EF2C8A7426F6F1D5438B32A9B3DC516`, unchanged), Extras 54,272 B (`B4AD024D6908D52C76FA8F054A34D40E4C0ED9780C045731EA8B3F044F2FA3C1`, unchanged). Installer defaults: `HandPoseFrame=Head`, `TurretCycle=Stick`, `SnapTurnDegrees=30`; manifest version `0.9.6`.
- Verified: 254 entries under a single `VampireHuntersVR-0.9.6/` root with forward-slash entry names; install → 237 files → uninstall round trip in a throw-away game folder (stock exe untouched, no leftovers); Stage0 hash inside the ZIP = installed hash.
- Headset: snap turn at 30° confirmed (D-211 / BUG-203 closed). 45°/off and D-202/D-206/D-207 behavior still not worn separately.

## 0.9.5-rc1 package set (confirmed hashes, 2026-10-19)
- Package 0.9.5 (binaries built from this tree) embeds the existing 0.9.4 package layout and the installed D-206/D-207 binaries: Stage0 437,248 B (`2155A791373276EA3C610FF18624FCFA1C737D31993BCD36938EAD78EE655777`), NativeHost 339,456 B (`E6C9BFD41E1E536087C945563F4751FC3EF2C8A7426F6F1D5438B32A9B3DC516`), Extras 54,272 B (`B4AD024D6908D52C76FA8F054A34D40E4C0ED9780C045731EA8B3F044F2FA3C1`). Installer defaults set `HandPoseFrame=Head` and `TurretCycle=Stick`.
- Archive structure, complete entry reads, manifest version, defaults, DLL hashes, and SHA sidecar were verified. This is not headset acceptance or a public release. D-202/D-206/D-207 headset behavior and crash recurrence remain unverified; project license is undecided and redistribution terms remain uncleared. Keep private.

## D-207 — Native hands ABI (confirmed, 2026-10-19)
- Confirmed contract in `src\\render\\NativeHost\\Host.h`: sequential `HandPose` is **64 B** (`Squeeze` at byte 60); `HandsStatus` is **144 B** (16-byte header, left hand at 16, right at 80). `src\\interaction\\HandAimDriver.cs` mirrors this layout.
- Extras `MenuScreenRig.ReadHands` consumes the same native `VhvrNativeHost.dll` output; its mirror must remain byte-for-byte ABI compatible. `ArmHaptics` reads the `Reserved` delivery counter through `MenuScreenRig.HapticAppliedCount()`.
- Confirmed faulty pre-fix Extras source had no `Squeeze`: 60 B per hand, 136 B status, right hand at 76. Thus pre-fix `Right.Active` aliased the native left-hand `Squeeze` field. Extras buffer was 8 B short for native output.
- Fixed Extras mirror is shared with pure tests and fail-fast layout checks. Test verifies size and offsets. Current build/live install: 54,272 B, SHA256 `B4AD024D6908D52C76FA8F054A34D40E4C0ED9780C045731EA8B3F044F2FA3C1`; previous live DLL preserved at `artifacts\\d207-install\\20261018T155649Z\\` with hash `52E6707290D430F25615CF50EFAA7ADA3013DD21527FB7861D999BFDDB1CA533`. This is managed/native contract evidence; it does not prove headset ray visibility or conclusively attribute the recorded CoreCLR AV.


## D-170 — eye MSAA implemented behind an off-by-default key; settings exe for resolution + MSAA
- **user direction (2026-10-13):** "게임 해상도 설정 및 MSAA on/off 만들기 및 설정을 선택할 수 있게 exe 파일로 만드는 작업". Markers/HUD untouched as instructed.
- **reading of "resolution" (stated assumption):** in VR the only resolution that sets image quality is the **per-eye render size** (`EyePreset`/`EyeWidth`/`EyeHeight`, D-147); the monitor view is a by-product and the game's flat window size is not used on the VR path. MSAA likewise only makes sense on the eye image.
- **eye MSAA (off by default; off = the accepted path, byte-for-byte):** `EyeMsaa` key (0 default / 2 / 4 / 8). 0 -> the eye `RenderTexture` keeps `antiAliasing=1`, no native resolve target exists, and `Host::Submit` copies `source[eye]` straight into the swapchain image. 2/4/8 -> the eye target is created multisampled, `SetEyeMsaa` (before `Configure`/init, like every other opt-in) declares the count, the native init **verifies the real texture description** (`eye render texture samples=N vs EyeMsaa=M` fails loud - Unity refusing MSAA can never produce a receipt that claims samples), one single-sampled resolve target per eye is created, and each `Submit` issues exactly one `ResolveSubresource` (`Gpu.cpp: ResolveMsaa`) into it before the proven copy. The copy still only ever sees a single-sampled texture whose format is the same family as the source and the same sRGB format as the swapchain, so `Gpu.cpp`'s shape/format checks and the exact-copy fixtures are unchanged.
- **cost / honest limits:** one resolve and one extra single-sampled texture per eye (2064x2208 ARGB32 ~= 18 MB/eye). fps impact and how much the edge aliasing actually improves are **not measured**; the game's own AA still does not reach our eyes (D-147 probe), so this is the only AA that can reach the submitted image.
- **settings exe:** `dist\tools\VhvrSettings.exe` (net10.0-windows, framework-dependent, 162,304 B, sha256 `F07131FC…`; `src\settings\Build.ps1 -SelfContained` for a single file). It edits exactly `EyePreset`/`EyeWidth`/`EyeHeight`/`EyeMsaa`, preserves every other key, refuses a value the runtime would clamp (512-4096, 0|2|4|8, low|normal|high|custom) instead of silently changing it, writes a `*.yyyyMMddTHHmmssZ.bak` backup before an atomic temp->move replace, locates the config only next to itself or in `BepInEx\config\` (no Steam path guessing), and warns when the game is running. CLI `--config/--print/--validate/--apply/--selftest/--form-selftest` makes every claim checkable headlessly (a WinExe is not waited for by PowerShell's `&`, so scripts use `Start-Process -Wait`; `--form-selftest` really opens the window for 400 ms and reports what it loaded, so the form itself is covered).
- **offline evidence:** native selftest **2 real hardware MSAA resolve cases** (RGBA/BGRA typeless Count=4 -> resolve -> exact Y-flip copy -> byte check) + **6 refusals**; fault fixtures 52 -> **55** (`eye-msaa-opt-in-contract`, `eye-msaa-off-keeps-raw-source-copy`, `eye-msaa-resolve-precedes-copy`); HandAim 317 -> **319**; Settings 18 -> **26**; new `tests\settings-tool.Tests.ps1` (publish + round trip on a copy of a shipped config + refusal + backup) wired into the verifier.
- **not yet:** **no headset acceptance.** One sitting with `-EyeMsaa 4` is the recommended judgement (or an off/4x A/B); `dist\preview` does not carry the exe or the new key yet.

## D-147 — opt-in raw dump + per-eye resolution preset; MSAA needs native work
- **P1-3 (user decision):** the frame-30 eye/source raw dump stays available but is **opt-in**. New key `CaptureFirstFrames` (default `false`) is passed to the native host through a new `Configure(...,int frameDump)` argument, where the save is now `if(frameDump&&status.frames==30) savePair(output);`. Harness-requested captures (`capture.request`) are unaffected. Distribution default = raw 0 MB per sitting (was 73.9 MB measured in d145a).
- **Resolution (agent, user direction):** `EyeWidth`/`EyeHeight` are **per eye** (2064x2208 default; both eyes 4128x2208; each raw file 18,229,248 B = 2064x2208x4). The user-suggested 3840x2160 / 3072x1728 / 4800x2700 are 16:9 flat/combined numbers, so they were NOT used as absolute per-eye sizes: that would change the eye aspect from 0.935 to 1.778 and the compositor would stretch the image. New pure policy `src\render\RenderResolutionPolicy.cs` + key `EyePreset`: `low` 0.8x (1651x1766), `normal` 1.0x (2064x2208, default = unchanged behaviour), `high` 1.2x (2477x2650), `custom` (use `EyeWidth`/`EyeHeight` as given), all clamped to 512-4096. Effective size is logged (`RESOLUTION preset=... base=... effective=... pair=...`) and recorded in the receipt (`eyePreset`, `eyeWidth`, `eyeHeight`).
- **Anisotropic filtering** already exists as `Anisotropic` (D-104, default off). Honest limit: it affects how the game's own textures are sampled; our eye textures are 1:1 non-mipmapped GPU copies, so the option does not re-sample the eye image.
- **Measured quality settings (6 real headset runs, d106a-d141a):** every `[OPENXR_HOST] QUALITY` line reads `aniso=ForceEnable msaa=0 lodBias=1.5 eyeMsaa=1` - the game already forces anisotropic filtering (so the D-104 `Anisotropic` option is effectively a no-op in this game, harmless), the game's own AA is off, and our eye targets are created with `antiAliasing=1` (no MSAA). The game runs URP (`UniversalRenderPipelineAsset` in its own assembly recon), but our eye cameras are new GameObjects cloned with `Camera.CopyFrom`, so URP per-camera AA/post data (`UniversalAdditionalCameraData`) is NOT copied - whether the game's AA reaches our eyes is unverified and must be measured, not assumed.
- **MSAA (open decision, TOOL-080):** `Gpu.cpp:12` rejects `SampleDesc.Count!=1` and the swapchain is created with `sampleCount=1`, so raising Unity's `RenderTexture.antiAliasing` alone fails the copy. A native resolve path (per-eye resolve target + `ResolveSubresource`, ~30 lines, +18 MB/eye) is required; proposed as opt-in and not yet implemented. -> **implemented as `EyeMsaa` (D-170), off by default, NOT headset accepted.**
- **D-147b (measurement first):** `Initialize()` logs and records a read-only `QUALITY_AA` probe - `GraphicsSettings.currentRenderPipeline` type name, a reflection probe of the pipeline asset (`msaaSampleCount`/`supportsHDR`/`antialiasing`/`postProcessingEnabled`, reported as `absent`/`error` when unreadable rather than guessed), `QualitySettings.antiAliasing`, the eye render target MSAA, `allowMSAA` on the source and eye cameras, and the component name lists of the source camera vs our cloned eye camera, so "does URP per-camera AA reach our eyes" is measured rather than assumed. Frame cost is attributed the same way: one `[OPENXR_HOST] PERF` line and one receipt `perf` object per 45 submitted frames (update/late/prepare/save ms, save bytes, Unity delta time, managed heap, GC, evidence count). The native link now uses `/link /Brepro` (TOOL-079 FIXED: identical source yields `42C6D7FF...` across rebuilds, including the rebuild inside `tests\NativeHost.Tests.ps1`).

## D-146 — d145a headset sitting measured; a stage end is no longer a failure; receipts reset per sitting
- **headset evidence (user-witnessed, tag d145a, 2026-10-11 23:46 -> 2026-10-12 00:05 KST = 1,132 s)**: an unlimited sitting (`HeadsetSeconds 0`) ran to the end of a stage with no wall-time close; `runtime json from registry=C:\Program Files\Virtual Desktop Streamer\OpenXR\virtualdesktop-openxr.json` and `runtime=VirtualDesktopXR` prove the D-145 runtime auto-detect on real hardware; submitted=36,320 frames (gpuCompleted=72,640), both hands driven (127,963 / 130,928 frames), fire 2,861 (1,384 left / 1,477 right), haptic pulses 49, UI clicks 49, moves 21,262, reloads 4. User: ballistics and flame direction work.
- **defect found and fixed (D-146a)**: the normal stage end was recorded as `FAIL` in `host.json.failure.json` (`gameplay camera/scene ownership lost; no menu fallback; failed=source-destroyed,player-destroyed,scene:Egypt_Map->MainMenu_2`). A finished stage now closes orderly through `StageEnded()`/`EndGameplay()` with verdict `HEADSET_GAMEPLAY_LEFT_NOT_ACCEPTED` and no failure receipt; the soft gate (never "accepted") is untouched.
- **D-146b**: every receipt carries `endReason` (running / waiting-for-gameplay / stop-request / level-exit / session-ended / watchdog / error), and every sitting starts clean - `ResetRunFolder()` archives the previous receipts and `*.raw` into `previous\` and removes a left-over `stop.request` that would otherwise stop the new sitting at once.
- **build**: plugin `CF875990BAECA921C3B1165AF13FCE439AAC380CBFB3E30A0FFD36000CD3496C` (292,352 B) replaces `50204473…`; native host and loader unchanged (`6162A025…`, `66946EA6…`). Offline: HandAim 200/200 (3 new D-146 guards) and the full verifier green; **no headset sitting has run on this build** (`NOT_HEADSET_VERIFIED_D-146`).
- **cleanup (P3-9)**: stale `artifacts\transactions\current.txt` (named d141a) and `%TEMP%\VampireHuntersVR Diagnostics` removed; the 18 transaction workspaces and the pinned stock backup were left alone.
- **found while re-verifying (TOOL-079)**: `tests\NativeHost.Tests.ps1` rebuilds `bin\VhvrNativeHost.dll` on every run and `Build.ps1`'s raw `cl /link` has no `/Brepro`, so identical source hashed `6162A025…` -> `16FE0392…` -> `DCB39715…` -> `B936B76B…` within one session (all 326,656 B). A pinned native sha256 is only valid until the next build; the proposed fix is the one word `/link /Brepro`.
- **open, deferred to review**: the per-sitting 73.9 MB `*.raw` dump at `Host.cpp:147` (wants an opt-in flag), frame cost growing from ~20 ms to 35-37 ms over 18 minutes, menu re-entry (no VR in menus), and repacking the preview zip as 0.3 plus one more headset sitting.

## D-145 — unlimited session + auto OpenXR runtime + diagnostics removed
## Game dossier and coverage
Official Steam page confirms first-person roguelite, unique hunter abilities, upgrades, stacking up to 14 weapons, revolver and holy-water hose examples, single-player, Steam Cloud, achievements and leaderboards; 11 interface languages including Korean.
- Standard stages cover head tracking, stereo, movement, hand aiming, HUD/menus and calibration. Optional comfort/turning scope is customized by D-013: snap turn only; exclusions below are user-selected, not engine limitations.
- Stage 6 needs a **14-slot weapon-bank design** preserving simultaneous firing and upgrades; do not silently reduce to two equipped guns. Proposed grouping by dominant/off-hand banks requires user approval and runtime evidence.
- **confirmed (user report, 2026-10-06)** Double jump, dash, Q/E auxiliary weapons, a frenzy-like blood ability, primary fire and secondary fire. Preserve each separately in Stages 1/3/4/6/8; auxiliary weapon use is not presumed identical to secondary fire. Other mechanics/reload/auto-fire/camera transitions remain investigation items, not absent.
- Actual reload, ammo, auto-fire, recoil, projectile origins, abilities and all weapon classes need discovery; no manual-reload assumption.
- Stage 1/4 must inventory death, level transitions, camera shakes, dash/jump and all scripted movement. Vehicles/swimming/climbing presence unknown, not declared absent.
## D-127 TOOL-071 rotation trace built — 2026-10-11 (read-only, verified)
- User: the left-weapon spin is a **regression** (absent in d114a) and the rotation trace was **approved**.
- New read-only sampler `src/interaction/HandPoseTrace.cs` + hook `src/interaction/HandAimDriverPoseTrace.cs` (both compiled into the plugin): per frame it reads container / weapon prefab / muzzle / aim proxy / camera forward+up, `weaponVsContainerDeg`, `muzzleVsAimDeg`, and `containerVsWrittenDeg` (game movement after our write). Logs `[POSE_TRACE]` on a >30° axis swing or a >15° up-axis roll with a <5° forward swing (capped), a 5 s `[POSE_TRACE_SUM]`, and a `poseTrace` receipt block per hand.
- Guarded by test: `source-pose-trace-writes-nothing` (no `SetPositionAndRotation`, `.rotation =`, `.Play(`, `SetParent`, `new GameObject`, `Instantiate`), `source-pose-trace-reads-rotation-and-aim`, `source-pose-trace-hooked-after-both-hands`; `verify.ps1` fixture count 25→28.
- Verified: build 0 warnings, plugin `D3D6E972…` (deterministic), all fixtures EXIT=0 and `VERIFIED:` with 0-byte stderr; native host untouched; game tree still stock.
- Pending: phase 1 install + one sitting (`d127a`) reproducing the spin; TOOL-071 stays OPEN and no pose writer changed.

## D-126 TOOL-071 triage — 2026-10-11 (left-weapon visual spin, not from D-123)
- User: d123a ended by death (game over → menu) = user-side; new symptom filmed (`왼손 사격시 회전.mp4`, 2.00 s): left weapon model sweeps/rotates while the world is static, attacks still land.
- Clip read via a new WPF frame-grab devtool (`artifacts/devtools/Grab-VideoFrames.ps1`; no ffmpeg on this machine) and log analysis via `artifacts/devtools/Analyze-PoseSwings.ps1`: 26/26 shot scopes keep muzzle-vs-aim constant across all four phases (0–15.6°) and the flame transform's forward is byte-identical across phases → the transaction is clean and the fire origin is on the aim ray.
- Attribution: **not D-123** (read-only sampler, guard-enforced; the rotation-accumulating `HandAimDriver.cs:210` is D-095 from 2026-10-10). Suspect: per-frame tug-of-war between the driver writes, the game's container overwrites (5575/11154) and `BeginShotScope` forcing the camera + shot roots to the aim pose.
- Nothing changed; TOOL-071 OPEN; next is an approval-gated ~10 Hz read-only rotation trace.

## D-125 d123a sitting read out — 2026-10-11 (HOST_FAILED, flame mechanism measured)
- Install-first worked: guards passed, headset already attached, **0 s install wait**, GAMEPLAY LOCKED 12:25:26, ~102 s played in `Egypt_Map`, then `HOST_FAILED` 12:27:07 (`scene:Egypt_Map->MainMenu_2`, no menu fallback) and **stock verified restored** (121 files/8 dirs/7,726,223,158 bytes).
- Flame = single ParticleSystem `id=-349022` on weapon `-349004`, `lifetimeMax=0.30 s`, `active` always true; `isVfxPlaying`/`ps.playing`/`particles` fall to False/False/0 while `alreadyFiring=True`; cumulative `vfxPlayToggles=11`, `vfxCountResets=7` (2 s sampling ⇒ lower bounds). `[FLAME_POOL]` 0/0/0 in all 51 samples ⇒ `Flames`/`Flames_Projectile_V2` are not the source.
- Trigger cadence: fire/release paired (left 9/9, right 21/21) under held fire → leading mechanism for symptom C via the game's `stop_shooting()`. `timingMedianFps=37.3`; 26 clean shot-scope transactions; muzzle angle ≤0.028°; both hands in the correct container.
- Unresolved: why the game returned to the main menu (user report needed). TOOL-070 stays OPEN; no fix authorized.

## D-124 install-first sitting (no headset wait) — 2026-10-11 (verified)
- `tests/headset-handaim.ps1` gains `-UsePreinstalled`: it skips only the install step for a tag whose `transaction\journal.jsonl` + `config.json` already exist, after failing closed on two checks — installed `VampireHuntersVR.Stage0.dll`/`VhvrNativeHost.dll` must hash-equal the current builds, and every config key must equal the pre-installed `config.json`. It still uninstalls and stock-checks in `finally`.
- Phase 1 (no headset): `headset-handaim.ps1 -Tag d123a -DisableVSync -DryRun` then `Install-Stage0.ps1 -Action Install -Workspace artifacts\headset-d087\d123a\transaction -OpenXrHostConfig artifacts\headset-d087\d123a\config.json`. Phase 2 (user ready): `headset-handaim.ps1 -Tag d123a -DisableVSync -UsePreinstalled`. Undo anytime: `Install-Stage0.ps1 -Action Uninstall`.
- The game tree is modified between the phases (do not launch it by hand; a manual launch is outside any monitored run). Verified artifact: plugin sha256 `9056036E…`, native `CC593A69…`; verified by parse check + negative guards + HandAim 195/195 + full verifier 30/30 exit 0 / 0-byte stderr.

## D-123 diagnostics-only flame FX instrumentation — 2026-10-11 (verified)







## Intake
User selected existing-tool reuse plus game-specific development; no UUVR GPL vendoring approved. Hard gates for Stages 0–4 explicitly confirmed (Q-011/D-017): automatic checks plus actual stage behavior/required human feedback before next-stage implementation. Stage 0 NOT PASSED; no install/launch/private saves consent inferred. Primary standing/right-handed preference confirmed; seated support and both handedness remain planned.
- **confirmed (user preference, 2026-10-06)** Primarily standing, right-handed. Initial posture/dominant-hand target only; no measured height/offset or implemented control mapping inferred. Seated support/handedness switching retained; Q-012 resolved, D-016.
- **confirmed (user report, 2026-10-06)** No coding knowledge; experienced VR player. Agent handles technical development within consent, using plain technical explanations without beginner VR-play coaching.
- **confirmed (user report, 2026-10-06)** Game, headset connection and development are on the same PC. Q-009 same-machine requirement resolved; test-session duration unanswered, not inferred from VR experience.
- **confirmed scope (2026-10-06)** User answered "스냅 턴만 만들고 나머지는 모두 필요없어." Turning scope: snap turn only, no smooth/continuous turn. Exclude vignette/tunneling, teleport and optional locomotion anti-nausea fades from implementation, not merely default them off. Ordinary stick locomotion remains in the plan; snap angle/repeat behavior will be tuned at Stage 4, no numeric default selected now. Q-015 resolved; D-013 supersedes D-012's pending scope.
- **retained core scope** Correct head tracking/stereo, camera ownership, collision/play-area boundary behavior, required transition handling, calibration, usable UI/settings and safe-mode/config validation remain. Physical head/body movement is not excluded by the snap-only controller-turn choice. No evidence/gate or existing installation/launch/private-data consent boundary is relaxed. This is a documented customized Phase 1 scope, not fulfillment of the unmodified Wizard all-comfort checklist. No movement/turning code or config implemented.

No in-game hook, stereo, head tracking or motion control is confirmed.

## Verified backup operation — 2026-10-06
Q-014/D-018 permit narrow install/game-save inventory and copy/hash. Snapshot: `<RepoRoot>\backups\20261006T104307Z-cdd080e1e50a4fda845ffa7c01158173`. Install 121 files/8 directories/7,726,223,158 bytes; save folder 6 files/51,618 bytes. All source/snapshot/isolated restore SHA256 and path inventories matched; original timestamps/hashes unchanged. Completed 10:43:44 UTC, backup script exit marker 0, independent game/save re-hash readback passed. Snapshot manifest SHA256 828B45CCC6D82AF32E2E4636B63D718E55FC188AFB5245F1B8DCC39A8F593A6F; local evidence `<RepoRoot>\artifacts\backup-result.json`. Backups ignored/uncommitted; `.sav`/`.head`, two logs and cloud VDF copied as bytes without payload interpretation. F free after backup 842,201,567,232 bytes; ~14.39 GiB includes both snapshot and retained full isolated restore copy. Same-drive mod-change recovery only, no disk-failure guarantee. No stock restore/game launch/install/settings changes/Cloud changes or Stage 0 pass. Runtime persistentDataPath/Cloud pairing unverified; no claim of account-wide cloud backup.

## Autonomous entry preparation — 2026-10-06
- **confirmed** User delegates routine research/build/tests/commits (D-020), retaining hard gates and prior explicit scope boundaries.
- **confirmed** Installed appmanifest 2206270/build 22267731, TargetBuildID 22267731, StateFlags 4, AutoUpdateBehavior 0. Live third-party public appinfo matches build/depot, exposes public only and privatebranches=1; available private/UI historic branches unknown. No Steam settings changed/version pinned.
- **confirmed** Immutable supported-build file and read-only planner check exact core bytes plus all 121 stock file hashes/path/directory/timestamps against existing backup, all 228 extracted loader files against pinned ZIP and diagnostic DLL/target-root conflicts. Proposed 229-file plan saved locally, not deployed; no save access or extra backup needed.
- **confirmed historical developer statements** Mod support not in near-term plans (2024/2025); best score reaches leaderboards and off-mode only suggested in 2024. **unknown** Current supported off-mode/deferral behavior, applicable game license and protected execution. No anti-cheat-free or permission verdict from missing filename markers. Policy sources/quotes, reversible design and unsent inquiry in `<RepoRoot>\docs\ENTRY_PLAN.md`.
- **unknown** Runtime loader/interop compatibility, visible Stage 0 label, config reload/safe mode and stock relaunch. Exact-build guard is not version retention; policy/retention/consent holds stay open, no hook confirmed.
- **confirmed automatic verification** Entry 21/21, backup 12/12, recon 23/23; fresh full verifier marker 0/final VERIFIED, nine expected child exits, both builds zero warnings/errors. Read-only 229-file plan completion 2026-10-06T11:01:58.7938014Z. Eight test-script ASTs and Git diff checks pass; backup/artifacts ignored. Initial cancelled/stalled verifier is not pass evidence; mitigation and records in BUGS/VERIFICATION. No loader/game/XR execution, save reading/new snapshot, settings changes or gate pass.

## Coding continuation — 2026-10-06
- **confirmed automatic tooling verification** Same-volume staged/root-move transaction engine, append-only flushed intent journal, explicit stock-preserving quarantine/replay and same-session tool mutex; **27/27** including fixture-child termination/recovery. No actual target deployment entry point or invocation. Settings **18/18**, recon **23/23**, backup **12/12**, entry **21/21**; fresh full exit 0/11 named checks with exact suite identities. Prior wrapper false-positive shared apphost corrected via separate outputs; BUGS/VERIFICATION record attempts.
- **confirmed implementation, runtime unknown** Two-flag strict-UTF8/bounded cfg snapshots, true defaults for invalid/missing/duplicate entries, content fingerprint and `.cfg.safe-mode` marker; typed ConfigEntry apply with automatic save disabled. No Unity/game objects touched on timer thread. Existing settings bytes preserved by pure layer; first oversize fixture failure/correction recorded in BUGS.
- **unknown** Visible label API/Unity generated interop, loader first-run writes, actual cfg timer/lifecycle and real recovery. BepInEx AddComponent API observed in pinned source, but no generated Unity references present in loader core; do not invent Unity signatures or claim screen diagnostic. No camera/render/input/Stage 1–4 code.
- **limitations** Trusted local journal is not authenticated; same-session tool mutex does not lock Steam/external launch, same-volume moves/flush do not guarantee power-loss durability. BasePlugin constructor config read occurs before this validation layer; malformed startup safety/actual XR-failure fallback remain unproven. Marker controls only effective diagnostic safe mode, not proxy/protection bypass.
- **confirmed final read-only plan** Completion 2026-10-06T11:16:44.6616735Z, 121 stock files/hash/timestamps match original snapshot, pinned 228 loader + new DLL=229 proposed additions; not deployed. DLL SHA256 7D705CED852B0093996BB11078607AF243EFB9A70674B7E6B32BAE0DEAD01695; all 10 PowerShell ASTs/diff checks pass, generated evidence ignored. No save/new backup access or settings changes.

## Initial static observations (2026-10-06; historical counts, latest below)
- **confirmed** Actual read-only recon completed with exit 0 after explicit partial-coverage handling: 97,424 nonempty identifier segments, 2,962 distinct keyword candidates, 46 undecodable UTF-8 segments recorded with offsets/hex.








































- **confirmed final read-only plan** Completion 2026-10-06T11:16:44.6616735Z, 121 stock files/hash/timestamps match original snapshot, pinned 228 loader + new DLL=229 proposed additions; not deployed. DLL SHA256 7D705CED852B0093996BB11078607AF243EFB9A70674B7E6B32BAE0DEAD01695; all 10 PowerShell ASTs/diff checks pass, generated evidence ignored. No save/new backup access or settings changes.

## Initial static observations (2026-10-06; historical counts, latest below)
- **confirmed** Actual read-only recon completed with exit 0 after explicit partial-coverage handling: 97,424 nonempty identifier segments, 2,962 distinct keyword candidates, 46 undecodable UTF-8 segments recorded with offsets/hex.
- **confirmed** Identifier table offset 633560, bytes 1770628. Report: `<RepoRoot>\docs\playtests\reports\static-stage0\static-recon.json`.
- **confirmed** Report labels coverage PARTIAL_UNDECODABLE_NAMES, VR NOT_VERIFIED, hooks NOT_ATTEMPTED, actual graphics API NOT_INSPECTED.
- **confirmed** 18 parser/PE tests pass with exit 0; positive/negative fixtures and raw undecodable evidence included.
- **confirmed** Entry diagnostic DLL builds net6.0 with zero warnings/errors against unmodified BepInEx be.788. Not installed, no Unity interop generated, no visible in-process response.
- **confirmed** Pinned BepInEx LGPL-2.1-or-later license checked at 5b766a3, notices in README; downloaded only in project tools. `docs/DEPENDENCIES.json` records source/ZIP hash.
- **confirmed** Full verifier exit 0: unit/config checks, both builds, real partial recon, CLI missing-argument exit 2 and forbidden-output exit 1 as expected. Four core/metadata hashes rechecked unchanged; no loader directory installed. PowerShell AST checks passed.
- **confirmed** Candidate text includes `PlayerController`, `PlayerActions`, `CameraShake`, `get_muzzle_position` and muzzle VFX names. These are candidate strings only, not mapped classes/methods or working hooks.
- **assumed** Runtime camera/input/renderer mapping remains unknown. Static method-looking names do not prove callable ownership/signatures.

## Fresh mechanics evidence — 2026-10-06
- **confirmed (static file contents only)** Installed data directory is `<GameRoot>\Vampire Hunters_Data` (spaces matter). Boot config build GUID ebd102e1bdba42dab63b0078a52de6e2. RuntimeInitializeOnLoads lists GI.game_init, Cinemachine initialization and InputSystem initializers; none executed by this investigation, active pipeline/XR still unverified.
- **confirmed** Expanded bounded reader: 97,424 nonempty segments, 3,920 distinct keyword candidates, 46 undecodable; first string occurrence/UTF-8 byte length retained. Increase from 2,962 is expanded filtering, not a changed game. Candidate input not truncated (cap 10,000); grouped samples capped at 64 with explicit flags. Groups overlap and include unrelated framework/generated names (e.g. dash punctuation, Average matching rage).
- **confirmed static search buckets, not mechanic counts** Jump 32, dash 41, auxiliary weapons 38, blood ability 508, primary fire 133, secondary fire 95, camera effects 79. Last four sample lists truncated. Raw reports local at `<RepoRoot>\docs\playtests\reports\static-stage0\mechanics-recon.json` and static-recon.json; no assets/binaries redistributed.

| Candidate string | Identifier offset | Absolute metadata file offset | Interpretation |
|---|---:|---:|---|
| double_jump | 255677 | 889237 | Movement search candidate |
| activate_dash | 285639 | 919199 | Dash search candidate |
| FOV_TO_ADD_ON_DASH | 288803 | 922363 | Possible dash FOV effect; camera test required |
| SecondaryWeapon | 407562 | 1041122 | Auxiliary weapon candidate; Q/E routing unknown |
| fire_secondary_shot | 285145 | 918705 | Secondary-fire candidate, ownership/signature unknown |
| BloodyRushInput | 320632 | 954192 | Blood-ability input candidate |
| BLOOD_ABILITY_DURATION | 381270 | 1014830 | Ability duration candidate, actual value/effect unknown |
| CameraShake | 234410 | 867970 | Camera-effect candidate |

These offsets locate identifier text only, **not function addresses, verified class members or hooks**. Duplicated names retain their first occurrence only. User Q/E report remains the control evidence; no controller mapping or activation/effects inferred. No hook confirmed.
- **confirmed fresh automatic verification** 23/23 fixtures, full process exit 0, recon/plugin zero-warning/error builds, config/CLI/offset/truncation/hash checks passed. Four original core/metadata hashes unchanged; metadata SHA256 E8F040A9D5E24D701C3CFAB16DFC0B2564040F9C54582DF537FCB4513C796F7F. No game/loader/XR launch, stock writes, settings changes or private saves read. Stage 0 remains NOT PASSED.

## Run 4 runtime facts — 2026-10-06 (probe v4, read-only; D-039)
Log `artifacts/tx/run/run-v4-live-20261006/LogOutput.v4.log` (381,901 B, `3F6A6A2C…`), 35 samples, `probeErrors=0`,
`xrEnabled=False` on every sample, 1920x1200, Steam launch. All statements below are **observed runtime values**, not
static guesses, and none is a hook or a working mod feature.
- **Scenes**: `Intro` -> `MainMenu_2` -> `Maze_Small` -> `MainMenu_2` (t=0.2 / 7.4 / 37.6 / 322.5).
- **Cameras**: gameplay `Main Camera` (perspective, fov 70, tag MainCamera, mask 59768831, `stereoTarget=Both`,
  no render texture) parented directly to `Prefab_Player_Level1`; during the intro it instead sits under
  `.../Hunter_For_Intro/Player_Anim_Armature/Root/CameraControler/Main Camera` (fov 60) and that `CameraControler`
- **unknown** Visible label API/Unity generated interop, loader first-run writes, actual cfg timer/lifecycle and real recovery. BepInEx AddComponent API observed in pinned source, but no generated Unity references present in loader core; do not invent Unity signatures or claim screen diagnostic. No camera/render/input/Stage 1–4 code.
- **limitations** Trusted local journal is not authenticated; same-session tool mutex does not lock Steam/external launch, same-volume moves/flush do not guarantee power-loss durability. BasePlugin constructor config read occurs before this validation layer; malformed startup safety/actual XR-failure fallback remain unproven. Marker controls only effective diagnostic safe mode, not proxy/protection bypass.

## Stage 1 preparation — 2026-10-07 (D-046 → D-048)
- **confirmed automatic tooling verification** Read-only assembly-shape reader with 31 recon fixtures and re-derived camera/aim pins (D-046); pinned UUVR IL2CPP artefact and frozen spike expected file set (D-047); transaction engine now handles scoped roots, declared generated files and one declared stock mutation that recovery restores byte-for-byte with its write time (D-048). Fixture suites: transaction **32/32**, entry **28/28**, backup 12/12, settings 18/18, recon **31/31**; full verifier `VERIFIED` with empty stderr (`artifacts/full-verifier.d047a.*`, `artifacts/full-verifier.d048.*`), plus a new `uuvr-payload-check` that re-derives the payload from the release ZIP and proves no scope masks pinned stock.
- **confirmed read-only target state** Game folder still stock (121 files / 8 directories, hashes and times equal to the pinned backup, 0 loader/injector references); read-only entry preflight prints both the loader plan (228+1 = 229) and the UUVR spike plan (`installed=36; generated=23; scopes=10; mutations=1; no existing scope, no merge`).
- **unknown** Everything runtime about UUVR: whether v0.3.1's legacy-generation IL2CPP build loads under this project's be.788 loader, engages XR, produces stereo, survives the menu/level-up/death states, or leaves the flat game playable with VR off. No hook is confirmed; the camera writer stays a hypothesis (`PlayerController.LateUpdate()`), and the whole spike is still unrun.

- **confirmed final read-only plan** Completion 2026-10-06T11:16:44.6616735Z, 121 stock files/hash/timestamps match original snapshot, pinned 228 loader + new DLL=229 proposed additions; not deployed. DLL SHA256 7D705CED852B0093996BB11078607AF243EFB9A70674B7E6B32BAE0DEAD01695; all 10 PowerShell ASTs/diff checks pass, generated evidence ignored. No save/new backup access or settings changes.

## Initial static observations (2026-10-06; historical counts, latest below)
- **confirmed** Actual read-only recon completed with exit 0 after explicit partial-coverage handling: 97,424 nonempty identifier segments, 2,962 distinct keyword candidates, 46 undecodable UTF-8 segments recorded with offsets/hex.
- **confirmed** Identifier table offset 633560, bytes 1770628. Report: `<RepoRoot>\docs\playtests\reports\static-stage0\static-recon.json`.
- **confirmed** Report labels coverage PARTIAL_UNDECODABLE_NAMES, VR NOT_VERIFIED, hooks NOT_ATTEMPTED, actual graphics API NOT_INSPECTED.
- **confirmed** 18 parser/PE tests pass with exit 0; positive/negative fixtures and raw undecodable evidence included.
- **confirmed** Entry diagnostic DLL builds net6.0 with zero warnings/errors against unmodified BepInEx be.788. Not installed, no Unity interop generated, no visible in-process response.
- **confirmed** Pinned BepInEx LGPL-2.1-or-later license checked at 5b766a3, notices in README; downloaded only in project tools. `docs/DEPENDENCIES.json` records source/ZIP hash.
- **confirmed** Full verifier exit 0: unit/config checks, both builds, real partial recon, CLI missing-argument exit 2 and forbidden-output exit 1 as expected. Four core/metadata hashes rechecked unchanged; no loader directory installed. PowerShell AST checks passed.
- **confirmed** Candidate text includes `PlayerController`, `PlayerActions`, `CameraShake`, `get_muzzle_position` and muzzle VFX names. These are candidate strings only, not mapped classes/methods or working hooks.
- **assumed** Runtime camera/input/renderer mapping remains unknown. Static method-looking names do not prove callable ownership/signatures.

## Fresh mechanics evidence — 2026-10-06
- **confirmed (static file contents only)** Installed data directory is `<GameRoot>\Vampire Hunters_Data` (spaces matter). Boot config build GUID ebd102e1bdba42dab63b0078a52de6e2. RuntimeInitializeOnLoads lists GI.game_init, Cinemachine initialization and InputSystem initializers; none executed by this investigation, active pipeline/XR still unverified.
- **confirmed** Expanded bounded reader: 97,424 nonempty segments, 3,920 distinct keyword candidates, 46 undecodable; first string occurrence/UTF-8 byte length retained. Increase from 2,962 is expanded filtering, not a changed game. Candidate input not truncated (cap 10,000); grouped samples capped at 64 with explicit flags. Groups overlap and include unrelated framework/generated names (e.g. dash punctuation, Average matching rage).
- **confirmed static search buckets, not mechanic counts** Jump 32, dash 41, auxiliary weapons 38, blood ability 508, primary fire 133, secondary fire 95, camera effects 79. Last four sample lists truncated. Raw reports local at `<RepoRoot>\docs\playtests\reports\static-stage0\mechanics-recon.json` and static-recon.json; no assets/binaries redistributed.

| Candidate string | Identifier offset | Absolute metadata file offset | Interpretation |
|---|---:|---:|---|
| double_jump | 255677 | 889237 | Movement search candidate |
| activate_dash | 285639 | 919199 | Dash search candidate |
| FOV_TO_ADD_ON_DASH | 288803 | 922363 | Possible dash FOV effect; camera test required |
| SecondaryWeapon | 407562 | 1041122 | Auxiliary weapon candidate; Q/E routing unknown |
| fire_secondary_shot | 285145 | 918705 | Secondary-fire candidate, ownership/signature unknown |
| BloodyRushInput | 320632 | 954192 | Blood-ability input candidate |
| BLOOD_ABILITY_DURATION | 381270 | 1014830 | Ability duration candidate, actual value/effect unknown |
| CameraShake | 234410 | 867970 | Camera-effect candidate |

These offsets locate identifier text only, **not function addresses, verified class members or hooks**. Duplicated names retain their first occurrence only. User Q/E report remains the control evidence; no controller mapping or activation/effects inferred. No hook confirmed.
- **confirmed fresh automatic verification** 23/23 fixtures, full process exit 0, recon/plugin zero-warning/error builds, config/CLI/offset/truncation/hash checks passed. Four original core/metadata hashes unchanged; metadata SHA256 E8F040A9D5E24D701C3CFAB16DFC0B2564040F9C54582DF537FCB4513C796F7F. No game/loader/XR launch, stock writes, settings changes or private saves read. Stage 0 remains NOT PASSED.

## Run 4 runtime facts — 2026-10-06 (probe v4, read-only; D-039)
Log `artifacts/tx/run/run-v4-live-20261006/LogOutput.v4.log` (381,901 B, `3F6A6A2C…`), 35 samples, `probeErrors=0`,
`xrEnabled=False` on every sample, 1920x1200, Steam launch. All statements below are **observed runtime values**, not
static guesses, and none is a hook or a working mod feature.
- **Scenes**: `Intro` -> `MainMenu_2` -> `Maze_Small` -> `MainMenu_2` (t=0.2 / 7.4 / 37.6 / 322.5).
- **Cameras**: gameplay `Main Camera` (perspective, fov 70, tag MainCamera, mask 59768831, `stereoTarget=Both`,
  no render texture) parented directly to `Prefab_Player_Level1`; during the intro it instead sits under
  `.../Hunter_For_Intro/Player_Anim_Armature/Root/CameraControler/Main Camera` (fov 60) and that `CameraControler`
  node carries **no** components. Menu camera `Camera Things/ArenaCamera` (fov 40, mask -1) carries
  `UniversalAdditionalCameraData` + `SetCameraTransform`. `Mini Map Camera` is orthographic with
  `targetTex=RenderTextureMiniMap`, tag MiniMap, mask 1048576, 5 units above the player root, yaw following the main
  camera - it must never be stereoized.
- **Camera writer (pinned statically 2026-10-07, D-046; runtime confirmation pending)**: the gameplay camera GameObject
  carries only `UniversalAdditionalCameraData`; its parent `Prefab_Player_Level1` carries `PlayerController`,
  `BobAnimation` x2, `ShakeAnimation`, `Tutorial`, `UnityEngine.InputSystem.PlayerInput`, `PlayerInputController`.
  The read-only assembly-shape reader shows `PlayerController` owns that whole pipeline (`camera_transform`,
  `fp_camera : Camera`, `camera_parent`, `camera_start_position/rotation`, `current_mouse_look`,
  `mouse_look_min/max_y_rotation`, `mouse_sensitivity_multiplier`, `target_fov`, `play_fov_animation`/`stop_fov_animation`,
  `head_bob`/`weapon_bob : BobAnimation`, `shake_animation : ShakeAnimation`) and carries `Update()` plus `LateUpdate()`;
  the working hypothesis is that `LateUpdate()` is the write site (see `CAMERA-PIN.md`). `SimpleCameraFP` was rejected as a
  leftover asset script. The transform is rewritten in 68% of frames (`moved=31291`, `rotated=14913` of 45861) and the FOV
  in short bursts (29-30 frames per dash).
- **Look ownership**: yaw and pitch both accumulate on the `Main Camera` transform itself (`chain[0]`
  yawSum=11900 / pitchSum=1925); the player root keeps a fixed yaw (`(0,270,0)` in every gameplay sample).
- **Three stopped states (timeScale 0), all now captured**: level-up = 5 HUD canvases + `Level Up/NewNewCanvas`,
  cursor released (15 occurrences); pause = the same 5 canvases with the cursor released (ESC toggle, `ESC_DOWN`
  at t=51.1/t=52.8); death = the same 5 canvases with the cursor still `Locked/False` (t=318.0, panel inside
  `Canvas - Screens`, which does not change active state).
- **UI inventory (gameplay)**: `canvases total=153 distinctPaths=44 modes=[WorldSpace:137, ScreenSpaceOverlay:16]`,
  9 active; overlay `scaleFactor=1.11` on the HUD canvases; `Player HUD#-3`, `Canvas - ComboAndCrosshairs#-2`,
  `Canvas - HealthBarMainHUD#-2`, `Canvas - Score#-2`.
- **Player root**: 27 direct children, including `Death Volume` (the death trigger), `Level Up`,
  `Player For Anim Parent`, `Main Camera`, `Mini Map Camera`, `Hunter_For_Intro`, `Skip Cutscene HUD` and the HUD
  canvases; `CharacterController` h=3.50 r=0.50 plus a kinematic Rigidbody; peak ~12 m/s; 1202.49 m travelled over
  29 gameplay samples.
- **Aim subtree (V3-3, narrowed statically 2026-10-07, D-046)**: `Player For Anim Parent` -> `Player_For_Anim`





























  `BobAnimation` x2, `ShakeAnimation`, `Tutorial`, `UnityEngine.InputSystem.PlayerInput`, `PlayerInputController`.
  The read-only assembly-shape reader shows `PlayerController` owns that whole pipeline (`camera_transform`,
  `fp_camera : Camera`, `camera_parent`, `camera_start_position/rotation`, `current_mouse_look`,
  `mouse_look_min/max_y_rotation`, `mouse_sensitivity_multiplier`, `target_fov`, `play_fov_animation`/`stop_fov_animation`,
  `head_bob`/`weapon_bob : BobAnimation`, `shake_animation : ShakeAnimation`) and carries `Update()` plus `LateUpdate()`;
  the working hypothesis is that `LateUpdate()` is the write site (see `CAMERA-PIN.md`). `SimpleCameraFP` was rejected as a
  leftover asset script. The transform is rewritten in 68% of frames (`moved=31291`, `rotated=14913` of 45861) and the FOV
  in short bursts (29-30 frames per dash).
- **Look ownership**: yaw and pitch both accumulate on the `Main Camera` transform itself (`chain[0]`
  yawSum=11900 / pitchSum=1925); the player root keeps a fixed yaw (`(0,270,0)` in every gameplay sample).
- **Three stopped states (timeScale 0), all now captured**: level-up = 5 HUD canvases + `Level Up/NewNewCanvas`,
  cursor released (15 occurrences); pause = the same 5 canvases with the cursor released (ESC toggle, `ESC_DOWN`
  at t=51.1/t=52.8); death = the same 5 canvases with the cursor still `Locked/False` (t=318.0, panel inside
  `Canvas - Screens`, which does not change active state).
- **UI inventory (gameplay)**: `canvases total=153 distinctPaths=44 modes=[WorldSpace:137, ScreenSpaceOverlay:16]`,
  9 active; overlay `scaleFactor=1.11` on the HUD canvases; `Player HUD#-3`, `Canvas - ComboAndCrosshairs#-2`,
  `Canvas - HealthBarMainHUD#-2`, `Canvas - Score#-2`.
- **Player root**: 27 direct children, including `Death Volume` (the death trigger), `Level Up`,
  `Player For Anim Parent`, `Main Camera`, `Mini Map Camera`, `Hunter_For_Intro`, `Skip Cutscene HUD` and the HUD
  canvases; `CharacterController` h=3.50 r=0.50 plus a kinematic Rigidbody; peak ~12 m/s; 1202.49 m travelled over
  29 gameplay samples.
- **Aim subtree (V3-3, narrowed statically 2026-10-07, D-046)**: `Player For Anim Parent` -> `Player_For_Anim`
  (`comps=[AnimationEventPlayer]`, 9 children: `ArsenyArm`, `EngenierArm`, `ExagoraArm`, `LeslieArm`,
  `Player_Anim_Armature` -> `Gun.L`/`Gun.R`/`Root`, a duplicate `Player_For_Anim`, `ReaperArm`,
  `RomanCandleGatling_Anim`, `SOSUKEArm`). `Gun.L`, `Gun.R`, `Root` and `Player_Anim_Armature` are byte-identical
  across all 29 dumps, so they are static bone proxies. The assembly shape explains the `*Arm` leaves: they are per-hunter
  arm / weapon-animation variants, not an aim pivot, and `mechanical_arm_animator : Animator` belongs to pet/companion
  scripts. A shot's aim source is the weapon scripts (`Weapon.muzzle_position : Vector3`, `muzzle_flash_spawn : Transform`,
  `shoot_forward(..., Transform)`, `aim_assist_radius`); which arm is active still needs one runtime observation.
- **Input reality**: primary fire 23608 frames/77 presses, secondary fire 1466/13, space 764, shift 359, `q` 82,
  `e` 68, `r` 104, `esc` 26, scroll 26 notches, 9076 px mouse-X. These are *presses*; the game's **own keybinding menu**
  (키 바인딩 — 키보드 및 마우스 전용, Korean build, screenshot supplied by the user 2026-10-07; D-058) supplies what they
  mean and adds the keys the probe missed: 발사 = LMB, 보조 발사 = RMB, 점프 = SPACE, **상호작용 = Z**, 재장전 = R,
  혈능력 = F, 특수 능력 = SHIFT, 왼쪽/오른쪽 로봇 팔 = Q/E, 수동 레벨 업 = T. Cross-checks against
  `PlayerInputController` (D-031: `Fire/Fire2/Reload/Jump/BloodyRush/SpecialAbility/Interact/ArmL/ArmR`). Useful as the
  control evidence for Stage 3 action routing; it is **not** a controller mapping. The menu scrolls, so further rows may
  exist.
- **XR support in the shipped build (read out of game data 2026-10-07, D-062)**: the game ships **no XR device
  enabled**. `Vampire Hunters_Data\globalgamemanagers` (Unity 2022.3.37f1; serialized-file format version 22, type
  tree **disabled**, 18 types / 18 objects) carries `BuildSettings` (class 141) at **path id 11**, a 1272-byte blob
  at offset 150424 whose `enabledVRDevices` vector is **empty** and whose `m_GraphicsAPIs` is `[2]` (Direct3D11). The
  container layout, measured from the file and cross-checked against `AssetsTools.NET` 3.0.5 (all 18 objects
  identical): a **48-byte big-endian** header (`version`@8 = 22, `metadataSize`@16 = 1668, `fileSize`@24,
  `dataOffset`@32 = 1728), then **little-endian** metadata — null-terminated 4-aligned `unityVersion`,
  `u32 targetPlatform`, `u8 enableTypeTree` = 0, `u32 typeCount` = 18, 18 × 23-byte type entries, `u32 objectCount`
  = 18, 8-byte alignment, then 24-byte object entries of `i64 pathId, u64 byteStart, u32 byteSize, i32 typeIdx`. The
  `BuildSettings` body is `vector<string> scenes` (22 entries) → `preloadedPlugins` → **`enabledVRDevices`** →
  `buildTags` → 14 `bool`s → 4-byte alignment → `string m_Version` → `vector<int> m_GraphicsAPIs`. The parser
  round-trips the stock blob byte-for-byte, which is what `src/tools/XrDevicePatch` and `tests/xr-device-patch.ps1`
  rest on. Consequence for the port: a device name must be enabled here first, because UUVR's shipped loaders
  (`Vampire Hunters_Data\UnitySubsystems\UnityOpenXR` and `XRSDKOpenVR`) and plugin DLLs are otherwise discovered
  and simply never used.
- No VR write, no XR init, and no game file changed **by this recon**; Stage 0 remains NOT PASSED and the gate
  sequence has not begun. (The Stage 1 spike's install declares exactly one mutation of a stock file —
  `Vampire Hunters_Data\globalgamemanagers` — which the install now applies itself and which teardown restores; see
  D-062 and `docs/BUGS.md` TOOL-007.)
- **XR-less VR route opened (D-064, 2026-10-07)**: Unity's XR path is closed on this build (LIM-003), so the port
  drives the headset itself. Measured on this target rather than assumed: the loader-generated interop assemblies
  (built from this build's IL2CPP metadata) expose every API the route needs - `GetNativeTexturePtr` (CoreModule x3),
  `CreateExternalTexture`, `CopyTexture`, `SetRenderTarget`, `RenderTargetIdentifier`, `set_projectionMatrix`,
  `set_worldToCameraMatrix`, `beginCameraRendering`/`endCameraRendering`, `RenderPipelineManager`, `onBeforeRender`,
  `RenderTexture.GetTemporary`, `ScriptableRenderContext` - and the pinned loader quarantine carries URP
  (`Unity.RenderPipelines.Universal.Runtime.dll`, 1,967,616 B) and Cinemachine interop, so the game's own camera stack
  is reachable from a plugin. `Camera.SetStereoViewMatrix`/`SetStereoProjectionMatrix`, `XRSettings` and
  `XRDisplaySubsystem` also exist but are inert without a provider and are **not** part of the route.
- **OpenXR loader pin (D-064)**: official Khronos `openxr_loader.dll` 1.1.63 x64 (**Apache-2.0**) from
  `openxr_loader_windows-1.1.63.zip` (31,961,521 B, SHA256 `01C631AE…88B771`), fetched to `artifacts/openxr/`; the
  SteamVR-bundled 1.1.58 copy is deliberately not used, so which runtime serves us stays the registry's business
  (`HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` - VDXR by default, the Meta XR Simulator while SP5 runs).
- **assumed** Graphics binding will be `XR_KHR_D3D11_enable` on Unity's own `ID3D11Device`, obtained without a DXGI
  hook via `GetNativeTexturePtr` -> `ID3D11Texture2D::GetDevice()`. Unproven until SP5 M2/M4.
- **confirmed (file scan, 2026-10-07, D-068)** No anti-cheat or anti-tamper module is present in the install: no
  EasyAntiCheat, BattlEye, Denuvo, SteamStub, Vanguard or PunkBuster file exists under
  `<GameRoot>` (five files at the root - `Vampire Hunters.exe`, `UnityPlayer.dll`,
  `GameAssembly.dll`, `baselib.dll`, `UnityCrashHandler64.exe` - plus `Vampire Hunters_Data`). Single-player, Steamworks
  only. Injection therefore carries no ban risk on this build, and the anti-cheat rule stays a rule for any future build.
- **Policy (2026-10-07, D-068)** The install is now a **modifiable target**: the user removed per-step consent for
  reversible game work and accepts a Steam reinstall as recovery. The pinned backup stays as the restore point.
- **confirmed (in-game measurement, 2026-10-07, M4a)** A direct launch of `Vampire Hunters.exe` is **refused by the
  game**: the process stays alive with window title `Steam Error` and no Unity scene ever starts. The game must be
  launched through Steam (`steam://rungameid/2206270`); Steam does not forward a developer's environment, so in-game
  switches are carried by `BepInEx\config\vhvr-m4a.json` (TOOL-016).
- **confirmed (in-game measurement, 2026-10-07, M4a)** `UnityEngine.ScreenCapture` is **absent** from this build's
  interop assembly — IL2CPP stripped it — so a screenshot baseline cannot be taken with it. Pixels must come from a
  RenderTexture the mod owns, which is the M4b path anyway.
- **confirmed (in-game measurement, 2026-10-07, M4a)** Camera inventory in the menu scenes: `Intro` uses `Main Camera`
  (path `Main Camera`, fov 60, clearFlags SolidColor, position (0,1,-10)); `MainMenu_2` uses `ArenaCamera`
  (path `Camera Things/ArenaCamera`, fov 40, clearFlags Skybox, depth -1, position (75.48, 34, 43.70)). This matches
  CAMERA-PIN's note that `SetCameraTransform` sits on the menu `ArenaCamera`, and confirms the menu camera is not the
  gameplay camera.
- **confirmed (in-game measurement, 2026-10-07, M4a)** The mod can render a clone of the game's camera into its own
  1280x720 RenderTexture and read the pixels back (100% non-black, 4096+ distinct colours, mean 67.9/59.7/53.4) while
  the game keeps rendering normally to the screen. `PlayerController` does not exist in the menu
  (`no_player_controller_found`), so CAMERA-PIN conclusion 1 still needs its gameplay run.











































































































































































- **Policy (2026-10-07, D-068)** The install is now a **modifiable target**: the user removed per-step consent for
  reversible game work and accepts a Steam reinstall as recovery. The pinned backup stays as the restore point.
- **confirmed (in-game measurement, 2026-10-07, M4a)** A direct launch of `Vampire Hunters.exe` is **refused by the
  game**: the process stays alive with window title `Steam Error` and no Unity scene ever starts. The game must be
  launched through Steam (`steam://rungameid/2206270`); Steam does not forward a developer's environment, so in-game
  switches are carried by `BepInEx\config\vhvr-m4a.json` (TOOL-016).
- **confirmed (in-game measurement, 2026-10-07, M4a)** `UnityEngine.ScreenCapture` is **absent** from this build's
  interop assembly — IL2CPP stripped it — so a screenshot baseline cannot be taken with it. Pixels must come from a
  RenderTexture the mod owns, which is the M4b path anyway.
- **confirmed (in-game measurement, 2026-10-07, M4a)** Camera inventory in the menu scenes: `Intro` uses `Main Camera`
  (path `Main Camera`, fov 60, clearFlags SolidColor, position (0,1,-10)); `MainMenu_2` uses `ArenaCamera`
  (path `Camera Things/ArenaCamera`, fov 40, clearFlags Skybox, depth -1, position (75.48, 34, 43.70)). This matches
  CAMERA-PIN's note that `SetCameraTransform` sits on the menu `ArenaCamera`, and confirms the menu camera is not the
  gameplay camera.
- **confirmed (in-game measurement, 2026-10-07, M4a)** The mod can render a clone of the game's camera into its own
  1280x720 RenderTexture and read the pixels back (100% non-black, 4096+ distinct colours, mean 67.9/59.7/53.4) while
  the game keeps rendering normally to the screen. `PlayerController` does not exist in the menu
  (`no_player_controller_found`), so CAMERA-PIN conclusion 1 still needs its gameplay run.

## D-153 — 0.4c promotion (shipped-config axis) + `-EyePreset high` arm + left-weapon angle (TOOL-073) — 2026-10-13 (code+offline verified, headset pending)
- User: (1) the 0.4c sitting showed no tearing/flicker, so promote the two D-149 opt-ins to the default, (2) prepare an `-EyePreset high` sitting, (3) align the left-hand weapon angle.
- Promotion axis is the **shipped config file**: `config\schema.json` defaults true and `dist\preview\install.ps1` writes true. The **internal defaults stay false** in `OpenXrHostConfig` and `Host.h`, so a config that never mentions the keys keeps the pre-D-152 path; the drill still writes an explicit false for its control arm. Guarded by the new `source-promoted-defaults-live-in-the-shipped-config` fixture.
- TOOL-073: `Quaternion.FromToRotation` is a minimal-arc swing, so the muzzle lands exactly on the controller on both hands and the only free axis left is the **roll about the barrel**, which the captured authored rest pose decides - hence a fixed, hand-relative tilt that can differ between hands. Added `HandAimMath.Up/TwistAbout/TwistDegrees`, `HandAimPolicy.ClampWeaponTwist`, per-hand receipt fields (`weaponTwistResidualDegrees`, `maxWeaponTwistResidualAbsDegrees`) and a per-hand dial (`LeftWeaponTwistDeg`/`RightWeaponTwistDeg`, default 0). The dial's axis is the muzzle itself, so the muzzle, aim ray and fire origin cannot move.
- Offline: HandAim 237 → 254 (17 new fixtures), native 52/52, full verifier **32/32 PASS**, plugin/native Release 0 warnings, 0 errors.
- Pending: the 0.4d sitting reads the left residual with the dial at 0; the user puts that value on the dial for 0.4e. The dial cannot be validated offline - no code claims which angle is right.
- **0.4d sitting done (2026-10-13, headset tested)**: `HEADSET_RUN_COMPLETE_NOT_VERIFIED`, `endReason=level-exit`, 295 windows / 35,100 frames, `CLOSED orderly=1`, no failures, stock restored (121 files / 7,726,223,158 bytes). The promoted defaults ran through the real user path (`PREPARE_AHEAD on`, `DEFERRED_GPU_WAIT on`, `fenceMs 0` in every window, `aheadPrepares` 120/window) at `EyePreset high` (2477x2650 per eye). Whole-session `cycleMs 13.70` = 73.0 fps against 0.4c's 12.42 = 80.5 fps.
- **EyePreset high verdict: not free.** Matched 3000-frame buckets (12) show `d_cycleMs` +1.7..+2.2 ms in 9 of 12 (+0.74/-0.32/-0.37 in the other three), and the cost lands in `prepToSubmitMs` (the render tail), not in our submit or the fence. The pre-registered rule was +1.5 ms; the fair (bucket) comparison exceeds it, so the shipped default stays `normal` and `high` stays opt-in. This also retires the "fps does not scale with pixels" reading of d148f/d149a for this structure.
- **TOOL-073 measured, roll hypothesis rejected**: left `weaponTwistResidualDegrees +1.44` / right `-1.43` (mirror-symmetric, stable over 44,407/46,525 driven frames across 10 arm swaps per hand), `muzzleToHandForwardDegrees 0` both hands, `maxMuzzleVsAim 0.12/0.15 deg`, `containerRollSteps = weaponRollSteps = 0`, `maxContainerVsWritten 0 deg`, one benign pose anomaly per hand (fast hand motion: dUp 15.8 with dFwd 13.0). So the driven container's roll follows the controller within 1.4 deg and the muzzle is exactly on the controller - the container twist is not the cause of the left-hand angle report.
- **Remaining candidates for the visible tilt**: (1) the visible mesh's own roll inside the container - the receipt measures `weapon.forward` vs `container.forward` (4.06/1.23 deg) but never the up/roll axis, so this is the one unmeasured quantity; (2) the authored grip/rest pose, which the driver preserves by design and the D-153 dial can shift by eye. Next code step: add `weaponUpVsContainerUpDeg` to the receipt, then set the dial from that number (0.4e) and confirm visually (0.4f).
- Session observation stayed `DRIVING_BUT_GAME_OVERWRITES_CONTAINER` (23,254/24,784 position drifts, 0 deg rotation drift) - the known D-127/TOOL-071 behaviour, not a new problem.


## D-157 — world-anchored target icons for chests / potions / boss chests — 2026-10-13 (code+offline verified, headset pending)
- User's real complaint (after D-155): the UI is trapped inside a frontal rectangle and the things worth finding are not marked. Feature: a billboarded placeholder quad at each target's own world position, **depth test always on** (visible through walls), and the same quad clamped onto a **ring 40 degrees off the gaze** when the target is outside the view.
- Sources are the game's own registries: `PoolingSystem.active_pickups` (`List<PickUp>`; `PickUp.t`, `PickUp.type`) and `RewardBox`. Kind tokens are this build's exact `Pickup_Type` member names plus the reserved `CHEST` token for `RewardBox` (evidence `artifacts\d157-recon\`, `Assembly-CSharp.dll` sha256 `0494F1BA…`). Nothing of the game is written - only the mod's own quads are placed.
- Keys: `WorldMarkers` (`off`|`on`, default `on`), `MarkerKinds` (default `CHEST,HEALING,POWER_UP,BOSS_CHEST`), `MarkerMaxDistance` (5..200 m, default 40), `MarkerMaxCount` (1..32, default 12). Unknown values throw at Initialize. Ranking keeps in-view targets before off-gaze ones, then boss chests > chests > rest, then nearest.
- Session receipts: `hand-aim.json.worldMarkers` (`mode/kinds/wantedKinds/maxDistance/maxCount/scans/livePickups/liveChests/candidates/shown/offGaze/quads/shader/sample/error`), `host.json.worldMarkers`, log `[OPENXR_HOST] WORLD_MARKERS mode=`.
- Open (human): Q-D157-1 - do the quads appear, do they stay visible through walls, does the ring point the right way, and is the marker count/size/noise acceptable?
## D-155 — backdrop policy narrowed: only `Player HUD/Bottom Shadow` stays hidden, the game's own backdrops come back — 2026-10-13 (code+offline verified, headset pending)
- User decision (final): the in-game **HUD Bottom Shadow stays hidden**; the wrongly culled game backdrops - level-up black background, menus, chest / boss-chest screens - are **restored**.
- Cause is policy, not the driver: `HandUi.HideDarkHudPanels()` and `HandUi.UiDim()` used the D-102/D-105/D-106/D-112 width/darkness/translucency heuristics and culled at the CanvasRenderer, which also removed backgrounds the game itself drew (`Level Up/NewNewCanvas/Image - Background Full Screen (1)`, pure black alpha 0.80). Evidence: `artifacts\video-1010\hud40.png`, `artifacts\video-1010\hud70.png`, `artifacts\video1010b\montage.png`, `artifacts\perf-d104\eye0-full.png`.
- Fix: one closed-set key `GameBackdropMode` (`authored` default | `dim` | `hide`) validated by the pure `GameBackdropPolicy`; only `hide` runs the old cull, only `dim`/`hide` touch tint alpha, and the exact `HUD Bottom Shadow` identity (D-112) stays culled in every mode through `HideHudBackdrop`. An unknown value throws at Initialize (`invalid game backdrop mode: authored|dim|hide`), matching the EyePreset/PerfProfile contract.
- Wiring: `OpenXrHostConfig.GameBackdropMode` + validation + `GAME_BACKDROP mode=` log + `host.json` (`gameBackdropMode`, `hideHudBackdrop`) + `ui.gameBackdropMode` in `hand-aim.json`, `config\schema.json` (x-host-tunables), `dist\preview\install.ps1`, drill `-GameBackdropMode`, readiness `-ExpectGameBackdropMode` (plus the `+ [int]$gameBackdropKey` term in its unexpected-key count).
- Unchanged on purpose: the persistent Bottom Shadow suppression (D-112), the crosshair cull (D-100), card flattening/measurement (D-105/D-107), the ray policy (D-105) and the overlay-canvas WorldSpace conversion (D-090).
- Offline: plugin Release 0 warnings / 0 errors, HandAim 254 → 263 (9 new fixtures that pin "the level-up background still MATCHES the legacy rule but the shipped default does not act on it"), `verify-config.ps1` PASS, four edited PowerShell files AST-clean, `schema.json` parses.
- Not verified: no headset sitting has run on this build (`HEADSET_PENDING_D-155`). Whether the level-up background really returns and whether the frontal HUD rectangle stays gone is a human report (Q-D155-1). If the authored black wall is uncomfortable, `GameBackdropMode=dim` lowers it without a rebuild.


## D-166 — 포션/가고일 식별과 증거 정정 (2026-10-13)
- **confirmed / user**: 실제로 벽 가고일에 걸린 체력포션이 있었다. 빈 가고일은 표시하지 않으며 포션 있는 가고일과 그 포션만 대상. 상자 위치 표시는 유지.
- **confirmed / source**: D-165는 AddPotionAt 실패 시 holder를 HEALING으로 표시한다. 이것은 포션 존재 검사가 아니라 오표시 fallback이다. 모든 소스가 PickUp.transform을 쓰도록 통일 필요.
- **confirmed / 0.4j receipt**: 6 Gargoyle, pickup=n, HEALING@off 샘플, currentPickup 빈 값; final chestTargets1/shown7/boundsPlaced0/offGaze7. UninstallExit0/stockTrue/errors null/runtime VDXR 유지.
- **assumed / unresolved**: 매달린 포션이 PickUp인지 별도 자식 모델인지, 현재 스캔에 누락되는 이유, PickUp.t 의미/실제 위치, 상자 실종 원인. 포션 부재/자식 모델/풀 아티팩트/bounds 원인을 확정한 이전 문구는 D-166으로 철회한다.
- **policy**: 미확인 holder는 표시 0. 진단에 모든 7개 자식 및 하위 노드·활성/위치/타입을 포함하고 정체가 확정되기 전 이름 추정 마커를 만들지 않는다. 0.4k RETIRED_NOT_LAUNCHED, 자동 게임 실행 없음.

## D-167 — 0.4l 실제 포션 앞 진단·실패한 캡처 (2026-10-13)
- **confirmed / live**: 사용자 `실행`→headset PASS, Maze_Small gameplay lock, controllersAvailable=True/nativeFailures0, 양안 제출. plugin24152491…/nativeF7FDF2D6…/VDXR 유지, 설치 생략.
- **confirmed / sampled nodes**: 사용자 `나 포션앞` 이후 09:01:48Z/09:02:23Z hand-aim/scans348→416, shown0/chestTargets0. Gargoyle/HealthPoint/Potion id214560 self=True/live=True/renderer=on/pickup=none/pos135.47,5.47,57.68; 나머지5개 Potion 샘플은 self=True/live=False(HealthPoint parent inactive). 활성 포션 모델 후보가 typed PickUp 스캔에 포함되지 않은 경로를 관측함.
- **unresolved**: 이름/renderer/활성만으로 사용자 시야의 정확한 포션 일치를 확정 못함. host.evidence는256개 bounded 누적이라 마지막 position도 최신 위치로 단정 금지. 수집 후 변화·실제 게임 availability 필드·상자 좌표/시각 결과 미관측.
- **confirmed / capture failure**: 에이전트ID16601은 RequestCapture 허용0–10 밖(Host.cpp385), return2를 managed가 throw하여 HOST_FAILED. raw/양안 캡처 없음. 사용자가 잘못한 것이 아님, TOOL-087 OPEN. 0.4l consumed/NOT_READY. 09:05:34Z 자동 원복 완료:Uninstall0/Recovered27/stock121files/8dirs/7,726,223,158bytes/hash+write times 일치/errors null/VDXR 유지. 현재STOCK, 재실행·재설치 없음.

## D-175 — 요청 5기능(메뉴 패널·쿨타임 햅틱·UI 배율·Y/B 기술·트리거 유지 연사) — 2026-10-14 (코드+오프라인 검증, 헤드셋 0건)
- **scope**: 사용자 요청 5개를 `extras\VhvrExtras` 패치 플러그인으로 구현(D-174의 별도 플러그인 축). **게임 IL2CPP 패치 0줄**, Stage0 관리 메서드 후처리 + 자체 P/Invoke(`ReadHands`/`RequestHaptic`)만 사용.
- **confirmed / offline**: 빌드 경고0/오류0 (`VhvrExtras.dll` 39,936 bytes, SHA256 `EF8B9735…`), extras 픽스처 **60/60 PASS**, 설정 도구 `uiScale` 쓰기→읽기→거부→경계(0.5/3.0) 실행 확인 + `--selftest`/`--form-selftest` PASS, 페이로드 설치/제거 **235파일 왕복**(플러그인 포함/제거, 잔여 없음), 번들 zip 250 엔트리/SHA256 `0FD1F546…`.
- **assumed / unverified (headset 0건)**: ① 검정 배경 + 1.5 m 패널 ② 레이저 클릭이 게임 EventSystem에 도달 ③ HUD 1.5배 무잘림 ④ `Weapon.attributes.base_fire_rate <= 2/s` 판정이 실기기에서 유효 ⑤ 좌 Y=특수 / 우 B=블러디 러시 ⑥ 햅틱 도착 시점.
- **known cost / policy**: `MenuFirst=true` 세션은 호스트가 `GameplayOnly=false`로 시작해 **손/무기 통합이 설치되지 않는다**(평면 스테레오 릴레이). 그래서 기본 **OFF**, 켜면 경고 로그, 기본값 전환은 사용자 승인 후에만.
- **policy**: `PATCH_APPLIED`을 동작 수락으로 쓰지 않는다. 수락 근거는 `docs\playtests\PLAYTEST-EXTRAS-KO.md`(6항목 체크) + `tests\headset-extras.ps1` 영수증뿐이다.
- **open**: TOOL-088(src 트리 손상으로 `src` 기반 픽스처와 `settings-tool.Tests.ps1`(`dist\versions\0.4d\config.json` 부재)이 이 트리에서 불가 → `verify.ps1` 전체 초록 불가).


- **D-104-iso-promotion (0.7.1, 2026-10-17):** D-104 의 `Anisotropic` 스위치를 `VhvrSettings.exe` 의 체크박스 + CLI `--aniso`/`--no-aniso` 로 노출. **호스트 코드 변경 0줄**(`OpenXrHost.cs:879` 분기 이미 존재). 기본 OFF, 16x 강제(`SetGlobalAnisotropicFilteringLimits(16,16)`). 비용 미측정 — 다음 PLAYTEST-D104 라운드에서 확정.


- **D-198 (2026-10-18, 코드+오프라인 검증 / 헤드셋 미확인):** 게임 시작 후 **15초 VR 지연**의 원인은 게임이 아니라 호스트였다 — `OpenXrHost.Update()` 의 `if (Time.unscaledTime < 15f) return;`. `Time.unscaledTime` 은 게임 프로세스 **시작 이후 누적 초**(씬 로드 리셋 없음, Unity 문서 *"seconds since the start of the game"*)이므로 호스트는 게임 시작 후 15초 동안 `Initialize()` 를 호출하지 않았다. 매 프레임 readiness 게이트(카메라 존재 + D3D11)로 교체해 **준비 즉시 VR 시작**, 대기는 `GameplayWaitSeconds` 예산 + `STARTUP_WAIT`/`waiting.json` 영수증으로 증명, `host.json` 에 `startup.readySeconds`/`waitReason`/`gatePassed` 기록. 즉 게임은 3초에 준비돼도 15초를 기다리던 상태였고, 수정 후에는 **준비 완료 시점**이 곧 VR 시작 시점이 된다. 게임 쪽 startup 타이밍(메인 카메라 생성 시각)은 다음 receipt 의 `startup.readySeconds` 로 처음 측정된다.

