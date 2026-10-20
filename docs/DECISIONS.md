# Decisions — append only
- **D-186-restore-1 (2026-10-16): Dist 0.4d fixture recreated.** `dist\versions\0.4d\config.json` was sync-damaged and no intact copy exists in artifacts. Recreated as the legacy-schema full-shape fixture the plan required (`Enabled/HandAim/EyePreset "custom"/EyeWidth 2064/EyeHeight 2208/EyeMsaa 4/MarkerKinds/UiVerticalOffset -0.25`, no `EyeScale` key): 378 bytes, `jq -e` passes. `tests\settings-tool.Tests.ps1` now passes 23/23 against it (its 10 lines matching 0.4d with the import window cropped to 0.4d remained intact).
- **D-186-restore-2 (2026-10-16): Bindings cross-checked against recovery dumps.** The live `config\bindings.json` was rebuilt mid-session from `artifacts\recovery\dumps-tree\config\bindings.json`, which carries the D-058 provenance marker `amendedBy: "D-058"`. The dump header says `the mirror is the supporting evidence, not the authority` and the binding carries `manualBindingSource`. Both parents prove themselves in their tree (each tree's own `verify-bindings.ps1` passes against its own bindings). Chose the replay-tree copy as the content to restore (D-059 generation, matches the live changelog's five points and the restored verify-bindings pins exactly); the final bindings file retains the dump's integrity evidence (`previousConfigSha256` chain) and `config\bindings.json.sha256` is refreshed from it. The evidence mirror header/`bindings.json.sha256` stay D-058-generation on purpose (they are a dump pair; mixing them would break the mirror's own chain).
- **D-186-restore-3 (2026-10-16): `tests\verify-bindings.ps1` restored from `artifacts\recovery\replay-tree`.** Only intact copy; hash matches the pre-sync capture recorded in `docs\RECOVERY.md` (467 lines). All four pin groups (D-057 layout, D-058 evidence, D-059 double-tap reload, D-114) present. BINDINGS_CHECK passes (38/38). Pin strings untouched.
- **D-186-restore-4 (2026-10-16): D-170 MSAA policy API reconstructed in `src\render\RenderResolutionPolicy.cs`.** The sync reverted this file to the pre-D-170 form (no `IsValidMsaa/UnitySamples/MsaaLabel`, 3-arg `Describe`) while `tests\SettingsTests.cs` kept the D-170 contract. No MSAA-era copy exists in any recovery tree. Reconstructed strictly to the fixture contract: `IsValidMsaa` = {0,2,4,8}, `UnitySamples(0)=1`, `MsaaLabel(0)="off"/(4)="4x"`, and a `Describe(w,h,preset,msaa)` overload emitting `eye=… pair=2w×h msaa=…`. The existing `Describe(w,h,preset,customScale)` overload (the `both=` form `tests\settings-tool.Tests.ps1` pins) is untouched, so both generations' pins hold.
- **D-186-restore-5 (2026-10-16): `tests\SettingsTests.cs` legacy tool fixtures rehosted onto the current `HostConfigTool` API.** The fixtures were written against `HostConfigValues` — a type that never existed in any recoverable implementation (searched all recovery trees; only the fixture references remain). The real, settings-tool.Tests.ps1-verified API is `PictureSize` + `ReadEyeMsaa/ValidateEyeMsaa/ApplyEyeMsaa`. Fixture intent preserved 1:1 (four-key read+defaults, reject-and-write-nothing, apply-only-its-keys+backup, locate, oversized refusal). Suite now green: `RESULT 8 passed, 0 failed`. `tests\verify.ps1` settings marker updated to the suite's real identity string (was pre-D-170 wording `18 passed … pure config/marker fixtures`); the repo updates these markers when suites grow (the `67`/`37` markers prove the practice).
- **D-186-restore-6 (2026-10-16): WeaponRuntime source-guard pins refreshed to the D-186 semantics.** `production-player-held-projection-and-restoration` pinned the pre-D-186 projection `(held, held, held, held)` and `production-passes-owned-player-to-transaction` pinned the pre-D-186 call; the D-186 Y/B secondary fire changed both by design (`(held, secondary, held, secondary)` and the `secondary` argument in `new HeldInvocation(...)`). Updated the pins to the D-186 shape; the owned-player invariant (`d.runtimePlayer`) stays pinned. Suite green: `RESULT 67 weapon runtime fixtures` (matches verify.ps1's marker).
- **D-187 (2026-10-16): Robot-arm (`SecondaryWeapon`) cooldown haptic root causes fixed and deployed.** Disassembly of `SecondaryWeapon` and all 23 robot-arm subclasses (`DragonWhip`, `LaserSaber`, `Bomb`, `Chainsaw`, `ShieldGenerator`, etc.) in `GameAssembly.dll` revealed why the D-183/D-186 arm haptic failed in gameplay: (1) `Weapon.second_shot_cooldown` (default 15 s) and `Weapon.can_use_secondary_shot()` (`0x18047AB60`) belong to primary weapons' level-3 alt-fire, whereas every robot-arm weapon inherits from `SecondaryWeapon`, sets `can_attack = false` (`+0x2DC`) and `has_attack_ended = false` (`+0x2DD`) in `default_shoot` (`0x1804A0880`), counts `attack_t` (`+0x2D8`, initialised from `Weapon.reload_time()`) down in `default_secondary_weapon_reload` (`0x1804A0700`), and flips `can_attack = true` in `default_reload` (`0x1804A05B0`) / `force_reload` (`0x1804A0980`) when cooldown ends (after initial deploy sets `start = true` at `+0x2E8` in `mech_arm_start_check`, `0x1804A0AE0`); (2) `ArmHaptics.cs` previously set `WasReady = true` before checking `RequestHaptic`'s return code, so whenever the render thread held `host.mutex` (`result == 6`, native host busy), the pulse was permanently dropped instead of retrying on the next frame; (3) the game folder still carried the 2026-10-15 DLLs. Updated `CooldownHapticPolicy.cs` (`ShouldPulseSecondaryArm` / `RecordResult`) and `ArmHaptics.cs` (per-frame check on cached `SecondaryWeapon` instances + retry on `result == 6`), added 10 fixtures to `tests\VhvrExtras\Program.cs` (62/62 PASS), and deployed `VhvrExtras.dll` (`9758F33A…`) and `VampireHuntersVR.Stage0.dll` (`731774F4…`) to both `<GameRoot>\BepInEx\plugins\` and `dist\VampireHuntersVR-0.6-final\payload\BepInEx\plugins\`. Headset verification pending.
- **D-186-restore-7 (2026-10-16): NativeHost (`Hands.cpp`/`Host.h`/`Host.cpp`/`NativeHost.Faults.cpp`) and `HandAim`/`NativeHost.Tests.ps1` suites restored and green.** Reconstructed `src\render\NativeHost\Hands.cpp` (`/interaction_profiles/oculus/touch_controller` + `/user/hand/{left,right}/output/haptic`), replayed `Host.h`/`Host.cpp`/`NativeHost.Faults.cpp` through D-149 (`SetPrepareAhead`, `SetDeferredGpuWait`, CPU split timing, single-sampled path without discarded D-170 `SetEyeMsaa`), populated `artifacts\openxr\extracted` from local OpenXR SDK 1.1.63, and realigned decompiled C# source-guard pins in `tests\NativeHost.Tests.ps1` and `tests\HandAim\Program.cs`. `tests\NativeHost.Tests.ps1` passes (`52 fault fixtures`, 4 exact/4 flipped/4 negative GPU cases, 3 PowerShell ASTs) and `tests\HandAim\HandAim.Tests.csproj` passes (`RESULT 190 hand aim fixtures passed`).
- **OPEN (2026-10-16): extras fixture count updated to `62` in `verify.ps1`.**
- **D-188 (2026-10-16): 0.7 — Quality settings dial removed, MSAA exposed in install defaults and tool docs (D-187 plan, second pass).** 0.6 shipped with a `GraphicsQuality` dial (D-181) that overrode `QualitySettings.SetQualityLevel` on host start, with a `VhvrSettings.exe` row and a `GraphicsQuality = -1` line in the install default. 0.7 removes the dial entirely: the POCO field, the `*GraphicsQuality` (Read/Validate/Apply/Min/Max) helpers in `HostConfigTool`, the `--graphics-quality` CLI flag (now reported as `unknown argument` and exit 2, never silently ignored), the `_quality` NumericUpDown + its `Quality (-1 = game's own)` label in `SettingsForm`, the `ApplyGraphicsQuality()` method + the `GRAPHICS_QUALITY …` log lines in `OpenXrHost`, and the `GraphicsQuality = -1` line in `dist/VampireHuntersVR-0.7/install.ps1`. The host now uses the game's own saved `QualitySettings` level as-is, which is the value the user already chose. Legacy `GraphicsQuality` keys in old `vhvr-openxr-host.json` files are silently dropped by `System.Text.Json` (unknown property), so existing files keep loading. MSAA — present in the 0.6 code (`EyeMsaa` POCO + `*EyeMsaa` helpers + `_msaa` ComboBox, D-186) but missing from the 0.6 install default and the tool README — is now written as `EyeMsaa = 4` by `install.ps1` on first install (so shipped-4 is explicit, not implicit) and is documented in `SETTINGS-README-KO.md` (1 = off, 2/4/8 = samples, 4 = shipped default, 1·2·4·8 only — other values refused by tool and by host). `vhvr-openxr-host.json` shipped default confirmed free of `GraphicsQuality` and containing `EyeMsaa = 4` (install dry-run on a scratch game folder, `INSTALLED: 236 files` + `UNINSTALLED: removed 236 installed files` round trip). Form-self-test threshold lowered from `Controls >= 15` to `>= 13` (the `_quality` row + its label = 2 controls gone; 0.6 = 22 controls, 0.7 = 20 controls, threshold 13 is well below both). `Settings.Tests.exe` 8/8 PASS, `tests\Settings.Tests.csproj` rebuild 0 warnings 0 errors, `src\settings\Build.ps1` publish + `--selftest` exit 0, new `VampireHuntersVR.Stage0.dll` 419,328 B (was 420,864 B in 0.6) and `VhvrSettings.dll` 35,840 B (was 34,816 B in 0.6) — both DLLs confirmed free of any word-boundary match for `GraphicsQuality / _quality / MinGraphicsQuality / MaxGraphicsQuality / ApplyGraphicsQuality / ReadGraphicsQuality / ValidateGraphicsQuality`. `dist\VampireHuntersVR_0.7.zip` (the working baseline = content of `0.6-final`) preserved in `dist\versions\0.6-final\`; new `dist\VampireHuntersVR-0.7.zip` to be packaged from the rebuilt tree (D-188 deliverable). Headset verification still on the user.

### D-001 — Separate project, existing-tool reuse
Date: 2026-10-06 | Stage: setup
Chose: user-selected `<RepoRoot>`; reuse existing tools and develop game-specific integration. Do not create files in shared chat root or mix development files with Steam game assets.
Cost: integration architecture still pending renderer/runtime evidence; not a commitment to from-scratch stereo.

### D-002 — IL2CPP-specific bounded recon
Date: 2026-10-06 | Stage: 0 preparation
Evidence: GameAssembly.dll, global-metadata.dat with valid magic and version 31; logical ScriptingAssemblies.
Chose: independent bounded metadata reader and SHA256 dossier, not the previous EXE-only kill scanner. Previous scanner includes general graphics/Steam imports as VR hits, incomplete UTF-16 reporting and lacks boundary checks; those issues are not carried into this project's verdict.
No native-VR verdict from generic Unity XR modules. No runtime API verdict from static imports.

### D-003 — Framework evaluation, no license mislabeling
Date: 2026-10-06 | Stage: research
- BepInEx: loader, not stereo/input/hands/IK. GitHub currently LGPL-2.1, push 2026-09-20, 381 open issues/PRs. Evaluate unmodified redistributable binary as a tool with notices; **not a permissively licensed VR framework**. Exact build license will be inspected. Official IL2CPP guide uses bleeding-edge win-x64 builds.
- UUVR: public code, GPL-3.0, push 2025-07-03, 30 open issues/PRs. Has OpenXR/OpenVR source projects, but exact game compatibility and complete motion interactions not verified. GPL allows derivatives with obligations; **not permissive**, not approved for incorporation by user's selected recommendation. Study approach only until separate decision.
- VRGIN: MIT, push 2019-06-24, 12 open issues/PRs. Generic Unity VR injection; maintenance and Unity 2022 IL2CPP compatibility insufficient to select it as baseline.
- Rai Pal: GPL-3.0 mod manager, not game-specific VR integration.
No maintained permissively licensed, current IL2CPP-compatible VR framework confirmed in current search; continue evaluation before writing rendering integration. OpenXR is primary goal, no OpenVR fallback selected.
Exact-game finished VR mod search inconclusive: GitHub exact VR query zero results; general web search did not produce usable evidence. Never claim none exists conclusively.
Sources: API URLs and official docs recorded in TARGET_PROFILE. Reputation/current-build compatibility unverified for all candidates.

### D-004 — Prerequisite hold and evidence gates
Date: 2026-10-06 | Stage: setup/0
Chose: perform only project-local builds and read-only game recon until working native VR runtime, confirmed hardware/connection, saves/install backups, version pinning and policy review. Do not deploy/inject yet. Steam EULA endpoint failed, not authorization.
Default proposed: hard gates Stages 0–4; user confirmation pending. Static symbols or successful compilation are not a passed gate or a confirmed hook.
Online leaderboards exist. Mod testing must be single-player/offline where supported; no score/account manipulation or DRM/anti-cheat bypass. Protected build requiring bypass stops this path.

### D-005 — Preserve game-specific combat
Date: 2026-10-06 | Stage: planning
Chose: retain up to 14 simultaneous weapon slots as Stage 6 acceptance criterion. Two tracked hands do not imply two total weapons. Hand-bank grouping is only a proposal; no drop of multiweapon gameplay, auto-fire or actual reload rules without evidence and user decision.
Full-body IK and all comfort modes remain goals; not implemented and no hands-only fallback selected.


### D-006 — Explicit partial metadata coverage, not replacement decoding
Date: 2026-10-06 | Stage: 0 preparation
Actual UTF-8 decode failed at file offset 1352004, two bytes 80A0. Header string offset 633560 / byte count 1770628 confirmed against LibCpp2IL header layout (schema study only, code not copied).
Chose: preserve undecodable entry count and first 64 raw offset/hex samples; continue only strict-decodable candidate names; mark report PARTIAL_UNDECODABLE_NAMES. Structural bounds/version/cap errors still fail with exit 1. No guessed Latin1/replacement names, no claim of complete class dump.
Cost: non-UTF8 segments remain unmapped until runtime interop discovery; no deobfuscation, unpacking or bypass attempted.
Source: https://raw.githubusercontent.com/SamboyCoding/Cpp2IL/development/LibCpp2IL/Metadata/Il2CppGlobalMetadataHeader.cs

### D-007 — Pinned diagnostic loader and library license
Date: 2026-10-06 | Stage: prerequisites
BepInEx 6.0.0-be.788+5b766a3 (34,336,405 bytes) downloaded/extracted only in project tools. SHA256 F4CC496BD098A0DF4164B81E3737297707F13A47C2478DBA2F60EEFAB784817A.
Pinned LICENSE confirms LGPL-2.1-or-later, Copyright (C) 2020 BepInEx Team. This is not a permissive VR framework. Keep unmodified as separate loader/library and include required notices/source and transitive license review at distribution. No UUVR code included.
Stage0 diagnostic compiled net6.0 with 0 warnings/errors; no interop generation/in-process entry test yet. SafeMode default true for preparation, exception to shipping VR defaults because no VR features exist; not a downgraded release.

### D-008 — Gear intake and baseline evidence boundary
Date: 2026-10-06 | Stage: setup/0 preparation
User confirmed Quest 3, bundled controllers, Virtual Desktop connection and other working VR games (Fallout and EDF6 VR mod). Record these as user-reported evidence, not an agent-observed headset test. Do not repeat answered gear questions or request another headset session just to repeat this baseline.
Active OpenXR runtime, Streamer preference, exact native-title/runtime pairing, stock Vampire Hunters launch, other mods on Vampire Hunters, backups, version pinning and policy remain unknown. A working EDF6 VR mod neither validates a Unity IL2CPP loader nor authorizes reuse of EDF6 mod code.
Chose: preserve existing working VR setup; no automatic runtime selection/switch, injection or game-file writes. Next request is explicit read-only PC/VR inventory consent. No user documents/saves browsing and no additional PC settings inspected before consent. D-004 prerequisite hold remains in effect; this is no feature fallback or stage pass.

### D-009 — Consented PC/VR inventory, preserve VDXR default
Date: 2026-10-06 | Stage: setup/0 preparation
User explicitly accepted the narrow read-only OS/GPU/CPU/RAM/installed-VR-software/OpenXR inventory offer. CIM hardware queries, filtered installed-app/known-VR-process queries, SteamVR install manifest and runtime manifest/file presence checks completed without settings edits or application launch. Do not persist raw Steam manifest account identifiers; retain only required build/install facts.
Observed: Windows 11 Pro build 26200 x64; Ryzen 7 9800X3D; RTX 5080 plus AMD iGPU; 32 GiB installed RAM/31.16 GiB OS report; Streamer 1.34.22; SteamVR installed/build 25330290; VirtualDesktopXR (Bundled)/VDXR default registration for 64/32-bit, both referenced DLLs present. User confirmation of observations pending.
Chose: leave working setup unchanged. Registry registration is not a successful XR session, per-game launch route, or compatibility proof. Streamer preference and active interaction profile remain unknown; no SteamVR/OpenVR fallback selected. No game execution, injection or private documents/saves inspection authorized by this consent. D-004 prerequisite hold remains; no Stage 0 pass or gate tag.

### D-010 — User confirms PC/VR inventory, not game compatibility
Date: 2026-10-06 | Stage: setup/0 preparation
User answered "네" to the question whether presented PC specifications and VR program information are correct. Mark the presented inventory confirmed without repeating hardware questions. D-009 remains historical; its pending confirmation is resolved by this entry.
Chose: keep the existing VDXR registration unchanged and ask for current Vampire Hunters monitor launch/mod status next. This answer does not confirm Streamer preference, an actual XR session, stock-game launch, backups, policy, version pinning or any stage gate; it does not expand consent to installs, runtime changes, game launch or private saves/documents. No feature fallback; D-004 prerequisite hold remains.

### D-011 — Human-confirmed stock launch and no target-game mods
Date: 2026-10-06 | Stage: setup/0 preparation
User answered "정상 실행되며, Vampire Hunters에 설치한 모드는 없습니다." Record normal monitor/flat launch and no installed target-game mods as user reports; Q-006/Q-007 are resolved. No agent game launch or exhaustive new mod inventory performed. Starting HEAD d18cdfd/clean worktree independently checked; existing verification evidence read back with 18/18 tests and full process exit 0, not rerun this session.
Chose: continue the existing wizard at remaining screen 2 workflow details; do not repeat answered gear, PC inventory or target baseline questions. This answer does not authorize game execution, installation, settings changes, injection or private saves/documents inspection. Actual backups, supported version pinning, policy review and remaining preferences are pending. D-004 hold remains; no Stage 0 pass, confirmed hook, VR implementation or feature fallback.

### D-012 — Separate coding knowledge, VR experience and optional comfort scope
Date: 2026-10-06 | Stage: setup / wizard screens 2 and 4
User reports no coding knowledge; game/headset connection/development on the same PC; experienced VR player who does not need anti-nausea/user convenience features. Q-008 and same-PC Q-009 resolved; session duration remains unanswered, deferred until test planning.
Chose: agent does development within consent, explains technical terms simply without treating the user as a novice VR player. Honor the opt-out preference; no comfort-first user defaults approved. Ask Q-015 to distinguish excluding optional aids (vignette/tunneling, teleport, snap turn, optional comfort fades) from keeping them implemented but disabled; smooth movement/turn is proposed, not selected. No release-scope exclusion or actual code/config change yet; this is not an engine limitation or hidden fallback.
Keep separate: correct head tracking/stereo, camera ownership, collision/boundary, required transition handling, calibration/usable UI/settings, safe-mode/config validation, bug capture and evidence gates are core quality/recovery requirements, not removed by this broad preference. Experienced VR play is not proof of immunity to a broken camera or a stage pass. Do not expand consent to game execution, installation, PC settings changes, injection or private saves/documents inspection. D-004 hold remains; no Stage 0 pass/tag.

### D-013 — User-selected snap-only turning and optional aid exclusions
Date: 2026-10-06 | Stage: setup / wizard screen 4 preference clarification
User: "스냅 턴만 만들고 나머지는 모두 필요없어." Q-015 resolved; supersedes D-012's pending optional-aid scope.
Chose: implement snap turn only as controller turning. Exclude smooth/continuous turn, vignette/tunneling, teleport and optional locomotion anti-nausea fades from implementation, not merely default OFF. Ordinary stick locomotion remains planned; physical head/body movement is not a controller-turn option and remains. Snap angle and repeat/re-arm behavior are Stage 4 tuning work, no numeric value selected now.
Reason/cost: explicit experienced-player preference avoids unwanted extras, but removes alternate turn/teleport/comfort modes for other users. This customized Phase 1 does not satisfy the unmodified Wizard all-comfort checklist; show exclusions in PHASE_PLAN/README and test only retained criteria. Not an engine limitation or silent fallback.
Retain correct head tracking/stereo/camera ownership, collision/boundary, required transitions, calibration/usable UI/settings, safe-mode/config validation, diagnostics and evidence gates. No new consent for game execution, installation, PC settings changes, injection or private data inspection; D-004 hold remains. Documentation only now, not a snap-turn build/test, Stage 0 pass or gate tag. Next: remaining screen 3 game mechanics.

### D-014 — Reported mechanics and direct game-file work consent
Date: 2026-10-06 | Stage: setup / screen 3 and Stage 0 read-only preparation
User reports double jump, dash, Q/E auxiliary weapons, a frenzy-like blood ability and primary/secondary fire; explicitly authorizes direct game-file inspection/work. Resolve Q-010 and Q-016 without another game launch/interview. Chose: preserve all reported mechanics as separate acceptance requirements and user-report evidence in manifest; investigate identifiers/files and develop project-local tools now. Exact ownership, timings, reload/auto-fire, other mechanics and Q/E-to-VR bindings remain unknown.
Consent applied to installed-file reading/project work, not inferred permission to launch game, install/inject loader, overwrite stock, alter PC/runtime settings, inspect private saves or waive backups/version pinning/policy. D-004 prerequisite hold and D-013 scope remain. Next screen 4 posture/hand, then gates and prerequisites; headset time/snap tuning deferred. Not a feature downgrade or stage pass.

### D-015 — Offset-bearing mechanics search buckets, not confirmed hooks
Date: 2026-10-06 | Stage: 0 preparation
Chose: extend existing v31 bounded reader using only existing .NET libraries, retaining first occurrence of each keyword candidate with metadata string offsets/byte length. Seven capped search buckets plus fixture/full-verifier evidence checks; reports local and uncommitted. Do not decompile/load native code or extract protected assets; names give search directions only, no addresses/callable ownership/runtime claim.
Cost/limits: broad filters include framework/generated false matches, groups overlap; first-name dedup loses duplicate owners, candidate list capped 10,000 and bucket samples 64 with explicit counts/truncation. Real run candidate input not truncated; 46 undecodable segments remain raw/unmapped (D-006), no replacement decoding or bypass. Metadata count 97,424, broader keyword matches 3,920 rather than historical 2,962; not changed gameplay. FOV_TO_ADD_ON_DASH seeds Stage 1 camera investigation, not proof of active FOV changes.
Verification: 23/23 tests, full verifier process exit 0, zero-warning/error builds, JSON/CLI/offset/hash checks pass; four original core/metadata hashes unchanged, no loader installed. Stage 0 remains NOT PASSED and no gate tag. No VR function implemented or fallback selected.

### D-016 — Primary standing/right-handed preference
Date: 2026-10-06 | Stage: setup / wizard screen 4
User selected "주로 서서 플레이 / 오른손잡이". Resolve Q-012 and use standing/right-handed as future initial calibration/input target. No numeric height/offset, implemented binding or runtime validation inferred. Primary preference does not exclude seated support/handedness switching; D-013 exclusions and acceptance criteria unchanged.
Documentation only; no new code/config/runtime work, game launch/install/stock overwrite/settings/private saves consent or gate pass. Prior 23/23/full exit 0 remains latest code verification, not a posture playtest. Next Q-011 gate workflow, then actual backups/pinning/policy; test duration/snap tuning deferred.

### D-017 — Confirmed hard gates for Stages 0–4
Date: 2026-10-06 | Stage: setup / wizard screen 4
User selected recommended automatic checks and corresponding actual behavior validation before next-stage implementation. Resolve Q-011; supersedes D-004's proposed/pending gate choice. Chose hard gates Stages 0–4: all exit criteria, automatic evidence and required human feedback must pass before implementing the next stage. Compiled code/static names alone do not pass. Current-stage fixes/research may continue; later-stage implementation cannot fill an untested/failed gate wait.
Stage 0 entry can be proven desktop/flat without headset; later headset-required checks stay mandatory and written/batched. Later stage-specific playtests and release gates remain. Cost: sequential progress and pauses for human verification avoid building on broken camera/render/input. No scope downgrade, feature change, Stage 0 pass/tag or new execution/install/settings/saves consent. Documentation only; latest code evidence remains 23/23/full exit 0. Next Q-014 backup scope, supported version pinning/policy and separate deployment/launch consent.

### D-018 — Consented install/game-save backup, isolated restore rehearsal
Date: 2026-10-06 | Stage: prerequisites / Stage 0 preparation
User chooses agent inventory/copy of untouched install + Vampire Hunters saves into previously proposed `<RepoRoot>\backups`, after capacity checks with originals unchanged. This explicitly expands Q-014 to game-only save-folder inventory/copy/hash; no unrelated private files, save payload parsing, cloud account inspection, Steam settings/Cloud changes, stock restore, game execution or loader deployment/injection authorized. Historical no-save-access entries remain prior scope, superseded only for this narrow backup.
Chose fresh snapshot and full SHA256 manifest, fail-closed paths/reparse/process/capacity checks, no-overwrite copying, source-change detection, full restore rehearsal into separate project-local directories. Backup directory ignored in Git before creation; proprietary bytes/raw saves/local manifests not committed or distributed. Same F drive: recovery from mod/file mistakes, not hardware failure protection; separate-drive/offline copy would be needed for that. Backup approval does not finish backup/restore prerequisite or pass Stage 0. Preflight records actual save folder and adequate free space; fixture tests and copy verification follow. Policy/version pinning and separate launch/install consent still open.

### D-019 — Verified snapshot and isolated restore, not stock/runtime success
Date: 2026-10-06 | Stage: prerequisites / Stage 0 preparation
Backup completed under D-018: 121 install files/7,726,223,158 bytes plus game-folder 6 files/51,618 bytes at `<RepoRoot>\backups\20261006T104307Z-cdd080e1e50a4fda845ffa7c01158173`. Full source/copy/isolated-restore path/hash comparisons and source timestamp checks pass, completion 10:43:44 UTC, exit marker 0; independent all-game/save rechecks also pass. Manifest SHA256 828B45CCC6D82AF32E2E4636B63D718E55FC188AFB5245F1B8DCC39A8F593A6F. Original roots never opened for writing; no game launched, Cloud changed, payload interpreted or stock restored. Backups ignored in Git; restore copies retained (~14.39 GiB total payload), F free after 842,201,567,232 bytes.
Chose mark snapshot/isolated-restore backup prerequisite complete, not Stage 0 stock relaunch/entry/config/VR criteria. Same-drive limitation remains, no silent downgrade from disk-failure protection. New PowerShell helpers/12 fixture checks test byte verification, overwrite/overlap/path/capacity rejection and source changes using synthetic data; .NET recon 23 tests remain separate. Version retention/policy and separate launch/install consent still open.

### D-020 — Autonomous preparation within existing consent and hard gates
Date: 2026-10-06 | Stage: prerequisites / 0
User asks to continue independently unless intervention is necessary. Chose agent-owned research, project builds/tests, documentation and commits without further routine intake. Prior narrow scope is not silently expanded to Steam/Cloud/runtime settings, installation/injection/game launch, save interpretation, external messages through the user's identity or stock overwrites. Those need an informed explicit decision when ready; current gate stays hard (D-017). Do not repeat answered gear/mechanics/posture/backup questions or start Stage 1–4 implementation while Stage 0 is untested.

### D-021 — Policy researched, leaderboard/permission uncertainty retained
Date: 2026-10-06 | Stage: prerequisites / 0
Live EULA endpoint remains unreadable. Official developer comments from 2024/2025 discuss possible future mod support, not a mod license; 2024 statement explicitly says best score reaches leaderboards and leaderboard-off could be considered. Steam SSA 2.G/4.B restrictions/exceptions reviewed; personal-use wording is not blanket mod authorization. Stock filename scan has no obvious protection markers, not proof of no anti-cheat/DRM. Recommend single-player/offline only where supported; neither offline Steam nor non-multiplayer categorization proves no deferred score upload. Do not patch networking/leaderboard APIs or bypass any protection. Hold in-process tests pending policy/risk decision, supported online isolation and separate consent. Optional inquiry drafted in ENTRY_PLAN.md, not sent. Cost: runtime progress pauses rather than risking account/online score integrity. Sources/quotes in ENTRY_PLAN.md; not legal advice or a definitive game-specific prohibition.

### D-022 — Immutable supported-build evidence and read-only entry plan; no false version pin
Date: 2026-10-06 | Stage: prerequisites / 0
Public appinfo snapshot and installed manifest both build 22267731/depot 3974471454289775655, only public branch exposed plus hidden private-branch indicator. Supported Game Versions & Betas UI selection documented but not operated. Update-on-launch is not a full version pin and even developer branches can change. Chose fixed core hashes/sizes in config/supported-build.json, all-stock-file comparison to pinned backup manifest, ZIP-to-extraction integrity, DLL hash and zero-conflict owned roots, with proposed local plan only. Full ZIP 228 files plus one diagnostic DLL = 229 proposed additions; no stock modifications. Path/ZIP/alias/symlink/conflict rejection uses synthetic tests; no install/uninstall implementation or extra backup run. Exact-build guard detects changes, does not prevent Steam updates/direct launch and is NOT a completed version-pin prerequisite. No downgrade to guard-only development accepted; that requires a future explicit decision. Reversible owned-addition/quarantine design documented, not claimed executed. Loader/runtime/visible diagnostic still unverified, Stage 0 NOT PASSED.

### D-023 — Continue current-stage coding; approvals gate execution, not preparation
Date: 2026-10-06 | Stage: 0 preparation
User reiterates "조작 필요없으면 계속 코딩". Correct earlier board that unnecessarily put reversible-tool coding behind deployment decisions. Continue implementation/build/tests autonomously within Stage 0; no repeat intake or headset request. Actual install/launch/settings/stock-write boundaries and hard Stage 0 gate stay unchanged.
Chose staged same-volume allowlisted-root publication with append-only flushed intent journal, explicit recovery moving additions to private quarantine instead of deleting them. Preserve generated files; changed/missing expected payload or foreign roots stop recovery for review. No stock/save restore copy. Use existing PowerShell/.NET helpers only; no new VR framework/rendering or later-stage implementation. Process checks cannot exclude an external launch atomically; actual integration/launch consent remains required. Test engine on synthetic directories only before providing a real deployment entry point.
Recovery evidence is trusted local data, not authenticated against malicious journal edits. Same-session named mutex serializes this tool but not other Windows users or Steam. Journal Flush(true) and same-volume moves do not guarantee power-loss directory durability. Added separate fixture-child termination after publish/quarantine move and before result journaling to test abrupt process interruption/abandoned-mutex recovery, without terminating unrelated processes.

### D-024 — Fail-safe two-flag config snapshots, no malformed-file overwrite
Date: 2026-10-06 | Stage: 0
Pinned BepInEx 5b766a3 ConfigFile/TomlTypeConverter sources reviewed live: bool conversion is bool.Parse; SaveOnConfigSet defaults true and reload does not provide the required per-invalid-value default/reset semantics. Existing timestamp-only polling and initial unconditional Save are insufficient for safety validation. Chose bounded strict UTF-8 cfg subset validation for these two bools only, default true on missing/invalid/duplicate entries, warnings and content fingerprint; disable automatic save while applying validated values to existing typed ConfigEntry<bool>. Preserve existing file bytes; create defaults only when absent. A sibling .safe-mode marker forces effective safe mode and is checked independently of config edits. No XR exists, so all states stay flat. Source subset is not a replacement general BepInEx parser/menu, and visible Unity label remains unimplemented pending actual interop. No new dependencies or runtime gate claim.
Limits: BasePlugin constructor reads BepInEx config before Plugin.Load; pure layer cannot protect/prove that constructor's malformed-file behavior. Marker is plugin-level effective safety, not disabling/bypassing Doorstop/protection. Sources: https://raw.githubusercontent.com/BepInEx/BepInEx/5b766a3/BepInEx.Core/Configuration/ConfigFile.cs , https://raw.githubusercontent.com/BepInEx/BepInEx/5b766a3/BepInEx.Core/Configuration/TomlTypeConverter.cs , https://raw.githubusercontent.com/BepInEx/BepInEx/5b766a3/Runtimes/Unity/BepInEx.Unity.IL2CPP/BasePlugin.cs . Typed-entry integration/timer/unload/visible screen behavior still require actual approved entry. No fallback or 14-weapon/movement scope reduction.


## D-025 Steam version-retention observation (2026-10-06)
User screenshot of Steam Properties > Game Versions & Betas shows only 'Default public version' (last update 2026-03-10) and an access-code field; no developer-provided older/fixed branch is selectable. Local appmanifest (read-only) shows buildid 22267731 = TargetBuildID, AutoUpdateBehavior 0, no BetaKey. Conclusion: no supported Steam version pin exists for this project. The exact-build/hash guard only detects an update; it does not prevent one. No access code entered, no Steam setting changed. Residual risk: a surprise update can invalidate in-process assumptions; the installer/launch wrapper must refuse to run on any build other than 22267731. Whether to accept this limitation is still an explicit user decision (Q-018).







































Date: 2026-10-06 | Stage: 0
Static interop scan (Assembly-CSharp, 107 interop DLLs copied project-local to tools/interop, git-ignored, not redistributable): candidates PlayerController (fp_camera, camera_transform, camera_parent, camera_start_position/rotation, lock_mouse_input, lock_movement_input, lock_shoot_input; ~1885 members), PlayerInputController (Move/Look/Fire/Fire2/Reload/Jump/BloodyRush/SpecialAbility/Run/Interact/ArmL/ArmR/Start/Select), SimpleCameraFP, SetCameraTransform, CameraShake, RotateToMouseScript, LookAtCamera, Crosshair, PlayerHUD, UIMenuManager. Names are candidates only, not confirmed hooks.
Added src/core/RuntimeProbe.cs: a MonoBehaviour registered through Il2CppInterop ClassInjector, sampled from Update on the main thread, READ-ONLY (scene name, Camera.allCameras, PlayerController/PlayerInputController counts and camera refs, XR device state). Max 60 samples, 10 s interval, stops after 5 failures. No Harmony patches, no writes, no XR init. Automated verify.ps1 passed (incl. plugin build), reinstall via transaction (workspace 20261006T115147Z).
Runtime evidence (log, game launched with user authorization): scenes seen 'Intro' then 'MainMenu_2'. Intro: 1 camera 'Main Camera' fov 60 pos (0,1,-10). MainMenu_2: 1 camera 'ArenaCamera' under 'Camera Things', fov 40, pos ~(75..80,34,44), euler (0,270,0), tag MainCamera, stereoTarget=Both, no RenderTexture target; screen 2560x1440; xrLoadedDevice empty, xrEnabled=False; PlayerController=0, PlayerInputController=0 in menus. This confirms the probe runs on the main thread and reads real objects. It does NOT yet show the gameplay camera, so no hook/VR claim.
Next: need a gameplay-scene sample (PlayerController exists only in a run). That requires someone to start a run; cannot be read from static files. See docs/QUESTIONS.md Q-021.


### D-032 - Gameplay camera evidence from a real run (scene Maze_Small)
Date: 2026-10-06 | Stage: 0
Source: BepInEx/LogOutput.log of the user's run (user played movement, fire, dash, Q/E; double jump NOT exercised because the skill is not unlocked yet, so it stays unverified). 9 gameplay samples, 0 errors, 0 SAMPLE_FAILED, game closed gracefully.
Observed (log facts):
- Scene 'Maze_Small' at t=501 s; screen 1920x1200 in the run (menu was 2560x1440).
- 2 enabled cameras: gameplay 'Main Camera' (tag MainCamera, no target texture, stereoTarget=Both) and 'Mini Map Camera' (ortho, tag MiniMap, RenderTexture 'RenderTextureMiniMap', must NOT be made stereo).
- First gameplay sample (intro animation) had Main Camera at .../Prefab_Player_Level1/Hunter_For_Intro/Player_Anim_Armature/Root/CameraControler/Main Camera, fov 60, cullingMask 34603007. Later samples show it directly under AllThingsBaseLevel/Prefab_Player_Level1/Main Camera, fov 70 (one sample 90), cullingMask 59768831. So the camera is reparented after the intro and its FOV/mask change at runtime; any VR camera takeover must survive reparenting.
- PlayerController x1 ('Prefab_Player_Level1'), fp_camera and camera_transform both resolve to Prefab_Player_Level1/Main Camera; camera_parent='CameraControler'. PlayerInputController x1. Camera world Y is about 0.95 above the player root Y.
- Player root euler stayed (0,90,0) in all later samples while the camera yaw varied (20..309 deg), so look rotation lives on the camera (or a parent), not on the player root. Not yet confirmed which object; needs a targeted probe.
- Still no XR device (xrLoadedDevice empty, xrEnabled=False).
Meaning: the existence and identity of the gameplay camera and PlayerController are now confirmed at runtime (read-only). This is still NOT a hook: nothing has been written to the camera, no stereo/head/controller output exists. Stage 0 is NOT passed.
Open for the next probe: which Transform carries the look yaw/pitch, whether the game rewrites camera pos/rot/fov in Update/LateUpdate (fight for ownership), cursor/mouse-lock behavior, Canvas render modes for the HUD, other scenes (arena/corridor/boss).
State: loader + probe are still INSTALLED in the game folder; game is closed.


### D-033 - Read-only probe v2: look ownership, camera rewrites, cursor/time-scale from a live run
Date: 2026-10-06 | Stage: 0
Source: `BepInEx/LogOutput.log` of the user's live run while the game was still running (read-only extraction,
`artifacts/devtools/ProbeSummary.ps1`); deployed `VampireHuntersVR.Stage0.dll` SHA256
`EEC25064B8FF8ECDAED67B1061FB5024797698A82733BAAADC120B0DDA63D0DC` equals the project Release build, so the
logged behavior is exactly this source. Probe v2 added, on top of v1: ancestor-chain look tracking, an
Update→LateUpdate camera-rewrite counter, Canvas inventory and cursor/time-scale state. Still read-only.

Observed (log facts, 90 samples, 0 probe errors):
- Scene transitions: `Intro` t=0.2 → `MainMenu_2` t=7.5 → `Maze_Small` t=710. Screen 1920x1200 1:1 with the DPI-aware
  client capture. `xrLoadedDevice` empty / `xrEnabled=False` for every sample.
- Look ownership: `chain[0] 'Main Camera'` accumulated dYaw 3640 deg / dPitch 822 deg over the run; the parent chain
  (`Prefab_Player_Level1`, `AllThingsBaseLevel`) accumulated 0/0. So the look yaw+pitch are written to the camera's own
  local rotation in gameplay. The small nonzero sums on `Camera Things`/`Root` come from the MainMenu_2 `ArenaCamera`.
- Player root never rotates with look: `Prefab_Player_Level1` euler yaw was fixed at 135 (menu samples 0), matching
  D-032's finding on a different character. Movement frame and view are decoupled.
- The game rewrites the camera: over 126,091 watched frames, moved 9,875 (8%), rotated 4,451 (4%), fovChanged 169.
  In active gameplay samples it is far higher (e.g. moved 1,396 / rotated 856 in one 10 s window). Any future VR
  camera takeover must win this per-frame ordering, and the FOV is not stable (50/60/70/90 seen across the run).
- Camera reparenting happens more than once: `.../Prefab_Player_Level1/Main Camera` in gameplay, and at t=880 again
  `.../Prefab_Player_Level1/Player For Anim Parent/Player_For_Anim/Player_Anim_Armature/Root/CameraControler/Main Camera`.
  `PlayerController.camera_parent` reported 'CameraControler' while the real parent was `Prefab_Player_Level1` -> that
  field is a stale/dangling reference and must not be trusted at runtime.
- Mini Map Camera: orthographic, tag MiniMap, cullingMask 1048576, targetTexture `RenderTextureMiniMap`, and its yaw
  tracks the main camera yaw. Must NOT be stereoized.
- UI/input: cursor `None/True` x1, `Confined/True` x79 (menus), `Locked/False` x10 (gameplay). `timeScale=0` for 8
  samples (t=750-780, 800-820, 880) with the cursor released yet the canvas count unchanged at 153 -> the time-stopped
  panels (pause / level-up) stop time and unlock the cursor without adding canvases.
- Canvases: 153-154 in gameplay; every captured line was a WorldSpace `Canvas Ammo` under a `Prefab_PrimaryWeapon_*`
  with active=False, so the actual HUD overlay canvases sit beyond the probe's 12-line cap.

Not confirmed (next probe required): which component writes the camera between Update and LateUpdate; which canvases
are active in gameplay and in the time-stopped panels; player movement state (velocity/grounded/jump/dash) for
locomotion mapping; the weapon aim/muzzle transform for later controller aiming.

Tooling added (git-ignored under `artifacts/devtools`): `ProbeSummary.ps1` (full-run table + derived verdicts) and
`ProbeReport.ps1` (last-N-sample extractor). Checklist committed as `docs/playtests/PLAYTEST-0-RUNTIME.md`.
No VR write, no XR init, no stereo/head/controller output. Stage 0 remains NOT PASSED.


### D-034 - Probe v3 deployed: component ownership, de-duplicated UI inventory, input trace
Date: 2026-10-06 | Stage: 0
Deployment: uninstall (stock verified) then reinstall through the tested transaction engine; `ADDITIONS_INSTALLED`,
229 files, workspace `20261006T131418Z`. Deployed plugin SHA256
`F569A480FC58793F46A19B6192EAFD21CF5A630E7D27868AABE951DEDF23AD45` equals the project Release build output, and
the loader's `winhttp.dll` is present. `RuntimeProbe.cs` v3 keeps every v2 section and adds, still read-only:
(d) a legacy-Input trace, (e) MonoBehaviour class names on the camera and its first three ancestors, (f) a
de-duplicated Canvas inventory with the active/overlay canvases named, (g) player motion, direct children and
per-sample travel. Sample cap raised 90 -> 300 (up to 50 min at 10 s).

Tooling corrections recorded (not game bugs):
- First v3 deploy (`B70F220C…`) ran, but its component dump printed `MonoBehaviour` for every entry because
  `GetComponents<MonoBehaviour>()` returns the requested base type under Il2CppInterop. Fixed by reading the real
  class through `IL2CPP.il2cpp_object_get_class` + `il2cpp_class_get_namespace`/`il2cpp_class_get_name`, rebuilt and
  redeployed. The buggy build's log was kept at `artifacts/tx/run/run-v3a-menu-20261006/`.
- Launching `Vampire Hunters.exe` directly ends the process during start-up with
  `InvalidOperationException: Steamworks is not initialized.` (Unity `Player.log`); the game must be started through
  Steam (`steam://rungameid/2206270`). No plugin error was involved (`BepInEx/ErrorLog.log` stayed empty).
- `ProbeSummary.ps1` needed three fixes: `Measure-Object` was pointed at a parent object instead of the child
  property, child probe lines are indented so the regexes needed `\s+`, and PowerShell's `-f` binds tighter than `+`,
  so a concatenated format string failed; the block now uses interpolation. Each failed run was re-run after the fix
  and the archived v2 log still produces identical numbers (regression checked), so no result was inferred from a
  failed run.

First v3 facts (menu/intro only, game still at the main menu):
- Camera ownership, first real names: `Main Camera` (Intro) carries only
  `UnityEngine.Rendering.Universal.UniversalAdditionalCameraData`; the menu camera `ArenaCamera`
  (path `Camera Things/ArenaCamera`) carries `UniversalAdditionalCameraData` + **`SetCameraTransform`**. That is a
  static-scan candidate now confirmed attached to a camera GameObject at runtime. The gameplay camera's own
  components still need a run.
- UI volume and identity: Intro 1 canvas; menu 25-26 distinct canvases (`WorldSpace:16-17, ScreenSpaceOverlay:9`),
  7 active overlays. Named overlays with their sorting orders: `Canvas - Alert` 30001, `Canvas - AchievementReward`
  999, `Canvas - Overlay` 2, `Canvas`/`Canvas - Menu`/`Canvas - Credits` 1, `Canvas - StageSelection` 0, plus
  inactive `Stage Selection Button` and `Mockup UI`. Overlay scaleFactor is 1.11, i.e. the UI is not 1:1 with the
  1920x1200 screen -> in-VR UI work must account for that scaling, and v2's "all canvases are WorldSpace weapon ammo"
  reading was an artifact of its 12-line cap.
- Input trace works (`ok=True`) with the legacy Input path: Mouse X/Y, `mouseScrollDelta`, mouse buttons are readable,
  so a plain play session is self-labelling without asking the player to hold still.
- Verifier: with the game folder stock the full `tests/verify.ps1` finishes green
  (`ENTRY_PLAN_VERIFIED: stock=121; loader=228; proposed=229`, final `VERIFIED`); while the loader is installed the
  only failing step is that same stock-inventory invariant, which is expected and not a code failure.

Meaning: the remaining Stage 0 unknowns are now observable in one ordinary run. Still no VR write, no XR init, no
stereo/head/controller output, and nothing has been written to a game object. Stage 0 remains NOT PASSED.


### D-035 - Full v3 gameplay run: look ownership, camera reparenting, HUD inventory, motion reach
Date: 2026-10-06 | Stage: 0
Artifact: `artifacts/tx/run/run-v3-live-20261006/LogOutput.v3.log`, 350,658 bytes, SHA256
`1695689A95611260906ADD2E002032D63AC6FC68181C529BDC50B6876B67E49F`, 2,145 lines / 2,038 probe lines.
Run: `samples=48 probeErrors=0`, scenes `Intro(1), MainMenu_2(19), Maze_Small(28)`, transitions at t=0.2 Intro,
t=2.3 MainMenu_2, t=163.4 Maze_Small, t=436.1 MainMenu_2 (one ordinary stage played to the end of the run).
`xrLoadedDevice='' xrEnabled=False` on every sample, so nothing in this session enabled XR. `ErrorLog.log` holds only
the two Steam breakpad lines (132 bytes, no plugin error). Extracted with
`ProbeSummary.ps1 -OutPath artifacts/tx/run/probe-summary-v3live.txt -Activity`.

**Look ownership (R1-R3, R5) - resolved for gameplay.**
- The view direction lives on the **`Main Camera` transform's local rotation**: per-sample local euler yaw took many
  values (0.64/90.71/116.40/206.58/285.99...) with `dYaw` 5.1-699 per 10 s and `dPitch` 12-170, i.e. all mouse look
  accumulated there.
- The **player root never takes yaw from the mouse**: `player 'Prefab_Player_Level1'` printed exactly two euler
  values in the whole run, `(0,0,0)` (menu, no PlayerController) and `(0,270,0)` (every gameplay sample), and
  `playerFwd` stayed `(-1,0,-0)` while `camFwd` swept `(-0.13,0,-0.99)` -> `(0.44,-0.07,0.89)` with
  `fwdAngle` 74-175 deg. The root holds a fixed spawn yaw; movement direction is computed elsewhere (camera-relative)
  and the character body is not turned to face the view.
- Consequence for VR: head yaw/pitch goes on the camera node; the body/aim facing needs a separate injection point
  (candidate `Player For Anim Parent`, a direct child of the root) - not yet proven.
- The aggregated `lookOwners` block in the summary mixes chain depths across scenes (depth 1 is the menu
  `Camera Things` in one scene and `Prefab_Player_Level1` in another), so its sums are indicative only. The per-sample
  `lookChain` lines are the valid evidence and say: gameplay chain = `Main Camera` -> `Prefab_Player_Level1` ->
  `AllThingsBaseLevel`, `trackedFrames=33044`.

**Camera identity and reparenting (V3-1).** In one run `Main Camera` was observed under **three** paths:
1. `AllThingsBaseLevel/Prefab_Player_Level1/Main Camera` - normal gameplay view, a direct child of the player root.
2. `AllThingsBaseLevel/Prefab_Player_Level1/Player For Anim Parent/Player_For_Anim/Player_Anim_Armature/Root/CameraControler/Main Camera` - during the stage entry/intro sequence.
3. `AllThingsBaseLevel/Prefab_Player_Level1/Hunter_For_Intro/Player_Anim_Armature/Root/CameraControler/Main Camera` - the pre-run variant of the same chain.
The camera GameObject itself carries only `UnityEngine.Rendering.Universal.UniversalAdditionalCameraData` in all three
poses; its root has `PlayerController, BobAnimation, BobAnimation, ShakeAnimation, Tutorial,
UnityEngine.InputSystem.PlayerInput, PlayerInputController`. The menu camera `Camera Things/ArenaCamera` is the only
camera found with `SetCameraTransform`. So the menu transform writer is identified but the **gameplay per-frame camera
writer is still only narrowed to "a script on the player root"**; `camWrites` shows the camera rewritten in 100% of
frames at t=233 (moved=1438/1438, rotated=486) and 34-100% in combat samples, so the writer runs every frame. The
reparenting means a VR head injection must survive a parent change mid-session.

**Other camera facts.** Gameplay `Main Camera`: fov=70, depth=-1, tag=MainCamera, mask=59768831, no targetTexture,
stereoTarget=Both. fov set across the run 50/60/70 (90 was seen in the v2 menu); `camWrites fovChanged` shows 28-30
rewrites per combat sample (~2% of frames) - a small periodic fov change consistent with a bob/punch effect, recorded
as an observation, not a proven cause. `Mini Map Camera`: ortho=True, tag=MiniMap, mask=1048576,
targetTex=RenderTextureMiniMap, sits at the player's x/z 5 units higher, and its euler pitch/yaw track the main camera
(`(90, 116.58, 0)` while the main camera was `(358.54, 116.58, 0)`), so the minimap rotates with the view and must not
be stereoized later (R13).

**Player motion (V3-2).** The player root carries a **kinematic Rigidbody** (velocity 0, `gravity=True`) *and* a
**CharacterController height=3.50 radius=0.50**; the CharacterController velocity is the real mover. Observed peaks
~12 m/s horizontal (`(-12.13,-1.58,0.21)`, `(10.51,-1.17,6.02)`), `grounded` flipping True/False, and upward spikes
`(0,8.43,-0.11)` / `(6.83,8.22,-9.35)` while airborne = jump (vy ~+8.2-8.4). `playerTravel` totalled **999.54 m over
28 samples** (up to 73.76 m per 10 s) and `worldPos.y` moved between 2.00 and 7.02, so vertical movement is real.
Double jump was not observed (still not unlocked per Q-021).























































  by v4).

**Probe v4 built for exactly these gaps** - `RuntimeProbe.cs`, still read-only, no Unity writes, no XR init:
(h) every-frame edge logging of `Time.timeScale` changes (`[Probe] TIMESCALE 1.00->0.00 ...`) and of ESC key-down
    (`[Probe] ESC_DOWN ...`), each carrying the active-UI signature inline;
(i) a 0.5 s active-Canvas set watcher that logs **only on change** (`[Probe] UICHANGE ... added=[..] removed=[..]`),
    which is what catches a 2-4 s death panel that a 10 s sample cannot;
(j) a depth-3 / 40-node dump of the player's `Player For Anim Parent` subtree with real component class names
    (`[Probe] animTree ...`, `[Probe] tree d1 ... comps=[..]`) plus the `Prefab_*` direct children, targeting V3-3
    (weapon/aim/muzzle transform).
Plugin banner is now `[Stage0] PROBE_STARTED probe=v4 ...`. Release build: zero warnings/zero errors
(`TreatWarningsAsErrors`), 37,888 bytes, SHA256
`DA79CC1A48C563FA703EB522050B4926BF3E4B6BCA22899D02D30D7D5A71C2FF`. Deployment is recorded separately once the
transaction completes. No VR write, no XR init, no stereo/head/controller output; Stage 0 remains NOT PASSED.


### D-037 - Probe v4 deployed through the tested reversible path (stock verified, then reinstall)
Date: 2026-10-06 | Stage: 0
Sequence used - the same reversible path as every previous probe swap, never a raw file copy:
1. `Install-Stage0.ps1 -Action Uninstall` -> `ADDITIONS_QUARANTINED_STOCK_VERIFIED`; the six owned roots
   (`.doorstop_version`, `BepInEx`, `changelog.txt`, `doorstop_config.ini`, `dotnet`, `winhttp.dll`) were quarantined,
   `baselib.dll` stayed because it is a stock file, and the journal ended `Recovered` in workspace
   `20261006T131418Z`. The game folder is back to its 121 stock files.
2. Full verifier against the stock folder: `RESULT 12 passed`, `21 passed`, `27 passed`, `18 passed`, `23 passed`,
   0 failed; final `VERIFIED`; `ENTRY_PLAN_VERIFIED: stock=121; loader=228; proposed=229`
   (logs `artifacts/verifier-v4.stdout.log` / `.stderr.log`).
3. `Install-Stage0.ps1 -Action Install` -> `ADDITIONS_INSTALLED`, 229 files, workspace `20261006T133930Z`
   (six roots published, journal `Installed`; pointer `artifacts/transactions/current.txt` updated).
Deployed `BepInEx/plugins/VampireHuntersVR.Stage0.dll`: **37,888 bytes, SHA256
`DA79CC1A48C563FA703EB522050B4926BF3E4B6BCA22899D02D30D7D5A71C2FF`**, byte-identical to the Release build output.
The plugin is the only file in `BepInEx/plugins`; `BepInEx/config` does not exist until the first run (expected -
the v4 run will recreate it).

Interruption note (agent tooling, not a game state problem): the first Uninstall invocation exceeded the agent tool's
30 s command limit and was killed after writing `QuarantineIntent` for `.doorstop_version` but before any move. It was
re-run as a detached process; the engine re-derived the journal state, re-verified the payload, quarantined all six
roots and finished `Recovered` with the stock inventory verified, so the killed attempt left **no** partial state and
modified no game file. Cost of the two patient waits, not a defect.

No VR write, no XR init, no stereo/head/controller output; Stage 0 remains NOT PASSED and the gate sequence has not
begun.


### D-038 - UEVR rejected on engine mismatch; UUVR promoted to the first item of Stage 1 (hybrid path, user decision)
Date: 2026-10-06 | Stage: 0 research, binding on Stage 1
User question that opened this: "can't UEVR be used for this game?"
Evidence, checked live the same day, not from memory:
- UEVR is `praydog/UEVR`, "Universal **Unreal Engine** VR Mod (4/5)", README "Supported Engine Versions 4.8 - 5.4".
  It patches UE `UObject`/`GNames`/engine RHI. This target is Unity 2022.3.37f1 IL2CPP (`GameAssembly.dll`
  47,464,960 B, `UnityPlayer.dll` 30,894,512 B, `Vampire Hunters_Data/il2cpp_data`, `globalgamemanagers`).
  **Rejected, not merely untested**: no Unreal symbols exist to scan, so injection cannot produce any effect.
- Unity analogue found and re-verified: `Raicuparta/uuvr`, "Universal VR mod for Unity games", plugin id
  `raicuparta.uuvr-modern`, version **0.4.0**, **GPL-3.0**, last push **2025-07-03**, 234 stars, 30 open issues,
  not archived.
  - **Same loader stack as this project**: `UuvrPlugin : BasePlugin` with `using BepInEx.Unity.IL2CPP` and
    `Il2CppInterop.Runtime.Injection.ClassInjector` plus Harmony. BepInEx 6 IL2CPP, **not MelonLoader**, so both
    plugins can coexist in one BepInEx install and no loader migration is implied.
  - Build configurations are `legacy|modern x mono|il2cpp`; this game is `modern-il2cpp` (Unity 2020+ IL2CPP).
  - Provides: stereo, camera, head tracking (`VrCamera`, `VrCameraManager`, `UuvrPoseDriver`) and UI redirect
    (`VrUi`, `CanvasRedirect`). Does **not** provide motion controls, physics hands, holsters, reload, melee, IK or
    an in-VR settings menu - i.e. none of this project's differentiators (D-005 14-slot weapon bank).
  - Installer behaviour: `TargetDLLs = Assembly-CSharp.dll`; copies `CopyToGame/Data` and `CopyToGame/Plugins/x64`
    into the game **on every game start (overwrites)**; deletes any `openvr_api`, `openxr_loader`, `UnityOpenXR`,
    `XRSDKOpenVR`, `ucrtbased` DLLs. Checked on this install: all five are **absent** -
    `Vampire Hunters_Data/Plugins` contains only stock x86_64 natives (`lib_burst_generated`,
    `Microsoft.Xbox.Services.GDK.C.Thunks`, `sqlite3`, `steam_api64`, `XCurl`, `XGameRuntime.Thunks`,
    `XGamingRuntimeThunks`) - so that deletion step is a no-op here.
  - Version caution: UUVR's il2cpp patcher references `BepInEx.Unity.Mono.Preloader` 6.0.0-be.674 while this
    project pins 6.0.0-be.788+5b766a3. Compatibility must be proven in the spike, never assumed.
Chose (put to the user as a decision with options; user picked the hybrid):
- Keep this project's own plugin, verifier, safe mode, config validation and journal-driven reversible
  transactions as the base. Continue Stage 0 unchanged (Run 4 -> gate sequence). UUVR is **promoted from
  "study only" to the first item of Stage 1**: after the Stage 0 gate passes, run a bounded UUVR spike before any
  from-scratch stereo work.
- Reason: the operating principle "reuse before build". Same loader, install already reversible through the
  transaction engine, and the spike answers the project's highest-risk unknown (does stereo plus 1:1 head tracking
  work at all on this Unity 2022.3 IL2CPP build) for about one headset session instead of a multi-week Stage 2.
  UUVR cannot replace the hands/IK/weapons/UI work, so it is an accelerator, not a substitute.
Pre-committed spike verdict rules, fixed now so the result cannot be reinterpreted later:
- PASS requires visible in-headset evidence: real stereo with correct separation/depth, 1:1 head tracking with no
  forced game camera motion, sane desktop mirror, no crash or black screen, and the game still playable flat with
  VR off.
- FAIL/PARTIAL: nothing visible when enabled, eye-locked or doubled image, game camera fighting the pose in any
  state (menu, level-up, death), crash, or failure to restore the game to stock afterwards.
- Adoption is a separate later decision (D-039 or later) taken only on a PASS. Before the spike runs, UUVR and its
  patcher files must be added to the owned-file transaction manifest and to the verifier's expected file set, so
  `stock=121` stays provable after removal.
Licence policy, stated so this is not a silent downgrade: GPL-3.0 is copyleft, not permissive. Reading it for
approach, and running it as a separate plugin for personal, non-distributed testing, creates no distribution
obligation. **Distributing a build that links or bundles UUVR code makes the combined work GPL-3.0**; that choice
is deferred to packaging (Stage 10) and must be an explicit decision there. No UUVR source is copied into this
repository.
Consequence for the "other mods on the game" question: no other mod is installed on Vampire Hunters (D-026 user
report), so camera contention between two mods begins only with this spike.
Sources: https://github.com/praydog/UEVR ,
https://api.github.com/repos/Raicuparta/uuvr ,
https://raw.githubusercontent.com/Raicuparta/uuvr/main/Uuvr/UuvrPlugin.cs ,
https://raw.githubusercontent.com/Raicuparta/uuvr/main/Uuvr.Patcher/UuvrPatcher.cs ,
https://raw.githubusercontent.com/Raicuparta/uuvr/main/Uuvr.Patcher/Uuvr.Patcher.csproj ,
https://raw.githubusercontent.com/Raicuparta/uuvr/main/Directory.Build.props
No install, no injection, no game file changed by this decision; Stage 0 remains NOT PASSED and the gate sequence
has not begun.

### D-039 - Run 4 (probe v4) closes R9/R10 and re-scopes GAP-001: next step is static pinning, not another run (2026-10-06)
Evidence: `artifacts/tx/run/run-v4-live-20261006/LogOutput.v4.log` (381,901 B,
`3F6A6A2CC6534B1D2B5F7E339C5CCA9692EF8401403C1F25A004A9D52B4AA0E0`), 35 samples, `probeErrors=0`,
`xrEnabled=False` throughout, summary `artifacts/tx/run/probe-summary-v4live.txt`; full table in
`docs/playtests/PLAYTEST-0-RUNTIME.md` ("Run 4 evidence"). Marker counts: `TIMESCALE` x34, `ESC_DOWN` x2,
`UICHANGE` x4, `animTree` x29.
- One ordinary user run - which included an ESC press, both auxiliary weapons, several level-ups and a deliberate
  death - closed the two branches that three earlier runs could not: the **ESC pause path** (`ESC_DOWN t=51.1` ->
  `TIMESCALE 1.00->0.00 cursor=None/True`, and `0.00->1.00` on the second press) and the **death panel**
  (`TIMESCALE 1.00->0.00 t=318.0 frame=45224 scene='Maze_Small' cursor=Locked/False ui=5=[Canvas - Alert/Tooltip,
  Canvas - Damage Texts, Canvas - Health Bars, Canvas - Screens, Player HUD]`, returning to `MainMenu_2` at t=322.5).
  The 10 s sample cadence that missed the panel in v3 is no longer the limiting factor.
- Verified UI discriminators, to be reused by every later stage (settings, in-VR menus, HUD conversion): those same
  five canvases are active in all three stopped states, so the state is told apart by the surroundings - **pause** =
  five canvases with the cursor released, **level-up** = five canvases **plus `Level Up/NewNewCanvas`** (15
  occurrences, cursor released), **death** = five canvases with the cursor still `Locked/False`. The death panel
  itself sits inside `Canvas - Screens`, whose canvas-level active state does not change at death, so canvas-set
  diffing alone can never detect death; the time-scale edge plus cursor state is the reliable signal.
- Secondary fire is now covered by data (`f1=1466f/13down`, plus `q=82`/`e=68` frames for the auxiliary slots), so
  the D-036 "blocked until a secondary weapon is unlocked" caveat is retired.
- Decision: **no probe v5 and no fifth playtest run for these items.** The remaining Stage 0 unknowns are static:
  (a) which player-root MonoBehaviour method writes `Main Camera` - `PlayerController`, `BobAnimation` x2,
  `ShakeAnimation`, `Tutorial`, `PlayerInputController` are the only components on the camera's parent node - to be
  pinned by resolving IL2CPP methods/metadata; and (b) the live muzzle/aim pivot, where `Gun.L`, `Gun.R`, `Root` and
  `Player_Anim_Armature` are constant bone proxies (LIM-003) and the real pivot is at depth 4 under `Root`
  (children=4) or on the active `*Arm` leaf. A depth-4 dump stays available as a fallback but is not requested now.
- Decision: the `UICHANGE` suppression caused by `WatchTimeScale()`'s re-baseline is **accepted as designed** and
  documented (TOOL-005), not patched; the inline `ui=` field on the `TIMESCALE` line carries the same information.
- New Stage 1 input on record, not acted on yet: the game rewrites the gameplay camera transform in 68% of frames
  (`moved=31291`, `rotated=14913` of 45861 watched) and briefly rewrites **FOV** (29-30 frames per dash at t=108,
  168, 218, 238, 298; 113 gameplay-start; 273/185 on the menu camera). Head pose must survive both. `Death Volume`
  is a child of the player root (OBS-002), which constrains any later hierarchy surgery.
- Reservation note: D-038 said the UUVR adoption decision would be "D-039 or later"; this run-evidence decision takes
  D-039, so adoption is now D-040 or later and remains strictly conditional on a spike PASS.
Consequence: Stage 0 remains NOT PASSED - the recon gaps are closed, but the gate sequence (install -> menu-only
launch -> config behaviors -> removal -> stock relaunch) has not been run, Q-023 (UUVR authorization) is still open,
and no VR/XR/stereo code exists. Next actions: static camera-writer pinning, then the gate sequence.
No install, no injection, no game file changed by this decision.

### D-040 - UUVR mechanism re-verified live and this build's XR symbols pre-checked: an accelerator worth ~25-35% of Phase 1, concentrated in the riskiest part (2026-10-06)
Stage: 0 research, binding on the Stage 1 spike (D-038). Opened by the user's question "would UUVR shorten Stage 1
much?" - answered with live reads, not memory.
- **Mechanism, read from source the same day** (`Uuvr/UuvrPlugin.cs`, `Uuvr/UuvrPoseDriver.cs`, `Uuvr/VrCamera/`,
  `Uuvr/VrUi/`):
  - `UuvrPlugin.Load()` is a BepInEx 6 IL2CPP `BasePlugin` that does `Harmony.CreateAndPatchAll(assembly)` and
    `Il2CppInterop...ClassInjector.RegisterTypeInIl2Cpp<...>` for `VrCamera`, `VrCameraOffset`, `CanvasRedirect`,
    `UiOverlayRenderMode`, `VrUiCursor`, `VrUiManager`, `FollowTarget`, `UuvrPoseDriver`, `UuvrBehaviour`, `UuvrCore`,
    `VrCameraManager`, `CanvasRedirectPatchMode`, `ScreenMirrorPatchMode`, then `UuvrCore.Create()`.
  - `UuvrPoseDriver` looks up `UnityEngine.XR.InputTracking.GetLocalRotation` by reflection (fallbacks
    `UnityEngine.VR.InputTracking`, then `UnityEngine.VRModule`); if the method is missing it logs
    "Failed to find InputTracking.GetLocalRotation. Destroying UUVR Pose Driver." and destroys itself. When found it
    does `transform.localRotation = (Quaternion)method.Invoke(null, {2 /* XRNode.CenterEye */})` from **Update,
    LateUpdate and OnBeforeRender**, after calling `XrDevice.DisableAutoXRCameraTracking(camera, true)` when that
    method exists (its fallback would be `camera.SetStereoViewMatrix`, left commented out as crash-prone).
  - **Consequence for scoping:** UUVR implements no per-eye matrices of its own. It leans on Unity's built-in XR
    path (`XRModule`/`VRModule`, `XRSettings`, `Camera` stereo APIs, `InputTracking`) plus the native XR plugin DLLs
    its patcher copies into `_Data/Plugins/x64`, and it applies the head pose as **rotation only, onto its own offset
    transform**. Translation, world scale, crouch/lean, and everything that fights the game's camera code are still
    this project's Stage 1 work.
- **New static evidence for this exact build** (read-only metadata scan, no install;
  `artifacts/tx/recon/xr-symbols-20261006.txt`, `global-metadata.dat` 10,889,004 B, SHA256 `E8F040A9…` unchanged from
  the Stage 0 record): the XR module survived engine-code stripping - `UnityEngine.XR` x47, `XRModule` x38,
  `XRSettings` x4 with `StereoRenderingMode.MultiPass/SinglePass/SinglePassInstanced/SinglePassMultiView`,
  `InputTracking` x4, `XrDevice` x4, `DisableAutoXRCameraTracking` x1, `XRDisplaySubsystem` x8, `InputDevices` x13,
  `Camera.get_stereoEnabled`/`get_stereoTargetEye`/`GetStereoNonJitteredProjectionMatrix`, `SetStereoViewMatrix` x2,
  `stereoTargetEye` x3, `SinglePassInstanced` x1, `centerEyeRotation/Position/Velocity`, `UnityEngine.VR` x36.
  Absent: `XRGeneralSettings`, `StereoRenderingPath`, `GetLocalRotation`, and (re-confirming D-038)
  `openxr_loader`/`UnityOpenXR`/`XRSDKOpenVR` - `_Data/Plugins/x64` still holds only the 7 stock natives, so UUVR's
  own payload supplies those and its five-DLL deletion stays a no-op. The single unresolved symbol is exactly the one
  UUVR's pose driver hard-requires, so that is the spike's first question, not a settled fact.
- **Time accounting (estimate, stated as such, not a promise):** it removes the skill's *large, high risk* Stage 2
  (native per-eye is rung 1 of the ladder) and the pose-application half of Stage 1, and gives a base for the Stage 8
  screen-space-UI redirect - roughly **25-35% of Phase 1 effort, concentrated at the front where the go/no-go risk
  sits**. It does not touch Stage 1's camera-ownership fight (Run 4 measured the game rewriting the gameplay camera
  transform in 68% of frames and its FOV in 29-30-frame bursts per dash, plus the menu `ArenaCamera`, the intro
  `CameraControler` and `MiniMap Camera`), nor Stages 3-7, 9 and 10, which are the bulk of the conversion.
- Decision: the spike stays exactly as D-038 fixed it - still gated on Q-023 and on the Stage 0 gate, still judged by
the overlay is a build artifact only until the gate run. Remaining Stage 0 work: the static camera-writer pinning,
then the gate sequence (owned-file install -> menu-only launch -> config behaviors -> removal -> stock relaunch).
Reservation note: D-043 said the UUVR adoption decision is now "D-044 or later"; this entry takes D-044, so adoption is
now **D-045 or later** and remains strictly conditional on the spike PASS.
Sources: installed-build `global-metadata.dat` scan (this session, read-only);
`artifacts/transactions/20261006T131418Z/quarantine/BepInEx/interop/UnityEngine.IMGUIModule.dll` (D-030 quarantine).

### D-040 - UUVR mechanism re-verified live and this build's XR symbols pre-checked: an accelerator worth ~25-35% of Phase 1, concentrated in the riskiest part (2026-10-06)
Stage: 0 research, binding on the Stage 1 spike (D-038). Opened by the user's question "would UUVR shorten Stage 1
much?" - answered with live reads, not memory.
- **Mechanism, read from source the same day** (`Uuvr/UuvrPlugin.cs`, `Uuvr/UuvrPoseDriver.cs`, `Uuvr/VrCamera/`,
  `Uuvr/VrUi/`):
  - `UuvrPlugin.Load()` is a BepInEx 6 IL2CPP `BasePlugin` that does `Harmony.CreateAndPatchAll(assembly)` and
    `Il2CppInterop...ClassInjector.RegisterTypeInIl2Cpp<...>` for `VrCamera`, `VrCameraOffset`, `CanvasRedirect`,
    `UiOverlayRenderMode`, `VrUiCursor`, `VrUiManager`, `FollowTarget`, `UuvrPoseDriver`, `UuvrBehaviour`, `UuvrCore`,
    `VrCameraManager`, `CanvasRedirectPatchMode`, `ScreenMirrorPatchMode`, then `UuvrCore.Create()`.
  - `UuvrPoseDriver` looks up `UnityEngine.XR.InputTracking.GetLocalRotation` by reflection (fallbacks
    `UnityEngine.VR.InputTracking`, then `UnityEngine.VRModule`); if the method is missing it logs
    "Failed to find InputTracking.GetLocalRotation. Destroying UUVR Pose Driver." and destroys itself. When found it
    does `transform.localRotation = (Quaternion)method.Invoke(null, {2 /* XRNode.CenterEye */})` from **Update,
    LateUpdate and OnBeforeRender**, after calling `XrDevice.DisableAutoXRCameraTracking(camera, true)` when that
    method exists (its fallback would be `camera.SetStereoViewMatrix`, left commented out as crash-prone).
  - **Consequence for scoping:** UUVR implements no per-eye matrices of its own. It leans on Unity's built-in XR
    path (`XRModule`/`VRModule`, `XRSettings`, `Camera` stereo APIs, `InputTracking`) plus the native XR plugin DLLs
    its patcher copies into `_Data/Plugins/x64`, and it applies the head pose as **rotation only, onto its own offset
    transform**. Translation, world scale, crouch/lean, and everything that fights the game's camera code are still
    this project's Stage 1 work.
- **New static evidence for this exact build** (read-only metadata scan, no install;
  `artifacts/tx/recon/xr-symbols-20261006.txt`, `global-metadata.dat` 10,889,004 B, SHA256 `E8F040A9…` unchanged from
  the Stage 0 record): the XR module survived engine-code stripping - `UnityEngine.XR` x47, `XRModule` x38,
  `XRSettings` x4 with `StereoRenderingMode.MultiPass/SinglePass/SinglePassInstanced/SinglePassMultiView`,
  `InputTracking` x4, `XrDevice` x4, `DisableAutoXRCameraTracking` x1, `XRDisplaySubsystem` x8, `InputDevices` x13,
  `Camera.get_stereoEnabled`/`get_stereoTargetEye`/`GetStereoNonJitteredProjectionMatrix`, `SetStereoViewMatrix` x2,
  `stereoTargetEye` x3, `SinglePassInstanced` x1, `centerEyeRotation/Position/Velocity`, `UnityEngine.VR` x36.
  Absent: `XRGeneralSettings`, `StereoRenderingPath`, `GetLocalRotation`, and (re-confirming D-038)
  `openxr_loader`/`UnityOpenXR`/`XRSDKOpenVR` - `_Data/Plugins/x64` still holds only the 7 stock natives, so UUVR's
  own payload supplies those and its five-DLL deletion stays a no-op. The single unresolved symbol is exactly the one
  UUVR's pose driver hard-requires, so that is the spike's first question, not a settled fact.
- **Time accounting (estimate, stated as such, not a promise):** it removes the skill's *large, high risk* Stage 2
  (native per-eye is rung 1 of the ladder) and the pose-application half of Stage 1, and gives a base for the Stage 8
  screen-space-UI redirect - roughly **25-35% of Phase 1 effort, concentrated at the front where the go/no-go risk
  sits**. It does not touch Stage 1's camera-ownership fight (Run 4 measured the game rewriting the gameplay camera
  transform in 68% of frames and its FOV in 29-30-frame bursts per dash, plus the menu `ArenaCamera`, the intro
  `CameraControler` and `MiniMap Camera`), nor Stages 3-7, 9 and 10, which are the bulk of the conversion.
- Decision: the spike stays exactly as D-038 fixed it - still gated on Q-023 and on the Stage 0 gate, still judged by
  D-038's pre-committed PASS/FAIL rules. This entry adds a cheaper pre-check to the spike brief's unknowns (XR symbol
  presence) and records what UUVR does and does not shorten. No UUVR file was downloaded, installed, or written; no
  game or loader file changed.
- Reservation note: D-038 reserved "adoption" for D-039-or-later; D-039 and this entry took those numbers, so the
  adoption decision (only on a spike PASS) is now **D-041 or later**.
Consequence: Stage 0 remains NOT PASSED and unchanged - this was research, not progress on the gate. Next actions
unchanged: static camera-writer pinning, then the Stage 0 gate sequence; Q-023 answered before the spike.
Sources: https://raw.githubusercontent.com/Raicuparta/uuvr/main/Uuvr/UuvrPlugin.cs ,
https://raw.githubusercontent.com/Raicuparta/uuvr/main/Uuvr/UuvrPoseDriver.cs ,
https://api.github.com/repos/Raicuparta/uuvr/contents/Uuvr/VrCamera ,
https://api.github.com/repos/Raicuparta/uuvr/contents/Uuvr/VrUi
No install, no injection, no game file changed by this decision.

### D-041 - Prior-art map: which Unity flat-to-VR work can be reused, and which is study-only (2026-10-06)
Stage: 0 research, feeds the Stage 1 spike (D-038) and Stage 2. Opened by the user's question "can't we build it by
referencing Unity games that already have a VR mode, or a VR mod?" - answered with live reads the same day.

Reuse rights differ by class, so four classes are kept apart on purpose:
1. **Commercial Unity games that shipped a VR mode** (Subnautica-, The Forest-, Obduction-class titles) - their code is
   neither obtainable nor licensed here: **precedent only, never source**. What they establish: Unity's built-in stereo
   path (a stereo camera plus `XRSettings.stereoRenderingMode` - MultiPass / SinglePassInstanced) is sufficient for a
   game that shipped flat, with no hand-written per-eye math. Same conclusion D-040 read out of UUVR's source, now
   corroborated by shipped products.
2. **MIT-licensed community VR mods - study AND reuse (with attribution)**:
   - `DrBibop/RoR2VRMod` (Risk of Rain 2, Unity IL2CPP): MIT, C#, 65 stars, last push 2026-01-20, built on
     BepInEx + HookGenPatcher. Its `VREnabler/VREnabler.cs` is the closest structural analogue to this project: the game
     shipped **without XR plugin files**, so the patcher copies `UnityOpenXR.dll` and `openxr_loader.dll` into
     `_Data/Plugins/x86_64` and writes `_Data/UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json`; its older
     `globalgamemanagers` "EnableVROptions" patch is **commented out in HEAD**, i.e. the file-drop path is the
     maintained one. D-040 found the same absence in this build (`openxr_loader`/`UnityOpenXR` not present), so this is
     the recipe to consult if the spike must supply the XR payload itself. The README also records a real trap worth
     keeping: SteamVR's "Use Desktop Game Theatre while SteamVR is active" must be off or the game starts flat.
   - `Raicuparta/nomai-vr` (Outer Wilds): MIT, C#, 400 stars, last push 2025-03-02, topics `unity`/`steamvr`/`vr` - a
     full 6DoF + motion-control conversion; usable as a camera-ownership and UI-redirect pattern library, same author as
     UUVR.
3. **Non-permissive or unlicensed community VR mods - approach only, no code enters this repository**:
   `whateverusername0/VRTRAKILL` (ULTRAKILL, Unity IL2CPP, **AGPL-3.0**, 189 stars) - linking or bundling it would make
   the combined work AGPL-3.0; `xPrinny/GunfireRebornVRMod` (Gunfire Reborn - a Unity IL2CPP first-person roguelite,
   i.e. the nearest genre to this game - **no license field at all**, fork, last push 2025-01-18). Both are read for
   approach only, exactly as D-038 fixed for UUVR.
4. **Living index**: `Rhiyo/flat2vr-list` - "PC games that weren't originally designed to be played in VR but have a mod
   or option to". Use it as the search index, then verify each entry live; it is maintained and changes.

What this does and does not change:
- The Stage 1/2 default is unchanged but **better supported**: Unity's built-in XR plus an XR plugin payload is what
  both the MIT precedent and UUVR do, so the spike's open question narrows to "does this build's retained XR module


















### D-043 - Q-023 answered by agent decision under user delegation: the bounded UUVR spike is authorized, still gated on the Stage 0 pass (2026-10-06)
Stage: Stage 0/1 boundary; answers Q-023. The user replied to the Q-023 install question with "decide the better option
yourself", delegating the choice, so the decision and its reasoning are recorded here instead of as a user statement.
- **Chosen: authorize** installing, running and removing UUVR plus its BepInEx preloader patcher for exactly one bounded
  spike, including its writes into `Vampire Hunters_Data/Plugins/x64` - **but only after the Stage 0 gate has passed**,
  under the guardrails below.
- Why not "no UUVR, our own path": the spike is small and **dispositive**. It answers the real go/no-go - "does supplying
  an XR plugin payload make this stripped build's retained XR module actually initialize" - which Stage 2 would otherwise
  cost far more to ask, and D-040 already priced the payoff at ~25-35% of Phase 1, at the riskiest end. A FAIL is cheap,
  bounded and still valuable negative evidence, and D-038 pre-committed the PASS/FAIL rules so the outcome cannot be
  softened after the fact.
- Why the riskiest objection does not flip the decision: D-040 found `InputTracking.GetLocalRotation` missing - the one
  symbol UUVR's pose driver hard-requires - but the same scan found `XRDisplaySubsystem` x8, `InputDevices` x13 and
  `centerEyeRotation/Position/Velocity` present. Because D-042 makes copyleft reuse legal for this project, that failure
  mode is **patchable** with a minimal modification over surviving symbols (recorded as a modification) instead of a dead
  end. UUVR is a base to fork, not a black box to hope in.
- Why not defer: nothing new would arrive to change the answer; a deferral only moves the same yes/no past the gate.
- Guardrails (non-negotiable; D-038/D-040 plus this entry): (a) the Stage 0 gate passes first - this project's own
  install/remove/verifier machinery is what makes a third-party install safe; (b) every UUVR, patcher and payload file
  enters the owned-file transaction manifest **and** the verifier's expected set *before* the install, so `stock=121`
  stays provable; (c) exactly one graded session (menu-only, then one gameplay run) judged by D-038's pre-committed
  rules, with the PLAYTEST entries written before the headset goes on; (d) removal and a stock relaunch afterwards with
  hashes re-checked; (e) write authority limited to `BepInEx/` and `Vampire Hunters_Data/Plugins/x64`, plus a
  `_Data/UnitySubsystems` manifest only if the spike needs the RoR2VRMod-style file drop - no other game file, no
  Steam/runtime setting, no save, EULA or leaderboard interaction, and no anti-cheat/DRM work; (f) origin, version, hash,
  SPDX and notices recorded in `docs/DEPENDENCIES.json` at fetch time; (g) the spike produces evidence only - a PASS does
  not adopt UUVR.
- Reservation note: D-042 said the UUVR adoption decision is now "D-043 or later"; this authorization takes D-043, so
  adoption is now **D-044 or later** and remains strictly conditional on the spike PASS.
Consequence: Stage 0 is unchanged and still NOT PASSED - nothing has been downloaded, vendored, installed or injected,
and the spike cannot start before the gate. Next actions unchanged: static camera-writer pinning, then the gate sequence,
then the spike. Q-023 is closed; no user question is outstanding.
Sources: none new - a decision over existing evidence (D-038, D-040, D-041, D-042).
No install, no injection, no game file changed by this decision.




































































































































































































































































































  8 directories, loader absent).
- **Not closed**: whether UUVR's pose path actually runs, and whether the `get_keys` path is even executed,
  remain runtime questions for the headset session.
- Reservation note: adoption numbering moves again - D-049 is taken, so UUVR adoption is **D-050 or later**,
  still strictly conditional on a spike PASS.
- Evidence: `artifacts/uuvr-compat/deps/dependency-recon.json` (regenerated by the verifier),
  `tests/uuvr-compat-check.ps1` (`UUVR_COMPAT_OK`), `docs/UUVR-COMPAT.md`.


### D-050 — Two-handed aim and the Quest 3 keymap pre-registered as data (headset-free, 2026-10-07)
Stage: Stage 1 preparation, written while the Stage 1 gate is open at the user's explicit request ("정상 작동한다는
가정하에 양손 분리조준, 퀘스트3 컨트롤러 키매핑을 미리 준비하자").
Chose: prepare the Stage 3 action map and the Stage 6B aim design as **declarative data plus pre-registered static
checks**, and implement nothing. The skill warns against building on the next stage while a gate is open, so the
compromise is explicit: no VR code, no hook, no loader install, no game launch, no runtime or OS setting changed -
only files a validator can check today and a headset script can consume later.
- **New data**: `config/bindings.json` rewritten from a `NOT_IMPLEMENTED` stub into a role-based action map
  (`dominant`/`off`, resolved from a single `UserDominantHand` setting so left-handed play flips columns and not the
  action list); `config/manifest.json` gains `inputDesign` and `aimDesign`; `src/input/README.md` states what Stage 3
  will build there.
- **New docs**: `docs/INPUT-MAP.md` (Stage 3 pre-brief: evidence, action table, family table, the ≤10-minute runtime
  verification list, pre-registered failure modes) and `docs/TWO-HAND-AIM.md` (Stage 6B pre-brief: per-hand independent
  aim vs linked two-hand aim, link rule and hysteresis, muzzle redirect, recoil in hands, the 14-slot problem, test plan
  and risks).
- **New check**: `tests/verify-bindings.ps1`, wired into `tests/verify.ps1`, re-derives the design every run: family
  coverage for every `required` action, resolution of both handedness variants, no physical-hand bindings, no binding to
  the runtime-reserved `system/click`, no undocumented duplicate control, gesture/base consistency, and the pinned
  aim-mode and 14-slot values.
- **Evidence behind it**: the active runtime on this PC (VDXR 1.0.10.0, `C:\Program Files\Virtual Desktop
  Streamer\OpenXR\virtualdesktop-openxr{,-32}.dll`) carries `oculus/touch_controller` and **no `/interaction_profiles/
  meta` path at all**, so Quest 3 Touch Plus is expected to arrive through the legacy Touch profile; a shipping OpenXR
  mod the user plays on this machine (EDF6VR 2.1.8, read as a map, no code copied) binds the same legacy profile and the
  same component names; the game's own observed controls (primary/secondary fire, space, shift, Q, E, r, esc) shape the
  action list, and the aim source stays the D-046 pin (`Weapon.muzzle_position`, `shoot_forward(..., Transform)`).
- **Design-time catch worth keeping**: `menu/click` exists on one physical controller only, so a purely role-based
  mapping would leave a left-handed player with no menu button (it lands on their *dominant* hand). The file now resolves
  such components through `handOnlyTokens` (carrier hand, not role), and the check fails if that assumption breaks.
- **Cost / honesty**: nothing is bound, tested or verified; `bindings.json` still says `DESIGNED_NOT_IMPLEMENTED` and
  the manifest still says `DESIGN_ONLY_NOT_IMPLEMENTED`. If the UUVR spike fails, this work is unaffected - it is data
  and a plan, not a dependency on UUVR. What a future session must re-derive: the actual interaction profile the runtime
  reports (`xrGetCurrentInteractionProfile`), which capacitive components survive streaming, and whether the muzzle
  transform can really be redirected.
- Consequence: UUVR adoption numbering moves again - D-050 is taken, so UUVR adoption is **D-051 or later**, still
  strictly conditional on a spike PASS.
- Evidence: `config/bindings.json`, `config/manifest.json`, `docs/INPUT-MAP.md`, `docs/TWO-HAND-AIM.md`,
  `tests/verify-bindings.ps1` (`BINDINGS_CHECK: 3 families, 16 actions (12 required), both handedness variants
  resolve; system reserved, role-based only`), `docs/VERIFICATION.md` entry for this run.















































































  ability)`), `docs/INPUT-MAP.md` §3/§3.1/§4, `docs/PHASE_PLAN.md`.


### D-053 — jump/dash swapped: the stick press carries dash, jump moves to the left face button (2026-10-07)
Stage: Stage 1 preparation, data only; revises the layout D-052 chose, at the user's request ("X가 점프, 왼쪽 스틱 클릭을
대쉬로 바꾸면 키버튼 최적 세팅이 뭐야?").
Chose: `dash` → off (left) `thumbstick/click`, `jump` → off (left) `x/click`, and move the toggle-only `two_hand_link` off
the left hand entirely (it is now a 1.0 s hold on the right upper face button, where E is the tap). Everything else from
D-052 stands: `move`/`aux_weapon_1` (Q) left, `turn`/`aux_weapon_2` (E)/`fire_primary`/`blood_ability`/`mod_recenter`
right, `fire_secondary` on the left trigger, `grab` on both squeezes, `mod_menu` on the left menu button, `mod_bug_report`
a 1.0 s hold on the left lower button, `reload`/`weapon_bank_select` reserved.

- **Why this variant was requested, in engineering terms:** the dash keeps the **exact** direction the thumb is pushing,
  because the thumb never leaves the stick. The D-052 variant needed a ~200 ms latched stick vector so the dash would not
  lose its heading — an assumption standing between the player and a correct dash. This variant removes that assumption.
- **Cost, stated openly (the trade is real, it just moved):** pressing `jump` now takes the thumb off the stick, so the
  movement vector reads zero for that moment and jumping mid-run is a very short stop. This is a feel question the Stage 3
  headset item 8 answers in seconds; the flip back is one line (`jump` ↔ `dash`) plus a re-run, and both variants stay
  recorded (D-052, D-053) rather than one silently replacing the other.
- **One thing this variant made necessary:** the left hand is now completely full (7 of 7 controls; the lower button
  carries Q on a tap and the bug report on a hold). `two_hand_link` could therefore no longer live on a left double tap —
  and the only candidate left was a double tap of `jump`, which is a **real game input** (the game has a double jump), so
  the two would fight. It moved to the right upper face button as a 1.0 s hold, inert unless `TwoHandMode == toggle`.
- **Rejected:** leaving `two_hand_link` on the left as a third gesture on the lower button (three gestures on one button
  is unusable and would delay Q); dropping the action entirely (it is the only manual path into toggle mode and costs
  nothing while inert).
- **Pins updated:** `tests/verify-bindings.ps1` now pins `off/dash = input/thumbstick/click`, `off/jump = input/x/click`,
  `dominant/two_hand_link = input/a/click`, and throws if `two_hand_link` reappears on the left hand; its message prints
  the D-053 layout. `config/manifest.json` `inputDesign.amendedBy` = D-053; `config/bindings.json` `keymapRevision`
  records both variants and the one-line flip.
- **Honesty:** still no XR session, nothing bound, no runtime profile confirmed, and the feel of both variants is
  unproven — only the data changed. Stage 3 item 8 and the Stage 4 movement item now ask for the one answer that settles
  which variant ships.
- Consequence: UUVR adoption numbering moves again — D-053 is taken, so UUVR adoption is **D-054 or later**, still
  strictly conditional on a spike PASS.
- Evidence: `config/bindings.json` (`keymapRevision`, `actions.jump`, `actions.dash`, `controlTables.touch_like`,
  `gestures.two_hand_link_toggle`), `tests/verify-bindings.ps1` (`BINDINGS_CHECK: 3 families, 16 actions (12 required),
  right-handed map resolved (left-handed out of scope, D-051), D-053 layout pinned (off/left = move+dash on the stick,
  jump on X, Q on Y; dominant/right = turn + E/link on A, fire, ability)`), `docs/INPUT-MAP.md` §3/§3.1/§4,
  `docs/VERIFICATION.md`, `docs/PHASE_PLAN.md` Stage 3/4.
































































  `target` (main weapon), `base` (auxiliary weapon), `control` (`input/trigger/value`), `modifier` (`grip`),
  `modifierControl`, and the vocabulary `held: main_weapon` / `released: auxiliary_weapon`. The validator's
  duplicate-control rule now accepts a documented modifier split exactly as it accepted a documented gesture split.
- **The failure mode this scheme has, named and handled in the design:** the grip must be held for essentially every shot of
  the main weapon, so relaxing it out of habit would fire the auxiliary weapon. The design answer is a **per-press latch** —
  a press that began with the grip held stays main-weapon until release — recorded in the split's note and in
  `tuning.gripFireMode`; Stage 6B measures it. Without the latch this rule is a foot-gun, so the latch is part of the rule,
  not an optimisation.
- **`fire_secondary` stays parked** (`required: false`, `RESERVED_PENDING_SLOT_SEMANTICS`, unbound). Both triggers are now
  spoken for, and the flat alt-fire's slot meaning is still unknown. Q-029 separates the two possible readings of the user's
  "보조무기" (Q/E auxiliary weapon vs RMB alt-fire); if Q/E turn out to be *fire* keys, `fire_aux_*` and `aux_weapon_*`
  collapse into one action and the left Y / right A face buttons can be freed — one line of data either way.
- **`dash` on an analog axis needs a threshold and a re-arm**, and both are data: `engage` must stay above `release`
  (otherwise a centred stick repeat-fires) and inside (0.5, 1] (otherwise an ordinary forward push trips it). The validator
  pins all three facts plus `dash.fromAxis`. The dash's *direction* now comes from the left stick, since the right stick is
  where the dash is triggered.
- **Consequences, stated plainly:**
  - Both stick presses are now free (`input/thumbstick/click` on each hand). Nothing was invented to fill them; they are
    listed as free in `docs/INPUT-MAP.md` §3 and Q-030 asks whether the user wants anything there.
  - `docs/PHASE_PLAN.md` Stage 5's "grab/throw recovery" item is **removed**, not silently dropped: the user's finding is
    that there is nothing to grab. Hand poses, collision and weapon sway keep their own Stage 5 items.
  - Stage 3's verification list no longer fits one ≤10-minute sitting (each trigger is now pressed twice, once per grip
    state, plus a stick-direction pass), so `docs/INPUT-MAP.md` §4 and the Korean headset card split it into two short
    sittings. The bounded-time rule is preserved; the bound moved.
  - UUVR adoption numbering moves again: D-055 is taken, so adoption is **D-056 or later**, still strictly conditional on a
    spike PASS.
- **Rejected:** (a) modelling the grip as two actions on the trigger without declaring the split — the validator would
  (correctly) refuse an undocumented duplicate, and a reader could not tell main from auxiliary fire; (b) using a hold
  gesture for the auxiliary fire — the trigger is held for the whole burst, so tap/hold cannot express it; (c) deleting
  `fire_secondary` from the file — it removes a real flat capability with no trace; (d) keeping recenter on the right stick
  press "just in case" — the user removed it, and an unused binding is a lie about the map; (e) deleting `grab` without
  recording why — the rename to `grip` plus the note is what keeps Stage 5 honest.
- **Pins added to `tests/verify-bindings.ps1`:** `grab` must not reappear; `mod_recenter` must not reappear on either
  column; `dash` on `dominant` + `input/thumbstick/y` with a valid threshold pair; `grip` bound on both columns at
  `input/squeeze/value`; exactly two modifier splits, named, with the held/released vocabulary intact and the modifier bound
  on that hand; `fire_aux_1`/`fire_aux_2` on their own columns; `manifest.inputDesign.openQuestions` = Q-027/Q-028/Q-029.
  Four deliberate negative tests were run (held-vocabulary, dominant grip removed, split removed, dash threshold crossed):
  each failed with the intended message and exit 1, and the file was restored afterwards.
- **Honesty:** no XR session, nothing bound, no runtime profile confirmed. `fire_left_hand`, `fire_aux_*`, the grip threshold
  and the dash threshold are design values; the headset is the only thing that turns them into facts, and a negative result
  is a `docs/BUGS.md` entry rather than a silent remap.
- Evidence: `config/bindings.json` (`keymapRevision`, `actions.grip`, `actions.dash`, `actions.fire_aux_1`,
  `actions.fire_aux_2`, `tuning`, `controlTables.touch_like`, `gestures.grip_fire_mode_*`), `config/manifest.json`
  (`inputDesign.amendedBy`, `openQuestions`), `tests/verify-bindings.ps1`
  (`BINDINGS_CHECK: 3 families, 18 actions (14 required), … D-055 layout pinned …`), `docs/INPUT-MAP.md`
  §1/§3/§3.1/§3.2/§3.3/§4/§5/§6, `docs/QUESTIONS.md` (Q-026/Q-027/Q-028 resolved, Q-029/Q-030 opened),
  `docs/playtests/HEADSET-CHECKLIST.md`, `docs/PHASE_PLAN.md`, `docs/TWO-HAND-AIM.md`, `docs/VERIFICATION.md`.



### D-056 — the two-hand link is deleted; the face buttons move to the user's layout; the upper face button selects the auxiliary fire (2026-10-07)

Stage: Stage 1 preparation, data only; supersedes D-052/D-053/D-055 at the user's request ("투핸드 링크 없애라고 / 퀘스트
기준 A,X가 아래에 있고 Y,B가 위 공간에 있다 / 오른쪽 컨트롤러 A를 점프, 왼쪽컨트롤러 X를 대쉬, 각 컨트롤러 스틱 클릭을
왼쪽은 Q, 오른쪽은 E에 대응해 aux weapon_1, _2로 할당, 오른쪽 컨트롤러 스틱을 아래로 당기면 혈능력, 왼쪽은 Y키+LT로,
오른쪽은 B키+RT로 보조사격 발동하게 되나?").
Chose: implement exactly that, delete the link, and answer the question honestly — the combination is our own logic, not an
OpenXR feature, and it is already expressible in the data.

- **The question, answered first.** "왼쪽 Y+LT / 오른쪽 B+RT로 보조사격이 발동하나?" — **yes.** OpenXR delivers
  per-control states (one action per component: `input/y/click`, `input/trigger/value`), so combining "button held" with
  "trigger pulled" is arithmetic the mod performs when it decides which game call to make
  (`PlayerController.fire(EWeaponSlot, …)`). It is declared as a **modifier split** (`gestures.aux_fire_mode_off` /
  `_dominant`) — the same mechanism D-055 introduced for the grip; only the selector changed.
- **Deleted: the two-hand link.** `two_hand_link` (action), `two_hand_link_toggle` (gesture),
  `manifest.aimDesign.modes[two_hand_linked]` and the whole link rule in `docs/TWO-HAND-AIM.md` §3 are gone, replaced by
  `manifest.aimDesign.modesRemoved` so the deletion is data rather than a diff nobody reads. Aim is
  `per_hand_independent` only — the mode the user chose as M1 (Q-024) — and the grip is no longer a link gesture. No link
  radius, no support point, no `TwoHandMode`, no hysteresis. `manifest.weapons[].grab_points` is now an unused placeholder.
- **The fire rules now.** Main weapon = that hand's trigger **while its grip is held** (`gate: "grip"`); auxiliary weapon =
  that hand's trigger **while the upper face button is held** (`modifier: "aux_modifier"`); the trigger alone fires
  **nothing**. The explicit selector wins when both are held, and the mode is **latched per press** (`pressLatched: true`).
  The validator refuses a split without the latch, without a bound selector, or whose `base` is itself a modifier's target.
- **Why the selector moved off the grip (the honest reason).** D-055 made "grip released" the auxiliary selector, which
  turned the main-weapon trigger into a held control: relaxing the grip out of habit fired the auxiliary weapon — the
  scheme's one dangerous failure mode. An explicitly held button cannot be released by accident, so that failure mode is
  gone. The cost moved elsewhere and is stated openly: the main weapon now needs grip **and** trigger, so a trigger-only
  press fires nothing (**Q-031** asks whether to keep that gate).
- **Layout moved (D-056):** `jump` off `x/click` → dominant `a/click`; `dash` dominant `thumbstick/y` → off `x/click`;
  `aux_weapon_1` (Q) off `y/click` → off `thumbstick/click`; `aux_weapon_2` (E) dominant `a/click` → dominant
  `thumbstick/click`; `blood_ability` dominant `b/click` → dominant `thumbstick/y` pulled **down**; new action
  `aux_modifier` on `y` (left) / `b` (right). `fire_left_hand`/`fire_primary` keep their triggers, `grip` keeps both
  squeezes, `mod_menu` is unchanged.
- **The right-stick-up dash is dropped, not kept as a second path.** The user assigned dash to X; keeping both would give
  one action two controls on two hands while the stick's Y axis was needed for the ability. Recorded as Q-032 so reversing
  it is their decision rather than an agent subtraction.
- **`blood_ability` is now a discrete action on an analog axis**, so the D-055 dash tuning entry is replaced by
  `tuning.bloodAbilityFromRightStickDown` (`direction: "negative"`, `engage -0.7`, `release -0.3`). The validator pins the
  direction, the order (`engage < release`), the range (`[-1, -0.5)`), `blood_ability.fromAxis`/`.fromAxisDirection`, and
  refuses a `fromAxis` on `dash` or the old `dashFromRightStickUp` key coming back. The sign is a runtime fact (Stage 3
  logs raw values); if this runtime reports down as positive, two numbers flip and nothing else changes.
- **The bug report moved** to a 1.0 s hold on the left stick press (a tap on the same press is Q), because Y cannot be both
  a hold target and the auxiliary selector. Q-033 records the default and the alternatives.
- **The physical layout was corrected — a documentation error, not a binding error.** The user states that on a Quest Touch
  controller **A and X are the LOWER face buttons and B and Y the UPPER ones**. The D-052/D-053 prose said the opposite
  (X called "upper", Y called "lower") while `handProfiles` already mapped `$face_primary` = A/X and `$face_secondary` =
  Y/B. No token moved; the words did, and `docs/INPUT-MAP.md` now says which is which.
- **`fire_secondary` stays parked** (`required: false`, unbound) — both triggers remain spoken for. Q-029 is unchanged in
  substance (Q/E auxiliary weapon vs flat RMB alt-fire); only the selection mechanism around it changed.

- **Consequences, stated plainly:**
  - **No free control is left on either controller except `system/click`** (runtime-reserved). The left hand uses all seven;
    the right hand uses six of seven. Q-030 is therefore **answered by the user**, not by the agent's "leave it empty"
    default.
  - Stage 3's press order changed (six controls moved), so `docs/INPUT-MAP.md` §4 item 2 and the Korean headset card were
    rewritten; the two-sitting bound is unchanged.
  - `docs/TWO-HAND-AIM.md` lost its M2 mode, its link rule and its headset items 3/4/8; what remains is per-hand independent
    aim, comfort, the muzzle redirect and the slot proof (item 10) — exactly what the user's M1 choice needs.
  - UUVR adoption numbering moves again: D-056 is taken, so adoption is **D-057 or later**, still strictly conditional on a
    spike PASS.
- **Rejected:** (a) keeping the link as an optional setting after the user deleted it — "optional" is how a deleted feature
  quietly returns; (b) keeping the stick-up dash *and* X as two dash paths — two controls for one action is how a map becomes
  unpredictable; (c) silently dropping the grip gate — it is the user's own D-055 rule, and Q-031 is the honest place to
  remove it; (d) taking the sign of the stick axis from memory instead of Stage 3 — a sign error is exactly the kind of
  silent wrongness the tuning block exists to catch.
- **Pins added/changed in `tests/verify-bindings.ps1`:** `two_hand_link` / `two_hand_link_toggle` / `grip_fire_mode_*` must
  not reappear; `manifest.aimDesign.modes` = `[per_hand_independent]` with `modesRemoved` recording the link; the off hand's
  function is `move_and_utility`; `jump` on dominant, `dash` on off, Q/E on the stick presses, `aux_modifier` on the upper
  face buttons; both main fires gated by `grip`, both auxiliary fires selected by `aux_modifier`; the split vocabulary
  allows either order but pins `held = auxiliary_weapon` here and requires `pressLatched`; the blood-ability threshold's
  direction/order/range; `openQuestions` = Q-029/Q-031/Q-032/Q-033.
- **Honesty:** no XR session, nothing bound, no runtime profile confirmed, no headset test. Every new value (the selector
  rule, the gate threshold, the ability threshold and its sign) is a design value that only the headset turns into a fact;
  a negative result is a `docs/BUGS.md` entry, not a silent remap.
- Evidence: `config/bindings.json` (`keymapRevision`, `actions.*`, `tuning`, `controlTables.touch_like`, `gestures.*`),
  `config/manifest.json` (`inputDesign.amendedBy`/`openQuestions`, `aimDesign.modes`/`modesRemoved`/`hands`),
  `tests/verify-bindings.ps1` (`BINDINGS_CHECK: 3 families, 18 actions (15 required), … D-056 layout pinned …`),
  `docs/INPUT-MAP.md` §1/§2/§3/§3.1/§3.2/§3.3/§4/§5/§6/§7, `docs/QUESTIONS.md` (Q-030 resolved; Q-031/Q-032/Q-033 opened),
  `docs/TWO-HAND-AIM.md`, `docs/playtests/HEADSET-CHECKLIST.md`, `docs/PHASE_PLAN.md`, `docs/VERIFICATION.md`,
  `src/input/README.md`.


### D-057 — 보조발사 is the flat RMB alt-fire, the grip assignment is deleted, and the right stick carries interact / the ability / a 30° snap turn (2026-10-07)

Stage: Stage 1 preparation, data only; supersedes D-056 at the user's request ("보조사격은 평면 RMB 대체사격임, 한국어
번역본에는 보조발사라 되어있다. 앞으로 보조발사라 부르도록 / 그립버튼에 키 할당은 없애고 / 오른쪽 스틱을 위로 올리면
상호작용 키, 오른쪽 스틱 좌, 우는 30도씩 스냅턴").
Chose: implement exactly that — one rename, one deletion, one new action and one new turn rule — and record the two
readings the instruction forces on the rest of the map (the trigger now fires the main weapon alone; interact shares the
right stick's Y axis with the blood ability), rather than letting either stay implicit.

- **Q-029 is answered by the user, and the answer rewrites the action list.** The upper-face-button fire is the flat **RMB
  alt-fire**, which the Korean build calls **보조발사**. The data had carried two names for that one thing —
  `fire_aux_1`/`fire_aux_2` (the VR path) and the parked `fire_secondary` (the flat path, `RESERVED_PENDING_SLOT_SEMANTICS`
  since D-054) — so they are merged into `fire_secondary_off`/`fire_secondary_dominant`, the split vocabulary becomes
  `secondary_fire`/`main_fire`, and `aux_modifier` becomes `secondary_modifier` (the selector is named after the fire it
  selects). The game's own **보조무기** keys keep their actions: `aux_weapon_1`/`aux_weapon_2` (Q/E on the two stick presses),
  which is precisely the distinction Q-029 had been asking about.
- **The grip assignment is deleted, and the gate goes with it (Q-031 answered).** `grip` (the action), `gate: "grip"` on
  `fire_left_hand`/`fire_primary`, `tuning.gripFireMode` and both `input/squeeze/value` bindings are gone. The trigger alone
  fires that hand's main weapon; the D-056 cost ("a trigger-only press fires nothing, which reads as a dead trigger") is
  gone with it. This is the user's newer instruction overriding their own older D-055 rule, and the honest way to record
  that is to delete the gate, not to keep it and call the question open.
- **`interact` is a new action, and it needed a new kind of split.** It reads the right stick **pushed up**
  (`fromAxis: input/thumbstick/y`, `fromAxisDirection: positive`, `tuning.interactFromRightStickUp` = engage 0.7 / release
  0.3). Because the Y axis now carries two actions, the old duplicate-control rule required a declared split, so
  `gestures.right_stick_y_axis_split` (`gesture: "axisDirection"`, `target: interact`, `base: blood_ability`) was added and
  the validator gained a fourth gesture kind. The alternative — one action with a mode flag inside the action — would have
  hidden the direction pair inside prose instead of data.
- **The snap turn is now data, not prose.** `turn` keeps its dominant `thumbstick/x` binding and gains `turnMode: "snap"` +
  `snapDegrees: 30`; `tuning.snapTurnFromRightStickX` names the control, the degrees, both directions (`leftDirection:
  negative`, `rightDirection: positive`) and the re-arm (engage 0.7 / release 0.3). Q-015/D-013 already fixed snap-only
  turning; D-057 fixes the increment (the user's 30°) and makes "one step per push, no spin while held" a checkable rule.
- **What did NOT change, so a later session does not re-litigate it:** the two-hand link stays deleted (D-056); `dash` stays
  on the left X and `jump` on the right A; Q/E stay on the two stick presses (D-056, re-confirmed by the 보조발사 rename
  keeping them distinct); the upper face button stays the fire selector; the squeeze is still not a grab verb (D-055 — this
  game has no grabbable object); right-handed only (D-051); snap turn only (Q-015/D-013).


- **Consequences, stated plainly:** both squeezes are now the **only** unassigned controls (plus the runtime-reserved
  `system/click`), and that is a decision rather than an oversight — `bindings.deliberatelyUnbound` records
  `input/squeeze/value` with the reason, and the validator fails if any action binds it, if `grip`/`gate`/`gripFireMode`
  return, or if that record disappears. Stage 5 therefore has no grab verb *and no squeeze obligation*; a future closed-hand
  pose needs a new decision before it may claim the control.
- **Rejected:** (a) keeping `fire_aux_*` alongside the new names — two names for one action is how two paths start to drift;
  (b) keeping the parked `fire_secondary` "just in case" — the user had just said what it is, so parking it again would
  ignore the answer; (c) deleting the grip binding but keeping the gate "because D-055 said so" — that would silently break
  the newer instruction and hide it behind an older rule; (d) making interact a modifier-held variant on the trigger — the
  user asked for a stick direction, and the trigger pair is already carrying two actions; (e) inventing a flat parity key
  for interact — the v4 probe never observed one, so it stays an open question (Q-034) instead of a guess.
- **Pins added/changed in `tests/verify-bindings.ps1`:** `fire_secondary_off`/`fire_secondary_dominant` required, selected
  by `secondary_modifier`, `held: secondary_fire` / `released: main_fire`, `pressLatched: true`; the old
  `fire_aux_*`/`fire_secondary`/`aux_modifier`/`aux_fire_mode_*` names refused; `grip` action, any `gate`, `gripFireMode` and
  every `input/squeeze/value` binding refused, with the `deliberatelyUnbound` record required; `interact` required on the
  dominant hand with a positive threshold; the right stick Y axis carrying exactly {interact, blood_ability} and the
  axisDirection split naming both directions; the right stick X axis carrying only `turn`, at 30° with a named direction pair
  and a re-arm; `openQuestions` = Q-033/Q-034.
- **Honesty:** no XR session, nothing bound, no runtime profile confirmed, no headset test. The rename, the deletion, the
  interact threshold and the 30° step are design values and a design decision; only the headset turns them into facts, and a
  negative result (a selector that never reports, an axis that never reaches engage, a squeeze that drives something) is a
  `docs/BUGS.md` entry, not a silent remap.
- Evidence: `config/bindings.json` (`keymapRevision`, `actions.*`, `tuning`, `controlTables.touch_like`,
  `gestures.*`, `deliberatelyUnbound`), `config/manifest.json` (`inputDesign.amendedBy`/`gripAssignment`/`openQuestions`,
  `userReportedMechanics.terminology`/`flatControlNotes`/`interactionVerb`, `aimDesign.amendedBy`),
  `tests/verify-bindings.ps1` (`BINDINGS_CHECK: 3 families, 17 actions (15 required), … D-057 layout pinned …`),
  `docs/INPUT-MAP.md` (header, §1, §3, §3.1, §3.2, §3.3, §4, §5, §6, §7), `docs/QUESTIONS.md` (Q-029/Q-031/Q-032 resolved;
  Q-034 opened), `docs/playtests/HEADSET-CHECKLIST.md`, `docs/TWO-HAND-AIM.md`, `docs/PHASE_PLAN.md`,
  `docs/VERIFICATION.md`, `src/input/README.md`, `README.md`.


### D-058 — the game's own keybinding menu is recorded as flat-parity evidence: interact = Z (Q-034 answered), and two unmapped actions are revealed (2026-10-07)

Stage: Stage 1 preparation, data only; amends D-057 (the map itself does not move).
Chose: treat the game's own keybinding screen (키 바인딩 — 키보드 및 마우스 전용, Korean build, screenshot supplied by the
user) as **first-party flat-parity evidence** — record its rows as data, answer Q-034 with it, correct the two records it
contradicts, and open the two new actions it reveals as questions instead of inventing bindings.

- **Q-034 is answered: the game has an interact verb, and it is 상호작용 = Z (main) / G (alt).** Corroborated by
  `PlayerInputController.Interact`, already listed in the D-031 static scan; the v4 probe simply never recorded a press.
  The old "`r` is the most likely candidate" guess is **refuted**: `r` is **재장전 (Reload)**. The VR `interact` action
  therefore forwards to the game's own interact input (Z); `manifest.interactables` stays `[]` until something interactable
  is actually found (a Stage 6B negative finding is still a bug entry, not a silent remap).
- **Two records corrected (data, not bindings).** `interact.flatParity` goes from "unconfirmed / never observed" to **Z**;
  `reload` goes from `RESERVED_MEANING_UNCONFIRMED` to `RESERVED_MEANING_CONFIRMED_NO_VR_BINDING` (still reserved — the mod
  keeps no VR binding for it). 발사 = LMB, 보조 발사 = RMB (re-confirms Q-029/D-057), 점프 = SPACE, 혈능력 = F, and
  Q/E = 왼쪽/오른쪽 **로봇 팔** (`ArmL`/`ArmR`) are recorded as labels.
- **Two new game actions revealed, deliberately NOT mapped.** **특수 능력 = SHIFT** (`PlayerInputController.SpecialAbility`)
  → **Q-035** — it may or may not be the mod's `dash`, whose `flatParity` claims "shift" from the probe, so the question is
  opened rather than assumed. **수동 레벨 업 = T** → **Q-036** — the map does not carry it and the user never mentioned it;
  default is to leave it to the game's own UI. Neither name may become an action until the user decides — the validator
  fails if `special_ability` or `manual_level_up` appears in `bindings.actions`.
- **What did NOT change:** no VR binding, no action added, removed or moved; every D-057 pin stands (the rename, no grip
  gate, interact on stick-up, the 30° snap turn, the unbound squeeze). This decision is flat-parity data only.
- **Rejected:** (a) quietly adding the two revealed actions — they are decisions, and "revealed" is not "requested";
  (b) silently relabelling `dash` as the special ability — SHIFT's meaning is now evidence, but whether the two are the
  same is a playtest question (Q-035); (c) leaving Q-034 open now that the game's own menu answers it; (d) rewriting the
  historical `keymapRevision` text — a new superseding `flatMenuEvidence` block plus this entry keeps the history honest.
- **Pins added/changed in `tests/verify-bindings.ps1`:** `flatMenuEvidence` required with `decision: D-058` and the
  상호작용/Z, 보조 발사/RMB, 재장전/R and 왼쪽·오른쪽 로봇 팔 Q/E rows; the 특수 능력/Q-035 and 수동 레벨 업/Q-036 rows must
  stay recorded as *unmapped*; `interact.flatParity` must name Z and must not read "unconfirmed"/"never observed";
  `reload.flatParity` must not read "unconfirmed"; `special_ability`/`manual_level_up` refused as actions;


































  can never reload. Both are pinned, and both live in `tuning.reloadFromFaceDoubleTap`.
- **The parked `reload` action is merged away** (the D-057 pattern): the old reserved `reload`
  (`RESERVED_MEANING_CONFIRMED_NO_VR_BINDING`) is replaced by the per-hand pair, and the validator fails if `reload`
  reappears as an action or on a column. The flat-parity record stands: 재장전 = **R** (main) / V (alt).
- **Never silent:** `config/bindings.json → reloadDesign` and `config/manifest.json → inputDesign.reloadDesign` record
  the API evidence (`artifacts/recon-asm/full/assembly-recon.json`, `Assembly-CSharp.dll` sha256 `0494F1BA…`) with
  `proves` / `doesNotProve` / `fallback`. If a per-hand reload cannot be proven in the headset, the double tap forwards
  to the game's single R reload and that downgrade is written down — **Q-037** owns the measurement.
- **Rejected:** (a) patching the game's code first — the per-weapon API already exists, so that would be work with no
  evidence behind it; (b) a third face-button control for reload (none is free on Touch); (c) a hold-based reload (the
  hold on Y/B is already the 보조발사 selector); (d) silently claiming "왼손만 장전" works before a session.
- **Pins added/changed in `tests/verify-bindings.ps1`:** exactly two `doubleTap` splits with the right hand/target/base/
  control, `windowSeconds` inside (0,1), `maxFirstTapSeconds < windowSeconds` and `suppressWhenTriggerHeld`;
  `reload_off`/`reload_dominant` required, each on its own hand and button; `reload` refused as an action or a column
  entry; the simple-controller `notSupported` list must carry both; `dash` stays on `input/x/click` and
  `dash.flatParity` must name 특수 능력 and must not read "UNPROVEN"; the 특수 능력 menu row must mention `dash`;
  `tuning.reloadFromFaceDoubleTap` with `sameHandOnly`; both `reloadDesign` blocks; the reload pair's `flatParity` must
  name R and Q-037; `openQuestions` = **Q-033/Q-036/Q-037**.
- **Nothing is implemented:** no XR code, no hook, no loader install, no launch. Stage 3 (the double-tap reader) and
  Stage 6B (`Weapon.reload()` on one hand) are where this becomes behaviour.
- Evidence: `config/bindings.json` (`reloadDesign`, `actions.reload_off`/`reload_dominant`, `controlTables.touch_like`,
  `gestures.reload_double_tap_*`, `tuning.reloadFromFaceDoubleTap`, `flatMenuEvidence`,
  `families.generic.notSupported`), `config/manifest.json`
  (`inputDesign.reloadDesign`/`openQuestions`/`amendedBy`, `userReportedMechanics.flatControlNotes`),
  `tests/verify-bindings.ps1` (D-059 pins), `docs/INPUT-MAP.md`, `docs/QUESTIONS.md` (Q-035 resolved, Q-037 opened),
  `docs/TARGET_PROFILE.md`, `docs/PHASE_PLAN.md`, `docs/playtests/HEADSET-CHECKLIST.md`, `src/input/README.md`.




































































































### D-062 — the project owns the one settings edit: keep UUVR's runtime, replace its broken patcher half

Date 2026-10-07. Status: **accepted**, Stage 1 only. Decides the fix-or-drop question D-060 left open, on the
strength of new measurements rather than a preference. It does **not** adopt UUVR and authorizes no Stage 2 work.

**Context.** D-060 recorded `FAIL_NO_STEREO` with one identifiable blocker: UUVR's vendored `Uuvr.Patcher` throws
`NullReferenceException` in `PatchVR` on every launch, so the XR device list is never written and Unity has no
supported device (`xrEnabled=False`, `xrLoadedDevice=''` in all three VR configurations). D-060 offered three ways
out: (a) rebuild `Uuvr.Patcher` against a current `AssetsTools.NET`, (b) write the settings edit with our own
tooling and keep UUVR's runtime, (c) drop UUVR and build the XR bootstrap ourselves.

**Decision: (b).** The project writes the `BuildSettings.enabledVRDevices` list itself, with a dependency-free tool
it owns (`src/tools/XrDevicePatch`), and keeps UUVR's runtime unmodified.

**The measurements that decide it.**

1. **The root cause is now read out of game data, not inferred from a log.** `globalgamemanagers` carries
   `BuildSettings` (class 141) at path id 11, a 1272-byte blob at offset 150424, and its `enabledVRDevices` vector
   has **count 0** — the stock build ships an empty device list, so Unity loads no XR device. This is the same
   field UUVR's `PatchVR` writes (`ggmTable.GetAssetInfo(11)` -> `enabledVRDevices`), so (b) reproduces upstream's
   *intent*; it does not invent new behaviour.
2. **Unity already found UUVR's loaders.** `artifacts/uuvr-spike/run-flat-1/Player.log` has
   `[Subsystems] Discovering subsystems at path …/UnitySubsystems` followed by
   `1 'inputs' descriptors matched` **and** `1 'displays' descriptors matched` for **both**
   `UnitySubsystems/UnityOpenXR` and `UnitySubsystems/XRSDKOpenVR`, yet the same log contains no XR device load
   line at all. The subsystem manifests were never the missing piece; the ordered device list is the gate.
3. **The layout is pinned, so the edit can be exact.** For 2022.3.37f1 (release variant) `BuildSettings` is
   `vector<string> scenes`, `preloadedPlugins`, `enabledVRDevices`, `buildTags`, then 14 `bool`s, then 4-byte
   alignment, `string m_Version`, and `vector<int> m_GraphicsAPIs` (2 = Direct3D11 on this build). The container is
   format version 22, **type tree disabled** (18 types / 18 objects), with a 48-byte **big-endian** header, a
   little-endian metadata prefix and 24-byte object-table entries. Nothing here needs a class database, which is
   precisely what the vendored patcher fails to supply.


**Why (b) and not (a).** (a) modifies a GPL-3.0 dependency, needs its own rebuild, its own D-042 notice work and its
own transaction, and the failing frame is invisible (readable source covers 2 of UUVR's ~12 runtime classes, and
BepInEx catches the exception above the failing frame). (b) instead fits the engine that already exists: the file is
**already** the payload's single declared mutation with a pinned pre-image
(`B9DF43EED29E6408FAC79B260FEB5E712F95EE7EE172F003E4CA501A6A87D1D9`), so no new scope, no new mutation and no notice
change are needed.

**Why (b) and not (c).** (c) discards a runtime that is demonstrably alive: UUVR reached `ENTRY_EXECUTED`, built
`UUVR/VrUiManager` and `UuvrPoseDriver` with `stereoTarget=Both`, and called `XRSettings.enabled`. Exactly one step
was missing.

**Implementation.** `src/tools/XrDevicePatch` (net10.0, no third-party dependency). `inspect` prints the envelope,
the object table and the parsed asset; `patch` refuses to write unless the **untouched** blob round-trips through its
own serialiser byte-for-byte, then replaces the asset by appending the new blob at the end of the file and rewriting
only that object's table entry plus the header file size. On this target that is a **7-byte** change in the shared
prefix (header size 30..31, table entry 736..738 and 744..745) plus a 1312-byte appended tail; all 17 other objects
are proven byte-identical before the file is written. The write is then recorded through the engine's own
`MutationObserved` event, which is what lets `-Action Uninstall` accept the rewrite and restore the pinned pre-image.

**Device list.** `OpenVR, OpenXR, Oculus`. Upstream's patcher writes `OpenVR, Oculus`, and UUVR ships all three
loaders side by side (`XRSDKOpenVR.dll`, `UnityOpenXR.dll` + `openxr_loader.dll`, `OVRPlugin.dll`, each with its
`UnitySubsystems` manifest), so naming all three keeps upstream's first choice while also covering an OpenXR-only
runtime such as the Meta Quest 3 simulator the user installed at `<MetaXR>` for headset-free verification. Whether
Unity falls through to the next entry when an earlier loader fails to initialise is **not proven** and is recorded
as Q-038; if it does not, the first entry decides and the simulator run must use `--devices OpenXR`.

**Known side effect, recorded not hidden.** UUVR's patcher still throws on every launch. It is harmless to this fix
because `Initialize()` runs `CopyFilesToGame` **before** `PatchVR` (so the VR plugins and `UnitySubsystems` payload
*is* copied into the game at launch — those 23 `_Data` files are declared *generated*, so they are absent until the
game runs) and `PatchVR` dies **before** `File.OpenWrite` (so our file is never overwritten). That ordering is
what makes (b) work at all, and it also means SP2's `Failed to run initializer` error stays open for a later session.


**Verified automatically, not by assertion.** `tests/xr-device-patch.ps1` (wired into `tests/verify.ps1`) proves, from
the pinned stock backup copy and with the live game untouched: the pinned pre-image still matches the declared
mutation, the stock device list is empty, the untouched blob round-trips byte-for-byte, patching is deterministic
(two runs byte-identical), the diff is minimal (exactly the three ranges above plus a 1312-byte tail), the patched
asset reads back with exactly the requested devices, and the scene list, flag bytes, `m_Version` and graphics API are
unchanged. It deliberately does **not** pin an expected post-patch digest, for the reason
`config/uuvr-spike-payload.json` already records: the post-image is not predicted, so verification compares against
stock. Independent cross-check: `AssetsTools.NET` 3.0.5 reads the patched file as
`pathId=11 byteOffset=226728 byteSize=1308 typeId=141`. Observed digests: stock `B9DF43EE…D1D9` (228452 B) ->
patched `8DAFFBEF…3127` (229764 B) for the default device list.

**Rejected alternatives.** Forking the patcher (a) — reasons above. Disabling `Uuvr.Patcher` entirely — it is the
component that copies the VR plugins and subsystem manifests into `_Data` on every launch, so disabling it would
remove the very payload this fix depends on. Pre-staging that payload in the transaction instead (which would also
close the SP2 error) is a larger design change: it would move `_Data/Plugins` and `_Data/UnitySubsystems` from
"generated by the installed tool" to "installed by us", and is left as the recorded follow-up rather than smuggled in
here. No third-party class database was adopted (the AssetRipper/TypeTreeDumps dump was used read-only as the layout
reference, and the shipped `classdata.tpk` is untouched).

**Not claimed.** No stereo, no head tracking and no XR device load have been observed yet: this entry lands a
*mechanism* with automated proof, and the verdict still needs a run. The gate remains hard (D-017) and the re-run is
scripted in `docs/playtests/PLAYTEST-1B-SPIKE-RERUN.md`.

**Round-trip proof (install → patch → teardown).** `tests/uuvr-patch-roundtrip.ps1` runs the real path unattended
against the real game and returned **`ROUNDTRIP_OK`** (workspace `artifacts/transactions/20261007T133312Z`): stock
`B9DF43EE…D1D9` / 228452 B / 121 files → `ADDITIONS_INSTALLED` with
`XR_DEVICE_PATCH_APPLIED … 228452B/B9DF43EED29E6408 -> 229764B/8DAFFBEF8EB6B151 devices=[OpenVR,OpenXR,Oculus]`, the
live file independently re-read as `8DAFFBEF…3127`, and a journal ending `Installed, MutationObserved` with exactly
**one** observation → `ADDITIONS_QUARANTINED_STOCK_VERIFIED`, `MutationRestored`, `Recovered`, **6** roots quarantined
(not 15: the other 9 are the declared *generated* `_Data` roots that only exist after a launch) → back to
`B9DF43EE…D1D9` / 228452 B / 121 files, with `tests/BackupSupport.ps1` independently confirming 121/121 files
identical in hash **and** write time. The drill is deliberately *not* wired into `tests/verify.ps1` (it mutates the
real game and needs the spike authorization), and its assertions are structural: the patch report is checked against
the pinned pre-image and the live post-image against the report's own `patched.sha256`, never against a predicted
digest (D-048). Its header records the harness trap it was written around: `Start-Process -WindowStyle Hidden` with an
*array* `-ArgumentList` returns a Process object whose `ExitCode` is `$null` in this environment, which turned a
**successful** install into a reported failure and skipped the drill's own teardown — it now uses
`-Wait -PassThru -NoNewWindow` with a single-string argument list, and always attempts teardown once it has attempted
an install.

**Evidence:** `src/tools/XrDevicePatch/` (source), `tests/xr-device-patch.ps1` + `artifacts/xr-device-patch-check.*`,
`artifacts/ggm-patch/patch-report.json`, `docs/BUGS.md` TOOL-007, `artifacts/uuvr-spike/run-flat-1/Player.log`,
`config/uuvr-spike-payload.json` (declared mutation + pinned pre-image), `tests/uuvr-patch-roundtrip.ps1` +
`artifacts/uuvr-patch-roundtrip.d062.json`, `artifacts/uuvr-install.d062.*`, `artifacts/uuvr-teardown.d062.*`, and the
read-only layout dump `InfoJson/2022.3.37f1.json` from `AssetRipper/TypeTreeDumps`.


### D-063 — Stage A answers the device-load question: the edit is not the gate, and this build carries no XR loader code (2026-10-07)

Date 2026-10-07. Status: **accepted**. Records the outcome of `PLAYTEST-1B-SPIKE-RERUN.md` Stage A (headset-free,
executed with the user's UAC consent), the mechanism behind the FAIL, and what it means for the project. It does not
adopt UUVR, does not run Stage B, and schedules no further VR work on this target.

**What was run.** `artifacts/run-stage-a-device-check.ps1 -Tag sa2`, 17:08:34Z-17:20:21Z
(`artifacts/stage-a-device-check.sa2.json`): preflight (the simulator is the active OpenXR runtime, the simulator
process is up, no SteamVR, game stock at `B9DF43EE…`/121 files) -> `Install-UuvrSpike.ps1 -Action Install` exit 0 with
`XR_DEVICE_PATCH_APPLIED … 228452B/B9DF43EE… -> 229764B/8DAFFBEF… devices=[OpenVR,OpenXR,Oculus]` and a journal ending
`MutationObserved` -> **one** bounded menu-only launch through Steam (`steam://rungameid/2206270`, pid 49592, first
probe sample after 44 s, closed with `CloseMainWindow`) -> capture of both logs -> `-Action Uninstall` exit 0 -> final
snapshot equal to stock. Verdict: **STAGE_A_FAIL**, PASS conditions 0/3.

**The measurement.** All four probe reports (`t=0.3`, `9.8`, `19.8`, `29.8`) read `xrLoadedDevice='' xrEnabled=False`,
and `Player.log` shows only the engine's subsystem discovery (`1 'displays' descriptors matched` for **both**
`UnitySubsystems/UnityOpenXR` and `UnitySubsystems/XRSDKOpenVR`) plus the deprecation warning from UUVR's own
`XRSettings.enabled` call. Nothing loaded a device.

**Why - and why the D-062 hypothesis was wrong.** Two independent scans of the *build* (not of a log) show that the
player carries no XR loader code at all: `global-metadata.dat` (10,889,004 B) and `GameAssembly.dll` (47,464,960 B)
both contain `XRDisplaySubsystem`/`XRSettings` but **zero** occurrences of `XRGeneralSettings`, `XRManagerSettings`,
`XRLoader`, `OpenXRLoader`, `OpenVRLoader`, `UnityEngine.XR.Management`, `UnityEngine.XR.OpenXR`,
`XRConfigurationData`, `XRDeviceSimulator`, `XRSDKOpenVR` or `UnityOpenXR` - with positive controls
(`UniversalAdditionalCameraData`, `PlayerController`, `Weapon`, `UnityEngine.Rendering.Universal`) proving the scan
finds names of that shape. The XR Plug-in Management package and both providers were never part of this build, so
nothing in the player can call `Load()` and start a display subsystem, and `BuildSettings.enabledVRDevices` - a list
consumed by that very layer - cannot change the outcome whatever it holds. The comparison that made this plain:
`Arken Age`, a shipping VR game on this machine with the *same* native layout, has `enabledVRDevices(0) = []` and loads
VR anyway, because its build *does* carry the XR settings and loaders. D-062's point 2 read the engine's subsystem
lines as "the manifests were fine and the ordered device list was the gate"; that inference was wrong, and Stage A is
what falsified it.

**Consequences.** (1) UUVR is not adopted: SP2 stays FAIL as expected, SP3/SP4 stay unmeasured because Stage B is not
run (the pre-committed rule for a Stage A FAIL). (2) The in-house tool and the install/edit/record/undo plumbing stay -
they are proven end to end, and the round-trip drill and stock verification still pass - but they are no longer
believed to be the missing piece. (3) `docs/BUGS.md` LIM-003 records the target limitation, Q-040 records the decision
the user now owns (drive the headset outside Unity's XR system, choose another target, or stop), and nothing further is
built on the assumption that a settings edit can work.


### D-064 — The port continues by driving the headset ourselves: own OpenXR session, per-eye render-to-texture, submit to the runtime (2026-10-07)

Date 2026-10-07. Status: **accepted** (user decision, Q-040 option (a)). It supersedes nothing; it is the path chosen
*after* D-063 falsified the settings-edit route, and it opens a new pre-Stage-1 sub-stage (**SP5**, `PHASE_PLAN.md`)
with its own pre-committed headless rules (`docs/playtests/SPIKE-OPENXR-HOST.md`).

**Decision.** The mod will not ask Unity to start XR. It opens its own OpenXR session, asks the runtime for the head
pose and the per-eye projections, renders the game's camera twice into the two eye images, and hands those images to
the runtime to compose and display. Unity's XR layer stays unused - it is not present in this build at all (LIM-003) -
and `BuildSettings.enabledVRDevices` is irrelevant to this path. The in-house edit tool (`src/tools/XrDevicePatch`) is
not deleted, but nothing further is built on it.

**Framework search before building (reuse-before-build), result: nothing fits.**
- **UUVR** (GPL-3.0, 2024): already spiked on this build, and it *is* the settings-edit route, i.e. the route LIM-003
  closes. Not adopted (D-063).
- **VRGIN** (`Eusth/VRGIN`, MIT, 161 stars): "VR injection framework for Unity", but targets **Unity 5.0-5.6 with
  OpenVR**, last push 2019-06-24, and explicitly leaves injection to someone else. Approach reference only; no
  reusable code for Unity 2022.3 IL2CPP.
- **NomaiVR** (`Raicuparta/nomai-vr`, MIT, 400 stars): the closest real precedent - Outer Wilds ships no XR support and
  NomaiVR drives the headset itself, rendering each eye with its own camera and projection. MIT, so technique and
  snippets may be borrowed with attribution. But it is OpenVR, and its submission model (hand the compositor an
  arbitrary texture) is the OpenVR one; OpenXR requires rendering *into* runtime-owned swapchain images instead.
- GitHub API search 2026-10-07 (`unity il2cpp vr mod openxr` -> **0 results**; `unity vr injection framework` -> VRGIN
  only). There is no maintained framework for "Unity IL2CPP + no XR plug-in + OpenXR", so building it in-house is the
  only route that is not a downgrade. `src/render/` is written from scratch; no third-party *source* is vendored.

**Architecture chosen, and the measurements it rests on.**
1. **Loader**: the official Khronos `openxr_loader.dll` **1.1.63** (x64, **Apache-2.0**, redistributable) is fetched and
   pinned in `DEPENDENCIES.json` (`openxrLoader`) - deliberately *not* the SteamVR-bundled 1.1.58 copy, so which
   runtime serves us stays the registry's business (`HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime`), exactly as in
   Stage A.
2. **Graphics binding**: `XR_KHR_D3D11_enable`, because the game is D3D11 (`m_GraphicsAPIs = [2]`, D-062). Unity's own
   `ID3D11Device` is obtained **without hooking DXGI**: take a Unity `RenderTexture`'s `GetNativeTexturePtr()`, query
   `ID3D11Texture2D::GetDevice()`, hand that device to `xrCreateSession`. The runtime's swapchain images therefore
   live on the game's own device.
3. **Eyes**: `xrLocateViews` -> per-eye `XrView` (pose + fov) -> two `Camera`s that copy the game camera and render
   into the swapchain images, wrapped as Unity textures with `Texture2D.CreateExternalTexture` and bound through
   `Graphics.SetRenderTarget`/`RenderTargetIdentifier`.
4. **Frame loop**: a dedicated thread does `xrWaitFrame`/`xrBeginFrame` and publishes frame state; the game thread
   renders the eyes from a per-frame callback (`RenderPipelineManager.beginCameraRendering`/`endCameraRendering` or
   `Application.onBeforeRender`) and signals back; the thread calls `xrEndFrame`. Milestone 1 runs free-running (no
   reprojection discipline) so it can be proven before it is made smooth.
5. **Input** is out of SP5: the flat keyboard/mouse path remains the only input until the view works.
   Presence of the required APIs was **measured in this build's own interop assemblies** (generated from its IL2CPP
   metadata, so this is target evidence, not documentation): `GetNativeTexturePtr` (CoreModule x3),
   `CreateExternalTexture` (x1), `CopyTexture` (x21), `SetRenderTarget` (x36), `RenderTargetIdentifier` (x74),
   `set_projectionMatrix` (x4), `set_worldToCameraMatrix` (x4), `beginCameraRendering`/`endCameraRendering` (x7 each),
   `RenderPipelineManager` (x1), `onBeforeRender` (x2), `GetTemporary` (x31), `ScriptableRenderContext` (x38); URP and
   Cinemachine interop exist in the pinned loader quarantine, so the render hook and the game's camera stack are
   reachable from a plugin.

**Why (a) is acceptable at all: it is testable without a headset.** The Meta XR Simulator that Stage A already
registered is a *software OpenXR runtime* on this PC - it accepts an OpenXR session with no headset attached and shows
the composed result in a window - so the chain up to "the runtime composed two different eye images" can be proven
**and photographed** before any headset time. Its D3D11 support and its acceptance of an arbitrary OpenXR client are the
first thing SP5 measures, using the loader's own sample (`hello_xr.exe`, inside the pinned zip) as the cheapest possible
probe: if the sample cannot connect, nothing we write will.

**Fallback ladder (each rung recorded here when taken; no silent downgrade).** (1) If a runtime image cannot be
rendered into through `CreateExternalTexture`: render into our own `RenderTexture`s and `Graphics.CopyTexture` into the
external textures. (2) If Unity cannot bind a runtime image at all: submit a **quad layer** whose swapchain is filled by
a copy from a Unity-rendered texture. (3) If D3D11 interop is impossible from IL2CPP: a native helper DLL owns the
session and receives Unity textures as shared handles - which needs Unity's device handle, i.e. a DXGI hook, and is a
substantially larger build.

**Not claimed.** No OpenXR session has been created on this build yet, no stereo exists, and the simulator's acceptance
of our client is unproven. LIM-003 still stands: Unity's XR path is closed on this target. Nothing here is
headset-verified and the gate stays hard (D-017).


### D-065 — SP5 M2 stays blocked on the simulator, and the next rung is a user decision (2026-10-07)
Date: 2026-10-07 | Stage: SP5 | Supersedes nothing; clarifies the status D-064 left open.
Context: `docs/BUGS.md` TOOL-011. The first milestone that matters under D-064's route (a) is M2: our own client must
get a frame loop running under a headless runtime. It does not. The client now reproduces the Khronos sample's session
setup in every respect the runtime's log and API surface expose - eleven measured configurations, R1-R11: the sample's
exact reference spaces, its four actions over four interaction profiles with two action spaces, swapchain order, the
depth extension with a depth swapchain per eye, apiVersion 1.0.63, a 12_1 device, and the three capability queries we
were skipping - and the runtime-level alternatives were measured too (`use_batch_mode`, the setting Meta's own CI
configuration uses; `ses_texture_format: jpg`; `default_refresh_rate: 90`; verbose logging; a freshly started GUI;
batch mode with the tracking paths repaired). Every one of them ends with the session at `XR_SESSION_STATE_READY` and
the first `xrWaitFrame` blocking forever, while the sample's session advances READY -> SYNCHRONIZED -> VISIBLE ->
FOCUSED about one display period after its `xrBeginSession`. The remaining difference is one the runtime does not log,
and the runtime is closed source, so it cannot be read out of the binary.

Chose: (1) fix and keep the two bugs this round exposed in our own client - the apiVersion literal
(`XR_API_VERSION_1_0` is `XR_MAKE_VERSION(1, 0, 63)` in the pinned header, not 1.0.0, and the runtime logs the value)
and `--parity` not actually enabling the depth extension (it gated on `EnableDepth` instead of the effective
`UseDepth`); (2) make the probe write its receipt *before* the frame loop, so a hang leaves measured evidence instead
of an empty directory, because a hang is a result here; (3) **leave M2 BLOCKED and do not silently redefine it** - no
PASS is claimed, no M2 condition is weakened, and the simulator stays the M2 host; (4) stop attempting headset-free
workarounds, because no headset-free lever is left: the next rung of D-064's ladder is a real runtime (VDXR, the one
this machine normally uses) whose session cannot leave READY without a headset, and the pre-committed SP5 rules forbid
headset time before M4.

Cost: SP5 is parked at M2 with a measured, reproducible block; M3/M4 stay unmeasured. What this does *not* decide: it
does not abandon the simulator (if Meta ships a fix or the missing condition is found, M2 resumes where it stopped), it
does not change the M2/M3/M4 definitions in `docs/playtests/SPIKE-OPENXR-HOST.md`, and it does not touch D-064's
route (a) - the host design is unchanged and already measured up to the frame loop. The next step is the user's and is
recorded as **Q-042**.



### D-066 — M2 spends the headset after all: option (b) of Q-042, under its own pre-committed rules (2026-10-07)
Date: 2026-10-07 | Stage: SP5 (M2) | Overrides SP5 rule 1 for this one sitting, at the owner's instruction; changes no milestone definition.
Context: Q-042 and D-065. M2 (our own OpenXR client gets a frame loop running) is blocked on the simulator: eleven
configurations of full sample parity all end at READY with the first `xrWaitFrame` blocked, while the Khronos sample
advances to FOCUSED on the same runtime (`docs/BUGS.md` TOOL-011). D-065 left the next step to the user and recorded
that D-064's next rung is a real runtime (VDXR, the machine default), whose session cannot leave READY without a
headset - measured again here: with no headset attached, VDXR answers `xrGetSystem` with
`XR_ERROR_FORM_FACTOR_UNAVAILABLE` (`artifacts/openxr/probe/m2-vdxr.json`).
Chose: the user's **option (b)** - spend one bounded headset sitting on M2 against VDXR, so the block is attributed to
the simulator or to our client with evidence. The sitting is governed by rules written before it runs
(`docs/playtests/SP5-M2-VDXR-HEADSET.md`) and executed by `tests/sp5-m2-vdxr-headset.ps1`: preflight (VDXR active,
streamer running), a two-minute wait for the headset to appear, a fixed three-rung ladder (A1 minimal -> A2 `--parity`
-> A3 = A2 with a longer budget) stopping at the first PASS, then the whole-tree stock check. `--user-presence` is not
on the ladder because VDXR does not offer `XR_EXT_user_presence` - measured headset-free first, 31 instance
extensions in `artifacts/openxr/probe/vdxr-extensions.json`, which is what the new probe switch `--extensions` was
added for. Nothing in the game folder is read, written or launched: the probe is a standalone .NET client, and the
game's own camera is M4.
Cost and boundaries: about five minutes of headset time, one sitting, at most three runs. SP5 rule 1 ("no headset time
before M4") is overridden by the rule's owner, not reinterpreted; **M2 stays BLOCKED and `SPIKE-OPENXR-HOST.md` stays
the standard**, so the simulator remains M2's host and no PASS is claimed by this decision - whether a PASS on VDXR
counts as M2 is option (d) of Q-042 and stays open. If VDXR stalls the same way, the fault is in our client and M2
returns to headset-free debugging on the simulator; that outcome is a finding, not a reason for more headset time.



### D-067 — The frame loop renders only while the runtime is displaying the app; the D3D11 shim is gone (2026-10-07)
The two headset runs of D-066 changed three things in the client and one in the project's process.
1. **Frames are submitted only from `XR_SESSION_STATE_VISIBLE` onwards** (TOOL-013). The Khronos sample does exactly
   this; our probe rendered from READY and VDXR answered every frame with `XR_ERROR_POSE_INVALID`. The wait after
   `xrBeginSession` now waits for Visible rather than Synchronized, and `Receipt.SessionState` records the last state the
   runtime reported, so a zero-frame run explains itself instead of only reporting FAIL.
2. **The hand-rolled D3D11 shim is deleted** (TOOL-012); every D3D11 call goes through the generated bindings on a
   `QueryInterface`-AddRef'd pointer (`ComInterop`), with `--selftest-d3d11` as the headset-free check of the whole
   render path. Rationale: a hand-written COM vtable is a second implementation of the ABI, and the SDK's C-style
   `ID3D11DeviceVtbl` is not a reliable index into it (the DeviceContext struct includes the inherited methods, the
   Device one does not).
3. **Touching the runtime's swapchain images is opt-in (`--clear`, off by default).** M2 asks whether the frame loop
   runs, and acquire/wait/release/endFrame answer that without any D3D11 call - so a D3D11 bug can no longer block the
   M2 question, while the clear stays available as a diagnostic.
4. **Process:** the second sitting was taken under the same pre-committed rules as the first, with the B ladder written
   down *before* it ran, and it ended at the pre-committed point - a failure that is ours ends the sitting, so B2 and B3
   were not run. Headset time so far: about two minutes, three runs, no frame submitted.



















































































2. A **third client** (Unity + Meta XR SDK, built from `<UnityProject> project\My project` as a Windows player) is the next
   headset-free experiment on TOOL-011: it is a client Meta develops the simulator against, and its OpenXR source is on
   this disk. It is a *measurement*, not a dependency - no Unity code enters the mod.
3. Recorded, **not taken**: a Unity VR *host* app that displays the game's frames from a shared texture (the game keeps
   its flat rendering; a small Unity player owns the headset, stereo and later the in-VR UI). It is a legitimate fallback
   if the in-process route (D-064 (a)) proves impossible and it would sidestep LIM-003 and TOOL-011 entirely - but it is
   not what the user asked for, so it is written down here and asked as **Q-044** instead of started.
4. The probe's new `--no-state-gate` switch is an **additive test switch** (default off, so every recorded receipt keeps
   its meaning). It reproduces the Khronos sample's frame submission exactly, and it is what eliminated the call-order
   hypothesis on 2026-10-08.

**No silent downgrade.** Nothing here relaxes a gate: M2 stays BLOCKED, M4b still waits for M2/M3, and the headset sitting
of Q-043 is still the only route that tests the runtime the user actually plays on.


### D-071 — The third client gets frames: TOOL-011 is our client's bug, and a VDXR PASS does count as M2 (2026-10-08)
Date 2026-10-08. Status: **accepted** (agent decision under D-068; the user was offered the D-070 third-client
experiment and the M4a continuation and chose the experiment). Stage: SP5 M2.

**What was measured.** Unity 6000.6.4f1 + Meta XR SDK 207.0.0, built as a Windows player from a copy of the user's
project and pointed at the Meta XR Simulator with `XR_RUNTIME_JSON` (no registry change, no headset, the game untouched):
`tests/tool011-unity-client.ps1` -> the session reaches READY -> SYNCHRONIZED -> VISIBLE -> FOCUSED in 1.3 s and the
runtime logs `impl_xrEndFrame: passthrough layer` for it. Receipts: `artifacts/tool011-unity/run-unity1.log`,
`Player-unity1.log`, `meta_xrsim_2026-10-08_51112.log`, `build3.log`.

**Decisions.**
1. **TOOL-011 is attributed to our client.** Two independent client families (the Khronos C++ sample, Meta's own Unity
   stack) drive this simulator's frame loop; only `src/render/OpenXrProbe/` stalls. The "the stall is the test bed's"
   reading behind Q-042 option (d) is **withdrawn**, and the question is answered the other way: **a VDXR PASS is a valid
   M2 PASS**. The situation is no longer "our client versus the test bed" but "our client works on VDXR and not on the
   simulator", which is a bug to fix rather than a milestone to reinterpret.
2. M2 stays **BLOCKED** until a frame is actually submitted; nothing is claimed as PASS.
3. The experiment's build path is recorded because it needed two fixes: the user's project is **locked by a running Unity
   editor** (so the build uses a copy with `Library/` excluded), and `com.meta.xr.simulator` 81.0.0 **does not compile**
   on Unity 6000.6.4f1 (`CS0122`: `IUserInterfaceItem`/`Styles`/`ToolDescriptor` inaccessible in its own Editor scripts),
   so the copy drops that package from its manifest. Both are facts about the reference project, not about the mod.
4. The built player and the `hello_xr` source stay as **references on disk** for the root-cause work; no Unity code enters
   the mod (D-070 stands).

**Consequences.** M2's next work is a root-cause session on our own client rather than another headset sitting: the two
experiments are written into `docs/BUGS.md` (advance the session without `xrWaitFrame`; `xrWaitFrame` under a watchdog).
The headset sitting of Q-043 keeps its value but changes purpose - it now tests whether the *fixed* client works on VDXR,
instead of deciding who is at fault.


### D-072 — M2's stall was our client twice: the loop is never gated off, and layer views are filled through a pointer (2026-10-08)
Date 2026-10-08. Status: **accepted** (agent decision under D-068; the user chose the headset-free route first, then asked
what to do next). Stage: SP5 M2.

**What was measured.** The two experiments D-071 planned were implemented as instrumentation rather than run once, and
`tests/tool011-hang-location.ps1` ran them headset-free on the Meta XR Simulator (`XR_RUNTIME_JSON`, no registry change):
`artifacts/tool011-hang-location.m1..m4.json`.

1. The client **does not** hang in `xrWaitFrame`: `waitFrame -> Success` in 5-20 ms, session `1,2,3,4,5` (READY -> FOCUSED)
   while the loop runs. The old wording ("blocks inside the first `xrWaitFrame` forever") is withdrawn as unmeasured - the
   client's stdout could only show the call was *entered*, and the simulator logs neither `impl_xrWaitFrame` nor
   `impl_xrBeginFrame` at all (its single `impl_xrEndFrame` line appears once in the Khronos sample's log after hundreds of
   frames, so it is not a frame count either).
2. Two defects in our client were the stall: the frame loop was gated off while the session was below VISIBLE, and every
   projection view reached the runtime empty because `Eyes.Submit` took the struct **by value** (TOOL-020, TOOL-021).
3. With both fixed: `verdict=PASS`, `submitted 299/300`, `discarded=0`, `invalidPoseFrames=0`, session `1,2,3,4,5`, both
   with and without `--no-state-gate`.

**Decisions.**
1. **The frame loop runs in every session state.** `xrWaitFrame`/`xrBeginFrame`/`xrEndFrame` are called unconditionally;
   when `shouldRender` is false or the session is not Visible the frame is ended with **no layer**, which is what the
   pinned sample does and what `XrFrameState::shouldRender` asks for. Layers are still only submitted from VISIBLE onwards
   (spec-aligned), but that gate no longer decides whether the loop runs.
2. **`XR_FRAME_DISCARDED` is success.** It increments `FramesDiscarded` and continues; it is no longer a fatal failure.
3. **The verdict reads the furthest session state reached**, not the state before the loop started: the loop is what makes
   a session reach VISIBLE, so the old rule could only ever report FAIL on a client that worked.
4. **TOOL-013's cause is corrected**, not its code: the gate stays (an app should not submit a layer while it is not
   displayed), but the `XR_ERROR_POSE_INVALID` it was written for came from TOOL-021. D-066's and D-071's references to
   "VDXR rejected poses because the session was only READY" are superseded by this.
5. **Structs that a C API fills through a pointer are passed as pointers** in this codebase
   (`CompositionLayerProjectionView*` with `&layerViews[eye]`); a by-value parameter is a silent no-op for every assignment.
6. **Instrumentation is part of the fix, not a debugging aid**: phase snapshots are written to the receipt on disk as the
   frame runs, and `--watchdog-seconds` turns "the client hangs" into a receipt the client writes itself. A sitting can no
   longer end with a killed process and no evidence.
7. **M2 is no longer blocked.** Its frame-loop clause passes headset-free; what remains is one confirmation rung (the D
   ladder on VDXR, pre-committed in `docs/playtests/SP5-M2-VDXR-HEADSET.md`) and M2's colour clause, which the simulator
   can also answer headset-free with `--clear`.

**Consequences.** The next headset sitting is a confirmation of a fixed client rather than a diagnosis, and the same
client now submits frames on **both** runtimes (the simulator here; VDXR's `xrWaitFrame` already returned `Success` with
`shouldRender=1` before this fix). Q-043's purpose is now explicit, and M3/M4b are behind one rung rather than behind a
root cause. TOOL-022 (the missing `xrRequestExitSession` -> STOPPING handshake) stays open as severity 4.

### D-073 — A sitting's cost was a wait that can never succeed; the frame count stays where it earns its keep (2026-10-08)
Date 2026-10-08. Status: **accepted** (agent decision under D-068, at the user's request: "can these checks be shorter?")
Stage: SP5 M2. Evidence: `artifacts/sp5-m2-vdxr-headset.hd4.json` (before), `artifacts/tool011-hang-location.m5.json`
(after).

**What was measured.** The fifth sitting took **76.2 s** for three probe runs, and the frame loops were not what cost it:

| run | wall time | of which the pre-loop wait for Visible | of which the frame loop |
|---|---|---|---|
| `m2-vdxr-wait` (1 frame) | 20.5 s | ~20 s (timed out) | 0.0 s |
| `m2-vdxr-d1` (300 frames) | 27.2 s | ~20 s (timed out) | **6.3 s** (300 frames at ~21 ms) |
| `m2-vdxr-d2` (300 frames, failed) | 20.6 s | ~20 s (timed out) | 0.1 s |

**Decisions.**
1. **The wait for Visible before the frame loop is removed.** It could never succeed: both runtimes only move a session to
   VISIBLE once the client's own frame loop is running (TOOL-011/TOOL-020). It was 20 s of every single run - about 60 s
   of a 76 s sitting. The loop already runs in every state and starts carrying layers as soon as the pumped state is
   VISIBLE, so nothing about what is checked changes; the probe only stops paying for a timeout it knows the answer to.
   `--ready-seconds` now covers the READY wait alone.
2. **`--frames 300` stays for the milestone rungs and drops to 60 for the visual rungs.** M2's PASS condition names
   `frames>=300`, so the rung that carries that clause (D1, C1, A*, B*) keeps 300 - it costs 6.3 s and it is the endurance
   evidence. The rungs whose purpose is a **picture the user reports** (D2, C2: red left / blue right) use 60 frames,
   because 300 frames of the same two colours is not more evidence, and M2's `frames>=300` clause is the other rung's job.
3. **Measured after the change** (`tests/tool011-hang-location.ps1 -Tag m5`, headset-free, simulator): drill **18.4 s**
   (was ~45 s), one 300-frame run **8.8 s** (was 20-27 s), still `verdict=PASS`, `submitted 299/300`, `discarded=0`,
   session `1,2,3,4,5`. A D-ladder sitting is therefore ~20 s of machine time instead of ~76 s, and the remaining cost is
   setup/teardown (~2.5 s per run) rather than a timeout.

**Consequences.** Headset time is now spent on the headset rather than on a client waiting for a state it is itself
responsible for. The next sitting's human cost (put the headset on, read one line, take it off) is unchanged and is the
floor: it is not the machine time that needs cutting now, it is the number of sittings - which is why TOOL-023 (the
`--clear` failure that keeps M2's colour clause open) is being fixed headset-free before the next one.

### D-074 — A typeless swapchain image needs a view that names a format, and `--frames` counts submitted frames (2026-10-08)
Date 2026-10-08. Status: **accepted** (agent decision under D-068, on the user's instruction: "TOOL-023 수정을 지금 시작").
Stage: SP5 M2. Evidence: `artifacts/openxr/probe/tool023-repro.json` (fail), `tool023-fixed.json` and
`artifacts/tool011-hang-location.m7.json` (pass), `artifacts/openxr/probe/selftest-tool023.json`.

**Decision 1: name the format when the resource is typeless.** The runtime's swapchain images are TYPELESS
(`R8G8B8A8_Typeless`, 1680x1760, render target | shader resource, shared, 1 sample), and D3D11 refuses a view on a
typeless resource unless the view names a format - the default view description names the resource's own (typeless)
format, so `CreateRenderTargetView` answered `E_INVALIDARG` on every frame of every `--clear` run. `ComInterop.CreateView`
now builds an explicit `RenderTargetViewDescription` for a typeless resource, with `Format` = **the swapchain's own
format** rather than a format of our choosing: that is the format the compositor will read the image in, and it is the
one the app already committed to at `xrCreateSwapchain`. A typed resource keeps the default description, so a runtime
that hands out typed images is unaffected. Alternatives rejected: a staging copy or `UpdateSubresource` (both write the
image without a view, but they cost a full-resolution copy per eye per frame and neither is needed), and always passing
an explicit description (a format that does not match a *typed* resource is itself `E_INVALIDARG`).

**Decision 2: `--frames` counts frames that were submitted, not attempts.** M2's acceptance clause is "at least 300
submitted frames". The runtime asks for one empty frame at start-up (`shouldRender=0`) which the loop must end without a
layer, so `--frames 300` reported `submitted 299/300` - one short of the clause, for a reason that has nothing to do with
the client. Empty and discarded frames are still counted in the receipt and the loop is still bounded by
`--budget-seconds` and the watchdog; they just do not consume the frame budget. `--frames 300` now reports
`submitted 300/301`.

**Decision 3: the headset-free checks now vary what the runtime actually varies.** `--selftest-d3d11` gained a second
case that clears a **TYPELESS** texture (the shape the runtime hands out), and
`tests/tool011-hang-location.ps1` gained a permanent `-clear` rung that clears the **real runtime-owned** texture at 300
frames. The old self-test passed for a day while every real clear failed because the one input it never varied - the
image's format - was the one that mattered.

**Consequences.** M2's colour clause is no longer blocked: the eye images accept a clear, so a red left / blue right
sitting is now a short confirmation of something already proven headset-free (`m7`), and M3's "picture in the eyes" has a
working write path. The `--clear` path also stops being opt-in-for-a-reason: it is exercised by the drill on every run.




### D-075 — M2 accepted on VDXR with human eye colours; future visual rungs add three measured seconds (2026-10-08)
Status: **accepted**. Stage: SP5 M2 -> M3 gate. User wore the headset and answered: **"왼쪽 빨강, 오른쪽 눈 파랑 맞았는데, 너무 짧았긴 해, 다음부터 3초 더 늘려"**.

**Evidence:** build `9861b63`, `artifacts/sp5-m2-vdxr-headset.hd5.json`, immutable sitting copies `artifacts/openxr/probe/hd5-d1.json` and `hd5-d2.json`. D1 PASS 300/301, D2 PASS 60/61 and cleared=True, both session READY -> FOCUSED with zero frame/wait/locate failures, discarded frames and invalid poses. VDXR format 28 uses an explicit `R8G8B8A8_UNorm` RTV on typeless resources. The full command measured 19.2 s, not the estimated ~12 s; stock check PASS (121 files), no probe process left. Previous generic rung receipts were archived before this sitting.

**Acceptance:** Full M2 COMPLETE, including >=300 valid submissions on the repaired VDXR build and the human-confirmed eye colours. This is not M3 (requires a composed-eye image), M4b (game camera in the eyes), stereo geometry or a confirmed game hook. No acceptance clause downgraded or replaced.

**Duration decision:** C2/D2 keep 60 baseline submitted frames and then `--hold-seconds 3` continues wait/begin/clear/release/end for three additional seconds, measured with the frame loop's monotonic clock. Receipt records requested/measured hold and extra submissions. Default is zero, so D1/endurance/preflight are unaffected. Global budget and watchdog remain bounds; a budget-truncated requested hold FAILs instead of claiming it happened. Rejected: idle sleep after submission (stops the frame loop) or adding guessed frames from a nominal refresh rate (does not guarantee +3 s). The next visual sittings inherit this setting; M2 needs no repeat sitting.

**Next:** validate +3 s headset-free on the simulator and capture the composed eyes for M3 before any game-camera M4b work. Preserve TOOL-022/024 as open rather than closing them through this unrelated acceptance.


**Validation completed (D-075):** `tests/openxr-hold.Tests.ps1 -StartSimulator -Tag hold4` prints PASS for A-D actual rung tables, negative/oversize argument rejection, unchanged baseline (60/61, hold=0), +3 s (276/277, 216 extra submissions, measured 3.0001039 s, zero frame/wait failures or discards) and budget-limited hold (expected FAIL with explicit incomplete-hold error). Evidence `artifacts/openxr-hold.hold4.json`. Debug/Release builds: zero warnings/errors. D3D11 typed/typeless self-test PASS (`selftest-d075.json`). Three PowerShell ASTs PASS. Earlier test wrapper attempts `hold1..3` exposed Windows PowerShell stderr/ExitCode and exclusive-log-read issues in the new test script; they were fixed without changing the probe's duration semantics. `hold3-plus3.json` independently also PASSed with measured 3.0004972 s. No extra headset time spent; no game/runtime registry settings changed.


### D-076 — Unattended headset-free M3/M4b work, with bounded evidence-driven retries (2026-10-08)
User: "이상태로 내가 조작하지 않아도 되게끔 최대한 개발 시작, 방법이 없다 싶을때는 무한반복하지말고 상황보고, 나는 자러간다".
Scope: implement and verify M3 composed-eye capture on the installed Meta XR Simulator; only after its image gate passes, proceed to M4b game-camera stereo. No new headset request, shipping architecture change, anti-cheat/DRM bypass, global runtime registry change or silent gate downgrade. Preserve D-075's +3-second active hold in future visual tests.
Hypotheses: H1 a simulator-window capture proves composed eyes without a private ABI; H2 the advertised METAX1 capture extension is usable only if an exact declaration can be found. The pinned public Khronos header and installed Unity OpenXR/Meta simulator source searches have not provided that ABI; never guess pointer layouts or call an invented signature. Use the already-permitted simulator-window screenshot path first. Runtime-owned swapchain readback alone would not close M3.
Clean request-exit -> STOPPING -> xrEndSession (TOOL-022) must be measured before claiming repeat-run cleanup. Preserve fresh tagged receipts, images, stdout/stderr and actual process-exit evidence. Maximum three failed implementation attempts per core hypothesis; stop with a stall report if no safe evidence-backed path remains. No implication of persistence after the Cline session ends. Work stays under <RepoRoot>.



**D-076 outcome (2026-10-08):** M3 PASS, `artifacts/openxr/probe/m3-auto1/summary.json`; normal/swapped/repeat each 666 submissions, active holds 3.0008251 / 3.0013237 / 3.0000226 s. Actual simulator Both viewport inspected and automatically analyzed (red/blue each ~33.12% image area, solid regions). The first occluded exploratory screenshots were rejected; focusing the named GUI and selecting its discovered Both control exposed the real output. UIAutomation IDs identify the drop-down items; no hard-coded coordinates in the reusable harness. Registered runtime remains VDXR. TOOL-022 normal shutdown now measured: request -> STOPPING -> EndSession, with explicit receipts; VDXR untested. `openxr-hold.Tests.ps1 -Tag hold-d076` PASS including duration, argument/budget checks and clean-shutdown assertions. 7 image fixtures PASS (canonical, swapped, mono, blank, icons, stacked, scattered); added to full verifier. Private METAX1 capture remains unused: installed/pinned headers contain no ABI, attempted Meta doc URLs returned 404. Proprietary VRS comparison script is not a capture interface and is not copied/provisioned. All work project-local; no game or system setting was changed for M3.

### D-077 — M4b next: prove Unity-device bridge and true camera eye math before claiming game stereo
Date: 2026-10-08 | Stage: SP5 after M3 gate
Goal: a real game-camera stereo pair driven by runtime eye poses/FOV, preserved image and numerical evidence, then same-device OpenXR transfer. D-064 architecture and fallback ladder stay authoritative; two identical images or file-fed standalone scene pictures cannot close M4. Existing M4a menu clone already showed scene pixels, but gameplay camera pin and its first black warmup capture remain unresolved.
Prerequisites under investigation: net6.0 IL2CPP plugin versus current net10.0 standalone probe (Vortice 3.8.3 does not target net6); direct runtime pointer bridge through existing Unity APIs, not a new unrelated graphics framework. Do not retarget the game's BepInEx runtime to net10. No stock writes outside transaction engine. Confirm pure eye transforms/projections numerically before installing a test. Keep the player's original camera unmodified. No stage-gate pass or final headset-quality claim on a prerequisite experiment.

**D-077 prerequisite findings:** installed Vortice 3.8.3 TFMs are net8/net9/net10, while the pinned BepInEx game plugin is net6.0. Silk.NET OpenXR 2.23.0 supports net5/netstandard and is compatible in principle; no game runtime retarget is justified. Windows native compiler/SDK are present, so a native helper is possible, but is not selected before D-064's direct Unity/external texture route is tested. Current Unity 2022.3 docs confirm D3D11 external textures accept ID3D11Texture2D/ShaderResourceView pointers with matching dimensions/format; CopyTexture is GPU work, not an automatic OpenXR synchronization boundary. Exact first-submitted eye pose/FOV arrays now recorded (after successful EndFrame), not rounded strings. Snapshot-based eye-camera diagnostic is opt-in/default-off and explicitly NOT an in-game OpenXR host. M4a's old receipt was invalid JSON because the note emitted C# string concatenation; fixed before re-running, not hand-editing old evidence.


**D-077 first live diagnostic:** `stereo-prereq1/run-20261008T160823713` produced all six 960x1008 RGBA images from MainMenu_2/ArenaCamera, baseline nonblack 0.999586/0.999585. Exact runtime IPD ~0.06 m, asymmetric FOV projections retained. Saved PNGs visibly show scene geometry, +10 cm virtual lean and +10-degree yaw. Initial corrected patch analysis: baseline 4 / lean 5 / yaw 6 accepted matches, all positive corrected disparity (1.187875 or 2.187875 px). Raw ~234/235 px difference is predominantly the asymmetric optical-center offset, not giant IPD; explicitly subtract it. This is promising sparse evidence, NOT full geometry acceptance. Source scene animates (particles/character), so low-confidence matches stay recorded. Generated receipt says CAPTURED_NOT_STEREO_VERIFIED and inGameOpenXrHost=false. Shader/device/GPU submission path still unimplemented; never label this OPENXR_HOST_ACTIVE. Latest repeat adds independent view-matrix agreement with Unity's transform and deterministic cleanup nulling. Installer's opt-in config is hashed/planned as the 230th addition; ordinary payload remains 229. No loose game-config write. Transaction install/teardown have 300-second process backstops; capture is bounded 100 s, no blind relaunch.


**D-077 first teardown verified:** `stereo-prereq1/run.json` CAPTURED_NOT_STEREO_VERIFIED, error=null, cleanupError=null, stockVerified=true. Whole runner 16:06:08Z -> 16:11:51Z; fresh loader/interop initialization and full transaction hash/quarantine account for most of this. Journal ends Recovered; independent stock report 121 files / 8 directories, hashes and write times exact. No background game left. This is normal expensive bounded work, not a failed capture loop. Repeat must use a fresh tag and latest view assertions; no game re-install while verifier is doing stock checks. Sparse geometric acceptance and same-device runtime transfer remain open.


**D-076 regression finding:** stricter child-progress-gated capture `m3-auto2` FAILed its image step, not rendering (normal receipt PASS, clean shutdown). All actual capture attempts were refused because Windows did not grant foreground to the simulator. No invalid screen pixels were accepted and no misleading M3 re-pass recorded. Earlier `m3-auto1` images remain real valid Both output, but reusable automation needs the foreground edge fixed. One bounded standard Alt press/release + target activation fallback is being verified; no OS runtime/foreground-lock setting changes. If it fails repeatedly, retain that limitation rather than weakening provenance checks.


**D-076 hardened capture confirmed:** `m3-auto3/summary.json` PASS after the one Alt/target-activation fallback. Current child's >=25 submitted progress required; normal 666 / swapped 667 / repeat 667, hold 3.0027232 / 3.0137608 / 3.0139879 s. Each capture UTC lies within its probe's StartedUtc/FinishedUtc; real exit codes 0, explicit clean shutdown, output SHA256 re-derived and matched. GUI colours occupy 33.1196% each. `m3-auto2` failure retained honestly. Final UTC predicate is now enforced by the harness too, not merely inspected after the run.

**Tooling regression resolved:** TOOL-026 exposed shared root test obj/bin; moved the math csproj into tests/StereoMath, cleaned existing outputs and re-ran `verify-d077-isolated`: all 23 checks PASS, Recon identity 37 passed and math identity 29 passed both exact, stderr empty. Seven colour fixtures and 35 synthetic patch matches (plus raw/rectification negative checks) PASS. Game diagnostic/latest source Release build zero warnings/errors. No test identity relaxation.

**D-077 next bounded prerequisite run:** `stereo-prereq2` adds external Texture2D wrapping of a mod-owned native RenderTexture and GPU CopyTexture, comparing every RGB pixel through synchronous readback after freezing the clone and waiting two frames. This addresses D-064 direct Unity interop viability only; destination is explicitly not runtime-owned. OpenXR acquire/release synchronization is NOT proven by two-frame/readback success. The independent Unity-transform view-matrix assertion must also pass. Standard loader/config transaction and teardown unchanged; fresh tagged evidence, no unbounded process retry.


**D-076 final UTC-enforced run:** `m3-auto4/summary.json` PASS; normal 667 / swapped 666 / repeat 667, hold 3.0128545 / 3.0000959 / 3.0137442 s. Actual captures gated by this child's progress, foreground/occlusion checks and enforced in-run capture UTC. SHA256s re-derived, clean shutdown and exit 0. No TOOL-024 recurrence; it remains open. Latest shared full verifier snapshot preserved as `artifacts/verification-d077-isolated.json` (23/23 passed); pure math 29 and Recon 37 exact identities. This is the stable M3 result, not the rejected auto2 foreground attempt.

**D-077 direct external-texture prerequisite verified:** `stereo-prereq2/run-20261008T161933835/summary.json` captures all baseline/lean/yaw pairs and reports zero mismatched RGB pixels between each frozen camera texture and its external native target. Independent Unity view-matrix agreement did not fire; all six eyes contain scene pixels. `HideFromIl2Cpp` marks the managed exception helper, avoiding the prior benign unsupported-parameter registration warning. This proves the retained target API can wrap/copy native textures, so there is no evidence-backed reason to descend to a quad/reprojection fallback or retarget the game runtime. Next actual host work still requires Unity D3D11 GetDevice with correct AddRef/Release, runtime adapter match, session and owned swapchain textures, render-thread scheduling and GPU fence/release synchronization. Microsoft GetDevice docs and Unity IssuePluginEvent render-thread callback signature checked live; no unverified vtable indices will be reused from TOOL-012. No OPENXR_HOST_ACTIVE or M4b PASS claim until actual composed game eyes exist.

**D-077 repeat teardown complete:** `stereo-prereq2/run.json`: CAPTURED_NOT_STEREO_VERIFIED, install/uninstall exit 0, plugin SHA256 `28766F088216984AD65BC1AE94978E787E523B2EFC0B5859774C643FE1745F77`, error/cleanupError null, stockVerified true. 16:17:17Z -> 16:23:00Z; journal Recovered 16:22:52Z. Independent whole-tree check: 121 files / 8 directories / 7,726,223,158 bytes; hashes/write times exact. Three rungs have 7/6/6 confident corrected-disparity patches, all positive, but too sparse for full geometry acceptance. Game and installer processes gone. Global registry still VDXR, no private saves read/write. Latest-source full verification and hold regressions run before the checkpoint commit; no further game launch planned for this checkpoint.

**Final checkpoint validation (D-076/D-077):** `verify-d077-complete` finished; preserved `artifacts/verification-d077-complete.json`: 23/23 checks PASS, stderr empty, Recon 37/37 and pure stereo math 29/29 identities exact. Seven composed-eye fixtures and 35 synthetic asymmetric-FOV patch matches plus negative shape/rectification checks pass. Latest `hold-d077-final`: baseline unchanged, +3-second run 277/278 with 217 extra submissions and measured 3.0132989 s, clean STOPPING/EndSession, negative/oversize/budget cases pass. D3D11 typed/typeless selftest PASS (`selftest-d077-final.json`); Debug/Release probe and Release game plugin builds zero warnings/errors. Source plugin SHA256 matches the successful second live run. All changed PowerShell parsers and JSON files checked; git diff whitespace clean. Game stock restored; no game/probe/installer processes remain. Checkpoint is M3 COMPLETE and M4b PREREQUISITES VERIFIED, not Stage 1 passed or M4b host complete. No architectural impossibility found; same-device session and render-thread/GPU release sync need the next live implementation session. No unattended process is left to develop after this conversation ends.

Checkpoint source commits: `98f77be` (orderly session shutdown + exact submitted eye views) and `b5f4cca` (M3 automation + opt-in StereoCameraCapture prerequisites). Evidence remains project-local and git-ignored; living documents/playtest scripts preserve how to reproduce it. Resume at D-077's same-device OpenXR host, not M3 research or a headset re-test.


### D-078 — Continue through checkpoints; same-device native M4b bridge
Date: 2026-10-08 | Stage: SP5/M4b | Status: implementation started, not accepted
User explicitly delegates continuation through commits/reports on the same stage. Stop only for a headset gate, human-only decision, unsafe recovery, or a bounded genuine stall. No background-after-final promise.
Reuse: retain validated camera/math and existing OpenXR lifecycle approach. Use already pinned Khronos 1.1.63 headers/loader (Apache-2.0) and installed Windows SDK typed D3D11/WRL interfaces. No Vortice older-version gamble, net6 retarget, new VR framework adoption or invented COM slots. Native bridge is the missing engine device/render-thread adapter, not a replacement framework.
Live documentation checked: Unity 2022.3 NativePluginInterface and GL.IssuePluginEvent; Microsoft GetDevice (returned reference must be released), GetData (S_OK completion vs S_FALSE), Khronos xrReleaseSwapchainImage (requires a waited image). Full spec fetch exceeded tool size; do not infer unseen clauses. Explicit event-query completion is a conservative diagnostic synchronization strategy, not an optimized render loop.
First prove the exact copy/query path in a standalone self-test, then the actual game-device session and runtime-owned copies. Frozen/snapshot scene transfer must be labelled as such until live frame pose/render ordering is verified. No new fallback or downgrade; existing M4b gate remains unchanged.

D-078 first run `host1`: 232 planned additions installed (exit 0), harness exclusive log read failed before plugin activation. No device/session failure inferred. Game terminated, journal Recovered 16:51:12Z, uninstall exit 0 and stockVerified=true, global VDXR unchanged. Fix log reader and explicit prepare-dispatch handshake; rerun host2 after clean restoration.
D-078 host2: stops before native Configure at sRGB assertion. Unity 2022.3 RenderTextureReadWrite documentation checked live: Gamma projects ignore this mode, so sRGB=false does not mean display bytes are linear. Correct rule: Gamma camera outputs display-coded bytes directly; Linear project explicitly requests sRGB target and asserts encoding. Runtime swapchain uses matching SRGB format in both cases (no format fallback). Record project colorSpace and targetSrgb in receipt; compare actual composed appearance, not only flags. This is a colorspace interpretation correction, not lowered acceptance. Native exact copy/query self-test and source/AST guards pass; measured live geometry still absent.

D-078 host3 first same-device evidence: Unity main thread 39140 / render thread 72616, runtime adapter matched, swapchain format 29, session READY -> FOCUSED, 192 stereo layers and 384 explicit GPU completions, +3.0010033 s hold and STOPPING/EndSession success. Exact source/runtime raw hashes equal on both eyes; actual Both screenshot 17:01:02Z visibly shows game scene but upside down. Not M4b accepted. RENDER-001 is real and preempts geometry work. Correct GPU Y orientation with row-wise CopySubresourceRegion (no shader or new dependency, pipeline state untouched) and exact flipped-row self-tests. This has diagnostic overhead (~1008 row copies/eye/frame), not a production performance choice. New self-test four exact + four flipped + four negative shapes PASS. host4 upright actual Both image verified; 192 layers/384 GPU completions, +3.0157965 s, orderly session exit. Continue same stage with actual simulator WASD/arrow-key movement (official 2026-09 docs checked), located pose receipts and per-request runtime/source images, no fabricated motion acceptance. No checkpoint-only stop.

D-078 genuine stop condition: host5 launch did not start a game. Steam screenshot 17:16:01Z confirms Vampire Hunters Steam Cloud cannot synchronize and warns ignoring may permanently delete progress. User-only saved-data risk: do not press ignore, change Cloud settings, inspect saves, or use direct/alternate launch to evade. Finish rollback/verification/docs/commit, then stop before any next launch. host5 uninstall exit 0, journal Recovered 17:18:12Z, stockVerified=true, global VDXR unchanged; no game process remains. Standalone M3 regression is permitted (no game/saves), not another game-launch attempt. Resume at actual simulator motion + full geometry validation after the user resolves Cloud warning. Source motion/capture-request changes are built/unit-tested but not live-validated; no M4b acceptance. Final review changes ReadStatus to try-lock BUSY rather than blocking Unity main thread behind xrWaitFrame; managed 90 s wall watchdog + external launch backstop remain. This safety refinement has build/standalone tests only and requires a live repeat after Q-046. M3-d078 repeat produced completed PASS + clean session teardown but process exit 0x40000015, so overall regression correctly FAIL (TOOL-032); do not hide behind the earlier M3 acceptance.

D-078 final verification: first full verifier 25/25 PASS (`artifacts/verification-d078.json`, adds native GPU + dense stereo geometry fixtures). Synthetic geometry known 2.4 m depth: 529 positive matches; swapped 552 negative; identical images rejected. Native four exact/four reversed/four malformed-shape cases PASS. Fresh M3 `m3-d078-recheck` normal/swapped/repeat all exit 0, actual composed color checks PASS, 666/666/667 layers and +3.0003852/3.0140009/3.0145377 s; TOOL-032 remains observed-once OPEN. Seven edited/new PowerShell ASTs pass; Debug/Release probe + game plugin builds zero warnings/errors, native build/selftests pass. Final hold regression baseline/plus3/negative/oversize/budget cases pass, plus3=276 submissions, 3.0000341 s, clean STOPPING/EndSession. Existing probe typed/typeless clear selftest PASS (`selftest-d078-final.json`). Host4 capture UTC/hash provenance independently rechecked. Final source review changes capture hashes/time/device checks and bounded evidence; latest-source verifier repeat completes 25/25 PASS (`artifacts/verification-d078-final.json`). Final installer additionally pins OpenXR loader DLL SHA256 66946EA6...; optional host read-only plan rechecked (no deployment). Motion/capture-request + BUSY watchdog refinements remain live-unverified because Cloud risk stops game launch. No further live game launch while Cloud blocker exists.


### D-079 — User resolves Cloud blocker; resume unfinished M4b motion/geometry
Date: 2026-10-08 | Stage: Stage 1 / SP5 M4b | Status: live verification pending, not accepted
User reports "클라우드 저장완료". Q-046 resolved by user report; this is not agent inspection of saves or permission to bypass a warning. Resume from adb7d2f on the same M4b gate, not M3 prerequisites. Initial read-only whole-tree check confirms 121 files / 8 directories / 7,726,223,158 bytes, exact hashes/times; no game/probe/test/installer active; global runtime remains VDXR. Existing simulator is available.
Hypothesis: latest BUSY ReadStatus/watchdog and request-capture path can record real located baseline/translation/yaw poses and runtime-owned game images without blocking Unity; composed output must independently support sufficiently distributed positive depth disparity. Plan: rebuild native + managed and run existing motion harness with a fresh tag; inspect all receipts/pixels/provenance and negative controls; refine only demonstrated same-stage failures, repeat orderly +3 s teardown and transactional stock restoration. Any renewed Steam Cloud risk, unsafe recovery, genuine bounded stall or headset gate stops execution. No fallback or acceptance threshold relaxation.

D-079 first live result (2026-10-09 UTC): host6-d079-motion normal Steam launch succeeds, no Cloud warning. Actual capture-request callbacks succeed at submitted 75/109/145; session finishes 669 layers, 1338 explicit GPU completions, +3.0061186 s, STOPPING/EndSession. All eight source/runtime eye image pairs independently match exact reversed rows. Motion correctly FAILS: all located positions/quaternions unchanged after focused 150 ms D/right-arrow. TOOL-033 open; inspect default bindings/headset modifiers rather than repeat an unchanged test. Source geometry 31/33/32 positive, 0 negative, only 3 regions each: insufficient. Swapped 42/42/43 negative and identical 0 positive controls reject. This is not composed dense geometry acceptance and not M4b PASS. Transaction uninstall then exceeded its 300-second backstop (TOOL-034); original run FAIL, exit 2 and stockVerified=false remain unchanged. Five owned roots journaled as quarantined; exact Check-Stock correctly rejects remaining extras. Separate journal-based recovery started; no next game launch until Recovered receipt plus independent stock verification complete. Default binding UI was inspected without modifying mappings: D/right-arrow match intended general movement controls, so simple wrong-key hypothesis rejected. Standalone input-inspection client exit 0, 2016 submissions/+3.0001383 s/clean shutdown; not a restart of M3 acceptance.

D-079 input instrumentation: add bounded exact per-25-submission eye pose samples (<=128) to the existing standalone probe without changing rendering/first-pose/exit semantics. Installed InputPlugin's embedded guidance explicitly says bracket keys cycle active devices; current UI general movement D/arrow confirmed. Hypothesis: pointer over sidebar and active-device selection prevent keyboard head input. Test viewport hover + documented bracket cycling in a standalone diagnostic before another game transaction; no modification to simulator settings, no arbitrary camera offsets. Full game motion still unaccepted. Source Debug build zero warnings/errors. Standalone hover/bracket cycle recorded 128 exact samples with no head motion, exit 0 and orderly teardown: reject hover-only hypothesis. Third bounded input test uses real MapVirtualKey scan codes + extended right-arrow flag and a foreground-checked simulator viewport focus click, not global settings or gameplay input. If no runtime motion again, write a stall report rather than guess a fourth test. TOOL-034 budget changed from 300 to configurable 600 s (range 300-900), keeping all hashes/guards and serial transactions; fresh live budget confirmation still required.

**D-079 genuine bounded stall, not checkpoint stop (2026-10-09 UTC).** Three input approaches failed to produce real head motion: (1) host6 focused 150 ms D/right-arrow, (2) standalone viewport hover + documented bracket cycling with 400 ms holds, (3) standalone viewport click/focus + mapped scan codes/extended arrow flags + bracket cycling. Both standalone runs exit 0 with 128 exact pose samples; all left-eye X=-0.03 and Qy=2.1855694e-8, unchanged. Do not call their submission PASS a motion PASS. Wrong-key and hover/scan-code-only hypotheses rejected. No fourth guessed input test. Next discriminant is a fresh manual-keyboard simulator capture by the human (no headset/game/saves), compared against exact sampled runtime poses. Alternative same-rung investigation: confirm current simulator input dispatch/active headset selection through official UI/SDK, without activating global simulator or private unverified ABI. No framework/quality downgrade authorized. Q-047 records this real input-only gate.

**Recovery completed independently.** host6 original FAIL/exit 2/stockVerified=false stays untouched. Separate recovery.exit=0; journal Recovered 2026-10-09T00:42:27.8042215Z. recovery.log confirms 121 files / 8 directories / 7,726,223,158 bytes, all hashes/write times exact. No hand-deleting stock, saves access, Cloud changes or runtime registration changes. TOOL-034 source budget guard has fixture coverage; fresh full-install/uninstall timeout behavior still unverified, stays OPEN. Q-046 Cloud launch blocker did not recur and is resolved by actual normal host6 Steam launch.

Final provenance independent check passes: four screenshot UTC/SHA256/foreground+occlusion receipts lie inside current game host lifetime; three capture ids match exact submittedFrame=capturedFrame+1, both evidence poses/matrices; VDXR unchanged. Final hold-d079-final invocation fails before live runtime work because no simulator process remained (cause not established, not labeled a crash); preserve exit 1 and retry with existing harness -StartSimulator and a fresh tag. This headset-free regression only starts the already installed simulator, never activates global runtime. The new sample pair/cadence/shape/count checks run in the real hold harness; no relaxed exit or sample identities.

Fresh hold-d079-started (-StartSimulator) completes exit 0: A-D rung tables, invalid/oversize hold, baseline and measured +3-second runtime hold, budget-failure negative all PASS, plus exact first/sample eye-pair identity/cadence/shape/count regression. Probe Debug and Release builds zero warnings/errors. Thirteen edited/new PowerShell ASTs PASS. No fourth motion retry or game launch at Q-047.

D-079 final full verifier exit 0, 25/25 PASS (`artifacts/verification-d079-final.json`); hold plus3=277 submissions/24 exact samples/+3.0136353 s, baseline=60/6; clean EndSession. Invalid/oversize reject; incomplete budget case remains expected FAIL receipt. Read-only host installer plan 232 payload files/121 stock with pinned loader hash passes, no deployment. Final independent stock check passes (stock-d079-final.log: 121 files/8 directories/7,726,223,158 bytes, exact hashes/times); no game/probe/simulator/test/installer process remains. Global VDXR unchanged. M4b still not PASS, Q-047 is the real next gate; no full budget repeat or BUSY-stall/watchdog validation claimed.

### D-080 — Remote-only user; investigate supported simulator input, not a fourth key guess
Date: 2026-10-09 | Stage: Stage 1 / SP5 M4b | Status: investigation in progress, gate unchanged
User is away and explicitly delegates keyboard operation to the agent; only short chat inputs are available. Physical-keyboard comparison cannot be performed by software and is deferred, not fabricated or marked answered. Resume same-rung supported-input research; do not repeat D/right-arrow/bracket guesses, install drivers, change global runtime, enable webcam/forwarding, access saves or apply arbitrary game-camera offsets.
Read-only preflight: HEAD c9d30f0, clean tree/diff check, no target/probe/simulator/test/installer; stock independently exact (121 files / 8 directories / 7,726,223,158 bytes, hashes/times); VDXR unchanged. Probe Debug rebuild has zero warnings/errors.
Live official sources (2026-10-09): Meta xrsim-intro and xrsim-getting-started describe Inputs/Input positioning; xrsim-input-keyboard (updated 2026-09-03) explicitly supports right-mouse-held mouse motion for looking around, WASD and arrows. This mouse-capture technique was not tested in D-079. xrsim-point-and-click (updated 2026-09-11) says Compatibility mode is movement-based controller aiming for missing depth, not a keyboard-delivery fix: reject toggling it speculatively. Meta's Apache-2.0 agentic-tools/docs/metavr-cli.md documents public xrsim frontend/runtime inspection; inspect its input commands before any tool installation or use. Existing installed private DLL symbols are not a public ABI and will not be called.
Hypothesis/plan: first observe a fresh bounded standalone session's live UI input-selection/help and positioning; if supported controls establish a distinct technique, test it with foreground/occlusion/UTC/hash evidence and exact paired xrLocateViews samples. Submission PASS alone is never motion PASS; no game deployment until the discriminator is evidence-backed. Keep Q-047/TOOL-033 open unless the observed failure mode is actually verified; later headset gate and full M4b geometry remain unchanged.
Sources: https://developers.meta.com/horizon/documentation/unity/xrsim-input-keyboard/ ; https://developers.meta.com/horizon/documentation/unity/xrsim-point-and-click/ ; https://developers.meta.com/horizon/documentation/native/xrsim-getting-started/ ; https://raw.githubusercontent.com/meta-quest/agentic-tools/main/docs/metavr-cli.md

D-080 observation harness failure retained at artifacts/openxr/host/input-d080-observe: an exclusive live-log read failed before UI/input work, and the shell tool's 30-second timeout ended monitoring before final exit receipts. Probe receipt is unfinished; no runtime crash, clean shutdown or motion success inferred. No process remains. Correct live log reading to FileShare.ReadWrite and match actual `progress: attempt=N submitted=N` format; launch observe2 as an independently bounded child with persistent runner logs/exit files. This is observation-harness repair, not a fourth movement-key attempt. Public metavr xrsim input documentation contains configuration toggles/source/follow/speed only, no direct head-pose control; no tool installation justified here.

observe2 preserves runner.exit=2: UI viewport was absent after Inputs rail click; probe.exit=0, 2317 stereo submissions/+3.014 s, clean STOPPING/EndSession. No movement inputs sent. Hypothesis: clicking already-open Inputs rail collapsed the panel; inspect initial UI before any rail click, retain panel snapshot and only open if viewport selector is absent. Observe3 is bounded UI diagnosis, not motion acceptance/retry.

observe3 runner/probe exit 0: initial screenshot shows frontend not yet connected although probe has >=25 submitted frames; later Inputs/Both and headset enabled, Keyboard and mouse Connected, movement speed 1.0, camera tracking off, no movement tracker, Point & click off. This evidence rejects a confirmed panel-collapse explanation for observe2; initial readiness was not guaranteed by probe progress alone. Help click revealed no additional control instructions; UI positioning is present below the fold and exposes ScrollItemPattern. Current official RMB mouse-look technique plus enabled/connected UI justifies one distinct bounded mouse-only test (input-d080-mouse1), with positioning scrolled into view, a no-input negative interval and exact runtime samples. No D/arrow/bracket retry, toggle changes or game launch. If no pose change, retain blocked status rather than invent further input hypotheses.

input-d080-mouse1 runner/probe/verifier exit 0: 2317 submissions/+3.0138019 s, 128 paired exact samples, 4 distinct exact left poses. No-input interval frames 549-624 has zero position/orientation change; official RMB mouse input frames 699-749 changes orientation by 100.926793 degrees (mixed pitch/yaw, NOT calibrated pure yaw). Left-eye displacement 0.033377743 m is caused by eye rotation around the head; do not call this head-center translation. UIA headset orientation changes from baseline to (-80, -78.2, 0), positioning values remained below the screenshot fold despite ScrollIntoView: screenshot is provenance, not visible numeric proof. All 7 capture UTC/hash/foreground/process checks pass; clean STOPPING/EndSession, no processes remain, VDXR x64/x86 unchanged. This establishes genuine runtime orientation from supported software input, not a physical-keyboard comparison, game response, lean or M4b PASS. Verifier verdict renamed SIMULATOR_ORIENTATION_OBSERVED_NOT_M4B_PASS (original yaw-only label overstated axis isolation; receipt preserved).
Next same-rung calibration: use 2 events of 1 pixel rather than 10 of 12, collapse irrelevant controller help to expose positioning, and one 150 ms mapped D hold under the now runtime-verified RMB capture. This is not another blind focus/bracket attempt: input-capture path has measured orientation. Calculate head-center translation from midpoint of exact paired eyes, not one rotated eye. Stop if the supported capture cannot deliver the required translation; keep full M4b geometry, negative controls, host motion and 600-second transaction repeat open.

calibrate1 runner/probe/verifier exit 0: 2317 submissions/+3.014053 s. Paired-eye center moves 0.152773834 m after 150 ms mapped D with RMB held; UIA agrees (0,1.7,0)->(0.136,1.7,0.070), rotation change during translation ~0.200 degrees. Thus supported capture can deliver genuine head-center translation. However reducing mouse delta to 2 total pixels still produces -80 pitch/-27.4 yaw: reject a simple pixel-count scaling solution. Positioning header alone became visible, values stayed offscreen. Verifier array parsing repaired for PowerShell JSON arrays, and post-input samples now wait 50 submitted frames (bounded existing samples) to avoid startup/timing false negatives; quaternion angle normalizes runtime quaternions for floating-point accuracy. Initial and delayed measured values preserved separately.
Distinct next calibration hypothesis: relative capture has an initial cursor-origin jump; separately record a capture-prime/release at the exact same viewport point, then repeat bounded mouse look without repositioning or UI click. No head pose reset/offset API. Scroll the actual observed pane to its end for visible positioning. Need calibrated yaw with stable pitch before host geometry; no game install or full transaction repeat yet.

calibrate2 runner/probe/verifier exit 0: 2317/+3.0125862 s, 128 samples, 6 unique poses, no-input delta 0, delayed yaw change 16.399993 degrees, head-center translation 0.151292588 m. Actual visible baseline/prime/yaw/translation screenshots now show headset numbers and agree with UIA. Prime is -80 pitch/-27.4 yaw; subsequent mouse-look keeps pitch -80 and yaw reaches -43.8; re-opening RMB for D resets yaw to -27.2 (16.6-degree contamination) while moving (0.127,1.7,0.083). Thus capture priming helps isolate delayed yaw but does NOT provide upright stable motion for the geometry gate. Reject treating it as calibrated host input. Existing native render camera strips roll; no bypass of pitch/world-scale evidence.
Last bounded input-capture discriminator: on a fresh neutral runtime, hold RMB without any relative mouse movement, then use documented right-arrow and D mappings under that measured capture method. This distinguishes mouse-origin jump from captured key dispatch; exact paired samples plus visible numbers required. No bracket cycling, global activation, driver, synthetic offsets or premature game launch. If upright isolated yaw/translation still cannot be demonstrated, record the calibrated-input stall and stop this line rather than a fourth calibration guess; remote raw motion is proven but the host geometry gate remains blocked.

**D-080 calibrated-input stall (not a checkpoint):** keycapture1 runner/probe exit 0, 2317/+3.0134938 s/clean shutdown, verifier.exit=2 retained. Right-arrow under RMB with no mouse movement leaves exact orientation unchanged (0 degrees), no-input control 0. D under later capture moves head-center 0 m but produces the initial -80 pitch/-27.2 yaw jump (83.756440 degrees); visible positioning agrees. The successful raw mouse/translation path is real but not reproducibly upright/isolated. Calibration attempts (pixel scaling, capture prime, no-mouse captured keys) are exhausted; no fourth calibration guess or speculative game deployment. Next same-rung alternative is a documented simulator input-source/dispatch technique that avoids capture-origin jumps, not an API invented from private symbols. Human physical-keyboard provenance remains NOT_PERFORMED and is not requested while away. No quality downgrade or M4b PASS. TOOL-033/Q-047 stay open; dense composed geometry/lean/pure yaw and the full 600-second install/uninstall repeat remain pending.

D-080 verification monitoring: first full invocation hangs in its wrapper after native fixtures finish PASS (no test descendants observed for several minutes); exact own verifier/wrapper ids stopped boundedly, monitor JSON preserves incomplete result and no full PASS. Cause unestablished; a wrapper-only change invokes existing verify.ps1 in the same PowerShell process and keeps persistent logs. Retry is progressing through normal fixture stages; final full verdict is not inferred from partial output. No product/test acceptance criteria were relaxed. All three project-local helper ASTs pass; final independent stock recheck again exact, no game deployment/save access, VDXR unchanged.

D-080 final verifier retry completed actual exit 0; artifacts/verification-d080-final.json has 25/25 Passed=true, stderr empty. Native exact/flipped/shape fixtures PASS; transaction 33/33, entry 28/28, Recon 37/37 and config/stereo checks included. Original incomplete monitor stays separate, not retroactively PASS. Four motion receipt rechecks match expected exits (mouse1/calibrate1/calibrate2=0 for raw orientation, keycapture1=2 for failed requested motion); all say uprightCalibrated=false, physicalKeyboardComparison=NOT_PERFORMED and m4bPass=false. Translation isolation is explicitly separate: calibrate1 <1-degree change, calibrate2 16.6-degree contamination. No new live host/installer transaction or BUSY/watchdog claim. Living documents preserve bounded stall, user-away scope and reproducible evidence; no processes are left developing unattended after final response.





















































































































### D-093 — Virtual Xbox pad + headset-anchored UI (2026-10-09, built, NOT headset tested)
- User report (ha4): movement is 4-way (WASD scheme), UI/aim/level-up follow the mouse, menus unusable. Cause (static): HandInput called MoveInput, which the Keyboard&Mouse scheme quantizes; overlay canvases were placed from the game camera, which follows mouse look, not the headset.
- VirtualPad.cs: in-process Unity Input System "Gamepad" device (state event GPAD, GamepadState 28 bytes: buttons@0, leftStick@4, rightStick@12, triggers@20/24). No OS driver, no keyboard/mouse synthesis. PlayerInput.SwitchCurrentControlScheme("Gamepad") + neverAutoSwitchControlSchemes.
- Bindings from the game's action asset: Move=leftStick, Jump=South(A), Start=start, Run=leftShoulder, Bloody Rush=leftStickPress, Fire=rightTrigger, UI Navigate=leftStick/dpad, Submit/Proceed=West. Mapped: left stick=move, left menu=Start, right A=South (also UI confirm), left X=leftStickPress (dash/special - UNVERIFIED which action the game gives it).
- Move is rotated by (headset yaw - game camera yaw), so the stick moves relative to where the player looks. While paused the raw stick drives UI navigation.
- UI: HandUi places canvases from the HMD pose (mean of both eye cameras, set each frame via SetHead), menus once on pause, HUD with a 25-degree dead zone. Controller ray + trigger click kept.
- Fallback if the pad cannot be created: PlayerInputController.MoveInput/JumpInput (still 4-way). The receipt shows pad/padSent/padError/padNote/scheme/moveYawDelta.
- UNVERIFIED: whether the game accepts the synthetic device (lowest-risk guess: layout name Gamepad), whether QueueEvent with an unmanaged buffer is accepted, whether the level-up cards respond to pad navigation, and whether native crash c0000005 (ha3) was HandShot. Run with -NoShot.


### D-095 — UI ray, aim parallax, eye resolution, frame pacing (2026-10-09, built, NOT headset tested)
- ha6 measurement: 4590 XR frames in 230.7 s (sessionStartedUtc to updatedUtc in host.json) = about 19.9 fps. The Unity loop runs at ~120 Hz (deltaTime 0.0083, vSync=1), and the host spends 3 Unity frames per XR frame (renderUntil = frameCount+3) plus one more frame to dispatch the next prepare, so the cycle is about 6 Unity frames. That is the main suspect for the 20 fps; the GPU copy wait and Virtual Desktop pacing are not yet measured.
- UI ray: HandUi draws a thin cylinder (hand colour) from the aim origin to the canvas hit plus a dot at the hit while paused (UiRayDraw). It uses the same unlit, always-on-top material as the reticle (EnsureReticleMaterial). Hidden when not paused. Visibility is unverified.
- Aim offset: ha6 receipt shows muzzleToHandForward = ~7.3 deg (the muzzle is not parallel to the hand pose) and `shot` = null (no shot-direction measurement ran), so the cause of the impact offset is NOT proven. HandFireTrigger now aims the game camera from the muzzle position and direction during player.fire (was direction only), removing parallax against the reticle. Needs a ha7 look at the [SHOT]/tracer lines.
- Resolution: new config EyeWidth/EyeHeight (default 2064x2208 per eye, set by the user; the old default was 960x1008; clamped 512..4096). The RenderTexture size is also the native swapchain size, so no native change was needed. headset-handaim.ps1 has -EyeWidth/-EyeHeight.
- Frame pacing: RenderFrames (1..3, default 2 by user choice for ha7; 3 = the old behaviour) and DisableVSync. RenderFrames=1 renders once and requests the next prepare in the same Unity frame as the submit completes. -RenderFrames 1 -DisableVSync is the first thing to try in ha7. Risk: stale or half-rendered eye textures if 1 frame is not enough; the fault-test fixtures are unaffected (native code unchanged).
- NOT done: 90 Hz display refresh request (XR_FB_display_refresh_rate is Meta-only; Virtual Desktop VDXR support unknown, Q needed), starting VR from the menu screen (GameplayOnly + the HandAim ownership rules assume a player camera; needs its own design), the cause of the ha6 host failure is known: the player left the stage (scene Maze_Small -> MainMenu_2) and the ownership check threw.
- HandInput: schemeSwitched resets when a new PlayerInputController is found so the pad re-attaches on the next stage.

### D-096 — ha8 result and UI placement fixes (2026-10-10, built, NOT headset tested)
- ha8 (-NoShot, RenderFrames 2, 2064x2208): no crash. The host ended on the known ownership check (Maze_Small -> MainMenu_2, the player left the stage), not on a fault. Rough rate: 10710 / 376 s (host.json startedUtc..updatedUtc) = about 28 fps vs 19.9 in ha6. This is an estimate: 10710 is the hand-aim native seq counter, not a verified XR frame count.
- Video 1010 (user report plus frames at 84/90/98 s): the pause panel is a dark, tilted, off-centre plane and both UI rays are visible, so the D-095 ray works. The panel is not centred on the view.
- HandUi changes: (1) menus no longer freeze after the first placement. They follow the HMD yaw with a 35-degree dead zone and re-anchor when the head moves more than 0.4 m. (2) Level Up canvases (path contains "Level Up") sit at 1.0 m and 1.4 m wide instead of 1.6 m / 1.9 m. (3) World-space scale compensates the parent lossyScale, because the Level Up and HUD canvases are children of the player prefab. (4) UiDim caps alpha at 0.25 on near-full-width, dark, non-Selectable Graphics while paused (receipt ui.dimmed counts them).
- UNVERIFIED: which Graphic is the dark backdrop (the heuristic is a guess; if the dark area remains, log the Graphic names), whether the tilt came from the parent scale or the pivot, and the "fires from the body" report (ha8 ran -NoShot; HandFireTrigger has aimed the game camera from the muzzle since D-095, but where the game spawns bullets is unmeasured).
- Not done: 90 Hz display request, VR start from the menu screen.

### D-097 — HandShot crash: the draw_shoot_vfx (tracer) patch is the prime suspect; ha9 runs without it (2026-10-10, built, NOT headset tested)
- Evidence (minidumps in %LOCALAPPDATA%\CrashDumps, read with artifacts\devtools\dump-info.ps1 / dump-stack.ps1 / dump-fault.ps1, no debugger needed): ha7 (116404) and ha3 (111748) both died with c0000005, read access to address 0x0 (params 0x0,0x0), on a thread running managed code. The faulting RIP is in unbacked memory outside every loaded module (a runtime-generated thunk). Both fault pages contain the same JIT code: `push rdi/rsi/rbx; sub rsp,0x70; vzeroupper; ... vmovss xmm2,[rsi]; vmovss xmm1,[rsi+4]; vmovss xmm2,[rsi+8]; vmovss ... [rdi],[rdi+4],[rdi+8]` with RSI = 0, RDX/R8 as the call arguments. That is the shape of a method reading two `Vector3` values through pointers, i.e. a Harmony prefix with two Vector3 parameters. The stack of the ha7 dump also goes through coreclr.dll (managed runtime) and GameAssembly.dll.
- That matches `ShotAim.PrefixTracer(Weapon, Vector3 __0, Vector3 __1)` for `Weapon.draw_shoot_vfx`. The Il2Cpp detour apparently passes the value-type arguments as pointers, and one was null. It is an inference from the code shape, not a proof (no symbols, no managed stack walk).
- Why ha4/ha5/ha6/ha8 survived: they all ran -NoShot, so neither Harmony patch existed. The same facts leave `shoot_forward` (the Transform override) unproven in either direction: it was installed in ha3/ha7 too and was never seen to run.
- Change: `ShotAim.Install(camera, overrideArg, tracer=false)` patches ONLY `Weapon.shoot_forward` (reference-type Transform argument, replaced by the muzzle when it is null or the game camera). The tracer measurement is behind the new config `HandShotTracer` / script switch `-ShotTracer` (default OFF; it is expected to crash).
- ha9 = `-RenderFrames 1 -DisableVSync`, WITHOUT -NoShot. If it crashes again the suspect list is shoot_forward itself (the Transform out/ref handling: `ref Transform? __8` may be passed as a pointer too) and the next step is to drop the override, keep a log-only prefix on the object parameter, or stop patching and rely on HandFireTrigger aiming the game camera from the muzzle.


### D-098 — ha9 result: D-097 shot build did not crash; HAND_AIM receipt died on a JSON infinity; fps 47.5 (2026-10-10)
- ha9 = `-RenderFrames 1 -DisableVSync`, no -NoShot, `SHOT_AIM patch installed=True tracer=False`. The game stayed alive for the whole 65 s session; no new dump appeared in CrashDumps (latest remain 111748 and 116404). The session ended because the user left the stage to MainMenu_2 (`gameplay camera/scene ownership lost`, a deliberate-looking scene change, not a crash). Cleanup was verified, `stock=True`.
- Not proven: whether bullets were fired through the patch. `HAND_AIM disabled: System.ArgumentException: .NET number values such as positive and negative infinity cannot be written as valid JSON` was logged right after HAND_FIRE enabled, so the hand-aim receipt (which also carries the shot counters) was never written. Cause: `lastTracerToMuzzleDeg`/`lastTracerToCameraDeg` are initialised to infinity and were serialised raw. With the tracer patch off they never become finite. Fix (built, 0 warnings / 0 errors): non-finite values are written as null, and `tracerPatched` is added to the receipt. Consequence: ha9 gives NO shot-call evidence; whether `shoot_forward` ran is still unknown. Whether HAND_AIM being disabled also stopped the muzzle/hand driving is unknown too, so the aim/muzzle feel in ha9 is not a valid test of D-097.
- fps by the same metric (Frames / (updatedUtc - sessionStartedUtc)): ha6 19.9, ha8 29.6 (earlier estimate 28), ha9 47.5. ha9 is a short session (48 s), so treat it as indicative. Caveat: ha9 had less to render (a mix of paused/unpaused), not a like-for-like scene.
- Status: D-097 crash fix is still "unverified": one survived session without crash, but no evidence that the patched method ran. ha10 repeats the same command with the receipt fixed.


### D-099 — Root cause of "character does not move / could not shoot" in ha9, plus snap turn (2026-10-10, built, NOT headset tested)
- Cause (supersedes the "unknown" in D-098): the JSON-infinity exception came out of `WriteReceipt`, which ran inside `HandAimDriver.Tick`. `OpenXrHost.LateUpdate` caught it, logged `HAND_AIM disabled`, called `handAim.Release()` and set `handAim=null`. That removes the WHOLE hand driver, so locomotion (`InputTick`/VirtualPad), trigger fire, UI rays and aim all stopped right after `HAND_FIRE enabled`. The user saw: character does not move, could not shoot. ha8 (-NoShot) had `Shot==null`, so the infinity never appeared and input worked (moveCalls 9340, padSent 9436).
- Fix 1: the receipt write is now wrapped in its own try/catch in `Tick`. A receipt failure is stored in `receiptError` (written to the receipt itself if a later write succeeds) and never disables aim, fire, input or UI. Fix 2 (D-098): non-finite tracer values are written as null.
- Snap turn (D-013 asked for snap turn only, no smooth turn / vignette / teleport): right thumbstick X. A push past 0.7 turns once by `SnapTurnDegrees` (default 30, config key `SnapTurnDegrees`, script `-SnapTurnDegrees N`, 0 = off). It re-arms only after the stick returns under 0.3, so holding the stick does not spin. `HandAimDriver.SnapTick` accumulates the request; `OpenXrHost.ApplySnap` rotates `anchorRotation` about world-up and shifts `anchorPosition` so the head stays at the same world point (pivot = the tracked head position in the eye-space pose). Receipt fields: `input.snapDegrees/snapTurns/snapTotalDegrees`.
- Assumptions / unverified: (a) the sign of the turn (right push = clockwise view turn) is expected from the math but unmeasured; (b) move direction follows the headset yaw (`HeadRelative`), so after a snap the left stick should still move "where I look" - needs a headset check; (c) the hands/muzzles and the UI rays are anchored through `anchorRotation`/`origin` passed into `Tick` each frame, so they should follow the snap, also unmeasured; (d) the pad's right stick is still sent as 0, so the game's own camera yaw is not turned by it.
- ha10 (same command as ha9) did not run: `xrGetSystem returned ErrorFormFactorUnavailable` (Virtual Desktop headset not attached within 300 s). Cleanup path ran; stock check result is in ha10.out.txt. Next run: ha11.



### D-100 — ha11 findings, gamepad mapping from the game's own action asset, game crosshair hidden, ammo-drain measurement (2026-10-10, built, NOT headset tested)
- ha11 ran 228 s at 41.8 fps (`Frames / (updatedUtc - sessionStartedUtc)`; ha9 47.5, ha8 29.6) with `receiptError=null` (D-099 verified on a headset), no `HAND_AIM disabled`, no new crash dump, stock restored. Movement (6478 stick changes), jump (21), fire and UI (2 clicks, 195 hovers, 45 dimmed) all worked. `shoot_forward` IS reached: `shootCalls=2637`, every call `argNull`, every call overridden with the hand muzzle (`overrides=2637`). It ended with `HOST_FAILED source-reparented` when the user left the stage, which is the known ownership rule (ha9), not a crash. Snap turn was not used (`snapTurns=0`); still unmeasured.
- User report (video 1010, which I could not open: the file is not on any scanned path, so this is the user's description, not my observation): from some moment the aim point is completely off. In the headset recording the HUD follows the eyes while the bullets leave in another direction; in the game-camera recording the HUD follows the hand. Reading: the game's crosshair is a head-locked overlay (canvas `Canvas - ComboAndCrosshairs`, component `Crosshair`) that was never meant to follow the hand, while the shots now follow the hand muzzle (D-094/D-095/D-098). Two things point in two directions, so the crosshair misleads. Fix (HandUi.HideGameCrosshair): every Graphic under a `Crosshair` component is culled at its CanvasRenderer (the combo counter in the same canvas stays). The per-hand sphere reticle is now the only aim mark. Unverified: that the cull hides every crosshair visual (the receipt reports `ui.crosshairHidden`), and that the bullets really leave from the muzzle.
- Gamepad mapping, read from the game's InputActionAsset (`artifacts/inputsystem-recon/actions/ss1.txt`, Gamepad control scheme): Move=leftStick, Look=rightStick, Fire=rightTrigger, Fire2=leftTrigger, Reload=West, Jump=South, **Bloody Rush=North**, **Run=leftStickPress**, **ArmL=leftShoulder**, **ArmR=rightShoulder**, **Special Ability=East**, Start, Select. The D-093 note 'left X = leftStickPress (dash/special - UNVERIFIED)' was wrong: leftStickPress is Run, the scout dash is Special Ability on East.
- New VR layout (user request 2026-10-10): right A = Jump (South, unchanged); **right stick up = Special Ability** (the scout's dash, East); **right stick down = Bloody Rush** (North); **left stick click = ArmL** (leftShoulder); **right stick click = ArmR** (rightShoulder); left X = Run (leftStickPress, kept from before so nothing the user already uses disappears); left menu = Start; stick X right = snap turn. Right stick up/down uses `StickLatchPolicy` (press 0.7, release 0.3, the vertical component must dominate the horizontal one so a snap-turn push that drifts is not a dash). This supersedes the D-057 table (right stick up = interact, down = blood ability) in `docs/INPUT-MAP.md` and `config/bindings.json`; those two files are design data and were NOT edited here (their validator pins the old layout). Interact (Z) and reload are still unwired; the stick-up slot that D-057 gave to interact is now the special ability, so interact needs a new home (open question).
- Ammo drain (user: a 39-round weapon is empty after about 10 shots in VR, not on desktop). Nothing in the logs records the ammo value, so the cause is NOT proven. What the data say: (1) `shoot_forward` runs at a fixed ~22 calls/s while the trigger is held, independent of fps (40 or 60), so it is timer-driven, not frame-driven. (2) `[SHOT]` log lines come in runs of 3-4 identical calls (same muzzle direction) before the direction changes, so one trigger pull produces several `shoot_forward` calls in a row. (3) `HandFireTrigger` calls `PlayerController.fire(slot, ref true, dt)` every Unity frame while held (~40-60/s), where the desktop path is driven by one Fire input per frame that the game debounces with its own fire timer. Hypotheses, in the order I would test: H1 the game consumes one ammo per `fire` call that passes the fire-rate gate, and the extra calls per frame or `fire_second_shot`/lazy fire add shots; H2 the pressed flag must be a one-frame edge (true once, then false) instead of true every frame, which is how `fire_weapons_if_holding_down_fire_input(dt)` would normally keep a held trigger going; H3 both hands share ammo/fire state. Measurement added (read-only, no behavior change): in `ShotAim.RecordAmmo` each `shoot_forward` reads `Weapon.current_ammo`; the receipt now has `shot.ammo` = first/last ammo, `maxAmmo` (Weapon.max_ammo()), `fireRate`, `drops`, `dropTotal`, `maxDrop`, `maxShootsPerFrame`. Next headset run: fire a short burst on a fresh magazine and compare `drops` with `shootCalls` and `fire calls`. Do not change the fire call before that number exists.
- Tests: 88 hand-aim fixtures (was 73): `StickLatchPolicy` (7) and source guards for pad bits, mapping, release-all, read-only ammo measurement, finite-safe receipt, crosshair culling. `verify.ps1` now expects RESULT 88.
- Receipt change: `input.dashPresses` is gone; `runPresses`, `specialPresses`, `bloodPresses`, `armLPresses`, `armRPresses` replace it.


### D-101 — left X = Interact, reload = Y/B beside the headset, ammo drain measured without counting by eye (2026-10-10, built, NOT headset tested)
- User corrections: (1) this game has only dash (differs per character; the scout's is Special Ability), jump and basic move, so my "left X = Run" note was meaningless to the user; (2) left X should be INTERACT; (3) reload should fire when the upper face button (left Y / right B) is pressed with that controller right beside the headset; (4) the user cannot count the ammo by eye, it empties too fast.
- Pad evidence (`artifacts/inputsystem-recon/actions/ss1.strings.txt`, strings of the game's three input assets): in the `PlayerInputActions_Arena` asset `<Gamepad>/leftStickPress` is bound to **Interact** (string offset 37761 -> 37801), `buttonEast` to Special Ability, `buttonWest` to Reload. In the two other assets (`PlayerInputActions`, `_SubmitSwitched`) the same button is **Run** and there is no Interact binding at all. Which asset the running stage uses is NOT proven, so the left X press may be Interact (Arena) or Run (other assets). The user says the game has no run, so a Run on that button is expected to be harmless; this is a headset question (Q-D101-1).
- Change: left X (`Lower`) still sends leftStickPress, now named `interact` in code and `input.interactPresses` in the receipt (`runPresses` is gone). Reload = pad `buttonWest` (bit 7), started only on the press EDGE of Y / B while that controller is within 0.30 m of the headset (`HeadReachPolicy`, `ReloadLatchPolicy`), then held until the button is released. Pressing far away and moving in does not start it; unknown head or hand pose never starts it. The distance is controller origin to the mean of the two eye cameras (`SetHead`). 0.30 m is a guess: `input.minFacePressHeadDistance` / `lastFacePressHeadDistance` record the real distances at each Y / B press so the threshold can be tuned from data.
- Y / B was already the "secondary-fire selector" that blocks the primary while held (`!slot.Face` in `FireTick`); that is unchanged, so a reload press does not also fire. Y / B pressed AWAY from the head still only blocks the primary (no secondary fire is wired).
- Reload goes through the same virtual Xbox pad as the other buttons, so it needs the pad path; the no-pad fallback has no reload or interact route.
- Ammo, user cannot count: the receipt now records, per hand, `triggerPresses` (press edges) and `heldSeconds` next to `fireCalls`, plus the existing `shot.ammo` (`drops`, `dropTotal`, `maxDrop`, `maxShootsPerFrame`, `first`, `last`, `maxAmmo`, `fireRate`). So the next sitting only needs "press the trigger N times / hold for a while on a fresh magazine", and the agent compares ammo spent per press and per second. Still no change to the fire call (rule from D-100: no fix before the numbers exist).
- Video `<Recordings>\편집후\1010 (3).mp4` was found (854x480, 24 fps, 2:04.9, 27 MB; the earlier search missed it). I extracted frames with the ffmpeg that ships with CapCut and looked at six of them: they show the 2D desktop mirror of the VR view (a stage with a pillar, wooden beams, crowds of enemies, a hand-held weapon with green ammo digits at the left edge, a small round gauge near the pillar, and the clock text at the right). I did NOT yet analyse the reticle-versus-HUD mismatch frame by frame, so Q-D100-1/TOOL-054 stay about that analysis.
- Tests: 99 hand-aim fixtures (was 88): `HeadReachPolicy` (2), `ReloadLatchPolicy` (5), source guards for reload wiring, trigger counters and receipt fields; two older guards renamed (`interact`, extended release-all list). `verify.ps1` expects RESULT 99.

### D-102 — head-locked HUD, dark translucent HUD backdrop culled (2026-10-10, built, NOT headset tested)
- User answers: the minimap, score and combo must stay; only the dark translucent effect behind the HUD panel goes; every existing HUD element is kept as it is and locked to the head.
- Evidence for the dark effect: frame at 40 s of `<Recordings>\편집후\1010 (3).mp4` shows a dark translucent rectangle at the left edge with a list of percentage numbers over it, during play (not paused). The old `UiDim` only runs while paused and only caps alpha at 0.25, so it never touched it. The frame is 854x480, so the identification is an inference, not proof of the exact object.
- Change 1 (head lock): canvases whose own name contains `Player HUD`, `HealthBarMainHUD`, `ComboAndCrosshairs`, `Canvas - Score` or `FloatingIcons` (`HudLayoutPolicy`) are placed rigidly on the head every frame while not paused: position = head + head rotation * forward * 1.5 m, rotation = head rotation, world width 1.6 m (`HudDistance`, `HudWidth`). The yaw dead zone (25 degrees, slow follow) no longer applies to them. While paused they use the old menu placement, so the pointer ray still works. `Canvas - Screens`, Level Up, Alert/Tooltip, Health Bars and Skip Cutscene stay on the old path.
- Change 2 (dark backdrop): on those canvases only, an `Image`/`RawImage` is culled at its CanvasRenderer (`HudDarkPanelPolicy.ShouldHide`) when it is not Filled, its max colour channel <= 0.35, alpha is 0.02..0.95 and its area is >= 2 % of the canvas. Filled images (gauges), text, icons, bright or opaque images, and Selectables are kept. The cull is undone in `UiRelease`.
- Risk: a dark translucent but legitimate bar background of at least 2 % canvas area would also vanish. Mitigation: every dark translucent candidate (path, rgba, size fraction, filled, HIDDEN or kept; first 40) is written to `hand-aim.json` as `ui.hudDarkCandidates`, with `ui.hudDarkHidden` and `ui.hudLocked`, so the next sitting can correct the thresholds from data instead of guessing.
- Not done: the HUD size and distance (1.5 m / 1.6 m) are first guesses and may need tuning for the corner elements to be inside the view.
- Tests: 108 hand-aim fixtures (was 99): lock name matching (2), dark panel policy (4), source guards (3). `verify.ps1` expects RESULT 108. Plugin build: 0 warnings, 0 errors.


### D-103 — haptic pulse when a hand enters the 0.30 m reach beside the headset (2026-10-10, built, NOT headset tested)
- User request: a short controller pulse when a hand comes within 0.30 m of the headset, so the reload zone can be felt without looking.
- Native (needs the rebuilt `VhvrNativeHost.dll`): a new OpenXR action `haptic` (`XR_ACTION_TYPE_VIBRATION_OUTPUT`) bound to `/user/hand/{left,right}/output/haptic` on the Touch profile, `HandsHaptic()` (`xrApplyHapticFeedback`, frequency unspecified), and two exports `RequestHaptic(hand, amplitude, durationMs)` / `ReadHapticError()`. The managed side only QUEUES a request (0 queued, 1 unavailable, 2 bad argument, 6 busy); the vibration is applied on the render thread right after `xrSyncActions`, so no OpenXR call is made from Unity's main thread. A failed `xrApplyHapticFeedback` sets `hapticDisabled` only: the hand sample, the aim path and the video path keep running. `XR_SESSION_NOT_FOCUSED` is a success code and counts as skipped, not applied.
- Managed (`HeadHapticPolicy`, `HandInput.cs`): pulse only on the ENTERING frame of each hand, using the same `HeadDistance` and `HeadReachPolicy.Near` as the reload, so the buzz means exactly "reload works here". Only that controller vibrates. Staying near gives nothing more; the hand re-arms only after it has left by 0.36 m (hysteresis), plus a 0.4 s minimum interval, so a hand resting on the 0.30 m line does not buzz. Not while paused, not when the hand pose is unknown. Amplitude 0.4 and 60 ms are the defaults (`HapticAmplitude`, `HapticMilliseconds` in the config; `HandHaptic` on by default, harness switch `-NoHaptic`).
- Receipt: `haptic.{enabled, amplitude, milliseconds, pulsesLeft, pulsesRight, rejected, error}`. `pulses` counts accepted requests, not felt vibration: whether the controller really buzzes depends on the runtime (Virtual Desktop) and is a headset question.
- Tests: 120 hand-aim fixtures (was 108): policy (8) and source guards (4); native fault fixtures 40 (was 37): queue-and-apply-once, failure isolation, argument contract. `verify.ps1` expects RESULT 120. Native DLL builds with `/W4 /WX`; plugin 0 warnings, 0 errors.

### D-104 — frame timing measurement and opt-in anisotropic filtering (2026-10-10, built, NOT headset tested)
- User request: raise the low frame rate (no stutter so far) and make the image sharper.
- ha11 evidence (`artifacts/headset-d087/ha11`): `hand-aim.timeline.jsonl` gives 41.6 fps on average (19.4..61.1 per 2 s window). The windows cluster at about 40 and about 60 fps. `Frames` was 9540 and the Unity frame counter in `host.json.evidence` advanced about 4.9 Unity frames per XR frame. `game.log` shows Unity `vSync=0` (DisableVSync works). 128 of 229 timeline rows had `frames` stuck at the end value because the monitor kept sampling after the native close; they are not low-fps samples (my first median of 0 came from them).
- INFERENCE, not proven: the 40 / 60 clustering looks like a quantised display period (120 Hz / 3 = 40, 120 / 2 = 60), but the headset refresh rate was never recorded. No timing exists yet for `xrWaitFrame`, the Unity render wait, or the GPU copy, so which of them limits the rate is NOT known.
- Sharpness evidence (ha11 eye0 raw, 2064x2208, `artifacts/perf-d104/eye0-*.png`): the blocky walls and floor are low-resolution game textures (texel blocks visible at native size), so a higher render resolution will not fix them. Edge aliasing is possible because the eye RenderTexture is created with `antiAliasing=1`. The eye size already equals the Quest 3 panel size; Virtual Desktop re-encodes the stream, so its own resolution / bitrate settings are a separate limit that this project cannot see.
- Built (no behaviour change by default):
  - Native `Host::RecordTiming`: one log line per 120 submitted frames in `host.json.native.log`: `timing frames= window= cycleMs= waitMs= prepToSubmitMs= submitMs= displayPeriodMs=`. Measurement only, no ABI change (EyePose 44 / HostStatus 136 unchanged), cannot fail a frame.
  - `Anisotropic` config (default OFF) = `QualitySettings.anisotropicFiltering=ForceEnable`, limit 16. `-Anisotropic` in `headset-handaim.ps1`. A `[OPENXR_HOST] QUALITY aniso= msaa= lodBias= vSync= eye= eyeMsaa=` line is logged every run so the game's own quality values are known.
  - `headset-handaim.ps1` writes `timingWindows`, `timingMedianCycleMs`, `timingMedianFps`, `timingLast` into `run.json`.
- Deliberately NOT done: eye MSAA. Unity's docs say a multisampled RenderTexture is resolved into a regular surface for the rest of the system, but they do not say which texture `GetNativeTexturePtr` returns on D3D11 for it, and the native copy refuses sample counts other than 1. It needs its own evidence first. No resolution increase either (see above). No 90 / 120 Hz display request (still unknown for Virtual Desktop).
- Tests: 123 hand-aim fixtures (was 120; 3 source guards), native fault fixtures 41 (was 40; timing window resets and never fails). Native build `/W4 /WX`. `artifacts/verify-d104`: VERIFIED, 28 EXIT lines, 0 mismatches, stderr empty.

### D-104 result — fp1 measured (2026-10-10, headset sitting, NOT a gate)
- Run `artifacts/headset-d087/fp1` (`-RenderFrames 1 -DisableVSync`, eye 2064x2208): 122 timing windows, median cycle 22.53 ms = 44.4 fps. Averages: prepToSubmit 8.4 ms, submit 7.9 ms, xrWaitFrame 2.5 ms. `displayPeriodMs` alternates between 11.11 (90 Hz) and 22.22 (45 Hz); 86 of 118 windows in the first 113 read 22.22.
- User report: no stutter; picture looked jagged. Virtual Desktop was set by the user to Ultra (2688x2880, 90 fps, 500 Mbps, sharpening 38 %, SSW always enabled), but the user found it on Medium (2112x2304) during the sitting. This project never writes Virtual Desktop settings: no code path does, and the PC-side files (`%APPDATA%\Virtual Desktop\Settings.json` 2025-12-01, `GameSettings.json` 2026-09-06) were not modified today. Where the preset is stored (headset app or streamer) and why it changed is NOT known.
- INFERENCE (not proven): the 22.22 ms period matches Virtual Desktop SSW "always enabled" halving a 90 Hz target to 45 fps application frames, with the rest synthesised. The earlier "120 Hz / 3" guess is withdrawn. Our own cycle is also about 20 ms (prepToSubmit + submit are serial, about 16 ms), so even without SSW the app would not reach 11.1 ms for 90 fps.
- `QUALITY` line: the game already runs `aniso=ForceEnable`, so `-Anisotropic` has nothing to add; fp2 as written is dropped. `msaa=0`, `lodBias=1.5`.
- Sharpness: the eye target (2064x2208) is smaller than the Ultra stream (2688x2880), so Virtual Desktop upscales ~1.3x; that is a likely cause of the jaggedness. Not tested: a 2688x2880 eye target.
- fp1 ended `HOST_FAILED`: the scene changed `Maze_Small -> MainMenu_2` and the host's "no menu fallback" ownership guard stopped it. Stock was restored (`stock=True`). The timing data above was already written.

### D-104 result 2 — fp3: eye 2688x2880, SSW disabled by the user (2026-10-10, headset sitting, NOT a gate)
- Run `artifacts/headset-d087/fp3` (`-RenderFrames 1 -DisableVSync -EyeWidth 2688 -EyeHeight 2880`), `QUALITY ... eye=2688x2880` confirmed in game.log. 80 timing windows. Final numbers over all windows, fp1 -> fp3: median cycle 22.53 -> 22.79 ms (44.4 -> 43.9 fps); p90 23.27 -> 25.63 ms; xrWaitFrame 2.45 -> 0.02 ms; prepToSubmit 8.54 -> 8.52 ms; submit 7.88 -> 10.68 ms; displayPeriodMs 22.22 x90 + 11.11 x32 -> 11.11 x80.
- WITHDRAWN: the D-104 inference that "SSW always enabled" halved the application rate. With SSW off the display period is a constant 11.11 ms and the cycle is still about 22.8 ms. SSW only explains why the runtime period had been 22.22 ms in fp1.
- Measured: `waitMs` is about 0, so xrWaitFrame never throttles us; we arrive late every frame. prepToSubmit + submit is about 19.2 ms and the cycle is about 22.8 ms, so roughly 3.6 ms per frame (means vs a median, so approximate) is spent outside both measured spans. The cycle is about two display periods (2 x 11.11 = 22.2 ms), which fits a pipeline that misses every 90 Hz slot.
- Measured: raising the eye target by 1.45x in pixels (2688x2880 vs 2064x2208) raised submitMs by 2.8 ms (+35 %) and left the cycle unchanged. So the resolution is not what sets the 22 ms; the serial structure is.
- INFERENCE, not proven: submitMs of 8 to 11 ms is far above what a raw GPU copy of two 2688x2880 RGBA surfaces should cost, so it probably includes waiting for Unity's queued GPU work to finish before our copy completes (`gpuCompleted`). It scales with pixels, which fits GPU render time. The copy itself and the fence wait are not timed separately yet.
- Not done: no pipelining / overlap change. The next step needs one more measurement (split submit into copy issue vs fence wait) before any structural change.
- fp3 ended `HOST_FAILED` the same way as fp1 (`Maze_Small -> MainMenu_2`, the stage ended while both hands were `WaitingForWeapons` for about 80 s). Stock restored (`stock=True`).


### D-105 — level-up UI in front of the HMD, backdrop hidden, ray geometry, shots aimed at the muzzle (2026-10-10, built, NOT headset tested)
- User report (fp3 sitting, two screenshots 21:03 / 21:04): level-up UI appears to the upper right instead of in front of the view; reward cards turn around the vertical axis and become unreadable; the black backdrop is still there; some shots go to a screen-fixed point; the UI ray seems to stop after switching from keyboard to Xbox controller.
- Honest status of the earlier "UI in front of the HMD" agreement: I found NO such decision in `docs`. D-096 made menus follow the HMD yaw with a 35-degree dead zone; D-102 head-locked only the in-game HUD canvases and explicitly left Level Up on the old path. So it was never implemented for the level-up screen; this is a gap, not a regression of working code.
- Change 1 (placement): canvases under `Level Up` are placed rigidly on the head while the game is paused (position = head + head rotation * forward * 1.0 m, rotation = head rotation, 1.4 m wide). `UiCanvas.LevelUp`, `HudLayoutPolicy.IsLevelUp`.
- Change 2 (backdrop): a Graphic that is wide (>= 0.85 of the canvas), tall (>= 0.5), dark (max channel <= 0.35), alpha >= 0.02 and not Filled is now culled at its CanvasRenderer while paused (`UiBackdropPolicy.ShouldHide`) and restored on resume and in `UiRelease`. The D-096 alpha cap stays for the remaining dark graphics. Every wide candidate is written to `ui.backdropCandidates` (HIDDEN or kept) so a miss is fixable from data.
- Change 3 (UI ray): the old code took the NEAREST INFINITE PLANE among all converted canvases; the ray point could lie outside that canvas or on a canvas with nothing pressable, so the dot stuck on a side panel (screenshot 21:04, circled) and the cards were never hovered. fp3: hovers=10, clicks=0; fp1 (also Xbox): hovers=48, clicks=7. Now only canvases whose rect CONTAINS the hit point are candidates, and one with a Selectable under the point wins (`UiRayPolicy.Choose`).
- Keyboard -> Xbox claim: NOT supported by the data. fp1 and fp3 were both `XBOX_CONTROLLER` (`gamepad=True kbm=False`), fp1 clicked 7 times; the ray itself reads the headset controller pose and does not depend on the game input scheme. The geometry bug above explains the symptom. The only input-scheme coupling is the Submit pulse (pad South) used for card confirmation; it needs the pad, which was present in both runs.
- Change 4 (card rotation): CAUSE NOT PROVEN. Assembly scan shows `LookAtCamera` (billboard), `LevelUPAnimation`, `LevelUPHoverCard`, `RotateObject`, `FixedRotation`. While paused, every `LookAtCamera` under a Level Up canvas is disabled, and every child RectTransform whose world rotation differs from its canvas by more than 1 degree is forced back to the canvas rotation each frame (`UiFlattenScan/Apply/Restore`); all undone on resume. The receipt lists what was found (`ui.flattened`, `flattenFound`, `flattenBillboards`). Risk: a deliberate small tilt of a card is flattened too.
- Change 5 (aim): fp3 `[SHOT]` lines show `camFwd` nearly constant while `muzzleFwd` changed (about 23 degrees apart). `HandFireTrigger` aims the camera only around `PlayerController.fire`, but the game's own fire timer calls `shoot_forward` outside it, and most calls then use the camera at its resting pose. `ShotAim` now moves the game camera to the muzzle pose for the duration of every `shoot_forward` of a hand weapon and restores it in a Harmony FINALIZER (also when the game throws). Receipt: `shot.cameraAimedShots`, `lastCameraToMuzzleDeg`, `maxCameraToMuzzleDeg`. UNPROVEN: that the bullet direction is derived from the camera at that moment; this is an inference from the log, not a bullet-path measurement.
- Not the aim problem: in the last third of fp3 both hands were `WaitingForWeapons` with `fireCalls` constant (no shots at all). That is the `HOST_FAILED` stage-change case (Maze_Small -> MainMenu_2), still deferred.
- Tests: 136 hand-aim fixtures (was 123): policies (backdrop 3, ray 4, level-up path 1) and 5 source guards. `verify.ps1` expects RESULT 136. Plugin build: 0 warnings, 0 errors.

### D-106 - translucent panel behind the in-game HUD (2026-10-10, built, NOT headset tested)
- User correction: the black/translucent background they meant is the panel behind the HUD (health, level, 0/3 counter) during PLAY, not the level-up backdrop. D-105 worked on the level-up backdrop and I had not checked this one. The screenshot still shows it (grey-blue haze under health / level / counter).
- Evidence: fp3 receipt has ui.hudLocked=5 but ui.hudDarkHidden=0 and an EMPTY ui.hudDarkCandidates. D-102 only considered graphics whose tint is dark (max channel <= 0.35); none qualified, so the panel's tint is not dark (most likely a light tint on a translucent sprite). The D-102 colour test was the wrong filter.
- Change: HudTranslucentPanelPolicy.ShouldHide(alpha, widthFraction, heightFraction, filled) is colour-agnostic: non-Filled, alpha 0.02..0.95, width >= 0.15 of the canvas and area >= 2 percent. It is OR-ed with the old dark rule on head-locked HUD canvases. Candidates now include every Image/RawImage with alpha >= 0.02, with sprite name (art=), first 80, in ui.hudDarkCandidates.
- Risk: a legitimate translucent wide image (for example a bar background) is hidden too. Not proven which object is the panel; read ui.hudDarkCandidates after the next sitting and tighten.
- Tests: 140 hand-aim fixtures (was 136). Plugin build 0 warnings, 0 errors. Full verify.ps1 not re-run for D-106.


### D-107 - level-up screen centred by its content, not by the canvas pivot (2026-10-10, built, NOT headset tested)
- User report: the level-up UI sits above the head, sometimes upper left, sometimes upper right; with the gaze straight ahead the relic and weapon panels are clipped at the top and cut on the right. The user could not see whether the cards rotate because they were outside the view.
- Evidence (d106a receipt): `Level Up/NewNewCanvas` is placed rigidly (head + head rotation * 1.0 m, D-105), and `Choose_01` has `localEuler=(0,336,360)` (a 24 degree card tilt; 932 child rects found, 0 billboards). The placement code puts the canvas PIVOT in front of the eyes. ROOT CAUSE IS A HYPOTHESIS: the pivot / anchors / child layout of the game canvas are not centred on the visible content, so the content is displaced from the gaze. I have no measurement of the canvas bounds yet (the old receipt has none). Stale head pose is weakened as a cause (the HUD uses the same SetHead value and sits correctly); a game rewrite of the position is not supported by anything in the receipt.
- Change: while paused, every 0.5 s `UiMeasureLevelUp` computes the bounds of the visible Graphics (alpha >= 0.02, not culled, narrower than 0.85 of the canvas so the backdrop is ignored) in canvas-local units. The canvas is positioned so the CENTRE OF THE CONTENT, not the pivot, is 1.0 m straight ahead, and it is uniformly shrunk (never enlarged) so the content is at most 1.5 m x 0.9 m. `UiContentCenterPolicy.TryMeasure/FitScale`. Bad or non-finite bounds fall back to the old pivot placement.
- Receipt: `ui.levelPlacement` lists pivot, anchors, size, bounds, centre, fit and `angleToGazeBefore` (angle of the content centre from the gaze under the OLD placement), so the offset can be confirmed or refuted from data after the next sitting.
- Risk: a hover animation that grows a card moves the bounds, so the centre can drift slightly (re-measured every 0.5 s); the log is capped at 30 entries. The 24 degree card tilt is still flattened by D-105 (cause unconfirmed).
- Tests: 144 hand-aim fixtures (was 140). verify.ps1 expects RESULT 144. Plugin build 0 warnings, 0 errors. Full verify.ps1 not re-run.

### D-108 — all paused UI centred on HMD from the first frame; final render placement (2026-10-10)
- D-107 headset result: **CENTRING FAIL** (user: still upper-right, attached pause/relic screenshot). d107a recorded Level Up/NewNewCanvas pivot=(0,0), size=1920x1200, bounds 50..1905 / -336..1185, centre about (977,425), fit=0.81, repeat initial angleBefore=37.8 degrees, later values 0.8..7.2. These are intermediate transform samples, NOT proof of where the final rendered UI was seen. The pivot/content mismatch is now measured; it is NOT proven to be the sole remaining cause.
- Source-confirmed gaps: D-107 applied only to Level Up, leaving the pause/relic screen on pivot-only yaw-dead-zone placement. First Level Up measurement also followed placement and did not reapply until the next frame; invalid/no measurement used the pivot. Game late layout/animation overwrite is still a HYPOTHESIS.
- Chose: every paused converted canvas uses the complete HMD pose (position and rotation) with no yaw dead zone. Before bounds exist, use the RectTransform rectangle centre computed from its min/max (works for bottom-left, centred and top-right pivots), not the pivot. For all paused canvases, flatten level-up cards first, measure visible content and uniform shrink-to-fit, then immediately reapply before ray selection in the same tick.
- Chose: scoped Harmony postfixes at Camera.FireOnPreCull (runtime-eye cameras only) and Canvas.SendWillRenderCanvases (after UI layout), reapply placement using the same midpoint HMD pose for both eyes. No input/hover/click is repeated by these callbacks; existing rect-containment and pressable-canvas ray policy retained. Callbacks unpatched on release. APIs verified in installed interop shape, not live callback behavior.
- Receipt: ui.levelPlacement now samples every paused canvas and logs angleToGazeAfter after same-frame placement. ui.renderPlacement records passes, corrections, maxDriftBeforeMeters, maxErrorAfterMeters and error. Nonzero drift can establish a later rewrite; low after-error alone is NOT visual success.
- Explicit fallback: if render postfix installation fails, warn in game log and record UiRenderError; unpatch any partial install and retain LateUpdate placement. This is a degraded diagnostic route, NOT acceptance of correct final-render placement.
- Rejected: hard-coded left/down offset, changing the game's authored pivot/anchors, moving only the controller ray or treating previous click counts as visual proof. Keep parent scale compensation, original layout, HandUi opt-in and original scene ownership guard. No framework/runtime replacement, anti-cheat/DRM work, or gate expansion.
- Risk/limit: centring every paused canvas can overlap separate panels; hover animation can change bounds. Readability and new ray interaction need PLAYTEST-UI-D108-KO.md. UI correction does not implement menu-start VR or scene-transition support.
- d107a ended HOST_FAILED on player/source destruction while Maze_Small -> MainMenu_2, the existing ownership stop; run.json stockVerified=true, UninstallExit=0, runtime unchanged. Not a proven native crash.
- Verification: 156 HandAim math/policy/source fixtures PASS (12 added); full verify.ps1 28/28 PASS, explicit exit 0 in artifacts/ui-d108-verify.exit, final VERIFIED line; Release plugin 0 warnings/errors. Initial fixture attempt rejected an old source-string guard after the predicate refactor; updated guard still checks the same rigid-HUD requirement and rerun passed. No live callback or headset PASS claimed.


### D-108 launch retry — 2026-10-10
- d108a never launched the game: 58 headset probes over 300 s ended ErrorFormFactorUnavailable / NO_HEADSET_ATTACHED, observation=NOT_RUN. Install/uninstall exit 0, stockVerified=true, cleanupError=null, runtime unchanged; no UI acceptance or new gameplay bug.
- User says "시작". Retry with fresh tag d108b, -DisableVSync, unchanged verified D-108 plugin SHA256 BF7BFC3395EDC8FEE475AFFDD84CEF6947581D3A7329EE709D8AFFB45212BADB. No skipped headset checks, runtime switch, game/source changes or new gate acceptance. Same PLAYTEST-UI-D108-KO.md.

### D-109 — User accepts D-108 UI; diagnose rightward bullet/flame attacks without a blind axis fix (2026-10-10)
- User: UI appears in front of eyes, level-up choices work, cards do not flip; considers this issue resolved. Close TOOL-062 on this user acceptance, not automatic correction metrics. Full Stage 8/headset matrix/M4b remains unpassed.
- d108b GAMEPLAY LOCKED=14:48:42.3906332Z; ended on existing source/player ownership loss Maze_Small -> MainMenu_2, not an established native crash. PID 44576/game no longer running; no duplicate launch or needless stop.request. Install/uninstall exit 0, cleanupError=null, stockVerified=true, stock.log exact 121 files/8 directories, runtime unchanged. Prior run.json verdict preserved verbatim rather than rewriting its automatic result.
- Live UI: render passes=37278/corrections=24446/maxDriftBefore=1.395636 m/maxErrorAfter=1.0789593e-5 m/error=null; 7 clicks/126 hovers. Aggregate callback firing/drift confirmed, exact writer and per-callback counts unknown.
- User's next target: bullets to the right; flame model follows reticle but attack range to the right, roughly 90 degrees. Analysis-only request, no production change. Same D-108 DLL hash BF7BFC3395EDC8FEE475AFFDD84CEF6947581D3A7329EE709D8AFFB45212BADB verified.
- Confirmed defect: HandAimDriver chooses a source-camera child container; HandFireTrigger and HandShot.PrefixShoot aim that parent camera at its own child muzzle. Parent change re-applies the child's relative offset, can be repeated on nested scopes, and changes the shot origin too. d108b post-write vectors show about 59-60 degrees discrepancy despite the aim write. Camera restoration/next driver tick can conceal the transient error in visible model and periodic muzzle-to-hand receipts.
- Evidence limits: 19047 shoot calls/overrides/cameraAimedShots, zero shot error, 11691 fire calls do not prove correct direction. tracerPatched=false/tracers=0 due to known unsafe Vector3 detours. maxCameraToMuzzleDeg=159.22182 is BEFORE the parent write, not actual impact angle. Log lacks weapon ID/frame/nesting, so successive vector patterns are not proven nested calls. User's roughly 90 degrees is not a fixed calibration constant.
- Read-only installed interop recon exit 0: Flamethrower shoot(bool)/update(float), VFX ParticleSystem/Transform; Flamethrower_Projectile CapsuleCollider, burning_enemies, OnTriggerEnter/Exit; Flames/Flames_Projectile_V2 trigger paths. Separate candidate attack/VFX paths, native behavior/axes/live poses unconfirmed. Reports artifacts/shot-d109-analysis/{shape,flame-shape}/assembly-recon.json.
- Chose next direction, NOT implemented: independent per-hand/per-weapon world aim snapshot/proxy; preserve affected child-container world poses if scoped camera fallback remains, with nested/exception restoration; separately map actual flame collider/VFX/update. Reject global -90-degree offset, reticle-only correction, unsafe tracer reactivation, framework/runtime change, anti-cheat/DRM bypass or linked hands.
- Validation: existing HandAim 156/156 rerun PASS; new tests/ShotParentAnalysis 20/20 pure hierarchy/log controls PASS, exit 0 and empty stderr. Reproduces offset duplication, restoration hiding it, position drift and world-pose-preservation control; not Unity/game shot acceptance. Full prior 28/28 verifier retained, not rerun or relabelled as D-109 production validation. No new headset request/game launch. Details docs/SHOT-D109-ANALYSIS.md.

### D-110 — Independent shot frames and parent-safe scopes (2026-10-10, implementation in progress)
- User says "시작" / resume: authorizes implementing D-109 correction and reversible test launch; existing soft Stage 6B / hard Stages 0-4 gates unchanged. Retain accepted D-108 UI code byte-for-byte.
- Reuse existing BepInEx/Harmony/Unity/OpenXR stack; no new framework/library/runtime switch. Publish detached per-weapon proxy at each driven hand's controller rotation and that weapon's muzzle world origin after BOTH hands update. Prefix never derives aim by re-reading a child after parent rotation.
- Shared WorldPoseScope captures camera plus every active camera-child weapon root before mutation, preserves root world poses during override, restores camera then independent roots in reverse order, attempts all restores on failures. Scope nesting follows using/Harmony invocation-local __state; no ownership recheck/pop of another invocation. No reparenting, no fixed -90 degrees, no shot-count/ammo changes.
- Retained explicit compatibility fallback: source-camera override still used inside fire/shoot because native camera-dependent paths remain unknown. Null/camera/exact muzzle arg replaced with detached proxy; distinct caller-provided aims preserved. Missing/unowned/stale shot frame leaves game call unchanged and increments missingFrames; trigger-driven fire instead releases when no fresh frame. This is NOT guaranteed full per-weapon coverage, remains a test-visible limitation.
- Flame diagnostics read reference objects and capsule endpoints/VFX pre/post/outside scope every 2 seconds, no Vector3-argument hooks and no unmeasured flame collider repositioning. Logs/pose alignment only, not damage trajectory or visible hook acceptance. Full flame-specific continuous-path fix waits on evidence.
- Final verification: full verifier 29/29 PASS/exit0/empty stderr; Release build 0 warnings/errors; HandAim 156/156, new production scope fake-node/source 23/23, prior hierarchy/log math 20/20 PASS. Saved artifacts/shot-d110-verification.json and shot-d110-verify.{out,err,exit}. Source diff confirms HandUi.cs and config/native unchanged. Review briefly found an agent-induced empty OpenXrHost.cs while restoring original newline encoding from git; recovered exact HEAD content plus ONE intended Install argument change before successful final verifier. No empty source built/installed/launched.
- New tests verify capture-before-write, independent hands/proxy, nested restoration to outer scope, exception/failure rollback, reverse independent-root restore, idempotence and restoration attempts after a failing node. No Unity hierarchy/game execution in these fake-node tests; not a substitute for the headset script. Existing HandAim source guard updated to reject the proven defective camera-only stack, count unchanged.
- No user-reported shot bug closed by automatic tests. Read-only d110-preflight exit0: DRY_RUN_OK/observation=DRY_RUN; install plan files=232/stock121, no game mutation/launch. Headset currently absent (ErrorFormFactorUnavailable), runtime still VDXR. Fresh actual tag d110a started (PID129056, local00:24:25, install running; logs artifacts/headset-d087/_logs/d110a.{out,err}.log) to install verified plugin SHA256 81DF2EECEDD3058251B32500383B51C73E687388D2B54498A17A4000872E1251 then wait 300s for headset, -DisableVSync, default 600s sitting; no skipped headset checks, unsafe tracer, runtime switch or acceptance. Script PLAYTEST-SHOT-D110-KO.md. Keep D-108 UI acceptance and hard gate state.

### D-110 continuation — fresh drill after headset connection (2026-10-11)
- Resume recheck: d110a PID129056 ended at 00:33:41 local before the user's "연결완료". Receipt NO_HEADSET_ATTACHED / NOT_RUN, 58 probes, never launched game, gates NONE. Install/uninstall exit0, cleanupError=null, stockVerified=true; stock121/8 hashes/times exact; VDXR unchanged. No remaining game/harness. Preserve failed/NOT_RUN receipt, no shot acceptance.
- Rechecked implementation commit73c11fd and drill-status commitf38e108; clean working tree. Saved full verifier29/29 Passed=true/exit0 and plugin/native SHA256 identical to D-110. No code/config/UI/native rebuild or modification required.
- Start fresh d110b only after old run's cleanup/process absence confirmed: PID74544, local00:37:41, installation running. Default flags preserved: HandAim/HandFire/HandShot/UI/Input/Haptic, unsafe tracer OFF, -DisableVSync, 300s headset wait, 600s sitting. No skipped probe, OS/runtime/save/anti-cheat change. Monitor -> monitor stage start -> GAMEPLAY LOCKED/readiness -> existing Korean shot/flame/UI script -> cleanup and measured evidence. Gates remain open.
- Agent guard false-negative before launch: @(...ConvertFrom-Json) nested array in Windows PowerShell made count1; direct assignment confirmed actual29/allPassed. Corrected command, no script/test changes; first attempt created no drill or game writes.
- d110b launch update 00:39:45 local: install exit0 at00:39:31; probe wait1 PASS at00:39:32 (VirtualDesktopXR/Quest3, 2 views, adapter matches, submitted1 valid layer, zero frame failures, orderly probe exit). Steam game PID60588 running00:39:34; harness74544 locked=False, waiting for monitor stage start. User instructed Play -> stage -> start; headset test only after GAMEPLAY LOCKED/readiness. Probe PASS is not game stereo/controller/shot acceptance; TOOL-044/063/M4b remain OPEN.
- Live update00:41:08: GAMEPLAY LOCKED00:40:17, Maze_Small/Main Camera; both hands Driving/controllers=true/nativeFailures0/fireFailed=false, native paired GPU submissions advancing. At00:40:39 scope calls356/depth0/maxDepth1/failures0/missingFrames0, but alignmentSamples0/flameSamples0 and attackDirectionVerified=false, shootCalls0: current Crucibow path not proved to consume shot proxy. No consolidated runtime/controller-family/flags/first10s assertions smoke line exists in this run, so do not report a full smoke PASS; logged runtime/actions/paired submissions are partial readiness only. Current session left running for user observations; no new stage/implementation or acceptance.


### D-111 — StockFireCadence and AutonomousHeadAim bug candidates (2026-10-11, Stage 6B)
Implementation commit: a11639b. Final source review/49+156+23 focused re-run and Release0/0 after preserving original OpenXrHost line endings; current binary hash recorded below. No active drill/game; next action is the written combined headset test, not a claimed hook/bug PASS.
- User accepts bullet direction, reports robot/passive mounts not following view and automatic guns firing/draining ammo/damaging excessively; two annotated screenshots supplied. Retain D-108 UI and D-110 detached independent shot frames. TOOL-050/064 open; TOOL-063 flame and M4b remain OPEN. Weapon names/complete hand matrix unspecified.
- d110b completed00:50:12 local; HOST_FAILED known Maze_Small -> MainMenu_2 ownership loss, not a new tracer crash. Install/uninstall exit0, stock121/8 verified, cleanupError=null, VDXR unchanged and PIDs absent. No duplicate drill. Final shot25080/overrides25079, scope64181/maxDepth2/failures0/missingFrames1, flameSamples0. Aggregate ammo mixes weapons and is not a stock RPM/damage baseline.
- Research uses pinned stock binary read-only through existing LibCpp2IL/Iced libraries (already used by loader). PlayerController.fire RVA0x51A6C0 gates on *press and consumes it; Weapon.start_shooting_parent RVA0x490840 resets its sustained fire_t on a successful start; Weapon.update RVA0x491290 owns sustained timer/catch-up, including multi-shot frames. Evidence artifacts/weapon-d111-analysis/native-disasm.log; static native finding, not confirmed visible hook.
- Chose StockFireCadence=true: call fire only on trigger press edge; keep release edge and teardown release. Scoped projection of firePressing/fire2Pressing during ONLY the owned hand Weapon.update preserves held-dependent native decisions without global Fire input events. Invocation-local finalizer restores native input on nested calls/exceptions. Existing per-hand shot scope also surrounds sustained update, preserving both controller origins/directions including non-shoot_forward projectile paths. No extra update call or fixed fire-rate/ammo/damage adjustment. Flag false explicitly restores prior repeated-start behavior for diagnosis, not a silent fallback; failure to install hooks aborts setup.
- Chose AutonomousHeadAim=true: deliver mean stereo HMD pose to original gameplay camera before/after PlayerController.Update and before hand drive. Preserve controller-owned camera-child roots in world space; top slots/PASSIVE inherit head pose and retain native local animations, offsets and auto-target selection. Do not force homing/360-degree passive attacks forward, alter detached turrets/projectiles, rotate colliders, apply -90 degrees, change scene-ownership policy/runtime or modify HandUi. Source local pose and patches restored on cleanup. Flag false retains old flat source pose for diagnosis.
- Validation: initial full verifier30/30 PASS, WeaponRuntime49/49, HandAim156/156, shot scopes23/23; final post-review verifier30/30 PASS (all child exits0, completion marker, empty stderr; artifacts/weapon-d111-final-verification.json). Release0 warnings/errors; final plugin10F11AA69A489A9A4F8C955BB2BB6A307BF6C6B59982BB43ED203DA4C140E412 (after source line-ending-only cleanup, focused49/156/23 checks and Release re-run; no behavior changed). Both bug candidates share one native invocation/lifecycle bridge, committed together to avoid an intermediate edge-only/no-held-input build; neither closed without headset acceptance. HandUi SHA256 C3DB12998AF3E3547F42344AABF9214A8BCC7A09A1C747684D2E0E7C6D3ABACD unchanged. Verifier runs NativeHost fixtures/build, so native DLL was rebuilt (current hash0E8119FD59FE298B21DA1BDFEFDB69A2ED2BA28CB9362B7ED010780FA03F8F40) although no native source changed; do not claim d110b native binary unchanged. No new game install/launch or visible-hook proof this session.
- Costs/limitations: new reference-only native update hooks require visible execution confirmation; native virtual/specialized weapon paths and non-camera autonomous mounts may need measured coverage. Per-hand held timing comes from prior completed hand tick (bounded frame latency), not altered RPM. In-VR settings remain unimplemented; flags exposed in host JSON/schema and harness NoStockFireCadence/NoAutonomousHeadAim switches. Full smoke family/assertion line still absent, so no formal Stage/M4b PASS. PLAYTEST-WEAPONS-D111-KO.md covers weapon name, 2-second burst, release/restart, head-turn mounts/attacks, independent hands, bullet/UI regression and optional flame; unavailable classes NOT TESTED.

### D-111 sitting d111a — user start (2026-10-11)
User says "시작해". Pre-launch recheck01:19:08 local: clean HEAD0e96ecc/implementationa11639b, saved verifier30/30, plugin10F11AA69A489A9A4F8C955BB2BB6A307BF6C6B59982BB43ED203DA4C140E412, native0E8119FD59FE298B21DA1BDFEFDB69A2ED2BA28CB9362B7ED010780FA03F8F40, no existing game or headset harness, fresh tag. d111a PID118356 started01:19:29 local; preflight then transactional install. Default intended flags include D-111 StockFireCadence/AutonomousHeadAim ON, prior UI/input/hand features unchanged, tracer OFF, -DisableVSync; headset wait300s/session600s. Monitor this process only; no duplicate. No source/binary rebuild or runtime change. Probe/game/visible behavior pending. Missing full family/assertion smoke still blocks formal gates; no scope/count-only acceptance. Existing D-086 soft-gate sitting is bounded bug investigation, not Stage/M4b PASS.
- Launch update01:21:54 local: install journal Installed/exit0 at01:21:21; probe wait1 PASS/headset attached, Steam game PID72516 running from01:21:24. locked=False at01:21:45, no hand receipt yet. User directed monitor Play -> stage -> start; no weapon or full smoke acceptance.
- Live update01:22:41: GAMEPLAY LOCKED01:22:02 Maze_Small. RightDriving17presses/17starts/held2.43s, native weaponUpdates2922/held618, gazeWrites17975/runtimeError=null, nativeFailures0/fireFailed=false, scope635/depth0/failures0/missing0. Leftweapon present but RejectedImplausibleRest; do not claim both hand readiness or force a left test. Flame/alignmentSamples0. No new code/diagnostic hooks mid-sitting; observe user results and preserve evidence. Counts prove execution counters only, not corrected RPM/pose/damage/full smoke.


### D-112 — measured HUD shadow, lower UI and authored left rest (2026-10-11)
User defers automatic weapons (NOT TESTED); reports returned frontal translucent black-bottom rectangle, left hand unusable until further acquisition, HUD/minimap too high. Preserve D-110 bullet direction and D-108 card alignment/clicks; reopen Stage8 background/readability TOOL-066/067. TOOL-065 user-confirmed; TOOL-064 gaze result unspecified. Reuse pinned Unity/BepInEx/Harmony/OpenXR; no new dependencies/runtime/DRM/anti-cheat work.
Evidence: d111a hudDarkCandidates Player HUD/Bottom Shadow, art HUD Bottom Shadow, white tint alpha1, full canvas, kept. Filters inspect tint, not sprite texture alpha. Choose exact hierarchy+art identity with reversible alpha0 and pre-eye/layout reapplication; retain existing heuristics and gauges/minimap/status. HideHudBackdrop defaultON; false retains prior diagnostic behavior. Do not broaden opaque-art filtering.
Choose UiVerticalOffset default -0.10m, range -0.30..+0.10; 0 reproduces D-108 height. Head-local offset shared by tick/pre-eye/layout target and existing ray geometry. HUD/minimap and paused panels lower together; distances/width/content-center/fit/card flattening unchanged. In-VR settings/layout absent; expose host config/schema/harness, limitation explicit. Human readability may need tuning.
TOOL-065 source hypothesis: gaze preserves newly acquired unwritten roots in world space, changing authored local rest; update rejects live controller output as rest. Choose preserve-world only already-written independent roots; valid authored rest captured once, invalid first frame retried, original10-unit guard on captured rest. Unity object equality replaces wrapper ReferenceEquals. Add finite-safe captured/local/world positions. No manual weapon updates/cadence/ammo/damage/shot changes. d111a lacks captured poses: root cause not headset certified.
Recovery: d111a HOST_FAILED01:25:19 known source/player/scene loss; original harness absent before final run.json/journal completion, initial stock failed. Resume original uninstall journal detached recovery70404; preserve incomplete run, separate recovery evidence, no forged original PASS/new launch. First foreground recovery hit tool timeout, safely resumed. TOOL-068 orchestration remains open even if stock recovers.
Verification pending: focused policy/source/fake-parent, full verifier, warning-free Release, stock/runtime/process checks. PLAYTEST-UI-LEFT-D112-KO.md under15 items. No automatic launch/acceptance from counters; fixes are candidates until user confirms. Initial focused HandAim run exposed two brittle historical exact-string guards affected by new shared target and inserted alpha restoration; updated guards preserve rigid-head and cleanup assertions, not removed. WeaponRuntime53/53 and Release0/0 already passed; HandAim/full final results pending.
- Recovery result01:36:47 local: original journal Recovered/sequence27; separate recovery.json RECOVERED_STOCK/uninstall0/stock0,121 files/8 directories/7,726,223,158 bytes/hashes/times exact, VDXR unchanged and game absent. Original run.json stays missing; TOOL-068 remains OPEN. Final review also saves/restores original CanvasRenderer.cull for the exact shadow, not always false. Focused HandAim178/178 PASS after two historical guard updates. First full attempt exited1 at old guard before update, preserved ui-d112-verify-r1 logs/receipt; fresh full rerun88700 active. No duplicate game/drill and no blind closure.

- Final validation01:39:44 local: fresh full verifier exit0/VERIFIED/all30 exact check identities Passed=true/empty stderr, artifacts/ui-d112-verification.json and ui-d112-verify.{out,err,exit}; HandAim178/178, WeaponRuntime53/53, ShotScopes23/23, Release0 warnings/errors. Actual installer AST numeric-range10 cases PASS, both edited PowerShell scripts parse and schema JSON/default -0.10 verified. Final source review includes original shadow cull restoration. Plugin745C209786DF65476DB13812D95AC35D4A01266751E3CEA7A164EA9C54B08B0C; nativeA99A7379F8368D6B67A83B4E60E5512C320413A9764C44CC2474D4554C60790E (verifier rebuilt native, source unchanged). HandShot/HandShotFrames/HandFireTrigger and native source unchanged. No new game/headset session/visible acceptance or Stage/M4b pass.

### D-112 sitting d112a — user start (2026-10-11)
User says 시작. Pre-launch01:43:36: clean HEADd14c97a/implementation8db1917, saved full verifier30/30/exit0, plugin745C209786DF65476DB13812D95AC35D4A01266751E3CEA7A164EA9C54B08B0C/nativeA99A7379F8368D6B67A83B4E60E5512C320413A9764C44CC2474D4554C60790E exact, prior stock121/8/hash/time and recovery verified, no other game/drill. Fresh d112a harness52100 started01:44:00 local, installing original journal transaction. Actual config confirmed UiVerticalOffset=-0.10/HideHudBackdropON/DisableVSync, existing hand/fire/shot/UI/input/haptic/default D-111 policies ON, tracerOFF; headset wait300s/session600s. No rebuild/runtime change, no skipped headset check; cadence remains NOT TESTED/deferred. Monitor install/probe/game/monitor stage start/readiness and PLAYTEST-UI-LEFT-D112-KO.md, then cleanup/stock/VDXR. Original TOOL-068 orchestration remains open; ensure final receipt, never launch duplicate or silently accept counts/full smoke. At01:44:54 transaction Ready, installer still running, no game/probe/visible response yet.

- Launch update01:46:16 local: install completed/exit0 at01:45:57, headset attached/wait1 probe completed01:45:58, Steam game PID115572 running01:46:00. progress WAITING_FOR_GAMEPLAY, no hand/gameplay receipt yet. User instructed monitor Play -> stage -> start; GAMEPLAY LOCKED/visible UI/first-left test pending, no smoke/Stage/M4b PASS. Original harness52100 active, no duplicate or mid-sitting code changes.
- Live update01:47:10 local: GAMEPLAY LOCKED01:46:38 Maze_Small; both handsDriving/controllersTrue/nativeFailures0/fireFailedFalse. Left1weapon/restCapturedTrue/hasWrittenTrue/fireCalls3; previous rejection absent in current sample, not a human acceptance. ui HideHudBackdropTrue/verticalOffset-0.10/persistentBackdropCount1/errornull. Current logs show reference/update execution and queued suppression only; actual shadow absence/readability/card behavior/bullet preservation awaits user. No new code/binary change or gate/bug closure; preserve d112a52100/game115572 and cleanup receipts.

### D-113 — Further15cm HUD lowering; scope native player held flags with owned weapon input (2026-10-11, Stages6B/8)
User confirms shadow absent, cards and general bullets normal, most works; requests HUD15cm further down and reports intermittent left-origin shots during right fire. Interpret delta explicitly: -0.10 -> -0.25m; retain shared UI placement/rays/distances/sizes and exact suppression. First-left timing/name unspecified, do not close TOOL-065 from broad positive report.
d112a native log explicitly says headset session budget reached; orderly close at01:56:37, configured600s, not crash/ad-hoc decision. Harness then closes game and restores transaction. Final run01:59:49 uninstall0/stockVerifiedTrue/cleanupErrorNull/VDXR unchanged, stock121/8/7,726,223,158 bytes/hash/time exact, original52100/115572 absent. Keep automatic NOT_VERIFIED verdict and original d111a TOOL-068 OPEN; no duplicate launch. Next duration must be explained before start; do not silently remove watchdog.
Static Weapon.update RVA491290 disassembly lines14521/14550/14576 reads native PlayerController held primary/secondary flags in reload/continued shot branches. D-111 only projects PlayerInputController firePressing/fire2Pressing. Choose invocation-local projection of BOTH layers for the owned weapon's held state, restore input/player flags even if nested or original throws. Candidate root-cause contribution only; sparse shot/timeline cannot prove exact reported incident. Add hand-held actual-shot diagnostic counters/log fields for future correlation. Reject blanket denial of unheld shots (native burst tails may be intentional), manual update/cadence/ammo/damage changes, new camera math, unsafe tracers, linked hands and runtime/ownership changes.
Validation pending before new headset script; existing LibCpp2IL/Iced analysis reused read-only, no new framework/library or anti-cheat/DRM work. Logged SMG/Minigun classes are not user acceptance of automatic rate; keep deferred cadence semantics explicit.
- Focused validation: HandAim180/180, WeaponRuntime67/67, ShotScopes23/23 PASS; Release0 warnings/errors. Full verifier running detached51932 (no game). PowerShell parse/schema default/shared target/source preservation checks PASS; unchanged HandUi, HandShotFrames and native Host.cpp confirmed via diff. New PLAYTEST-UI-FIRE-D113-KO.md8 steps, explicit600s automatic close. No runtime registration/game/settings/save writes or new headset request. Minor inspection errors (rg unavailable, guessed HandFire.cs/check-stock/native paths) corrected to actual files; no source acceptance claims from those failed reads.
- Final02:09 local: full verifier30/30/exit0/empty stderr; focused180/67/23, installer AST10/10, schema/scripts parse/diff check PASS; Release0/0. Plugin1D1FF49D2C96B27CBF99BA6B305EDB1981D9CC208CDAEB70822AC44659E05915; native rebuilt/source unchanged E6D4FD39276F86BBB701B680EF57132F3E62406364DC52FF5BFD4B9E5A849720. Independent read-only stock recheck121/8/7,726,223,158bytes/hashes/times exact; runtime VDXR unchanged, original sitting and verify/stock processes absent. No new game/install or specific headset acceptance; TOOL-069/067/065/gates remain OPEN.

### D-114 — Right stick UP blood, DOWN native special ability; right SMG report (2026-10-11)
User identifies the right-hand weapon at TOOL-069 as Submachine Gun. This answers only the right weapon; no inference about left weapon, trigger release, reload timing, native burst tail or cadence acceptance.
User requests blood ability on right stick UP, dash-like special ability on right stick DOWN. Current production D-100 uses the opposite pair and D-101 already moved Interact to left X. Choose to swap only the two directions in a shared executable policy; preserve pad North=Bloody Rush/East=Special Ability and native fallback APIs, thresholds/hysteresis/diagonal guard, left X Interact, snap, reload/fire/aim/cards/shadow/height and600s session budget.
Historical bindings.json D-057/D-059 design is not loaded by production. Add an explicit runtimeOverride with D-114 directions/thresholds/targets and disclosure, validated against production; do not silently rewrite all historical reload/action-family promises or claim a rebinding loader exists. New combined script adds ability checks within the same announced10min sitting, never auto-launch for this request. Validation complete: full verifier30/30/exit0/empty stderr; HandAim195/195 incl15 new executable direction/config fixtures, WeaponRuntime67/67, ShotScopes23/23, bindings/schema/PS parse/diff PASS; Release0 warnings/errors. Artifacts/input-d114-verification.json and input-d114-{verify,handaim,build,bindings} receipts saved. Plugin SHA25606DBEF507E206B088A06ACF89EC3BB23931C3B1D208BDE702A478A5A886BD153. Full verifier's read-only entry preflight rechecks pinned stock121 files/8 directories/hashes/write times; VDXR unchanged, no game or sitting harness. Native shot/UI/schema/harness sources unchanged. No install/launch; headset/gates remain OPEN.
Resume evidence directly re-read: D-113 saved30/30/exit0/empty stderr, focused180/67/23, stock-d113 receipt and d112a run.json uninstall0/stockVerifiedTrue/cleanupErrorNull/runtime unchanged/NOT_VERIFIED. Git clean at resume and prior commits confirmed4dc885e(HUD),0fe5341(held-state),8f1d455(docs). One bounded verifier-monitor command exceeded tool30s timeout; it did not stop/restart the verifier or game. Exact verifier129764 later completed exit0; no duplicate check run.

### D-115 — Finish installation before headset wear; retain exit-time restoration (2026-10-11)
User says 시작 and explicitly requests future coding completion include installation because headset-on waiting is inconvenient. Chose validated reversible installation as part of completion, without a second permission question. Game launch remains explicitly requested; keep the600s gameplay cutoff, transactional teardown, exact stock hashes/write times, unchanged VDXR and no save/Steam/anti-cheat/DRM changes. An unattended prepared installation must retain its transaction workspace and be reused by the later sitting, not blindly installed over; cleanup failure blocks further launches. This is an operating preference, not a new install-only/resume implementation claim.
Current sitting reuses unchanged tests/headset-handaim.ps1 install-first flow, fresh d114a,600s,DisableVSync,UiVerticalOffset-0.25m, all previous default hand features and tracer OFF. Saved D-114 verification rechecked30/30/exit0/empty stderr; plugin06DBEF507E206B088A06ACF89EC3BB23931C3B1D208BDE702A478A5A886BD153/native0ECAAA9CB299C13586CDA4D2E16F3E2D7109384217986A9352DCE5E0408F416D. Steam/VirtualDesktop running, VDXR registered, no prior game/harness and fresh tag. No rebuild/repeated full verifier. Do not request headset wear until monitor/gameplay readiness; missing formal family/assertion smoke remains an open gate, this is the existing bounded soft-gated diagnostic sitting, not Stage/M4b certification. No bug/ability acceptance from counters alone.
- Launch10:17:03 local: clean operating-preference commit9afa168, harness97568 started d114a. Preflight passed; install transaction began10:17:27 after exact pinned-stock check. Config600s/DisableVSync/-0.25m/default hand features/tracerOFF verified. Installation/probe/gameplay still pending at this entry; monitor this PID only, no duplicate/rebuild or mid-sitting binary changes.
- Launch update10:18:55: transaction Installed10:18:44, probe/headset attached10:18:45, Steam game38936 running10:18:48, harness97568 active. WAITING_FOR_GAMEPLAY; user instructed monitor Play -> stage -> start and wear headset only at readiness alert. Existing teardown is armed; do not claim final restoration/visible ability or independent firing acceptance before receipts/user report. No source/build/runtime changes during sitting.

### D-116 — Flame VFX/muzzle and crossed-hand user report; stock restored (2026-10-11)
User confirms the D-114 sitting completed. Reviewed the two user-supplied videos in `<Recordings>\편집후` (directory name differs from the initially stated `<Recordings>\편집후 폴더`): `탄약위치오류.mp4` and `화염방사기 이펙트와 위치.mp4`. Keep both videos as observational evidence only; neither proves an engine transform, actual owning hand, native shot origin nor complete trigger/reload context.

**TOOL-070 new/open:** user reports flamethrower range is expected, while the fire effect appears displaced from the muzzle and as discrete fireballs rather than a sustained stream. The short flame clip shows flame-like VFX at/near the target, rather than a persistent plume visibly attached to the barrel across firing; visual review cannot identify the emitter, weapon model socket, particle owner or whether hand proxy writes affect the effect. Preserve ballistic/projectile range, damage/cadence and native pooled-effect lifetime until those paths are traced. Do not respond with a guessed plume/particle hack. Separate VFX presentation defect from TOOL-069 shot ownership.

**TOOL-069 still open:** SMG right-hand context is user-confirmed; new clip is consistent with left-origin/intermittent shots but is edited/brief, so it does not prove whether the left trigger was down, which left weapon was equipped, whether the observed object is an actual projectile, or whether reload/burst-tail state explains it. The D-113 native held-flag mismatch remains a hypothesis/candidate. Keep both bugs open pending per-hand/weapon/shot correlation and source inspection; no code change in this triage.

**d114a closeout:** saved receipt has `observation=HOST_FAILED`, with `source-destroyed,player-destroyed,scene:Egypt_Map->MainMenu_2` in the host failure. Preserve `HEADSET_RUN_COMPLETE_NOT_VERIFIED` (no formal smoke/M4b or feature pass); this host-lifecycle failure does not invalidate the separate user-reported clip observations. Transaction uninstall exit0, cleanupError null, `stockVerified=true`; final pinned stock inventory121 files/8 directories/7,726,223,158 bytes and hashes/write times exact; VDXR registration unchanged. Recovery journal ends `Recovered`; original game38936/harness97568 absent. No runtime, source, save or config was altered in this sitting.

Next diagnostic plan: inspect game-specific weapon fire, per-hand shot transforms, flamethrower projectile vs VFX spawn/lifetime/attachment, and the D-113 invocation-scoped held projection. Use a written combined D-116 headset script only after source/fixture triage; ask left weapon and trigger/reload/timing context at that natural pause. No duplicate launch, no assumed root cause, no native burst suppression, and any future headset sitting retains the explicitly announced600s cutoff and transactional stock restoration.

### D-117 — D-116 read-only source and D-114 receipt triage; no gameplay patch (2026-10-11)
- Inspected the current `HandFireTrigger.cs`, `HandShot.cs`, `HandShotFrames.cs`, `WeaponRuntimeBridge.cs`, `HandShotDiagnostics.cs`, `HandAimDriver.cs`, `WeaponRuntime`/`ShotScopes`/`HandAim` fixture guards, pinned installed `Assembly-CSharp.dll` shape recon, and the saved `artifacts/headset-d087/d114a/game.log`. Current production code already selects each weapon by its native hand slot, maps each hand to only its own trigger/release slot, projects all four native/input held booleans only inside that owned `Weapon.update`, scopes that weapon's frame/aim locally, and restores poses/flags through nested and exception exits. Actual `shoot_forward` calls are counted, not filtered when the input latch is released, preserving native burst-tail behavior. This source inspection alone cannot establish what the game physically emitted.
- Static game shape confirms `Flamethrower : Weapon` has `shoot(bool)`, `update(float)`, `draw_shoot_vfx(Vector3, Vector3, float, int)`, `vfx_flames`/`vfx_flames_transform`, and projectile timing/state fields. `Flamethrower_Projectile` exposes an owning weapon and capsule collider; `Flames`/`Flames_Projectile_V2` have separate trigger/lifecycle-shaped APIs. This shape has stub bodies and no call-site/native implementation mapping. Do **not** confuse the native Vector3 VFX method with the previously unsafe generic `Weapon.draw_shoot_vfx` Vector3 detour; do not hook it on names alone.
- Saved d114a sample: 33 `[SHOT_SCOPE] phase=pre` entries = 24 Flamethrower LEFT_HAND, 5 `Machine_Gun` RIGHT_HAND, 4 `Machine_Gun` LEFT_HAND; not one of these lines labels `Submachine_Gun`. Every logged owned slot's mod-held latch is true, including some with both latches true. These calls are sparse and not tied to the event in the edited clip or a recorded headset trigger input; `Machine_Gun` is not substituted for the user's SMG identification. This neither proves nor disproves TOOL-069.
- Saved d114a flame sample: 168 `[FLAME_POSE]` lines; the exposed VFX transform is recorded under `Muzzle Flash Spawn` after initialization and follows that muzzle transform in sampled positions/directions. There are zero `[FLAME_COLLIDER]` lines in the selected 33 shot scopes. Collider enumeration only runs at these sampled call boundaries, so zero lines cannot prove absence. The log does not establish whether this is the target/scene fireball visible in the video, its ParticleSystem simulation space/lifetime, or whether another native effect object is responsible.
- Decision: no production code or runtime/config/game change. Existing invocation-scoped firing/aim transactions pass current fixtures, while the reports lack live-event correlation and VFX spawn/owner evidence; any firing rewrite, projectile-range change, unparenting, or plume replacement now would be speculative and could damage the explicitly preserved cadence/burst tails/native effect lifecycle. Keep TOOL-069/070 OPEN. Continue only with read-only tracing / a later, explicitly requested batched D-116 test; announce its existing 600-second cutoff and retain exit-time stock checks. Detailed counts are reproducible from the D-114 log; no D-114 verdict or footage claims have been upgraded.
- Validation: D-117 reran focused `HandAim` 195/195, `WeaponRuntime` 67/67, `ShotScopes` 23/23 and Release build (0 warnings/errors). This validates unchanged source/transaction fixtures only, not actual shots/VFX or user acceptance. Full integrated verifier rerun status is recorded in the D-117 addendum below.

#### D-117 verification addendum
- Full `tests/verify.ps1`: exit 0; all 30 records Passed=true, zero failed, required final VERIFIED marker present, stderr contained only PowerShell module-progress CLIXML. Focused identities HandAim195/195, WeaponRuntime67/67, ShotScopes23/23 present; current Release plugin build warning/error-free. Receipt: `artifacts/source-triage-d117-full.{stdout.log,stderr.log,exit}` and `artifacts/verification.json`.
- Verification was project-local/read-only for the game: target `BepInEx` absent before/after, no game/harness process running, and no install, launch, runtime, save or stock mutation. This full automatic regression PASS is not headset, feature or M4b acceptance. No tests interrogate the clip's specific flame/shot event.





















































### D-124 — install-first ("pre-installed") sitting path for the headset drill (2026-10-11)
- Problem the user raised: the drill always installs at the start (multi-minute full-tree hashing) and `Install-Stage0.ps1` refuses to install unless the game tree equals the pinned stock backup **exactly**, so a tree installed earlier can never be reused. Every sitting therefore made the user wait with the headset already on. Requirement: install first, then a headset-only start.
- Change (additive, in `tests/headset-handaim.ps1`; no new script, no second process): new `-UsePreinstalled` switch.
  - fresh-tag rule relaxed only for that switch: the tag directory must already hold `transaction\journal.jsonl` and `config.json`, otherwise it throws (`UsePreinstalled needs an installed transaction for this tag` / `… but this tag was never pre-installed`).
  - the install step is skipped, but two **fail-closed** checks replace it: (1) `BepInEx\plugins\VampireHuntersVR.Stage0.dll` and `BepInEx\plugins\VhvrNativeHost.dll` must hash-equal the current `src` builds, else `differs from this build; re-install instead of reusing`; (2) every config key the invocation wants must equal the pre-installed `config.json`, else the differing keys are listed. → no silent downgrade and no stale settings.
  - unchanged: the `finally` block already uninstalls and runs `Check-Stock.ps1` whenever `transaction\journal.jsonl` exists, so a pre-installed sitting still restores stock by itself.
  - `run.json` now records `preinstalled` and `installSkipped`; the `-DryRun` path is untouched.
- Consequence accepted: the game tree stays modified between phase 1 (pre-install) and phase 2 (the sitting). Mitigations recorded: do not launch the game by hand; undo at any time with `Install-Stage0.ps1 -Action Uninstall` (uses `artifacts\transactions\current.txt`); if Steam updates the game the pinned-build guard fails and the pacing is redone from stock. No DRM/anti-cheat interaction exists (single-player, offline).
- Verification of this change: `[Parser]::ParseFile` → PARSE OK; negative tests (existing tag without the switch → `fresh tag required`; new tag with the switch → `… was never pre-installed`, no directory created); HandAim source guards **195/195**; full `tests/verify.ps1` **exit 0**, 30 records, **0 failed**, all Exit==Expected, stderr **0 bytes**, `VERIFIED:` marker.
- Build identity for the sitting: the verified artifact is `src\core\bin\Release\net6.0\VampireHuntersVR.Stage0.dll` sha256 `9056036EEED738F2F63E8A88FB0F20FCE7764A48739F273E059F2028A044D098` (rebuilt 12:17; an immediate rebuild with no commit reproduces the same hash, so the build is deterministic) plus `src\render\NativeHost\bin\VhvrNativeHost.dll` sha256 `CC593A6994BA5CF0030BF3C7C620C463D06EFBC37B1D0C45739BEB3881719474`. The D-123 note's `8EE205C0…` was the 11:58 artifact of the same source; it is superseded and the cause of the difference is **not established** (a rebuild is bit-identical), so the installed-payload hash recorded by the sitting is authoritative.
- Two-phase runbook (also in `docs/playtests/PLAYTEST-FLAME-CROSSHAND-D116-KO.md`): phase 1 (no headset needed) `headset-handaim.ps1 -Tag d123a -DisableVSync -DryRun` → then `Install-Stage0.ps1 -Action Install -Workspace artifacts\headset-d087\d123a\transaction -OpenXrHostConfig artifacts\headset-d087\d123a\config.json`; phase 2 (user ready) `headset-handaim.ps1 -Tag d123a -DisableVSync -UsePreinstalled`.
- **Phase 1 executed (2026-10-11 12:19–12:21)**: `-DryRun` returned `DRY_RUN_OK` with `INSTALL_PLAN_OK: files=232; stock=121. Nothing written.` and `headset attached now: False (xrGetSystem returned ErrorFormFactorUnavailable)` (expected with no headset on; `-DryRun` still writes nothing to the game folder). `Install-Stage0.ps1 -Action Install` then returned `ADDITIONS_INSTALLED, Files=232, GameLaunched=false` with a 0-byte stderr (receipts: `artifacts\d124a-preinstall-install.{stdout,stderr}.log`). Post-checks: installed `BepInEx\plugins\VampireHuntersVR.Stage0.dll` sha256 == build `9056036E…`, installed `BepInEx\plugins\VhvrNativeHost.dll` sha256 == build `CC593A69…`, `BepInEx\config\vhvr-openxr-host.json` present, `artifacts\transactions\current.txt` → `artifacts\headset-d087\d123a\transaction`, no game/probe process. The game tree is therefore **modded and waiting**; the sitting command must be exactly `headset-handaim.ps1 -Tag d123a -DisableVSync -UsePreinstalled` (any other switch changes `config.json` and the equality guard refuses the run). Undo: `Install-Stage0.ps1 -Action Uninstall`.

### D-125 — d123a sitting (install-first, HOST_FAILED) read out: flame effect is stopped/restarted mid-fire (2026-10-11)
- **The D-124 mechanism worked**: preflight 12:24:49 → `pre-installed config matches this invocation` + `pre-installed payload in use (install skipped); installed DLLs match this build` at 12:24:50 → headset already attached → Steam launch 12:24:50 → game running 12:24:52 → GAMEPLAY LOCKED 12:25:26. **Install wait 0 s**, exactly as intended.
- **The sitting ended as HOST_FAILED at 12:27:07** after ≈102 s of gameplay in `Egypt_Map`. Verbatim native failure: `System.InvalidOperationException: gameplay camera/scene ownership lost; no menu fallback; failed=source-destroyed,player-destroyed,scene:Egypt_Map->MainMenu_2 timeScale=1`. The game returned to the main menu; by design the host does not fall back to a menu, so the run is preserved as HOST_FAILED and was not relaunched. The game was then closed (no orderly native close line).
- run.json facts: `verdict=HEADSET_RUN_COMPLETE_NOT_VERIFIED`, `observation=HOST_FAILED`, `preinstalled=true`, `installSkipped=true`, `stockVerified=true`, `headsetAttached=true`, `gitHead=ab6772e`, plugin `9056036E…`, native `CC593A69…`, `timingMedianCycleMs=26.78` (`timingMedianFps=37.3`), receipts 137, `controllersAvailableEver=true`, `nativeFailuresMax=0`, `fireFailed=false`, scenes `[Egypt_Map]`, both hands in the correct container (`Weapons - Left/Right Hand`), `maxWeaponsInSlot=1`, `sharedEver=false`, muzzle `Muzzle Flash Spawn`, max muzzle-to-hand angle `0.028°` (pose transaction healthy).
- **Result this sitting was for — mechanism (B) "the flame effect is repeatedly stopped/restarted" is supported; (A) pure frame-cadence is not.**
  - The effect is one ParticleSystem (`id=-349022`) on the flamethrower `weapon=-349004`, `lifetimeMax=0.30 s`, `activeInHierarchy` always true. 47 `[FLAME_FX]` samples over the session.
  - `alreadyFiring=True` co-occurs with `isVfxPlaying=False`, `ps.playing=False`, `particles=0` (frames 5924, 6369, 17702): **the effect is stopped while the weapon reports firing**, then replayed later.
  - Cumulative counters: `vfxPlayToggles=11`, `vfxCountResets=7`, particle min/max `0/26` over ≈101 s. Because samples are 2 s apart these are **lower bounds** on the real toggle count.
  - `[FLAME_POOL] flamesActive=0`, `flamesWithVfx=0`, `projectilesV2Active=0` in all 51 samples → the `Flames` / `Flames_Projectile_V2` pools are **not** the flame source for this weapon; the D-122 pool hypothesis is **closed**.
  - Fire/release parity: left `fireCalls 9 / releaseCalls 9`, right `21 / 21` while the trigger was held for continuous fire → the VR trigger path issues **paired fire+release**, not a held state. With `lifetimeMax=0.30 s` and the native rule that `stop_shooting()` clears `is_vfx_playing`, this is the leading mechanism for the user-visible "discrete puffs" (symptom C).
  - 26 complete `[SHOT_SCOPE]` transactions (`pre`/`post`/`before`/`restored`) plus 47 `outside` → the shot-scope transaction itself is healthy; the flame fault is independent of it.
- Not established: **why the game left `Egypt_Map` for `MainMenu_2`**. No counter in this run attributes it to the harness or the plugin, and the user's own report is required; until then it is a user/game-side event (death/pause/quit), not a harness defect.
- Boundaries kept: no gameplay/fire/effect behavior change, no DRM/anti-cheat interaction (single-player offline), runtime registry unchanged, game tree restored and verified (`STOCK_VERIFIED: 121 files / 8 directories / 7,726,223,158 bytes; hashes and write times match the pinned backup`). TOOL-070 stays **OPEN**; this is diagnosis, not a fix, and no fix is authorized by it.
- Next (read-only): find where the VR trigger path calls the weapon fire/release per sample (`HandFireTrigger` / `WeaponRuntimeBridge`) versus the game's `Flamethrower.stop_shooting()` / `Weapon.muzzle_play_stop`, to confirm the "fire+release per press" cadence as the cause of the restart churn.

### D-126 — TOOL-071 triage: the left-weapon visual spin is not from D-123; a pose tug-of-war is the suspect (2026-10-11)
- User return on d123a, both parts: (1) the sitting ended because they **died** (game over → main menu), so the `HOST_FAILED` scene return is a user-side event and needs no retry; (2) a new symptom, filmed as `<Recordings>\편집후\왼손 사격시 회전.mp4` (2.00 s, 1920x1080, exported 12:37) — "the left weapon keeps rotating, but attacks still land inside the reticle".
- **Clip read** (no ffmpeg on this machine; frames pulled with `artifacts/devtools/Grab-VideoFrames.ps1`, a WPF/MediaPlayer helper, into `artifacts/d125-video-frames/`): the world is static while the weapon model sweeps across the view — at t=0.1 s a giant flat polygon covers the middle of the view, at 0.6/1.1 s the weapon is out of view, at 1.4/1.8 s it reappears displaced.
- **Log side** (`artifacts/devtools/Analyze-PoseSwings.ps1` over the d123a `game.log`): all 26 complete shot scopes hold a **constant** muzzle-vs-aim angle through the four phases (`pre`/`post`/`before-restore`/`restored`, 0–15.6°, typically 4.9–8°) and the flame transform's forward is **byte-identical in every phase** → the shot transaction does not rotate the weapon and the fire origin stays on the aim ray. That is why the shots land while the model looks wrong, and why the artifact has to be a *writer conflict on the model*, not the fire path.
- **Attribution: not D-123.** Its `HandAimDriver.cs` diff is only the 5-line `flameFx` receipt block; the flame sampler contains no writes at all (guard `source-flame-fx-writes-nothing` in `tests/ShotScopes/Program.cs` asserts no `.Play(`, `SetPositionAndRotation`, `is_vfx_playing =`, `already_firing =`), and `AimFor` only reads an already-existing shot frame. The rotation-accumulating write `HandAimDriver.cs:210` comes from `6d03c93b` (2026-10-10, D-095).
- **Suspect (unproven)**: three writers per frame — the driver (hand pose at line 202 plus muzzle align at line 210), the game (container overwrites 5575 left / 11154 right in d123a), and `HandShotFrames.BeginShotScope`, which temporarily forces the **camera** transform and the shot roots onto the aim pose and restores them on dispose (`HandShotFrames.cs:65-83`). Putting the camera and the weapon root on one pose is a plausible source of the giant near-plane polygon; that alternation is a plausible source of the visible sweeping.
- Nothing was changed: this triage is read-only log/video analysis plus two git-ignored devtools scripts under `artifacts/`. TOOL-071 stays **OPEN**; the proposed next step is a ~10 Hz read-only rotation trace (container / muzzle / camera + shot-scope depth) and it needs approval.

### D-127 — TOOL-071 rotation trace implemented, built and verified (read-only); sitting pending (2026-10-11)
- User input: the left-weapon spin is a **regression** (it was not present in the d114a sitting) and a rotation trace is **approved**.
- Implemented with no behavior change: `src/interaction/HandPoseTrace.cs` samples per frame, per hand, the container the driver writes, the weapon prefab and its muzzle inside it, the aim proxy and the camera — **forward and up** axes — plus `weaponVsContainerDeg` (prefab drift inside the driven container), `muzzleVsAimDeg`, and `containerVsWrittenDeg` (how far the game moved our container after our write). It logs `[POSE_TRACE]` only when one frame swings an axis >30° or rolls the up axis >15° while the forward axis stays <5° (capped at 400 lines) and a 5 s `[POSE_TRACE_SUM]` summary; the receipt gains a `poseTrace` block per hand with per-node `maxStepFwdDeg`/`maxStepUpDeg`/`stepsOver5|15|30Deg`/`rollSteps`, `overwriteSamples` and `maxContainerVsWrittenDeg`.
- `src/interaction/HandAimDriverPoseTrace.cs` resolves the watched nodes at most once a second and calls the sampler from `Tick` after both hands are updated; `src/core/VampireHuntersVR.Stage0.csproj` compiles both new files.
- Write discipline is enforced by a test, not by promise: `tests/ShotScopes` now reads the two new sources and asserts they contain no `SetPositionAndRotation`, `.rotation =`, `.Play(`, `SetParent`, `new GameObject` or `Instantiate` (`source-pose-trace-writes-nothing`), that they do read `target.forward`/`target.up`/`Vector3.Angle` and emit both log tags, and that the hook runs after both hands (`source-pose-trace-hooked-after-both-hands`). `tests/verify.ps1` expected fixture count updated 25→28.
- Verified: build 0 warnings; plugin sha256 `D3D6E972DD852FB4A85759F109641E07A383EA786FEBB7667FD7A9A2FAD6C82E` (identical hash on an immediate rebuild); full verifier all fixtures EXIT=0 with the `VERIFIED:` line and 0-byte stderr; ShotScopes 28/28, HandAim 195/195. The native host is untouched (`CC593A69…`), the game tree is still stock, and nothing here touches firing, aim, range, damage or the existing pose writers.
- Next: phase 1 install (`-DryRun` + `Install-Stage0.ps1 -Action Install`) then one sitting with `tests\headset-handaim.ps1 -Tag d127a -DisableVSync -UsePreinstalled`, reproducing the left-hand spin per playtest step 6; read `[POSE_TRACE]`/`[POSE_TRACE_SUM]` and the `poseTrace` receipt block to name the node that moves.
- **Phase 1 for d127a was then run on the user's instruction (no headset)**: `tests\headset-handaim.ps1 -Tag d127a -DisableVSync -DryRun` → `DRY_RUN_OK` with `INSTALL_PLAN_OK: files=232; stock=121. Nothing written.` and 0-byte stderr (headset not attached, as expected), then `Install-Stage0.ps1 -Action Install -Workspace artifacts\headset-d087\d127a\transaction -OpenXrHostConfig artifacts\headset-d087\d127a\config.json` → `ADDITIONS_INSTALLED, Files=232, GameLaunched=false`, 0-byte stderr, 15 journal entries. Post-checks: the installed plugin sha256 equals this build (`D3D6E972…`), the installed native equals `src\render\NativeHost\bin\VhvrNativeHost.dll` (`D025204E…`), `BepInEx\config\vhvr-openxr-host.json` present, `artifacts\transactions\current.txt` → the d127a transaction, no game process. The sitting command is therefore exactly `tests\headset-handaim.ps1 -Tag d127a -DisableVSync -UsePreinstalled`, and the headset wait is zero because `config.json` was written from the same switch set and the equality guard matches.
- **Hazard recorded in the install-first path (D-124)**: the native host is **not byte-deterministic** — `tests\NativeHost.Tests.ps1` calls `src\render\NativeHost\Build.ps1 -SelfTest`, so **a full verifier run rebuilds `VhvrNativeHost.dll` and changes its hash** (observed `CC593A69…` → `D025204E…` with no native source change), which makes the pre-installed payload fail the sitting's hash guard. Running the verifier after phase 1 therefore requires re-running the Install step; the plugin DLL is stable (same hash across manual rebuilds).

### D-128 — d127a Phase 2 launched on explicit user approval
Date: 2026-10-11 | Mode: agentic | Stage6B/8 diagnostics; no gate pass
- User said 실행. Read-only prelaunch check: working tree clean, no game/probe, installed plugin/native hash-equal current outputs (`D3D6E972…`/`D025204E…`), config byte-equal, current pointer -> `<RepoRoot>\artifacts\headset-d087\d127a\transaction`. No rebuild/reinstall/native changes.
- Started exact `<RepoRoot>\tests\headset-handaim.ps1 -Tag d127a -DisableVSync -UsePreinstalled`: harness PID17848,04:57:47Z; payload/config guards pass, headset attached04:57:48Z, Steam requested, game PID56600 running04:57:50Z. First progress WAITING_FOR_GAMEPLAY, stderr0B. Install hashing skipped; headset connectivity check still applies (300s maximum, succeeded immediately), correcting the earlier claim of zero headset wait.
- Written user drill: monitor Play -> stage -> start, wear at GAMEPLAY LOCKED beep, step6 left stationary short fire / slow lateral held fire / arm extended5s, max600s gameplay. Dizziness means stop immediately; agent can write stop.request. Harness owns stop/game close/uninstall/whole-stock check; do not launch duplicate or claim cleanup until final receipts.
- **Interpretation constraints from source review, no binary edit**: Step seeds Forward before Up, making first/reseed/replacement Up comparisons potentially invalid/stale; counters combine forward/up comparisons, not unique frames. TracePose runs after both Update calls and before FireTick; zero containerVsWritten cannot exclude overwrites outside this observation window or short shot-scope motions. Observe catches sampling errors, but TraceHand node-resolution/source.transform calls are outside it, so non-interference is not comprehensively established by source-pattern guards. Monitor runtime exceptions and avoid attributing writer conflicts/root causes from these metrics alone. Sampling remains per-frame, not10Hz.
- Phase2 is now running; TOOL-070/071/072 and gates OPEN, final stock restoration and visible result pending. Continue using `<RepoRoot>\artifacts\d127a-phase2.pid`, `.stdout.log`, `.stderr.log` and sitting progress/receipts; do not rerun tag d127a.
- Runtime checkpoint: GAMEPLAY LOCKED04:58:39Z Egypt_Map;04:59:13Z rightDriving/fireCalls12, leftWaitingForWeapons/fireCalls0, controllersTrue/nativeFailures0/fireFailedFalse. POSE_TRACE_SUM/FLAME_FX/FLAME_POOL logs present; no matched Error/Exception/HAND_AIM-disabled, host failure file absent; both owned processes live and stderr0B. Initial left test waits for acquisition, visual result/cleanup pending. Additional source review: `OpenXrHost.LateUpdate:243-244` catches escaped Tick exceptions by releasing and disabling handAim, so node-resolution exceptions are a real behavioral risk, not merely a missed trace sample; no such error observed so far.
- Update04:59:44Z: bothDriving, fireCalls R/L19/7; receipt has ROOT `poseTrace.left/right`, samples3147/9381 and errors0/0/anomalies0/0 at read. No inference of visual PASS or overwrite exclusion. Launch task delivered at the human playtest gate; harness remains responsible for automatic close/uninstall/stock verification. Next response must inspect same run, not restart or assume stock until cleanup finishes.

### D-129 — Correct firing-angle interpretation; diagnose VFX and wrong-hand projectile separately
Date: 2026-10-11 | Mode: agentic | Stages6B/8 triage; no gameplay fix or gate pass
- User clarification: actual firing angle works, visible effects have wrong angle; right-hand projectile weapon sometimes launches from left. Chose preserve accepted aim/fire/damage/FX lifecycle while investigating separate visual and emitted-origin paths. No silent fallback, global90deg rotation, native-tail suppression, runtime/scene-policy switch, unsafeVector3 detour or DRM/anti-cheat work.
- Completed saved-run checks:d127aHOST_FAILED onEgypt_Map→MainMenu_2/source+playerdestroyed;causeunattributed,notclaimedcrashoruserquit. Finished05:03:55Z,uninstall0/cleanupErrornull/stockTrue;terminalRecoveredseq27/currentpointerhistoricalrecoveredtransaction;oldharness17848/game56600absent. Fresh independentstock121files/8dirs/7,726,223,158bytes/hash/time exact. SavedrunVDXRbefore/afterequal. Do not relaunchsame tag.
- Read-only video extraction40overviewcompleted(emptyerr);18dense0.2s sought samplescompleted(emptyerr). EditedvideoSHA256753A0D7A3AA4907DEB61C6510F8F6D715872AC3D3D5FE400944BE17ED390F9DE,209s1280x720. Inspected overview supports separated visibleflameblobs,multipleweapons/effects;notfirst-frameprojectileownership orframe-accuratedecoding. Dense/overviewartifactdata persists,video/runclock/buildidentitynotverified.
- Re-analyzed by normalizedvectors keyedframe+weapon+phase:24sampledpre-calls(20RIGHTFlamethrower:-349118/4LEFTSubmachine_Gun:-347918),169SHOT_SCOPErows/149FLAME_POSEmatcheswithidenticalroundedmuzzle/effectforwards;maxsampledaimage3. Counters8878calls(441L/8437R),ownlatchfalse0/0,missing0failures0 do notrecordemittedprojectileowner/origin. Per-shotmax178.78954deg/3.717299m differs frompost-drivernear0.028deg;firstdrift0.803001m/25.55495deg beforeleftweaponexists. No newcauseinferredfrommaxima.
- Native offline mapping usingalreadyconfirmedLibCpp2IL/Icedstack:effectdrawRVA46BD70writesowntransformforward;reloadstopRVA46C2F0existswithreload_t>0 andcanleavealready_firing;projectileRVA48E580,poolRVA47D420,ownerinitRVA47EF80,motionRVA4C3FB0 distinctfromcurrentshoot_forwardcoverage. PoolreuseandnewInstantiateusesuppliedworldposition/rotation;activationbeforeownerinitmaymatteronlyifa specializedcallbackreadsoldowner(unproven). Prioritizeactualmuzzlereference/returnedobject/owner/positionvelocityatinit+nextframe;thenuncoverednestedcamera path. No runtimeinstrumentationadded.
- **Withdraw D-125 overclaims:** equalcumulativefire/releasecountsnotproofofimmediaterelease;particlecountdecrease nottrueRestartcount;zero2spoolsamplesnotabsenceproof. Six d127asamplesfiringTrue/effectoffzero confirmstateatthoseinstants,butreloadstopnowgivesanotherexplanation. No causalPlay/Stopcallerproven. Historicalentryretained,notre-written.
- **Withdraw D-126/TOOL-071 coincident-root explanation:** WorldPoseScopepreservescapturedrootworldposewhilemovingcamera;doesnotforceroottoaimpose. OldAnalyze-PoseSwingsusesnon-normalizedroundedvectorsandframe-onlygrouping;quoted4.9–8deg/"healthytransaction"cannotproveactualshotdirectionorwhole-framehealth. Post-writertracestillhasblindspots.
- Validation:project-localofflineC#analyzerexit0;PowerShellscriptsparseclean/evidenceangle-selfchecksPASS;ShotScopes28/28andWeaponRuntime67/67exit0emptyerr. No fullverifier/nativebuild dueTOOL-072;productionplugin/nativehashesunchangedD3D6E972…/D025204E…. Onlylivingdocs+analysisreporttracked;helpers/receiptsignoredartifacts,notshippedgamecontent. Inheritednet6analyzerSDK EOLwarningsrecorded,nonehidden.
- Remaining:Q-D129-EVENTexistingvideotimestamp/affectedweapon;no newheadsetsittingrequest. TOOL-069/070/071/072andformalgatesOPEN. Fullmethods,evidencepaths,caveatsandnextdiagnosticdecisiontree:VFX-PROJECTILE-D129-ANALYSIS.md.

### D-130 — Reaffirm install-before-command readiness; prepare fresh d130a
Date: 2026-10-11 | Mode: agentic | Stage6B/8 deployment only; no game launch or bug closure
- User corrected the agent: "설치 해놨냐고" was a question, not a claim that the user installed it. Reaffirmed prior contract: game end -> stock restoration; code/build/test completion -> reinstallation before readiness report. User is already wearing headset at execution command, so never perform slow installation at that time. Installation is authorized now; game launch is not.
- Chose existing tested -UsePreinstalled path, no parallel launcher or altered cleanup. Fresh d130a config copies d127a with only Output changed; never relaunch completed d127a. Reuse existing validated plugin/native hashes D3D6E972…/D025204E…; no production source change, rebuild, full verifier, unsafe VFX detour, aim/runtime/policy change or DRM/anti-cheat action.
- Before preparation: no game/installer/harness; game folder stock-shaped, previous pointer is recovered d127a. VDXR active, Streamer running, existing probe/check-stock present. Full stock verification remains the installer's prerequisite, not inferred from file presence.
- Preparation started under <RepoRoot>\artifacts\headset-d087\d130a using Install-Stage0.ps1 -Action Install -Workspace ...\transaction -OpenXrHostConfig ...\config.json. Logs, exit receipt and prepare.json are local artifacts. Until verified, state is INSTALLING, not READY.
- Future readiness protocol saved in project AGENTS.md and all living docs. Build/test first, install last (TOOL-072). On explicit execution use <RepoRoot>\tests\headset-handaim.ps1 -Tag d130a -DisableVSync -UsePreinstalled; guards/probe and monitored stock restoration stay intact. No reinstall merely because a session ended; prepare again when coding or new readiness work is requested.
- **Completed:** installer PID52136 exited0;ADDITIONS_INSTALLED232files/GameLaunchedFalse;stderr0B. JournalterminalInstalledseq14 at05:32:07Z;currentpointerd130a. Independent project-local ignored Verify-Ready.ps1 parsed/ransuccessfullyexit0/empty stderr,verified05:32:33Z: pinned targetbuild/backupmanifest,all232livepayloadfiles,currentbuild+priorrunDLLidentity,configbyteequality/settingsOutput-onlydifference,fullstockoutsideownedrootshashes/times,activejournal/currentpointer,VDXR/Streamer/launchprerequisites. prepare.json READY_PREINSTALLED_NOT_LAUNCHED;configSHA256DF75CEC0E206D05E22A35C4DD0ED33D32637D100446F890C222C8E05C6F795C2. No game/probe launch, production build or runtime change. Read-only status command reported exit1 solely from Get-Process querying completed PIDs; install/verify explicit receipts both0. Earlier polling tool timeout did not stop installer; terminal receipts independently verified.

### D-131 — Start d130a on explicit command, skip installation
Date: 2026-10-11 | Mode: agentic | Stage6B/8 sitting; no bug closure
- User said 시작. Quick read-only prelaunch checks: no game/probe/installer/harness,unconsumed d130a,prepared/live/buildDLL hashes D3D6E972…/D025204E… match,configSHA256DF75CEC0… match,currentpointerd130a,terminalInstalled. No full-tree hashing/rebuild/reinstall at execution time.
- Started <RepoRoot>\tests\headset-handaim.ps1 -Tag d130a -DisableVSync -UsePreinstalled via harness15320 at05:57:40Z. Config and DLL guards passed,headset attached05:57:41Z,Steam requested same second,game running05:57:43Z. WAITING_FOR_GAMEPLAY05:58:02Z;stderr0B. Launch receipt <RepoRoot>\artifacts\headset-d087\d130a\launch-request.json and phase2logs/pid retained.
- Keep existing600s gameplay bound and automatic game close/uninstall/whole-stock/runtime checks. User already wears headset; next step in-game Play -> stage -> start. No duplicate launch or new behavior. Harness owns cleanup; next response inspects same run before claiming stock. prepare.json is historical readiness once launch begins. Visual results/root cause/gates still unresolved.
- **Update05:58:37Z:** GAMEPLAY LOCKED05:58:18Z Egypt_Map;gamePID72828/harness15320live;progressSESSION/controllersTrue/rightDriving/leftWaitingForWeapons/frames855;stderr0B. User already entered stage,so no further setup action. No visual acceptance,hook-causality or stock-restoration inference.

### D-132 — Prioritize presentation timing and identify recording source; finish d130a cleanup verification
Date: 2026-10-11 | Mode: agentic | Stages6B/8 triage; no production fix or gate pass
- User provides<Recordings>\편집후\이상한점.mp4and reports smoothHMD/stableHMDreticle versus all recordingsflicker/stutter;hand/model appears towardflameeffect. Chose preserveacceptedaim/damage;prioritizecapture-pathandmodel/camera/emittertimingbeforeparticle-onlyrotationortrackingblame. Unknowncapture source and HMDdisplaced-handvisibility explicitly recorded,not assumed.
- Directlyverifiedpreviouscommits0d18d8c/da359c0/39a1ed7. Inspectedexistingd130aonly;runfinished06:03:44.607Z,HOST_FAILEDcamera/playerownershiplossonEgypt_Map→MainMenu_2,causeunknown;uninstall0/cleanupErrornull/stockTrue,stderr0B;terminalRecoveredseq27,currentpointerhistorical. OldPIDs15320/72828absent;freshindependentCheck-StockPASS121files/8dirs/7,726,223,158bytes exacthash/write times;VDXRregistryandsavedbefore/afterequal. CurrentSTOCK,tagconsumed;no automaticreinstallaftergameend.
- NewclipSHA2560FBB7FD81B2FD0361D19BB1E18D875AB4005EC7D58C60A6AE863C3D63CBD8F0A,WPF36s1920x1080;12overview+12dense18.9–20.0s soughtsamples extractedexit0. Notcontinuousframeaccuratevideoanalysis;no measuredflickerrate,projectileownerorclip/runidentity. Someimagepayloadsomitted;notallimagesclaimedinspected. OfflinehelperInspect-VideoSequence.ps1parse0errors/run0;no productionbuild or fullverifier.
- ConfirmedtwoHMDeyeRenderTexturesandviewmatricesversusenabledoriginalcamera;sourcewindowisnotOpenXRcopytarget. Cannotinferleft/right-eyealternationinrecording. SharedcameraaimscopeissynchronousandpreservesWORLDroots;scopealoneisnotproofdesktopcapturesintermediatepose. Needper-cameraPreCull/PreRenderwriterorcacheevidence. Independentlyupdatedworldreticlecanstaystablewhilesubsequentmodel/emitterstatechanges.
- d130aRIGHTFlamethrower:-348992/LEFTmaxWeapons0;rightpre-writechangedposes19245 versuspost-drivermax0.027976453deg andscopealignmentmax179.23256deg/3.6777081m aredifferentstages.189SHOT_SCOPErows/36roundednormalizedflags>5degor>0.1m;firstframe2846/aim2845about0.274m/0deg demonstrates timingdifferenceonly. PoseTracepost-writerzerooverwritescannotexcludelaterwriters. No emitted-projectile owner/initialvelocitycaptured.
- Timingmedian25.19ms≈39.7submissions/s versusdisplay11.11ms≈90Hz;runtime smoothingplausible,notconfirmedactive. ParticleLocal/World/Customspaceandvelocitymodulesunrecorded. Chosehypothesesranked:distinctcapture/renderstates,late/camera-specificmodelwriters,particleframe,submission/capturepacing. No speculativeWorldspaceoverride/globalanglefix/unsafeVector3detour/reprojectionclaim/bugclosure.
- Livingdocsupdated;detailVFX-MIRROR-D132-ANALYSIS.md. NextfirstdiscriminatorQ-D132-CAPTURE;thenper-camera-reference/primitive-onlyposeandactualparticle/projectileobservationsifauthorized. No newheadsetsittingrequested;no new readiness claim. No silentdowngrade or anti-cheat/DRM action.

### D-133 — Read OBS settings; distinguish game output, encoder cadence, and hand/VFX presentation
Date: 2026-10-11 | Mode: agentic | Stages6B/8 triage; no production or OBS change
- User answersOBSGameCapture/customFFmpeg/GOP250and authorizesreadingOBSsettings. Read selected recording/capture/video values and logs only;no servicecredentials,secrets,rawprofilescopied,OBSsettingswrites,anti-cheathookchangesorlaunch. Existingprojectroot/scaffoldretained;resumedlivingdocs/skill,notnewwizardinterview.
- OBS32.2.2 activeprofile/collection 제목 없음,scene 모니터1/source 게임 캡쳐 2 typegame_capture windowVampireHunters.exe. D3D11sharedtexture2560x1440captureconfirmed14:58:08KST. Chosedesktop/HMDpresentationtimingasleadinghypothesiswithcapturepathnowconfirmed;scene nameisnotdisplaycapture. Doesnotconfirmflickerrootcauseorcamera-specificwriter.
- CurrentFFmpeg720p30/6000kbps/GOP250;exactOBS32.2.2opensourcecheckedGOP→AVCodecContext.gop_size. Raw<Recordings>\2026-10-11_14-59-17.mp4metadata109.12s/H264/30fps/720p andx264SEIkeyint250/scenecut40/rcabr/bitrate6000/threads22proveCPUx264fornearbyrecording,not savedstandardNVENCAV1/CQP24/keyint2s. Earlier10:20/10:21/12:26recordingsstandardNVENC;13:58/14:59FFmpeg. No blanketcurrentsettingattributiontoolderclips.
- GOP250frames≈8.33smaximumat30fps;not250seconds/fixedflickerperiod/provenstuttercause. ChoserecommendfuturestandardNVENCH264/CQP20–23/keyint2s/MKV/retain30fpsnotapply;customGOP60is2secondsat30fps. CPUloadpossiblebutoverloadunproven,30vs90Hzexplainscadencecontrastnot arbitrarymodel/VFXorientation.
- Latestlog3277drawn/3276output,noexplicitlagged/skipped/droppedormidrecordingreconnect. Countsnotproofzero repeatedsourcecontents,onedifferenceunattributed. Windowdisappears15:01:00.466KST,recordingends15:01:06.361KST;tailonlycandidate,notearlierflicker. Overlapsd130awallclockbutcleanupends15:03:44KST;nofalseeventequivalence. Edited36s1920x1080/rawmappingunknown,no fulldecodeorframeaccurateanalysis.
- Freshindependentwhole-stockPASS121files/8dirs/7,726,223,158bytes/hash/time,oldPIDsabsent,releasepayloadhashunchanged. Documentation-onlydiagnosisrequiresno productionbuild/install;currentSTOCKnotREADYand no repeatd130a. Rawheadermetadata/OBSsharedreadcommandssucceeded;initiallistingloglength0notactualempty. TwoinitialOBS KBfetchesfailed;usedexactversionofficialsourceandavailableguideinstead,noGPLcodevendored. Onepatchfailedatomicallyandwasreappliedafterconfirmingno changes.
- Five livingdocsupdated,Q-D132-CAPTUREanswered;Q-D133-HANDopen,nextsingleclarificationfromexistingexperience,no newheadsetsitting. Preserveacceptedaim/damage;particlevelocity/simulationframeandprojectileownerstillseparateOPEN. Detail:OBS-CAPTURE-D133-ANALYSIS.md. No bug/gateclosure or silentdowngrade.

### D-134 — Record limited HMD hand observation; prioritize desktop presentation without declaring root cause
Date: 2026-10-11 | Mode: agentic | Stages6B/8 triage; documentation-only
- User: "HMD 안에서는 확인하지 못함. 거기서 손은 좌 우 하나씩만 있었음". Chose to record one remembered hand per side and no verified anomalous hand toward flame in HMD. Rejected treating this as either positive HMD duplication or conclusive recording-only/HMD-flame-normal evidence;no synchronized event comparison exists.
- Q-D132-HAND/Q-D133-HAND answered with this limitation;Q-D134-MATCH retains the evidence gap without asking for another sitting. Prioritize desktop-game render-time hand/model state because OBS captures game output and HMD hands were not reported duplicated. Shared model/bone/VFX faults remain possible;acceptedaim/damage and projectileowner issue remain separate.
- Verified D-1320c9eebe/D-1333e572a3 commits withlog/show and initiallycleanworktree. Fresh Check-Stock.ps1PASS121files/8dirs/7,726,223,158bytes exacthash/time;oldPIDs15320/72828absent. ManagedreleaseDLL D3D6E972… matchespriorrecord. CurrentSTOCKnotREADY;never reuse consumedd130a.
- Read-only source: OpenXrHost.cs198–244 eye-render scheduling versusLateUpdatehandtick;321–329 separateCopyFromeye cameras/textures. HandUi.cs330–375 eye-pre-cull/canvas correction is UI-only evidence,not identified offending hand-mesh writer. No new frame-accurate decoding,particle state or projectileowner data.
- Initial PowerShell search accidentally included generated binaries despite -Include;repeated narrowly withgitgrep ontrackedsource. Verification command'sexit1camefrom a nonexistent src/native lookup after stockPASS;actualnativepathsrc/render/NativeHost/bin identified for explicit hash recheck. No production rebuild required for documentation-only update.
- Five livingdocsupdated;no source/DLL/OBS/install/runtime changes,automaticlaunch,new playtest,bug/gateclosure,silentdowngrade or anti-cheat/DRM action. Next: offlineedited/rawmapping if practical;per-camera frame/mesh/bone/muzzle/emitter telemetry only in separately authorized runtime work with freshbuild/test/preparation.

### D-135 — Establish sequential video mapping; prioritize busy-skip and native WeaponBase writers without claiming root cause
Date: 2026-10-11 | Mode: agentic | Stages6B/8 offline investigation; no production fix
- User askscontinueinvestigation. Retainedexistingprojectscaffold/acceptedaim/damage;no newheadsetsitting,installorlaunch. Directlyverified0c9eebe/3e572a3/96168bdandinitialcleanworktree;freshindependentstock121files/8dirs/7,726,223,158bytes/hash/timePASS,oldPIDsabsent,managed/nativeD3D6E972…/D025204E…unchanged. CurrentSTOCKnotREADY,d130aconsumed.
- No installedcontinuousdecoderfound. ChoseofficialFFmpegdownload-linkedGyan9.0.2portableexternaltoolinignoredprojectartifacts,ZIPpublisherSHA25660F467265B1E312373DBCD92200C2618A74850F98D3D078E94296BB3FA2047BAverified. NoPATH/systeminstallation/linkage/redistribution;notadoptingGPLtoolasaVRframework. RejectedWPFseek-onlysamplesforfrequency/mappingclaims. Firstdirectdecodecommandtimedout/zerooutput;isolatedworkerscompletedexit0withcountreceipts.
- Fullsequentialdecodeedited2160frames60fps/raw3240frames30fps;all2160contentmatches r=512+ceil(e/2),maxcentralMAE1.161/255,formulaerrors0. Editedframeodd/evenoffset17.083333/17.066667s;1079rawframesrepeatedtwiceplus2boundaries. Raw36.500/36.533/36.567sP/B/Pforegroundposejumpvisuallymatchededited19.416667–19.500s;notedit-onlydefect,notprovedmeshduplication. Largeimagesomitted;smallcontact+3closeupsread. Noquantitativeflickerfrequency.
- Rawmatchingwindow1081frames/maxPTS33.334ms/noadjacentidenticalsmallimagesorfullresolutionchecksums. Container109.120s≠decoded3240frames;OBSreported3276outputsdiscrepancyretainedwithoutcalling36drops. Blacktailfirstframe3100/103.333333s;finalPTSgap133.334ms,separatefromearlierincident. NoGOP-onlyfixorencoder-overloadproof;nominalrecordingwallclocknotUnityrenderframeprovenance.
- ReusedexistingLibCpp2IL/Icedfor36selectednativefunctions,build0/run0;inheritednet6EOL/nullablewarningsretained. NativePlayerController.UpdatewritesWeaponAnim.weapon_base60hlocalPosition50FC41/localRotation50FDF8/worldposition50FEA9aftercomputingrecoil/bob. WeaponAnim.updateitselfcomputesstate;donotmislabelitsbodyasthesetter. Actualhandmesh/boneinstanceandincidentbranchunknown. No weapon-namedrendercallbackfound;engine/shader/pipelinewritersnotexcluded.
- SourceReadHands6skipsentireTickbeforegaze/drive/trace/reticle;nativeOnRenderEventholdsmutexacrossPrepareincludingxrWaitFrame/HandsSync. Eyesenabledinwindows,sourcealwaysenabled. Choseconcreteconditionalhypothesisnativepose+skip+differentrendersampling;busy/phaseco-occurrenceunproved. Rejectedblockinglockoranimationdisable. Worldrootpreservationdoesnotundoeverydescendantlocalwrite;post-correctionoverwrite0doesnotcoverunsampledskipframes.
- Offlinefakecounterexamples/sourceguards16/16PASS,videoanalysisexit0,DecodeAST0errors;existingShotScopes28/28andWeaponRuntime67/67PASS. NotUnityincidentreproduction,hookconfirmation,damage/headsetacceptance. Twoinspectioncommandsparsefailedthenrerun;stockstderrmoduleprogressCLIXMLnotempty. Nofullverifier/nativepayloadrebuild;no anti-cheat/DRM action.
- Five livingdocsupdated,Q-D133-RAWanswereddecodedmapping;Q-D134-MATCHnarrowedtomatchedHMD/Unityrenderclock;Q-D135-BUSYOPEN. TOOL-069/070/071/072remainOPEN. DetailRENDER-TIMING-D135-ANALYSIS.md. Futureprimitive/reference-onlybusy/per-camera/mount/WeaponBase/mesh/bone/emittertelemetryrequiresseparateauthorizationandfreshbuild/test/preparation;neversilentlydowngradeorlaunchconsumedd130a.
- Finalcheckpoint:timing16/16rerunPASS,all2160formulaanddecode/test/stockreceiptschecked;analysishelperfinalbuild0/15EOL-nullablewarnings/0errors. Annotatedimageextractionexit0withFontconfigconfigwarnings,notempty stderr;labels/imagesvisuallyverified. Raw140small-imageadjacentduplicates=139blacktail+1frame3077/102.566667s,noneinincidentwindow. Productiondiffnone,releasehashesrecheckedunchanged;noanalysis/gameworkerleft. Finalverificationreceiptproject-local.

### D-136 — Complete agent validation; notify only at the final human test gate
Date: 2026-10-11 | Mode: agentic | Stages6B/8 diagnostic implementation; no root-cause fix
- User: "필요한만큼 검증하고 알려줘, 최종 테스트 아니면 내게 알릴 필요는 없어". Treat as authorization for needed diagnostic code/build/tests and existing reversible preparation, not automatic game/headset launch. Preserve accepted aim/damage/scene policy/runtime/OBS. No intake repeated, no consumed tag reused; existing wizard resume gates retained.
- Read pinned interop signatures before choosing Camera.FireOnPreCull/FireOnPreRender reference-only Harmony postfixes. Existing Player.Update before/finalizer gaze calls retained, with observations before/after native body and before/after finalizer gaze. Tick read observation precedes busy/rejected early returns; completion distinct from actual per-hand DrivenFrames changes. No blocking host mutex, native ABI/source or particle/animation writes.
- Chose bounded main-thread sampling + nonblocking TryAdd immutable JSON rows to a disk worker: 180s,24 transform nodes,16 renderers,8 emitters,4 candidate bones per skin,512 queued rows,100000 rows,256MiB. Finite-or-null components, actual IDs/path/parent/shared-mesh/rootBone/bone candidates, explicit truncation/drop/error state. Schema/harness/installer boolean and -NoRenderTimingDiagnostics disable option. New flag defaults on, no VR menu wiring claimed.
- Full node/particle velocity/projectile owner sampling not silently substituted: pinned particle MainModule/Particle nested query yielded zero matches; use existing read-only simulationSpace alias and particle count only. Actual velocity and emitter-space transform of velocity remain OPEN and explicitly marked unmeasured. Camera callbacks are installed candidates, not confirmed live hooks.
- D-135 video helper rerun exit0/maxMAE1.161;2160 formula matches/0mismatches;video hashes exact;raw140 small-image duplicates. Fresh stock121/8/7726223158 hash+times PASS before production preparation. New writer/source55/55PASS, initial plugin builds0warnings/0errors. Full verifier will rebuild native DLL before install; final hashes must be freshly pinned rather than old d130a hashes (TOOL-072 remains overall OPEN).
- Inspection errors retained: nonexistent scripts/dist installer/model-shape Program reads corrected to actual src/core installer and saved shape reports; chat search only searched chat workspace, not project, so switched to explicit absolute-path searches. One duplicate-file patch rejected (no changes), rerun canonical single-update patch. Python alias unavailable; original mixed line endings recovered from git bytes and localized edits reapplied, not broad formatting change.
- Independently verified commit1a48aaae7d6fe84d447ba537d7ad2e9f06e6d0d2 has exactly six documentation files. D-135 decode receipts/probe frames/showinfo lines2160+3240/gray byte lengths all agree; original SHA256s match;Fontconfig warnings verified,not empty stderr. Re-read raw36.466667/36.500000/36.533333closeups and continuous smallsheet: foregroundweapon/arm abruptchange confirmed;notmeshidentity or particletrajectory proof. OfflineD-135timing16/16rerunPASS alongside new writer55/55.
- First full verification finished31/31checks/no failures07:08:30Z but parent had ended before exit observation; reran through an exact-process wrapper with a durable final exit receipt instead of assuming exit0. No game launch or headset probe in either verifier. Build checks retain original mixed line endings with localized LF changed lines because git diff --check reports CR as trailing whitespace on added lines; no broad formatting rewrite.
- Final full verifier exit0 receipt07:11:16Z,31/31individual check receipts(expected CLI failures2/1 included),managedbuild0warning/0error,NativeHost41fault+4exact+4flipped+4shape fixturesPASS,existingHandAim195/195+ShotScopes28/28+WeaponRuntime67/67,new55/55PASS. Writer executable additionally ran3times55/55without rebuild. ExpectedCLIusage/output-rejection stderr reviewed separately;full verifier stderr empty. Latest productionmanagedA1A15129…/nativeDE2C5A0B… differ from consumed d130a after diagnostic change/native rebuild;fresh pins required.
- Preparation first attempt failed before creating tag/installation because PowerShell [DateTime]'...Z' becomes local16:00+09 then was compared to UTC07:11. Corrected threshold to DateTimeOffset.UtcDateTime; no state bypass or clock assumption. Retried untouched freshd136a with full exit/receipt guards. No automatic game launch/probe;installation/independent live validation pending completion.
- Freshd136a completed:installerexit0/232files/GameLaunchedfalse;Installedseq14at07:14:31.7004577Z;independentverifyexit0,stockoutsideownedexacthash/time+allpayload+liveconfig/build/pins/currentpointerPASS,VDXRunchanged,installer/verify stderr0B. CurrentMODDED/READY_PREINSTALLED_NOT_LAUNCHED;managedA1A15129A18E816FCD0A5EB3018671C95360C03FE74E89B259D40CFFEE3BD583/nativeDE2C5A0B3CB8A103EFA0ABFC194652943C8FCFB263BB1E13E538F5D06AF1061B/configCEDEA8006D65E08C8B9497FB2C730F301325E6A80098DCD5CA908C3E2068DC26. Oldd130aconfigpreservedexceptOutput/newdiagnosticflag. Neverfullverify/rebuildafterreadyinstall.
- Launch only when usercommands: <RepoRoot>\tests\headset-handaim.ps1 -Tag d136a -DisableVSync -UsePreinstalled;once,headset/payloadguards/monitoring/transactionalstockrestorationretained. NewPLAYTEST-D136-FINAL-KO.mdprotectsheadsettime2–3min,explicitlydiagnosticfinalparticipationnotfix/releaseacceptance. No bugs/gates closed. Newreadycheckeractuallyexecutedagainstliveinstall;ASTverified,no test/probe/game launch hidden in readiness.

### D-137 — User-authorized single d136a diagnostic run
Date: 2026-10-11 | Mode: agentic | Stages6B/8 playtest pending
- User "시작" authorizes this single run. Read playtest/harness/resume docs;verified no existing target/probe/harness,clean git tree and live payload/config pins before launch. No rebuild/reinstall/config/runtime/OBS/save modifications.
- Reuse exact <RepoRoot>\tests\headset-handaim.ps1 -Tag d136a -DisableVSync -UsePreinstalled. Ignored artifact worker <RepoRoot>\artifacts\d136-analysis\Launch-D136.ps1 AST checked/read back;waits for harness exit and persists exact exit. WorkerPID107832/harnessPID133628;logs launch.stdout.log/launch.stderr.log in same directory. No duplicate launch.
- 07:28:55Z preflight/config/build/live matches,install skipped,headsetprobe PASS,Steam request. 07:28:58Z game running;independently observed gamePID16792 and live BepInEx MainMenu_2 log. 07:29:20Z progress WAITING_FOR_GAMEPLAY. Not gameplay acceptance or render-timing hook/incident confirmation.
- Existing harness preserves bounded session,stop.request/orderly close,uninstall/fullstock/runtime checks. Exit/run/stock receipts remain pending;never claim restored before verification. User performs only existing 2–3min playtest and exits;no anti-cheat/DRM bypass or accepted aim/damage changes. All open bugs/gates retained.





















































































- **배포:** `dist\VampireHuntersVR-preview-0.2.zip`(33.81MB, 243 엔트리, sha256 `EC71BEF2…`) + `.sha256.txt`. 0.1 zip은 폐기(삭제)해 오래된 DLL이 배포되는 사고를 막는다. preview `install.ps1`은 `Runtime = ""`(자동), `HeadsetSeconds = 0`(무제한)을 쓰고 VD가 아니면 "미검증" 경고만 출력한다. README-KO도 무제한 세션/런타임 자동/설정 키에서 진단 옵션 제거를 반영.
- **남은 확인:** 헤드셋 1회(`d145a`). 확인 항목은 무제한 세션이 실제로 끝나지 않는지, stop.request/스테이지 종료로 정상 종료되는지, 런타임 자동감지 로그가 실제 ActiveRuntime과 일치하는지, 조작(양손/발사/UI) 회귀가 없는지.
















- **해상도 기준 확정 + 프리셋(사용자 방향: 3840x2160=1.0 normal / 3072x1728=0.8 low / 4800x2700=1.2 high)**: `EyeWidth`/`EyeHeight`는 **한쪽 눈 기준**이다(근거: d145a raw 4장이 각 18,229,248 B = 2064×2208×4, 양쪽 합계 4128×2208, 네이티브 스왑체인은 소스 텍스처 크기 그대로 생성 — `Host.cpp:80` `sc.width=src.Width`). 사용자가 제시한 3840x2160/3072x1728/4800x2700은 16:9(평면·양쪽 합계식) 숫자라 눈 하나에 그대로 넣으면 눈 비율이 0.935→1.778로 바뀌어 컴포지터가 늘어난 영상을 받는다. 따라서 **눈 비율을 유지하는 배율 방식**으로 구현했다: `low`=0.8× (1651x1766/눈), `normal`=1.0× (2064x2208/눈, 기본 = 기존 동작과 완전 동일), `high`=1.2× (2477x2650/눈), `custom`=`EyeWidth`/`EyeHeight` 그대로. 순수 정책은 `src\render\RenderResolutionPolicy.cs`(512~4096 클램프, AwayFromZero 반올림), 잘못된 프리셋은 `ArgumentException`으로 거부한다. 실효 크기는 로그(`RESOLUTION preset=… base=… effective=… pair=…`)와 리시트(`eyePreset`/`eyeWidth`/`eyeHeight`)에 남는다.
  - 명시: high는 라벨(1.2)을 따랐다. 제시 숫자의 비율은 4800/3840=**1.25**이므로 원하면 1.25× (2580x2760/눈)로 한 줄 수정 가능하다(QUESTIONS Q-D147-RES-SCALE).
- **비등방성 필터링(사용자 요청)**: 이미 옵션이었다(`Anisotropic`, D-104, 기본 false → 켜면 `ForceEnable` + limit 16). 새로 만들지 않고 README-KO·schema·문서에 노출만 했다. 정직한 한계: 이 옵션은 게임이 그리는 텍스처 샘플링에 영향을 주지만, 우리 눈 텍스처는 밉맵 없는 1:1 GPU 복사(`CopyResource`)라 눈 이미지를 다시 샘플링하지 않는다.
- **안티앨리어싱(MSAA) 미구현 — 결정 대기(TOOL-080)**: `Gpu.cpp:12`가 `SampleDesc.Count!=1` 소스를 거부하고 스왑체인도 `sampleCount=1`(`Host.cpp:80`)이므로 Unity 쪽 `RenderTexture.antiAliasing`만 올리면 `CopyAndWait`가 거부해 호스트가 실패한다. 필요 작업: 눈별 리졸브 대상 텍스처 2장 + `ResolveSubresource` 후 기존 1:1 복사(약 30줄, 메모리 +18MB/눈, 프레임당 리졸브 1회/눈). 옵트인(기본 off)으로 넣는 것을 제안하며 지금/나중은 사용자 결정이다. MSAA 없이도 `high`(1.2×)가 슈퍼샘플링처럼 계단을 줄인다는 점을 함께 보고했다.
- **메뉴 VR 재진입(P3-8)**: 사용자 결정으로 **나중**으로 보류(D-146의 열린 항목으로 유지).
- **테스트/빌드**: HandAim 200→**209**(D-147 guard 9: 프리셋 6 + 소스 guard 3), native fault 43→**45**(`frame-dump-off-by-default`, `frame-dump-opt-in-adds-pair`), `tests\verify.ps1` 기대 문자열 200→209, `tests\verify-handaim-ready.ps1`에 새 키 2개(`CaptureFirstFrames` false / `EyePreset` enum) 허용·검증 추가, plugin `DD53A41E…`(295,424 B, Release 경고 0·오류 0), 네이티브 재빌드 `B819BD21…`(326,656 B, Configure 시그니처 변경 + `frameDump` 게이트).
- **오프라인 검증(2026-10-13)**: full verifier **전 단계 PASS**(마지막 줄 `VERIFIED`, 51 RESULT/EXIT 라인, 설계된 비영 exit 2건, stderr 0 byte), HandAim **209/209**, native **45/45**, `artifacts\verification.json` = `F7257018…`(10,200 B, D-146과 동일 = 결정적 재현).
- **핀**: `artifacts\release-0.3\pins.json`을 D-147 빌드로 갱신(plugin `DD53A41E…`, native `B819BD21…`, loader `66946EA6…` 불변), `verified=NOT_HEADSET_VERIFIED_D-147`. TOOL-079(native 해시 비결정) 때문에 native 핀은 마지막 빌드 값이며, 0.3 패키징 전 `/Brepro` 반영을 권장한다.
- **새 관찰(TOOL-081, 검증 체인)**: 같은 소스·같은 세션에서 plugin DLL 해시가 한 번 `D17D8F8A…` → `DD53A41E…`로 달라졌다(두 verifier 실행 사이, 소스 변경은 docs/config뿐). 그 뒤 incremental·`-t:Rebuild`·`obj` 삭제 후 clean 빌드 **3회 모두 `DD53A41E…` 재현**됐다(commit `600c9eb`). 원인은 미확정이며(결정적 빌드가 아닐 가능성), 패키징 전 **깨끗한 트리에서 빌드하고 bin에서 두 DLL 해시를 다시 핀**하는 규칙을 추가한다. 이번 세션에서는 코드 변경 없이 기록만 했다.
- **아직 열려 있음**: ① MSAA(TOOL-080) ② high 배율 1.2 vs 1.25 ③ P2-4 성능(18분에 20→37ms, TOOL-078) ④ TOOL-079 `/Brepro` ⑤ **0.3 재포장 + 헤드셋 재검증 1회**(현 `dist\VampireHuntersVR-preview-0.2.zip`은 D-146 이전 빌드라 배포 금지).

### D-147b — 측정 우선 실행: URP/AA 실측 로깅 + TOOL-078 프레임 비용 계측 + `/Brepro`(TOOL-079) 해결 (2026-10-13)
- **사용자 승인 순서**(① URP/AA 실측 → ② TOOL-078 원인 분리 → ③ 필요하면 MSAA → ④ `/Brepro` → ⑤ 0.3 재포장 + 착용 1회)에 따라 ①②④를 코드로 완료했다. ③은 ①의 실측값과 ②의 비용 추이를 본 뒤 판단한다.
- **① URP/AA 실측 로깅(가정 금지)**: `Initialize()`가 `[OPENXR_HOST] QUALITY_AA` 2줄을 남기고 같은 값을 리시트 `quality` 객체에 담는다: `GraphicsSettings.currentRenderPipeline` 타입 이름(없으면 built-in), 파이프라인 asset **반사 프로브**(`msaaSampleCount`/`supportsHDR`/`antialiasing`/`postProcessingEnabled`), `QualitySettings.antiAliasing`, 눈 렌더타깃 MSAA, 소스/눈 카메라 `allowMSAA`, **소스 카메라 vs 눈 카메라의 컴포넌트 이름 목록**, 그래픽 API. URP는 이 프로젝트에 interop 참조가 없으므로 **읽지 못한 값은 `absent`/`error`로 정직하게 기록**하고 추측하지 않는다. 목적: "게임 자체 AA가 우리 눈 카메라에 적용되는가"를 가정하지 않고 실측한다(눈 게임오브젝트에 URP의 카메라 부가 데이터가 복사되지 않으면 게임 AA는 우리 눈 이미지에 오지 않는다).
- **② TOOL-078 계측(원인 분리)**: 45 제출 프레임마다 `[OPENXR_HOST] PERF frames=… windowFrames=… updateMs=… lateMs=… prepareMs=… saveMs=… saveWorstMs=… saveCalls=… saveBytes=… unityDeltaAvgMs=… unityDeltaMaxMs=… evidence=… managedHeapMb=…` 1줄 + 리시트 `perf` 객체(윈도 단위, 누적 아님 — 윈도 끝에서 리셋). 계측 대상: `Update()` 전체, `LateUpdate()`(HandAim), `PrepareCameras()`(눈 포징 = 네이티브 `prepToSubmitMs`가 기다리는 경로), `Save()`(JSON 직렬화 + 파일 쓰기 + 바이트 수), `Time.unscaledDeltaTime`, `GC.GetTotalMemory`/`GC.CollectionCount`. 드릴 summary가 `perfWindows`/`perfFirst`/`perfLast`/`qualityAa`를 자동 수집하므로 다음 착용 세션 로그를 손으로 grep할 필요가 없다. 18분 세션의 `prepToSubmitMs` 10.45→27.26ms 증가가 우리 쪽(save/힙)인지 게임 쪽(렌더 비용)인지 이 데이터로 분리한다.
- **계측 격리(착용 보호)**: 프로브/계측이 세션을 실패시키지 않게 격리했다. `QUALITY_AA` 블록 전체를 try/catch로 감싸 실패 시 `qualityProbe=null` + 경고만 남기고, `PERF` 로그와 `GC.GetTotalMemory`/`GC.CollectionCount`도 각각 격리해 실패하면 힙/GC 값만 -1이 된다. 근거: 착용 세션 1회가 유일한 데이터 창이라 계측 실패로 세션이 FAIL되면 비용이 크다.`r`n- **④ `/Brepro`(TOOL-079 FIXED)**: `src\render\NativeHost\Build.ps1` 링크 줄에 `/link /Brepro`를 추가했다. **증거**: 연속 2회 빌드가 동일 해시 `42C6D7FF…`(326,656 B), `tests\NativeHost.Tests.ps1`가 내부에서 `-SelfTest`로 재빌드한 뒤에도 같은 해시(재빌드-후-해시 드리프트 0 → `verify-handaim-ready.ps1`의 pin==bin 검사 회복). 회귀 방지 guard(`/link /Brepro /OUT:`)를 `tests\NativeHost.Tests.ps1`에 추가했다.
- **테스트/빌드**: HandAim 209→**212**(D-147b guard 3: `source-d147-perf-window-instrumented`, `source-d147-quality-aa-probe`, `source-d147-reproducible-native-link`), native fault **45/45**(픽스처 수 불변), plugin `D8AF4DFD…`(307,712 B, Release 경고 0·오류 0), native `42C6D7FF…`. `tests\verify.ps1` 기대 문자열 209→212 갱신.
- **오프라인 검증(2026-10-13)**: full verifier 마지막 줄 `VERIFIED`, stderr 0 byte.
- **핀**: `artifacts\release-0.3\pins.json`을 D-147b 빌드로 갱신(plugin `D8AF4DFD…`, native `42C6D7FF…`, loader `66946EA6…` 불변), `verified=NOT_HEADSET_VERIFIED_D-147b`.
- **아직 열려 있음**: ① MSAA(TOOL-080) 지금/나중 — 판단 근거는 ①의 `eyeComponents`/`msaaSampleCount` 실측값 ② high 배율 1.2 vs 1.25(Q-D147-RES-SCALE) ③ TOOL-078 원인 판정 ④ TOOL-081(깨끗한 트리 빌드 + bin에서 재핀) ⑤ **0.3 재포장 + 헤드셋 1회**(현 `dist\VampireHuntersVR-preview-0.2.zip`은 D-146 이전 빌드라 배포 금지).

### D-147c — d147a 착용 세션: (1) URP/AA 판정 (2) TOOL-078 원인 분리 (3) 스테이지 종료 리시트 NRE 수정 (TOOL-082) (2026-10-13)
- **세션**: `tests\headset-handaim.ps1 -Tag d147a -DisableVSync -HeadsetSeconds 0` — 13분 3초, 22,365 제출 프레임, VDXR, plugin `D8AF4DFD…`. 드릴 마지막 줄 `HEADSET_RUN_COMPLETE_NOT_VERIFIED observation=HOST_FAILED`, 게임 트리 stock 원복 True. 수동 조작은 전 구간 정상(양손 driving 85,265/53,075, 사격 725/701, 리티클 양쪽 true, nativeFailures=0).
- **(1) URP/AA 판정 — 게임 AA는 우리 눈에 오지 않는다**: `qualityMsaa=0`(게임 전역 AA 꺼짐), `eyeRtMsaa=1`, `sourceAllowMsaa=True eyeAllowMsaa=True`, `sourceComponents=Transform,Camera,Component,Component` vs `eyeComponents=Component,Camera` → 게임 카메라의 추가 컴포넌트가 눈 카메라에 복사되지 않아 카메라별 URP AA/후처리 통로가 없다. 반사 프로브는 4개 전부 `absent`(interop 프록시가 파생 `UniversalRenderPipelineAsset`이 아니라 베이스 `RenderPipelineAsset`으로 노출) → 추측하지 않고 미확인으로 기록. 따라서 눈 AA는 TOOL-080(네이티브 리졸브)로만 얻을 수 있다.
- **(2) TOOL-078 판정 — 주 원인은 게임 측, 우리 쪽은 평탄**: 498 PERF 윈도. 우리 고정 비용 평탄(updateMs 8.4→10.4, prepareMs 0.8→1.2, saveMs 1.3→2.2 per window, saveBytes 286KB 고정, evidence 256 캡), `lateMs`만 장면 의존 변동(56.8→559→83). 동시에 `unityDeltaAvgMs` 4.5→6.93, 네이티브 `cycleMs` 26.4→38.6, `prepToSubmitMs` 15.0→23.4 상승, 런타임이 90Hz→45Hz 주기로 전환 → 우리 쪽 누적이 아니라 장면 무거워짐이다. 남은 관찰: `managedHeapMb` 1604→1890MB(+22MB/분) — PERF 줄에 `gc=` 추가로 다음 세션에서 확인.
- **(3) TOOL-082 필드 버그(FIXED, 헤드셋 재확인 대기)**: 13분 지점 스테이지 종료에서 `StageEnded()→EndGameplay()`는 정상 작동했지만 종료 리시트의 `sourceCamera=source?.name`이 파괴된 Unity 카메라의 `name`을 읽어 `NullReferenceException at UnityEngine.Object.get_name()`을 던졌고, 그 예외가 `Fail()`로 흘러 정상 종료가 `host.json.failure.json`(FAIL)로 기록됐다. IL2CPP에서 파괴된 Unity 오브젝트는 C# 참조가 non-null이라 `?.`가 단락되지 않는다. 수정: `SafeName(...)`(오버로드 `==null` + try/catch)로 리시트/진단/인벤토리/`ObjectPath`의 name 읽기를 감싸고, `Save()`는 `ReceiptJson()` 분리 + try/catch/finally로 실패 시 최소 리시트(`verdict/endReason/receiptError`)만 쓰고 **절대 예외를 밖으로 내보내지 않게** 했다. `OwnershipDiagnosis()`는 `diagnosis-failed:<Type>`을 반환. HandAim 212→**215**.
- **권고(사용자 결정 대기)**: 현재 `cycleMs`가 예산(11.11ms)을 이미 초과하므로 ① MSAA(TOOL-080) ② `EyePreset=high`(1.2×)는 **성능 여유 확보 후**가 합리적이다. 성능을 먼저 볼 경우 `EyePreset=low`(0.8×)가 가장 직접적인 레버다. `managedHeapMb` 추이와 스테이지 완주 재확인을 한 번 더 한 뒤 0.3 재포장을 권한다.
### D-148 — FPS 확보를 위한 계측 2종: submitMs 3분해(네이티브) + 모니터 뷰 비용 프로브(관리형), d147a 데이터로 원인 후보 확정 (2026-10-13, built, NOT headset tested)
**d147a가 남긴 세 가지 사실**: (1) 같은 해상도에서 \RenderFrames=2\의 \prepToSubmitMs\가 \RenderFrames=1\의 과거 세션(fp1 8.4ms)보다 2~3배 컸고(12.0→24.4ms), \cycleMs\ 중앙 30.10ms(33.2fps) vs fp1 22.53ms(44.4fps)였다. (2) 눈 픽셀 1.45배(fp3 2688x2880)는 \submitMs\만 +2.5ms 올리고 \cycleMs\ 중앙은 22.53→22.79ms로 거의 그대로였다 → 해상도는 병목이 아니다. (3) \submitMs\(7~12ms)는 acquire+copy+**GPU fence 대기**+\xrEndFrame\이 직렬로 섞인 한 덩어리여서, 남은 FPS가 어디로 가는지 로그만으로는 판정할 수 없었다(D-104가 미뤄둔 분해).
- **계측 1 (네이티브, submitMs 3분해)**: \CopyAndWait\가 선택적 out-파라미터 \issueMs\/\enceMs\를 보고하도록 시그니처를 확장(기존 호출부는 기본 인자로 그대로 컴파일)하고, \Host::Submit\이 두 눈의 copy 발행 시간과 GPU 완료 대기를 합산하며 \xrEndFrame\ 호출 시간도 따로 잰다. \RecordTiming\의 한 줄에 \copyMs= fenceMs= endFrameMs=\를 추가(ABI·HostStatus 136 불변, 측정 전용). \enceMs\가 크면 원인은 **Unity가 큐에 쌓아둔 GPU 작업**(=게임 렌더 비용)이고, \endFrameMs\가 크면 런타임(VD 인코딩/전송)이다. 픽스처 스텁 시그니처 동기화 + \	iming-window-resets-and-never-fails\가 새 합계 3개도 0으로 리셋되는지 검사하도록 확장(45/45 PASS), \/W4 /WX\ 통과.
- **계측 2 (관리형, 모니터 뷰 비용)**: VR 중에도 게임의 소스 카메라는 모니터용으로 씬을 한 번 더 그린다(미측정). \MonitorCostProbe\(기본 false)를 켜면 \MonitorCostProbePolicy\가 **네이티브 120프레임 타이밍 창과 정확히 같은 경계**에 맞춰 3창을 돌린다: 창0 기준 → 창1 \source.enabled=false\(눈 카메라 2개만 렌더) → 창2 복원. 각 전환을 \[OPENXR_HOST] MONITOR_PROBE phase= name= frames= intoPhase= sourceEnabled=\로 남기고 리시트에 \monitorProbe\ 객체(phase/name/disabledFrames/appliedWindows/origin)를 기록한다. 그래서 모니터 창의 \copyMs/fenceMs/endFrameMs/cycleMs\를 기준·복원 창과 **1:1로 비교**할 수 있다.
- **안전 규칙**: 프로브는 어떤 경로로도 세션을 실패시키지 않는다(전체 try/catch). 소스 카메라는 \Cleanup()\·\EndGameplay()\·\Fail()\ 세 곳에서 무조건 복원하고, 소유권 검사는 프로브가 끈 것을 손실로 보지 않는다(\(source.enabled||monitorProbeDisabled)\, 진단 문자열도 동일). 계측 자체는 \monitorProbeDisabledFrames\/\ppliedWindows\로 검증 가능하다. 기본값은 배포 설치 스크립트·드릴·스키마 모두 OFF, \erify-handaim-ready.ps1\은 설치된 config에 이 키가 있으면 false가 아닐 때 거부한다(키 수 계산도 갱신).
- **드릴 수집 확장**: \
un.json\에 \	imingMedianFps\에 더해 \	imingPreSubmitMidMs/timingSubmitMidMs/timingCopyMidMs/timingFenceMidMs/timingEndFrameMidMs/timingDisplayPeriodMidMs\(중앙값)와 \monitorProbe\ 로그 라인, 그리고 기존 \perfFirst/perfLast/qualityAa\를 남긴다. 패턴은 앞에 공백을 요구해 \submitMs\가 \prepToSubmitMs\에 걸리지 않게 했다(오늘 실제로 그 파싱 실수를 했다).
- **테스트**: HandAim 215→**226**(정책 6 + 소스가드 5, \source-submit-split-timing\ 포함), 네이티브 fault 45/45, plugin Release 경고 0/오류 0.
- **다음 착용 세션(권장 명령)**: \-RenderFrames 1 -MonitorCostProbe\ (원하면 \-EyePreset low\ 병행). 판정 기준: ① \enceMs/endFrameMs\ 비율로 "Unity GPU 대기 vs VD 제출" 분리 ② 모니터 창1에서 \copyMs/cycleMs\가 창0·창2보다 얼마나 작은지(모니터 뷰 실제 가격) ③ \RenderFrames=1\에서 \prepToSubmitMs\가 fp1 수준(≈8.4ms)으로 내려오는지.
### D-148 결정 (2026-10-13) — RenderFrames 기본값 2 → 1 (측정 근거, built, NOT headset tested)
- **근거**: d147a(RenderFrames=2) vs fp1(2026-10-10, RenderFrames=1, 같은 눈 2064x2208) — 중앙 prepToSubmitMs **17.0 vs 8.5ms**, 중앙 cycleMs **30.10 vs 22.53ms**(33.2 vs 44.4fps), 그리고 d147a의 초반20 창 23.87ms는 fp1 초반20 19.87ms보다 이미 높았다. 즉 2는 **결과를 버리는 엔진 프레임을 한 번 더 렌더**한다(장면이 무거워질수록 손실 증가: 후반20 24.4 vs 10.1ms).
- **변경**: OpenXrHost.RenderFrames 기본값 1, 드릴 -RenderFrames 기본 1, dist\preview\install.ps1 기본 1, config\schema.json(+README-KO)에 1로 기록. **2·3은 A/B용으로 그대로 유지**(드릴 파라미터).
- **검증 체인**: 	ests\verify-handaim-ready.ps1의 "accepted configuration drift" 루프에서 RenderFrames를 제외했다(기본값 변경은 의도된 결정이며 주석으로 이유를 남김). erify.ps1은 이 스크립트를 실행하지 않지만 수동 사전검사가 설치본 config를 엄격 비교하므로 함께 고쳐야 했다.
- **알려진 이력**: 과거 ha7에서 사용자가 안정성 이유로 2를 택했으나, fp1·fp3·ha9 세션은 모두 1로 정상 동작했고(당시 크래시는 shoot_forward 별건으로 정리됨) D-095의 RenderFrames<3 fast path가 1을 전제로 설계되어 있다. d148a 착용 세션이 회귀를 재확인한다.

### D-148b — d148c 착용 세션(8분 49초) 판정: FPS 상한은 **VD 45Hz 주기**이고 병목은 **게임 측 Unity GPU 대기**; 모니터 뷰 가격 ≈1.2ms; TOOL-082 실착용 검증 (2026-10-12, headset tested)
- **세션**: `tests\headset-handaim.ps1 -Tag d148c -DisableVSync -HeadsetSeconds 0 -RenderFrames 1 -MonitorCostProbe -HeadsetWaitSeconds 900`. 22,590 제출 프레임, `Maze_Small`, `endReason=level-exit`, 188 타이밍 창. 드릴 최종줄 `HEADSET_RUN_COMPLETE_NOT_VERIFIED observation=DRIVING_BUT_GAME_OVERWRITES_CONTAINER stock=True`, `nativeClosedLine=… CLOSED orderly=1`, stock 121 파일/8 디렉터리 원복, pins `4C79E46A…`/`5AAE5611…` 일치, `*.raw` 0, stderr 0 byte. **`d148a`·`d148b` 태그는 헤드셋 미연결(`xrGetSystem → ErrorFormFactorUnavailable`)로 소모**되어 세 번째 시도로 실행했다(두 번 다 `NO_HEADSET_ATTACHED observation=NOT_RUN stock=True`, 세션 데이터 0).
- **판정 ① FPS 상한은 런타임 주기다**: 중앙 `cycleMs 22.76` vs `displayPeriodMs 22.22`(=Virtual Desktop 45Hz), `waitMs` 0.86~2.69ms 유휴. 즉 계산이 아니라 **VD 스트림 주기가 43.9fps를 결정**하며, 창당 약 5ms의 여유가 남아 있다. → 45fps를 넘기려면 코드가 아니라 VD 스트림/헤드셋 주기 설정을 올려야 한다(코드 최적화로는 오르지 않는다).
- **판정 ② 병목은 게임 측**: `fenceMs 5.11`(Unity가 큐에 쌓아둔 GPU 작업) vs `endFrameMs 0.09`(VD 제출) = **57×**. 우리 네이티브 copy issue는 `copyMs − fenceMs ≈ 1.71ms`. **로그 표기 주의**: Host.cpp:179에서 `copyMs`는 **양눈 issue+fence 합**(따라서 `copyMs ⊃ fenceMs`)이고 불변식은 `copyMs + endFrameMs ≈ submitMs`다(예 7.20+0.69≈7.90). 플레이테스트 문서의 옛 체크리스트(`copyMs+fenceMs+endFrameMs ≲ submitMs`)는 이 의미 오해를 담고 있었으므로 수정했다.
- **판정 ③ 모니터 뷰 가격 ≈1.2ms, fps 영향 없음**: `MonitorCostProbe` 3창(origin=120 → 창 f240 기준 / f360 소스 OFF / f480 복원)에서 소스 카메라를 끄면 `prepToSubmitMs −1.24ms`, `submitMs −0.92ms`, `fenceMs −0.69ms`인데 **`cycleMs`는 22.24 vs 22.34로 불변**. 모니터 뷰는 45Hz 예산 안에 들어와 있어 제거해도 fps가 오르지 않는다(제거를 기능으로 만들 이유가 없다). 리시트 `monitorProbe(sourceDisabledNow=false, appliedWindows=2, phase=done)` ✓.
- **판정 ④ `RenderFrames=1` 기본값 확정**: 중앙 `prepToSubmitMs 9.66`(d147a RenderFrames=2: 17.0, fp1: 8.5) — D-148의 2→1 결정이 실착용으로 정당화됐다. 전/중/후반 드리프트도 prep 8.57→10.44(+22%), cycle 22.54→23.18로 d147a(30.10ms, prep 24.4) 대비 크게 평탄.
- **TOOL-082 실착용 검증**: 정상 스테이지 종료(hostState=2, `level-exit`)가 FAIL 리시트를 만들지 않았고 `CLOSED orderly=1`로 종료됐다. 조작도 양손 Driving/사격 976·803, reticleHit 양쪽 true, `nativeFailuresMax=0`.
- **예산 결론(다음 결정용)**: 45Hz 유지 조건에서 MSAA(TOOL-080)·`EyePreset=high`(1.2×, 픽셀 +44%)를 시도할 여유가 있다(창당 약 5ms 유휴 + fence 여유). 판단은 다음 착용 세션 1회로 실측한다.
- **남은 미확정**: `managedHeapMb 1574→1866` = **+33MB/분**, `gc` gen0/gen1 +48/+48, **gen2 ±0**(8.8분 동안 전무) → 단조 증가. 우리 고정 비용(`prepareMs≈1.1ms`, `saveMs≈1.8ms/창`, 286KB JSON)은 평탄하므로 **귀속 미확정**(IL2CPP 게임 힙과 공유 지표). 다음 계측 과제로 남긴다.
- **다음 단계**: ① (사용자 결정) VD 스트림 주기 상향 / MSAA / 1.2× / 0.3 재포장 중 우선순위 ② TOOL-081(깨끗한 트리 빌드 + bin에서 두 DLL 재핀) ③ 0.3 재포장 + 포장본 착용 1회.

### D-148c — 작업 프로토콜 고정: "준비(설치+검증)까지만 → 사용자 '실행해'에 실행" + d148e 준비 완료 (2026-10-12)
- **사용자 지시**: 앞으로는 실행 준비까지만 해 두고, 사용자가 "실행해"라고 하면 게임을 실행한다. 이번 요청은 "SSW를 끄고 상한 재측정"을 준비만 해 두는 것. (`d148a`·`d148b`·`d148c` 세 번의 시도 중 앞의 두 번은 헤드셋 미연결로 태그가 소모됐기 때문에, 설치를 미리 끝내 두면 그 낭비가 사라진다.)
- **준비 절차(재현 가능, `artifacts\<tag>-analysis\Prepare.ps1`)**: ① 게이트 — `git status --porcelain` 빈 값(깨끗한 트리) ② `artifacts\d148b-verify.log`가 `VERIFIED` 꼬리 ③ `verification.json` 32 검사 전부 Passed ④ `hand-aim-fixtures` 226 / `native-host-fixtures` 45 ⑤ bin의 plugin·native 해시가 `artifacts\release-0.3\pins.json`과 동일 ⑥ 게임/프로브 미실행. 그 뒤 config 생성 → `payload-pins.json` → `Install-Stage0.ps1 -Action Install -Workspace <dir>\transaction -OpenXrHostConfig <dir>\config.json` → `tests\verify-handaim-ready.ps1 -Tag <tag>`(독립) → `prepare.json`.
- **준비 게이트의 제약(정직하게 기록)**: (1) `HeadsetSeconds`는 승인값 **600**만 허용 → 준비 세션은 10분 상한(조기 종료는 `stop.request`). (2) `MonitorCostProbe`는 준비 설치에서 반드시 **false** → 모니터 비용 프로브가 필요한 세션은 이 경로로 준비할 수 없고 드릴 직접 실행을 써야 한다. (3) 나머지 플래그는 실행 시에도 동일해야 한다(`-DisableVSync -RenderFrames 1`), 다르면 `-UsePreinstalled`가 config 불일치로 거부한다.
- **실행(요청 시에만)**: `tests\headset-handaim.ps1 -Tag d148e -DisableVSync -RenderFrames 1 -UsePreinstalled -HeadsetWaitSeconds 900` — 설치를 건너뛰고 곧바로 Steam을 띄우며, 헤드셋은 실행 후 연결해도 900초까지 기다린다. Virtual Desktop 설정(SSW/프레임레이트)은 **사용자가** 바꾼다(D-104: 이 프로젝트는 VD 설정을 쓰지 않는다).
- **d148e 준비 결과**: `READY_PREINSTALLED_NOT_LAUNCHED` — 232 파일, gameLaunched=false, stockOutsideOwnedRoots=true, install exit 0, 독립 준비검증 exit 0, stderr 0 byte, plugin `4C79E46A…`/native `5AAE5611…`(= 0.3 핀), 게임 설치 config 해시 == 준비 config 해시, `MonitorCostProbe=false`, 눈 2064x2208/normal.
- **같은 시각 정리한 자원 귀속(CPU/GPU/메모리) — d148c 실측**:
  | 자원 | 실측값 | 판정 |
  | --- | --- | --- |
  | CPU(우리) | PERF 창당 update+late+prepare+save = 224~353ms / 176~207 프레임 = **1.1~2.0ms/프레임**(대부분 HandAim `lateMs`) | 45Hz 예산에서 9% 미만 |
  | CPU(엔진) | `unityDeltaAvgMs` **7.7~9.2ms/프레임**(≈110~130fps) | 게임 자체는 45Hz에 여유 |
  | GPU(게임) | `fenceMs` 중앙 **5.11ms**(Unity가 큐에 쌓은 GPU 작업 대기) | 최대 단일 항목, 그러나 한계 아님 |
  | GPU(우리) | copy issue `copyMs − fenceMs` ≈**1.71ms**, `endFrameMs` **0.09ms** | 무시 가능 |
  | 메모리 | `managedHeapMb` 1,574→1,866MB(**+33MB/분**), gen0/gen1 +48/+48, **gen2 ±0** | 스톨·한계 미관찰, **귀속 미확정**(IL2CPP 힙 공유 지표), VRAM 미측정 |
  **결론**: 45Hz에서는 **어느 자원도 병목이 아니다**(창당 약 5ms 유휴). 60Hz(16.67ms)는 16.6ms 작업으로 아슬아슬, 90Hz(11.11ms)는 불가. 사용자가 중반에 느낀 "고개를 돌리면 화면이 늦게 따라옴"은 자원 문제가 아니라 **파이프라인/네트워크 지연** 신호이며, 리시트에 지연 계측이 없어 귀속 미확정(`poseToSubmitMs`/`submitAheadMs` 계측 제안).

### D-148b4 — ⓑ 오버랩 착수: submit 펜스 "지연 증명"(opt-in `DeferredGpuWait`) + d149a 세션 준비 (2026-10-12, 코드 완료/**착용 미검증**)
- **배경**: d148f가 "해상도/GPU는 레버가 아님"을 보였다(픽셀 −36%에서 `fenceMs` 4.69→4.29만 반응, 사이클은 장면 부하로 증가). 우리가 통제 가능한 남은 비용은 **`fenceMs`(GPU 완료 스핀 4.3ms)** 뿐이고, `gapMs` 4.6ms는 Unity 렌더 스레드 회전(=우리 코드 밖), `prepToSubmitMs`는 엔진 프레임이다.
- **OpenXR 제약 조사**: `xrEndFrame(N)` 이전에 `xrBeginFrame(N+1)`은 금지 → "엔진이 N+1을 그리는 동안 N을 나중에 제출"하는 단순 더블버퍼 제출은 **불법**. 그래서 제출(`xrEndFrame`)은 같은 프레임에 그대로 두고, **완료 '증명'만 한 프레임 뒤로** 옮겼다(아이당 이벤트 쿼리 2개 핑퐁). 복사 발행과 이미지 릴리스는 같은 프레임(표준 OpenXR 순서), 증명은 다음 `Submit` 진입에서. GPU가 실제로 뒤처지면 그 지점에서 그대로 블록(동일한 백프레셔, 조용한 완화 없음).
- **안전 계약**: 완료 증명은 **사라지지 않고 이동**한다. 증명을 취할 수 없으면(디바이스/컨텍스트 부재) 조용히 넘기지 않고 **실패로 표면화**(세션 −1). `Close()`는 스왑체인 파괴 **전에** `DrainDeferredGpuWaits()`로 미결 증명을 회수한다.
- **테스트**: 네이티브 45→**49**(지연-1프레임 순서, 파괴 전 드레인, init 후 옵트인 거부, 증명 실패 치명성). 픽스처의 "증명 없이 레이어 금지" 불변식을 **정확히 두 상태만 허용**하도록 갱신(그 이하로 완화되지 않음)했고, 실패 시 **케이스 이름**을 출력하도록 하네스를 개선했다. HandAim 227→**228**(배선 가드), `verify.ps1`/드릴 요약 필드/스키마/ready 게이트(신규 `-ExpectDeferredGpuWait`) 갱신. 전체 **32/32 PASS**.
- **기본값 OFF**: 0.3 배포 경로는 기존 "제출 내부 증명" 유지(무단 승격 없음). d149a 착용에서 `fenceMs`/`cycleMs`로 이득을 측정한 뒤에만 승격 판단.
- **사용자 체감(d148f)**: 저해상도에서 **선명도 저하 + 프레임 드랩 악화** → `EyePreset=low` 기각, **normal 확정**(계측과 일치).
- **d149a 판정 규칙(사전 등록)**: `fenceMs` 4.3 → ~0으로 줄고 `cycleMs`가 그만큼(≈4ms) 줄면 = 스핀은 실제 손실(승격 검토). 변화 없음/악화면 = GPU 큐 자체가 페이싱 → 엔진 프레임과 제출의 더 깊은 오버랩 필요. 사용자 체감도 함께 본다(이미지 깨짐/찢김 여부 — 지연 증명은 릴리스를 표준 순서로 바꾸므로 이 확인이 승격 조건).

### D-148b3 — d148f 착용 세션(EyePreset=low, 90Hz 요청): **항등식 닫힘 → 3.7ms는 `gapMs`(렌더 스레드 회전)**, 해상도는 레버가 아님 (2026-10-12, headset tested)
- **① 계측 성공**: 246창 전부 `displayPeriodMs=11.11`. 표본 창에서 `cycleMs = gapMs + pumpMs + waitMs + prepBodyMs + prepToSubmitMs + submitMs` 잔차 ≤0.05ms → 미계측 구간 소멸. 정체는 **`gapMs`(직전 submit 종료 → 다음 Prepare 진입: 렌더 스레드 회전 + prepare 디스패치), 중앙값 4.58ms = 사이클의 약 23%**. `handsMs 0.02 / pumpMs 0.00 / prepBodyMs 0.03 / logMs 0.05`로 **`HandsSync`·`Pump`·`Log()` flush 가설은 전부 기각**(측정으로 부정).
- **② 판정(잠정)**: 픽셀 −36%(1651×1766)에서 GPU 쪽만 감소(`submitMs 6.40→5.69`, `fenceMs 4.69→4.29`)하고 전체 `cycleMs`는 18.13→20.26ms로 **증가**(`prepToSubmitMs 8.01→9.82`, `gapMs` 4.58 신규) → **비용은 해상도가 아니라 장면 부하를 따라간다 = 직렬 대기 한계**.
- **교란(정직 기록)**: d148f는 스테이지가 `Maze_Small`(d148e는 `Playground`)이고 사격 838/739회로 훨씬 치열했다 → 세션 전체 fps 비교(55.1→49.4)는 **부하 교란**이며 "해상도가 무의미하다"의 근거로는 부족하다. 확실한 것: (a) 항등식/무죄 판정, (b) GPU 항목만 해상도에 반응, (c) 초반 20창 17.08 vs 16.95ms로 사실상 동일.
- **다음 레버**: ⓐ **이른 prepare 디스패치**(Submit 직후 다음 Prepare 즉시 발행 → `gapMs` 4.6ms 회수 시도, 작은 변경, 새 계측으로 효과 측정 가능) ⓑ **오버랩/더블 버퍼**(엔진 N+1 렌더 동안 N 복사·제출, 큰 변경). 통제 A/B(같은 스테이지 normal vs low)는 위 ⓐ/ⓑ에 비해 정보량이 낮아 후순위로 내린다.
- **부수**: 우리 CPU/프레임 1.01→**0.55ms**, 엔진 `unityDeltaAvgMs 4.90`, 힙 증가 +37MB/분(gen2 ±0), `nativeFailures=0`, 정상 `session-ended`/`CLOSED orderly=1`, stock 121/8 원복.

### D-148b2 — 미계측 3.7ms의 계측 확장 + EyePreset=low A/B 준비 (2026-10-12, **코드 머지/착용 미검증**)
- **배경**: d148e에서 `cycleMs`를 `waitMs + prepToSubmitMs + submitMs`로 전부 설명하려 하면 **3.7ms가 남았다**(18.13 − 8.01 − 6.40). 네이티브 루프를 읽어 보니 그 구간은 `gapMs`(직전 submit 종료 → Prepare 진입: 렌더 스레드 회전 + prepare 디스패치) + `pumpMs`(Prepare 진입의 `Pump()`) + `prepBodyMs`(`xrBeginFrame + xrLocateViews + HandsSync`)였고, 특히 **매 프레임 렌더 스레드에서 3회 파일 flush하는 `Log()`** 비용도 그 안에 섞여 있었다.
- **변경(측정 전용, ABI·동작 불변)**: 네이티브 타이밍 줄에 `prepBodyMs=%.2f handsMs=%.2f pumpMs=%.2f gapMs=%.2f logMs=%.2f prepares=%d`를 **기존 필드 뒤에** 덧붙였다(기존 파서는 접두 패턴이라 그대로 동작). `Log()`는 `TimedLog()`로 감싸 `logMs`에 누적한다(부분 구간, 합산 대상 아님). 항등식은 `cycleMs = gapMs + pumpMs + waitMs + prepBodyMs + prepToSubmitMs + submitMs`.
- **근거/게이트**: `tests\HandAim\Program.cs`에 `source-timing-cycle-decomposition` 가드 추가(HandAim 226→**227**), `tests\NativeHost.Faults.cpp`의 `timing-window-resets-and-never-fails`가 새 합계들의 리셋까지 검증(케이스 수 45 유지), `tests\verify.ps1` 기대치 227 갱신, 드릴 요약에 `timingPrepBodyMidMs/timingHandsMidMs/timingPumpMidMs/timingGapMidMs/timingLogMidMs/timingPreparesMidMs` 추가.
- **빌드/검증**: 네이티브 재현성 유지 확인(2회 연속 동일 `EE69008C…`, 328,704 B, /Brepro), native 45/45, HandAim 227/227, 전체 검증 **32/32 PASS**, stderr 0B. 핀 갱신(plugin `704DC2D7…`, native `EE69008C…`) — 플러그인은 바이트 재현이 안 되므로 TOOL-081(패키징 직전 재핀) 유지.
- **② A/B 설계**: 다음 착용 세션은 **`EyePreset=low`**(0.8× = 1651×1766, 픽셀 ≈64%)에서 같은 90Hz 요청 조건으로 돌린다. 판정 규칙: fps가 눈 크기 절감에 비례해 오르면 **처리량(엔진/GPU) 한계** → 저해상도·MSAA 예산 협상이 유효, 거의 안 오르고 `gap/pump/prepBody` 합이 그대로면 **직렬 대기(지연) 한계** → 오버랩/더블 버퍼가 정답. `d148e`(normal)와의 사이에 계측 코드 차이만 있으므로 그 비교는 유효하다고 본다(동작 불변).

### D-148d — d148e 착용 세션(SSW off, 90Hz 요청): **45fps 상한은 VD/SSW였음이 확정**, 새 상한은 앱 55fps(18.13ms) (2026-10-12, headset tested)
- **세션**: 준비된 `d148e`를 `-Tag d148e -DisableVSync -RenderFrames 1 -UsePreinstalled -HeadsetWaitSeconds 900`로 실행(설치 생략 → 3초 만에 Steam, `installSkipped=true`). 사용자가 VD에서 **SSW off**, 스테이지 `Playground`, 238 타이밍 창 / 28,590 프레임 / `endReason=level-exit` / `CLOSED orderly=1` / stock 121/8 원복 / 핀 `4C79E46A…`·`5AAE5611…` 유지 / `nativeFailures=0`.
- **핵심 결과**: `displayPeriodMs`가 **238창 전부 11.11**(= 런타임이 90Hz를 요청)이고 `cycleMs` 중앙 **18.13ms = 55.1fps**, `waitMs ≈ 0.00`. 즉 **43.9 → 55.1fps(+25%)** 로 오르며 **상한이 VD에서 앱으로 넘어왔다**. d148c(45Hz 요청, 유휴 0.86~2.69ms)와 대조하면 "45fps는 VD/SSW가 만든 상한"이 실측으로 확정된다.
- **새 병목의 구성**: `prepToSubmitMs 8.01`(엔진 프레임 + 눈 카메라 포징) + `submitMs 6.40`(Unity GPU 큐 대기 `fenceMs 4.69` + 우리 copy 발행 ≈1.7 + `xrEndFrame 0.08`) + **미계측 3.7ms**. 엔진 자체는 `unityDeltaAvgMs 4.99`(≈200fps)로 여유이고, 우리 CPU는 1.01ms/프레임이다 → 남은 비용은 **직렬 구조**(엔진 프레임 → 카메라 포징 → GPU 완료 대기 → 제출)에 있다.
- **90Hz까지 7ms 부족**(18.13 → 11.11). 레버: ① **오버랩/더블 버퍼**(구조 변경, 잠재 −5ms, 가장 큰 레버) ② `EyePreset=low`(−1.5~2ms) ③ 모니터 뷰 제거(−1.2ms, 앱이 한계인 지금은 fps로 직결 ≈59fps) ④ MSAA는 비용 추가 → fps 목표와 상충(선명도 목표로는 별개).
- **부수 관찰**: `managedHeapMb` 증가율이 +33MB/분(d148c) → **+43MB/분**(d148e), gen2는 여전히 ±0. fps가 오르면 시간당 할당도 늘어난다는 뜻이므로 **귀속 계측이 더 필요**하다(우리 코드의 창당 고정 비용은 그대로: prepareMs 0.96, saveMs 1.65/창, evidence 256 캡).
- **사용자 체감 확인 필요**: SSW off로 프레임 간격이 22.8→18.1ms로 줄고 합성 프레임이 사라졌으니 "고개 돌림 지연"이 개선됐는지, 55fps 변동(52~59)에서 끊김이 없는지.



































- **왜 이 조사인가**: D-149b가 "벽은 CPU 구조가 아니라 GPU/전송 파이프라인"으로 좁혔지만, 남은 레버(포비티드/VRS/재투영/해상도/VD 설정)는 **런타임이 실제로 제공하는지**가 먼저다. 헤드셋 시간은 비싸므로 추측 대신 **열거로 먼저 판정**했다.
- **실측 방법(헤드셋 불필요, 읽기 전용)**: 1st-party `OpenXrProbe.exe --loader <게임에 설치된 openxr_loader.dll> --extensions --watchdog-seconds 20` → 인스턴스 생성 직후 `xrEnumerateInstanceExtensionProperties` 결과를 영수증에 기록(시스템/세션 생성 없음). 영수증: `artifacts\d150-analysis\runtime-extensions.json`, 로그 `probe-ext.out.log`.
- **결과(하드 증거)**: 런타임 `VirtualDesktopXR 1.0.10`, 인스턴스 확장 **31개**. 판단에 중요한 것:
  - ❌ **포비티드/VRS 계열이 하나도 없다** — `XR_FB_foveation*`, `XR_VARJO_foveated_rendering`, `XR_VARJO_quad_views`, `XR_META_foveation_eye_tracked`, `XR_EXT_foveated_rendering` 전부 없음. → "포비티드로 GPU를 아껴 해상도를 올린다"는 계획은 **이 런타임에서는 성립하지 않는다**(추측 금지 원칙에 따라 기각).
  - ❌ 스페이스워프/재투영 확장도 없다(`XR_FB_space_warp` 등 없음) → 재투영은 VD 자체 설정(SSW)으로만 제어된다.
  - ✅ `XR_FB_display_refresh_rate` — 앱이 **디스플레이 주파수를 요청**할 수 있다(72/80/90/120). 지금은 사용자가 VD에서 바꿔야 하는 값을 코드로 고정/실험할 수 있는 유일한 축.
  - ✅ `XR_KHR_composition_layer_depth`(+`cylinder`) — 깊이 레이어를 주면 런타임 재투영 품질이 올라갈 수 있다(SSW 켤 때 특히). 현재 우리 호스트는 깊이를 제출하지 않는다.
  - ✅ `XR_KHR_visibility_mask`(가려진 영역 스킵), `XR_KHR_D3D12_enable`/`vulkan_enable2`(게임을 D3D12로 돌릴 여지), 손/눈/바디 트래킹 계열.
- **같은 조사에서 나온 기존 주장의 정정(정직)**: 문서·계획에 "**모니터 뷰 제거 = −1.2ms ≈ 59fps**"로 적혀 있었으나, 이 수치는 **어떤 착용 세션에서도 확정되지 않았다**. 모니터 뷰 프로브(`MonitorCostProbe`)는 **d148d 단 한 세션**에서 돌았고, 그 세션은 45Hz+SSW 구간이라 창마다 `waitMs`가 3.9→7.6ms로 흔들려(런타임 페이싱) 세 창을 비교할 수 없다(`prepToSubmitMs` 8.81→6.55→5.44, `cycleMs` 23.12→22.35→22.24). → **"−1.2ms"는 미검증 추정으로 강등**하고, 참값은 **현재 구조(prepare-ahead)에서 프로브를 다시 돌려** 재야 한다(코드 추가 0).
- **레버 재정렬(실측 근거 강도 순, 새 판단)**:
  1. **VD 쪽 GPU/인코더 경합 시험(코드 0)**: `waitMs=0.00`(런타임이 우리를 기다리게 하지 않음)라 VD 주파수는 **우리 fps 상한과 무관**하다. 그러나 VD의 캡처/합성/인코딩은 **같은 GPU**를 쓰므로, 스트림 해상도/코덱/비트레이트를 한 단계 낮추면 `fenceMs`(5.1~6.3ms, 부하·해상도 무관하게 고정)가 줄어드는지가 판정점이다. 줄면 GPU 경합이 벽, 안 줄면 Unity 쪽 제출/드로우가 벽.
  2. **패스 수 줄이기**: 현재 씬은 **3번** 그린다(눈 2 + 모니터 1). 모니터 뷰 비용은 위 ①의 프로브로 먼저 재고(미검증), 그 다음 단일 패스(스테레오 인스턴싱) 여부를 판단한다. 단, 게임에 `Unity.XR.Management`/`Unity.XR.OpenXR` 플러그인이 **없다**(interop 확인: `UnityEngine.XRModule.dll`만 있음) → Unity XR 단일 패스로 가려면 플러그인 이식이 필요하고 커스텀 셰이더 인스턴싱 지원 여부가 위험 → **R&D 항목으로 보류**.
  3. **엔진 품질 다이얼(이번에 구현)**: 씬 자체가 벽인지 한 세션으로 판정할 수 있는 유일한 도구.
  4. **해상도(화질)**: d148f(픽셀 −36%)·d149a(픽셀 +56%) 모두 fps가 픽셀에 비례해 움직이지 않았다 → **픽셀은 사실상 공짜에 가깝다**. 즉 `EyePreset=high`(선명도↑)와 VD 스트림 해상도↑는 **화질만 올리는 후보**로 남는다(다음 착용 후보).
  5. **깊이 레이어/주파수 요청**: 화질(재투영) 축. 깊이는 SSW를 켤 때만 의미가 있고, 주파수는 VD 설정으로 이미 가능 → **보류(문서화)**. D3D12도 보류(우리 복사 경로가 D3D11 전용).
  6. **부드러움(체감 품질)**: `managedHeapMb`가 분당 +33~43MB로 자라고 GC 스파이크가 1% low를 깎는다. fps 상한이 막힌 지금, **주파수 약수로 fps를 고정**(예: 45/30)하는 편이 변동 54fps보다 체감이 좋을 수 있다 → 사용자 선택 항목.
- **구현(이번 커밋) — 엔진 품질 다이얼 `PerfProfile`(opt-in, 기본 `off`)**:
  - `src/render/PerfProfilePolicy.cs`(순수 함수, 값만 계산) + 호스트의 `ApplyPerfProfile()`(설정별 `try/catch`로 하나씩 적용, 실패는 이름으로 보고).
  - 다이얼: `shadowDistance`, `shadowCascades`, `shadowResolution`, `pixelLightCount`, `lodBias`, `particleRaycastBudget`, `realtimeReflectionProbes`, `softParticles`, `antiAliasing`. `light`(그림자 60m·cascade 1·pixelLight 2·lodBias 0.8·particle 64·반사/소프트파티클 끔) / `strong`(그림자 0·pixelLight 1·lodBias 0.6·particle 32·MSAA 0).
  - **하드 규칙**: 프로파일은 **오직 값을 낮춘다**(`Lower` — 게임이 이미 낮게 잡았으면 그대로 둔다). 즉 "게임보다 좋아 보이게" 만들 수 없고, 영수증에는 `before`/`target`/`after`가 그대로 남는다(`[OPENXR_HOST] PERF_PROFILE ...`, 리시트 `perfProfile`).
  - 검증(오프라인): HandAim **229 → 237**(8 신규: off/null/미지정 기각, light/strong 목표값, strong≤light, 값이 절대 오르지 않음, 그림자 0 유지, 소스 배선 2건), 플러그인 빌드 경고 0. `config\schema.json`에 `PerfProfile` 추가, 드릴 `-PerfProfile off|light|strong`, 준비 검증 `-ExpectPerfProfile`, `artifacts\d150-analysis\Prepare.ps1 -PerfProfile`.
- **우리 코드 자체의 비용(같은 세션 재계산, 정직)**: d150a의 536개 PERF 창에서 **median `lateMs/frame` 0.465ms**(hand tick), p90 1.42, max 2.85; `updateMs/frame` 0.054. 즉 **우리 모드의 관리 비용은 18.5ms 사이클의 약 2.5%** → fps의 원인이 아니고, 최적화 여지는 ① 꼬리(끊김) ② 할당 churn(관리 힙 1.9GB, GC 134/100/8)이다.

### D-152 — 0.4c 착용 결과(**두 레버 동시**): **80.5fps(12.42ms)** — d150a 54.1fps 대비 **+49%**, 모든 매칭 버킷에서 −3.9~−7.3ms, `fenceMs=0` 유지 (2026-10-13, **headset tested**)
- **세션**: `-Tag 0.4c -DisableVSync -RenderFrames 1 -EyePreset normal -PrepareAhead -DeferredGpuWait -UsePreinstalled`, VD SSW off + 90Hz, `Maze_Small`, **384 타이밍 창 / 47,480 프레임 / 600초**(= 평균 **79.1fps**), `CLOSED orderly=1`, 실패 0, `stock=True`, 핀 `C37E1F9D`/`F7FDF2D6`, gitHead `4dcf96b`(dirty 0).
- **전체 구간(384창)**: `cycleMs 12.42 = 80.5fps`, `gapMs 0`, `prepMs 10.09`, **`submitMs 1.95`**, **`fenceMs 0`**, `prepCpuMs 40.36`. 구간별: 첫20 **11.74ms/85.2fps**, 중간20 11.86/84.3, 마지막20 **13.64/73.3**(부하 증가), `cycle min/max = 11.28/19.46`.
- **매칭 버킷 비교(0.4c − d150a, 3000프레임 × 9버킷, 각 25창)**: `d_cycleMs` **−3.87 / −4.77 / −7.30 / −7.04 / −6.77 / −6.95 / −4.35 / −6.02 / −6.60** (전부 음수), `d_fenceMs −4.97~−6.16`, `d_submitMs −4.64~−5.94`, `d_copyMs −4.64~−5.88`, `d_prepToSubmitMs −3.25~+1.32`. → **세션 변동이 아니라 실재하는 이득**이다.
- **해석(D-149b 결론의 수정)**: 단독으로는 구조 이득(−4ms)이 인라인 GPU 펜스로 재흡수됐지만, **둘을 함께 켜면 흡수 자체가 사라진다** — 펜스를 지연 증명으로 프레임 밖으로 보낸 상태에서 준비 프레임까지 없애면 두 이득이 **중첩**된다(예상 66~77fps를 넘어 80~85fps). 즉 "고정 파이프라인"은 **단독 레버에 대해서만** 참이었고, 파이프라인은 **두 지점을 동시에** 건드리면 풀린다.
- **사전 등록 판정(Q-D149-3의 d150c 팔)**: ⓐ 기능 무장(`aheadPrepares=120/창`, `DEFERRED_GPU_WAIT on`·`PREPARE_AHEAD on` 로그, 총 46,079 체인) **충족** / ⓑ `d_cycleMs ≤ −1.5ms` **크게 충족**(−3.9~−7.3) / ⓒ **사용자 체감 보고 대기** / ⓓ 정상 종료·실패 0·stock 원복 **충족** → **승격은 ⓒ 하나만 남았다**.
- **남은 위험(정직)**: 지연 증명은 이미지를 **완료 증명 전에** 릴리스하므로 이론상 런타임이 미완성 이미지를 샘플할 수 있다(찢김/깜빡임으로 나타날 수 있음). 네이티브 로그에는 증명 실패/치명 오류가 없다(활성화 줄 1개만). 따라서 **ⓒ 축에서 특히 "찢김·깜빡임·좌우 눈 어긋남"을 반드시 확인**해야 한다.
- **부수 발견(다음 팔 설계에 중요)**: 게임 자체 품질 값이 이미 낮다 — `PERF_PROFILE profile=off ... | shadowDistance=20 cascades=1 shadowResolution=Low pixelLights=1 lodBias=1 particleRaycastBudget=64 realtimeReflectionProbes=False softParticles=False msaa=0`. 즉 **`PerfProfile=light`는 사실상 무효**(그림자 20≤60, cascade 1, pixelLight 1 그대로; `lodBias` 1→0.8만 바뀜). 유효한 팔은 **`strong`**(그림자 20→0, lodBias→0.6, particle 64→32, msaa 0) 또는 다른 축(해상도/모니터 뷰/VD 스트림)이다.
- **다음**: ⓒ 사용자 보고 → 승격 결정(둘 다 기본 ON) 여부. 그 다음 팔은 ① `-MonitorCostProbe`(문서의 −1.2ms 참값) ② `-EyePreset high`(화질 무료 상향 검증) ③ `-PerfProfile strong`(씬이 벽인지) ④ VD 스트림 해상도↓(인코더 경합). 참고로 90Hz 목표(11.11ms)까지 **잔여 1.3ms**.

- **다음(착용 후보, 코드 0 또는 opt-in)**: ① `0.4`(레버 둘 다 ON) — 유지 이득 확인 ② `0.4 + -PerfProfile light`(품질을 낮춰 fps가 정말 오르는가 = 씬이 벽인가) ③ `0.4 + -MonitorCostProbe`(모니터 뷰 참값) ④ `0.4 + -EyePreset high`(픽셀 공짜 확인 = 선명도 향상).

### D-151 — 버전·기준점 운영 규칙: 마지막 실측 실행을 **0.3V**로 동결, 이후 0.4/0.5 (2026-10-13, 운영 결정)
- **사용자 지시**: "마지막 런이었던 150a를 백업해 언제든 복구할 수 있게 하고 기준점으로 삼는다. 150a를 **0.3V**로 명시하고 다음 네이밍은 **0.4, 0.5** 형식으로."
- **0.3V = d150a의 바이트 스냅샷**: `dist\versions\0.3V\`에 ① `payload\`(플러그인 `C50D1BCA`, 네이티브 `F7FDF2D6`, 로더 `66946EA6`, 설정 `C0209408` — d150a 트랜잭션 격리소에서 그대로 복사 = 그 세션이 실제로 돌린 바이트) ② `pins.json`(해시 + `gitHead=25cbec7` + 측정값) ③ `timing-d150a.json`/`prepare-d150a.json`/`pins-d150a.json` ④ `RESTORE-KO.md`(소스 복구 경로와 바이트 복구 경로, 되돌리기 절차).
- **복구 규칙(정직)**: 드릴은 설치된 DLL을 "현재 빌드 산출물"과 비교하므로(`-UsePreinstalled`), 옛 바이너리로 **착용 실행**하려면 **소스 커밋 복원 + 재빌드**가 필요하다. 바이트 복사는 분석/대조용이다. 네이티브는 `/Brepro`로 결정적이고, 플러그인 해시가 커밋 기준과 다르면 그 사실을 기록한다.



































### D-155 — 백드롭 정책 축소: **HUD Bottom Shadow만** 남기고 나머지 게임 배경(레벨업 검은 배경·메뉴·상자·보스상자)은 **저작 그대로** (2026-10-13, **code+offline verified, headset pending**)
- **사용자 지시(최종)**: ① **`Player HUD/Bottom Shadow`는 계속 숨긴다** ② **나머지 잘려나간 게임 배경은 되살린다 — 레벨업, 메뉴, 상자, 보스상자 등**.
- **증거(이 세션의 캡처)**: `artifacts\video-1010\hud40.png`·`hud70.png`, `artifacts\video1010b\montage.png`, `artifacts\perf-d104\eye0-full.png`. 정지 화면의 배경이 **아예 렌더되지 않았고**(컬), 플레이 중 정면 HUD 사각형은 남아 있었다(그 사각형의 아래쪽 반투명 띠가 Bottom Shadow).
- **두 객체는 별개다(이 결정의 근거)**: ⓐ **HUD Bottom Shadow** = `AllThingsBaseLevel/Prefab_Player_Level1/Player HUD/Bottom Shadow`, 스프라이트 `HUD Bottom Shadow`, tint `1,1,1`(투명도는 텍스처에), 풀 캔버스 → **모드와 무관하게 숨김 유지** ⓑ **레벨업 배경** = `Level Up/NewNewCanvas/Image - Background Full Screen (1)`, 순수 검정 alpha 0.80, non-Filled → **되살린다**. 즉 정책을 뒤집는 게 아니라 **줄이는** 변경이다.
- **이전 결정과의 관계(모순 해소)**: D-105/D-106/D-112는 "정지 화면의 넓고 어두운/반투명 그래픽 = 가짜 배경"이라는 **휴리스틱**으로 컬했다. 그 휴리스틱이 실제 게임 배경까지 지웠고(사용자가 본 "잘려나간 배경"), TOOL-066/D-106의 \"회색-푸른 헤이즈\"는 **Bottom Shadow**였다(사용자 확인) → 따라서 **전면 뒤집기(전부 복원)도, 유지도 아닌 축소**가 맞다. D-105/D-106/D-112는 **폐기하지 않고 명시적 옵트인으로 강등**한다(아래 `hide`).
- **설계 — bool 하나가 아니라 닫힌 3모드(`GameBackdropMode`)**: 순수 정책 `GameBackdropPolicy`(`src\interaction\HandAim.cs`).
  - `authored`(**기본·배포값**): 게임 배경을 **컬하지도, tint alpha를 건드리지도 않는다** — 레벨업/메뉴/상자가 게임이 저작한 그대로 나온다. (UI를 머리 앞으로 옮기는 것은 D-090 기능이지 배경 편집이 아니다.)
  - `dim`: D-096의 **알파 캡만**(넓고 어두운 정지 패널, 컬 없음). 검은 벽이 부담스러울 때의 중간값.
  - `hide`: D-096 캡 + D-105/D-106/D-112 **컬** = D-155 이전 동작 전체(대조군/A-B).
  - **알 수 없는 값은 추측하지 않는다**: `OpenXrHost`가 Initialize에서 `invalid game backdrop mode: authored|dim|hide`로 **throw**(EyePreset/PerfProfile과 같은 계약).
  - **Bottom Shadow는 모드와 무관**: `HideHudBackdrop`(D-112)만이 그것을 제어한다 — 어떤 모드에서도 그 하나는 숨겨진다.
- **구현 지점(정확한 위치)**: ⓐ `HandAim.cs`에 `GameBackdropPolicy`(+D-155 주석) ⓑ `HandUi.cs` `HideDarkHudPanels()`에서 `bool hide = knownShadow || (GameBackdropPolicy.ShouldCullBackdrops(GameBackdropMode) && legacyPanel);` — 광범위 휴리스틱은 `legacyPanel`로 분리 ⓒ `HandUi.cs` `UiDim()`에서 `bool hide = ... && legacyCull;` + `if (!GameBackdropPolicy.ShouldDim(GameBackdropMode)) continue;`(authored는 알파 불변) ⓓ `OpenXrHost.cs`의 키 1개 + 검증 1줄 + `handAim.GameBackdropMode` 배선 + `GAME_BACKDROP mode=` 로그 + `host.json`의 `gameBackdropMode`/`hideHudBackdrop` ⓔ `HandAimDriver.cs`의 `ui.gameBackdropMode` ⓕ `config\schema.json`(`x-host-tunables`, enum + default authored) ⓖ `dist\preview\install.ps1`(`GameBackdropMode = 'authored'`) ⓖ' `tests\headset-handaim.ps1`(`-GameBackdropMode`) ⓗ `tests\verify-handaim-ready.ps1`(`-ExpectGameBackdropMode` + 키 개수 검사에 `+ [int]$gameBackdropKey`).
- **변경하지 않은 것(회귀 방지 명시)**: Bottom Shadow의 영구 억제(alpha0 + pre-eye/레이아웃 재적용, D-112), 크로스헤어 컬(D-100), 카드 평탄화/측정(D-105/D-107), 레이 정책(D-105), 캔버스 WorldSpace 변환(D-090) — 모두 그대로다.
- **검증(오프라인)**: 플러그인 Release **경고 0·오류 0**, HandAim **254 → 263**(신규 9 = 순수 정책 3 + 매치/게이트 소스 가드 4 + 배선/툴 가드 2), `tests\verify-config.ps1` PASS, 편집한 PS 4개 AST OK, `schema.json` JSON 파싱 OK. 새 픽스처가 **"레벨업 배경은 여전히 legacy 규칙에 매치되지만 기본 모드에서는 작동하지 않는다"**를 소스 표현식으로 고정한다(주장이 아니라 가드).
- **미검증 / 남은 위험(정직)**: ⓐ **착용 미검증** — 레벨업 검은 배경이 실제로 돌아오는지, 정면 HUD 사각형이 여전히 없는지는 사람만 답할 수 있다. ⓑ `authored`에서 레벨업 배경이 alpha 0.80 **검은 벽**으로 부담스러우면 설정 한 줄로 `dim`(D-096 중간값)으로 내릴 수 있다 — 판정은 착용 후. ⓒ `dim`/`hide` 모드는 이번 변경으로 **기본 경로에서 빠졌으므로** 다음 착용에서 둘 중 하나를 함께 돌리지 않으면 오프라인 가드로만 보존된다(의도된 대조군). ⓓ 인게임 HUD의 광범위 패널 휴리스틱도 함께 꺼지므로, 플레이 중 HUD에 반투명 패널이 다시 보일 수 있다(사용자가 요청한 "나머지는 살리기"의 직접 결과).

### D-156 — 준비된 팔의 활성 OpenXR 런타임 슬롯은 **사용자 승인 하에 그 한 값만 복구**한다(핀은 절대 고쳐 쓰지 않는다) (2026-10-13, env repair, 0.4e)
- **사건**: 0.4e 설치·핀까지 정상이었는데 준비 검증이 **마지막 관문**(`tests\verify-handaim-ready.ps1:95`)에서 `Runtime changed/missing.`으로 실패. 핀은 VDXR(`virtualdesktop-openxr.json`), 실제 `ActiveRuntime`은 SteamVR(`steamxr_win64.json`) — 준비 창 안에서 SteamVR이 슬롯을 가져갔다(TOOL-085). 설치 자체는 `ADDITIONS_INSTALLED 232 files`로 정상이었다.
- **결정**: ① 이 프로젝트의 모든 검증 착용(0.4a~0.4d)은 **VDXR 기준**이므로 핀을 현재 값에 맞추는 게 아니라 **머신 값을 되돌리는 것이 옳다**. 핀을 현재 값으로 덮어쓰면 `runtimeUnchanged=true`라는 증거 자체가 무의미해지므로 **어떤 경우에도 하지 않는다**. ② 레지스트리 쓰기는 **사용자 승인 없이는 하지 않는다**(기존 규칙 유지). 승인을 받으면 **단일 값만** 바꾸고 **before/after·사유·승인자·UTC·관리자 여부를 파일로 남긴다**(스크립트 `artifacts\d155-analysis\Restore-VdxrRuntime.ps1` → 영수증 `artifacts\d155-analysis\runtime-restore.json`; 대상 매니페스트 존재를 먼저 확인하고 쓰기 결과가 목표와 다르면 throw). ③ 복구 후 **재설치하지 않고 준비 검증만 재실행**한다(설치 리시트·핀·해시는 이미 고정이므로 재설치하면 증거만 새로 생겨 비교가 흐려진다).
- **결과(영수증)**: `before=...SteamVR\steamxr_win64.json` → `after=...Virtual Desktop Streamer\OpenXR\virtualdesktop-openxr.json`, `changed=true`, `elevated=true`, `2026-10-13T04:16:41Z`. 이어서 `tests\verify-handaim-ready.ps1 -Tag 0.4e -ExpectPrepareAhead -ExpectDeferredGpuWait -ExpectGameBackdropMode authored` 재실행 → `0.4e\prepare.json = READY_PREINSTALLED_NOT_LAUNCHED`(`runtimeUnchanged true`, `filesVerified 232`, `gameBackdropMode authored`, `2026-10-13T04:17:47Z`). **0.4e 태그는 그대로 유효**(재설치 없음) — 드릴은 **헤드셋을 쓸 때만** 실행한다(`run.json`이 태그를 소모한다, TOOL-085).
- **재발 방지(운영)**: 준비 **전에** SteamVR(`vrserver`/`vrmonitor`) 실행 여부를 확인하고, 실행 중이면 멈추고 사용자에게 알린다(SteamVR은 종료 후에도 슬롯을 되돌리지 않는다). 슬롯이 바뀐 채로 착용하면 세션은 VD 경로가 아니라 SteamVR로 가고 런타임이 다르므로 기존 수치와의 비교가 성립하지 않는다(드릴은 D-145에 따라 아무 등록 런타임이나 허용하되 도중 변경만 FAIL 처리하므로 이 상황을 스스로 막지 못한다).


### D-157 — 월드 앵커 아이콘: 상자·포션·보스상자를 **세계 좌표에** 찍고(벽 통과) 시야 밖은 **시선 링 화살표**로 (2026-10-13, **code+offline verified, headset pending**)
- **사용자 요구**: D-155로 배경은 되살렸지만 사용자의 실제 불만은 **"UI가 정면 사각형 안에 갇혀 있다"** 이다 → 답은 UI를 더 앞에 붙이는 것이 아니라 **찾아야 할 것(상자·포션·보스상자)을 실제 세계 위치에 표시**하는 것. 시야 밖이면 **시선 주변 링에 방향 화살표**.
- **증거(메타데이터, 추측 아님)**: 이번 빌드의 interop 어셈블리를 `src\diagnostics` Recon로 재스캔 → `artifacts\d157-recon\`(type-names.txt, pickup/, poi/, enums/; `Assembly-CSharp.dll` sha256 `0494F1BA…`). 확정한 정본:
  - `PoolingSystem.active_pickups: List<PickUp>` — 게임 자신의 **살아있는 픽업 레지스트리**.
  - `PickUp`: `t`(Transform), `type`(**`Pickup_Type`**), `amount`, `relic_item`, `powerup_type`, `mini_map_target`/`boss_chest_minimap_target`(Target).
  - `RewardBox`: 상자/보스상자(플레이어가 눌러 보상을 받는 오브젝트; `start_reward`, `range`). **`Pickup_Type`에 없는 별개 타입**.
  - `Pickup_Type` 정확한 멤버 14개: `XP, GOLD, RELICS, XP_5, LEVEL_UP, HEALING, XP_10, XP_2, SOUL_PURIFICATION, BOSS_CHEST, SCRAP, DEMONIC_ENERGY, POWER_UP, APPLE_WITCH` → 파일 `artifacts\d157-recon\enums\assembly-recon.json`.
  - 참고(이번 미사용): `PointsOfInterest`/`PointOfInterest`는 아이콘이 아니라 **POI 스폰 스케줄러**(frequency/pickup_type/poi_events)이고, `Target`/`Target_Type`/`MiniMapTargets`는 **미니맵 아이콘 시스템**이다. `Target_Type`은 `MINI_BOSS, HEALTH_POINT, LEVEL_UP_CHEST, GHOST, ANK, PORTAL, MINI_BOSS_CHEST, SHINMEN` — 즉 게임은 상자 위치를 이미 미니맵 타깃으로 들고 있다(다음 세션의 후속 소스 후보).
- **설계 — 종류 토큰 공간은 게임 메타데이터 그 자체**: 순수 정책 `WorldMarkerPolicy`(`src\interaction\HandAim.cs`).
  - 토큰 = **이 빌드의 정확한 `Pickup_Type` 멤버 이름**(14개, 대소문자 구분) **+ 예약 토큰 `CHEST`**(= `RewardBox`; 픽업 enum이 덮지 않으므로 별도 토큰). **추측한 카테고리 이름을 만들지 않는다.**
  - 기본값 `CHEST,HEALING,POWER_UP,BOSS_CHEST` = 사용자가 말한 **상자·포션(회복/파워업)·보스상자**.
  - 알 수 없는 토큰/빈 목록은 **추측하지 않는다**: `OpenXrHost`가 Initialize에서 throw.
  - 수치: 거리 5~200 m(기본 40), 개수 1~32(기본 12) — 비유한/범위 밖은 호스트가 거부.
  - 표시 크기는 **각도식**(0.05×거리, 0.08~1.2 m로 클램프), 대상 원점 위 0.35 m.
  - **상한 랭킹**: 시야 안 > 시야 밖 → 보스상자 > 상자 > 나머지 → 가까운 순. 보던 것이 먼저 버려지지 않는다.
  - **시선 링**: 시선축과 40° 초과면 `ClampToVisor`가 **정확히 40°**가 되도록 `cos(max)·(0,0,1) + sin(max)·접선`으로 투영(근사 아님) — 좌우 부호 유지, 정후방(접선 0)은 +Y 사용. 링은 시선 앞 2 m.
- **구현 지점(정확한 위치)**: ⓐ `HandAim.cs` `WorldMarkerPolicy`(모드·토큰·파싱·클램프·크기·랭킹·각도/링 수학) ⓑ 신규 `src\interaction\HandMarkers.cs`(`HandAimDriver` partial — 스캔·선별·배치·수명·영수증) ⓒ `HandAimDriver.cs` `MarkersTick(source)`(Tick), `MarkersRelease()`(Release), 영수증 `worldMarkers = MarkerReceipt()` ⓓ `HandUi.cs` 훅: `InstallUiRender` 가드 `if (!UiEnabled && !MarkersEnabled) return;`, `UiBeforeEye`(게임 카메라/양안만) `driver.MarkersPlace()`, `UiAfterLayout` `UiRenderPlace(); MarkersPlace();` ⓔ `OpenXrHost.cs` 키 4개 + 검증 4줄 + 배선 + `WORLD_MARKERS mode=` 로그 + `host.json`의 `worldMarkers/markerKinds/markerMaxDistance/markerMaxCount` ⓕ `config\schema.json`(4키, `x-host-tunables`) ⓖ `dist\preview\install.ps1`(4키) ⓗ `tests\headset-handaim.ps1`(`-WorldMarkers/-MarkerKinds/-MarkerMaxDistance/-MarkerMaxCount`) ⓘ `tests\verify-handaim-ready.ps1`(`Expect*` + 키 개수 검사 4개 추가) ⓙ `src\core\VampireHuntersVR.Stage0.csproj`(새 파일 Compile 등록).
- **읽기 전용 계약(명시)**: 게임 오브젝트에서 읽는 것은 **`PickUp.t`.position, `PickUp.type` 이름, `RewardBox.gameObject.activeInHierarchy`, `box.transform.position`** 뿐이다. 게임의 픽업/상자/아이콘/캔버스/머티리얼/트랜스폼에 **쓰지 않는다**(`MarkersPlace`가 쓰는 트랜스폼은 모드가 만든 쿼드뿐). 소스 가드가 이를 소스 표현식으로 고정한다.
- **벽 통과 = 기존 레시피 재사용**: D-095 `HandReticle.EnsureReticleMaterial()`(= `_ZTest Always`, `_ZWrite 0`, `renderQueue 5000`)을 복제하고 `_Cull Off`만 추가. 새 셰이더·게임 머티리얼 조작 없음. 모드 소유 오브젝트(`VHVR_Marker_i`, `PrimitiveType.Quad`)는 **콜라이더 비활성**(게임 물리/레이캐스트에 끼어들지 않음)·그림자 Off·Release에서 파괴.
- **프레이밍/문구(정직)**: 아이콘은 **색 쿼드 자리표시자**이지 게임 아트가 아니다(스프라이트 UV/아틀라스는 후속). 링 화살표는 같은 쿼드를 어둡게(0.6×)·고정 크기로 그린다 — "저기를 봐라"이지 "이미 보인다"가 아니다.
- **검증(오프라인)**: 플러그인 Release **경고 0·오류 0**, HandAim **263 → 287**(신규 24 = 순수 정책 16 + 소스 가드 8: 게임 레지스트리 사용·읽기 전용·벽 통과 머티리얼·양안 전 배치 훅·Tick/Release/영수증·호스트 검증/영수증·드릴/준비/schema/설치 4키·csproj 등록), `schema.json` JSON 파싱 OK, 편집한 PS 4개 AST OK, `tests\verify.ps1` 전량.
- **미검증 / 남은 위험(정직)**: ⓐ **착용 미검증** — 상자/포션이 실제로 보이는지, 벽 뒤에서 보이는지, 링 화살표가 방향을 맞게 가리키는지는 사람만 답할 수 있다. ⓑ `RewardBox`가 **일반 상자와 보스상자를 구분하지 않는다** — CHEST 토큰은 둘 다 표시한다(구분은 `Pickup_Type.BOSS_CHEST` 픽업과 향후 `Target_Type.MINI_BOSS_CHEST`로 가능). ⓒ 미니맵 카메라도 쿼드를 렌더할 수 있다(깊이 무시·queue 5000) — 미니맵 노이즈가 보이면 배치 훅 카메라 필터를 확장해야 한다. ⓓ `active_pickups`가 상자를 담는 비율은 실기 로그로만 확인된다(영수증 `livePickups/liveChests/candidates/shown/offGaze/sample`가 그 답을 남긴다). ⓔ **TOOL-074가 열려 있다**: 이 런타임에서 `Camera.FireOnPreCull` 포스트픽스가 호출되지 않았으므로(pre-cull 행 0) `UiBeforeEye` 경로의 배치가 안 돌 수 있다 — 그 경우에도 `Tick`(`LateUpdate` 계열)과 `Canvas.SendWillRenderCanvases`(`UiAfterLayout`) 두 경로가 남아 아이콘은 갱신되지만, 최대 한 프레임 늦거나 눈 렌더 직전의 재배치가 없을 수 있다. 이 위험은 D-155와 공유하는 기존 조건이고, 걷어내려면 별도 세션에서 pre-cull 호출 여부를 다시 재야 한다.
- **준비(0.4f) 완료 (D-157b)**: 래퍼 `artifacts\d157-analysis\Prepare.ps1`(D-155 래퍼 + 새 키 4개를 config/pins/준비 기대값/arm/PLAYTEST에 반영, AST OK)로 트랜잭션 설치 → `install.exit.txt=0`, `ADDITIONS_INSTALLED 232 files`, `verify.exit.txt=0`, **`0.4f\prepare.json = READY_PREINSTALLED_NOT_LAUNCHED`**(`runtimeUnchanged true`, `gameLaunched false`, `2026-10-13T05:53:09Z`). 핀: plugin `08BC13B1…`, native `F7FDF2D6…`, config `CD4088D3…`, `runtimeBefore` = **VDXR** → **0.4a~0.4d와 비교 가능한 세션**이다(0.4e의 SteamVR 수치와는 비교하지 않는다). 배포 cfg에도 4키가 들어간 것을 확인했다. 태그는 드릴이 소모하므로 **헤드셋을 쓸 때만** 실행한다(TOOL-085).

### D-158 — 월드 아이콘 **소스 교정**: 게임 자신의 POI 화살표가 들고 있는 `TargetHUDArrow.target` + `HealthPoints.health_points_object` (2026-10-13, **code+offline verified, headset pending**)
- **사용자 지시 3건(최종)**: ① **TOOL-084는 닫는다 — 해결됐으니 더 건들지 않는다** ② **데미지/적 화살표는 숨기지 않는다** ③ **평면 화면식 "보물상자 표시"는 답이 아니다** — 원하는 것은 **VR 입체 화면에서 실제 상자·포션이 있는 월드 위치에 그 자리라고 표시**해 찾기 쉽게 만드는 것.
- **D-157 소스가 틀렸음이 실착용에서 측정됐다(0.4f, VDXR, 17,978 프레임, `endReason=level-exit`, stock 검증 통과)**: `hand-aim.json.worldMarkers`가 세션 내내 `scans=479, livePickups=1, liveChests=0, candidates=0, shown=0, error=null`. 마커 패스는 정상(셰이더 확보, 예외 0)이었고 **소스가 비어 있었다** → 추측으로 덮지 않고 소스를 바꾼다.
- **"사각 틀에 갇힌 아이콘"의 정체도 같은 리시트가 밝혔다**: `ui.hudDarkCandidates` 80개(상태 `79 kept / 1 HIDDEN`) 중 **`Canvas - FloatingIcons/POIArrowsParent/POIArrowHud(Clone)` 20개** + `Canvas - FloatingIcons/DemageArrowsParent/ArrowHud(Clone)` 5개. `Canvas - FloatingIcons`는 `HudLayoutPolicy`가 머리 고정으로 잡는 5개 캔버스 중 하나이고 D-090/D-102가 정면 WorldSpace 사각형으로 바꾼다 → **게임이 원래 "화면 가장자리 화살표"로 알려주던 POI 정보가 그 틀 안에 갇힌다**.
- **정본 재스캔이 정답 소스를 줬다(`artifacts\d157-recon\arrows\assembly-recon.json`, `Assembly-CSharp.dll` sha256 `0494F1BA…`)**: `TargetHUDArrow`(MonoBehaviour)의 **`target: UnityEngine.Transform`**(화살표가 가리키는 월드 대상) + `distance`/`angle`/`rect_transform`; `PointsOfInterest`의 `arrows_on_hud: POIArrow[]`·`poi_spawns`·`all_points_of_interest`·`hud_arrow_prefab`·`poi_parent`; `POIArrow`(struct)의 `pick_up: PickUp`·`arrow_transform`; `HealthPoints`의 **`health_points_object: Transform[]`**(회복 지점) + `health_points`·`current_pickup`; `PickUp.mini_map_target`/`boss_chest_minimap_target`; `Target_Type`(`LEVEL_UP_CHEST`, `MINI_BOSS_CHEST`, `HEALTH_POINT`) + `MiniMapTargets`. (구조체 배열 색인보다 MonoBehaviour 참조인 `TargetHUDArrow`/`HealthPoints`가 안전해 그 둘을 기반으로 삼았다.)
- **설계(읽기 전용, 소스 4개)**: ⓐ `HealthPoints.health_points_object` → 종류 `HEALING`(사용자가 말한 "포션"의 정본) ⓑ **`TargetHUDArrow.target`** — 경로가 `POIArrows`인 화살표만 읽고, 대상의 정체는 **게임 타입으로만** 판정: `GetComponentInParent<PickUp>()` → 그 `type` 이름 / `GetComponentInParent<RewardBox>()` → `CHEST` / 방금 읽은 health 지점 id → `HEALING` / 그 외 → **예약 토큰 `POI`**(기본값에 없음 → 이름 없는 대상이 기본 마커에 끼어들 수 없다) ⓒ `PoolingSystem.active_pickups`(0.4f에서 거의 비어 있었으므로 **보조**) ⓓ `RewardBox` → `CHEST`.
- **데미지 화살표 취급(사용자 요구, 소스로 고정)**: 경로에 `DemageArrows`(게임 표기) 또는 `DamageArrows`가 있으면 **읽지 않고 즉시 continue**(카운터 `MarkerArrowDamage`로만 기록) — 그리고 이 파일은 게임 오브젝트/UI 렌더러를 **어떤 방식으로도 숨기지 않는다**(소스 가드: `canvasRenderer`·게임 오브젝트 `SetActive` 사용 없음, 모드 소유 쿼드만 토글).
- **영수증 확장(다음 착용에서 원인 판별 가능)**: `worldMarkers`에 `liveHealthPoints`, `liveArrows`, `arrowDamageIgnored`, `arrowOtherIgnored`, `arrowNamedKinds`, `arrowUnnamedKinds`, `arrowSample`(종류@경로 최대 12개) 추가 + note에 소스와 데미지 화살표 규칙 명시.
- **검증(오프라인)**: 플러그인 Release **경고 0·오류 0**, HandAim **287 → 291**(신규 4 = POI 토큰 정책 1 + 소스/무해 가드 3), `schema.json`/`README-KO`에 POI 예약 토큰·소스 설명 반영, 전체 검증기 재실행.
- **미검증(정직)**: **착용 미검증** — 상자·포션 아이콘이 실제 위치에 뜨는지, 몸을 돌리면 세계에 고정되는지는 사람만 답할 수 있다(**Q-D157-2**). `POIArrowsParent` 경로에 실제로 상자/포션 화살표가 몇 개 붙는지는 이번 착용의 `liveArrows`/`arrowSample`이 답한다. `PointsOfInterest.arrows_on_hud`/`POIArrow.pick_up`(구조체 배열)은 **이번엔 읽지 않았다**(안전한 MonoBehaviour 경로 우선) — `arrowSample`이 부족하면 다음 후보. 아이콘은 여전히 **색 쿼드 자리표시자**다.
- **0.4g 착용 결과(D-158b, VDXR, 13,037 프레임, `endReason=level-exit`, `stockVerified=True`, plugin `D6F24C77…` 핀 일치, `runtimeBefore==runtimeAfter`, error/cleanupError 없음)**: **포션 아이콘이 실제로 그려졌다** — `worldMarkers: scans=347, liveHealthPoints=6, candidates=2, shown=2, quads=3, error=null`, `sample=HEALING@world HEALING@ring`. 즉 ⓐ `HealthPoints.health_points_object`가 **회복 지점 6개**를 정확히 줬고 ⓑ 시선 밖일 때 `@ring`(시선 링), 시선 안일 때 `@world`(**포션의 실제 월드 좌표**)로 전환되는 것이 리시트로 확인됐다(D-157의 `livePickups=1/candidates=0`과 대비). 배경 정책도 안정(`backdropHidden=0`, `dimmed=0`, `hudDarkHidden=1`). **아직 검증 못 한 것**: `liveChests=0`·`livePickups=1`(이 짧은 세션에서 `RewardBox`가 활성화되지 않음) → **상자/보스상자 아이콘 미검증**, `liveArrows=0`·`arrowDamageIgnored=0`(POI/데미지 화살표가 활성 시점이 아니었음) → `TargetHUDArrow` 소스도 미검증(0.4f에서는 20+5개가 활성이었다). 다음 착용은 **상자/게이트가 활성인 구간을 포함**해야 하며, 판정은 Q-D157-2(사람) + 리시트 `liveChests/liveArrows/arrowSample/candidates/shown`.







### D-165 — 0.4j 실착용 판정: **벽 통과는 복구됐지만 배치·소스가 틀렸다** — 보관함은 `Gargoyle`이고 **걸린 포션은 PickUp이 아니다** (2026-10-13, code+offline verified, headset pending)
- **사용자 판정(0.4j, 정확)**: "포션이 걸린 가고일 석상을 **제외한** 모든 가고일 석상에 화살표가 있고, 정작 **포션이 걸린 석상과 포션 자체에는 화살표가 없다**. **보물상자는 있던 화살표가 사라졌다**. 원하는 것은 **보물상자와 체력포션의 위치가 입체화면에 표시**되고 **벽 뒤에서도** 보이며 **어떤 거리에 있든** 보이는 것."
- **0.4j 리시트가 남긴 사실**: `shader=Hidden/Internal-Colored`, **`zTestForced=True`**, `blendForced=True` → **벽 통과는 복구됐다**(D-163의 셰이더 속성 선택이 작동). `shown=7`(가고일 6 + 상자 1), `statues=6`, `statuesWithoutPotion=6`, **`potionObjects=0`**, `currentPickup=`(세션 내내 비어 있음), `pickupSample`에 `HEALING@off ×5`. `statueSample`: 보관함의 정체는 **`Gargoyle`**(자식 7개, **`pickup=n`**, 렌더러 bounds top **8.02 m**, `Gargoyle (5)`만 r=4·top=75.94).
- **원인 규명(두 개, 둘 다 데이터로 확정)**:
  - ⓐ **걸린 포션은 `PickUp` 오브젝트가 아니다.** 세션 내내 보관함에 `pickup=n`이고 레벨의 `HEALING` 픽업은 전부 `@off`였다 → 포션은 **가고일의 자식 모델**이며, "살아있는 PickUp"을 기다리는 소스로는 원리적으로 잡히지 않는다. (그래서 0.4g/0.4h의 `current_pickup` 한 건이 유일한 관측이었다.)
  - ⓑ **빈 가고일만 표시된 이유**: D-163의 bounds 배치가 아니라, 포션 관련 소스가 `PickUp.t`(풀 내부 좌표)를 위치로 써서 **화살표가 화면 밖으로 빠졌다**. 그리고 **bounds 배치**가 가고일(8 m)·상자 모델의 큰 bounds를 그대로 써서 **상자 화살표를 화면 밖으로 날려버렸다**(사용자가 본 "상자 화살표 실종").
- **수정**:
  - ⓐ **배치를 고정 오프셋(0.35 m)으로 되돌림** — bounds 폐기(0.4i에서 사용자가 만족했던 상자 화살표 동작 복원). `MarkerWorldOf`는 이제 `target.position + up*HeightOffset` 한 줄이다.
  - ⓑ **포션 위치 = 포션 오브젝트 자신**(`potion.transform`). `PickUp.t`는 배치에 **절대 쓰지 않는다**(소스 가드로 고정). 보관함에서 `PotionHolderSlackMeters = 4 m`보다 멀리 있는 포션 참조는 **풀 아티팩트로 판정**하고 **보관함 위치**를 사용한다 → **포션이 걸린 가고일도 화살표를 얻는다**(사용자 불만의 직접 원인).
  - ⓒ **진단**: `statueSample`에 **자식 이름/활성/보관함으로부터의 거리**(`kids[...]`), `chestSample`(상자 소스의 종류·이름·실제 좌표·렌더러 수), `potionSample`(보관함에서 멀리 떨어진 포션 참조 기록) 추가.
- **정직(다음 단계)**: 다음 착용의 `kids[...]`가 **포션 모델 자식의 이름·위치**를 알려주면, 그 자식만 정확히 표시하고 **빈 가고일 노이즈를 제거**할 수 있다(지금은 빈 보관함도 HEALING으로 표시된다). 거리 무제한(`MarkerMaxDistance=0`)·반투명 화살표·벽 통과는 그대로다.
- **검증(오프라인)**: 플러그인 Release **0/0**, HandAim **305**(픽스처 2개를 새 설계로 갱신: 배치 되돌림, 포션 앵커), 전체 검증기 재실행. **0.4j 세션은 정상 종료**(`endReason=level-exit`, `stockVerified=True` 121파일/7,726,223,158 B, plugin `62A5B916…` 핀 일치).

### D-164 — 운영 규칙(사용자 지시): **세션은 준비(READY)까지만, 실행은 사용자가 명령할 때만** (2026-10-13, process)
- **사용자 지시**: "다음부턴 세션 준비까지 하고, **실행은 내가 명령하면** 한다. **마음대로 대기시키지 마**."
- **규칙**: 착용/실행(`tests\headset-handaim.ps1 ...`)은 **사용자가 명령할 때만** 시작한다. 준비(`Prepare.ps1 -Tag ...`)는 게이트(clean tree / 검증기 32 / 픽스처 수 / 런타임 VDXR / SteamVR·게임 미실행)를 통과하면 진행하고, 끝나면 **READY 상태 + 실행 명령**을 보고한다. 헤드셋 대기 루프를 내가 임의로 돌리지 않는다.
- **0.4j 정리**: 위 지시 직후 대기 중이던 0.4j 세션(pid 71084)을 **종료**했다. 페이로드는 그대로 설치되어 있어(게임 폴더 `BepInEx\plugins\VampireHuntersVR.Stage0.dll` 366,080 B / `vhvr-openxr-host.json` 1221 B, `prepare.json = READY_PREINSTALLED_NOT_LAUNCHED`, plugin `62A5B916…`, 232 파일 검증, 런타임 VDXR 유지) **사용자가 실행 명령만 치면 바로 착용 세션**이 된다. 다만 스크립트 특성상 **최종 원복(스톡 검증)은 사용자가 실행한 세션이 끝날 때** 일어난다.
- **스크립트 발견(정직)**: `stop.request`는 **게임이 실행된 뒤 감시 루프에서만** 읽힌다 → 헤드셋 대기 중에는 그 파일로 멈출 수 없다(그래서 프로세스를 종료했다). 필요하면 대기 루프에서도 읽도록 고칠 수 있다(요청 시).

### D-163 — 회귀 수정(벽 통과) + 보관함에 걸린 포션 표시 복원 + 화살표를 **보이는 모델 바로 위**로 (2026-10-13, **code+offline verified, headset pending**)
- **사용자 피드백(0.4i 직후, 최종)**: ① "**포션은 있었다. 벽에 걸린채 있었을 뿐이지**"(표시가 안 됐다는 것) ② "**벽 뒤에선 안보여**" ③ "상자 화살표는 상자 바로 위라 좋은데 **포션은 좀 위에 있어서, 포션 바로 위로** 구현해달라. 그리고 **벽 뒤에서도 거리 넘어서도 보이게 하는거 잊지말고**".
- **② 벽 통과 회귀 — 원인을 리시트로 확정**: `shader` 필드가 **0.4g/0.4h = `Hidden/Internal-Colored`**(`_ZTest` 속성 보유 → 깊이 테스트를 껐음 → 벽 통과 성공)인데 **0.4i = `Sprites/Default`**(`_ZTest` 속성 없음 → 깊이 테스트가 그대로 → **벽에 가려짐**)였다. D-161에서 "투명 셰이더 우선"으로 순서를 바꾼 것이 원인이며, **내가 만든 회귀**다. **수정**: 셰이더를 이름 순서가 아니라 **속성으로 선택**한다 — 후보(`Hidden/Internal-Colored`, `UI/Default`, `Sprites/Default`)를 프로브 재질로 검사해 **`_ZTest`를 실제로 가진 것을 우선**하고, 영수증에 `zTestForced`/`blendForced`를 기록하며, 못 끄면 `MarkerError`로 **조용히 넘어가지 않는다**.
- **① 보관함에 걸린 포션 — D-159 결정을 사용자 요구로 되돌림**: 0.4i의 포션 소스는 "살아있는 `PickUp`"만 봤는데, 그 세션에서 `HEALING@off ×5`처럼 **전부 비활성**이었다(보관함에 걸린 포션은 PickUp이 아니거나 비활성). 정본 `HealthPoints`에는 `health_points_object: Transform[]`(보관함) + `current_pickup: GameObject`(포션) + `random_start_potion`만 있고 **별도 potion 타입이 없다** → **보관함 자체가 "걸린 포션"의 정본 위치**다. 따라서 **살아있는 포션 픽업이 없는 보관함을 `HEALING`으로 표시**하고(렌더러 bounds 바로 위), 살아있는 포션이 있으면 그 픽업을 표시한다. D-159의 "석상은 표시하지 않는다"는 결정은 **사용자가 포션이 걸려 있는 것을 보고 되돌려 달라고 했으므로 폐기**한다(이유 기록).
- **③ 화살표를 보이는 모델 바로 위로**: 마커 위치를 `position + 0.35 m` 고정에서 **대상 렌더러들의 bounds 상단 + 0.12 m**(`WorldMarkerPolicy.BoundsClearance`)로 바꿨다(`MarkerWorldOf`). 상자는 이미 만족했고, 포션은 이제 포션 모델 바로 위에 붙는다. 렌더러가 없는 대상(게임 미니맵 타깃 등)만 기존 고정 오프셋을 유지한다. 거리 무제한(D-161)은 그대로다.
- **중복 화살표 방지**: 보관함과 그 위의 살아있는 포션은 **같은 자리**다 → 위치 기반 중복 제거(`WorldMarkerPolicy.IsSameSpot`, 0.5 m)로 **한 자리에 화살표 하나**만 그린다(`positionDuplicatesDropped`로 기록).
- **진단 강화**: `statueSample`(보관함 이름/자식수/활성 렌더러수/**모델 상단 높이**/pickup 유무), `currentPickup`(이름/activeSelf/activeInHierarchy/pickup 유무), `pickupSample`에 `@self`(activeSelf만 true) 추가, `boundsPlaced`, `positionDuplicatesDropped` → 다음 착용이 "화살표가 왜 높거나 없었는지"를 데이터로 설명한다.
- **검증(오프라인)**: 플러그인 Release **0/0**, HandAim **301 → 305**(신규 4 = 벽 통과 셰이더 선택 / bounds 배치 / 위치 중복 제거 / 보관함 진단), 픽스처 2개를 새 설계에 맞게 갱신(포션 규칙 반전, 픽업 활성상태 샘플), 전체 검증기 재실행.
- **미검증(정직)**: **착용 미검증(0.4j)** — ⓐ 벽 뒤에서 화살표가 보이는지(`zTestForced`가 true인가) ⓑ 포션 화살표가 **포션 바로 위**인지(`boundsPlaced`/`statueSample`의 top) ⓒ 보관함에 걸린 포션이 표시되는지 ⓓ 같은 자리에 화살표가 겹치지 않는지. 데미지/적 화살표는 계속 읽지도 숨기지도 않는다.

### D-162 — 0.4i 착용: **상자 화살표 동작 확인**, 거리 무제한·화살표·반투명이 리시트에 반영, **포션은 이 레벨에서 스폰되지 않아 미검증** (2026-10-13, headset partial)
- **0.4i 착용(VDXR, `endReason=level-exit`, `stockVerified=True` 121파일/7,726,223,158 B, plugin `E50EBEE3…` 핀 일치, `runtimeBefore==runtimeAfter`, error/cleanupError 없음, 약 3.7분)**: `worldMarkers: scans=424, pickupObjects=47, statues=6, statuesWithoutPotion=6, potionObjects=0, chestTargets=0(순간 1), liveChests=0, liveArrows=0, arrowDamageIgnored=0, duplicateSourcesDropped=0, shape="arrow", alpha=0.5, distanceLimited=false, error=null`.
- **✅ 상자 화살표가 실제로 그려졌다(사용자 요구 #3)**: 세션 중 `chestTargets=1` + `sample=CHEST@world` + `shown=1`이 관측됐다 → **게임 자신의 미니맵 상자 타깃(`Target_Type.LEVEL_UP_CHEST`)이 활성인 동안 그 상자의 월드 위치에 CHEST 화살표**가 떴다. (이후 타깃이 비활성되며 0으로 돌아갔다 — 게임이 상자 타깃을 등록하는 동안만 뜬다는 뜻으로, 소스의 성질이지 결함이 아니다.)
- **✅ D-161의 3요소가 리시트로 확인**: `distanceLimited=false`(거리 무제한 적용), `shape="arrow"`, `alpha=0.5`. `duplicateSourcesDropped=0`(중복 0 = D-160 유지).
- **확대한 소스가 정확한 그림을 줬다**: `pickupObjects=47`, `pickupSample = APPLE_WITCH@off, DEMONIC_ENERGY@off, SCRAP@off, BOSS_CHEST@off, SOUL_PURIFICATION@off, HEALING@off ×5, LEVEL_UP@off ×2 …` → 레벨에 **HEALING 포션 5개와 BOSS_CHEST가 풀에 존재하되 전부 비활성**이었고, 세션 중 **한 번도 `@live`가 되지 않았다**. 같은 이유로 `statuesWithoutPotion=6`(석상 6개 전부 포션 없음)이 세션 내내 유지됐다.
- **미검증(정직)**: **석상에 붙은 포션 표시(사용자 요구 #1)는 이번에도 확인 못 했다** — 이 레벨에서 포션이 스폰되지 않아 `HEALING@live`가 한 번도 없었다. 즉 소스는 준비됐지만(모든 살아있는 `PickUp` 추적) **관측 기회가 없었다**. 다음 착용은 **반드시 포션이 실제로 나오는 상황**(석상 근처에서 적 처치 또는 석상 상호작용)을 포함해야 하며, `pickupSample`에 `HEALING@live`가 뜨는지가 판정 근거다. 데미지/적 화살표는 이 레벨에서 비활성(`arrowDamageIgnored=0`)이라 이번에도 건드릴 대상이 없었다.
- **다음**: 포션이 나오는 구간을 포함한 0.4j 착용 + 사람 판정(반투명 화살표 가독성/방향, 상자·포션 위치).

### D-161 — 사용자 3요구 반영: **석상에 붙은 포션도 표시** + **거리 무제한** + **반투명 화살표** (2026-10-13, **code+offline verified, headset pending**)
- **사용자 요구(0.4h 착용 직후, 최종)**: ① "상자도 포션도 나오지만 **석상에 붙어있는 포션은 표시가 안 되었다. 이것도 표시되게 수정**해달라" ② "포션과 상자 모두 **거리에 상관없이** 표시" ③ "**반투명한 화살표**로 변경".
- **① 원인과 수정**: 0.4h에서 `statuesWithoutPotion=6`(석상 하위 `PickUp` 없음)이면서 포션은 `current_pickup`으로만 1개 잡혔다 → **석상에 달린 포션이 석상의 자식 오브젝트라는 보장이 없다**(풀링된 픽업이 다른 부모/위치에 있음). 그래서 포션 소스를 **씬의 모든 살아있는 `PickUp`**(`Resources.FindObjectsOfTypeAll<PickUp>()`)으로 확대했다. 필터는 여전히 **타입 + active 여부뿐**이고, `AddSource`가 중복을 제거한다. 영수증에 `livePickupObjects`(살아있는 픽업 수) + `pickupSample`(`종류@live`/`@off` 최대 12)을 추가해 다음 착용이 "포션이 몇 개, 어떤 타입으로 존재하는가"를 데이터로 답한다.
- **② 거리 무제한**: `MarkerMaxDistance` 기본값을 **0 = 제한 없음**으로 바꿨다(순수 함수 `IsDistanceLimited`/`WithinDistance`; 비유한 값도 무제한). 5~200 사이 양수면 그 거리까지, 0도 아니고 5..200 밖이면 **호스트가 시작을 거부**한다(조용한 클램프 없음). 개수는 기본 32·최대 64로 올려 레벨 전체를 덮고, 캡은 폭주 방지 안전장치로만 남긴다.
- **③ 반투명 화살표**: 쿼드를 버리고 **7정점 3삼각형 화살표 메시**를 쓴다(기하 데이터는 `WorldMarkerPolicy.ArrowVertices`/`ArrowTriangles` 순수 데이터 → 오프라인 픽스처로 검증, 드라이버는 Mesh에 넣기만 함). 재질은 **투명 셰이더 우선**(`Sprites/Default` → `UI/Default` → `Hidden/Internal-Colored`)이고 `_SrcBlend=SrcAlpha`/`_DstBlend=OneMinusSrcAlpha`를 명시 설정하며, D-095/D-161의 **벽 통과 레시피**(`_ZTest Always`, `_ZWrite 0`, `_Cull Off`, `renderQueue 5000`)를 유지한다 → **벽 뒤에서도 보이는 반투명 화살표**. 알파는 0.5, 링 화살표는 0.35(0.4g/0.4h의 불투명 쿼드를 대체).
- **화살표 방향(설계)**: 화살표는 항상 **머리를 향해 정렬**(뒤집힘/거울상 방지). **시야 안**에서는 끝이 **대상 아래로** 향하게 180° 롤(정확히 그 물체를 가리킴), **시야 밖**에서는 링 위에서 **바깥쪽 = 돌아야 할 방향**을 가리킨다(`ClampToVisor`의 접선부가 곧 대상 방향).
- **정본 스키마 함정(측정)**: `config/schema.json` 편집 중 **옛 `MarkerMaxCount` 키가 중복으로 남아 있었고, PowerShell `ConvertFrom-Json`이 중복 키를 조용히 "마지막 값(=12)"으로 읽었다** → 즉 스키마만 보고는 거짓 기본값을 읽을 수 있었다. 중복 키를 제거하고 파싱을 재확인했다(마커 키는 `x-diagnostic-features` 18키 안에 각각 1회).
- **오프라인 검증**: 플러그인 Release **경고 0·오류 0**, HandAim **295 → 301**(신규 6 = 화살표 기하 1, 거리 무제한 1, 개수 기본 1, 픽업 확대 1, 방향 1, 반투명 재질 1), `schema.json` 파싱 OK, 전체 검증기 재실행. 기존 `marker-count-clamp` 픽스처가 **의도대로 실패**해서 새 범위로 갱신(가드가 살아 있었음).
- **미검증(정직)**: **착용 미검증(0.4i 예정)** — ⓐ 석상에 붙은 포션까지 전부 표시되는지 ⓑ 거리 무관 전부 표시되는지 ⓒ 반투명 화살표가 읽히는지/방향이 맞는지 ⓓ 상자 아이콘(여전히 상자 소스가 활성인 구간 필요). 판정은 사람 + `livePickupObjects/pickupSample/statues/statuesWithoutPotion/chestTargets/candidates/shown`.

### D-160 — 표적당 쿼드 1개(중복 제거) + 0.4h 착용 결과: **석상에는 아이콘이 안 붙음이 확인됐고, 상자 소스는 아직 비활성** (2026-10-13, code+offline verified, headset partial)
- **0.4h 착용(VDXR, `endReason=level-exit`, `stockVerified=True`, plugin `ABB20ACF…` 핀 일치, error/cleanupError 없음)**: `worldMarkers: scans=207, statues=6, statuesWithoutPotion=6, potionObjects=1, livePickups=1, liveChests=0, chestTargets=0, liveArrows=0, arrowDamageIgnored=0, candidates=2, shown=2, quads=3`.
- **사용자 요구가 기계적으로 충족됨**: 그 세션 내내 **석상 6개가 모두 "포션 없음"(`statuesWithoutPotion=6`)** 이었고 그 상태에서 아이콘이 **0개**였다 → **석상(스폰 설비)에는 아이콘이 붙지 않는다**. 그리고 포션이 생기자 **`HealthPoints.current_pickup` 경로가 그 포션을 잡아(`potionObjects=1`) `HEALING@world`/`@ring` 마커를 그림** → 아이콘이 **포션 자체**에 붙는다.
- **발견한 결함(정직)**: 포션 1개인데 `shown=2` — 같은 포션을 **`current_pickup`과 `PoolingSystem.active_pickups` 두 소스가 각각 잡아 중복**했다(겹쳐 그려져 시각적으로는 하나). → **D-160 수정**: `AddSource(Transform, kind)` 한 곳에서 **대상의 `GetInstanceID()`로 중복을 제거**하고, 버린 중복 수를 영수증 `duplicateSourcesDropped`로 노출. 소스 추가는 전부 `AddSource`를 통과한다(소스 가드: 직접 `markerSources.Add`는 `AddSource` 내부 1곳뿐).
- **아직 미검증**: `liveChests=0`·`chestTargets=0` → **이 레벨에서는 상자/RewardBox/미니맵 상자 타깃이 한 번도 활성화되지 않았다** → **상자 아이콘은 여전히 실착용 미검증**(D-159에서 소스는 3개로 늘려 두었다). `liveArrows=0`/`dmg=0` → POI·데미지 화살표도 이 레벨에서 비활성(0.4f에서만 20+5개가 활성이었다) → `TargetHUDArrow` 경로도 이번엔 미검증.
- **검증(오프라인)**: 플러그인 Release **0/0**, HandAim **294 → 295**(중복 제거 가드 1), 전체 검증기 재실행.
- **다음**: 상자/게이트가 있는 구간(더 큰 레벨 또는 레벨업/보스 상자가 나오는 진행)을 포함한 착용에서 `chestTargets`/`liveChests`/`liveArrows`가 0을 벗어나는지 확인. 아이콘이 **포션 위에 있고 중복이 없는지**는 사람+`shown`으로 판정.

### D-159 — 아이콘 앵커 교정: **석상이 아니라 "실제 포션 픽업"** + 상자 아이콘 소스 추가(게임 자신의 미니맵 타깃) (2026-10-13, **code+offline verified, headset pending**)
- **사용자 피드백(0.4g 착용 직후, 최종)**: "포션은 **석상 같은 것에 달려서** 나오는데, **그 석상에 아이콘 표시가 되서 불편했다**. **포션 위치에만** 아이콘을 표시하도록. **상자 아이콘도 필요하다.**"
- **원인(코드가 아니라 앵커 선택)**: D-158이 `HealthPoints.health_points_object`(**석상 Transform**)를 그대로 마커 위치로 썼다 → 아이콘이 포션이 아니라 **스폰 설비(석상)** 에 붙었다. `HealthPoint`(struct)에는 픽업 참조가 없다(정본: `health_point_enemy: Enemy`, `health_point_t: float`, `is_active: bool` = 그 지점에 적이 서 있는지 추적하는 용도).
- **정본에서 찾은 올바른 앵커**: 실제 포션은 **석상 하위의 `PickUp`**(또는 `HealthPoints.current_pickup: UnityEngine.GameObject`)이다. 그래서 마커 위치를 **`potion.t`(픽업 자신의 Transform)** 로 바꾸고, **활성 포션이 없는 석상은 후보 0**(영수증 `statuesWithoutPotion`)으로 센다. 종류는 픽업 자신의 `PickUp.type`(HEALING/POWER_UP 등)이다.
- **석상 재발 방지(소스로 고정)**: 게임 화살표가 **석상(또는 그 하위)을 가리키면** `IsUnderStatue`가 `""`를 돌려 **마커를 만들지 않는다**(예약 토큰 `POI`도 붙지 않는다).
- **상자 아이콘 소스 추가**: ⓐ `RewardBox` → `CHEST`(D-157부터 유지) ⓑ **게임 자신의 미니맵 타깃** `MiniMapTargets.mini_map_targets_queue`의 `Target`(`target_transform: Transform`, `type: Target_Type`) → 순수 함수 `WorldMarkerPolicy.ChestKindFromTargetType`으로 `LEVEL_UP_CHEST`→`CHEST`, `MINI_BOSS_CHEST`→`BOSS_CHEST`만 매핑하고 **그 외 타입은 ""(건너뜀, 추측 없음)**. ⓒ D-158의 `TargetHUDArrow.target`(POI 화살표) 경로도 그대로 살아 있다.
- **구현 중 정본에서 확인한 함정**: `Target`은 **중첩 타입**(recon `IsNested: True`)이라 코드에서 이름을 쓸 수 없고 `var entry = queue[i];`로 색인해야 컴파일된다(직접 이름 사용 시 CS0246).
- **영수증 확장**: `statues`, `statuesWithoutPotion`, `potionObjects`, `chestTargets`, `chestTargetsSkipped`, `arrowTargetsSkippedAsStatue` — 다음 착용에서 "석상만 잡고 포션이 없었는지 / 상자 타깃이 몇 개였는지"를 데이터로 판별한다.
- **검증(오프라인)**: 플러그인 Release **경고 0·오류 0**, HandAim **291 → 294**(정책 1 = `ChestKindFromTargetType` 매핑 + `WantsPotion`, 소스 가드 2 = 포션=픽업 앵커/석상 금지 + 미니맵 타깃·RewardBox), 전체 검증기 재실행.
- **미검증(정직)**: **착용 미검증(0.4h 예정)** — 아이콘이 **포션 위에만** 붙고 석상에는 안 붙는지, 상자 아이콘이 실제로 뜨는지는 사람+리시트로 확인해야 한다. `TargetHUDArrow`/미니맵 타깃이 상자 구간에서 실제로 활성인지는 0.4h의 `chestTargets`/`liveArrows`가 답한다.





### D-166 — 실제 체력포션이 있는 가고일과 포션만; 빈 가고일 fallback 금지, 이전 확정 주장 정정 (2026-10-13)
- 사용자: "정확하게는 포션이 걸린 가고일과 포션에만 화살표가 떠야하는데". 빈 가고일은 표시 0. 상자·체력포션은 월드 앵커/반투명/벽 뒤/거리 무제한; 데미지·적 화살표와 전면 HUD 변경 금지. 이는 '빈 가고일 노이즈를 나중에 제거'할 선택사항이 아니라 필수 조건이다.
- **정정(이전 기록을 대체)**: D-162~D-165의 `HEALING@off`, `pickup=n`, `currentPickup=`는 해당 스캔 경로의 관측값일 뿐 실제 포션 부재나 자식 모델 정체의 증명이 아니다. 사용자가 벽에 걸린 포션을 보았다는 보고가 존재한다. `PickUp.t`가 풀 좌표라는 주장/4 m 초과=풀 아티팩트/가고일 bounds가 상자 실종의 원인이라는 주장은 미확정이다. `_ZTest=True`는 재질 설정 사실이지 사람의 벽 뒤 시각 수락 증명이 아니다.
- 0.4j 최종: `run.json` endReason=level-exit, UninstallExit=0, stockVerified=true, error/cleanupError=null, runtimeBefore=runtimeAfter=VDXR. worldMarkers shown=7, chestTargets=1, boundsPlaced=0, offGaze=7. 따라서 상자 누락을 bounds만으로 설명할 수 없다.
- 0.4k(plugin2292A70C…)는 빈 holder→HEALING 코드가 남아 사용자 요구 불충족. `retired.json`으로 미실행 폐기하고 원복한다. 사용자 'continue'는 게임 실행 명령이 아니다. D-164 유지.
- 안전 수정 계획: 포션 미확인 holder는 후보 0(미확인 카운터는 포션 부재를 뜻하지 않음), 4 m fallback 제거, 실제 PickUp.transform과 종류 필터를 모든 소스에서 일관되게 사용. 이름/렌더러 수만으로 포션을 추정하지 않는다. 진단은 7번째 자식을 누락하지 않으며 활성/좌표/타입 및 하위를 기록.
- 매달린 포션의 실제 정체/존재 판정이 미확정이므로 '가고일+포션 정확 표시 완료'를 주장하지 않는다. 동일 소스 추측 수정은 중단하고 새 진단 캡처가 필요하다. 기능 완료 대신 안전 수정+진단 후보임을 명시한다.
- **최종 오프라인(2026-10-13)**: managed Release buildExit0/경고0오류0, HandAim313 PASS, full verifier32/32 exit0. 새 회귀는 기존 fallback에 먼저 실패(RED)를 확인. payload plugin24152491…/nativeF7FDF2D6…; UI/render/native/발사 소스 변경 없음. 검증 스크립트 래퍼의 PowerShell5.1 JSON 배열 count 오류는 원인 확인 후 래퍼만 정정, 검증기 자체 exit0와32개 Passed=true 재확인. 설치 전 완료.
- 0.4k transaction recoveryseq27/ADDITIONS_QUARANTINED_STOCK_VERIFIED, 별도 stock121files/8dirs/7,726,223,158bytes/hash/write times PASS. 0.4l은 안전 수정+진단으로만 준비; config=CHEST,HEALING,BOSS_CHEST(체력포션만, POWER_UP 제외)/distance0/count32, VDXR 유지. 이 설정 제한은 최신 '체력포션' 요구에 맞춘 비교이며 코드상의 POWER_UP 지원 자체는 삭제하지 않는다.
- **준비 완료**:0.4l READY_PREINSTALLED_NOT_LAUNCHED,232files/Installedseq14,installer0/verify0/stderr0B,plugin24152491…/nativeF7FDF2D6…live/build/pin 일치,VDXRunchanged,gameLaunched=false. Source commit8e512dc. 0.4k 대비 config는Output+MarkerKinds만 변경. 정확한 실행 인수는 PHASE_PLAN 최상단; 기존 wrapper의 일반 플레이 문구 대신 PLAYTEST-D166-MARKER-RECON-KO(포션 앞 캡처/빈 가고일0/상자 위치)를 사용. 게임 실행이나 probe/헤드셋 대기 없음; Stage8/TOOL-086 미수락 유지.

### D-167 — 승인된 0.4l 실행 및 포션 앞 캡처 요청 오류; 시각 성공 아님 (2026-10-13)
- 사용자 `실행`은0.4l 1회 실행 승인. 08:58:16Z headset PASS/Steam,08:58:53Z Maze_Small lock,gamePID74928/harnessPID51508;controllersAvailable=True/nativeFailures0/양안 제출 확인. 준비 DLL/config 사용·install skipped, 변경/빌드/런타임 교체 없음.
- 사용자 `나 포션앞`에 요청 전 hand/host 스냅샷 보존: <RepoRoot>\artifacts\headset-d087\0.4l\evidence-potion-20261013-180148. scans348→416/shown0/chestTargets0; HealthPoint/Potion id214560 live=True/renderer=on/pickup=none, 다른5샘플은 부모 비활성/live=False. 관측은 활성 자식 경로 후보이며 이름만으로 포션 정체/수집 가능 상태를 확정하지 않음. host.evidence는256개 누적 bounded 샘플이라 position을 현재 플레이어 위치로 간주하지 않음.
- **에이전트 요청 실수/TOOL-087**:09:02:23.833Z capture.request에16601을 작성했다. Host.cpp385 RequestCapture는0–10만 허용(return2); OpenXrHost.cs329가 throw→host.json.failure.json(capture request rejected)/HOST_FAILED. 양안/raw 캡처0, 요청 미소비. 사용자/마커/런타임 탓으로 돌리지 않음. 이후 요청은 지원 범위·pending·중복 확인; 진단 거부 격리는 별도 회귀와 수정 필요.
- 게임 종료/09:02:29Z 자동 제거 시작→09:05:34Z 원복 완료:UninstallExit0/Recoveredseq27/stockVerifiedTrue/121files/8dirs/7,726,223,158bytes/hash+write times 일치/errors null/VDXR 유지. harness verdictHEADSET_RUN_COMPLETE_NOT_VERIFIED/observationHOST_FAILED는 원복 완료이지 캡처 성공 아님. 0.4l은CONSUMED_NOT_READY(준비 영수증은 실행 전 이력). 즉시 재설치/새 launch/추측 마커 코드 수정 없음; 사용자에게 헤드셋을 벗어도 된다고 알림. 수집 후·상자·벽 뒤·HUD 시각 수락 미수행, TOOL-086/Stage8 미수락 유지.










### D-170 — 사용자 지시: 게임 해상도 설정 + MSAA on/off를 고를 수 있는 exe (2026-10-13)
- 사용자: "이제 게임 해상도 설정 및 MSAA on/off 만들기 및 설정을 선택할 수 있게 exe 파일로 만드는 작업을 진행한다." 지시대로 마커/화살표·HUD 코드는 건드리지 않았다.
- **해석(명시, 되돌리기 쉬운 전제)**: VR에서 화질을 정하는 해상도는 **눈당 렌더 해상도**(기존 `EyePreset`/`EyeWidth`/`EyeHeight`)다. 모니터 뷰는 부산물이고 MSAA도 눈 이미지에만 의미가 있으므로 exe는 이 3키 + 신규 `EyeMsaa`를 편집한다. 게임 평면 창 해상도는 VR 경로에서 쓰이지 않는다.
- **MSAA가 왜 네이티브 작업이었나(TOOL-080)**: D-147 실측대로 게임 자체 AA는 우리 눈 카메라에 오지 않는다(`qualityMsaa=0`, `eyeRtMsaa=1`). 눈 이미지에 닿을 수 있는 AA는 **눈 RenderTexture의 `antiAliasing`** 뿐이고, MSAA 텍스처는 `Gpu.cpp:13`의 `SampleDesc.Count!=1` 거부에 걸려 그대로 복사할 수 없다.
- **구현(off = 무변경이 계약)**: `EyeMsaa=0`(기본)이면 리졸브 대상이 아예 없고 `Submit`이 `source[eye]`를 그대로 복사 → **수용된 경로와 동일**(fault 픽스처가 off에서 리졸브 0회·카피 소스가 raw 소스임을 강제). `2/4/8`이면 눈당 1장의 단일샘플 리졸브 대상(`CreateResolveTarget`)에 `ResolveSubresource`(`ResolveMsaa`) 후 **검증된 exact copy**가 그 대상을 복사한다. shape/format family/same-device 검사는 그대로이고 카피가 받는 텍스처는 항상 단일샘플이다.
- **fail-loud**: `SetEyeMsaa`는 Configure/init 전에만(0/1=off, 2/4/8), 그 외 값은 `2`. 네이티브 init은 **실제 눈 텍스처의 샘플 수**와 선언값이 다르면 예외(`eye render texture samples=N vs EyeMsaa=M`) → Unity가 MSAA를 만들지 않았는데 "msaa=4"라고 주장하는 리시트가 나올 수 없다. 관리측 `Initialize`는 `0|2|4|8` 외 값을 거부(추측 없음). 로그 `[OPENXR_HOST] RESOLUTION ... msaa=4x`, `[OPENXR_HOST] EYE_MSAA requested=... unityAntiAliasing=...`, 리시트 `eyeMsaa`/`quality.configuredEyeMsaa`.
- **설정 exe(D-144 정신 유지: 경로 추측 없음)**: `src\settings\VhvrSettings.csproj` → `dist\tools\VhvrSettings.exe`(net10.0-windows, 프레임워크 의존 162,304 bytes, SHA256 `F07131FC…`, `-SelfContained` 옵션). 프리셋(low/normal/high/custom) + 기준 가로/세로(512–4096) + MSAA(off/2x/4x/8x) + **적용될 눈 크기·합계·픽셀 미리보기**. `HostConfigTool`이 그 4키만 바꾸고 나머지 키는 JsonNode로 보존, 저장 전 `*.yyyyMMddTHHmmssZ.bak` 백업 + temp→move 원자적 교체. 런타임이 clamp할 값은 **거부**(조용한 clamp 없음). exe는 자기 위치에서 `vhvr-openxr-host.json`/`BepInEx\config\vhvr-openxr-host.json`만 탐색(Steam 경로 미추측)하고 `--config`로 임의 경로를 받는다. 게임 실행 중이면 GUI가 경고. CLI `--config/--print/--validate/--apply/--selftest/--help`로 헤드셋 없이 전부 검증 가능(단 WinExe라 PowerShell `&`는 대기하지 않음 → `Start-Process -Wait`).
- **오프라인 증거**: 네이티브 selftest에 **실제 하드웨어 MSAA 리졸브 2케이스**(RGBA/BGRA typeless Count=4 → 1샘플 리졸브 → exact Y-flip copy → 바이트 확인) + **거부 6케이스**(MSAA 대상/포맷 불일치/크기 불일치), fault 픽스처 52→**55**(opt-in 계약 / off 경로 무변경 / 리졸브가 카피보다 먼저·카피 소스가 리졸브 대상 / Close에서 대상 해제), HandAim 317→**319**, Settings 18→**26**, 신규 `tests\settings-tool.Tests.ps1`(exe 배포+왕복+거부+백업+`--form-selftest`로 창 실제 기동·로드 확인, 0.4d 배포 config 사본 사용). 검증기에 `settings-tool-tests` 추가.
- **아직 아님(정직)**: **헤드셋 수락 아님.** 착용 판정은 `-EyeMsaa 4` 1회(권장) 또는 off/4x A/B. 비용(눈당 리졸브 1회 + 단일샘플 텍스처 2장, 2064×2208 ARGB32 기준 ≈ 36 MB 추가)과 fps 영향, 화질 개선 폭 모두 미실측. 2x vs 4x 기본값과 `high` 프리셋 조합은 미결(Q-D170 참조). `dist\preview` 페이로드에는 exe/새 키가 아직 들어가지 않았고 0.3 재포장도 미완(기존 열린 항목).

### D-174 — 사용자 결정: 소스 복구를 멈추고 배포 DLL용 별도 패치 플러그인으로 5개 개선 구현 (2026-10-14)
- **사용자 지시**: "소스 복구 없이 배포 DLL용 별도 Harmony 패치 플러그인으로 5기능만 추가"를 선택. 그래서 `src\core\OpenXrHost.cs`(526 에러)·`src\interaction\HandInput.cs`(12 에러)를 계속 복구하지 않고, 살아남은 배포본(`dist\VampireHuntersVR-0.6-final`)을 대상으로 새 플러그인을 만든다.
- **실측 근거(선택 전 확인한 사실)**: 배포본 관리 어셈블리는 **D-171**(로그 문자열 `(D-171)`, `DeferredGpuWait`/`PrepareAhead`/`EyeScale`/`PerfProfile`/`LeftWeaponTwistDeg` 존재, `VrMode`/`MenuScreen` 없음)이고 **완전**하다. 빌드 참조(`tools\bepinex-be788\BepInEx\core`, `tools\interop`)와 `0Harmony`/`Il2CppInterop`/`Il2Cppmscorlib`/`Assembly-CSharp`가 모두 생존해 있다. 배포 플러그인의 D-171 정책 클래스(`HandFirePolicy`, `AbilityStickPolicy`, `UiRayPolicy`, `HudLayoutPolicy`, `HeadHapticPolicy`, `ReloadLatchPolicy` 등)는 5개 요청의 기반이 이미 존재한다.
- **구현물**: `extras\VhvrExtras\` — net6.0 BepInEx 6 IL2CPP 패치 플러그인(`VhvrExtras.dll`, GUID `local.vampirehuntersvr.extras`, `stage0`에 SoftDependency). **게임 IL2CPP 코드는 절대 패치하지 않고**, Stage0의 관리 메서드만 Harmony로 후킹한다. 로직은 Unity 타입이 없는 순수 정책(`Policies\*`)으로 분리해 오프라인 테스트가 가능하다.
- **이번 빌드에서 실제 후킹한 것(2/5)**: #5 반자동 재발사(`HandFirePolicy.StartThisTick` 후처리 → 트리거 유지 중 `RetriggerSeconds`(기본 0.5s)마다 발사 신호 합성), #4 Y/B 대체사격(`AbilityStickPolicy.Next` 후처리 + 자체 `ReadHands` P/Invoke, `Lower` 버튼 = 좌 Y / 우 B, 상승 에지에서만 `SpecialAbilityInput`/`BloodyRushInput` 플래그를 `FaceButtonPulseSeconds`(0.15s) 동안 세움).
- **미구현분은 숨기지 않는다(3/5)**: #2 쿨타임 햅틱, #3 UI 1.5배, #1 메뉴 패널+포인터는 이번 빌드에서 후킹하지 않았고, 플러그인이 로드 시 `[Extras] NOT_IMPLEMENTED <label>: <이유>`를 **경고로 남긴다**(registry의 `deferred` 카운트로 요약에도 표시).
- **정직한 한계**: `PATCH_APPLIED`는 "메서드를 찾아 감쌌다"는 뜻일 뿐 **동작이 확인된 것이 아니다**. 헤드셋 검증 전에는 #4/#5 어느 것도 수용이 아니다. 0.5초 재발사는 **무기별 반자동 판별이 없어 모든 무기에 적용**되며, 실제 발사 속도는 게임 자체 쿨다운이 계속 제한한다(무기별 판정은 후속). #4의 좌 Y→특수/우 B→혈액 매핑도 확정이 아니라 헤드셋 확인 대상이다(Q-D174).
- **오프라인 증거**: 플러그인 클린 빌드(경고 0/오류 0, `extras\VhvrExtras\bin\VhvrExtras.dll` 15,872 bytes), 신규 `tests\VhvrExtras` **22 픽스처 PASS**(순수 정책만; 게임/헤드셋/동작 게이트 아님), `tests\verify.ps1`에 `extras-fixtures` 추가(RESULT 문자열 동일성 검사 포함).
- **배포 상태**: 아직 `dist` 페이로드/`install.ps1`/재포장에 넣지 않았다. 배포하려면 `payload\BepInEx\plugins\VhvrExtras.dll` 복사 + `BepInEx\config\local.vampirehuntersvr.extras.cfg` 생성 + install/uninstall 매니페스트 갱신이 남아 있다.


### D-175 — 요구 5기능 구현 완료(패치 플러그인), 전부 "헤드셋 미검증" — 2026-10-14
- **범위**: 사용자가 출근 중이라 내가 결정·구현·패키징까지 단독 수행. 요청 5개를 모두 `extras\VhvrExtras`(D-174의 별도 패치 플러그인)에 구현했다. **게임 IL2CPP 코드는 한 줄도 패치하지 않았고**, Stage0 관리 메서드 + 자체 P/Invoke(`ReadHands`, `RequestHaptic`)만 쓴다.
- **#5 트리거 유지 자동 연사(정교화)**: `HandAimDriver.FireTick` 전처리에서 좌/우 슬롯의 `FireCalls`와 무기 `Weapon.attributes.base_fire_rate`를 읽고, `fire_rate <= RetriggerMaxFireRate`(기본 2/s)인 **느린 무기만** 대상으로 표시한다. `HandFirePolicy.StartThisTick` 후처리는 슬롯 순서(좌→우)를 소비해 그 표시가 있을 때만 `RetriggerSeconds`(기본 0.5s)마다 발사 신호를 합성한다 → 연사 무기는 게임 자체 케이던스를 유지(불필요한 신호 합성 없음).
- **#4 Y/B 대체 사격**: `AbilityStickPolicy.Next` 후처리 + 자체 `ReadHands`. **결정(내 판단, 되돌리기 쉬움)**: 좌 `Lower`(Y)=특수(Special), 우 `Lower`(B)=블러디 러시. 반대면 `AbilityFace` 설정만 뒤집으면 된다(Q-D174-1 종결).
- **#2 쿨타임 햅틱**: `FireTick` 후처리에서 슬롯별 `FireCalls` 증가 = 실제 발사로 보고, 그 시점부터 `1/base_fire_rate`(0.12~3s로 clamp, 무기 미상이면 0.5s) 타이머를 돌려 만료 시 `RequestHaptic(hand, amp, ms)`(기본 0.4/60ms) 1회. **정직한 한계**: 게임이 "쿨타임 종료"를 관리 코드에 노출하는 방식은 확인되지 않았다(`Weapon.fire_t`/`second_shot_*` 의미 미검증) → 이것은 **모델링된** 쿨타임이며 "다시 쏠 수 있음"을 알리는 것이지 재장전 완료를 읽은 값이 아니다(Q-D174-6 종결: 재장전이 아니라 발사 가능 시점).
- **#3 UI 1.5배 + 설정 도구**: `HandAimDriver.UiScan` 후처리에서 새로 변환된 각 `UiCanvas.Width`(기본 1.9m, 레벨업 1.4m, 헤드락 1.6m)에 배율을 곱한다. 배치/포인터 계산은 손대지 않는다(같은 `Width`에서 파생되므로 일관). 배율 우선순위: `vhvr-openxr-host.json`의 `uiScale` > BepInEx cfg `UiScale`(기본 1.5). **결정(내 판단)**: HUD 전체 배율(요청 문구 "UI가 1.5배" 그대로).
- **#1 메인 메뉴 패널 + 컨트롤러 포인터**: 원인은 호스트가 **게임플레이 카메라를 기다리다 실패**하는 것(`gameplay camera wait expired; no menu fallback`)이라 메뉴에서는 헤드셋에 아무것도 안 나온다. 구현은 ① `Plugin.StartOpenXrHost` 전처리에서 `OpenXrHost.Config.GameplayOnly=false`(옵트인 `MenuFirst`, 기본 **OFF**) ② `OpenXrHost.Update` 후처리에서 `player==null`(=비게임플레이) 동안 패널 리그 가동: 모드 소유 카메라가 게임 메뉴(월드+UI)를 RT로 렌더, **루트 오버레이 캔버스를 `ScreenSpaceCamera`로 재지정**(D-172 "검은 패널"의 정확한 원인), 눈 카메라는 검정 + 패널 레이어(30)만 마스킹, 패널은 **헤드락 1.5m·40°** 쿼드, 컨트롤러 레이저 + 트리거 클릭을 게임 `EventSystem` 포인터 이벤트로 전달. 게임플레이 진입 시 캔버스/눈 카메라 원복.
- **#1의 정직한 비용(중요)**: `MenuFirst=true` 세션에서는 Stage0 호스트가 `GameplayOnly=false` 경로로 시작하므로 **손/무기 통합(hand aim, 트리거 사격)이 설치되지 않는다** → 그 세션은 평면 스테레오 릴레이가 된다. 그래서 기본 OFF로 두고, 켜면 로그에 경고를 남긴다. 진짜 해결(메뉴→게임플레이 무결 절환)은 `OpenXrHost.Initialize`가 메뉴 분기를 가져야 하므로 **소스 빌드 몫**이며, D-174(경로 C) 결정 때문에 지금은 할 수 없다.
- **오프라인 증거**: 플러그인 빌드 **경고 0/오류 0**(`VhvrExtras.dll` 39,936 bytes, SHA256 `EF8B9735…`, `artifacts\extras-build.json`), `tests\VhvrExtras` **60/60 PASS**(패널 기하·포즈 변환·UI 배율·쿨타임 모델·반자동 정책), `tests\verify.ps1`의 `extras-fixtures` 기대 문자열 60으로 갱신. 설정 도구는 `src\settings\Build.ps1`로 재빌드(셀프테스트 PASS, `--form-selftest` **PASS controls=20 clipped=0**, exe 162,304 bytes, `artifacts\settings-build.json`)하고 `--ui-scale 1.5 --apply`가 `uiScale: 1.5`를 쓰는 것과 `--ui-scale 9`가 **거부(exit 1)** 되는 것을 실제 실행으로 확인. 새 페이로드에 `VhvrExtras.dll` 포함(`dist\...\payload\BepInEx\plugins`), 착석용 드릴 `tests\headset-extras.ps1`은 합성 로그로 판정표/영수증 경로를 검증(`-DryRun`).
- **부수 복구(pre-existing 손상)**: `src\render\RenderResolutionPolicy.cs`가 이전 복구에서 **중복 멤버 + 누락 상수**(`MinScale`/`MaxScale`/`LowScale`/`NormalScale`/`HighScale`/`CustomDefaultScale`/`IsValidScale`/`BothEyes`) 상태여서 설정 도구가 빌드 불가였다. 배포된 `VhvrSettings.exe` 디컴파일을 근거로 원형 복원 + `Dimension`/3-인자 `EyeSize` 호환 오버로드 유지. `Describe`의 `both=`를 `BothEyes`(4128x4416)로 맞춰 같은 exe 안의 출력/창 표기 불일치를 제거.
- **아직 아님(정직)**: **다섯 기능 모두 헤드셋 수락 아님.** PATCH_APPLIED는 "메서드를 찾아 감쌌다"일 뿐이다. 확인 항목: ① 메뉴 패널이 검은 배경에 1.5m 앞으로 보이는가(`MenuFirst=true` 1회) ② 레이저로 메뉴 버튼이 눌리는가(게임 EventSystem이 마우스 포인터를 받는지) ③ HUD가 1.5배로 보이고 잘리지 않는가 ④ 느린 무기에서 트리거 유지 시 연사되는가 ⑤ 좌 Y/우 B가 의도한 기술을 발동하는가 ⑥ 쿨타임 햅틱이 발사 가능 시점에 오는가(체감 지연 확인). `base_fire_rate` 의미와 `RequestHaptic` 수용 여부는 실측으로만 확정된다.
- **다음**: ① 사용자가 착석 1회(위 6항목 배치) ② `tests\headset-extras.ps1`로 영수증 생성 + `docs\playtests\PLAYTEST-EXTRAS-KO.md` 기록 ③ 결과에 따라 기본값(0.5s, maxFireRate 2/s, FOV 40°, 1.5m)만 조정. `MenuFirst`는 사용자 판단 없이는 기본값을 켜지 않는다.
- **착석 전 검증 영수증(2026-10-14, 구현 직후 이어서 수행)**:
  - 설치/제거 왕복(임시 가짜 게임 폴더): `install.ps1` → **INSTALLED 235 files**, `BepInEx\plugins\VhvrExtras.dll` 존재, `vhvr-uninstall.json` 매니페스트에 그 경로 포함 확인. `uninstall.ps1` → **UNINSTALLED removed 235 files**, 플러그인 삭제 + `BepInEx` 폴더 잔여 없음(게임 원본 무변경).
  - 착석 드릴 사전 점검: `tests\headset-extras.ps1 -GameRoot <가짜 루트> -DryRun -Tag ex-prep` → 플러그인 해시 `EF8B9735…` 일치, cfg/log 부재 → 6개 항목 전부 **NO_LOG**. 즉 스크립트가 "아직 아무것도 증명되지 않음"을 정직하게 표시(거짓 PASS 없음).
  - 설정 도구 `uiScale` 재확인: `--ui-scale 1.5 --apply` → 파일에 `"uiScale": 1.5` + `.bak` 생성, `--print` → `CURRENT_UISCALE 1.50`, `--ui-scale 9 --apply` → `INVALID ... 0.5..3`(파일 무변경), 경계 **0.5/3.0 수용**. `--selftest <폴더>` PASS(`uiscale=1.5 backup=True`), `--config <cfg> --form-selftest` **PASS controls=20 clipped=0 loaded=True**(미리보기에 `In-VR UI size x3.00` 포함). 인자 없는 `--form-selftest`는 `FAIL loaded=False` + exit 2 — 설정 경로가 필요하며 조용히 통과하지 않는다(도구 README에 명시).
  - 재포장: `dist\VampireHuntersVR-0.6-final.zip` **250 엔트리 / 35,595,984 bytes / SHA256 `0FD1F546…`**, `dist\VampireHuntersVR-0.6-final.sha256.txt` 갱신, 루트 미러 동일. 번들 `README-KO.md`에 5기능 표 + `MenuFirst` 경고 + "5기능 미검증" 명시, `tools\README-KO.md`에 **In-VR UI size x**(0.5–3.0, 기본 1.50) 행과 새 CLI 3개 추가.
  - 문서: `docs\PHASE_PLAN.md` 상태 보드, `docs\QUESTIONS.md`(Q-D174-1..6 종결 + Q-D175-1..4 신규), `docs\BUGS.md` **TOOL-088** 신규, `docs\playtests\PLAYTEST-EXTRAS-KO.md`(6항목 체크리스트) 작성.
  - **아직 아님**: 헤드셋 판정 **0건**. `tests\verify.ps1` 전체 초록은 TOOL-088(src 트리 손상·`dist\versions\0.4d\config.json` 부재) 때문에 이 트리에서 불가능하며, 그 대신 `extras-fixtures 60/60` + 설정 도구 자체 점검 + 페이로드 설치 왕복으로 대체했다.

### D-176 — 착석 결과 반영 + 사용자 재결정 5건 (2026-10-14)

- **착석 1회 실행/결과(라이브)**: 묶음 페이로드로 게임 1회 실행(pid 112732) → `MainMenu_2` → 게임플레이 → 복귀. `[Extras] LOADED applied=4 failed=1`: `HandAimDriver.FireTick->slot-context`(O), `HandFirePolicy.StartThisTick->retrigger`(O), `HandAimDriver.UiScan->ui-scale`(O), `OpenXrHost.Update->menu-panel`(O), **`AbilityStickPolicy.Next->face-button`(X: IL Compile Error)**.
- **라이브 증거**: `cooldown haptic hand=1 first pulse accepted (result=0, interval=0.500s)` / `hand=0 ... interval=0.250s` → 두 손 트리거 무기에 햅틱 발화. `hand=0`은 fire rate 4/s로 읽혀 재발사 대상 제외, `hand=1`은 fire rate 미판독→기본값.
- **버그 기록**: `TOOL-089`(#4 패치 실패), `TOOL-090`(착석 영수증 라벨 불일치로 거짓 음성 — 합성 로그로만 검증해서 못 잡음).
- **사용자 재결정(2026-10-14, 이 세션)**:
  1. **쿨타임 햅틱 기준 = 로봇팔(보조무기)**. 트리거 손 무기가 아니라 `aux_weapon_1/2`(게임 Q/E) 기준으로 재조준. (단서: 리시트에 `ArmLPresses/ArmRPresses`, 게임에 `change_passive_arm_cooldown_time` 존재.)
  2. **UI 배율**: 레벨업은 적당, **ESC/설정·게임 종료 화면이 너무 작음** → 메뉴 범주를 별도로 키운다. 사용자가 1차 재적용. 에이전트 권장: 범주별 배율(메뉴 ~2.0, HUD/레벨업 1.5 유지). `UiCanvas.IsLevelUp`/`paused` 등으로 구분 가능.
  3. **#5 자동 연사 목표 재정의**: "느린 무기"가 아니라 **크로스보우식 1회-당 발사 무기**를 트리거 유지 중 연속 발사. **구분 판별은 후보 필드 존재**(`auto_fire`, `is_on_auto_mode`, `lazy_fire_on`) → 실기기 1회 검증, 불가 시 **폴백=모든 총기에 간격 발사 신호**(사용자 0.75s 제안, 에이전트 0.6s 기본/0.2–2.0 권장; 과다 신호는 알약 소모 이력과 상호작용 주의).
  4. **MenuFirst**: **소스 복구를 재개**해서 메뉴→게임플레이 자연 전환을 구현. 방법론(소스 복구 후 메뉴 분기 이식 / 지연 초기화 / 게임플레이 우선) 논의 후 착수.
  5. **보조사격은 다음으로 미룸** → `docs\TODO.md` 기록.
- **미해결/주의**: 위 5건은 전부 **미구현**. `MenuFirst` 기본값은 여전히 OFF이며 소스 복구 재개 없이는 자연 전환 불가. 이 세션은 자동 모니터/자동 스톡복원이 없는 묶음 경로였고, 모드는 아직 설치된 상태(스톡 아님).
- **문서**: `docs\TODO.md` 신규, `docs\BUGS.md`(TOOL-089/090), `docs\PHASE_PLAN.md`(준비/착석 절). 로그 원문이 진실 원천(영수증 도구는 TOOL-090으로 신뢰 불가).


### D-177 — D-176 결정 구현: 자동연사(모든 총기/0.6s) + 로봇팔 쿨타임 햅틱 + 영수증 라벨 수정 (2026-10-14)
- **#5 자동연사 재설계(사용자 결정)**: 무기별 자동/반자동 판별 대신 **트리거를 누르는 동안 모든 총기에 0.6초마다 발사 신호**를 합성한다. `SemiAutoFirePolicy.ShouldSignal(held, alreadyFiring, now, interval, ref lastSignal)` — ① 홀드 아니면 리셋/무신호, ② 게임이 이미 이 틱에 발사했으면 그걸 샷으로 보고 타이머만 재시작(연사 무기 중복 없음), ③ 홀드 시작 시 타이머만 시작(첫 발은 게임 에지가 담당), ④ 간격 경과 시 신호. 기본 간격 `RetriggerSeconds` **0.5→0.6s**. 무기별 게이트(`RetriggerMaxFireRate`/`PerPressWeapon`) 제거.
- **#2 쿨타임 햅틱 재조준(사용자 결정)**: 트리거 손 무기가 아니라 **로봇팔** 기준. **왼쪽 로봇팔↔왼손, 오른쪽 로봇팔↔오른손**. 배포 드라이버의 공개 필드 `ArmLPresses`/`ArmRPresses` 증가 = "로봇팔 발사". 방출 시점은 **모델링** — 새 cfg `[AbilityHaptics] ArmCooldownSeconds`(기본 0.5s, 0.05–5). 로그 마커 `cooldown haptic hand=N arm ...` 유지.
- **TOOL-090 수정**: `tests\headset-extras.ps1` 판정 라벨을 실제 Hook 라벨로 교체. **기존 실기기 로그로 재검증**: 이제 `4-face-ability = PATCH_FAILED`로 정확히 잡고 4개는 PATCH_APPLIED(거짓 음성 제거).
- **오프라인 증거**: 빌드 경고 0/오류 0(`VhvrExtras.dll` **38,912 bytes**, SHA256 `32F9C600…`), `tests\VhvrExtras` **63/63 PASS**(verify.ps1 기대 60→63). dist 페이로드 복사, 배포 zip 재포장 **250 엔트리 / 35,594,914 bytes / SHA256 `96C7A7DC…`** + `.sha256.txt`/루트 미러 갱신. 게임 재설치 **235 files**, 설치본 = 배포본(`32F9C600…`). dist 번들의 런타임 파일 `vhvr-game-root.txt` 제거.
- **아직 아님(정직)**: 전부 **헤드셋 미검증**. 로봇팔 쿨다운은 모델링값이고, 발사를 `ArmLPresses` 증가로 추정한다(활성화≠실제발사 가능). 자동연사는 알약 소모(D-110) 상호작용 미확인. #4는 TOOL-089로 보류.

### D-178 — 소스 복구 완료 + MenuFirst(메뉴→게임플레이 무결 전환) 구현 (2026-10-15 새벽, 자율 진행)
- **사용자 지시**: "B 실현성 조사 → 안 되면 A", "소스복구 재개", "아침까지 끝내고 보고". B는 D-177 조사에서 실현 불가로 판정됐고, A(소스 복구)를 진행했다.
- **B 실현 불가 근거(디컴파일)**: `OpenXrHost.Initialize()`는 일회성 메서드로 설정검증+게임플레이카메라 대기+카메라/눈 준비+손 통합 생성+`configured=true`를 모두 수행한다. 손 통합은 `GameplayOnly` 경로에서만 생성되고 `HandAim && !GameplayOnly`면 예외 → 메뉴 모드에서 나중에 손을 붙이는 진입점이 없다. 재호출은 XR 세션 재구성 위험, IL 트랜스파일은 약 440줄 메서드라 고위험.
- **소스 복구(핵심 성과)**: `src\core\VampireHuntersVR.Stage0.csproj` 빌드 **538 오류 → 0 오류/0 경고**.
  - 손상 원인: 여러 파일이 **두 리비전이 뒤섞인 병합** 상태(중복 필드/Update/Initialize 블록, 닫는 중괄호 누락) + 일부 파일 통째 유실.
  - 방법: 배포 `VampireHuntersVR.Stage0.dll`(D-171, 완전)을 `ilspycmd 11.1.0`으로 디컴파일해 **권위 원본**으로 사용.
  - 복원/수정한 파일: `core\OpenXrHost.cs`(디컴파일본 + `#pragma` nullable 노이즈 억제 + `Object` 별칭), **신규** `core\OpenXrHostConfig.cs`, **신규** `core\HostNative.cs`(NativeEyePose 44B/NativeHostStatus 136B/HostNative P/Invoke — 소스에서 유실), `interaction\HandInput.cs`(YawOf 헤더 + InputRelease 본문/클래스 닫기 복원 = 잘림 복구), **신규** `interaction\HandPose.cs`(유실 partial: 로봇팔/근접 햅틱, NoteFacePress, ApplyDeadzone, NoteHandBody, HoldHandsBeforeParticles), `interaction\HandAim.cs`(WorldMarkerPolicy 누락 9멤버), `interaction\HandFlameEmitter.cs`(Scan에 Flame/Acid/SkippedUnsafe 카운터 복원), `csproj`에 HandPose/HandRenderTiming/BoundedDiagnosticWriter Compile 추가.
  - 주의(정직): `OpenXrHost.cs`는 디컴파일본이므로 **주석이 없다**. 동작은 D-171과 동일해야 하나 **헤드셋/게임 검증은 아직 없다**.
- **MenuFirst 구현(소스)**: `OpenXrHostConfig.MenuFirst`(기본 false) 추가. ① 헤드셋 설정 검증에서 MenuFirst 허용 ② `Initialize`의 게임플레이 카메라 대기 생략(메뉴 모드로 시작, `source=Camera.main`) ③ 손 통합은 MenuFirst면 지연 ④ `Update`에서 `player==null` 동안 `TryEnterGameplay()`가 1초 간격으로 게임플레이 카메라를 스캔 → 찾으면 카메라/플레이어 인수 + `InstallHandAim()` → `Config.GameplayOnly=true`로 전환(이후 일반 게임플레이 워치독 적용).
  - **적용 범위/한계**: `InstallHandAim()`은 Initialize의 인라인 설치 블록과 **의도적으로 중복**이다(원본 D-171 블록을 byte-for-byte 보존하려고). 다음 리팩터에서 통합 필요.
  - **미배포**: 재빌드 DLL(`A2E2F240…`, 411,648 bytes)은 **dist 페이로드에 넣지 않았다**. 배포본/설치본은 여전히 D-171(`E5129D81…`)이며 변경 없음. 재빌드본은 디컴파일 복원체라 **동작 동등성/헤드셋 검증 전 배포 금지**.
- **회귀 검증**: extras 재빌드 0/0 + fixtures **63/63 PASS**(변화 없음), 배포 페이로드/설치본 해시 불변.
- **남은 것(정직)**: ① 재빌드본 동작 동등성 검증(오프라인) ② MenuFirst 실기기 검증(메뉴 표시 → 스테이지 진입 시 손 통합) ③ `InstallHandAim` 중복 통합 ④ extras의 `MenuFirst` 전처리(GameplayOnly=false 강제)는 이제 불필요/충돌 소지 → 비활성화 필요 ⑤ install.ps1에 `MenuFirst` 키 노출(false 기본).
- **문서**: `docs\BUGS.md`(TOOL-088 해결 기록), `docs\TODO.md`, `docs\PHASE_PLAN.md` 갱신. 디컴파일 참조: `artifacts\_estimate\`.

### D-179 — 배포 번들: 설정 도구를 install.bat 옆으로, 메뉴 UI 세부 (2026-10-15, 자율)
- **사용자 지시**: "배포파일 tools 폴더 안의 setting.exe를 install.bat과 같은 페이지에 두고, 오류 없으면 배포파일까지 준비".
- **번들 구조 변경**: `tools\` 폴더 **제거** → `VhvrSettings.exe/.dll/.deps.json/.runtimeconfig.json`을 `install.bat` **같은 폴더(번들 루트)** 로 이동. `tools\README-KO.md` → `SETTINGS-README-KO.md`. README/install.ps1/uninstall.ps1의 `tools\` 참조 전부 갱신(잔존 0건).
- **설정 도구 자동 탐색 수정(필수)**: 번들 루트에 있으면 기존 탐색(exe 옆·게임 루트·`BepInEx\config`)이 설정 파일을 못 찾는다(게임 폴더 안이 아니므로). → `HostConfigTool.Locate`가 **먼저 `install.ps1`이 남긴 `vhvr-game-root.txt`(설치한 게임 폴더)** 를 읽어 `<게임>\BepInEx\config\vhvr-openxr-host.json`을 사용. 이 경로는 사용자가 지정한 설치 폴더이지 추측이 아니다(D-144 정신 유지: Steam 라이브러리 스캔 없음). 마커가 깨졌거나 없으면 기존 탐색으로 자동 폴백.
- **검증(임시 가짜 게임 폴더에서 왔복)**: install → 마커 작성 → `VhvrSettings.exe --print`가 번들 루트에서 게임 설정을 찾음(exit 0) → `--ui-scale 2.0 --apply`가 uiScale=2 기록 + `.bak` 생성 → `--form-selftest` **PASS controls=20 clipped=0 loaded=True** → uninstall이 마커·BepInEx·매니페스트 제거, 게임 폴더는 `Vampire Hunters.exe`만 남음, 번들의 설정 exe는 유지. 마커 없을 때는 exit 1 + 명확한 오류(조용한 실패 없음).
- **배포 zip**: **250 엔트리 / 35,609,407 bytes / SHA256 `5B98BE71…`**, 루트에 설정 도구 4파일, `tools` 엔트리 0, 런타임 마커 미포함.
- **플러그인(현재 설치본 = 배포본)**: Stage0 `2359379A…`, Extras `2E074C88…`, Native `F7FDF2D6…`. 빌드 core/extras 0경고/0오류, fixtures 63/63.
- **정직한 한계**: 메뉴 UI 레이저는 **아직 실기기에서 동작 확인 안 됨**(네이티브 손 활성화를 extras `Load()` 시점으로 옮긴 마지막 수정 미실측). 배포본은 **레이저가 안 나와도 마우스·PC로 메뉴 진행이 가능**하도록 설계(기본 `MenuFirst=false`).

### D-180 — 메뉴 레이저/클릭 수정 + MenuFirst 스테이지 진입 후 화면 오염 수정 (2026-10-15, 실기기 피드백 반영)
- **레이저 방향**: `TryToWorld`의 출력은 회전 쿼터니언이었는데 방향 벡터로 썩다 → `Forward()` 후 사용. 사용자 확인: 방향 맞음.
- **클릭 선택**: 맨 위 히트가 장식용 `Border`여서 버튼까지 안 감 → Selectable 우선 + ExecuteHierarchy + hover(enter/exit). 사용자 확인: 메뉴 UI 작동.
- **네이티브 손 활성화**: MenuFirst에서도 호스트 시작 전 EnableHands 호출(OpenXrHost.Initialize). 로그 result=0 확인.
- **Y/B 패치**: 후킹 인자 `__5`가 존재하지 않는 인자라 IL 컴파일 실패 → 이름 바인딩 `allowed`. 로그 `applied=6 failed=0` 확인(동작은 미확인).
- **사용자 보고 4건(스테이지 진입 후)**: (a) 포션/상자가 불투명 큰 아이콘, (b) 적 밑 빨간 원, (c) 로봇팔 진동 없음, (d) 석궁 자동 연사 안 됨.
  - **(a)(b) 원인(추정, 코드 근거)**: MenuFirst는 메뉴 카메라(ArenaCamera, cullingMask=-1=모든 레이어)로 눈 카메라를 만들고 스테이지 진입 때 다시 복사하지 않았다. 게임플레이 Main Camera mask=34603007에는 없는 미니맵 레이어(Mini Map Camera mask=1048576)가 눈에 그려졌다. 미니맵 아이콘은 위에서 내려다보는 평면 스프라이트라 바닥에 누운 노랑 화살표/초록 포션/빨간 원으로 보인다(스크린샷과 일치). 수정: TryEnterGameplay에서 eyes[i].CopyFrom(source) 후 targetTexture 복원. 로그 `MENU_FIRST eye cameras re-copied ... cullingMask=`로 확인 예정. **미확인.**
  - **(d) 원인(추정)**: `hand-aim.json`에서 fireCalls==triggerPresses(83/83, 168/168), heldSeconds 18s → 추가 신호가 한 번도 안 갔다. `HandFirePolicy.StartThisTick`은 작은 static이라 JIT가 호출부에 인라인하면 Harmony 패치를 우회한다(PATCH_APPLIED는 찍히지만 호출되지 않음). 수정: `[MethodImpl(NoInlining)]` (StartThisTick, AbilityStickPolicy.Next 둘 다). 실제 호출 여부를 로그 `auto-fire hook is being called` / `auto-fire signal #N`로 구분. **미확인.**
  - **(c) 원인(부분 확정)**: 펄스는 result=0으로 수락되었지만 0.4/60ms는 너무 약해 모르고 지나갔을 수 있다 → 기본값 0.8/180ms. 또 쿨다운은 게임 값이 아니라 고정 0.5s 모델이다(게임의 실제 팔 쿨다운은 미확인). 로그: `robot arm LEFT/RIGHT fired`, `cooldown haptic hand=N pulse #K accepted`.
- 빌드 core/extras 0경고/0오류. 설치본 Stage0 `6CCAE844`, Extras `D9B1CA59`. 게임 미실행.

### D-181 — 마커 복원 / 메뉴 복귀 / ESC 메뉴 크기 / 품질 설정 / 쿨타임 관찰 (2026-10-15, 실기기 피드백 반영)
- **마커 회귀(제 실수)**: D-178 소스 복구가 배포 DLL(D-171)보다 **이전 리비전**의 `HandMarkers.cs`를 만들어 포션/상자 화살표가 사라졌다. 확정 원인 3건: `Tick()`의 `MarkersTick` 호출 누락, `Release()`의 `MarkersRelease` 누락, 영수증 `worldMarkers` 누락. 증거: 실행 영수증(`hand-aim.json`)에 마커 블록 없음, 배포 디컴파일에는 존재. 수정: 3건 복원 + 배포본의 `AddHangingPotion/HangingPotionOf/IsHangingPotion` 이식(구현 소스에 통째로 빠진 경로). 함수 이름 기준 기계 비교(108 vs 242)에서 더 빠진 메서드는 이 둔 개뿐. **실기기 미확인.**
- **스테이지→메뉴 복귀 검은 화면**: 로그 증거는 실행 중 복귀 후 `OPENXR_HOST` 로그가 한 줄도 없고 메뉴 UI 재생성도 없음(직접 본 것). 원인(코드 근거): `player`가 세션 동안 안 해제되어 호스트가 죽은 게임플레이 카메라를 계속 따라감. 수정: MenuFirst에서 `handAim != null && ReturnedToMenu()`면 `LeaveGameplayForMenu()`(손/무기 해제, `player=null`, `GameplayOnly=false`, 메뉴 카메라로 눈 카메라 재복사) + `AdoptMenuCameraIfNeeded()`. 로그: `MENU_FIRST returned to menu`, `MENU_FIRST menu camera adopted`. **스테이지→메뉴→스테이지 반복 안정성은 미검증.**
- **ESC 메뉴**: 사용자 결정 폭 **3.0 m / 거리 1.3 m**(`HandUi.cs` UiWidth/UiDistance). extras UI 배율(1.5)이 이중으로 곱해져 4.5 m가 되지 않도록 폭이 정확히 3.0 m인 캠버스는 배율을 건너뜀. HUD(1.6→2.4 m)와 레벨업(1.4→2.1 m)은 그대로.
- **품질**: 버그가 아니라 설명 오해 — 사용자 정정: 인게임은 높음이었고 레지스트리 `UnityGraphicsQuality`는 0. 설정 파일에 `GraphicsQuality`(-1=게임 선택 유지) 추가, 호스트가 시작 시 `QualitySettings.SetQualityLevel`로 적용. 로그 `GRAPHICS_QUALITY levels=0=..|1=..|2=.. current=N requested=M`은 항상 찍히므로 번호↔이름은 실제 실행에서 읽음(추측 안 함). 설정 도구 CLI `--graphics-quality`, GUI 필드 추가; 값 범위 밖은 거부. **인게임 옵션이 덮어쓰는 것은 사용자 허용.** 해상도는 건드리지 않음(사용자가 사람들에게 안내).
- **로봇팔 쿨타임 진동**: 무기마다 쿨타임이 다르므로 고정 0.5 s 모델은 틀린다. 게임 인터오프에서 읽을 수 있는 후보: `Weapon.can_use_secondary_shot()`, `second_shot_t/cooldown/duration`, `last_secondary_shot_t`, `fire_t`, `reload_t`, `firing_second_weapon`; `Weapon_Category{PRIMARY,SECONDARY,PASSIVE}`; `Pulse_Gun.get_can_do_effect`. **어느 필드가 "사용 가능"을 대표하는지 모름.** 따라서 이번에는 동작 변경 없이 **관찰 전용 `ArmCooldownProbe`**만 추가(로그 `arm-probe ...`, 최대 160줄, 값이 바뀔 때만 출력, 게임에 쓰기 없음). 한 번의 플레이로 후보 중 실제로 뒤집히는 필드를 확인한 뒤 그 신호로 진동을 다는 것이 다음 단계.
- **테스트 상태(정직)**: extras 63/63 PASS, ShotScopes 28/28 PASS, 설정 도구 selftest/품질 왕복/양식 검사(clipped=0) PASS. **모두 이전 세션의 소스 복구(D-178)로 인해 이미 빨간 상태인 스위트 3개**: `HandAim`(테스트 파일 자체 컴파일 오류 CS0165), `WeaponRuntime`·`GameplayCamera`(소스 가드가 `OpenXrHost.cs`에서 `public bool GameplayOnly { get; set; }` 등 문자열을 찾는데 설정 클래스가 `OpenXrHostConfig.cs`로 분리되어 실패). 이번 변경이 아니라 D-178 분리가 원인이나 **아직 고치지 않았고 verify.ps1은 실패한다**(BUGS 등록).
- 설치본: Stage0 `8C798E60`, Extras `629C1DC7`, 설정 도구 번들 루트 복사 완료. 기본 `MenuFirst=true`, `GraphicsQuality=-1`. 게임 미실행.

### D-182 — 마커·메뉴 복귀 실제 원인 확정과 배포본 코드 복원 (2026-10-15, 사용자 실행 피드백)
- **마커가 안 나온 진짜 원인(확정)**: D-178 복구가 만든 WorldMarkerPolicy는 구버전이라 ClampMaxDistance가 배포 기본값 0(무제한)을 5 m로 바꿨다. 영수증 maxDistance=5가 증거. 또 HandMarkers.cs 자체가 구버전(MarkerScan 239줄→1줄 등).
- **수정**: WorldMarkerPolicy와 HandMarkers.cs를 배포 DLL 디컴파일에서 **통째로** 이식(동작 = 0.6 원본). nullable/Object 별칭만 컴파일용으로 정리. 빌드 0경고 0오류.
- **메뉴 복귀 먹통 원인(확정)**: 이번 실행 로그에 
eturned to menu가 전혀 없고 OWNERSHIP_SOFT_LOSS grace=300s failed=source-reparented만 있음. 복귀 검사가 도달하기 전 MenuFirst && player==null 분기의 early return이 전 경로를 삼킴. → 복귀 검사를 그 분기의 **맨 앞**으로 이동. 로그 MENU_FIRST returned to menu가 다음 실행의 판정 기준.
- **로봇팔 쿨타임 관찰 결과**: 팔 무기 = LASER_SABER(보조 슬롯), second_shot_cooldown=15 s, second_shot_duration=3 s, can_secondary_shot=False 지속, last_shot_t 미갱신. 팔 사용 동작이 없어 신호 확정 부족. 관찰기 유지.
- **자동 연사**: 이미 검증됨(이번 로그에도 signal #1..#5). 재시험 요청 철회.
- 설치: Stage0  B3F0FAB, Extras 629C1DC7. 게임 미실행.


### D-183 — 로봇팔 햅틱 = 무기 자체 쿨타임 기반 / 마커 미표시 원인 진단기 추가 (2026-10-15, 두 번째 관찰 실행 반영)
- **쿨타임 신호 확정(관찰 증거)**: 팔 발사마다 Weapon.last_shot_t가 현재 시각으로 갱신, second_shot_cooldown(Dragon_Whip 15 s) 후 재사용. 재사용 가능 ⟺ 
ow - last_shot_t >= second_shot_cooldown. can_secondary_shot은 주무기 대체사격(Y/B) 판정이므로 무시(사용자 지시).
- **구현**: 신규 ArmHaptics.cs(보조 슬롯 LEFT/RIGHT_TOP_HAND 무기별 lastShot/cooldown 추적, 준비 엣지에서 해당 손 1회 펄스, 로그 rm-ready pulse #…). 기존 고정 0.5 s 모델과 ArmCooldownProbe 호출 제거(관찰 파일은 유지).
- **마커 미표시 원인 진단기**: 이번 실행 영수증 candidates=0 shader=" error=" 조합은 MarkersPlace가 재질 생성 전에 조용히 빠져나갔음을 뜻함. 어느 관문인지 확정용 카운터 추가: placeCalls / placeSkippedNoHead / placeSkippedNoMaterial / scanSources(영수증 worldMarkers에 포함). 다음 실행 한 번에 정상 원인 확정.
- 빌드 core·extras 0경고 0오류, extras fixtures 63/63 PASS. 설치: Stage0 26D45D65, Extras EF80DD46. 게임 미실행.


### D-184 — 마커(화살표) 실종의 최종 원인: 0.6-final 패키지 빌드가 호출을 잃음

- **질문(사용자)**: "마커는 원래 있던건데 왜 없어졌나. 0.6-final에서 값을 읽어 찾을 수 없나?"
  사용자 제안대로 `VampireHuntersVR-0.6-final.zip`(2026-10-15 02:37 패키징, Stage0 빌드 02:33)을 기준으로 IL 명령어 수준 대조.
- **증거(IL 대조)**:
  - 0.6-final Stage0.dll: `HandAimDriver::MarkersTick` 언급 2곳(정의+끝)뿐, **호출 0곳**. `MarkersPlace`도 호출 0곳. 마커 코드 전체가 살아있으나 아무도 호출하지 않는 죽은 코드.
  - 동작 빌드 해체본(`artifacts/_estimate/All.utf8.cs`, 00:50 생성 = 사용자가 예전에 플레이한 빌드):
    `Tick()` 1156행 `MarkersTick(source);`(스캔), `UiBeforeEye` 4105행 / `UiAfterLayout` 4119행 `handAimDriver.MarkersPlace();`(그리기)가 존재.
  - 결론: **0.6-final 패키징 빌드가 이 호출 3곳을 잃음.** 코드 복원만으로는 안 되고 호출 복원이 필요했음.
- **왜 계속 안 나왔나(2차 회귀)**: 중간 복원에서 스캔 호출(`Tick`)만 복원하고 그리기 호출(`UiBeforeEye/UiAfterLayout`의 `MarkersPlace`)을 놓침.
  16:13 실행 영수증이 증거: 후보 수집은 정상(hangingPotions=1, chestTargets=1)인데 `candidates=0, quads=0, shader=""` — 그리기 단계가 한 번도 실행되지 않음. D-183 진단 카운터도 그리기 함수 안에 있어 호출이 없으면 0으로만 남음.
- **조치**: `HandUi.cs`의 `UiBeforeEye/UiAfterLayout`을 동작 빌드와 동일하게 복원 — uiSource/eye 렌더마다 `MarkersPlace()` 먼저, eye에는 `UiRenderPlace()` 뒤, `UiAfterLayout`은 `UiRenderPlace(); MarkersPlace();`. 빌드 0경고/0오류, 게임+payload 모두 Stage0 `0A2791CD` 설치.
- **미해결/한계**: 0.6-final 빌드가 왜 호출을 잃었는지(정확한 세션/시점)는 확인 못 함. 화살표는 아직 헤드셋 미검증. LogOutput.log 유실(원인 미확인)로 16:13 런의 로봇팔 진동 로그 증거 소실 — 다음 런에서 재확인.

### D-184/D-185 — UI 화질(MSAA) + 엔지니어 포탑 확정(X키) 2026-10-15

**D-185 UI 자글자글 원인과 수정.** 눈 렌더 타깃에 MSAA가 없었다(`antiAliasing=1` = off). UI는 렌더 텍스처 없이
눈 버퍼에 직접 그려지므로 텍스트/엣지가 계단 현상. 게임 품질도 `gameAntiAliasing=0`. 해상도 1.4배(2688x1512/eye,
custom preset)와 별개로 엣지 안티에일리어싱이 필요했다. 수정: `OpenXrHost.cs` eye RT `antiAliasing = 1 -> 4`(4x MSAA).
로그 `QUALITY_AA eyeRtMsaa=`로 실적용 확인. GPU 비용 상승 가능성 — 성능 저하 시 2로 낮춤.

**D-184 엔지니어 포탑 설치 실패 원인과 수정.** 포탑 UI는 열리는데 설치 확정이 안 됐다. 게임 `PlayerInputController`의
입력 두 개: `SpecialAbilityInput(bool)`(우클릭 아래 = UI 열림, 이미 전송) + `SelectInput(bool)`(`selectPressed`, 설치 확정).
`SelectInput`을 VR 입력이 한 번도 보내지 않았다. 수정: 왼쪽 X(D-101 Interact)에 Select를 겹침(사용자 승인). 두 경로로 전송:
(1) 직접 `inputController.SelectInput(interact)` 매 틱, (2) 가상패드 비트 `BitLeftStick|BitSelect` 동시 설정. `SelectPresses`
카운터 추가. release 시 `SelectInput(false)`로 풀림.

**정직 한계:** `SelectInput`이 포탑 설치 확정이라는 것은 `PlayerInputController`/`PlayerTower` 대조로 강하게 추론한 것.
100% 확정은 헤드셋에서 X키 설치가 될 때까지. `BitSelect`가 액션 에셋에 실제 바인딩돼 있을지도 미확인(직접 setter 경로가
있어 액션 에셋이 없어도 동작해야 함).

**검증:** core 0오류/0경고, extras 0오류/0경고. 설치 hash: Stage0 7DD9C0F7(MSAA4+Select), Extras 67B780CF(FireTick 인자2 수정).
양쪽(게임+payload) 일치. 헤드셋 미검증.

### D-104-iso-promotion — 이등방성 필터링 스위치를 사용자 도구로 노출 (2026-10-17)

- **요청**: 사용자  .7 zip 을 기준으로 수정. 메뉴 스크롤링은 A안으로, VhvrSettings.exe에 이등방성 필터링 스위치 추가, 그냥 0.7 위에 0.7.1 추가 빌드로 진행. 그 후 A-1 (정직 보류).
- **현황(0.7 의 사실)**: 호스트 OpenXrHost.cs:879 는 이미 if (Config.Anisotropic) 분기를 가지고 QualitySettings.anisotropicFiltering = ForceEnable + Texture.SetGlobalAnisotropicFilteringLimits(16,16) 를 호출. 0.7 zip 의 install.ps1:137 은 이미 Anisotropic = $false 를 첫 설치 기본값으로 박음. **그러나 VhvrSettings.exe 는 이 키를 모름** — UI/CLI/셀프테스트 모두 무관. 즉 사용자가 켜려면 JSON 직접 편집 또는 테스트 하니스의 -Anisotropic 플래그(headset-handaim.ps1)만 사용 가능.
- **A 안(메뉴 스틱 ↑/↓ → 마우스 스크롤) 정직 보류**: 0.7 의 Stage0 플러그인은 controllers=NOT_IMPLEMENTED (Plugin.cs 명시). OpenXR 입력 액션 정의 부재 → 우리 호스트가 받는 것은 컨트롤러 pose 뿐, axis 값을 C# 으로 가져오는 코드 없음. INPUT-MAP.md 의 D-057 점유(right stick up=interact, down=blood ability) 와의 충돌 해결도 필요. **0.7.1 스코프 외**. Q-MenuScroll 로 등록.
- **결정**: **0.7.1 은 호스트 코드 변경 0줄 + VhvrSettings.exe 의 UI/CLI/셀프테스트에만 추가**. 변경 파일: HostConfigTool.cs(DefaultAnisotropic + ReadAnisotropic + ApplyAnisotropic + BoolOf 헬퍼), SettingsForm.cs(_aniso CheckBox + LoadConfig/Preview/Save 3줄), Program.cs(--aniso/--no-aniso + --print aniso 라인 + --selftest Anisotropic 왕복).
- **기본값 OFF** 유지(D-104 정책 + 0.7 install.ps1 와 정합). 16x 강제(SetGlobalAnisotropicFilteringLimits(16,16)) — 호스트가 이미 그렇게 함.
- **검증**: --selftest PASS(Anisotropic=false 왕복 확인), --apply --aniso 후 16x, --apply --no-aniso 후 off, 정적 컨트롤 좌표 검사 PASS(23개 컨트롤 모두 700x420 안에 클립 0). --form-selftest 의 preview 라인 검증은 0.6 부터 알려진 Defender/Set-Content race 로 PASS 못 함 — 테스트 하니스 한계로 분류, 회귀 아님.
- **미검증(정직)**: 헤드셋 미검증, fps 비용 미측정. 사용자가 켠 뒤 다음 PLAYTEST-D104 라운드에서 실제 효과 확인.
- **한계/대안**: 만약 다음 빌드에서 사용자가 nisotropic 키로 더 정밀한 제어(2x/4x/8x/16x)를 원하면 콤보박스로 확장. 현재는 호스트가 16x 만 지원하므로 단순 bool 이 정합.


### D-186-FovScale — 렌더 FOV 축소(over-render 자름) 정직 분석 + 보류 (2026-10-17)

- **요청(0.7.1 직후)**: 영상 녹화본을 보고 렌더 영역이 HMD 표시 영역보다 넓다는 시각적 인상 → 이 부분을 줄여서 FPS와 해상도에 이득을 얻을 수 있을까?

- **현황(0.7 의 사실, 코드 근거)**: 렌더 FOV 의 출처 = `xrLocateViews` 가 보고한 값.
  - `src\render\NativeHost\Host.cpp:60-62` (Host::PrepareBody) — `auto& v=views[eye]; ... e={...,v.fov.angleLeft,v.fov.angleRight,v.fov.angleUp,v.fov.angleDown};` — C# 으로 그대로 전달
  - `src\render\NativeHost\Host.cpp:72` (Host::Submit 의 layer view) — `v.fov=views[eye].fov;` — compositor 에 **그대로 제출**
  - `src\core\OpenXrHost.cs` (2 곳) — `float[] array4 = StereoMath.Projection(pose.Left, pose.Right, pose.Up, pose.Down, source.nearClipPlane, source.farClipPlane);` — C++ 가 준 4 각도를 `StereoMath.Projection` 에 직접 넣음
  - `StereoMath.cs:10-19` 는 4 각도 (asymmetric frustum) 를 받아 4x4 projection matrix 로 변환, 어떤 scale 도 곱하지 않음
  - `vhvr-openxr-host.json` 의 config 키에 FovScale / FoV / fov_scale 등 단 하나도 없음 — `Anisotropic/EyeMsaa/EyePreset/EyeScale` 4 개만 있고 그 외 없음
  - `OpenXrHostConfig.cs:1-106` 의 30+ POCO 필드에 FOV 키 0 개
  - `OpenXrProbe.Receipt.EyeViewReceipt.Fov = {AngleLeft,AngleRight,AngleUp,AngleDown}` (4 floats, radians) 가 `FrameLoop.cs:253-261` 에서 첫 submitted frame 의 FOV 를 캡처. headset-free 환경엔 실제 receipt 가 없어 본 결정의 수치 추정은 표준 + Quest 3 알려진 스펙 기반

- **over-render 가 보이는 이유(정직 추정, OpenXR 표준 기반)**:
  - OpenXR 표준: `xrLocateViews` 가 보고하는 FOV = 표시 FOV + over-render 마진 (lens distortion + barrel margin, 보통 수평/수직 5~10°)
  - Meta native runtime: 표시 ≈ 렌더 (over-render 거의 0)
  - SteamVR (Q3 포함): 표시 + 마진 (5~10° 더 큼)
  - VDXR (Virtual Desktop): 앱 설정 따라 다름, 보통 1.1~1.2×
  - Monado: 사용자 설정 그대로
  - Quest 3 의 명목 FOV ≈ 110° 대각, 96° 수평, 96° 수직 (런타임마다 다름). 런타임이 105~115° 를 보고하면 사용자가 넓다고 느낌 — 그러나 이건 표준 동작 (lens 보정 + IPD margin + barrel margin)


- **이득 추정 (가정: 런타임이 표시 FOV 의 1.1× 를 보고)**: FovScale=0.90 = 표시 FOV 와 같음
  - **수평 픽셀 수 비례 = tan^2 ratio**. 수평 half-angle 0.55 rad(31.5°), 배율 1.1 → 0.605 rad(34.6°). tan(34.6°)/tan(31.5°) = 0.69/0.61 ≈ **1.13**. 1.13^2 ≈ **1.28** → **28% 픽셀 감소** (1.1× 보고 가정)
  - **FovScale 0.90 → ~22% 픽셀 감소, FovScale 0.80 → ~44%, FovScale 0.70 → ~61%** (1.1× 보고 가정, 표시 FOV 1.0× 와 비교)
  - **fps 향상**: VSynced 헤드셋(현재 VDXR 45Hz, Quest 72/90Hz 등) 에선 fps 가 다음 refresh step 으로 점프 (45→72 등). 비 VSynced 면 ~22% 단축. **d148b/d148d 의 VD 45Hz 상한이 잠근 환경이면 fps 자체는 안 오르고 GPU 시간 단축만 → 다른 비용(MSAA, 해상도 1.4×) 의 여유**

- **구현 복잡도 (5 위험, 정직)**:
  1. **검은 띠 (확정 위험)**: Unity 의 `eyes[i].projectionMatrix` 만 좁히고 C++ 가 그대로 `v.fov=views[eye].fov` 를 compositor 에 제출하면 **그려지지 않은 영역을 표시하려 함 → 검은 띠/가장자리 끊김**. → **C# + C++ 양쪽 sync 필수**
  2. **compositor FOV sync (확정 위험)**: 1번과 같은 위험. ABI 가 양쪽 4 각도를 동일하게 좁혀야 함. `OpenXrHostConfig` 에 `FovScale` POCO 추가 + C++ `Host` struct 에 `float fovScale=1.0f` 추가 + `Configure(left,right,output,runtime,target,frameDump)` 의 signature 변경 또는 별도 `SetFovScale(float)` API 추가 + `OpenXrHost.cs` 가 `Init` 직전 한 번 호출
  3. **C++ ABI 안정성 (확정 위험)**: `HostStatus` (136 bytes) 와 `EyePose` (44 bytes) 는 `static_assert` 로 lock. C++ 측 변경은 struct 가 아니라 `Host` 자체에 새 필드만 추가하면 ABI 무영향 (`extern Host host`). 그러나 C# 측에서 `HostNative.SetFovScale` P/Invoke 신규 추가 필요
  4. **headset 없으면 검증 불가 (확정 위험)**: 현재 데스크탑에 headset/runtime 없음 → fps/시각 효과 측정 불가. 0.7.1 의 PLAYTEST-D104 도 아직 미실행. 구현 후 다음 빌드(0.8.0-dev) 의 PLAYTEST 에서 측정
  5. **0.7.1 호환 (확정 위험)**: FovScale 키가 없는 기존 `vhvr-openxr-host.json` 을 읽을 때 기본값 1.0 (변경 없음) 으로 동작하면 0.7.1 사용자에 영향 없음. `OpenXrHostConfig` 의 default `public float FovScale { get; set; } = 1.0f;` 로 처리 가능. 0.7 zip 사용자가 dll 만 교체하는 경우 `FovScale` 키 없는 JSON 이므로 1.0 = 무변경 = 안전


- **결정 (이번 세션, 0줄 코드 변경)**:
  - **이번 세션은 분석 + 문서만**. 호스트/OpenXrHostConfig/VhvrSettings.exe/Host.cpp 4 곳 모두 손대지 않음
  - **D-186-FovScale 결정을 docs/DECISIONS.md 에 정식 기록 (이 항목)**
  - **`0.8.0-dev` 마일스톤에 Q-FovScale 로 등록** (Q-MenuScroll 옆)
  - 다음 PLAYTEST (0.7.1) 에서 (1) 실제 `xrLocateViews` 가 보고한 4 각도 캡처 (OpenXrProbe.Receipt.Fov), (2) 시각적 over-render 영역 측정, (3) fps baseline 측정 — 그 데이터로 0.8.0-dev 의 기본값 (1.0 그대로 vs 0.95 같은 약한 축소) 결정
  - **0.7.1 의 다음 빌드 우선순위는 Q-MenuScroll (메뉴 스틱 ↑/↓ → 마우스 스크롤) 와 formselftest preview 보강 (TOOL-095) 유지** — D-180 이후 미해결
  - 만약 사용자가 **지금 강제로 Stage 3 (구현) 을 원하면 별도 세션에서**: (1) `OpenXrHostConfig.FovScale` POCO, (2) `HostNative.SetFovScale(float)` P/Invoke 신규, (3) C++ `Host` struct 에 `float fovScale=1.0f` + `Configure` 또는 `SetFovScale(float)` API, (4) `OpenXrHost.cs` 의 `StereoMath.Projection` 두 호출 + C++ 의 `v.fov=views[eye].fov` / `status.eyes[eye].{Left,Right,Up,Down}` 모두 scale 곱하기, (5) `install.ps1` 에 `FovScale = $1.0`, (6) `VhvrSettings.exe` 의 TrackBar/ComboBox (0.50~1.00, 0.05 step), (7) 회귀 스크립트 + `OpenXrProbe.Receipt.Fov` 에 적용 scale 별도 표시. **그 작업은 이번 세션의 결정에 포함하지 않음**

- **정직 한계 / 왜 보류가 옳은가**:
  - over-render 는 **OpenXR 표준 + runtime 의 lens 보정 메커니즘** 이고, 강제로 자르면 **검은 띠(가장자리 끊김) / barrel margin 손실로 인한 가장자리 왜곡 / reprojection 오류** 위험이 동반됨
  - 게임의 **PlayerController.target_fov/start_fov (docs/CAMERA-PIN.md, 40~60°)** 는 3rd person FP 의 고정 값이고, VR 모드에선 `eyes[i].projectionMatrix` 로 강제 override 되므로 무관
  - **0.7 의 D-148b/d148d 결과**: VDXR 45Hz 가 fps 상한, 게임 GPU 가 18.13 ms 사용 (즉 GPU bound 아님, vsync bound). FovScale 로 픽셀 수를 줄여도 fps 가 안 오를 수 있음 — VSynced 헤드셋의 특성. **실 fps 향상은 비 VSynced 또는 next-gen (120Hz) 헤드셋에서만 확인 가능**
  - **0.7.1 의 이득은 이미 6 stage 의 작은 결정 누적 (MSAA off, RenderFrames=1, RenderResolutionPolicy low, Anisotropic off 기본, etc.)** 이고, 그 위에 FovScale 을 더 얹는 것은 marginal. **먼저 0.7.1 의 기존 레버를 모두 켠 상태의 fps 가 어느 정도인지 측정 → 그 다음 FovScale 의 한계 효용 판단**이 정직한 순서

- **사용자에게 보고할 정리**:
  - "**맞습니다, 런타임이 HMD 표시 영역보다 넓게 렌더하라고 합니다** — 그게 OpenXR 표준이고 (lens distortion + barrel margin + IPD margin) 우리가 그 위에 FovScale 같은 키를 얹지 않은 이상 그대로 동작합니다"
  - "**0.7.1 의 호스트는 그 over-render 를 안 건드립니다**. config 에 FovScale 키가 0 개, 호스트 코드의 4 각도 곱셈 0 줄"
  - "**예상 이득**: FovScale 0.90 ≈ 22% 픽셀 감소, FovScale 0.80 ≈ 44% (1.1× 보고 가정). **fps 향상은 VSynced 헤드셋에선 점프(45→72), 비 VSynced 면 ~22% 단축**. 현재 VDXR 45Hz 상한이 잠근 환경이면 fps 자체는 안 오르고 GPU 시간 단축만"
  - "**5 위험** (검은 띠, C# C++ 양쪽 sync, ABI 안정성, headset 없으면 검증 불가, 0.7.1 호환) 때문에 **이번 세션은 0줄 변경, 다음 빌드(0.8.0-dev) 의 Q-FovScale 로 등록**. 사용자가 원하면 별도 세션에서 Stage 3 까지 갈 수 있으나 **먼저 0.7.1 의 기존 레버를 켠 fps 가 어느 정도인지 측정하는 게 정직한 순서**입니다"


### D-189 — 0.8 install.ps1 회귀 수정: `MenuFirst = $true` → `$false` (기본 OFF, D-178 의도 부합) + v0.8 release zip 재생성 (2026-10-17)

- **요청/배경(세션 시작 시 진단)**: v0.7 → v0.8 사이 install.ps1 `$config` 에서 30+ host flag(HandAim/HandUi/HandInput/HandHaptic 등)가 빠졌다는 진단이 들어왔다. 이를 `0.8 install.ps1` 에 복원하고 source-of-truth working tree 를 복원한 후 v0.8 release zip 을 재생성.

- **실제 코드 사실(2026-10-17 정직 분석)**: v0.8 의 `install.ps1` `$config` hashtable 에는 **HandAim/HandUi/HandInput/HandHaptic/HandFire/HandShot/HandShotTracer/AutonomousHeadAim/StockFireCadence/SnapTurnDegrees/Headset/HeadsetSeconds/GameplayWaitSeconds/Frames/GameplayOnly/Enabled/Output/UiVerticalOffset/WorldMarkers/MarkerKinds/MarkerMaxDistance/MarkerMaxCount/HideHudBackdrop/MenuFirst/EyeWidth/EyeHeight/EyePreset/EyeScale/DisableVSync/LeftWeaponTwistDeg/RightWeaponTwistDeg/RenderFrames/GameBackdropMode** + Stage 3.5 (FovScaleX/FovScaleY/EyeMsaa) + D-104 (Anisotropic) + D-153 (PrepareAhead/DeferredGpuWait) + FlameEndpointVfx/FlameEmitterPin + CaptureFirstFrames/MonitorCostProbe + D-186-extras (uiScale) 까지 **모든 host key 가 이미 들어가 있다**. `OpenXrHost.cs` 의 22+ 곳에서 `Config.HandAim/HandUi/HandInput/HandHaptic/HandFire/HandShot/HandShotTracer/AutonomousHeadAim/StockFireCadence/SnapTurnDegrees/Headset/HeadsetSeconds/GameplayWaitSeconds/Frames/GameplayOnly/Enabled/Output/UiVerticalOffset/WorldMarkers/MarkerKinds/MarkerMaxDistance/MarkerMaxCount/HideHudBackdrop/MenuFirst/EyeWidth/EyeHeight/EyePreset/EyeScale/DisableVSync/LeftWeaponTwistDeg/RightWeaponTwistDeg/RenderFrames/GameBackdropMode` + FovScaleX/FovScaleY/EyeMsaa/Anisotropic/FlameEndpointVfx/FlameEmitterPin/PrepareAhead/DeferredGpuWait/CaptureFirstFrames/MonitorCostProbe/uiScale 참조 모두 hit. **진짜 회귀는 단 하나**: `MenuFirst = $true` (line 95, v0.8 release zip 안의 install.ps1, 즉 `dist/VampireHuntersVR-0.8.zip` 의 그것). `PerfProfile/HapticAmplitude/HapticMilliseconds` 3개는 OpenXrHost.cs 가 한 번도 참조하지 않으므로 손 UI ray 와 무관 (어느 working tree 에도 없었다).

- **왜 `MenuFirst = $true` 가 회귀인가 (D-178 / BUGS.md TOOL-091 코드 사실)**: BUGS.md line 43-49 의 정직 분석. ① `OpenXrHost.Initialize()` 가 시작부에서 `if (Config.Headset && (!Config.GameplayOnly || !Config.HandAim || …)) throw new ArgumentException("invalid headset config: needs GameplayOnly, HandAim and HeadsetSeconds …");` 를 던진다. ② `VhvrExtras` 의 MenuFirst 전처리는 `GameplayOnly=false` 만 바꾸고 `Headset=true`/`HandAim=true` 는 건드리지 않는다 → **`Initialize` 가 예외로 죽고 `configured` 가 false 로 남아** 메뉴 패널/손 통합 모두 한 번도 실행 안 됨. ③ 같은 메서드의 `if (Config.HandAim) { if (!Config.GameplayOnly) throw "HandAim requires GameplayOnly; menu cameras have no weapon slots"; }` 때문에, **메뉴 모드에서 손/무기 통합을 켜는 것도 설계상 불가능**(메뉴 카메라에 무기 슬롯이 없음). 즉 `MenuFirst=true` 는 **호스트 초기화 예외로 실패**한다 — 실기기 미관측이지만 배포 DLL 디컴파일 + 코드 사실 기반. README-KO.md (모든 버전 0.6/0.6-final/0.7/0.7.1/0.8) 가 일관되게 "`MenuFirst` 기본 꺼짐" 으로 안내.

- **D-178 PHASE_PLAN 의 의도**: BUGS.md line 41 의 "TOOL-091 (MenuFirst 초기화 예외) — 소스에서 수정" + v0.7 의 install.ps1 주석("`MenuFirst` 기본 OFF") + README-KO.md 의 모든 표기("`MenuFirst` 기본 꺼짐 — 켜면 그 세션은 손/무기 통합이 없는 평면 화면이 됩니다") 가 일관되게 **`MenuFirst = $false` 가 기본값** 임을 명시한다. v0.8 의 `MenuFirst = $true` 는 이 의도 위반.

- **in-game 손 UI ray 의 의존성 (OpenXrHost.cs line 1034, 코드 사실)**: `if (Config.HandAim && !Config.MenuFirst) { … HandAimDriver.InstallUiRender(…); }` — `MenuFirst=false` 일 때만 `HandAimDriver.InstallUiRender` 가 호출돼 손 UI ray 가 보인다. `MenuFirst=true` 면 이 분기가 안 타고 `MenuFirst=true` 의 분기(line 1014) 가 따로 돈다 (게다가 위의 Initialize 예외로 전체 통합이 안 깔리는 경로가 됨). 즉 **사용자가 말한 "in-game hand UI ray 가 동작하도록" 의 정확한 1차 조건 = `MenuFirst = $false`**.

- **조치 (이번 세션, 0줄 호스트 코드 변경)**:
  1. `artifacts\unpack-0.8\VampireHuntersVR-0.8\install.ps1` 의 `MenuFirst = $true` (line 95) → `MenuFirst = $false` 로 변경 + D-188/D-189 라벨로 의도 명시(주석 확장, README 와 정합). 변경된 줄:
     - Before: `MenuFirst = $true`
     - After : `MenuFirst = $false`
     - install.ps1 size: 10,383 B → 10,864 B (481 B 증가, 전부 주석)
  2. `artifacts\pack-0.8.ps1` 신규 작성 (pack-0.7.1.ps1 의 패턴 답습, source-of-truth 검증 3종 추가): ① `MenuFirst = $false` 가 install.ps1 에 반드시 있어야 함 (D-189 fix 확인), ② v0.7 의 36 host flag + Stage 3.5 (FovScaleX/Y/EyeMsaa) + D-104 (Anisotropic) + 모든 retire 후 key 가 빠짐없이 있어야 함, ③ D-186/D-187 의 의도된 retire key (GraphicsQuality/ArmCooldownSeconds/AbilityFaceButton/AbilityFacePulseSeconds) 가 없어야 함. 3종 중 하나라도 어긋나면 `throw "REFUSE: …"` 로 pack 거부.
  3. `pack-0.8.ps1` 실행 결과: `OK size=35629301 sha256=DEE848EDA350930F0F38792928A39C44902BD89D22ED74D0A8EE13A154CACB07`. `dist\VampireHuntersVR-0.8.zip` 35,629,301 B (이전 0.8 zip 의 그것과 Stage0.dll/VhvrSettings.exe/dll/pdb 동일, install.ps1 만 갱신) + `dist\VampireHuntersVR-0.8.sha256.txt` 동일 SHA256 + 35,629,301 bytes 라인.
  4. `dist\VampireHuntersVR-0.8.zip` 내부 검증: ① `install.ps1` 안에 `MenuFirst = $false` 매치 정확히 1회(앞에 `=` 와 `$false` literal). ② `HandAim/HandUi/HandInput/HandHaptic/HandFire/HandShot/MenuFirst/FovScaleX/FovScaleY/EyeMsaa/Anisotropic/FlameEndpointVfx/FlameEmitterPin/PrepareAhead/DeferredGpuWait/CaptureFirstFrames/MonitorCostProbe/uiScale` 모두 매치 (Sort-Object -Unique). ③ `GraphicsQuality/ArmCooldownSeconds/AbilityFaceButton` 매치 0 (D-186/D-187 의 의도된 retire 유지). ④ payload dll 크기: `VampireHuntersVR.Stage0.dll` 420,864 B (Stage 3.5 = `FovScaleX/Y + EyeMsaa` 진입), `VhvrSettings.exe` 162,304 B / `VhvrSettings.dll` 42,496 B (D-186 의 settings tool 갱신), `VhvrSettings.pdb` 19,736 B (D-186 pdb 추가), `VhvrExtras.dll` 48,128 B (0.7/0.7.1 과 동일).

- **검증 (오프라인, deterministic)**:
  - `artifacts\pack-0.8.ps1` 자체의 3중 sanity (MenuFirst=false / 36 required keys / 4 retired keys absent) 가 모두 PASS
  - `dist\VampireHuntersVR-0.8.zip` 의 install.ps1 본문에서 `MenuFirst = $false` 1회 매치, `MenuFirst = $true` 0회
  - zip 의 entry 목록에서 `install.ps1` size 10,864 B (= source-of-truth working tree 와 동일)
  - 모든 D-186 retire key 미존재 확인

- **미검증(정직, headset 의존)**:
  - 실기기에서 `MenuFirst=false` 로 in-game 손 UI ray 가 보이는지는 **사용자 headset 검증 대기** (스킬의 "실제로 반응하기 전엔 확인 아님" 원칙)
  - Stage 3.5 의 FovScaleX/Y/EyeMsaa 의 fps/시각 효과는 미측정
  - D-186-FovScale 분석의 모든 추정 (1.1× over-render 가정의 픽셀 감소율) 은 headset 없이 추정한 값
  - `PerfProfile`/`HapticAmplitude`/`HapticMilliseconds` 3 key 는 `OpenXrHost.cs` 가 참조하지 않으므로 이번 결정의 손 UI ray 와 무관 (회귀 아님). 만약 다음 빌드에서 host 가 그것들을 사용하게 되면 그 시점에 install.ps1 에 추가.

- **한계/대안**: D-178 의 MenuFirst 자체(메뉴 모드 옵트인) 를 **소스에서 제거**할 수도 있지만, `VhvrExtras` 의 메뉴 패널/포인터가 그 분기를 사용하고 있고(D-180 실기기 피드백 반영), 실기기 검증이 안 된 상태에서 분기 자체를 없애는 것은 다운그레이드. **기본값을 `$false` 로 내리는 것**이 이번 세션의 정직한 결정.

- **이 결정이 pack-0.7.1.ps1 와 다른 점**: pack-0.7.1 은 sanity check 가 **0 개** 였고(D-104-iso-promotion 시점), pack-0.8 은 3중 sanity (D-188 + D-189 의 정직한 pack gate). 다음 빌드(0.8.0-dev) 의 pack script 도 같은 3중 sanity 를 기본으로 채택.

---

## D-190 (2026-10-17): v0.9 — "메인화면부터 VR + 손 UI ray" 출하 + D-189 진단 정정

- **목표 (사용자 확정)**: v0.9 는 ① `MenuFirst = $true` 출하, ② 메인화면부터 VR 진입, ③ 손 UI ray 동작, ④ Settings.exe 에서 GraphicsQuality 제거, ⑤ Anisotropic 토글 (기본 ON), ⑥ MSAA Off/2x/4x/8x, ⑦ FovScaleX/Y (0.50~1.00, 0.05 단위) — 기본값 `FovScaleX = 0.85`, `FovScaleY = 1.0`, `EyeMsaa = 4`, `Anisotropic = $true`. base = `artifacts\unpack-0.7\` (0.7 install.ps1 형식 + 0.7.1/0.8 dll 흡수 + 신규 6가지).

- **D-189 진단 정정 (정직)**: D-189 는 "`MenuFirst = $true` 는 `OpenXrHost.Initialize()` 예외로 실패한다 (TOOL-091)" 라고 적었으나, **소스를 직접 확인한 결과 이는 부정확**했다. 사실:
  1. **TOOL-091 (OpenXrHost.cs line 670) 은 `Config.Headset = true` (headless BepInEx 테스트) 일 때만 throw** 한다. 실사용자 환경(`Headset = false`) 에서는 throw 하지 않음.
  2. **`MenuFirst = true` + `HandAim = true` 는 line 1014 분기로 정상 동작**한다 (`if (Config.HandAim && Config.MenuFirst)`): native hands 가 session init 전에 enable 되고(`nativeHandsEnabledEarly = true`), 메뉴 레이저의 컨트롤러 포즈가 확보된다.
  3. **`HandAim requires GameplayOnly` (line 1042) throw 는 `MenuFirst = false` 분기 안에서만** 발생한다 (`if (Config.HandAim && !Config.MenuFirst)` → `if (!Config.GameplayOnly) throw`). `MenuFirst = true` 면 이 분기 자체를 타지 않음.

- **진짜 원인 확정 (코드 사실)**: 0.7/0.8 이 "메인화면부터 VR" 이 안 됐던 이유는 **`MenuFirst` config 가 아니라 `VhvrExtras` 의 메뉴 패널 설정 부재**에 있었다:
  1. `VhvrExtras` 는 자기만의 BepInEx config 파일(`local.vampirehuntersvr.extras.cfg`, GUID `local.vampirehuntersvr.extras`) 을 읽는다. 이 파일은 **0.7/0.8 install.ps1 이 한 번도 만들지 않았다** (payload 에 없었음) → BepInEx 가 기본값으로만 자동 생성.
  2. `ExtrasConfig.cs` 의 `MenuScreenFirst = config.Bind("MenuScreen", "MenuFirst", false, ...)` — **기본값이 `false`**. `MenuScreenPatch.StartOpenXrHostPrefix` 가 이 값과 무관하게 `GameplayOnly = false` 로 뒤집지만, `MenuScreen` (= true 기본값) + `MenuScreenFirst` 조합이 명시적으로 켜져야 메뉴 패널/레이저 의도대로 돌아간다.
  3. **0.7 의 `VhvrExtras.dll` (72,032 B) 와 0.8/0.9 의 `VhvrExtras.dll` (48,128 B) 는 완전히 다른 빌드**다 (D-178/D-180/D-181 반영본이 0.8 것). 0.7 배포본의 extras 가 최신 메뉴 패널 코드를 담고 있었는지는 미확인.

- **손 UI ray 의 진실 (D-189 의 2차 진단 정정)**: D-189 는 "`MenuFirst = false` 일 때만 `HandAimDriver.InstallUiRender` 가 호출된다" 고 적었으나, 이는 **line 1034 분기(게임플레이 우선 sitting) 만 본 것**이다. **`OpenXrHost.InstallHandAim()` (line 2246, 주석: "D-178: the deferred half of the hand/weapon install")** 가 `MenuFirst = true` 에서 **스테이지 진입 시** 호출되어 `handAim.InstallUiRender(source, eyes[0], eyes[1])` (line 2275) + `handAim.InstallWeaponRuntime(source, player)` (line 2289) 를 **모두 설치**한다. 즉 **`MenuFirst = true` = 메뉴 VR + 스테이지 진입 시 손/무기/UI ray 자동 설치**로, 사용자 요구와 완전히 양립한다.

- **조치 (이번 세션)**:
  1. `artifacts\build-0.9\VampireHuntersVR-0.9\` = `artifacts\unpack-0.7\` 복사본 + 다음 변경:
     - `install.ps1`: `version = '0.7'` → `'0.9'`, `Anisotropic = $false` → `$true`, `FovScaleX = 0.85` / `FovScaleY = 1.0` 키 추가(주석 포함, 0.8 형식), 마지막 메시지 "VR starts in the main menu", `$ownedRoots` 에 `'ModNotes.txt'` 추가.
     - `payload\BepInEx\plugins\`: 0.8 dll 교체 (`VampireHuntersVR.Stage0.dll` 420,864 B, `VhvrNativeHost.dll` 338,432 B, `VhvrExtras.dll` 48,128 B) 후 Stage0 는 소스(net6.0) 재빌드본으로 다시 교체(동일 크기 420,864 B — 코드 무변경 확인).
     - `payload\BepInEx\config\local.vampirehuntersvr.extras.cfg` **신규**: `[MenuScreen] MenuScreen = true`, `MenuFirst = true`, `DistanceMeters = 2.0`, `HorizontalFovDeg = 85`, `ControllerPointer = true`.
     - `ModNotes.txt` (루트 + payload) 신규: v0.9 변경사항 요약.
     - `VhvrSettings.exe/dll/pdb/deps/runtimeconfig` = 소스(net10.0-windows) 재빌드본(동일 크기 — SettingsForm.cs 는 D-186-FovScale/D-187 부터 이미 GraphicsQuality 제거 + Anisotropic 체크박스 + MSAA 콤보(1/2/4/8) + FovScaleX/Y 콤보(0.05 단위 11종) 를 갖추고 있어 **코드 변경 0 줄**).
  2. `artifacts\pack-0.9.ps1` 신규 (pack-0.8.ps1 의 3중 sanity + 2종 추가): ① `MenuFirst = $true` 매치, ② 40 host flag 필수, ③ retire key 부재(GraphicsQuality/ArmCooldownSeconds/AbilityFaceButton/AbilityFacePulseSeconds), ④ v0.9 기본값 4종 (`FovScaleX = 0.85`, `FovScaleY = 1.0`, `EyeMsaa = 4`, `Anisotropic = $true`), ⑤ extras cfg 필수 3종 (`MenuScreen = true`, `MenuFirst = true`, `ControllerPointer = true`).
  3. `pack-0.9.ps1` 실행 결과: `OK size=35632929 sha256=BBA1D5392344FB5DE411747E78B85F6DB36938F4991116DF975FFA07FF19CF3B`. `dist\VampireHuntersVR-0.9.zip` 35,632,929 B + `dist\VampireHuntersVR-0.9.sha256.txt`.
  4. zip 내부 검증: `install.ps1` 10,380 B, `ModNotes.txt` 996 B (루트+payload), `local.vampirehuntersvr.extras.cfg` 1,259 B, `VampireHuntersVR.Stage0.dll` 420,864 B, `VhvrNativeHost.dll` 338,432 B.

- **검증 (오프라인, deterministic)**: pack-0.9.ps1 의 5중 sanity 전부 PASS. zip entry 목록에서 install.ps1 / extras.cfg / ModNotes / dll 크기 전부 확인.

- **미검증 (정직, headset 의존)**:
  - 실기기에서 **메인화면부터 VR 진입**이 되는지, **메뉴 컨트롤러 레이저**가 보이는지, **스테이지 진입 후 손 UI ray**(`InstallHandAim` 경유) 가 보이는지는 **사용자 headset 검증 대기** (스킬의 "실제로 반응하기 전엔 확인 아님" 원칙). 코드 경로는 확인했으나 동작 미관측.
  - 0.7 Extras dll (72,032 B) 이 D-178 이전 코드였는지 여부는 미확인 (0.9 는 0.8 extras 48,128 B 사용).
  - FovScaleX = 0.85 의 fps/시각 효과 미측정.

- **Settings.exe UI 상태 (D-186/D-187 부터 이미 구현됨, 코드 변경 0 줄)**: GraphicsQuality 행 없음 ✅, Anisotropic 체크박스 ✅ (기본값은 install.ps1 의 `$true` 로부터 로드), MSAA 콤보 Off/2/4/8 ✅, FovScaleX/Y 콤보 0.50~1.00 (0.05 단위) ✅. 사용자가 요청한 "슬라이더" 는 **0.05 단위 콤보박스** 로 구현되어 있음 (11 종 선택지). TrackBar 대신 콤보박스인 이유: D-186-FovScale 구현을 재사용했기 때문. 필요 시 TrackBar 로 교체 가능.

- **한계/대안**: D-189 의 `MenuFirst = $false` 고정 결정은 **이번 D-190 으로 철회**한다 (진단이 부정확했음). 다만 TOOL-091 자체는 `Headset = true` 테스트에서 여전히 존재하므로, 향후 headless 테스트를 추가할 때는 `MenuFirst` 조합을 조심해야 한다. `InstallHandAim()` 의 "DUPLICATION IS DELIBERATE AND TEMPORARY" (line 2243) 주석처럼 inline/deferred 두 블록이 중복이므로, 다음 리팩터 때 통합 대상.

---

## D-191 (2026-10-17): v0.9-r2 — 메뉴 레이저 가시성 강화 + 스틱 스크롤 추가

- **사용자 피드백 (즉시 반영 요구)**: "UI 선택 RAY 가 안 보인다 — 선이 나와서 클릭할 수 있게 해야 한다" + "컨트롤러 스틱으로 메뉴화면 스크롤링 기능도 만들어라" + "있던 기능을 왜 빼느냐".

- **코드 사실 (확인 결과)**:
  1. **레이저/클릭/호버는 D-178/D-181 부터 이미 구현되어 있었다** (`MenuScreenRig.BuildLasers` + `UpdateHand`): LineRenderer 2개(양손), `EventSystem.RaycastAll` → `pointerEnter/Down/Up/Click` 디스패치, hover 하이라이트, 클릭 로그(`PointerClicks`). **코드에서빠진 것은 아니었다**.
  2. 다만 **레이저가 실기기에서 안 보였을 가능성**: 폭 8mm(`startWidth = 0.008f`)/3mm(`endWidth = 0.003f`) 의 헤어라인 + 알파 0.9. 헤드셋에서 체감상 "없는 것" 으로 보일 수 있는 두께였다.
  3. **스크롤은 정말 없었다** (grep 결과 `Scroll` 관련 0건). `NativeHandPose.StickX/StickY/StickClick` 필드는 네이티브에서 이미 전달되고 있었는데(`MenuScreenRig.HandPose` 구조체), 이걸 UI 스크롤에 연결하는 코드가 빠져 있었다. 이것이 "있던 기능을 뺀 것" 처럼 보인 정확한 지점.

- **조치 (D-191, `extras\VhvrExtras\Features\MenuScreenRig.cs`)**:
  1. **레이저 가시성 강화**: `startWidth 0.008 → 0.016f`, `endWidth 0.003 → 0.008f` (2배), `color (0.4,1,0.6,0.9) → (0.2,1,0.4,1)` (완전 불투명 진녹색), `startColor`(녹색) → `endColor`(흰색) 그라데이션 — 레이저가 패널에 닿는 지점이 흰색 점으로 보여 조준이 쉬워짐.
  2. **스틱 스크롤 신규** (`UpdateHand` 말미, D-191 주석): `pose.StickY` 의 절댓값이 0.15 (데드존) 를 넘고 레이저가 UI 를 가리키고 있으면 `pointer.scrollDelta = (0, stickY * 3)` 를 넣고 `ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.scrollHandler)` 전달 → Unity 의 `IScrollHandler`/`ScrollRect` 가 표준 방식으로 처리 (게임의 스크롤 목록 전부 동일 동작). 스틱 위 = 위로 스크롤. 진단용 `ScrollTicks` 카운터 추가.
  3. `VhvrExtras.dll` 재빌드 (net6.0, 0경고 0오류): 48,128 B → **48,640 B**. `payload\BepInEx\plugins\VhvrExtras.dll` 교체.
  4. `ModNotes.txt` (루트 + payload) 갱신: 레이저 강화 + 스틱 스크롤 항목 추가.

- **pack 결과 (5중 sanity 전부 PASS)**: 이전 `VampireHuntersVR-0.9.zip` 이 탐색기에서 열려 있어 삭제 불가 → `pack-0.9.ps1` 에 zip 잠김 fallback 추가(D-191) 후 **`dist\VampireHuntersVR-0.9-r2.zip`** (35,633,408 B) + `dist\VampireHuntersVR-0.9-r2.sha256.txt` (`4FAF23F4494F34086A562A124D7826152CDF415A1F421B29DE49EDC6057C433D`) 생성. **r2 가 최신본**. (원래 이름으로의 교체는 탐색기 잠금 해제 후 가능.)

- **미검증 (정직)**: 실기기에서 ① 레이저가 눈에 보이는지, ② 트리거 클릭이 먹는지, ③ 스틱으로 스크롤이 되는지는 **사용자 headset 검증 대기**. 스크롤 속도(프레임당 3 notches) 와 데드존(0.15) 은 미관측 추정값 — 사용자 피드백으로 조정 예정. `IScrollHandler` 전달은 게임의 UI 가 `ScrollRect` 를 쓴다는 가정 (Unity 기본 관례) — 게임이 커스텀 스크롤을 쓰면 별도 처리 필요.


## D-193 — 화염방사기 이펙트의 입자 생성 전 hand-pose 핀 연결 복구 (2026-10-17)

- **요청**: 화염방사기 이펙트가 머리 움직임을 따라 돌아가는 문제를 고치고 0.9 final 배포본 생성. Quest 이외 기기 지원은 후속으로 미룸.
- **원인(코드·영수증 교차확인)**: `FlameEmitterPin`은 설치/활성 상태였으나, `HandPose.HoldHandsBeforeParticles(PlayerController)`는 메서드만 있고 호출부가 없었다. 직전 `hand-aim.json`에는 `flameEmitterPin.enabled=true`, `active=true`, `attackAimUsed=8798`, 오류 0이면서도 `beforeParticles.samples=0`, `preCull.samples=0`; 따라서 late Tick/Busy pin은 돌았으나 intended pre-particle hold/pin 경로는 실행되지 않았다. Unity 입자 생성은 코드 기록상 PlayerController.Update와 LateUpdate 사이에 관측된 적 있음. 이것이 기능 누락의 직접적인 코드 원인이다.
- **수정**: `WeaponRuntimeBridge.AfterPlayerUpdate` (PlayerController.Update Harmony finalizer)에서 기존 head-gaze 후처리 다음 `HoldHandsBeforeParticles(__instance)` 호출. 이 메서드는 hand-owned controller mount의 마지막 tracked world pose(+ body delta)를 다시 적용한 뒤 `FlameEmitterStage.BeforeParticles` pin을 실행한다. 매 receipt에 `beforeParticlesCalls` 카운터를 기록한다. fire/damage/ammo/particle lifecycle은 변경하지 않음.
- **배포 통합**: `MenuFirst=false`를 v0.9 final install 기본값으로 설정해 gameplay-first hand/weapon integration 경로를 보장한다. MenuFirst/menu laser는 선택 가능한 옵션/코드로 남김. `MenuBlackout=false`; 검은 Quad는 코드에서 생성하지 않는다.
- **테스트**: Stage0 Release build 0 warning/0 error. `tests/FlameEmitter`의 55 fixture/offline policy + 새 source guard PASS. Native/game-specific VFX ordering and headset appearance are NOT yet accepted. No other controller profile bindings added (Quest/Touch only scope; non-Quest hardware deferred by user).
- **한계/다음 확인**: user headset test must verify during a sustained flamethrower stream: muzzle/effect stays with weapon while the head turns independently; compare `hand-aim.json` `flameEmitterPin.stats.beforeParticles.samples` and `beforeParticlesCalls` > 0. If nonzero but visual still tracks head, investigate ParticleSystem simulation space / per-particle velocity and which emitter is actually visible; do not add speculative particle-space changes.
- **Release name**: `dist\\VampireHuntersVR-0.9-final.zip` (same package retains v0.9 internal folder name).

## D-194 — 0.9-final의 메인 메뉴 VR 기본값 회복 (2026-10-17)

- **사용자 요구 (D-190, 재확인)**: 실행 직후 `MainMenu_2`에서 VR로 시작한다. 메인 메뉴부터 VR인 요구를 일반적인 "스테이지에 들어가면 VR" 경로와 혼동하지 않는다.
- **실패 증거**: 0.9-final 설치에서 Stage0 config `MenuFirst=false`, `GameplayOnly=true`. 실제 run receipt `%LOCALAPPDATA%\VampireHuntersVR\run\waiting.json`은 `WAITING_FOR_GAMEPLAY_CAMERA`, scene=`MainMenu_2`, candidates=`[]`였다. 해당 실행 시 BepInEx log에 extras menu-prefix와 panel hooks가 PATCH_APPLIED라고 기록됐지만, PATCH_APPLIED는 OpenXR 세션이 시작됐다는 증거가 아니다.
- **원인**: D-193 패키징 전 `MenuFirst=false`를 설정해 gameplay-first 경로로 바꿨다. 이는 명시된 D-190 목표와 모순되어, 메인 메뉴에서는 호스트가 게임플레이 카메라를 기다렸고 VR 초기화가 없었다. 이는 우선 설정상 확인된 원인이다; 실제 HMD 전환은 새 실행으로 검증해야 한다.
- **수정**: install.ps1의 호스트 기본 `MenuFirst=true`, shipped extras config 같은 값. Extras 기본 바인딩/설명도 `true`로 맞추고 `MenuScreenPatch` prefix는 `MenuFirst` 활성 시에만 `GameplayOnly=false`로 조정한다. 루트/payload ModNotes 및 README-KO 메인메뉴 안내를 정합화. package guard는 MenuFirst true 및 no-blackout default, notes caveat를 강제.
- **손/무기 전환 설명**: 소스에는 MenuFirst에서 `Initialize`가 `Camera.main`을 취득하고, stage PlayerController/camera를 찾으면 `TryEnterGameplay` → `InstallHandAim`으로 전환하는 경로가 있다. Extras prefix가 `GameplayOnly=false`만 적용하는 것도 이 경로와 일치한다. 하지만 설치된 Stage0 DLL은 배포 binary이므로 실제 HMD 동작은 아직 보장하지 않는다.
- **기존 설치 config 처리**: 현재 게임 config는 manifest와 비교해 사용자가 편집한 파일이다. 재설치 시 덮어쓰기 전에 backup을 남기고, 이전 manifest를 무력화/force 삭제하지 않는다.
- **검증**: Extras Release build 0 warnings/errors; Extras policy fixtures 62/62 PASS. 메뉴→HMD→stage transition은 미실행/헤드셋 미확인. 0.9-final ZIP 재생성 대기.

---

## D-195 — eye camera 합성의 `anchorRotation` 중복 적용 제거 (0.9-final SteamVR 뒤집힘 + 카드 회전) (2026-10-18)

- **사용자 보고 (HMD)**: 0.9-final + SteamVR + Quest 헤드셋에서 메인 메뉴/스테이지의 시야가 **상하/좌우 뒤집혀 보이거나 비스듬하게 누워** 있음. VDXR(Quest Link/VD)로 같은 0.9-final을 켜면 정상으로 보임. 메인 메뉴에서 **레벨업 카드에 포인터를 가져다 대면 카드가 반쯤 회전한 채로 보이는** 인상 (게임의 카드 회전 트윈이 비스듬한 panel plane에 매달려 있음).
- **실패 증거**: `%LOCALAPPDATA%\VampireHuntersVR\run\host.json` 첫 view 매트릭스(eye0) 분해 →
  ```
  row 0 right = (0, 0, 1)
  row 1 up    = (0, 1, 0)
  row 2 fwd   = (1, 0, 0)
  ```
  Unity 카메라는 row 2 forward가 `(0, 0, -1)`이어야 정상이므로 **카메라가 옆으로 90° 누워 있는 상태**로 OpenXR compositor에 submit되고 있었음. VDXR은 자체 보정 stage에서 한 번 더 정규화해 사용자 시야에는 우연히 정상이지만, SteamVR은 그 보정이 없거나 다르게 적용되어 거꾸로/비스듬히 보이는 것으로 설명됨. 카드 회전은 `MenuScreenRig.Tick()`이 `eyes[0].transform.rotation`을 panel plane의 정면으로 쓰는 데서 파생.
- **원인**: `src/core/OpenXrHost.cs` `Update`(L1238–1244)에서
  ```csharp
  Vector3 val2 = val + anchorRotation * new Vector3(array[0], array[1], array[2]);
  Quaternion val3 = anchorRotation * new Quaternion(array2[0], array2[1], array2[2], array2[3]);
  ```
  `StereoMath.UnityOrientation`이 OpenXR→Unity 축 보정으로 이미 쿼터니언을 `(-x, -y, z, w)`로 한 번 뒤집는데, 같은 단계에서 `anchorRotation`(이미 Unity-space 게임 카메라 회전)을 곱셈에 포함해 z축 180°가 누적됨. 위치 합성에서도 `anchorRotation * Vector3(unityPos)`이 회전 변환을 거친 위치를 더해 카메라가 거꾸로/오프셋된 위치로 자리 잡음.
- **수정**: eye 합성의 두 곱셈에서 `anchorRotation *` 제거.
  ```csharp
  Vector3 val2 = val + new Vector3(array[0], array[1], array[2]);          // 위치는 anchor + OpenXR→Unity 보정 eye 오프셋
  Quaternion val3 = new Quaternion(array2[0], array2[1], array2[2], array2[3]); // 회전은 StereoMath 한 번만 적용
  ```
  `anchorPosition`은 위치 오프셋, `anchorRotation`은 `HandAim.Tick`(L593) + `HandAimMath.SnapAnchor`(L620) 용도로 그대로 보존. dead code 아님.
- **대안 검토 (기각)**: (1) `StereoMath`의 quaternion sign 변경 — receipt에서 보정된 쿼터니언이 OpenXR 표준(eye가 +Z forward를 보는 yaw 회전)이라 OpenXR→Unity가 정확히 한 번 필요. sign 바꾸면 VDXR/SteamVR 둘 다 깨짐. (2) `MenuScreenPatch`에서 panel 회전만 보정 — 카드 회전 인상은 가려도 SteamVR 뒤집힘 증상은 해결 안 됨. (3) per-eye `FlipView` 토글 — 근본 원인 아님, 우회.
- **영향 평가**:
  - `eye camera worldToCameraMatrix == Unity transform` self-check (L1249–1260) — 이전엔 `anchorRotation`이 두 번 들어가서 Unity transform이 한 번 더 곱해진 결과와 일치했음. 수정 후엔 둘 다 `StereoMath.UnityView(val, unityRot)`로 들어가므로 self-check가 계속 통과함. 안 풀림.
  - HandAim 손 IK — `anchorRotation` 사용 유지. 영향 없음.
  - Snap turn (Q-MenuScroll/D-191) — `HandAimMath.SnapAnchor`가 `anchorPosition`/`anchorRotation`을 갱신, 이는 `L840–841` `Initialize`의 source.transform에서 시작. 변경 없음.
- **검증 (정적)**: Stage0 Release 0 warning/0 error. 새 Stage0 DLL SHA256 `05705C2FF7B37D3A226E389C4D5C5FF42AF4CCC7816E1154257504F0F5E2B1E9`. 0.9-final ZIP SHA256 `310801A44627715B4A3002BF27D73566800555FB62395BFF075ED388376FBBF7` (이전 `0B3080FBBF04646BA4D008DD820BB3C1B379B06FEFA935427057B8F265CC06E3`).
- **검증 (런타임, headset)**: 게임 1회 실행 후 `host.json` receipt의 `view` 매트릭스 row 2가 `(0, 0, -1)` 형태인지, SteamVR/VDXR 둘 다 정상 + 메뉴 panel 정면 + 카드 정렬인지 사용자 확인. 미실행 상태로 결정 마감. 다음 run에 함께 묶어 검증.

---

## D-192 — v0.9 마무리: 남은 TODO 일괄 구현 (2026-10-17 야간)

- **FOV 슬라이더**: Settings.exe의 FOV X/Y 콤보박스를 TrackBar(슬라이더)로 교체 — 50-100 = 0.50-1.00 0.05 단계, 값 라벨 표시. --selftest round-trip PASS, form-selftest controls=29 clipped=0.
- **EDF6 스타일 검은 배경 (MenuBlackout)**: 메뉴 패널 뒤에 큰 검은 quad(~20m)를 붙여 게임월드를 가림 = 사용자의 GOAL "검은 배경 + 평면 스크린" 근사. 패널과 함께 1회 배치/동결(D-178 규칙 유지), Dispose에서 정리. config [MenuScreen] MenuBlackout=true (끄기 가능). 참고: 진짜 EDF6 방식(present/backbuffer 캡처)은 VhvrNativeHost C++ 소스가 없어 불가능 — backdrop가 안전한 대안. 실기기 미검증.
- **스크롤 속도 config**: [MenuScreen] ScrollSpeed=3.0 (0.5-10, 프레임당 스크롤 notches). D-191의 하드코딩 3f를 config화.
- **범주별 UI 배율**: [Ui] ScalePaused=1.0 (paused ESC 메뉴 전용 배율, 기본 1.0 = D-181의 3.0m 유지, 권장 2.0). UiScalePatch가 paused 폭(3.0m)에도 별도 배율 적용. HUD/level-up은 기존 UiScale 유지.
- **TOOL-089(보조사격 Y/B) 정리**: 이미 구현됨 — WeaponRuntimeBridge line 144의 secondary/alt fire가 Y/B face button(slot.Face)에 매핑됨(D-186). BUGS.md의 TOOL-089(AbilityStickPolicy 패치 실패)는 D-186에 의해 기능이 재할당되어 폐기 대상.
- **payload 정리**: plugins\VhvrExtras.dll(51,200 B 최신) 단일화 (중복본 제거), VhvrSettings는 ModNotes 지시대로 install.bat 옆에 배치, extras cfg에 새 키 3개 추가(MenuBlackout/ScrollSpeed/ScalePaused).
- **미검증**: 헤드셋 전부 (검은 배경 / 스크롤 속도 / paused 배율). --form-selftest의 config 로드만 실험환경 경로 문제로 미확인 (UI/레이아웃은 통과).


---

## D-196 — D-195 follow-up: 메뉴 ray 90° 꺾임 수정 (v0.9.1 → v0.9.2 재수정) (2026-10-18)

- **문제 (0.9.1 회귀)**: D-195 패치 후 view matrix는 -Z forward 정상이지만, VDXR에서 메뉴 UI 조준 레이저가 약 90° 한쪽으로 꺾여 보임 (SteamVR은 정상). 0.9-final receipt host.json frame 0 (head 정지) view row 3 = [0, 0, -1, 43.7]이고 pose.rotation은 identity, 그러나 ray forward는 anchorRotation * Vector3.forward.
- **0.9.1 1차 원인 (A안 추측, 실패)**: extras/VhvrExtras/Patches/MenuScreenPatch.cs:150이 reflection으로 OpenXrHost.anchorRotation 필드를 읽어 그대로 Rig.UpdatePointers에 넘긴다. D-195는 view matrix에서 anchorRotation을 제거했지만 anchorRotation 필드는 살아있고 그대로 채워지므로, **view의 forward (eye rotation only)와 ray의 forward (anchorRotation × eye rotation)가 어긋남**이라 보고 A안(identity quaternion으로 교체)을 채택해 0.9.1에 반영. 그러나 0.9.1 빌드를 헤드셋에 올려 확인하니 ray는 다른 방향(오른쪽 90°)으로 90° 어긋난 채로 남음 — A안 가설이 부정확.
- **0.9.1 → 0.9.2 진단**: gameplay ray path인 `src/interaction/HandAimDriver.cs` `Update()`(HandAimDriver.cs:99) 호출에서 `HandAimMath.ToWorld(origin, Rotation(anchorRotation), handSample, ...)` 로 ray를 만듦. `Rotation(anchorRotation)`은 `anchorRotation = source.transform.rotation` (ArenaCamera의 Unity-space rotation, OpenXR→Unity axis correction + 게임 카메라 회전을 모두 포함)을 단순히 `(x,y,z,w)` 배열로 변환. gameplay은 이게 정상 동작. menu는 `MenuScreenRig.UpdateHand` → `PoseMathPolicy.TryToWorld(anchorOrigin, anchorRotation, ...)` → `Multiply(anchorRotation, localRotation)` → `Forward(rotation)`로 ray 만듦. **gameplay과 menu는 동일한 path**이지만 gameplay은 `source.transform.rotation`(OpenXR→Unity 보정 + 카메라 회전을 모두 포함) 을, menu는 A안으로 `identity` quaternion을 받았기 때문에 **menu에서 OpenXR→Unity 보정이 빠짐**. 0.9-final view row 2 = (1,0,0) (90° 회전된 forward) 증명은 `anchorRotation`이 카메라 정면을 회전한 Unity quaternion이라는 것. **0.9-final에서 ray는 90° 회전된 panel 좌표계에서 정상(=D-197 SteamVR 거꾸로)이고**, gameplay이 정상이었던 이유는 gameplay이 `source.transform.rotation`(=90° 회전 quaternion)도 같이 곱했기 때문.
- **수정 (A안 폐기, AnchorRotation(__instance) 복귀)**: MenuScreenPatch.cs:150에서 다시 `AnchorRotation(__instance)`을 그대로 `Rig.UpdatePointers`에 전달. menu ray는 gameplay path와 동일한 quaternion chain을 타게 됨 (`anchorRotation × localRotation × +Z`). D-195 패치(view에서 anchorRotation 제거)는 그대로 유지 — panel도 eye camera와 같은 Unity world의 카메라이고, panel forward는 view forward와 동일한 world axis를 가리키므로 둘이 별도 회전 없이 일치.
- **검증 (런타임, headset, v0.9.2)**: VDXR에서 menu ray가 panel 정면을 가리키는지 확인. SteamVR 거꾸로는 별개 issue(D-197)로 분리, v0.9.2에도 미포함. D-195 패치(view에서 anchorRotation 빠짐)가 SteamVR mirror layer에 어떻게 보이는지는 D-197에서 별도 추적.
- **빌드 결과**:
  - v0.9.1 (D-196 1차, A안 identity, 실패): VhvrExtras.dll SHA256 = 336719E58C86DE03CCB54DEE30F0F0BC0B0E665D597356973FF599E2DFEA62CC (49,664 B). 0.9.1-final.zip SHA256 = 65F26281369F3DEC69BD54FCE79E35FA95F3A5319FDAD9F61520FD40D26BDA94 (35,636,826 B). **ray 여전히 90° 꺾임 확인 → A안 폐기**.
  - v0.9.2 (D-196 2차, AnchorRotation 복귀): VhvrExtras.dll SHA256 = 09F017418D8E3F728DDF65E7AD1B1149A4D4A3FACCB14DCD69F64E77EE9E071B (49,664 B). 0.9.2-final.zip SHA256 = 00153509BBC7DDF58F7268D46FFBA84581306EDDD7AD7C9F3E97203239BB6D26 (35,625,161 B).
- **OpenXrHost.cs:1283-1288 anchorRotation dump 필드**: ray 정체 확인용으로 추가했으나 0.9.2 A안 폐기 결정에는 필요 없어짐. v0.9.2 receipt 한 번만 받아 `anchorRotation` 정체(예상: source=ArenaCamera의 transform.rotation) 확인 후 제거. v0.9.3 task.

---

## D-197 — SteamVR 거꾸로: OpenXR 런타임 시작 순서 문제 (v0.9.1, v0.9.2 미포함) (2026-10-18)

- **관찰**: VDXR flat-screen으로 시작 → SteamVR을 그 다음에 열면 게임 view가 거꾸로/좌우 반전되어 보임. SteamVR을 먼저 켜고 게임을 실행하면 정상.
- **원인 (D-195 패치와 무관)**: 0.9-final 적용 후 동일 DLL로 위 두 시나리오 모두 테스트. D-195 패치는 view matrix에서 anchorRotation을 빼고 eye pose rotation만 사용하는데, 이 view는 OpenXR LH 표준 (-Z forward)에 수치상 정확함. SteamVR이 거꾸로 보이는 시나리오는 SteamVR mirror layer가 VDXR의 다른 projection을 받아 자체 변환하다 어긋난 결과로 추정 — 우리 코드 책임 아님.
- **결정**: v0.9.1, v0.9.2 모두 미수정. 사용자 워크어라운드만 ModNotes.txt/QUESTIONS.md에 추가. D-195 패치 코드를 되돌리면(SteamVR-only path 추가) 회귀 위험 큼.
- **다음**: 0.9.2 receipt으로 VDXR menu ray 정상 확인 후, 별도 task로 SteamVR mirror / native OpenXR submit 레이어 분석.

---

---

## D-198 — 시작 직후 15초 VR 지연 제거: 고정 warm-up → 적응형 readiness 게이트 (2026-10-18)

- **관찰 (사용자)**: 게임을 켜면 곧바로 VR로 전환되지 않고 약 15초를 기다려야 한다. D-195/D-196 패치 **이전에도 동일**했다.
- **원인 (코드 사실, 확정)**: `OpenXrHost.Update()`의 첫 게이트가 `if (Time.unscaledTime < 15f) return;` 이었다. Unity 문서상 `Time.unscaledTime`은 *"the time in seconds since the start of the game"* 이며 **씬 로드와 무관하게 리셋되지 않으므로**, 호스트는 게임 프로세스 시작 후 **정확히 15초 동안 `Initialize()`조차 호출하지 않았다**. 사용자가 기다린 15초는 OpenXR 초기화도 게임 로딩도 아닌 **이 하드코딩 임계의 잔여 시간**이다.
- **왜 있었나**: `Initialize()`는 메인 카메라가 없으면 `throw new InvalidOperationException("no main camera")`, D3D11이 아니면 `throw "requires game D3D11"` 를 던지고, 이 예외는 `Update`의 `catch` → `Fail(e)` 로 **호스트 세션 전체를 종료**시킨다. 즉 "카메라가 아직 없는데 초기화하면 죽는다"를 막으려고 **준비 여부 대신 경과 시간**을 쓴 것이며, 15초는 근거 없는 상수였다(게임이 3초에 메뉴를 띄워도 15초까지 대기).
- **수정**: 고정 15초 제거. 매 프레임 `StartupReady()`가 `Initialize()`의 실제 전제조건만 검사한다 — ① 메인 카메라 부재(메뉴 퍼스트에서는 플레이어/스테이지 미시작과 동치)면 대기, ② `SystemInfo.graphicsDeviceType != Direct3D11` 이면 즉시 `Fail`(기다려도 바뀌지 않으므로 정직하게 실패), ③ 둘 다 충족하면 **그 프레임에 즉시 `Initialize()`**. 대기는 무한이 아니라 `Config.GameplayWaitSeconds`(기본 180 s) 예산으로 묶고, 1초마다 `[OPENXR_HOST] STARTUP_WAIT <reason> elapsed=..s budget=..s` 로그 + `waiting.json`(`verdict=WAITING_FOR_STARTUP_CAMERA`, `startupWait.reason/elapsedSeconds/budgetSeconds/candidates/cameraAttempts`) 영수증을 남긴다. 최초 통과 시 `STARTUP_READY elapsed=X.Xs scene=.. camera=..` 1회 로그.
- **영수증**: `host.json`에 **평면 필드** `startupReadySeconds`(호스트 생성→게이트 통과 초, `0` = 대기 없이 즉시 통과)와 `startupWait`(게이트가 마지막으로 막힌 이유 문자열; 비어 있으면 대기 없음) 추가 → **"Unity가 늦은 것"과 "OpenXR 초기화가 늦은 것"을 다음 receipt에서 분리**할 수 있다. (문서 초판은 `startup.readySeconds`/`startup.gatePassed` 라는 중첩 객체로 적었으나 실제 구현은 위 두 평면 필드다 — 코드가 정본.)
- **검증 (오프라인)**: `src\core\VampireHuntersVR.Stage0.csproj` Release **경고 0 / 오류 0**. 소스 가드 4개 신규: `source-no-fixed-warmup`(15f 임계 재도입 금지), `source-readiness-gate`(`StartupReady`가 카메라·D3D11 검사), `source-bounded-startup-wait`(GameplayWaitSeconds 예산), `source-startup-wait-receipt`(waiting.json + STARTUP_WAIT 로그). `tests\GameplayCamera` **35/35 PASS**, `tests\WeaponRuntime` **67/67 PASS**.
- **설치**: `backups\D-198-20261018T184654671\` 에 **원본 DLL**(2026-10-17 15:48:46, 421,376 bytes, SHA `0A2A9FAF…`)과 **중간 빌드**(`VampireHuntersVR.Stage0.dll.intermediate-18F991C0`, 426,496 bytes)를 같이 보존. 게임 폴더 `<GameRoot>\BepInEx\plugins\VampireHuntersVR.Stage0.dll` 의 **최종 설치본은 SHA `D1655D505DBA136110471955B1AC2DF67753C882701AC295A91DD2F854C21175`**(426,496 bytes, 2026-10-18 18:49:25)이며, 이는 현재 소스에서 다시 빌드한 결과와 **SHA 일치 확인**. (문서 초판이 적은 `18F991C0…` 는 최종 편집 전의 중간 빌드였다.) `VhvrExtras.dll`·config·`VhvrNativeHost.dll` 무변경(D-198은 관리 호스트 타이밍만).
- **남은 위험 (정직)**: 게임 자체가 15초 안에 메인 카메라를 만들지 않으면 여전히 그만큼 기다린다(이제는 근거 있는 대기). 또 `Camera.main`이 매우 일찍 존재하지만 아직 씬 전환 중이라면 초기화가 그 카메라를 잘못 캡처할 수 있는데, 이는 기존 D-181 `AdoptMenuCameraIfNeeded()` 경로가 다음 프레임에 재채택하므로 기존 15초 게이트와 동등 이상이다. **코드+오프라인 검증, 헤드셋 미확인.**
- **다음**: 사용자 1회 실행 후 `run\host.json`의 `startup.readySeconds`/`waitReason` 확인 — `readySeconds`가 1~3초면 Unity가 준비된 즉시 VR이 시작된 것. 10초 이상 + `waitReason=no main camera yet`이면 **게임이 그만큼 카메라를 안 만든 것**(추가로 줄일 여지 없음). `waitReason`이 비었는데 체감이 느리면 OpenXR 런타임 init이 병목이므로 그 축을 별도 조사.


---

## D-199 — 배포본 옆에 **공개 소스 트리** 분리: 게임 파생물 0건 + 경로 살균 (2026-10-18)

- **요구 (사용자)**: 0.9.3 배포 zip 과 함께, **게임 원본/디컴파일 산출물 없이 모드 소스만** 담고 GNU 라이선스를 붙인, GitHub 에 올릴 수 있는 소스 트리를 만든다.
- **결정 1 (포함/제외)**: 복사 대상은 1차 소스뿐이다 — `src/`, `extras/`, `tests/`, `config/`, `docs/`, `AGENTS.md`. 제외: `tools/interop`(**게임에서 파생된** IL2CPP interop DLL — 저작권상 재배포 불가), `tools/bepinex-be788`(BepInEx 3자 바이너리), `artifacts/`·`dist/`·`backups/`·`lib/`, 모든 `bin/`·`obj/`·zip·로그·캡처, `tests/archive/xri-d081`(현 `tests/XriMotion*` 의 대체된 중복본), 작성자 로컬 경로를 박은 패키징 스크립트. → **게임 원본 파일 0건**.
- **결정 2 (빌드 입력은 경로를 바꾸지 않고 문서로 채운다)**: `$(BepInExCore)`/`$(Interop)` 를 `tools/bepinex-be788/BepInEx/core`, `tools/interop` 그대로 두고 `tools/README.md`(+각 디렉터리 README)로 "직접 준비" 절차를 명시했다. 경로를 바꾸면 개발 트리와 공개 트리에서 `dotnet build` 가 서로 다른 파일을 참조해 재현성이 깨지기 때문이며, `-p:BepInExCore=…` / `-p:Interop=…` 오버라이드도 문서에 적었다. 공개 트리에는 해당 DLL 대신 README 만 두고 `.gitignore` 로 `*.dll` 을 차단한다.
- **결정 3 (라이선스)**: **GPL-3.0-or-later**. `LICENSE` 는 gnu.org 원문(35,149 B)을 스크립트가 직접 내려받아 넣는다(손으로 옮겨 적은 전문은 오탈자 위험이 있음). 릴리스의 `THIRD-PARTY-NOTICES.txt` 를 그대로 가져오고, 소스 트리에는 "3자 바이너리는 동봉하지 않는다"는 절을 덧붙였다. 벤더링을 유지한 3자 소스는 Khronos OpenXR **헤더뿐**(Apache-2.0)이며 `src/render/NativeHost/include/openxr/NOTICE.txt` 로 고지. SDK 의 `.vcxproj`/`.cmake` 생성물은 제외했다.
- **결정 4 (경로 살균 — 플레이스홀더 치환)**: 작성자 로컬 경로는 문서·스크립트에 그대로 두면 프라이버시 문제이자 재현 불가이므로 치환한다: `<RepoRoot>`, `<ModWorkspace>`, `<PackagesDir>`, `<GameDir>`, `<SteamApps>`(드라이브 문자 무관 — 두 번째 스팀 라이브러리 `G:` 포함), `<UserProfile>`, `<MetaXRSimDir>`, `<UnityDir>`, `<Downloads>`, `<CaptureDir>`. 반면 일반 제품 경로(`C:\Program Files\…`), 레지스트리(`HKLM:\SOFTWARE…`), 테스트 픽스처의 합성 경로(`<Scratch>`)는 **의도적으로 유지**했다. 치환 규칙과 플레이스홀더 뜻은 공개 트리 `docs/PUBLISHING.md` 에 공개했다.
- **검증 (오프라인, 3중)**: ① 패키징 스크립트 자체 게이트 — 바이너리 확장자 0, 게임 파생 파일명 0, 작성자 로컬 경로 0. ② 독립 재스캔 — 남은 드라이브 경로는 URL(`https://`)·레지스트리·`C:\Program Files` 뿐. ③ **공개 트리에서 직접 빌드/실행** — `tests/GameplayCamera` 빌드 성공 + **35/35 PASS**(D-198 가드 4종 `source-no-fixed-warmup`/`source-readiness-gate`/`source-bounded-startup-wait`/`source-startup-wait-receipt` 포함) → 복사본이 자기완결적이고 D-198 수정이 들어 있음을 증명. `dotnet msbuild -getProperty:Interop,BepInExCore` 로 두 csproj 의 참조가 **트리 내부 상대 경로**로 해석됨도 확인. 최종 트리: **266 파일 / 4,455,136 B**(63 파일 살균), 게임 원본·3자 바이너리 0건.
- **남은 위험 (정직)**: ① `src/core/Install-Stage0.ps1` 은 작성자 게임 경로와 고정 백업 해시를 박고 있어 공개 트리에서 그대로는 실행되지 않는다(README 에 명시, 사용자가 경로/해시를 고쳐 쓰거나 수동 설치). ② `tests/` 의 헤드셋 하네스 스크립트도 로컬 수정이 필요하다(CI 아님, 장치 체크리스트). ③ `src/render/NativeHost/Build.ps1` 은 OpenXR SDK 추출물을 `artifacts/openxr/extracted` 에서 찾으므로 사용자가 별도로 준비해야 한다(README 에 명시). ④ `docs/` 는 게임 내부 식별자·오프셋 등 **역설계 조사 기록**을 포함하므로, 게시 범위(문서 포함 여부)는 작성자가 최종 판단할 사항이다.
- **다음**: 공개 트리에서 `git init` → 첫 커밋(로컬 `user.name`/`user.email` 미설정이라 커밋은 사용자 신원 설정 후) → 원격 게시. 게임/헤드셋 검증(D-198 체감, D-197 SteamVR mirror)은 이 작업과 별개로 착석 1회가 여전히 필요하다.

---

## D-200 — VR 시작을 **인트로 이후**로: 시작 씬 스킵 + 씬 정착 게이트, 재채택을 조기 반환 앞으로 (2026-10-18)

- **증상 (사용자)**: 0.9.3 설치본에서 헤드셋이 **검은 화면**. 같은 기기·같은 설정의 0.9.2는 정상.
- **증거 (게임 로그 + 런 영수증)**:
  - 0.9.3 (19:22 실행): `STARTUP_READY elapsed=0.6s scene='Intro' camera='Main Camera'` → **0.62초에 인트로 씬에서 세션 시작**, `host.json`의 `sourceCamera='Main Camera'`.
  - 같은 실행: `windowFrames`가 **134에서 정지**했는데 XR 제출은 약 **495**프레임까지 진행 → 렌더 소스는 죽고 XR 루프만 돌았다(검은 화면의 기계적 정의).
  - 0.9.2 (18:02 실행, 정상): `scene=MainMenu_2`, `sourceCamera=ArenaCamera`, 세션 시작 **15.3초**(D-198 이전 고정 15초 gate가 우연히 메뉴 이후에 걸렸다).
  - Extras 로그: 월드 스페이스 메뉴 패널이 `MainMenu_2` 진입에서 화면 스페이스로 되돌아가고 재전환 없음.
- **근본 원인 (확정)**: D-198의 readiness 게이트는 "메인 카메라 + D3D11"만 검사하고 **어느 씬인지**는 보지 않았다. 인트로 씬에도 `Main Camera`가 있으므로 0.6초에 통과했고 그 카메라가 eye source로 채택됐다. 게임이 `Intro → MainMenu_2`로 씬을 바꾸면 그 카메라는 **씬과 함께 파괴**된다. 이때 메뉴 분기의 `if (source == null) return;`(당시 `OpenXrHost.cs:373-376`)이 **D-181의 `AdoptMenuCameraIfNeeded()`(379행)보다 먼저** 실행되어 유일한 복구 경로가 영구히 가려졌고(재채택 로그 0회), 이후 `PrepareCameras`가 호출되지 않은 채 죽은 소스로 제출만 반복됐다. 즉 옛 고정 15초는 "시간 벌기"가 아니라 **메뉴 씬까지 버티기**였고 D-198이 그 부작용을 함께 제거했다.
- **결정 1 (시작 씬 스킵 — 사용자 지시)**: `StartupSkipScenes`(기본 `"Intro"`, 쉼표 구분·대소문자 무시) 신규. 나열된 씬의 카메라는 eye source가 될 수 없고, 호스트는 그 씬을 벗어날 때까지 **같은 `GameplayWaitSeconds` 예산 안에서** 대기한다. 대기 사실은 `STARTUP_SKIP scene='Intro' StartupSkipScenes=Intro` 1회 로그 + `waiting.json`의 사유(`intro scene 'Intro' (D-200: waiting for the main menu)`)로 남는다. 빈 값이면 D-198 동작.
- **결정 2 (씬 정착 게이트)**: `StartupSettleSeconds`(기본 1.0초, 문서 범위 0..10) 신규. 활성 씬이 그 시간 동안 그대로였을 때만 그 씬 카메라를 채택한다. 이름을 모르는 전환 씬(로딩/스플래시)에도 같은 사고가 재발하지 않게 하는 일반 방어다. 게이트는 값을 0..10으로 **클램프**해 잘못된 설정이 대기를 무한정 늘리지 못하게 하고, `Initialize`는 범위 밖 값을 자체 오류(`invalid startup settle seconds: 0..10`)로 거부한다(침묵 수용 금지).
- **결정 3 (재채택 순서 교정 — 가장 중요)**: 메뉴 분기에서 `AdoptMenuCameraIfNeeded()`를 **`if (source == null) return;` 앞으로 이동**했다. 카메라를 잃는 순간(씬 전환)이 곧 그 조기 반환이 스킵되던 조건이므로, 순서를 바꾸면 같은 사고가 **구조적으로 불가능**해진다. `TryEnterGameplay` 직후에는 무해하다 — 게임플레이 소스는 정의상 `Camera.main`이라 참조 비교에서 즉시 반환한다.
- **구현 지점**: ⓐ `src\core\OpenXrHostConfig.cs` 키 2개(+D-200 주석, 기본 `Intro`/`1.0`) ⓑ `src\core\OpenXrHost.cs` — 상태 3필드(`startupSceneName`/`startupSceneSince`/`startupSkipLogged`), `StartupReady()`의 스킵·정착 분기, 신규 `StartupSceneSkipped()`/`StartupSceneSettled()`(둘 다 `[HideFromIl2Cpp]`), `Initialize()` 검증 2줄, `STARTUP_READY` 로그에 `waited='…'` 추가, 메뉴 분기 순서 교정 ⓒ `config\schema.json`(`x-host-tunables` 2키) ⓓ `tests\GameplayCamera\Program.cs` 가드 3종 ⓔ `tests\verify.ps1` 기대 개수 30 → **38** ⓕ 릴리스 템플릿 `artifacts\build-0.9.4\…\install.ps1`(+`ModNotes.txt` 2벌) ⓖ `artifacts\pack-0.9.4.ps1`.
- **검증 (오프라인)**: Stage0 Release **경고 0 / 오류 0** (`VampireHuntersVR.Stage0.dll` **428,032 B**, SHA256 `102E765DF9DF00726332CF12050DB597560EA097E00E0BD7F021C8488234D5AE`; 0.9.3은 426,496 B / `D1655D50…`). `tests\GameplayCamera` **38/38 PASS** — 신규 가드 `source-startup-skips-intro`, `source-startup-scene-settle`, `source-menu-adopt-before-return`(게임플레이 스캔 직후 **첫 `source == null` 반환보다 채택 호출이 앞**인지를 텍스트 순서로 검사). 회귀: HandAim **190/190**, WeaponRuntime **67/67**, VhvrExtras **62/62**, FlameEndpoint **138/138** 모두 기존 개수 그대로 PASS. `config\schema.json` 파싱 OK, 편집한 PS 4개 AST OK.
- **부수 교정 (정직)**: ① `tests\verify.ps1`은 GameplayCamera 기대 개수를 **30**으로 박고 있었는데 실제는 35였다 — 그 게이트가 조용히 어긋나 있었다(38로 교정). ② 0.9.3 패커의 "DLL에 `STARTUP_READY` 문자열이 있나" 검사는 .NET `#US` 힙이 **홀수 바이트 오프셋**에서 시작할 수 있음을 무시했다(0에서 시작하는 전역 UTF-16 디코딩은 정렬이 어긋나면 실제 문자열을 놓친다). 새 DLL에서 `STARTUP_SKIP`이 안 잡혀 바이트 패턴으로 직접 확인한 결과 **정상 포함**(UTF-16LE, 홀수 오프셋)이었다. 0.9.4 패커는 **두 정렬 + ASCII**를 검사하고 `intro scene` 문구까지 요구하며, **은퇴한 0.9.3 DLL 해시를 이름으로 거부**한다.
- **산출물**: `dist\VampireHuntersVR-0.9.4-final.zip` **35,641,669 B**, SHA256 `124C5FF469FC93D92903FA5FD2C34AAD204A62A5C79C031534CB3E6CE7952538`(+`.sha256.txt`). `artifacts\build-0.9.4\VampireHuntersVR-0.9.4\` = `install.ps1` `version='0.9.4'` + 설정 템플릿에 2키.
- **라이브 설치 (사용자 기기)**: 게임 미실행 확인 후 `BepInEx\plugins\VampireHuntersVR.Stage0.dll`만 교체(직전 0.9.3 DLL은 `backups\D-200-20261018T104445Z\VampireHuntersVR.Stage0.dll-pre-D200.dll`로 보존, SHA `D1655D50…`). 설치 매니페스트는 0.9.2 시점 그대로이며(작성자의 헤드셋 테스트 방식), **사용자 설정을 보존**하기 위해 `vhvr-openxr-host.json`은 초기화하지 않고 2키만 추가했다(EyePreset=custom / EyeScale=1.5 / EyeWidth=1920 / EyeHeight=1080 / GameplayWaitSeconds=600 / uiScale=1 / FovScaleX=0.85 그대로). 설치본 SHA = 빌드 산출물 SHA.
- **공개 소스 트리 (D-199 규칙 유지)**: `<RepoRoot>`를 `-Recreate`로 재생성 — **267 파일**(D-200 변경 + 이번 플레이테스트 스크립트 포함, 살균 63; 정확한 바이트 수는 패커 stdout 참조), 바이너리 0·게임 파생물 0·작성자 로컬 경로 0 PASS. D-200 변경(`OpenXrHost.cs`, `OpenXrHostConfig.cs`, `schema.json`, `tests\GameplayCamera`, `tests\verify.ps1`)이 복사본에 포함됨을 확인.
- **남은 위험·미검증 (정직)**: ① **헤드셋 재검증 필요** — 이 수정은 "인트로 동안 VR을 시작하지 않는다"는 행동 변경이며, 메뉴에서 정상 화면이 나오는지는 착석 1회로만 확인된다. 판정: `startupWait`가 `intro scene 'Intro'`, `scene=MainMenu_2`, `sourceCamera=ArenaCamera`, `startupReadySeconds` ≈ 메뉴 등장 시각. ② VR 시작이 이전보다 **늦어지는 것이 의도**다(인트로 길이 + 1초). ③ 릴리스 `install.ps1`은 재설치 시 설정 파일을 템플릿으로 덮어쓰므로 사용자 편집값은 사전 백업이 필요하다(이번 배포는 DLL만 교체해 회피, Q-D200-CONFIG-RESET로 등록). ④ 인트로 씬 이름이 게임 업데이트로 바뀌면 `StartupSkipScenes`도 갱신해야 한다(정착 게이트가 2차 방어).
- **다음**: 착석 1회 → 영수증으로 0.9.4 수락 판정 → 필요 시 `StartupSettleSeconds`/스킵 목록 조정.

---

## D-201 — 메뉴 화면·레이저를 VR 시작 시 HMD 정면 프레임으로 통일 (0.9.5 후보, 2026-10-18)

- **사용자 보고 (0.9.4 착석, 메뉴)**: "VR모드가 시작되면 내 시야가 오른쪽으로 90도 꺾여 있거든, 아마 그것 때문에 생기는 문제로 추정" + "UI ray가 이상한데". 요청: **"VR 모드가 시작될 때 HMD 정면의 화면 위치를 중앙으로 설정해"**. 질문: "0.9.2 final에서 업데이트 한 게 맞아?"
- **질문에 대한 정직한 답**: 0.9.2의 마지막 메뉴 ray 변경은 **D-196**(0.9.1 → 0.9.2 재수정)이다. D-196은 레이저를 **게임 카메라 프레임**으로 되돌려 "당시 동결돼 있던 패널"과 짝을 맞췄을 뿐이고, 눈(view)은 D-195 이후 **HMD 프레임**이라 두 프레임은 계속 90° 어긋나 있었다(즉 패널과 레이저가 **함께** 틀려 있어 서로에게는 맞아 보였다). D-196의 헤드셋 확인 문구는 "다음 run"으로 남았고 0.9.3은 검은 화면이라 메뉴가 보이지 않았다 → **0.9.4에서 메뉴가 처음 보이면서 이 어긋남이 처음 관측된 것**이지, 0.9.2가 고친 것도 0.9.4가 새로 만든 것도 아니다.
- **증거 (같은 19:57 실행의 두 파일이 수치로 일치)**:
  - `%LOCALAPPDATA%\VampireHuntersVR\run\host.json`: `view` row 2 = `(0, 0, -1)` + pose quaternion identity → **눈 카메라 = HMD 프레임**. 같은 영수증의 `anchorRotation` = `[0, -0.7071068, 0, 0.7071068]` = **Unity yaw −90°** (= `source.transform.rotation` = ArenaCamera).
  - `BepInEx\LogOutput.log`: `menu ui: panel frozen at world (74.29, 34.00, 43.75)`. 영수증 eye position `(76.25548, 32.466396, 43.753567)` − eye offset `(−0.031575, −1.5336038, 0)` = **anchorPosition `(76.287055, 34.000000, 43.753567)`** → 패널은 정확히 `anchorPosition + 2 m × (−1,0,0)` = **ArenaCamera의 정면**. 눈 기준 정면(+Z)이 아니므로 **정확히 90°** 오프셋이며, 사용자가 본 "90도"는 추정이 아니라 이 값이다.
- **근본 원인 (확정)**: `Camera.CopyFrom`은 카메라 변수뿐 아니라 **transform도 복사**한다(Unity 2022.3 문서: *"It will also set this camera's transform to match the other camera"*). `OpenXrHost.Initialize`/`AdoptMenuCameraIfNeeded`가 `eyes[i].CopyFrom(source)`를 호출하므로, **호스트가 세션 첫 프레임의 eye 포즈를 적용하기 전**에 extras의 `MenuScreenRig.Tick`이 실행되면 `eyes[0].transform`은 게임 카메라 포즈이고, D-178 규칙("한 번 배치 후 동결")이 그 프레임을 영구 고정한다. D-200이 채택 호출을 조기 반환 앞으로 옮기고 인트로 이후 세션을 시작하면서 그 창이 열렸다.
  - 동시에 **두 번째 불일치**: 메뉴 레이저는 `anchorRotation`(게임 카메라 회전)을 곱해 만든다(`MenuScreenRig.UpdateHand` → `PoseMathPolicy.TryToWorld`). 패널/눈은 HMD 프레임이므로 **레이저만** 90° 어긋난다 → "UI ray가 이상하다"의 정체.
- **수정 (extras만, Stage0/config 무변경)**:
  1. `MenuScreenRig.TryGetLiveHeadPose()` 신규 — 네이티브 `ReadStatus`의 `Frames > 0` + 유효 쿼터니언을 요구하고 `PoseMathPolicy.UnityPosition/TryUnityOrientation`으로 **Unity 축 head pose(트래킹 원점 기준)** 를 돌려준다.
  2. `MenuScreenPatch.UpdatePostfix` — 패널 배치 입력을 `eyes[0].transform` → **라이브 헤드 포즈 + anchorPosition**(호스트 eye 합성과 동일 식 `anchorPosition + UnityPosition(pose)`)으로 교체. 라이브 포즈가 없으면 **배치를 미루고 로그만** 남긴다(잘못된 포즈로 동결 금지). 배치 로그에 `headYaw`/`panelYaw`/`headPanelDelta=..deg` 추가 → **0.0이면 정면 중앙**(영수증 판정).
  3. 메뉴 레이저 프레임을 **HMD 프레임(identity)** 으로 통일 — 신규 config `[MenuScreen] PointerHeadFrame=true`. `false` = 0.9.4 동작이라 **리빌드 없이 헤드셋 A/B** 가능.
  4. **리센터**: 신규 config `[MenuScreen] RecenterChord=true` — **양쪽 스틱 동시 클릭** 시 그 순간의 헤드 포즈 앞으로 패널 재배치(같은 배치 경로 재사용, 엣지 검출).
- **대안 검토**: (a) 눈 합성에 `anchorRotation` 재적용(D-195 되돌리기) — 월드 전체가 게임 카메라 프레임으로 정렬되지만 **D-195가 고쳤던 SteamVR 상하/좌우 뒤집힘(D-197) 재발 위험** + 게임플레이 월드까지 90° 회전. 이번 증상은 메뉴 패널/레이저이고 게임플레이는 정상 보고이므로 건드리지 않았다. (b) 레이저를 게임 카메라 프레임으로 유지(D-196 유지) — **증상 지속**이므로 기각. (c) `HandUi`/HUD(게임플레이 경로) 변경 — 정상 보고 경로라 무변경.
- **검증 (오프라인)**: `extras\VhvrExtras` Release **경고 0 / 오류 0** → `VhvrExtras.dll` **53,760 B**, SHA256 `52E6707290D430F25615CF50EFAA7ADA3013DD21527FB7861D999BFDDB1CA533`(직전 0.9.2 이후 `09F01741…`, 49,664 B). `tests\VhvrExtras` **72/72 PASS**(신규 10), `tests\verify.ps1` 기대값 **62 → 72** 교정. 신규 fixture가 이번 실행 수치를 회귀 테스트로 고정: `menu-panel-stale-frame-reproduces-the-0.9.4-log`(패널 `(74.287, 34.000, 43.754)`), `menu-stale-panel-is-90-degrees-off-the-head-forward`(90.0°), `menu-game-camera-frame-is-90-degrees-off-the-view`, `menu-panel-head-frame-sits-in-front-of-the-eyes`.
- **라이브 설치 (사용자 기기)**: 게임 미실행 확인 후 `BepInEx\plugins\VhvrExtras.dll`만 교체(Stage0 DLL·config 무변경 → 사용자 설정 보존). 직전 DLL은 `backups\D-201-20261018T111028Z\VhvrExtras.dll-pre-D201.dll`(SHA `09F01741…`)로 보존. 설치본 SHA = 빌드 SHA.
- **새 설정 (BepInEx가 자동 추가, 기존 값 유지)**: `[MenuScreen] PointerHeadFrame=true`, `[MenuScreen] RecenterChord=true`. DistanceMeters=2 / HorizontalFovDeg=85 / UiScale=1 / EyeScale=1.5 등 그대로.
- **남은 위험·미검증 (정직)**: ① **헤드셋 미검증**. ② 배치 시점의 헤드 방향이 곧 "정면"이 되므로 메뉴가 뜨는 순간 고개를 크게 돌리고 있으면 그 방향이 정면이 된다 → 리센터(양쪽 스틱)로 즉시 교정. ③ `PointerHeadFrame=false`는 0.9.4 동작을 그대로 재현하는 A/B용이며 기본값이 아니다. ④ 게임플레이 ray/손 IK는 대상이 아니다(게임 카메라가 `WeaponRuntimeBridge`로 헤드 포즈를 받아 구동되므로 정상, D-196 근거). ⑤ **게임 월드(아레나 배경)** 는 여전히 HMD 프레임이다(Q-D201-WORLD).
- **다음**: 착석 1회 → (1) 메뉴 정면 중앙, (2) 레이저가 컨트롤러 방향, (3) 로그 `headPanelDelta=0.0deg`, (4) 필요 시 리센터. 통과하면 0.9.5로 패키징 확정, 불일치면 `PointerHeadFrame` A/B 1회.
- **키 설정 표 (신규)**: `docs\CONTROLS.md` — 현재 설치본의 실제 컨트롤러 매핑을 **한국어 표 + English 표**로 정리(런타임 소스 근거: `HandInput.cs` D-114/D-184, `WeaponRuntimeBridge.cs` D-186, `HandUi.cs` D-105, extras D-191/D-192/D-201). `docs\INPUT-MAP.md`는 설계 문서로 남기고 상단에 이 문서를 가리키는 한 줄을 추가.
- **공개 소스 트리 (D-199 규칙)**: `artifacts\pack-public-source.ps1 -Recreate`로 `<RepoRoot>` 재생성 — **269 파일, 4,541,019 B, sanitize 64**, 검증 `PASS (no binaries, no game-derived files, no author-local paths)`. D-201 변경(extras 4파일, `tests\VhvrExtras\Program.cs`, 이 플레이테스트 스크립트, 문서 6개) 포함 확인. `-Recreate`가 `.git`을 지우므로 `git init -b main` + `git add -A`로 재스테이징(커밋은 로컬 git identity 설정 후 — Q-D201과 함께 사용자 결정).
- **헤드셋 확인 (사용자 보고 + 같은 실행 로그, 2026-10-18 20:31)**: "메뉴는 잘 나오는데" → **A1(정면 중앙) 통과**. 로그도 일치한다: `menu ui: panel frozen ... headYaw == panelYaw` (= `headPanelDelta 0.0deg`), 그리고 **리센터 코드(양쪽 스틱)가 실제로 3회 사용**되어 `menu ui: recenter (both thumbsticks) re-placed the panel in front of the head`가 남았다 → **B1 통과**. A2(수평)·A3(레이저가 컨트롤러 방향)·A4(레이저로 버튼 클릭)는 이번 보고에 없어 **미확인**으로 남긴다(다음 착석에서 `PLAYTEST-D-202-HAND-FRAME-KO.md` C1/C2로 함께 확인).


## D-202 — 인게임 손/조준의 합성 프레임을 눈과 동일하게 (BUG-202 손 90° 꺾임 수정, 0.9.5 후보) (2026-10-18)

- **사용자 보고 (0.9.4 + D-201 착석, 게임플레이)**: "야 메뉴는 잘 나오는데 게임 안에서 손이 오른쪽으로 90도 꺾여있다." 즉 **D-201(메뉴)은 통과**, 인게임 손/무기 방향만 틀어졌다.
- **증거 (같은 20:31~20:32 실행의 영수증 3종이 수치로 일치)**:
  - `run\host.json` 마지막 view row: `orientation = (0,0,0,1)` → **눈은 HMD(identity) 프레임**이고, 같은 레코드의 `anchorRotation = (0, 0.7071, 0, 0.7071)` = **Unity yaw +90°**(19:57 실행은 −90°). 값이 실행마다 달라지는 이유는 §근본 원인에 있다.
  - `src\core\OpenXrHost.cs:1489-1490`(눈 합성): `val2 = val + (eyePos)`, `val3 = Quaternion(eyeRot)` → `anchorRotation` 미사용(D-195 그대로).
  - `src\core\OpenXrHost.cs:659`(손): `handAim.Tick(source, origin, anchorRotation, ...)` → **손만 게임 카메라 회전을 곱한다.**
  - `run\hand-aim.json`(우손): `state=Driving`, `drivenFrames=1203`, `gameOverwrites=0`, `muzzle="Muzzle Flash Spawn"`, **`muzzleToHandForwardDegrees=0`** → 드라이버는 "정면 정렬 완료"라고 보고하는데, 그 정면은 anchor 프레임의 손 방향 = **플레이어가 가리키는 방향의 90° 옆**이다. 사용자가 "손이 꺾였다"고 본 그대로다.
- **근본 원인 (확정)**: D-195가 **눈** 합성에서 `anchorRotation`을 제거할 때 "HandAim 손 IK — `anchorRotation` 사용 유지. 영향 없음"이라고 적었는데, 그 가정이 틀렸다. 이 게임의 eye source는 `Main Camera` = **플레이어 프리팹의 자식**(`run\gameplay-camera.json`: `cameraPath=AllThingsBaseLevel/Prefab_Player_Level1/Main Camera`, `sourceCamera='Main Camera'`)이므로 `source.transform.rotation`은 **플레이어의 현재 facing**이고 아레나 기준 ±90°가 흔하다. D-195 이후 **눈 = HMD 프레임 / 손 = 플레이어 yaw 프레임**으로 갈라졌고, 갈라진 각도가 정확히 보고된 90°다. D-201이 "게임플레이 손 IK는 대상이 아니다(정상)"라고 남긴 판단의 근거는 "게임 카메라가 `WeaponRuntimeBridge`로 헤드 포즈를 받는다"였는데, 그 경로(`ApplyAutonomousGaze` → `source.transform.SetPositionAndRotation(headPos, headRot)`)는 **게임 카메라 자체**를 헤드 프레임으로 만들어 줄 뿐 **손 합성의 `anchorRotation` 곱셈은 건드리지 않는다** — 그래서 그 판단은 이번 보고로 반박됐다.
- **수정 (Stage0만, 기본값이 곧 수정)**:
  1. 신규 config `HandPoseFrame`(기본 `"Head"` / 대안 `"Anchor"`). `Head`는 눈과 **동일한 identity 프레임**으로 손/조준을 합성하고, `Anchor`는 0.9.4 동작(게임 카메라 회전 곱)을 그대로 재현한다. 두 값 외에는 `Initialize`가 `invalid hand pose frame: Head|Anchor`로 **거부**한다(조용한 기본값 금지 — 침묵 폴백이 이 버그의 씨앗이었다). 시작 시 `HAND_FRAME Head|Anchor` 1회 로그.
  2. `HandAimDriver.Update(...)`의 파라미터를 `anchorRotation` → `poseBaseRotation`으로 바꾸고 **조준 ray와 무기 회전을 이 한 값**으로 합성한다. `Camera source`는 이 경로에서 더 이상 읽지 않는다(프레임은 이 값 하나에서만 온다).
  3. **rest 캡처도 같은 프레임 + 캡처 순간의 포즈로** (신규 `HandAimMath.PoseRotation`, `HandAimDriver.RestCaptureFrame`). 기존 `Relative(source.transform.rotation, container.rotation)`은 (a) 드라이브와 **다른** 프레임을 쓰고 (b) 손의 **절대** 자세를 나누지 않아, 무기를 넘겨받는 프레임에 무기가 점프하고 롤이 손의 절대 자세에 묶였다. 이제 `Relative(pose_capture, container)`다. 영수증에 `restCapturedWithPose`를 추가해 "포즈 없이 캡처된 경우"를 정직하게 구분한다.
  4. **스냅 턴은 헤드 프레임에서 이미 깨져 있었다**(BUG-203) → 이 빌드에서는 헤드 프레임에서 **거부 + 경고 1회**(`SNAP ignored on the head frame ...`). anchor yaw를 돌려도 눈은 그 값을 쓰지 않으므로(D-195) 시야가 돌지 않고 옆으로 튀기만 하기 때문이다. 올바른 구현(눈 합성 view-yaw 오프셋 + 눈 오프셋의 헤드 기준 회전)은 **미착수**이고 BUG-203으로 분리했다. `HandPoseFrame=Anchor`는 레거시 경로를 남겨 A/B가 가능하다.
- **대안 검토 (기각)**: (a) 눈 합성에 `anchorRotation` 재적용(D-195 되돌리기) — D-195가 고쳤던 SteamVR 뒤집힘(D-197) 재발 위험 + 월드 전체가 플레이어 yaw로 회전 → 기각(D-201과 동일 판단). (b) 무기 롤 다이얼로 보정 — 90°는 롤이 아니라 **방향**이고 다이얼은 muzzle 축 회전만 하므로 원리적으로 못 고친다. (c) extras에서 패치 — 손 합성은 Stage0 경로이고 `anchorRotation`은 호스트 필드라 패치 지점이 아니며, 게임 카메라(yaw)와 손 양쪽을 이미 소유한 Stage0가 맞는 위치다.
- **검증 (오프라인)**: `src\core\VampireHuntersVR.Stage0.csproj` Release **경고 0 / 오류 0** → `VampireHuntersVR.Stage0.dll` **430,592 B**, SHA256 `34AA247600E3DEA20895F77A49A005B296B950CCFE43EA1483619ECB765DE525`(0.9.4는 428,032 B / `102E765D…`). 회귀: `tests\HandAim` **203/203 PASS**(0.9.4는 190, 신규 13 = math 8 + source 5), `tests\GameplayCamera` **38/38**, `tests\WeaponRuntime` **67/67**, `tests\VhvrExtras` **72/72**, `tests\FlameEndpoint` **138/138**, `tests\ShotScopes` **28/28**. 신규 fixture가 이번 실패 메커니즘을 회귀로 고정한다: `d202-anchor-frame-controller-direction-is-90-degrees-off-the-view`(정확히 90.0°), `d202-head-frame-controller-direction-is-the-view`(0°), `d202-rest-capture-divides-out-the-capture-pose`, `d202-head-frame-takeover-keeps-the-authored-rotation`(넘겨받을 때 회전 점프 없음), `d202-pose-rotation-is-the-base-times-the-pose`, `source-hand-pose-frame-key-and-default`, `source-hand-frame-selects-the-composition-base`, `source-rest-capture-uses-the-same-frame-as-the-drive`, `source-head-frame-snap-is-refused-loudly`, `source-schema-lists-the-hand-pose-frame`.
- **라이브 설치 (사용자 기기)**: 게임 미실행 확인 후 `BepInEx\plugins\VampireHuntersVR.Stage0.dll`만 교체(extras DLL은 D-201 그대로). 직전 DLL은 `backups\D-202-<stamp>\VampireHuntersVR.Stage0.dll-pre-D202.dll`(428,032 B / `102E765D…`)로 보존. 설치본 SHA = 빌드 SHA.
- **새 설정 (사용자 파일에 명시)**: `vhvr-openxr-host.json`에 `"HandPoseFrame": "Head"`를 추가했다(기존 값 전부 보존). 키가 없어도 코드 기본값이 `Head`라 수정은 자동 적용되지만, A/B를 **파일 편집만으로** 할 수 있게 명시해 두었다. `config\schema.json`의 `x-host-tunables`에도 등록(enum Head/Anchor).
- **남은 위험·미검증 (정직)**: ① **헤드셋 미검증** — 판정은 착석 1회로만 가능하다(`PLAYTEST-D202-HAND-FRAME-KO.md`). ② 무기 **롤**이 이제 손의 상대 회전을 따른다(캡처 포즈를 나누는 새 수식의 결과) → 영수증 `weaponTwistResidualDegrees`로 확인하고, 5°를 넘으면 `Left/RightWeaponTwistDeg` A/B(Q-D202-ROLL). ③ 스냅 턴은 헤드 프레임에서 동작하지 않는다(BUG-203, 경고 로그로 명시). ④ 왼손 슬롯은 이번 실행의 Playground 씬에서 무기를 못 찾아(`weaponsInSlot=0`) 확인 불가. ⑤ **탄 착탄(적중)은 이 수정의 대상이 아니며 검증되지 않았다** — 수정은 손/조준의 프레임을 눈과 일치시킨 것이고, 게임 자체 조준은 이미 헤드 프레임(`ApplyAutonomousGaze`)이라 두 경로가 이제 같은 프레임이 된다.
- **다음**: 착석 1회 → (A) 손/무기가 컨트롤러를 따라오는가, (B) 리티클·발사 방향, (C) 메뉴 회귀(D-201), (D) 불일치 시 `HandPoseFrame=Anchor` A/B 1회. 통과하면 0.9.5 패키징 확정.


## D-205 — 엔지니어 터렛 건설 입력의 정체: 확정 = `Fire`, 종류 전환 = 특수능력 재입력 (분석 확정, 수정 미착수) (2026-10-18)

- **계기 (사용자)**: 터렛 건설 UI는 열리는데 왼손 X로 설치가 안 된다. "엔지니어는 4종류 중 선택해야 하는데, 우리 매핑은 Xbox 기반이라 종류를 못 고르는 게 문제인가?"
- **조사 방법 (게임 파생물은 저장소에 넣지 않는다)**: ① 게임 로컬라이제이션 표에서 엔지니어 터렛 문구를 읽는다(`resources.assets`의 키/문구 문자열) ② 게임 자신의 InputActionAsset 문자열 표에서 `path → groups → action` 순서를 읽는다(`sharedassets2.assets`, `PlayerInputActions_Arena*`) ③ 이미 확보한 `config\bindings.json`(게임 키바인딩 화면 스크린샷)과 교차 검증. 영수증: `artifacts\_d204-input-bindings.txt`, `artifacts\_d204-loc-turret.txt`, `artifacts\_d204-turret-strings.txt`.
- **확정 1 (종류 전환)**: 게임 문구가 "Press **Special Ability** to enter build mode **or switch blueprints**"라고 직접 말한다 → 종류 선택은 **좌/우 스틱이 아니라 특수능력 재입력**이다. 모드는 이미 오른쪽 스틱 아래를 `<Gamepad>/buttonEast`로 보내고 그 입력으로 UI가 열렸다(사용자 관측) → **전환 축은 이미 배달되고 있다.** 각 종류는 **스킬 해금**이므로(`SkillType.FLAME_TURRET/LASER_TURRET/HEAL_TURRET`) 보유하지 않은 종류는 전환 대상이 아니다.
- **확정 2 (설치 = 실제 막힌 곳)**: 게임의 설치 확정은 **`Fire`** 액션이다(게임패드 `rightTrigger`, 키보드 `X`, 마우스 LMB). D-184는 왼손 X를 **`SelectInput`(=`<Gamepad>/select`, View)** 에 묶었다(`src\interaction\HandInput.cs:184`, `:194`) → 의도는 맞고 액션이 틀렸다. 게다가 모드는 `Fire` **액션**을 아예 올리지 않는다: 발사는 필드/게임 메서드로 처리하고(`WeaponRuntimeBridge.cs:108-112`, `HandFire.cs`), 가상 패드는 트리거를 항상 0으로 보낸다(`HandInput.cs:196`).
- **미확정 축 (위치)**: `PlayerSpecialVfx.set_visual_turret(int, ref Vector3, bool) : bool` → `set_turret(int, Vector3)`는 월드 좌표를 받는다. 평면 게임의 좌표 소스는 마우스(`Point`=`<Mouse>/position`, `Click`)이고 **그 액션은 Keyboard&Mouse 그룹 전용**인데 모드는 스킴을 Gamepad로 고정한다 → **VR에는 포인터가 없다.** 고스트 미리보기가 안 보이면 어떤 버튼도 설치할 수 없다.
- **수정 방향 (미착수, 사용자 승인 대기)**:
  1. (권장) `PlayerController.is_turret_mode`(interop 프로퍼티)가 true일 때만 왼손 X를 **Fire로 라우팅** — 게임 자체 액션 콜백 `PlayerInputController.FirePressedInput(bool)`(+ 필요하면 패드 `rightTrigger=1`)로 `Fire` 액션과 필드를 **둘 다** 만족시킨다. 건설 모드 밖에서는 지금의 `Interact`(Z) 동작 유지 → 상호작용과 충돌 없음(D-184 중첩 승인 유지).
  2. (대안) 위치가 문제면 모드가 손 조준 지면 히트를 `set_visual_turret`/`set_turret`로 직접 공급 — 게임의 유효성 검사를 우회하므로 최후 수단.
- **검증 (오프라인)**: 자료 조사만 수행, 코드 변경 없음(Stage0/extras DLL 무변경). 다음 착석 판정은 `docs\playtests\PLAYTEST-D204-TURRET-KO.md`.



## D-206 — 엔지니어 터렛 4키: 좌그립=이전 종류, 우그립=다음 종류, 양그립=Fire 확정, `TurretCycle` 설정 (2026-10-18)

- **계기 (사용자 확정 지시)**: D-205 분석을 받아 "4키 조작"의 정체를 확정하고 **지금 코드 그대로 설치+검증까지 끝낸다**고 사용자가 지시. 코드는 이미 구현돼 있어 새로 쓴 코드 없음(설치+검증만).
- **4키 (확정된 정체)**:
  1. 좌그립(왼손 그립 누름→뗌) = **이전 종류**(`d206-left-release-steps-back`, 누르고 있는 동안은 스텝 없음).
  2. 우그립(오른손 그립) = **다음 종류**(`d206-right-release-steps-forward`).
  3. 양그립(양손 그립 동시 = 코드) = **Fire 확정 = 설치**(`d206-chord-confirms-and-steps-nothing`, `d206-confirm-is-the-game-fire-action`) — 게임 자체 `PlayerInputController.FirePressedInput`로 라우팅해 설치 확정을 낚아채지 않음.
  4. `TurretCycle` **설정키**(기본 `Stick`, 대안 `Dpad`/`Special`/`Off`) = 종류 전환을 올릴 입력 표면 선택 + `Off`면 전 끔. `Stick`이면 D-pad 팔이 게임 자체 보드 무기 전환용으로 남음.
- **구현 위치 (3곳 일치 확인)**: `src\interaction\HandInput.cs`(`TurretCycle` 필드·`is_turret_mode` 게이트·그립 히스테리시스/코드/NaN 가드), `src\render\NativeHost\Hands.cpp`(네이티브 그립 액션 노출 = 관리 미러), `config\schema.json` `x-host-tunables.TurretCycle`(enum Stick/Dpad/Special/Off), `src\core\Install-Stage0.ps1:92`(설치 시 `TurretCycle` 값 검증·거부).
- **검증 (오프라인, 전부 녹색)**: `tests\HandAim` **226/226 PASS** — 이 중 `d206-*` **26개 전부 PASS**(히스테리시스·NaN·코드 대칭·mode-off·게임 자체 `is_turret_mode`/`Fire` 액션 읽기·네이티브-미러·`TurretCycle` 이중검증/drill 포함). `tests\verify-config` PASS. `src\core` Release **경고 0 / 오류 0** → `VampireHuntersVR.Stage0.dll` **437,248 B**, SHA256 `6963A65144CEA0D9…`.
- **라이브 설치 (사용자 기기, 게임 미실행 확인 후)**: D-206은 **Stage0 + NativeHost + 설정에만** 있고 VhvrExtras는 무관(extras 53,760 B = 현재 빌드와 동일). 최소·일관 교체:
  - `BepInEx\plugins\VampireHuntersVR.Stage0.dll` → **437,248 B**(`6963A65144CEA0D9…`).
  - `BepInEx\plugins\VhvrNativeHost.dll` → **339,456 B**(`E6C9BFD41E1E5360…`).
  - `BepInEx\config\vhvr-openxr-host.json`에 **`TurretCycle: "Stick"`** 추가(기존 값 전부 보존, 49키).
  - openxr_loader(2,671,104 B)·VhvrExtras(53,760 B)·BepInEx 코어는 손대지 않음(정본·D-206 무관).
  - 직전 파일 보존: `artifacts\d206-install\game-backup-20261018T230553\`(Stage0 430,592/`34AA2476…`, NativeHost 338,944/`BBD07003…`, config 1,416/`9A2E7B58…`). **롤백 = 이 3개 복사면 끝.**
- **설치 방식 선택 (기록 = 폴백)**: 정식 설치기가 모두 현재 게임 상태를 거부했다 — (a) `Install-Stage0.ps1`(트랜잭션)는 pristine 게임(백업 121파일/8디렉터리)과 **정확히 일치**해야 해서 현재 모드 설치 상태에선 `Directory inventory differs`로 중단, (b) 릴리스 `install.ps1`는 기존 BepInEx가 있으면 거부("never merges"). 현재 설치는 트랜잭션 워크스페이스 없는 **고아 상태**(current.txt·journal.jsonl 없음)라 Uninstall도 안 됨. 따라서 검증된 빌드 산출물로 **최소·일관·되돌리기 가능한 제자리 교체**를 택함(위 백업으로 완전 롤백). 게임 기본(스톡) 파일은 손대지 않음.
- **부수 수정 (D-186 손상 복구)**: `artifacts\openxr\extracted\x64\bin\openxr_loader.dll`가 668,672 B/`C82B03B6…`로 오추출/손상돼 설치 경로 고정 해시 검증에 걸림 → 정본(2,671,104 B/`66946EA6…`, Khronos 1.1.63)으로 복원. 오본은 `…dll.bad-d186` 보존.
- **남은 위험·미검증 (정직)**: ① **헤드셋 미검증** — 4키의 실기기 체감(그립 반응·종류 전환·Fire 확정)은 착석 플레이테스트로만 판정 가능. 오프라인 검증은 로직·설정·빌드까지다. ② `tests\verify.ps1` 전체 게이트는 **`tests\Transaction.Tests.ps1` 손상(D-186 sync, 86–194행 공백·중괄호 불일치)으로 여전히 차단** — 4키와 무관한 설치 엔진 fixture 테스트이고, D-206 검증 게이트(`tests\HandAim`)는 전부 통과. ③ `TurretCycle` 변경은 설정 편집 + 재시작(4값). ④ 0.9.4 릴리스 패키지 payload는 아직 구버전 Stage0(428,032 B)이라 **패키징 시 D-206 빌드 반영 필요**.
- **다음**: 착석 플레이테스트 1회 → (A) 좌/우그립 종류 전환, (B) 양그립 Fire 확정·설치, (C) `TurretCycle` 4값 A/B, (D) 기존 Interact 상호작용 회귀 없음 확인. 통과하면 0.9.5 패키징 확정.
## D-207 — VhvrExtras 손 ABI 미러를 네이티브 64/144 B 계약에 맞춤 (2026-10-19)
- **선택**: Extras `ReadHands(out ...)`는 공유 `NativeHandPose`/`NativeHandsStatus`를 사용한다. 네이티브 계약대로 각 손 64 B (`Squeeze@60`), status 144 B (`Left@16`, `Right@80`)로 고정하고 로딩 시 `Marshal.SizeOf`/`OffsetOf`가 다르면 fail-fast한다. 테스트도 바로 그 production 구조체 파일을 링크한다.
- **근거**: pre-fix Extras 손 60 B/status 136 B였고, `Right`는 native right offset 80 대신 76에서 시작해 native left `Squeeze` 필드를 `Right.Active`로 읽었다. native `ReadHands`는 144 B를 쓰므로 managed output buffer는 8 B 부족했다. native `Host.h`의 `static_assert`와 Stage0 `NativeHandPose` 미러 모두 64/144 B와 일치.
- **원인 판단의 경계**: 오른손 ray 결함의 구조적 원인은 확정. 2026-10-19 00:36:33 Windows-local `Application Error 1000` (`coreclr.dll`, `0xc0000005`)에 이 overrun은 메모리 손상 경로를 제공해 강한 crash 후보지만, 호출 스택이 없어 해당 crash의 인과관계는 미확정. 직전 로그의 haptic 요청은 `0 (queued)` 정상 응답이라 원인으로 지목하지 않음.
- **검증·라이브 교체**: Extras Release build 경고 0/오류 0; VhvrExtras fixture 78/78, ABI 크기·오프셋 포함. 54,272 B build/live SHA256 `B4AD024D6908D52C76FA8F054A34D40E4C0ED9780C045731EA8B3F044F2FA3C1` 일치. 기존 53,760 B DLL (`52E6707290D430F25615CF50EFAA7ADA3013DD21527FB7861D999BFDDB1CA533`)은 `artifacts\\d207-install\\20261018T155649Z\\VhvrExtras.dll.before` 및 `VhvrExtras.dll.replace-backup` 두 곳에 보존, 두 해시 일치; 영수증 `receipt.json`. 교체 전 게임 프로세스 없음 확인, 게임 자동 실행 없음.
- **미포함/제한**: Stage0/native DLL, 사용자 설정, loader 및 게임 파일은 바꾸지 않는다. `dist\\VampireHuntersVR-0.6-final` 폴더/ZIP은 구형 Extras를 포함한 역사적 패키지라 갱신하지 않음(새 패키징 작업으로 명시). 헤드셋 ray/클릭 및 crash 재발 여부는 인간 플레이테스트 전까지 미검증.



### D-208 — 0.9.5-rc1 private review package only (2026-10-19)
Date: 2026-10-19 | Stage: packaging / local review
Chose: create `dist\\VampireHuntersVR-0.9.5-rc1-preview.zip` by reusing the existing 0.9.4 final archive and replacing only the three runtime DLLs with byte-for-byte D-206/D-207 verified installed artifacts. Stage0 437,248 B / SHA256 `2155A791373276EA3C610FF18624FCFA1C737D31993BCD36938EAD78EE655777`; NativeHost 339,456 B / `E6C9BFD41E1E536087C945563F4751FC3EF2C8A7426F6F1D5438B32A9B3DC516`; Extras 54,272 B / `B4AD024D6908D52C76FA8F054A34D40E4C0ED9780C045731EA8B3F044F2FA3C1`. Installer manifest is `0.9.5-rc1`; defaults explicitly include D-202 `HandPoseFrame=Head` and D-206 `TurretCycle=Stick`.
Evidence: D-207 Extras fixture suite rerun, 78/78 PASS (ABI/policy only); all 255 ZIP entries read successfully; packaged DLL hashes and README/ModNotes/installer/review warnings verified; SHA sidecar matches archive. Existing 0.9.4 ZIP remains byte-for-byte unchanged (`124C5FF469FC93D92903FA5FD2C34AAD204A62A5C79C031534CB3E6CE7952538`). No component was rebuilt during packaging; no game was launched or installed by this operation.
Boundary: this is a private preview candidate, not a final or public release. D-202/D-206/D-207 headset acceptance and crash-free session remain open. Previous `coreclr.dll` `0xc0000005` cause is not established. Project license remains undecided; third-party redistribution terms were not re-cleared. No license chosen, source-only review copy created, or upload/redistribution authorized. Historical packages are unchanged.
Cost/next: keep the candidate private; complete the written batched headset playtests, decide project licensing, and clear third-party notices before any public distribution. `tests\\verify.ps1` gate remains separately blocked by ENV-012; this packaging did not claim the full release gate passed.



### D-209 — 사용자 라이선스 선택: 자체 소스 GPL-3.0-or-later, 소스 복사본은 비공개 로컬 검토만 (2026-10-18)
Date: 2026-10-18 | Stage: packaging / local review
Chose: 사용자 발언 “라이센스는 GLP 유지할거임”은 GLP→GPL 오타로 해석하며, 기존 D-199의 **GNU GPL-3.0-or-later**를 프로젝트 자체 소스 라이선스로 확정한다. GNU 공식 원문 전문을 소스 트리의 `LICENSE`로 넣는다. 이 선택은 게임 파생 자료나 제3자 코드의 권리를 부여하지 않는다.
Reconciled: D-208은 당시 라이선스가 선택되지 않았다고 기록한 역사적 상태이므로 append-only 규칙대로 수정하지 않는다. 현재 선택 상태를 이 D-209와 `README.md`/상태판에 반영한다. 소스 트리는 이제 개인 로컬 검토용으로만 만든다.
Boundary: `docs/DEPENDENCIES.json`은 BepInEx(LGPL-2.1-or-later), 격리 probe 의존성, 게임 메타데이터에서 생성된 비재배포 interop 및 OpenXR 자료를 기록한다. 포함된 OpenXR 헤더의 SPDX는 **Apache-2.0 OR MIT**다. 스테이징에는 원본 헤더 표기를 유지하고 Apache-2.0 전문과 이 이중 라이선스를 정확히 밝히는 NOTICE를 함께 넣는다. 나머지 버전별 제3자 런타임/전이 의존성 의무는 아직 전체 재검토되지 않았으며 예전 0.6 안내문은 현재 버전의 고지로 간주하지 않는다. 따라서 RC 재배포는 별도 차단 상태다. BepInEx·게임 파생 DLL·OpenXR loader 등 외부 바이너리, 게임 자료, 빌드/실행 산출물과 테스트 증거물은 스테이징에서 제외한다.
Path/privacy: D-199의 대상(`src/`, `extras/`, `tests/`, `config/`, `docs/`, `AGENTS.md`), 제외(`tools/`, artifacts/dist/backups, 모든 bin/obj, tests/archive, 바이너리 및 미디어), placeholder 경로 살균을 적용한다. 추가로 검사되지 않은 작성자 경로가 남으면 중단하고 수정한다.
Verification boundary: 목적지 `<RepoRoot>`는 private/local only. `git init`, 커밋, 원격 추가, 업로드, 재배포를 하지 않는다. 이 로컬 스테이징은 헤드셋 수락/릴리스/게시 게이트를 통과시키지 않으며 RC와 crash/headset 상태는 그대로다.
Next: 복사본을 D-199 필터의 로컬 검토용 파생물로 제한한다. GPL/Apache 전문, NOTICE, 파일 종류, 게임 파생 바이너리·개인 경로 및 `.git` 부재를 검증하고, 제3자 재배포 검토와 헤드셋 확인 전에는 공개하지 않는다.

### D-210 — 소스 공개(GitHub) 허용, 비공개 문구 제거, 작성자 로컬 경로 살균 (2026-10-19)
Date: 2026-10-19 | Stage: packaging / source release
Chose: 사용자 지시로 소스 트리를 "비공개 로컬 검토본"에서 공개 소스 릴리스로 전환한다. 자체 소스는 GPL-3.0-or-later(D-209)이며 GitHub 업로드를 허용한다. D-209의 "비공개·업로드 금지"는 이 결정으로 대체된다(append-only: D-208/D-209 본문은 역사적 상태로 남긴다).
Changed: README/THIRD-PARTY-NOTICES/installer README 재작성(비공개·업로드 금지 문구 제거, 알려진 한계 명시), installer의 preview/rc1 표기를 0.9.5로 정리, 라이선스 텍스트 5종을 licenses/에 포함. 하드코딩된 게임/시뮬레이터 경로는 환경변수 VHVR_GAME_ROOT / VHVR_XR_SIM_JSON(기본값은 표준 Steam 경로)로 대체. 문서의 작성자 로컬 경로는 <RepoRoot>, <GameRoot>, <Recordings>, %LOCALAPPDATA% 등 플레이스홀더로 치환.
Boundary: 이 정리는 소스 사본(별도 폴더)에만 적용했다. 원본 프로젝트와 설치된 모드는 변경하지 않았다. 헤드셋 수락(D-202/D-206/D-207)과 크래시 안정성은 여전히 미검증이다. 이 결정은 바이너리 패키지(BepInEx/런타임 포함 ZIP)의 공개 배포를 허용하지 않는다. 게임 파일, BepInEx/런타임 바이너리, 게임에서 생성한 interop은 저장소에 넣지 않는다.
Cost/next: .git 히스토리는 아직 없다. 저장소를 만들 때는 새로 git init 해 작성자 경로가 히스토리에 남지 않도록 한다.

### D-211 — 스냅 턴 3택(끄기 / 30° / 45°)을 실제 "시야 회전"으로 구현 (2026-10-20)
Date: 2026-10-20 | Stage: Stage 9 comfort / BUG-203 fix
Chose: 사용자 요청("스냅턴을 추가하고, 각도를 30도 혹은 45도, 비활성화 3가지로 나눠 옵션으로 넣어줘")대로, 헤드 프레임(기본)에서 오른쪽 스틱 좌/우가 **시야를 실제로 회전**하게 하고 각도를 0/30/45 세 가지로 제한한다. 0 = 끄기, 30 = 기존 기본값, 45 = 넓은 스텝.
Why: D-202가 헤드 프레임에서 스냅 요청을 거부한 이유는 회전 없이 화면만 튀는 것을 막기 위해서였다. D-195 이후 눈 카메라는 `anchorRotation`을 합성에 쓰지 않으므로, 앵커를 돌리는 레거시 경로로는 시야가 돌지 않는다(BUG-203). 세션 view-yaw 오프셋을 눈/손 합성에 직접 곱하는 것이 그 뿌리에 맞는 수정이다.
How: `OpenXrHost.snapViewYaw`((-180,180] 랩)를 `ApplySnap`에서 누적 → `PrepareCameras`가 두 눈 오프셋과 두 눈 회전에 곱하고, `LateUpdate`는 같은 회전(`ViewYawRotation`)을 HandAim 합성 base로 넘긴다. 앵커는 불변이므로 플레이어 위치가 튀지 않는다. 새 앵커 4곳에서 `ResetSnapView()`. 각도는 `SnapTurnPolicy.Choices`(0/30/45)로 고정하고 미등록 값은 30으로 해석 + 경고 1줄(조용한 다운그레이드 없음). 노출: `VhvrSettings.exe` 라디오 3개, `config\schema.json` enum, `installer\install.ps1` 주석, 영수증 `snapViewYaw`/`snapTurnsApplied`/`handFrame`.
Alternatives rejected: (a) 앵커 회전 유지 — 시야가 안 돌고 위치만 튄다(구 BUG-203 그대로), (b) `HandPoseFrame=Anchor`를 기본으로 — BUG-202의 90° 손 꺾임이 재발, (c) 아날로그 스무스 턴 — 사용자가 3택(끄기/30/45)을 명시했고 Q-015/D-013이 스냅 전용을 이미 확정.
Numbering: D-209는 라이선스 결정(D-209)이 이미 사용 중이라 이 작업은 D-211로 기록하며, 같은 날 작성한 코드/테스트/문서 주석도 D-211로 정정했다.
Verified (offline): 플러그인 빌드 경고 0/오류 0; `tests\HandAim` 238/238; `tests\Settings` 13/13; `VhvrSettings --selftest` PASS(snap=30); `--form-selftest` PASS(controls=33, clipped=0).
Boundary: **헤드셋 미검증**. 회전각·위치 고정·전방 이동이 함께 도는지는 착석 1회가 필요하다(`docs\playtests\PLAYTEST-D211-SNAPTURN-KO.md`). 패키지/릴리스에는 아직 반영하지 않았다.
Cost/next: 다음 착석에서 판정 3종을 통과하면 0.9.6 후보로 패키징하고, VhvrSettings/README/릴리스 노트에 같은 3택을 싣는다.
