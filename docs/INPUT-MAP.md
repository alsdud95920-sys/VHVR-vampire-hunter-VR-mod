# Stage 3 pre-brief — action map and the Quest 3 keymap (designed, not implemented)

Status: **DESIGN_ONLY_NOT_IMPLEMENTED** — no XR code exists, nothing is bound, no headset session has been held.
**설계가 아니라 현재 설치본에서 실제로 동작하는 키 매핑은 `docs/CONTROLS.md`를 보십시오** (D-114가 D-057/D-059의 인체공학을 대체했습니다).
Owner of the data: `config/bindings.json` (validated read-only by `tests/verify-bindings.ps1`, wired into `tests/verify.ps1`).
Written *before* the Stage 1 gate closes because the user asked for it (D-050) and because the mapping is data, not code:
a failed spike does not invalidate it; a different runtime behaviour does. **Amended 2026-10-07 (D-051):** left-handed play
is out of scope, and the two-hand aim feasibility was checked statically against the game assembly.
**Amended again 2026-10-07 (D-056):** the two-hand link is deleted, the face buttons moved to the user's new layout
(right A = jump, left X = dash), the stick presses carry Q/E, the right stick pulled down is the blood ability, and the
auxiliary fire is the upper face button held with that hand's trigger.
**Amended again 2026-10-07 (D-057) — this is the current map:** the user settled the naming ("보조사격은 평면 RMB
대체사격임 … 앞으로 보조발사라 부르도록"), so the upper-face-button fire is **보조발사 = the flat RMB alt-fire**
(`fire_secondary_off`/`fire_secondary_dominant`, the old `fire_aux_*`/`fire_secondary` names merged away); **the grip
assignment is deleted** ("그립버튼에 키 할당은 없애고"), so the trigger alone fires that hand's main weapon and both
squeezes are deliberately unbound; **`interact`** is the right stick **pushed up**; and the right stick left/right is a
**30-degree snap turn**.
**Amended again 2026-10-07 (D-058):** the game's **own keybinding menu** (Korean build, screenshot supplied by the user) is
recorded as flat-parity evidence. It settles **Q-034** — the interact verb is **상호작용 = Z (main) / G (alt)**, matching
`PlayerInputController.Interact` — and it **reveals two game actions the map does not carry** (특수 능력 = SHIFT, 수동 레벨
업 = T), opened as **Q-035/Q-036** rather than invented. It also refutes the old "`r` is interact" guess: **R = 재장전
(Reload)**. No VR binding changes; this is flat-parity data only.
**Amended again 2026-10-07 (D-059) — this is the current map:** each hand's weapon now reloads on a **double tap of that
hand's upper face button** (left **Y** → `reload_off`, right **B** → `reload_dominant`), the button's hold staying the
보조발사 selector; the parked `reload` action is merged into that pair; and **Q-035 is answered** — the **scout's 특수 능력
IS the dash**, so `dash` keeps forwarding to SHIFT and stays on the left **X** ("키 바인딩은 그대로 x키로 둬"). The game
already exposes reload per weapon object (`Weapon.reload()`, `force_reload(int)`, `instant_reload(float, bool)` — all public
virtual), so per-hand reload needs no edit to the game's code, only a call to its own API (Q-037 measures it).

## 1. What is fixed by evidence, what is still a proposal

| Item | Status | Source |
| --- | --- | --- |
| Quest 3 + bundled controllers | confirmed (user report) | Q-003 |
| Connection: Virtual Desktop | confirmed (user report) | Q-004 |
| Active OpenXR runtime: VDXR 1.0.10.0 "VirtualDesktopXR (Bundled)" | confirmed (registry + file version) | Q-004, re-read 2026-10-07 |
| **Which interaction profile VDXR offers this headset** | **static string evidence; runtime check pending** | scan of `virtualdesktop-openxr{,-32}.dll` |
| Flat control reality Stage 3 must route | v4-probe presses, now extended by the game's own keybinding menu (D-058) | TARGET_PROFILE "Input reality": primary 23608f/77 presses, secondary 1466/13, space 764, shift 359, q 82, e 68, r 104, esc 26; the menu adds the key→action labels (발사 LMB, 보조 발사 RMB, 상호작용 Z, 재장전 R, 혈능력 F, 특수 능력 SHIFT, Q/E 로봇 팔, 수동 레벨 업 T) |
| Key ergonomics (which button for which action) | **answered; the table stands, revised by D-052/D-053/D-054/D-055/D-056/D-057 (D-057 is the current map)** | Q-024 (2026-10-07), D-052, D-053, D-054, D-055, D-056, D-057 |
| Which game weapon slot each fire action drives (`LEFT_HAND`/`RIGHT_HAND`, or the `fire_secondary_shot` slot for 보조발사) | **not established — design intent only** | D-051 (API shape), D-054, D-057 |
| What 보조사격 means (the user's word) | **answered 2026-10-07: 보조발사 = the flat RMB alt-fire**, and the Korean build calls it 보조발사. Q-029 is closed; the Q/E auxiliary weapon (보조무기) is a different thing and keeps its own actions | D-057, Q-029 |
| Which flat key the interact verb is | **answered 2026-10-07: 상호작용 = Z (main) / G (alt)**, from the game's own keybinding menu and matching `PlayerInputController.Interact` — Q-034 closed; `interactables` is still empty (nothing found interactable yet) | D-058 |
| 특수 능력 / Special ability (SHIFT) | **answered 2026-10-07: the SCOUT's special ability IS the dash**, so it is not a separate VR action — `dash` forwards to SHIFT and stays on X | D-059, Q-035 |
| 수동 레벨 업 / Manual level up (T) | **revealed by the game's own menu; not mapped — Q-036** (left to the game's own UI unless the user decides to bring it into VR) | D-058 |
| Per-hand reload (왼손 / 오른손 장전) | **new 2026-10-07: a double tap of that hand's UPPER face button** (`reload_off` / `reload_dominant`). The flat game has only the single 재장전 = R, so the split is the mod's own per-weapon call (`Weapon.reload()`) and is unproven at runtime | D-059, Q-037 |
| The grip (squeeze) | **deliberately unbound** — the user deleted the assignment ("그립버튼에 키 할당은 없애고"), taking the D-056 main-fire gate with it | D-057 |
| The right stick left/right | **30-degree snap turn** per crossover, not an analog rate | D-057, Q-015/D-013 |
| Whether this game has any grabbable world object | **answered: no** (user report 2026-10-07) | D-055 |
| Two-hand linked aim (the M2 mode) | **deleted by the user 2026-10-07** | D-056 |

### 1.1 The decisive local finding

Scanning the active runtime's two DLLs for interaction-profile path strings returns exactly:

`ext/eye_gaze_interaction`, `google/daydream_controller`, `hp/mixed_reality_controller`, `htc/vive_controller`,
`htc/vive_pro`, `htc/vive_tracker_htcx`, `khr/simple_controller`, `microsoft/motion_controller`,
`microsoft/xbox_controller`, `oculus/go_controller`, **`oculus/touch_controller`**, `valve/index_controller`.

There is **no `/interaction_profiles/meta` path anywhere in this runtime**, so a Quest 3 Touch Plus controller is
expected to be presented through the **legacy `oculus/touch_controller` profile**, not `meta/touch_controller_plus`.
`ovrControllerType_Touch`/`_LTouch`/`_RTouch` and a `debug_use_touch_controller_buttons` switch in the same binary
agree: this runtime projects Quest controller data onto the legacy Touch profile.

Corroboration on the same machine: a shipping native OpenXR mod the user actually plays (EDF6VR 2.1.8, public source
`momotori01/EARTH-DEFENSE-FORCE-6-VR-MOD`, studied as a *map* only — no code copied) negotiates exactly five profiles
and its Touch table is `oculus/touch_controller` with `thumbstick`, `thumbstick/touch`, `thumbstick/click`,
`trigger/value`, `squeeze/value`, `a|b/click`, `menu/click` — the same component names used below.

**Limit:** a string inside a DLL proves the runtime knows the name, not that it selects it for this headset.
`xrGetCurrentInteractionProfile` at runtime is the authoritative test (verification item 1 below).

### 1.2 The game's own keybinding menu (D-058) — the flat-parity source of record

The v4 probe recorded *presses* but not what each key **means**, and it missed several keys entirely (no `F`, `Z` or `T`).
The game's own keybinding screen (키 바인딩 — 키보드 및 마우스 전용, Korean build; screenshot supplied by the user
2026-10-07) closes that gap and cross-checks against the `PlayerInputController` member list already recorded in D-031
(`Move/Look/Fire/Fire2/Reload/Jump/BloodyRush/SpecialAbility/Run/Interact/ArmL/ArmR/Start/Select`). The screen scrolls, so
rows outside the visible window may exist; this is what the window shows, not a claim of a complete list.

| Game action (menu label) | Primary | Alt | Map token | Note |
| --- | --- | --- | --- | --- |
| 오른쪽으로 이동 (move right) | D | RIGHT | `move` | WASD family |
| 발사 (Fire) | LMB | X | `fire_primary` / `fire_left_hand` | the VR split into two hands is VR-only |
| 점프 (Jump) | SPACE | SCROLL UP | `jump` | matches the 764 frames observed |
| **상호작용 (Interact)** | **Z** | **G** | `interact` | **answers Q-034**; matches `PlayerInputController.Interact` |
| 재장전 (Reload) | R | V | `reload_off` / `reload_dominant` (VR) | **flat meaning confirmed** — refutes the old "`r` is interact" guess; the per-hand VR pair on a Y/B double tap is **D-059** (Q-037) |
| 혈능력 (Blood ability) | F | C | `blood_ability` | the probe never recorded `F` |
| **특수 능력 (Special ability)** | **SHIFT** | CTRL | *none* | **not mapped — Q-035**; `PlayerInputController.SpecialAbility` |
| 왼쪽 로봇 팔 (left robot arm) | Q | FORWARD | `aux_weapon_1` | `PlayerInputController.ArmL` |
| 오른쪽 로봇 팔 (right robot arm) | E | BACK | `aux_weapon_2` | `PlayerInputController.ArmR` |
| 보조 발사 (alt fire) | RMB | CTRL | `fire_secondary_off` / `fire_secondary_dominant` | re-confirms Q-029 / D-057 |
| **수동 레벨 업 (Manual level up)** | **T** | L | *none* | **not mapped — Q-036** |

What it changes and what it does not: it **settles Q-034** and **corrects two records** (interact is Z, not unknown; reload
is 재장전, not the interact guess), and it **reveals** the two unmapped actions above. It changes **no VR binding** — the
action list, the controller tables and every D-057 pin stand as written. `bindings.flatMenuEvidence` stores the same rows
as data and the validator fails if the `상호작용 = Z`, `보조 발사 = RMB`, `재장전 = R`, robot-arm or unmapped-action rows
drift.

## 2. Roles, never hands (and right-handed only)

Every action is bound to a **role** — `dominant` (gun hand) or `off` (movement/utility hand); no downstream system may
ask for "the right hand". The roles resolve to **fixed** physical hands: `dominant` = right, `off` = left.

**Left-handed play is out of scope** by explicit user decision (2026-10-07: "왼손잡이는 고려하지 않는다", D-051). That
removed three things the earlier draft carried: the `UserDominantHand` swap, the second handedness variant the validator
had to prove, and the carrier-token indirection (a design-time catch that existed only because a left-handed player's
menu button sits on their *dominant* hand). Consequences, stated plainly: the mod will not ship a left-handed layout,
and Phase 1's checklist item "both handedness" is reduced to right-handed only. Re-adding it later needs a
roles-by-handedness table plus both columns proven again in the headset. `tests/verify-bindings.ps1` fails if a second
supported handedness or a `rolesByHandedness` table reappears without a decision.

## 3. The action map as prepared (D-057 is the current map)

**The user's rules that shape this map (all 2026-10-07):**

- **D-057:** *보조사격 is 보조발사 — the flat RMB alt-fire — and that is what the upper face button + trigger drives.*
  Also: **the grip assignment is deleted** ("그립버튼에 키 할당은 없애고"), so the trigger alone fires the main weapon;
  **`interact` = the right stick pushed up**; and **the right stick left/right is a 30-degree snap turn**.
- **D-059:** *each hand's weapon reloads on a **double tap** of that hand's upper face button* ("장전은 왼손 y버튼
  두번누르면 왼손 장전 오른손은 b버튼 두번 누르면 장전으로 만들 수 있겠어?"), while the **hold** on the same button stays the
  보조발사 selector — so Y/B carry three meanings (hold = selector, single tap = nothing, double tap = reload that hand).
  Also: **the scout's 특수 능력 IS the dash**, and the dash stays on the left **X** ("키 바인딩은 그대로 x키로 둬") — Q-035 is
  answered, not re-bound.
- **D-056:** the upper face button (left Y / right B) held + that hand's trigger selects that fire. The selector is an
  **explicit button**, never the absence of a grip. Also: `jump` = the right lower face button (A), `dash` = the left lower
  face button (X), Q/E = the two stick presses, the blood ability = the right stick **pulled down**, and **the two-hand link
  is deleted**.
- **D-055:** the grip was held for the main weapon (then a gate, not a selector), recentering was deleted, and this game has
  no grabbable object, so the grip was never a grab verb.
- **D-056 also corrected a description error.** On a Quest Touch controller **A and X are the LOWER face buttons and B and Y
  the UPPER ones**. The D-052/D-053 prose said the opposite while the bindings were already right; every "upper/lower"
  mention in this file is correct as of D-056.

**The words, once, because they are load-bearing (D-057):**

| Korean | English token in the map | What it is |
| --- | --- | --- |
| 주 무기 | main weapon (`fire_left_hand` / `fire_primary`) | that hand's own weapon, fired by its trigger alone |
| 보조발사 | secondary fire (`fire_secondary_off` / `fire_secondary_dominant`) | the flat **RMB alt-fire**; upper face button held + that hand's trigger |
| 보조무기 | auxiliary weapon (`aux_weapon_1` / `aux_weapon_2`) | the game's own **Q** and **E** keys, on the two stick presses |
| 혈능력 | blood ability | right stick **pulled down** |
| 상호작용 | interact | right stick **pushed up** |
| 대시 / 점프 | dash / jump | left X / right A |
| 장전 / 재장전 | reload (`reload_off` / `reload_dominant`) | that hand's own weapon; a **double tap** of its upper face button — the hold on that button stays 보조발사 |

| Action | Role | Control (Touch) | Flat parity | Required |
| --- | --- | --- | --- | --- |
| `move` | off (left) | `thumbstick` (vector2) | WASD | yes |
| `aux_weapon_1` | off (left) | `thumbstick/click` (tap) | Q (보조무기) | yes |
| `mod_bug_report` | off (left) | `thumbstick/click` hold 1.0 s | — | yes |
| `dash` | off (left) | `x/click` (**lower** face button) | **특수 능력** (SHIFT) — the scout's ability IS the dash (Q-035) | yes |
| `secondary_modifier` | both | `y` (left) / `b` (right) click — **modifier**, held (a single tap does nothing, a double tap reloads that hand) | — (VR-only) | yes |
| `fire_left_hand` | off (left) | `trigger/value` (trigger alone) | — (VR-only: the left hand's main weapon) | yes |
| `fire_secondary_off` | off (left) | `trigger/value` **+ Y held** | **보조발사** = the flat RMB alt-fire | yes |
| `mod_menu` | off (left) | `menu/click` | esc / pause | yes |
| `turn` | dominant (right) | `thumbstick/x` — **30° snap** per crossover | mouse-X | yes |
| `interact` | dominant (right) | `thumbstick/y` **pushed up** (threshold + re-arm) | 상호작용 = Z (Q-034) | yes |
| `blood_ability` | dominant (right) | `thumbstick/y` **pulled down** (threshold + re-arm) | frenzy ability | yes |
| `aux_weapon_2` | dominant (right) | `thumbstick/click` (tap) | E (보조무기) | yes |
| `jump` | dominant (right) | `a/click` (**lower** face button) | space | yes |
| `fire_primary` | dominant (right) | `trigger/value` (trigger alone) | primary fire | yes |
| `fire_secondary_dominant` | dominant (right) | `trigger/value` **+ B held** | **보조발사** = the flat RMB alt-fire | yes |
| `reload_off` | off (left) | `y/click` **double tap** (hold = the selector above) | 재장전 = R (the flat key is one; the per-hand split is the mod's call — Q-037) | yes |
| `reload_dominant` | dominant (right) | `b/click` **double tap** (hold = the selector above) | 재장전 = R (same) | yes |
| ~~`grip`~~ | — | **deleted by the user (D-057)** — the squeeze carries no action | — | — |
| ~~`fire_secondary`~~ | — | **merged into the two 보조발사 actions by D-057** | — | — |
| ~~`two_hand_link`~~ | — | **deleted by the user (D-056)** | — | — |
What changed and what is now free:

- **Nothing on either controller is free any more except `system/click` (runtime-reserved) and the two squeezes** — and the
  squeezes are free *by the user's decision*, not by omission: the grip assignment was deleted (D-057), so binding anything
  there again is a new decision, not a tidying-up. The two stick presses carry Q (left) and E (right), which **answered
  Q-030**.
- **The two-hand link is gone (D-056).** `two_hand_link` is no longer an action, `two_hand_link_toggle` is no longer a
  gesture, `manifest.aimDesign.modes` holds only `per_hand_independent`, and `modesRemoved` records why. There is no link
  radius, no support point and no `TwoHandMode` setting.
- **The grip is gone as an assignment (D-057).** `grab` → `grip` (D-055, because the user confirmed there is nothing to grab)
  → **deleted** (D-057, because the user removed the key assignment outright). `fire_left_hand`/`fire_primary` carry no
  `gate` any more, `tuning.gripFireMode` is deleted with it, and `bindings.deliberatelyUnbound` records `input/squeeze/value`
  so the validator can fail if it is ever bound again. Stage 5's "grab/throw recovery" item already had no grab verb to
  implement (the user's finding, not a scope cut); now it has no squeeze obligation either.
- **Every fire path ends in one of three game actions.** `fire_left_hand`/`fire_primary` are the flat primary-fire parity;
  `fire_secondary_off`/`fire_secondary_dominant` are **보조발사**, i.e. the flat RMB alt-fire (`fire_secondary_shot` in the
  game's API, D-051) that the D-055/D-056 data called the auxiliary fire — **Q-029 is answered**, and the two old names
  (`fire_aux_*` and the parked `fire_secondary`) were merged into this pair rather than kept as duplicates; `aux_weapon_1`/
  `aux_weapon_2` stay the game's own 보조무기 Q/E keys.

### 3.1 보조발사 is an explicit button, and nothing gates the main weapon any more (D-057 supersedes D-056)

The user's words: *"왼쪽은 Y키+LT로, 오른쪽은 B키+RT로 보조사격 발동하게 되나?"* (D-056), then *"보조사격은 평면 RMB
대체사격임, 한국어 번역본에는 보조발사라 되어있다. 앞으로 보조발사라 부르도록"* and *"그립버튼에 키 할당은 없애고"*
(D-057).

**What the action is, settled by the user — Q-029 is closed.** The upper-face-button fire is the flat **RMB alt-fire**,
which the Korean build calls **보조발사**. The data had two names for the same thing — `fire_aux_1`/`fire_aux_2` (the VR
path) and the parked `fire_secondary` (the flat path) — and D-057 merged them into `fire_secondary_off` /
`fire_secondary_dominant`. The Q/E **보조무기** keys are a different thing and keep their own actions
(`aux_weapon_1`/`aux_weapon_2`), so "보조발사 fires the Q/E weapon" is no longer a live reading of anything.

**Yes, the combination is our own logic, not an OpenXR feature.** OpenXR delivers per-control states (one action per
component); combining "button held" with "trigger pulled" is arithmetic the mod does when it decides which game call to make
(`PlayerController.fire_secondary_shot(EWeaponSlot)` for 보조발사, `fire(EWeaponSlot, …)` for the main weapon — D-051). It
is declared in `config/bindings.json` as the modifier split `gestures.secondary_fire_mode_off` / `_dominant` with
`gesture: "modifier"`, `modifier: "secondary_modifier"`, `held: secondary_fire`, `released: main_fire`,
`pressLatched: true`, and the validator fails if the split is missing, if the vocabulary changes, if the selector is not
bound on that hand, if the two fires stop sharing one trigger, or if the latch declaration disappears.

| That hand's trigger… | With the **selector** (upper face button) held | With it not held |
| --- | --- | --- |
| off (left) | `fire_secondary_off` — 보조발사, the flat RMB alt-fire | `fire_left_hand` — the left hand's **main** weapon, on the trigger **alone** |
| dominant (right) | `fire_secondary_dominant` — 보조발사, the flat RMB alt-fire | `fire_primary` — the right hand's **main** weapon, on the trigger **alone** |

- **Why the selector is a button (kept from D-056, and the user never reversed it).** D-055 made "grip released" the
  selector, which turned the main-weapon trigger into a *held* control as well: relaxing the grip out of habit fired the
  wrong weapon. An explicitly held button cannot be released by accident, so that failure mode stays gone.
- **What D-057 removed: the gate.** The user deleted the grip assignment ("그립버튼에 키 할당은 없애고"), so
  `fire_left_hand`/`fire_primary` carry **no `gate`** and `tuning.gripFireMode` is deleted with it. The trigger **alone**
  fires that hand's main weapon; the D-056 cost ("a trigger-only press fires nothing, which can read as a dead trigger") is
  gone. **Q-031 is answered** in the user's direction: no gate.
- **Precedence is now one line:** the selector held → 보조발사; otherwise → that hand's main weapon. The mode is still read
  at press time and **latched until release**, so pressing or releasing Y/B mid-burst cannot fire by itself.
- **The squeeze is deliberately unbound, and that is data.** `bindings.deliberatelyUnbound` records
  `input/squeeze/value` (both hands) with the reason and the decision number, and the validator fails if any action binds it
  or if that record disappears. Handing the squeeze a verb again — grab, throw, physics-hand squeeze, a new modifier — is a
  new user decision, not an agent tidy-up.
- **What the selector costs the game's own keys.** Y and B cannot carry Q/E, so the game's own 보조무기 keys sit on the
  **stick presses** (left = Q, right = E). That is the user's instruction, not a silent remap, and it closed Q-030.
- **What is still a game-side question, not a mapping question:** which `EWeaponSlot` each fire action drives, and whether
  Q/E are "fire" or "select". The game's API supports per-slot firing (`PlayerController.fire(EWeaponSlot, ref bool,
  float)`) and a per-slot 보조발사 call (`PlayerController.fire_secondary_shot(EWeaponSlot)` — D-051), but a keymap cannot
  prove a slot: Stage 6B item 10/10b does.
- **The old names must not come back.** `fire_aux_1`/`fire_aux_2`/`fire_secondary` as actions, `aux_modifier` and the
  `aux_fire_mode_*` splits are all refused by `tests/verify-bindings.ps1`; the map has exactly one 보조발사 pair, per hand.



### 3.2 Locomotion: move/dash on the left, jump + the full right stick on the right (D-057 supersedes D-056)

The user revised this feel five times; D-057 is the current state and the simplest one yet, because every action it added
or moved is a real button or a named axis direction:

- **Chosen (D-057): `turn` = the right stick X axis, as a 30-degree SNAP turn.** Q-015/D-013 had already fixed snap-only
  turning, so what D-057 adds is the increment and the direction pair in data: `turn.turnMode = "snap"`,
  `turn.snapDegrees = 30`, `tuning.snapTurnFromRightStickX` (`leftDirection: negative`, `rightDirection: positive`,
  `engage 0.7` / `release 0.3`). One step per crossover and a re-arm past `release`, so holding the stick at the edge cannot
  spin the player. The flat game's own mouse-X look must be suppressed while snap turn is active; the step size belongs in
  Stage 9's comfort section. The validator fails if the mode, the 30 degrees, the direction pair, the re-arm or the
  single-occupancy of the X axis changes.
- **Chosen (D-057): `interact` = the right stick PUSHED UP.** It is a discrete action read from an analog axis, so it needs
  a threshold plus a re-arm: `tuning.interactFromRightStickUp` (`direction: positive`, `engage 0.7` / `release 0.3`). It
  shares the Y axis with the blood ability, and that sharing is *declared*, not implied: `gestures.right_stick_y_axis_split`
  (`gesture: "axisDirection"`, `target: interact`, `base: blood_ability`, `directions {interact: positive, blood_ability:
  negative}`). The validator fails if the axis carries anything other than those two actions, if the split disappears or is
  renamed, if either direction flips, or if either `engage` slips out of its range.
- **Chosen (D-056, kept): `blood_ability` = the right stick PULLED DOWN.** `tuning.bloodAbilityFromRightStickDown`
  (`engage -0.7`, `release -0.3`); same threshold/re-arm shape as interact, opposite sign. The shared dead band
  (`-0.3 … +0.3`) is what keeps a near-centre stick from firing either.
  - *Cost, stated openly:* the right stick now carries turn (X) plus two discrete actions (Y up/down), so a hard push while
    turning is a real gesture on the stick the player is turning with. Stage 3 items 2/8 measure it; if this runtime reports
    the opposite sign, the two threshold entries swap signs together and nothing else changes.
- **Chosen (D-056, kept): `dash` = the left **lower** face button (X), `jump` = the right **lower** face button (A).** Plain
  buttons: no threshold, no re-arm, and the validator fails if `dash` regains a `fromAxis` or if the D-055
  `tuning.dashFromRightStickUp` entry reappears. The dash's direction still comes from the left stick the player is already
  moving with, and a double tap on A still maps onto the game's own double jump.
- **Why the D-055 right-stick-up dash never came back (Q-032 is closed by D-057):** the user has now given the stick's
  *positive* Y direction to `interact`, so the old dash path is not merely dropped — it is occupied. Restoring it would mean
  moving interact, which is a new decision, not a config edit.
- **`aux_weapon_1` (Q) = the left stick press, `aux_weapon_2` (E) = the right stick press.** The game's own 보조무기 keys
  sit there because Y/B are the 보조발사 selectors — see §3.1. A 1.0 s hold on the left one is the bug report.
- **Knock-on effects: the squeezes are now the only unused controls.** The left hand uses six of seven: stick (`move`),
  stick press (Q + bug report), X (`dash`), Y (`secondary_modifier`), trigger (main + 보조발사), menu. The right hand uses
  six of seven: stick X (`turn`), stick Y (`interact` up / `blood_ability` down), stick press (E), A (`jump`), B
  (`secondary_modifier`), trigger (main + 보조발사). **`system/click` stays reserved and unbindable**, and both
  `input/squeeze/value` inputs are deliberately unbound (D-057) — recorded in `bindings.deliberatelyUnbound`, refused if
  bound again.
- `tests/verify-bindings.ps1` pins this layout: it fails if any pinned action/control pair moves, if `jump` goes back to the
  left hand or `dash` to the right, if the two stick presses stop carrying Q/E, if `secondary_modifier` leaves the upper face
  buttons, if the 보조발사 splits disappear or change vocabulary, if `grip`/`gate`/`gripFireMode` return, if the Y-axis split
  disappears or either threshold direction flips, if the snap turn stops being 30 degrees, if `two_hand_link` /
  `grip_fire_mode_*` / `fire_aux_*` / `fire_secondary` / `aux_modifier` reappear, or if `mod_recenter` reappears anywhere.

Design notes that are load-bearing, not decoration:

- **`system/click` is never bound.** The runtime reserves it; a binding there silently does nothing or summons the
  system menu. It stays listed in `bindings.json` as `RESERVED_NOT_BINDABLE` so a later edit cannot "helpfully" use it.
- **Hold vs tap on one button.** The in-VR bug trigger must exist (highest-value diagnostic in the project) and buttons
  are scarce, so `mod_bug_report` is a 1.0 s hold and `aux_weapon_1` (Q) a tap on the **same left stick press** (D-056 moved
  the pair here when Y became the auxiliary-fire selector); a quick tap cannot fire the hold.
- **Recenter has no controller binding any more.** The user deleted it (D-055), which answers Q-026: recentering lives in
  the mod's settings menu (Stage 9's "two doors" rule) and nowhere else. If headset drift ever makes a controller path
  necessary, it returns as one line of data — but it does not silently keep a button "just in case".
- **`mod_menu` lives on `menu/click`, which only the left controller carries.** In this right-handed-only design the left
  hand is always the `off` role, so `$menu` resolves on it directly and no carrier indirection is needed (D-051 removed
  the `handOnlyTokens` table, whose only purpose had been to keep the menu button for a left-handed player). The oldest
  consequence still applies: the in-VR settings menu must *also* be reachable from the mod's own overlay and, where the
  game allows it, from a "VR" entry injected into the game's pause menu (Stage 9's "two doors" rule).
- **Four controls carry two actions each**, all documented in `gestures` with an explicit `base`: one *gesture* split —
  `aux_weapon_1` tap vs `mod_bug_report` 1.0 s hold on the left stick press — two *modifier* splits added by D-056 and
  renamed by D-057, one per trigger (`fire_left_hand`/`fire_secondary_off` on the left, `fire_primary`/
  `fire_secondary_dominant` on the right, selected by that hand's `secondary_modifier` and declaring `pressLatched: true`)
  — and one *axisDirection* split added by D-057, the right stick Y axis (`interact` up / `blood_ability` down). The
  validator refuses an undocumented duplicate, refuses a modifier split whose selector is not bound, refuses one that does
  not declare the per-press latch, refuses a base that is itself a modifier's target, and refuses an axisDirection split
  whose directions do not match the two actions' own `fromAxisDirection`. That is what keeps a future "just add a button"
  edit honest.
- **Capacitive inputs are not load-bearing.** `thumbstick/touch`, `trigger/touch`, `thumbrest/touch` exist as strings,
  but Touch Plus finger presence is not guaranteed to survive the streaming runtime, so no design (grab realism, finger
  poses) may depend on them. Stage 3 records which ones actually report.
- **The generic fallback degrades honestly.** `khr/simple_controller` has no thumbstick, so locomotion cannot be
  mapped; the plan is to leave movement to the game's own desktop input instead of pretending the fallback is complete.

### 3.3 The squeeze is deliberately unbound, and the hand-closed pose is still an open design question (D-057 supersedes D-056/D-055)

- **What the squeeze was, in one line of history.** `grab` (D-052/D-053, as a world-interaction verb) → `grip` with
  `role: modifier` (D-055, because the user reported there is nothing to grab: *"이 게임은 잡을 오브젝트가 없어"*) → the
  **main-fire gate** (D-056) → **deleted entirely** (D-057: *"그립버튼에 키 할당은 없애고"*).
- **What exists today: nothing.** There is no `grip` action, no `gate` on either main fire, no `tuning.gripFireMode`, and no
  binding on `input/squeeze/value` in either column. `bindings.deliberatelyUnbound` records the control, the decision and the
  reason, exactly so a later edit cannot "helpfully" fill it; `tests/verify-bindings.ps1` fails if an action binds
  `input/squeeze/value`, if the `grip` action reappears, if either main fire regains a `gate`, if `gripFireMode` returns, or
  if the `deliberatelyUnbound` record disappears.
- **Why this is not a loss of the grab verb either.** The grab verb had already gone at D-055 (nothing to grab). Stage 5's
  "grab/throw recovery" item therefore has no grab verb *and now no squeeze obligation* to implement; hand collision,
  weapon-sway and the physics-hand pose keep their own Stage 5 items.
- **What is honestly not known:**
  - **Whether the game's own code wants a closed hand** (e.g. a fist pose) that the mod should mirror. Nothing in the data
    says so; `config/manifest.json` `weapons` is still `[]` and `grab_points` is an unused placeholder (the link that would
    have consumed it is deleted). If Stage 5 finds a fist/pose state, it needs a new decision to reach the hardware — the
    squeeze is not a free control that a subsystem may claim by itself.
  - **Whether the selectors really cannot fire by themselves** (the per-press latch of §3.1). This is now purely a
    Stage 6B measurement; the D-056 gate question that went with it is closed (there is no gate).
  - **Whether `squeeze/value` even reports on the legacy `oculus/touch_controller` profile.** With the assignment gone it
    stops being load-bearing: Stage 3 item 2 still logs both squeezes so the answer is recorded, and "it reports" is then
    only useful as evidence for a *future* squeeze decision.

## 4. What Stage 3 must verify on the headset (bounded — still two short sittings)

D-057 renamed the fire pair, deleted the grip assignment and gave the right stick three jobs (turn, interact up, ability
down), so item 2's press order and items 4/8–10 were rewritten; the list still does not fit one 10-minute sitting. Items 1–7
are the original session; items 8–10 are a second, shorter one, and the Korean card in
`docs/playtests/HEADSET-CHECKLIST.md` is split the same way.

1. Log `xrGetCurrentInteractionProfile` for both hands as soon as a session exists; expect
   `oculus/touch_controller`. If it reports `meta/touch_controller_plus`, the family row already exists and the fix is a
   switch of `controlsRef`, not new code.
2. Log every action's state (active + value) for ten seconds while the user presses each control once **in order**, so a
   missing component reads as "never active" instead of as a mysterious dead button. The **D-057 order** is: left stick
   (all directions), left stick press (`aux_weapon_1`/Q on a tap — and hold it 1.0 s for `mod_bug_report`), left X
   (`dash`), left Y (`secondary_modifier`, the 보조발사 selector), left menu, left trigger **with Y held**
   (`fire_secondary_off`), then **without Y** (`fire_left_hand` — it must fire: the gate is gone), left squeeze (log the
   value only; **no action is expected**), right stick X **left then right** (`turn` — the 30° snap), right stick
   **pushed up** (`interact` — report how far up it had to go), right stick **pulled down** (`blood_ability` — same
   question), right stick press (`aux_weapon_2`/E), right A (`jump`), right B (`secondary_modifier`), right trigger with B
   held (`fire_secondary_dominant`) then without it (`fire_primary`), right squeeze (log the value only, no action).
3. Re-measure the capacitive list (`touch_like.capacitiveToConfirm`) and delete whatever does not report.
4. Confirm the `turn` axis sign **and the step**: pushing the stick left turns the player **left 30°**, pushing right turns
   30° right, one step per push, and holding the stick at the edge does **not** keep spinning (the re-arm).
   `tuning.snapTurnFromRightStickX` carries both numbers; if the sign is inverted, the two direction names swap.
5. Assert every `required: true` action is bound for the active family — statically checked today by
   `tests/verify-bindings.ps1`, asserted in-game once Stage 3 exists.
6. Confirm nothing is bound to `system/click` and that pressing it still opens the runtime overlay.
7. Confirm **which physical hand reports `menu/click`** — the file assumes the left one, which is also the `off` hand in
   this right-handed-only design. If the runtime reports it on the right instead, the whole `handProfiles` layout is
   wrong and the fix is a data change plus a re-run, not a one-liner.
8. **D-057 feel, while the session is already open (one axis, two directions):** push the right stick **up** and report
   whether `interact` fires at a comfortable point, then **pull it down** and do the same for the blood ability. The two
   thresholds are data (`tuning.interactFromRightStickUp`, `tuning.bloodAbilityFromRightStickDown`), so the answer changes
   numbers — and the real question is whether either one ever fires by accident while the player is turning with the same
   stick. If this runtime reports the opposite sign, both entries swap signs together.
9. **The 보조발사 selector (D-057, the user's rule):** with Y held, pull the left trigger and confirm it is **보조발사**
   (the flat RMB alt-fire); with Y released, pull the trigger and confirm it is that hand's **main** weapon — **with no
   grip held**, because the gate is deleted. Then press or release Y **while already firing** and confirm no stray shot
   appears (the per-press latch). Repeat on the right hand with B. This is the input half only — which `EWeaponSlot` each
   fire drives is **Stage 6B item 10**, and whether Q/E are "fire" or "select" stays a game-side question.
10. **Confirm the two squeezes report (or not) and that they do nothing.** They are no longer load-bearing (D-057), but the
    answer is still recorded: if a squeeze reports and pressing it changes nothing, the map is proven correct; if a squeeze
    silently drives something, that is a sev-2 Stage 3 finding, not a naming detail.

## 5. Pre-registered expectations and honest failure modes

- **Expected:** both hands report `oculus/touch_controller`; every action in section 3 reports activity; capacitive
  entries report only on the thumbstick, and possibly not at all.
- **If `menu/click` never reports:** the in-VR settings menu is reachable only through the hold gesture and the game-side
  door; recorded as a limitation, not patched with a reserved button.
- **If the runtime switches to `meta/touch_controller_plus`:** the mapping is unchanged; `controlsRef` follows the
  reported profile.
- **If a required action has no working component in the active family:** that is a sev-2 bug against Stage 3, not a
  silent remap. The action stays in the table with the failure recorded.
- **`fire_secondary` no longer exists** (D-057): the old parked alt-fire and the old `fire_aux_*` pair were the *same*
  thing under two names, so they were merged into `fire_secondary_off`/`fire_secondary_dominant`. What is still a recorded
  gap with a named owner (Stage 6B item 10b) is **which slot a 보조발사 touches** — not whether it has a binding.
- **The squeeze doing nothing is the expected result, not a broken control** (D-057): the assignment was deleted by the
  user. Stage 3 logs the raw squeeze values for the record; if a squeeze turns out to drive something, that is a sev-2
  finding, and if Stage 5 wants a closed-hand pose it needs a new decision before it may claim the control.
- **The 보조발사 selector is a rule, not a binding:** if Y/B never reports, `fire_secondary_*` is unreachable and the
  game's own Q/E keys on the stick presses are the only auxiliary path left — a sev-2 Stage 3 finding, recorded, not
  patched by re-pointing the selector at the squeeze (the D-055 scheme the user already replaced).
- **`fire_left_hand` is a design intent, not a promise:** if Stage 6B cannot drive the `LEFT_HAND` slot on its own, the
  report is a stall entry in `docs/BUGS.md` — not a relabel of the right trigger as "both hands".
- **Two discrete actions on one axis are a rule too:** if the stick never reaches `engage` (either direction), that action
  never fires; if a re-arm is too shallow, one push fires it repeatedly; if the two directions' thresholds ever overlap, the
  dead band is gone. All of it is tuning, not map changes — and the signs are checked at Stage 3 before the numbers mean
  anything.
- **The snap turn is a rule too:** if the axis never reaches `engage`, the player cannot turn; if the re-arm is too shallow,
  one push spins repeatedly. `degrees: 30` is the user's number, and Stage 3 item 4 checks that one push is exactly one step.
- **This file changes nothing about Stages 4–9.** Locomotion, comfort (snap turn only, Q-015/D-013 — now with the 30°
  increment D-057 pinned), hands and HUD keep their own pre-briefs.

## 6. Open questions

- **Q-024 — answered 2026-10-07:** the user asked for the *per-hand independent* reading ("양손 따로 조준 및 사격") and
  for its feasibility to be checked, so the mapping in section 3 stands as written. The feasibility work is in
  `docs/TWO-HAND-AIM.md` section 2.1, and the game-side evidence for it is real (D-051).
- **Q-025 — answered 2026-10-07:** one keymap with roles, right-handed only.
- **Q-026 — answered 2026-10-07 (D-055):** recentering was deleted from the controller map. It lives in the mod's
  settings menu only; the right stick press is free and `mod_recenter` no longer exists as an action.
- **Q-027 — answered 2026-10-07 (D-055), renamed by D-057:** the user gave the alt-fire question a concrete rule rather
  than a control, so the upper-face-button fire is a real action pair. D-056 chose *how* it is selected (upper face button +
  trigger instead of grip-released + trigger); D-057 settled *what it is* (the flat RMB alt-fire = 보조발사) and renamed the
  pair to `fire_secondary_off`/`fire_secondary_dominant`.
- **Q-028 — answered 2026-10-07 (D-055), closed by D-057:** the grip is not a "grab" action because the user reported that
  this game has **no grabbable object**, it was never a link gesture (the link is deleted, D-056), and it is no longer the
  main-fire gate either — D-057 deleted the assignment outright, so the squeeze carries no action at all and Stage 5 has no
  grab verb and no squeeze obligation.
- **Q-029 — ANSWERED 2026-10-07 (D-057):** the user settled it directly — *"보조사격은 평면 RMB 대체사격임, 한국어 번역본에는
  보조발사라 되어있다. 앞으로 보조발사라 부르도록"* — so the action is the **flat RMB alt-fire**, the old `fire_aux_*` pair and
  the parked `fire_secondary` were **the same thing under two names** and are merged into
  `fire_secondary_off`/`fire_secondary_dominant`, and the Q/E **보조무기** keys remain their own actions
  (`aux_weapon_1`/`aux_weapon_2`). The only remaining game-side question is *which slot* a 보조발사 touches (Stage 6B item
  10b).
- **Q-030 — resolved 2026-10-07 (D-056):** the user answered it by using both stick presses — **Q on the left, E on the
  right** — which freed the upper face buttons for the 보조발사 selector. The only controls not carrying an action are
  `system/click` (runtime-reserved) and the two squeezes, and the latter is a **D-057 decision**, not an empty slot.
- **Q-031 — ANSWERED 2026-10-07 (D-057):** the user deleted the grip binding ("그립버튼에 키 할당은 없애고"), so there is **no
  gate**: the trigger alone fires that hand's main weapon, `gate` and `tuning.gripFireMode` are deleted. The D-056 "dead
  trigger" cost is gone; the validator fails if either returns.
- **Q-032 — ANSWERED 2026-10-07 (D-057):** the D-055 right-stick-up dash cannot come back as a second path, because the
  user gave that same positive Y direction to **`interact`**. Restoring a stick-up dash would now mean moving the interact
  verb — a new decision, not a config edit.
- **Q-033 — open (D-056, housekeeping, blocks nothing):** the in-VR bug report lives on a **1.0 s hold on the left stick
  press** (a tap is Q), because Y is the 보조발사 selector and cannot carry a hold. Default: the stick-press hold.
  Alternatives if that feels cramped: the right stick press (a tap is E, so the same tap/hold shape) or the menu button's
  long press.
- **Q-034 — ANSWERED 2026-10-07 (D-058):** the game **does** have an interact verb, and it is **상호작용 = Z (main) / G
  (alt)**. The game's own keybinding menu settles it, corroborated by `PlayerInputController.Interact` (D-031); the v4 probe
  simply never recorded a press, and the old "`r` is the most likely candidate" guess is refuted (`r` is 재장전). The VR
  `interact` action now forwards to the game's own interact input (Z); `manifest.interactables` stays empty until something
  interactable is actually found.
- **Q-035 — open (D-058, game-side, blocks nothing):** is the mod's **`dash`** the game's **특수 능력 (Special ability,
  SHIFT)**, or a separate mechanic? The menu labels SHIFT as 특수 능력 and the assembly has
  `PlayerInputController.SpecialAbility`, while `dash.flatParity` claims "shift" from the probe. Default: treat SHIFT as the
  special ability and keep `dash` as the user's dash until a playtest separates them; if they are the same thing, `dash`'s
  flat parity is corrected, not re-bound.
- **Q-036 — open (D-058, coverage, blocks nothing):** the menu shows **수동 레벨 업 = T**, an action the map does not carry
  and the user never mentioned. Default: leave it to the game's own UI and do **not** invent a VR binding; if the user wants
  it in VR it becomes a new action with a new decision.


## 7. Honesty / limitations

- No XR code, no hook, no loader install, no game launch, no game or save write, no runtime setting change.
- Profile **paths and component names** are evidence from the local runtime binary and from a working reference mod on
  this machine; they are not a live session report. Nothing here is "bound" or "tested".
- `virtualdesktop-openxr.dll` is read-only-scanned bytes; the runtime is not loaded by this work and VDXR is not
  modified or switched.
- The reference mod (EDF6VR) is third-party, GPL-family, and was read as design information only; no code was copied
  into this project, and none of its files are redistributed.
- **Left-handed play is not supported** (D-051). The mapping, the action list and the validator are right-handed-only,
  and this file must not be read as claiming otherwise.
- Q-024 through Q-028 are answered, Q-030 was answered by D-056, **D-057 answered Q-029 (보조발사 = the flat RMB alt-fire),
  Q-031 (no grip gate) and Q-032 (the stick-up dash is occupied by interact)**, and **D-058 answered Q-034 (the interact
  verb is 상호작용 = Z)**. What remains is **Q-033** (where the bug report lives), **Q-035** (is `dash` the game's 특수 능력?)
  and **Q-036** (should 수동 레벨 업 be mapped?) — all defaulted and non-blocking. Answers can rearrange the table but not
  the action list, which follows the game's observed controls (section 1).

## D-104-iso-promotion (2026-10-17) — Anisotropic 필터의 입력 점유 부재

0.7.1 부터 D-104 의 `Anisotropic` 스위치는 `VhvrSettings.exe` 의 체크박스 + CLI `--aniso` / `--no-aniso` 로 노출된다. 이 키는 **GPU 텍스처 필터링 단계**(`QualitySettings.anisotropicFiltering`, `Texture.SetGlobalAnisotropicFilteringLimits`)이므로 **입력 점유와 무관** — D-057 의 right stick 점유 및 다른 모든 입력 액션과 충돌하지 않는다. `Config.Anisotropic` 분기는 `OpenXrHost.cs:879` 에 이미 존재.

