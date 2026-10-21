# 컨트롤러 키 설정 (현재 설치본 기준) / Controller Key Map (as installed)

기준 설치본: Stage0 **0.9.6** (`BepInEx\plugins\VampireHuntersVR.Stage0.dll`, SHA256 `129361927619F42C3A625D99EB85EB04B781DF981690C104D17FB4F35BE85F2F`) +
extras **0.9.5 후보** (`BepInEx\plugins\VhvrExtras.dll`, SHA256 `52E6707290D430F25615CF50EFAA7ADA3013DD21527FB7861D999BFDDB1CA533`).

- 이 표는 **설계가 아니라 런타임 코드 그대로**입니다. 근거: `src\interaction\HandInput.cs`(D-091/D-093/D-099/D-101/D-114/D-184),
  `src\interaction\HandAim.cs`(`AbilityStickPolicy`, `StickLatchPolicy`, `SnapTurnPolicy`, `ReloadLatchPolicy`, `HeadReachPolicy`),
  `src\interaction\WeaponRuntimeBridge.cs`(D-186), `src\interaction\HandUi.cs`(D-105), `extras\VhvrExtras\**`(D-191/D-192/D-201),
  라이브 설정 `BepInEx\config\vhvr-openxr-host.json`(2026-10-18 확인: `HandInput=true`, `SnapTurnDegrees=30`).
- **우완 전용**: `dominant` = 오른손, `off` = 왼손. 좌완 레이아웃은 범위 밖(D-051).
- 설계 문서(`docs\INPUT-MAP.md`, `config\bindings.json`)에는 D-057/D-059 안(더블탭 장전, 그립 게이트 등)이 남아 있으나
  **D-114가 그 인체공학을 대체**했습니다. 실기 동작은 이 문서를 신뢰하십시오.
- **D-206 (BUG-204)**: 그립은 D-057대로 평소 미할당이고, **엔지니어 건설 모드(`PlayerController.is_turret_mode`) 안에서만**
  좌그립=이전 종류 / 우그립=다음 종류 / 양쪽 동시=설치 확정(`Fire`)으로 살아납니다. 근거는 게임 자체 텍스트
  `skill_desc_engineer_1`("Press {action=Special Ability} to enter build mode or switch blueprints. Press {action=Fire} to build the turret")
  와 게임 InputActionAsset(`artifacts\_d204-input-bindings.txt`)입니다.

## 1. 표 — 한국어

| 입력 | 동작 | 세부/근거 |
| --- | --- | --- |
| 왼쪽 스틱 | 이동 (360° 아날로그) | 헤드셋 정면 기준으로 회전, 데드존 **0.18**. 일시정지 중에는 같은 스틱이 게임 UI 커서를 움직임 |
| 왼쪽 스틱 클릭 | 왼쪽 로봇 팔 (게임 `Q`) | 일시정지 중에는 발동하지 않음(`alive=false`) → 메뉴에서는 리센터 코드로만 쓰임 |
| 왼쪽 X (아래 얼굴) | 상호작용 (게임 `Z`) | D-101 / D-184. **D-206 교정**: 예전에 같이 보내던 `Select`(View 버튼)는 터렛 설치에 **무효**였다(BUG-204) — 설치 확정은 아래 그립 코드가 맡는다 |
| 왼쪽 Y (위 얼굴) | **머리 30 cm 이내에서 누르면 왼손 장전**, 그 밖에서 누르면 **왼손 보조발사(대체사격, 게임 `RMB`)** | 누른 "그 순간"의 거리로 결정, 이후 손을 멀리 옮겨도 유지 (D-101 / D-186) |
| 왼쪽 트리거 | 왼손 무기 주 발사 | 누름 ≥ **0.6**, 해제 < **0.35**. **Y를 누르고 있는 동안은 그 손의 주 발사가 막힘** |
| 왼쪽 메뉴(≡) | 일시정지 (게임 `Start`/ESC) | 레벨업 카드 중에는 무시(D-094) |
| 왼쪽 그립(스퀴즈) | 평소 **미할당**(D-057). **엔지니어 건설 모드에서만** = 이전 터렛 종류(한 칸) | D-206 (BUG-204). 게임이 `PlayerController.is_turret_mode == true`라고 말하는 동안에만 동작. 그립을 **놓을 때** 한 칸 (다른 그립과 함께 눌린 적이 있는 그립은 코드로 인정해 칸 이동 없음) |
| 오른쪽 스틱 좌/우 | **스냅 턴: 30°(기본) / 45° / 끄기** | 한 번 밀 때 1회(≥0.7), 중앙 복귀(<0.3) 후 재무장. **D-211: 헤드 프레임에서 시야를 세션 view-yaw 오프셋으로 돌립니다** — 두 눈 오프셋과 두 눈 회전, 그리고 손/조준 합성 base에 같은 회전을 곱하고 앵커(플레이어가 서 있는 자리)는 건드리지 않아 제자리에서 돕니다. 값은 `VhvrSettings.exe`의 "Snap turn (right stick)"에서 선택(BUG-203 수정) |
| 오른쪽 스틱 위 | 혈능력 (게임 `F`) | ≥0.7 진입 / <0.3 해제 + 축 우세 규칙(대각선 오발 방지) |
| 오른쪽 스틱 아래 | 특수 능력 = 스카우트 대시 (게임 `SHIFT`) | 위와 동일한 래치 정책 |
| 오른쪽 스틱 클릭 | 오른쪽 로봇 팔 (게임 `E`) | 일시정지 중 미발동 |
| 오른쪽 A (아래 얼굴) | 점프 (게임 `SPACE`) | 일시정지 중에는 확인(Submit) 역할 |
| 오른쪽 B (위 얼굴) | **머리 30 cm 이내 = 오른손 장전**, 밖 = **오른손 보조발사** | 왼쪽 Y와 대칭 |
| 오른쪽 트리거 | 오른손 무기 주 발사 | 왼쪽과 동일한 임계값 |
| 오른쪽 시스템(○) | **사용 안 함 — 런타임 예약** | D-002/D-050 |
| 오른쪽 그립 | 평소 미할당(D-057). **엔지니어 건설 모드에서만** = 다음 터렛 종류(한 칸) | D-206 |
| 양쪽 그립 동시 (건설 모드) | **터렛 설치 확정** = 게임의 `Fire` 액션 | D-206 (BUG-204). `FirePressedInput(true/false)` + 가상 패드 `rightTrigger`를 **동시에** 올리고, 어느 한 쪽을 놓는 즉시 둘 다 내린다 |

### 1b. VR 메뉴 화면 (extras, D-191/D-192/D-201)

| 입력 | 동작 |
| --- | --- |
| 양손 트리거 (≥0.5) | 각 손 레이저로 메뉴 항목 클릭 (게임 EventSystem 필요) |
| 레이저가 리스트 위에 있을 때 **그 손의 스틱 상/하** | 메뉴 스크롤 (데드존 0.15, `ScrollSpeed` 3 = 프레임당 3노치) |
| **양쪽 스틱 동시 클릭** | **메뉴 패널 리센터** — 지금 보는 방향 앞에 패널 재배치 (`[MenuScreen] RecenterChord=true`, 신규 D-201) |

### 1c. 인게임 UI (HUD·일시정지 메뉴·레벨업 카드)

| 입력 | 동작 |
| --- | --- |
| 손 레이저 + 트리거 (≥0.6) | 캔버스의 버튼/토글 클릭 |
| 레벨업 카드 | 레이저로 카드 위에 올린 뒤 트리거 → 카드 선택 + 게임 자체 Submit(A) 0.2초 펄스로 확정 |
## 2. Table — English

| Input | Action | Detail / basis |
| --- | --- | --- |
| Left stick | Move (360° analog) | Rotated to the **headset heading**, deadzone **0.18**. While paused the same stick drives the game's UI cursor |
| Left stick click | Left robot arm (game `Q`) | Does not fire while paused (`alive=false`), so in the menu it only feeds the recenter chord |
| Left X (lower face) | Interact (game `Z`) | D-101 / D-184. **Corrected by D-206**: the `Select` (View button) action that used to go with it is provably **inert** for turret placement (BUG-204) - the grip chord below carries the confirm |
| Left Y (upper face) | **Press within 30 cm of the head = reload the left hand**; press further out = **left alt fire (secondary, game `RMB`)** | Decided by the head distance at the press edge, then latched until release (D-101 / D-186) |
| Left trigger | Left-hand weapon, main fire | Press ≥ **0.6**, release < **0.35**. **While Y is held that hand's main fire is blocked** |
| Left menu (≡) | Pause (game `Start`/ESC) | Ignored during level-up cards (D-094) |
| Left grip (squeeze) | **Unbound by default** (D-057). **Inside the engineer's build mode only**: previous turret blueprint (one step) | D-206 (BUG-204). Live only while the game reports `PlayerController.is_turret_mode == true`; a step happens when the grip is **released**, and never for a grip that was held together with the other one (that is the chord) |
| Right stick left/right | **Snap turn: 30° (default) / 45° / off** | One turn per push (≥0.7), re-arms after the stick returns (<0.3). **D-211: in the head frame the host turns the VIEW by a session view-yaw offset** — both eye offsets, both eye orientations and the hand/aim composition base get the same rotation, and the anchor (where the player stands) is untouched, so you turn in place. Pick the step in `VhvrSettings.exe` → "Snap turn (right stick)" (BUG-203 fix) |
| Right stick up | Blood ability (game `F`) | ≥0.7 engage / <0.3 release + dominant-axis rule (no diagonal misfires) |
| Right stick down | Special ability = the scout's dash (game `SHIFT`) | Same latch policy |
| Right stick click | Right robot arm (game `E`) | Does not fire while paused |
| Right A (lower face) | Jump (game `SPACE`) | Acts as Submit while paused |
| Right B (upper face) | **Within 30 cm of the head = reload the right hand**, further out = **right alt fire** | Mirror of the left Y |
| Right trigger | Right-hand weapon, main fire | Same thresholds as the left |
| Right system (○) | **Not used — runtime reserved** | D-002/D-050 |
| Right grip | Unbound by default (D-057). **Inside build mode only**: next turret blueprint (one step) | D-206 |
| Both grips at once (build mode) | **Confirm the turret placement** = the game's `Fire` action | D-206 (BUG-204). Raises `FirePressedInput(true)` **and** the virtual pad's `rightTrigger` together, and drops both the moment either grip is released |

### 2b. VR menu screen (extras, D-191/D-192/D-201)

| Input | Action |
| --- | --- |
| Either trigger (≥0.5) | Click a menu item with that hand's laser (needs the game's EventSystem) |
| **That hand's stick up/down** while its ray is over a list | Scroll the menu (deadzone 0.15, `ScrollSpeed` 3 = three notches per frame) |
| **Both thumbsticks clicked at once** | **Recenter the menu panel** — re-places it in front of where you are looking (`[MenuScreen] RecenterChord=true`, new in D-201) |

### 2c. In-game UI (HUD, paused menu, level-up cards)

| Input | Action |
| --- | --- |
| Hand laser + trigger (≥0.6) | Click a canvas Button/Toggle |
| Level-up card | Hover the laser over a card and press the trigger → the card is selected and the game's own Submit (A) is pulsed for 0.2 s to confirm |

## 3. 평면(게임 자체) 키 대응 / Flat-key parity

이 모드는 OS 키보드·마우스 입력을 합성하지 않고 게임 자체 입력 API(가상 패드 + `PlayerInputController`)를 구동합니다.
The mod synthesizes no OS keyboard/mouse input; it drives the game's own input API (a virtual pad plus `PlayerInputController`).

| VR 동작 (VR action) | 게임이 받는 동작 (game receives) |
| --- | --- |
| 주 발사 (main fire) | `firePressing` (LMB) |
| 보조발사 (alt fire) | `fire2Pressing` (RMB) |
| 상호작용 (interact) | `Interact` (`Z`) |
| 점프 (jump) | `Jump` (`SPACE`) |
| 혈능력 (blood ability) | `BloodyRushInput` (`F`) |
| 특수 능력 (special ability) | `SpecialAbilityInput` (`SHIFT`) |
| 로봇 팔 L/R (robot arms) | `ArmL`/`ArmR` (`Q`/`E`) |
| 장전 (reload) | the game's own `Weapon.reload()` family (`R`) |
| 일시정지 (pause) | `StartInput` (`ESC`) |
| 터렛 설치 확정 (turret confirm) | 게임 `Fire` 액션 = `FirePressedInput(true)` + 가상 패드 `rightTrigger` (평면 키보드에서는 `X`가 이 액션) — D-206. 예전 `SelectInput`(View)은 무효였다(BUG-204) |
| 터렛 종류 전환 (blueprint step) | 게임 표면 3안(config `TurretCycle`) + `navigationDirection` 필드: `Stick` = 패드 왼스틱 X 0.9 (기본) / `Dpad` = `<Gamepad>/dpad/좌,우` / `Special` = 특수능력(buttonEast) 펄스 / `Off` = 미사용 |

## 4. 매핑하지 않은 것 (의도적) / Deliberately unbound

| 입력 (input) | 이유 (reason) |
| --- | --- |
| 양손 그립 (both grips) | **D-206 부분 개정**: 건설 모드(`is_turret_mode`) 안에서만 좌/우 그립이 각각 이전/다음 종류, 양쪽 동시가 설치 확정. 그 밖의 모든 순간에는 D-057 그대로 미할당 — 검증기는 `TurretCycle=Off`로 옛 동작을 재현할 수 있다 |
| 오른쪽 시스템 버튼 (right system) | D-002/D-050 런타임 예약 |
| 수동 레벨 업 (manual level up, `T`) | 미매핑 (Q-036, 게임 API 확인 전) |
| 왼손잡이 레이아웃 (left-handed) | 범위 밖 (D-051) |

## 5. 수치 요약 (코드 상수) / Numeric thresholds

| 항목 (item) | 값 (value) | 위치 (source) |
| --- | --- | --- |
| 이동 데드존 (move deadzone) | 0.18 | `HandInput.MoveDeadzone` |
| 트리거 눌림/해제 (trigger press/release) | 0.6 / 0.35 | `HandFirePolicy.PressAt/ReleaseAt` |
| 스틱 능력 진입/해제 (ability latch) | 0.7 / 0.3 (+축 우세) | `StickLatchPolicy` |
| 스냅 턴 (snap turn) | 30°(기본)/45°/끄기 중 선택, 1회/밀기, 해제 0.3 — **D-211: 헤드 프레임에서 시야 회전으로 구현** | `SnapTurnPolicy.Choices`, `SnapTurnDegrees`(0/30/45), `VhvrSettings.exe`, `HandPoseFrame` (BUG-203) |
| 손/조준 합성 프레임 (hand/aim frame) | `Head` = 눈과 같은 프레임 (기본) / `Anchor` = 0.9.4 A/B | `HandPoseFrame`, D-202/BUG-202 |
| 그립 눌림/해제 (grip press/release) | 0.6 / 0.35 | `TurretGripPolicy.PressAt/ReleaseAt` |
| 터렛 종류 한 칸 펄스 (blueprint step pulse) | 0.08 초 | `TurretGripPolicy.StepSeconds` |
| 건설 모드 스틱 스텝 값 (Stick step) | ±0.9 | `HandInput.StepStick` |
| 건설 모드 종류 전환 표면 (cycle surface) | `Stick`(기본) / `Dpad` / `Special` / `Off` | `TurretCycle`, D-206/BUG-204 |
| 튜렛 자동 재발사 (auto-repeat) | **없음** — 한 번 누를 때 한 칸 | `TurretGripPolicy`, D-206 |
| 장전 인식 거리 (reload reach) | 머리에서 0.30 m, 누른 순간만 | `HeadReachPolicy.NearMeters` |
| 인게임 UI 클릭 트리거 (in-game UI click) | 0.6 (해제 0.35) | `HandUi.UiPointer` |
| 메뉴 레이저 클릭 트리거 (menu laser click) | 0.5 | `MenuScreenRig` |
| 메뉴 스크롤 데드존/속도 (menu scroll) | 0.15 / 3 이벤트/프레임 | `[MenuScreen] ScrollSpeed` |
| 자동 연사 재발사 간격 (semi-auto retrigger) | 트리거 유지 중 0.6초 | `[Fire] RetriggerSeconds` |
| 메뉴 패널 거리/시야각 (panel distance/FOV) | 2.0 m / 85° | `[MenuScreen] DistanceMeters/HorizontalFovDeg` |

## 6. 상태 (정직) / Honest status

- 표의 모든 행은 **설치된 코드에 존재**하는 동작이며 오프라인 테스트(HandAim **238/238**, GameplayCamera 38/38, WeaponRuntime 67/67, VhvrExtras 72/72, FlameEndpoint 138/138, ShotScopes 28/28, Settings 13/13)를 통과했습니다.
  Every row exists in the installed code and passed the offline suites.
- 단 **행별 헤드셋 승인 기록은 없습니다.** 양 스틱 클릭 리센터·헤드 프레임 레이저는 `docs\playtests\PLAYTEST-D201-MENU-CENTER-KO.md`,
  **손/무기 방향·리티클·무기 기울기와 스냅 턴은** `docs\playtests\PLAYTEST-D202-HAND-FRAME-KO.md`의 착석 1회로 확인해야 합니다.
- **D-211 스냅 턴(30/45/끄기)**: 30°는 **헤드셋에서 확인됐습니다**(2026-10-22 사용자 보고 — 시야가 30° 돌고 몸은 제자리, 손·무기·리티클이 함께 회전). 45°와 끄기는 같은 코드 경로의 다른 값이지만 직접 착용 확인은 아직 없습니다. 영수증으로는 `run\host.json`의 `snapViewYaw`/`snapTurnsApplied`가 실제로 증가하는지 확인합니다.
- **D-202 변경점 (효력 있음)**: 게임플레이 손/조준 합성 프레임이 게임 카메라(플레이어 yaw) → **눈과 같은 헤드 프레임**으로 바뀌었습니다.
  D-202 이전 빌드에서는 무기와 리티클이 컨트롤러가 가리키는 방향에서 90° 벗어나 보였습니다(BUG-202). 스냅 턴은 D-211에서 시야 회전으로 구현했고(30/45/끄기), 30°는 헤드셋 확인 완료입니다.
  There is **no per-row headset acceptance record** — the recenter chord and head-frame laser need the D-201 script, the hand/weapon
  direction, reticle and weapon roll need the D-202 script; the D-211 snap turn was accepted in the headset at 30 deg on 2026-10-22
  (view turns one step, position stays put, hands/weapon/reticle turn with it). 45 deg and off reuse the same path and are untested
  on hardware; `snapViewYaw` / `snapTurnsApplied` in `run\host.json` are the receipt fields. D-202 moved the gameplay hand/aim composition into the
  head frame (pre-D-202 builds showed the weapon and reticle 90° off the controller, BUG-202).
- 바인딩을 바꾸려면 소스 상수(`HandAim.cs`)를 고치고 `tests\HandAim`·`tests\VhvrExtras`를 다시 돌려야 합니다.
  런타임 재바인딩은 **미구현**(`config\bindings.json`의 `rebinding.status = NOT_IMPLEMENTED`).

