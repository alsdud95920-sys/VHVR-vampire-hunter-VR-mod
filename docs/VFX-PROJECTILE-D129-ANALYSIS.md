# D-129 — VFX direction and intermittent opposite-hand projectile origin
Date: 2026-10-11 | Mode: agentic | Stages6B/8 triage | No gameplay change

## Requirement correction
The user's latest clarification supersedes the current task's firing-angle interpretation: **actual firing direction works; visible effects have the wrong angle**. Separately, **a projectile-firing weapon held in the right hand sometimes appears to launch from the left**. Preserve the accepted aim/damage behavior. This does not establish exhaustive weapon-family/handedness acceptance or close historical TOOL-063/071 reports automatically.

## Conclusions and confidence
| Finding | Evidence / confidence |
|---|---|
| VFX and actual shot direction have separate native paths | Confirmed offline native disassembly; not a live hook claim |
| Sampled flame emitter transform itself is not rotated away from its muzzle | 149 matched d127a phase/outside rows: identical rounded forward vectors, normalized angle 0 degrees; excludes only those samples, not particle velocity/render timing |
| Current diagnostics cannot identify the owner/origin of every spawned projectile | Confirmed: shoot_forward counters do not cover shoot_projectile/get_free_bullets/WeaponProjectile initialization |
| Wrong-hand projectile root cause | **Unresolved**. Prioritize actual spawned-object owner and live muzzle reference, then scope/callback timing. No proven right-to-left binding error |
| Shared camera can contaminate a nested/uncovered path | Plausible, not demonstrated in an incident. Camera is temporarily moved per weapon; roots are preserved, not moved onto the camera |
| Simple pooled-object reuse retaining an old position | Not supported in the inspected base path: both reused and new objects receive requested world position/rotation. Callback/owner ordering and specialized paths remain unverified |

## Video evidence, limits
- Source: `<Recordings>\편집후\전체영상.mp4`, SHA256 `753A0D7A3AA4907DEB61C6510F8F6D715872AC3D3D5FE400944BE17ED390F9DE`, decoder duration209s,1280x720.
- Prior overview extraction completed40 PNGs, empty stderr. Late overview frames138.5–206.4s are black; that alone cannot distinguish a black source tail from seek/decoder behavior. Do not treat them as additional gameplay evidence.
- Offline dense extraction completed18 PNGs at12.5–13.5,99.5–100.5,101.5–102.5s (0.2s spacing), empty stderr. These are sought still samples, not continuous first-frame projectile tracking. The decoder has no frame-accurate seek proof.
- Inspected overview13.1s shows a weapon at lower center/right and separated flame blobs toward center/right; it documents the visual symptom, not the projectile emitter/3D angle. At54.9s both lower hand weapons and additional world effects are visible;101.9s has multiple hand/top/passive weapons and flame/target effects. Single frames cannot attribute every blob/trail to a specific weapon.
- No verified clock/build correspondence between this edited video and d127a. The visually recognizable flamethrower/right and SMG/left arrangement resembles d127a, but resemblance is not timestamp/instance provenance. Do not reinterpret the user's separate right-hand-projectile report as a left-hand SMG solely from the log.

## d127a telemetry re-analysis
Source: `<RepoRoot>\artifacts\headset-d087\d127a`.
-8878 shoot_forward calls:441 LEFT,8437 RIGHT; own-hand latch false at call time0/0;8878 null aim args replaced;missingFrames0;scope29620/depth0/maxDepth2/failures0.
-24 sampled pre-calls:20 `Flamethrower:-349118 RIGHT_HAND`,4 `Submachine_Gun:-347918 LEFT_HAND`. This is **not** a log of a right-hand projectile weapon's wrong-left-origin event. No emitted-projectile identity/velocity record exists.
-169 SHOT_SCOPE phase/outside rows;149 matching FLAME_POSE rows. Match uses frame+weapon+phase rather than frame alone. Sampled proxy age up to3 Unity frames. It is a permitted freshness window, not proof that it causes a wrong-hand launch.
-Post-driver pose trace: L12857/R19091 samples, max muzzle/aim0.028deg,overwriteSamples0/errors0. Sampling after the writer cannot exclude pre-write or later changes.
-**Different observable:** per-shot maxima are178.78954deg muzzle/aim and3.717299m muzzle/published-origin drift. These must not be replaced by the near-zero post-driver figures. First receipt with drift already occurs04:58:43.6457788Z:0.803001m/25.55495deg while **left WaitingForWeapons** and right Driving. Therefore at least the initial divergence is not caused by simultaneous left/right fire. Maxima are cumulative, not the identified video incident or actual projectile error.
-73 FLAME_FX samples. Six have alreadyFiringTrue butisVfxPlayingFalse,ps.playingFalse,particles0 (frames12234,17660,18075,23663,23928,24262). Another frame11848 has the weapon flagFalse but particles still playing. These demonstrate state disagreement/absence at sampled times, not the causal caller.
-20 sampled playing transitions/8 particle-count decreases do **not** count actual Play/Stop calls. Particle expiry, reload and user releases can contribute. Sparse zero pool samples do not prove those classes are never used.

## Native paths checked without launching the game
Read stock `GameAssembly.dll` and metadata with the **already used** LibCpp2IL/Iced stack (Unity2022.3.37f1). Reports in `<RepoRoot>\artifacts\d129-analysis\native-disasm.log`, `offset-api.log`, `method-catalog.log`. Offsets mapped with GetFieldOffsetFromIndex, not guessed from stub bodies. No game binaries/assets are redistributed.

### VFX direction (TOOL-070 / latest angle clarification)
- `Flamethrower.draw_shoot_vfx` RVA46BD70 sets `vfx_flames_transform.forward` (setter call46BFC6). One branch uses **this weapon's muzzle_flash_spawn.forward** (field1B8,46BF2D–46BF45); the other constructs a direction from its start/end arguments. The metadata-annotated shape confirms that effect orientation is a separate calculation. No runtime argument/branch capture exists.
- `shoot_forward` RVA48BEF0 has camera-dependent calculations and a detached aim-argument path. The flame-specific effect call near48C373 precedes the general override-origin branch; native code therefore retains camera dependencies even when the trailing Transform is supplied. Current scopes try to make that camera consistent per weapon; the analysis does not prove they fail in the reported frame.
-Emitter forward equality cannot establish ParticleSystem simulation space, authored velocity modules, surviving world-space particles, billboard orientation, or the final draw-time pose. Those are leading **measurement targets**, not a reason to apply a90deg correction.
- `Flamethrower.stop_shooting` RVA46C200 clears already_firing, calls ParticleSystem.Stop, clearsis_vfx_playing. **Another stop exists:** `Flamethrower.update` RVA46C2F0 calls base update, then when `reload_t`(field28)>0 calls Stop and clearsis_vfx_playing without directly clearing already_firing in that branch. This is a concrete alternative explanation for firingTrue/effectFalse; causality needs contemporaneous reload state.

### Wrong-hand projectile origin (TOOL-069)
- `HandFireTrigger.FireTick` explicitly calls player.fire for the current slot. Published shot frames are keyed by weapon instance; AimFor checks slot/container membership. There is no obvious shared-left-proxy assignment in this code.
- `Weapon.shoot_projectile` RVA48E580 is separate from shoot_forward. Inspected ordinary paths read **this weapon's muzzle_flash_spawn.position** for `get_free_bullets`, while rotation/targeting can read PlayerController.camera_transform(field150). Some paths invoke shoot_forward internally; that does not make their whole lifetime covered by its scope/counters.
- `Weapon.get_free_bullets` RVA47D420 uses a per-weapon collection(field168) and prefab(field1E8). Reuse branch explicitly writes worldposition,worldrotation,then SetActiveTrue; new branch Instantiate uses supplied position/rotation. **It does not simply leave a recycled bullet at the previous hand's position.**
- Native order: pooled object activation happens inside get_free_bullets **before** the subsequent owner initialization in shoot_projectile. `Weapon.initWeaponProjectile` RVA47EF80 assigns WeaponProjectile.weapon(field80) then invokes initialization. A specialized activation callback reading the old owner could cause transient contamination, but no such callback/affected weapon is proven here. Treat as a testable secondary hypothesis, not "pool bug confirmed".
- `WeaponProjectile.movement_init` RVA4C3FB0 reads projectile forward and can read player camera forward/velocity for launch motion. Those paths make scope lifetime and callbacks relevant. Its presence does not prove a left-origin spawn.
- **Prioritized next diagnosis:** correlate initiating weapon/slot, live muzzle instance and ancestry, returned projectile instance, assigned owner/slot, position and velocity immediately after initialization and next frame. Compare projectile position to BOTH live muzzles. If the wrong-left position is already supplied by the right weapon's muzzle reference, diagnose socket ownership; if it changes on activation/init, diagnose callback/pool ownership; if only direction or later motion changes, diagnose aim/scope lifetime. Include right-only/left-only/both and reload/burst context without suppressing native tails.
-Use only reference-type/primitive boundary logging if instrumenting later. Do not re-enable Vector3 argument detours (`draw_shoot_vfx` crashed under IL2CPP in D-097). No diagnostic instrumentation added to the runtime this session.

## Corrections to previous conclusions (history retained)
1. Equal cumulative fireCalls/releaseCalls does not prove immediate/per-shot release. Edge-only source pairs one press with its later release; D-125's claim of paired release during held fire is withdrawn as unproven. A reload stop branch now provides an additional native explanation.
2. WorldPoseScope changes the camera to aim and restores/preserves each weapon root's **captured world pose**. It does not force the roots to the camera/aim pose. The old "forcing camera and weapon root onto the same pose" explanation for TOOL-071 is withdrawn. Render timing/near-plane clipping can still be investigated, not asserted.
3. Existing Analyze-PoseSwings uses non-normalized rounded directions and groups by frame alone; "same fwd produces several degrees" can be rounding/length error. D-129 analyzer normalizes and keys weapon+phase; includes self/orthogonal checks. Do not treat the old4.9–8deg values or sparse complete scopes as proof of damage direction/healthy whole-frame transaction.

## Safety, validation, remaining action
-d127a stopped on Egypt_Map→MainMenu_2 with source/player destroyed (HOST_FAILED, not formal success). Cause of menu return is unattributed; no orderly native-close line. Completed05:03:55Z;uninstall0;cleanupErrornull;stockVerifiedTrue. Journal terminalRecoveredseq27; pointer still refers to that **recovered**, not active-installed, transaction. Do not relaunch this tag.
-Fresh independent stock check passes121files/8directories/7,726,223,158bytes exact hashes/write times; no old harness/game/extraction/check process remains; VDXR registry unchanged in saved run.
-Plugin/native build hashes still equal d127a receipts (`D3D6E972…`/`D025204E…`). No production rebuild/reinstall, no source/config gameplay change; no full verifier (which would rebuild native per TOOL-072).
-Offline analyzer ran exit0; PowerShell analysis/frame scripts parse clean; evidence analyzer self-checks passed; dense extraction18frames/empty stderr; focused existing ShotScopes28/28 and WeaponRuntime67/67 exit0/empty stderr. These are offline/fake-node/source tests, not a fix or headset acceptance. Analyzer's inheritednet6.0 emits SDK end-of-support warnings; production framework was not changed.
-Reproducible helpers/receipts are project-local ignored artifacts, not shipped or committed game content. The tracked report preserves methods, limits and results for resume.
-Remaining human input, **no new sitting requested**: one timestamp in the existing video showing the right-hand projectile coming from the left (and weapon name if known). Until then root cause remains open; next runtime observables are proposed, not installed.