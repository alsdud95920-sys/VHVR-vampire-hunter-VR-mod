# HEADSET-CHECKLIST — everything only a person in the headset can answer

Status: **INDEX / ROLLUP — nothing new.** Every row below is copied from a script that was written and committed
*before* its session (source named per section). This file invents no criterion; if it and a source ever disagree, the
source wins. Update it when a new stage script is written, not after the fact.

Why it exists: the first headset session is the only remaining user-side action, later stages each carry their own
"headset gate", and the user asked for one list of what *they* must check. Agent-side desk work (install, logs,
inventory, teardown, verdict writing) appears only where it brackets a human step.

## Rules that apply to every row

- A criterion passes only on **observed** evidence (what the headset showed **+** the log lines **+** the file
  inventory) — never on inference, on a plugin having loaded, or on a log line alone.
- The human reports what is seen and does not edit configs, swap loaders or re-tune mid-session; the agent reads the
  logs and writes the verdict. Partial wins are recorded as PARTIAL, never rounded up to PASS.
- Any nausea, dizziness, sweating, headache or eye strain: **stop immediately**, remove the headset, wait at least an
  hour, record it as sev-1 (`docs/BUGS.md` "Safety"). A session stopped for comfort **cannot produce a PASS**.
- Anti-cheat/DRM/Steamworks are never touched; the online leaderboard is out of scope (D-045).
- Left-handed passes no longer exist anywhere in this project (D-051), so no row below asks for one.

## Session 1 — Stage 1 UUVR spike: **READY NOW, the only open gate** (≤25 min total)

Source: `docs/playtests/PLAYTEST-1-SPIKE.md` (pre-committed SP1–SP9 before the session; D-038/D-043/D-047/D-048/D-049).
UUVR adoption is a **separate later decision** (D-057+) taken only on a full PASS.
**Plain-language Korean card for this session: `docs/playtests/PLAYTEST-1-SPIKE-KO.md`** (same content, no new criteria;
if the two ever differ, `PLAYTEST-1-SPIKE.md` wins).

Before the headset goes on, the agent re-checks B1–B5 (Stage 0 gate passed; script written first; UUVR artefact pinned;
transaction path extended; stock 121 files / 8 directories, full verifier `VERIFIED`, journal last event `Recovered`).
**B6 is yours:** headset charged, room clear of obstacles, water within reach, seated start, 25 minutes available.
If B3/B4 were not met the session is **cancelled, not shortened**.

Budget (hard stops): Stage A menu ≤8 min → 2-min break → Stage B one ordinary gameplay run ≤12 min → 2-min break →
Stage C stop states + teardown ≤5 min. This is not a scored run: play normally, no scoring claims.

| # | What you do | What you report | FAIL looks like |
| --- | --- | --- | --- |
| SP3 | (A) main menu: look around ~60 s | genuinely stereo: separation, real depth cues, near/far difference, the two eyes differ | mono / eye-locked / duplicated / black-garbage image → FAIL_NO_STEREO |
| SP4 | (A+B) look around ≥2 s, stand still, then walk | 1:1 rotation **and** position; the world stays put when the head moves; no snap-back, drift or forced motion | offset, drift, visible lag, or the game camera pulls the view → FAIL_POSE |
| SP5 | (A) menu; (B) gameplay; if a level-up/pause/death panel occurs naturally, watch it | pose survives the menu (`ArenaCamera`) and at least one gameplay state; a panel does not yank the view away | pose fought or lost → FAIL_STATE |
| SP6 | (A+B) glance at the desktop mirror | the game window shows a sane image while the headset is active | black/garbage window → FAIL_MIRROR |
| SP7 | (A–C) continuously | no crash, hang or black screen; the game exits normally | crash/hang/black screen → FAIL_CRASH |
| SP9 | at ~10 min of play **and** at teardown | comfort: no discomfort at either point | any discomfort → sev-1 stop, session ends, **no PASS** |
| probe (SP4/SP5) | (B) while firing, and during a dash/jump | does the game's own FOV change (dash) or shake/ recoil (fire) move the view **without** head movement? | view moves with the head still = the camera-ownership fight; the D-046 pin predicts `PlayerController.LateUpdate()` is the writer |
| SP8 | (C) quit **through the game menu**, remove the headset | nothing left to report by hand — the agent then uninstalls, expects `ADDITIONS_QUARANTINED_STOCK_VERIFIED`, re-runs the verifier and relaunches flat | anything left behind or unprovable → spike invalid, no adoption discussion |

Verdict: PASS = SP1–SP9 all pass; PARTIAL = SP1/SP2 pass and stereo (SP3) passes but SP4/SP5/SP6 fails while the game
is still removable; FAIL = SP1, SP3, SP7 or SP8 fails. SP1/SP2/SP8 are agent-verified, listed only so the human sees
the full bar.

## Session 2 — Stage 3: XR runtime, interaction profile, action map (locked until Stages 1–2 pass)

Source: `docs/INPUT-MAP.md` section 4 (pre-registered 2026-07 with D-050, amended by D-051/D-055/D-057). **D-055 made this two
short sittings, not one ≤10-minute list:** rows 1–7 are the original session; rows 8–10 are a second, shorter one that the
modifier pairs and the stick-direction pass added. D-057 renamed the fire pair (보조발사), deleted the grip assignment and
gave the right stick three jobs, so rows 2/4/8–10 were rewritten again. The agent wires the logging; the human presses
controls in the order asked and reports what the game did.

| # | What you do | What you report |
| --- | --- | --- |
| 1 | put the headset on with a session live (agent logs `xrGetCurrentInteractionProfile`) | nothing by hand — expected `oculus/touch_controller`; a `meta/touch_controller_plus` report is a data change, not new code |
| 2 | press each control **once, in order** (agent logs every action's active+value for 10 s). **D-057 order:** left stick (all directions), **left stick press** (Q on a tap — hold it 1.0 s as well for the bug report), left X (`dash`), left Y (`secondary_modifier`), left menu, left trigger **with Y held** (`fire_secondary_off` = 보조발사), then **without Y** (`fire_left_hand` — it must fire; the gate is gone), left squeeze (log only, **nothing should happen**), right stick X **left then right** (`turn`, the 30° snap), right stick **pushed up** (`interact` — say how far up it had to go), right stick **pulled down** (`blood_ability` — same question), **right stick press** (E), right A (`jump`), right B (`secondary_modifier`), right trigger with B held (`fire_secondary_dominant` = 보조발사) then without it (`fire_primary`), right squeeze (log only, nothing should happen) | anything the game should have done but did not — a dead button must read as "never active", not as a mystery; and a squeeze that *does* change something is a sev-2 finding |
| 3 | touch each capacitive entry asked for (thumbstick first) | which ones actually report; everything that does not report gets deleted from the list |
| 4 | push the right stick left/right and snap-turn, then **hold** it left at the edge | the `turn` axis sign is correct (left turns left), one push = **one 30° step**, snap feels like snap (not smooth), and holding the stick at the edge does **not** keep spinning (the re-arm). Numbers live in `tuning.snapTurnFromRightStickX` |
| 5 | — | in-game assertion only: every `required: true` action is bound for the active family |
| 6 | press the reserved system button | nothing in the mod reacts, **and** the runtime overlay still opens |
| 7 | press the menu button with each hand once | **which physical hand reports `menu/click`** — the file assumes the left one (= the `off` hand here); if the runtime reports right, the `handProfiles` layout is wrong and the fix is data + a re-run |
| 8 | D-057 feel (one axis, two directions): push the right stick **up** until `interact` fires, then pull it **down** until the blood ability fires, then keep turning with the stick for a minute | how far up/down each one had to go, and whether **either** ever fired by accident while turning. Both thresholds are data (`tuning.interactFromRightStickUp`, `tuning.bloodAbilityFromRightStickDown`), so this changes numbers (and, if the sign is opposite on this runtime, both entries swap together) — not the map |
| 9 | D-057 fire modes, left hand: hold **Y** and pull the trigger (**보조발사**), then release Y and pull the trigger with **nothing else held** (main weapon — it must fire now), then press/release Y **while already firing**. Repeat on the right with B | three answers: (a) Y + trigger = 보조발사 (the flat RMB alt-fire), (b) trigger alone = that hand's main weapon (**no grip needed any more**), (c) a selector press or release mid-burst fires **nothing by itself** (the per-press latch). This is the input half only — the weapon slots are Stage 6B item 10 |
| 10 | squeeze both grips one after the other, slowly, and watch the game | the raw values are logged and **nothing in the game reacts** (both squeezes were unassigned by D-057). If a squeeze does drive something, that is a sev-2 Stage 3 finding, not a naming detail |

## Session 3 — Stage 4: movement and comfort (locked)

Source: `docs/PHASE_PLAN.md` Stage 4 checklist (Q-015/D-013: snap turn only, no anti-nausea overlays).

- Ordinary stick locomotion; **snap-only turn** at the D-057 increment (**30° per push**, `turn.snapDegrees` /
  `tuning.snapTurnFromRightStickX`); check the repeat/re-arm behaviour and that holding the stick at the edge does not spin.
- `dash` is on the **left lower face button (X)** and `jump` on the **right lower face button (A)** since D-056 — both plain
  buttons with no threshold — and the right stick carries three jobs since D-057: X = the 30° snap turn, **up** =
  `interact`, **down** = the blood ability. Confirm each of the two discrete ones fires at a comfortable point, that
  **neither fires while turning**, and that the re-arms stop one push from producing several activations. Those are numbers
  in `tuning.interactFromRightStickUp` / `tuning.bloodAbilityFromRightStickDown`.
- The **squeezes do nothing** now (D-057 deleted the assignment): if either one causes the game to react, that is a sev-2
  finding rather than a mapping quirk.
- `jump` now lives on the right hand, so the left thumb never leaves the movement stick for it. Whether the old
  movement-stop question is fully closed is one sentence from you, not a design assumption.
- Physical movement/crouch/collision; head pose is preserved when a snap turn changes player yaw.
- Double jump and dash preserve the game's movement, cooldown and upgrade rules, collision, and a free head pose.
- Seated/standing, recenter, boundary respected and required transitions handled.
- **Q-026 answered (D-055):** recenter has **no controller binding** any more — it lives in the mod's settings menu
  (Stage 9). The right stick press carries E; if drift makes a controller path necessary, it returns as one line of data.

## Session 4 — Stage 5: physics hands (locked)

Source: `docs/PHASE_PLAN.md` Stage 5 checklist. Frame-independent collision; every actual world interaction in this game
inventoried and supported (no invented interactions). **D-055 removed the "grab/throw recovery" item from the plan:** the
user reported that this game has no grabbable object, so there is no grab verb to implement. **D-056 narrowed the grip to
one job** (the main-weapon gate), and **D-057 then deleted that job too** — the assignment is gone, both squeezes are
recorded in `bindings.deliberatelyUnbound`, and the fire selector is the upper face button. Rows 1–3 are what is left of
that item's spirit: prove the squeezes report (for the record) and prove the inventory really is empty.

| # | What you do | Expected |
| --- | --- | --- |
| 1 | squeeze each grip (both hands) with the game running, then squeeze one, hold it, and squeeze the other | a log line per hand, with raw values, and **nothing in the game reacts** — D-057 deleted the grip assignment outright (`bindings.deliberatelyUnbound`), so a squeeze that *does* drive something is a sev-2 finding rather than a gate that failed to reach its threshold |
| 2 | try to pick up / throw everything the game actually offers, on foot | **the inventory**: the user says there is nothing grabbable (D-055) — this row either confirms that in one pass or corrects it, and a correction is a new decision, not a silent code path |
| 3 | (only if 2 found something) grab it with the **left** hand, then pull the left trigger | whether a real grab verb collides with the fire path. Since D-057 the squeeze has **no job at all**, so a real grab object needs a **new** decision about which control carries it — the squeeze is not a free control a subsystem may claim by itself |

## Session 5 — Stage 6A–6D: weapons, combat, per-hand aim (locked)

Source: `docs/TWO-HAND-AIM.md` section 5 (≤15 items, expected result each; PASS requires items 1, 2, 5, 6 **and** 10 plus
comfort — items 3, 4, 8 and 9 were removed: the two-hand link by D-056, left-handed play by D-051).
`docs/playtests/PLAYTEST-6B.md` and `docs/playtests/sim/stage6b.json` are named there but **not written yet** — they
are Stage 6B deliverables, so the rows below are the pre-registered intent, not an executable script.

| # | What you do | Expected |
| --- | --- | --- |
| 1 | draw the weapon, sweep the dominant hand (smoothing 0) | the muzzle ray follows 1:1 with no visible lag |
| 2 | fire five shots at a wall mark | impacts land where the muzzle points, not at screen centre |
| 3 | ~~bring the off hand to the support point~~ **removed (D-056)** | the two-hand link is deleted: there is no support point and no two-hand aim line to engage |
| 4 | ~~shake the off hand at the edge of the link radius~~ **removed (D-056)** | no link radius, no hysteresis, so no one/two-handed chatter to test |
| 5 | sweep the **off** hand while the dominant hand holds still | the left weapon's muzzle ray follows the left hand alone; neither ray is pulled toward the other (replaces the old link-release row) |
| 6 | fire with the off hand while the dominant hand also fires | recoil moves each weapon in its own hand; the **view does not shake** |
| 7 | set `AimSmoothing` to maximum | the difference is visible, and 0 is the default |
| 8 | ~~repeat 3–4 with `TwoHandMode = toggle`, then `hold`~~ **removed (D-056)** | `TwoHandMode` no longer exists — the link it switched is gone |
| 10 | put two weapons in two hands (game slots `LEFT_HAND` / `RIGHT_HAND`), aim at **two different walls**, then fire the left trigger and the right trigger independently — **with nothing else held** (D-057: there is no grip gate) — and each also once with its upper face button held (D-056/D-057: 보조발사) | **two independent aim rays**, and the log shows `PlayerController.fire(EWeaponSlot, …)` per shot so each shot's slot is visible — in particular that `fire_left_hand` drives `LEFT_HAND` and `fire_primary` drives `RIGHT_HAND`, and that the face-button-held case drives **보조발사** without firing the main weapon |
| 10b | with a secondary-fire (보조발사) weapon owned, press the flat **RMB alt-fire** on the desktop keyboard and use Q/E | **which slot 보조발사 touches** (per weapon or per hand — the expected call is `PlayerController.fire_secondary_shot(EWeaponSlot)`) and whether Q/E are the `LEFT_TOP_HAND`/`RIGHT_TOP_HAND` pair. This also feeds the interact question: the game's own menu shows 상호작용 = **Z**, so confirm the mod's `interact` forwards to it (Q-034 answered by D-058) |
| 10c | **double tap** each upper face button — left Y, then right B — first with that hand's trigger released, then once while the trigger is held | a double tap with the trigger up reloads **only that hand** (the log shows the intended `Weapon.reload()` call, or the recorded fallback `PlayerController.reload_all_weapons()`); with the trigger held it must reload **nothing**, and a single tap must do nothing either (D-059, Q-037) |
| 11 | comfort check | no nausea, arm fatigue or shoulder strain from the independent two-hand pose (there is no link to hold any more — D-056) |
| holsters | calibrate the holsters (Stage 6D) | holsters calibrated; aim independent of the head; recoil never moves the head |
| 14 slots | all 14 simultaneous weapon slots preserved | no drop of multiweapon gameplay, auto-fire or reload rules without evidence and a user decision (D-005) |

Item 9 ("repeat with the other dominant hand") was struck from the plan on 2026-10-07 (D-051) — left-handed play is out
of scope.

**If the muzzle redirect cannot be proven** (shots still come from the game's own aim), that is a stall report, not a
guess: record what was tried, what the shots showed, and the next rung into `shoot_forward` / `Weapon.shoot_*` callers.

## Session 6+ — Stage 7–10 (locked)

| Stage | What you check in the headset | Source |
| --- | --- | --- |
| 7 — body IK | player rig mapped and retargeted, calibration persists; look-down / mirror test | `PHASE_PLAN.md` Stage 7 |
| 8 — HUD/menus/audio | every UI type anchored and readable in VR; upgrade/loadout/progression menus usable; spatial audio listener follows the head | `PHASE_PLAN.md` Stage 8 |
| 9 — settings | every retained tunable reachable in the VR menu; calibration/snap-turn/weapons/input/UI/rendering/diagnostics; live persistence and reset | `PHASE_PLAN.md` Stage 9 |
| 10 — sweep/package | opening hour in the headset; seated verification (the left-handed pass was removed by D-051); no open sev-1/2 bugs; safe mode and restore | `PHASE_PLAN.md` Stage 10 |

Each of these stages carries its own "headset gate" bullet in `PHASE_PLAN.md`; this rollup only shows where the human
is on the critical path.

## Bottom line

**Only Session 1 is unlocked.** It needs ≤25 minutes of headset time, in three graded stages, and it is the single
input that unblocks everything below it. Session 2 needs Stage 3 code to exist first (Stages 1–2 must pass); Sessions
3–6 need their stages to be built. Left-handed rows are gone project-wide (D-051).