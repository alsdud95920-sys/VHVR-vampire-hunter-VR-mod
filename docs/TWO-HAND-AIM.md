# Stage 6B pre-brief — per-hand independent aim (designed, not implemented)

Status: **PARTIALLY_IMPLEMENTED_SOFT_GATE_NOT_HEADSET_VERIFIED (D-086)** — per-hand controller sampling and per-hand weapon-container driving exist as opt-in code (`src/interaction/HandAim*.cs`, `src/render/NativeHost/Hands.cpp`); fire, secondary fire, reload, the 14-slot mapping and shot direction are NOT implemented, no hook is claimed and no headset session has been held.
Companion of `docs/INPUT-MAP.md`; data lives in `config/manifest.json` under `aimDesign`. Written before the Stage 1 gate
closes at the user's request (D-050). **Amended 2026-10-07 (D-051):** the user selected the per-hand independent reading
and asked whether it is actually possible, so section 2.1 records the static check against the game assembly, and
left-handed play is out of scope. **Amended again 2026-10-07 (D-056):** the user deleted the two-hand link outright
("투핸드 링크 없애라"), so this file now describes **one** mode, with no link radius, no support point, no `TwoHandMode` and
no link gesture. `manifest.aimDesign.modesRemoved` records the deletion.

## 1. What the request means — one mode, not two

"양손 분리조준" can read two ways, but the user settled which one this project builds (2026-10-07, Q-024), and then deleted
the other outright (D-056):

| Model | Meaning | Status here |
| --- | --- | --- |
| **M1 — per-hand independent aim** | Each hand aims its own weapon independently; the two aim rays are unrelated (each hand fires where it points) | **selected by the user 2026-10-07 (Q-024), and the ONLY mode since D-056**; game-side basis verified statically in section 2.1 |
| ~~**M2 — linked two-hand aim**~~ | Both hands hold one weapon; the off hand's position/twist steers the muzzle (the standard rifle hold) | **deleted by the user 2026-10-07 (D-056: "투핸드 링크 없애라")** — no link radius, no support point, no `TwoHandMode`, no link gesture, and the squeeze is not a link control (D-057 removed the squeeze's assignment entirely). It is recorded in `manifest.aimDesign.modesRemoved` so it cannot be re-added silently. |

There is no "M1 as a base state with M2 entered and left" any more: the design has one aim state per hand, full stop. If a
link is ever wanted again it needs a new user decision and a fresh Stage 6B design pass, not a flag flipped back on.

## 2. Evidence this rests on (read-only, from Stage 0/1 preparation)

| Fact | Source |
| --- | --- |
| A shot's aim source is the weapon scripts: `Weapon.muzzle_position : Vector3`, `muzzle_flash_spawn : Transform`, `shoot_forward(..., Transform)`, `aim_assist_radius` | `docs/CAMERA-PIN.md` (D-046, assembly shape) |
| `Gun.L`, `Gun.R`, `Root`, `Player_Anim_Armature` are byte-identical across 29 dumps = static bone proxies, not aim pivots | TARGET_PROFILE "Aim subtree" |
| The gameplay camera is written by `PlayerController` (most plausibly `LateUpdate()`); FOV/bob/shake are its fields | `docs/CAMERA-PIN.md`, D-039/D-046 |
| Up to **14 simultaneous weapon slots** must stay usable; two tracked hands do not imply two weapons; hand-bank grouping is a proposal only | D-005, `config/manifest.json` `combatRequirements` |
| The game auto-fires and has no confirmed manual reload | TARGET_PROFILE "Combat surface" (reload/auto-fire still to be discovered) |

## 2.1 Static verification — can both hands really aim and fire separately? (2026-10-07, D-051)

The user asked this directly, so it was checked against the game's own assembly instead of guessed. Method: the read-only
CLI/PE shape reader (`src/diagnostics/AssemblyEvidence.cs`) was pointed at the game-derived interop assembly
(`tools\interop\Assembly-CSharp.dll`, git-ignored but present on this disk) with type filter
`^(EWeaponSlot|Weapon|PlayerController)$` and member filter `(?i)hand|fire|shoot|slot|muzzle|aim|weapon|passive`.
Raw output: `artifacts/assembly-aim-slot/assembly-recon.json`, and `tests/verify.ps1` re-derives it every run
(`assembly-aim-slot-pin`). Nothing was loaded, patched or executed; these are stub signatures, so this is shape, not
behaviour.

| Finding | Evidence from the assembly | Why it matters |
| --- | --- | --- |
| **The game has per-hand weapon slots** | `EWeaponSlot = LEFT_HAND, RIGHT_HAND, LEFT_TOP_HAND, RIGHT_TOP_HAND, PASSIVE, COUNT` | The player does not hold "a gun": weapons are addressed by hand slot, in two pairs plus a passive bucket. The exact meaning of the `*_TOP_HAND` pair is **not** established. |
| **Firing is dispatched per slot** | `PlayerController.fire(EWeaponSlot, ref bool, float)`, `fire_secondary_shot(EWeaponSlot)`, `fire_release(EWeaponSlot, ref bool)`, `const_shooting(EWeaponSlot)`, `weapon_slot_avaliable(EWeaponSlot)` | A fire request already carries *which hand slot* fires it, so two hands firing separately is inside the game's own model rather than bolted on from outside. |
| **Two weapons at once is a first-class rule** | `PlayerController.start_with_two_weapons()`, `more_damage_on_other_hand_after_shot()`, `inverted_weapon_slot(int)`, `update_weapons_position_primary_based_on_fov()` | "The other hand" is the game's own vocabulary, and the FOV-based positioning call is exactly the piece a VR mod must replace with per-hand transforms. |
| **A shot's frame is parameterised** | `Weapon.shoot_forward(bool, float, bool, bool, float, bool, ElementalDamageType, bool, UnityEngine.Transform) -> EnemyCollider` | A trailing `Transform` is the only object-typed parameter of the firing call — the plausible per-shot frame to drive. The stub carries no parameter names and no body, so **which** transform it is stays a runtime question. |
| **Aim assist can fight a hand-driven direction** | `Weapon.get_closes_enemy_direction(Vector3, Enemy, bool, int)`, `Weapon.aim_assist_radius()` | Per-hand aiming may be pulled toward enemies; Stage 6B must measure this rather than assume it away. |

**Verdict (headset-free): plausible and game-supported in shape — not proven.** The game owning hand slots and per-slot
firing removes the biggest objection (that a flat horde shooter has one shared aim state). It does **not** prove that two
slots can be held *and* aimed independently at the same time, nor that the trailing transform is the aim frame. Those are
Stage 6B runtime questions, covered by the checks in section 5.

**What this changes:** nothing is implemented. `config/bindings.json` still says `DESIGNED_NOT_IMPLEMENTED`,
`aimDesign.handSlotEvidence.evidenceStatus` is pinned to `STATIC_ASSEMBLY_SHAPE_NOT_RUNTIME_MAPPED`, and the verifier
fails if those strings or the pinned slot values drift.

The `shoot_forward(..., Transform)` parameter is the encouraging part: the game plausibly already takes the transform to
fire *from*, which is what a muzzle redirect needs. It is **not** proof — a transform parameter can be used for a
collision filter or a muzzle-flash parent. Stage 6 must prove the redirect with a visible shot, exactly as the camera
work must be proven with a visible head move.

## 3. Design

- **One aim state per weapon instance, owned by the hand that holds it.** `dominant` and `off` each carry an aim state;
  a weapon is attached to exactly one role. There is no link state and no third state: with M2 deleted (D-056) the aim state
  has exactly one input — the holder's controller orientation.
- **Aim direction.** The muzzle follows the holder's controller orientation, always. Because the two hands are independent
  there is no two-hand line to blend and no blend slider; `manifest.aimDesign` no longer carries link fields, and the
  validator fails if a link mode reappears.
- **The grip is not part of the aim path, and since D-057 it is not part of the input map either.** It was the main-fire gate
  (`docs/INPUT-MAP.md` §3.3) until the user deleted the assignment outright ("그립버튼에 키 할당은 없애고"), and it was
  briefly a link gesture in an earlier draft. Nothing in this file depends on the squeeze: the trigger on its own fires the
  hand's main weapon.
- **Rerouting the game's own aim.** Pointing the model is not aiming. The game's aim assist, crosshair raycast and hit
  logic must be fed the muzzle transform; until that is done the mod is the classic "aim still driven by screen center"
  failure mode from the skill's list. This is the single largest unknown of Stage 6B and the first thing to prove.
- **Recoil moves the weapon in the hands, never the camera**, with an intensity slider from zero to full. Haptics pulse
  on fire and on empty.
- **Crosshair off by default**, toggleable, drawn at the muzzle raycast's hit distance, not in screen space.
- **Per-hand spread/fire rate** belong to the game (auto-fire is its own rule; D-005 forbids changing it without
  evidence and a user decision). The mod only supplies where the hands point.
- **Which fire action drives which slot (D-057 supersedes D-056/D-054).** The action map carries two fire actions per hand:
  `fire_left_hand` (off/left trigger **alone**) for the `LEFT_HAND` slot and `fire_primary` (dominant/right trigger **alone**)
  for `RIGHT_HAND`; while that hand's **upper face button** is held the same trigger fires
  `fire_secondary_off`/`fire_secondary_dominant` (**보조발사**, the flat RMB alt-fire, expected to reach
  `PlayerController.fire_secondary_shot(EWeaponSlot)`) instead. Stage 6B is what turns that intent into a call into
  `PlayerController.fire(EWeaponSlot, …)` — the input map only reserves the controls, and the per-press latch rule
  (`docs/INPUT-MAP.md` §3.1) is what stops a selector press or release from firing a slot the player did not ask for. Q/E
  stay as the separate **보조무기** actions (`aux_weapon_1`/`aux_weapon_2`, on the two stick presses); the
  hypothesised mapping of those onto `LEFT_TOP_HAND`/`RIGHT_TOP_HAND` is **unconfirmed** and is part of item 10.
- **보조발사 is no longer parked (D-057).** The user settled Q-029 — 보조사격 means the flat **RMB alt-fire**, called
  보조발사 in the Korean build — so the parked `fire_secondary` action and the `fire_aux_*` pair were merged into
  `fire_secondary_off`/`fire_secondary_dominant` and both are `required: true`. What is still unestablished is **which slot**
  a 보조발사 touches (per weapon or per hand): the flat button exists (13 presses/run) but the probe never saw it fire because
  no secondary-fire weapon was owned, so Stage 6B/6C owns that discovery and the mod must not quietly fold 보조발사 into the
  main-fire path until it is known.
- **No aim smoothing by default.** A shipping reference mod on this machine smooths the aim with a first-order lag and a
  maximum angular rate (approach noted, code not copied). Comfort smoothing is *appropriate* for a flatscreen game's
  heavy aim feel but wrong for a VR conversion's first pass: it reads as input lag. It is therefore prepared as an
  opt-in setting (`AimSmoothing`, default `0`), not as the base behaviour.

## 4. The 14-slot problem (unresolved on purpose)

The game keeps up to 14 weapon slots live (D-005). Two tracked hands therefore cannot mean two weapons, and the mod may
not quietly drop multi-weapon gameplay. Options recorded for the Stage 6 decision, no default chosen yet:

1. **Role banks** — each hand carries a small bank; the off-hand face buttons cycle within the bank *only for the weapon
   that is stowed*, leaving auto-fire untouched. Cheapest to implement, but it makes the mod's buttons mean two things.
2. **Selection wheel** — a radial panel (Stage 8 world-space UI) picks which slot is bound to which hand per wave.
3. **Left the game's own switching alone** — the mod maps only the *aiming* hand and lets the game's existing
   flat weapons/auxiliary controls keep doing what they already do under VR bindings.

Criterion for all three: no drop of multiweapon gameplay, auto-fire or reload rules without evidence and a user decision
(D-005). Q-024/Q-026 do not cover this; it is asked only when Stage 6B starts and the slot behaviour has been observed.

## 5. Test plan (pre-registered)

**No headset** — desktop simulator script `docs/playtests/sim/stage6b.json` plus in-game assertions:

- The muzzle transform equals the holder's hand transform within tolerance (per frame, no drift). There is no link case to
  test any more (D-056).
- A selector press or release mid-burst never produces a synthetic fire request (the D-056 per-press latch), and a
  trigger-only press with the selector released produces a **main-weapon** request (D-057: the grip gate that used to make it
  produce nothing is deleted).
- Camera rotation is never written by the aim/recoil path (assert roll is zero and the head pose is the last writer).
- Recoil with intensity 0 produces a bit-identical muzzle pose (proves the slider is real, not cosmetic).
- Two slots loaded at once report two distinct aim tuples, and every synthetic fire request carries its slot id — the
  same field the headset check (item 10) logs, so the simulator can fail the design before the headset does.

**Headset** — `PLAYTEST-6B.md` items, ≤15, each with an expected result:

1. Draw the weapon; sweep the dominant hand; the muzzle ray follows 1:1 with no visible lag at smoothing 0.
2. Fire five shots at a wall mark; impacts land where the muzzle points, not at screen centre.
3. ~~Bring the off hand to the support point…~~ **Removed 2026-10-07 (D-056):** the two-hand link is deleted, so there is no
   support point and no two-hand aim line to engage.
4. ~~Shake the off hand at the edge of the link radius…~~ **Removed 2026-10-07 (D-056):** no link radius, no hysteresis, no
   one/two-handed chatter to test.
5. Sweep the **off** hand while the dominant hand holds still; confirm the left weapon's muzzle ray follows the left hand
   alone and neither ray is pulled toward the other (this replaces the old link-release item).
6. Fire with both hands at once; confirm recoil moves each weapon in its own hand and the view does **not** shake.
7. Set `AimSmoothing` to maximum; confirm the difference is visible and that 0 is the default.
8. ~~Repeat 1–4 with `TwoHandMode = toggle` and with `hold`.~~ **Removed 2026-10-07 (D-056):** `TwoHandMode` no longer
   exists — the link it switched is gone.
9. ~~Repeat with the other dominant hand.~~ **Removed 2026-10-07 (D-051):** left-handed play is out of scope, so the
   Stage 6 acceptance no longer contains a left-handed pass.
10. With two weapons assigned to two hands (game slots `LEFT_HAND` / `RIGHT_HAND`), aim them at two different walls;
    confirm **two independent aim rays**, and log `PlayerController.fire(EWeaponSlot, …)` so each shot's slot is visible.
11. Comfort check: nausea, arm fatigue, shoulder strain from the independent two-hand pose (no longer from holding a link).

## 6. Pre-registered verdicts and risks

- **PASS** requires items 1, 2, 5, 6, 10 (per-hand independence) plus a comfort check. Smoothing is a setting, not part of
  the pass; items 3, 4, 8 and 9 no longer exist (the link was deleted by D-056, left-handed play by D-051).
- **Pre-verdict from static evidence (section 2.1):** the risk is no longer "the game has no notion of two hands" — it
  does, with per-slot firing. The remaining risk is reach: whether the mod can hold two slots and drive each one's aim
  direction from the outside.
- **If the muzzle redirect cannot be proven** (shots still come from the game's own aim), stop and write a stall report:
  what was tried, what the shots showed, and the next rung (deeper in `shoot_forward` / `Weapon.shoot_*` callers, or a
  Harmony read of the firing transform) before guessing a third time.
- **Risk:** `Weapon.muzzle_position` may be a read-only output rather than the firing input, so the redirect may need a
  different member than the one Stage 0 pinned. The pin says where to look, not what the code does.
- **Risk:** the game fires **per slot** (`EWeaponSlot`), but each slot's *aim direction* may still be derived from one
  shared player aim source. If so, M1 becomes "two hands, one shared aim direction" and that must be stated plainly in
  the README rather than faked.
- **Risk:** each trigger driving its *own* slot (D-054) may collide with the game's own auto-fire or with one shared
  "shooting" state: if the flat game fires all held weapons from a single button, driving `LEFT_HAND` alone may require
  the per-slot entry points (`fire(EWeaponSlot, …)`) rather than the button state the mod would naturally mirror. Item 10
  is the deciding test, and a negative result is a stall entry, not a silent reroute of the left trigger back to alt-fire.
- **Risk:** driving a 3D weapon model that the game animates itself can fight the animation (see `action_weapon_visibility`
  in the reference mod for how that fight looks). The mod must hide or re-parent the game's own weapon model rather than
  blend against it. (Nothing is linked to anything: the model simply follows one hand — D-056.)