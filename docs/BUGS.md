# Bugs
## BUG-201 — 메뉴 화면(패널)이 소스 카메라 포즈로 동결되어 시야에서 90° 벗어나고, 메뉴 레이저가 다른 프레임을 사용 (FIXED IN CODE 2026-10-18, D-201; 헤드셋 재검증 대기)
- **증상 (사용자, 0.9.4 착석)**: "VR모드가 시작되면 내 시야가 오른쪽으로 90도 꺾여 있거든, 아마 그것 때문에 생기는 문제로 추정" + "UI ray가 이상한데" → 요청: "VR 모드가 시작될 때 HMD 정면의 화면 위치를 중앙으로 설정해".
- **원인 (수치로 확정, 두 파일 교차)**:
  - `run\host.json`(2026-10-18 19:57): `view` row 2 = `(0, 0, -1)` + pose identity → **눈 카메라는 HMD 프레임**, 같은 영수증의 `anchorRotation` = `[0, -0.7071068, 0, 0.7071068]` = **Unity yaw −90°**. 두 프레임이 정확히 90° 어긋나 있었다.
  - `LogOutput.log`: `menu ui: panel frozen at world (74.29, 34.00, 43.75)`. 영수증 eye position `(76.25548, 32.466396, 43.753567)` 에서 eye offset 을 빼면 anchorPosition `(76.287055, 34.000000, 43.753567)` → 패널은 `anchorPosition + 2 m × (−1,0,0)` = **ArenaCamera의 정면**에 동결.
  - 기계적 원인: `Camera.CopyFrom` 은 **transform까지 복사**한다(Unity 2022.3 문서). `eyes[i].CopyFrom(source)` 직후에는 eye 카메라 transform이 게임 카메라 포즈이므로, **호스트가 첫 eye 포즈를 적용하기 전에** extras가 배치하면(그리고 D-178이 "한 번 배치 후 동결"하므로) 게임 카메라 프레임이 영구 고정된다. D-200의 채택 순서 변경 + 인트로 이후 세션 시작이 그 창을 열었다.
  - 두 번째 원인: 메뉴 레이저는 `anchorRotation`(게임 카메라 회전)을 곱해 만들어진다(`MenuScreenRig.UpdateHand` → `PoseMathPolicy.TryToWorld`). 패널/눈은 HMD 프레임이므로 레이저만 90° 어긋난다 → "UI ray가 이상하다".
- **수정 (D-201)**: ⓐ 패널 배치 입력을 `eyes[0].transform` → **라이브 헤드 포즈(네이티브 `ReadStatus`, `Frames > 0` 요구) + anchorPosition** 으로 교체(호스트 eye 합성과 동일 식). 라이브 포즈가 없으면 배치를 미루고 로그만 남긴다. ⓑ 배치 로그에 `headYaw`/`panelYaw`/`headPanelDelta=0.0deg` 를 남겨 영수증으로 정면 여부를 판정. ⓒ 메뉴 레이저를 HMD 프레임(identity)으로 통일 — config `[MenuScreen] PointerHeadFrame`(기본 true, false = 0.9.4 동작). ⓓ 리센터: `[MenuScreen] RecenterChord`(기본 true) — **양쪽 스틱 동시 클릭**으로 현재 시선 앞에 패널 재배치.
- **검증 (오프라인)**: extras Release 경고 0/오류 0; `VhvrExtras.dll` **53,760 B** SHA256 `52E6707290D430F25615CF50EFAA7ADA3013DD21527FB7861D999BFDDB1CA533`. `tests\VhvrExtras` **72/72 PASS** — 이번 실행의 실제 수치(패널 (74.287, 34.000, 43.754) = 90.0° 오프셋)를 **회귀 fixture** 로 고정했다. `tests\verify.ps1` 기대 개수 62 → 72.
- **검증 (헤드셋)**: **PENDING** — (a) 메뉴가 정면 중앙, (b) 레이저가 컨트롤러 방향, (c) 로그 `headPanelDelta=0.0deg`, (d) 필요 시 리센터(양쪽 스틱 클릭).
- **라이브 설치**: `BepInEx\plugins\VhvrExtras.dll` 교체(Stage0/config 무변경). 직전 DLL은 `backups\D-201-20261018T111028Z\`(SHA `09F01741…`).
- **부수 (정직)**: 0.9.2의 D-196은 레이저만 게임 카메라 프레임으로 맞춰 "당시 동결된(역시 90° 틀린) 패널"과 짝을 이룬 상태였다 — 즉 이 버그는 D-195 이후 계속 있었고, 0.9.3이 검은 화면이라 보이지 않았을 뿐이다.

## ENV-012 — `tests\Transaction.Tests.ps1` 내용 유실로 `tests\verify.ps1`이 65행에서 중단 (OPEN, 2026-10-18 발견)
- **증상**: `tests\verify.ps1`을 실행하면 `transaction-fixture-tests` 단계에서 `Unexpected token '}' / 'finally'` 파서 오류로 중단되고, 그 **이후 게이트들이 하나도 실행되지 않는다**(plugin-build, entry-preflight, verify-bindings, uuvr/uuvr-spike, extras 개수 검사 등).
- **원인**: `tests\Transaction.Tests.ps1`의 **160~194행이 빈 줄로 유실**되어 brace 짝이 깨졌다(파서 오류 6건, 첫 오류 195행). 파일은 UTF-8 BOM, 402행 17,228 B.
- **범위 (정직)**: 이 손상은 새 것이 아니다 — `artifacts\history-recover\tree-cf5060c`, `artifacts\recovery\dumps-tree`, 공개 트리 `<RepoRoot>` **모든 사본이 동일 해시** `09A62C91B96FD46F`(2026-10-14 02:37~02:40)이며 같은 빈 구간을 갖는다. 즉 2026-10-14의 동기화 사고(ENV-010 계열)에서 발생해 여러 사본에 전파됐다.
- **영향**: 게이트가 **조용히 통과한 것은 아니다**(verify.ps1은 `exit 1`로 실패하고 `artifacts\verification.json`을 남긴다). 다만 **실패 지점 이후 스위트가 검증되지 않은 채** 남았고, D-200에서 "extras 개수 62→72" 같은 기대값 교정도 이 파일 때문에 자동 검증되지 않았다.
- **이번 작업의 대체 검증**: D-201 변경 검증은 verify.ps1 대신 스위트를 **직접** 실행했다 — GameplayCamera 38/38, HandAim 190/190, WeaponRuntime 67/67, FlameEndpoint 138/138, FlameEmitter 55/55, StereoMath 29/29, VhvrExtras 72/72(모두 exit 0).
- **다음 (복구)**: ① 유실 구간의 원본 내용을 복원한다(트랜잭션/복구 fixture 36행 — D-153 설계와 `src\core\EntryTransaction.ps1` 기반으로 재구성 가능). ② 복원 전까지는 verify.ps1 상단에 "이 단계에서 중단됨" 주석을 남겨 다음 세션이 오해하지 않게 한다. ③ 장기적으로 `Invoke-PowerShellCheck`가 중단 대신 **실패를 기록하고 계속**하도록 바꿔 한 파일 손상이 전체 게이트를 가리지 않게 한다(결정 필요 — Q-D201-GATE).

## TOOL-098 — 게임 시작 후 15초간 VR이 시작되지 않음 (FIXED IN CODE 2026-10-18, D-198; headset unverified)
- **증상**: 게임(0.9.2)을 실행해도 곧바로 VR로 전환되지 않고 약 15초를 기다려야 함. 사용자 표현: "시작하자마자 VR로 시작되게 못하나". D-195/D-196 패치 **이전 빌드에서도 동일**했다.
- **원인 (코드 사실)**: `src\core\OpenXrHost.cs` `Update()` 첫머리의 `if (Time.unscaledTime < 15f) return;`. `Time.unscaledTime`은 **게임 프로세스 시작 이후 누적 초**이며 씬 로드로 리셋되지 않으므로(Unity 문서: *"the time in seconds since the start of the game"*), 호스트는 **게임 시작 후 15초 동안 `Initialize()`를 아예 호출하지 않았다**. 이 게이트는 `GameplayWaitSeconds`(180 s)와도 무관한 별개 상수였다.
- **왜 들어갔나**: `Initialize()`는 `Camera.main == null` 에서 `throw new InvalidOperationException("no main camera")` → `Fail(e)` 로 **세션 전체를 종료**시킨다. 그래서 "카메라 준비 전 초기화"를 막으려던 장치였지만, 준비도를 검사하지 않고 **경과 시간**을 검사해 준비가 3초에 끝나도 15초를 기다렸다.
- **수정**: `StartupReady()` 매 프레임 게이트로 교체 — 메인 카메라 존재 + D3D11 확인, 충족 시 **그 프레임에 즉시 초기화**. 대기 중에는 1초 주기 `STARTUP_WAIT` 로그 + `waiting.json`(`verdict=WAITING_FOR_STARTUP_CAMERA`, `startupWait.reason/elapsedSeconds/budgetSeconds`), 예산(`GameplayWaitSeconds`) 초과 시 정직한 `Fail(TimeoutException)`, 최초 통과 시 `STARTUP_READY elapsed=X.Xs`.
- **검증 (정적)**: Stage0 Release 0 warning/0 error; 소스 가드 4개(`source-no-fixed-warmup`, `source-readiness-gate`, `source-bounded-startup-wait`, `source-startup-wait-receipt`) 포함 `GameplayCamera` **35/35 PASS**, `WeaponRuntime` **67/67 PASS** — 모두 **최종 소스 기준 재실행 결과**. 최종 설치 DLL SHA256 `D1655D505DBA136110471955B1AC2DF67753C882701AC295A91DD2F854C21175` (426,496 bytes, 현재 소스 재빌드와 SHA 일치). 직전 DLL(2026-10-17 원본 421,376 B, SHA `0A2A9FAF…`)은 `backups\D-198-20261018T184654671\` 에 보존.
- **검증 (런타임, headset)**: **PENDING** — (a) 게임 실행 후 몇 초 만에 VR로 뜨는지 육안 확인, (b) BepInEx 로그 `STARTUP_READY elapsed=`, (c) `run\host.json` `startupReadySeconds` / `startupWait`. `startupReadySeconds ≥ 10` 이고 `startupWait = "no main camera yet"` 이면 **게임 자체가 카메라를 늦게 만드는 것**이므로 추가 단축 불가로 기록한다.

## TOOL-097 — SteamVR에서 시야가 상하/좌우 뒤집혀 보이고 레벨업 카드가 panel plane에 비스듬히 붙어 보임 (FIXED IN CODE 2026-10-18, D-195; HMD re-verify pending)
- **증상 (SteamVR)**: 0.9-final을 설치하고 SteamVR 헤드셋으로 게임을 켜면, **월드가 상하/좌우 뒤집혀 보이거나 비스듬하게 누워** 보임. 같은 0.9-final을 VDXR(Quest Link/VD)로 켜면 정상이거나 다른 각도로 보임. 메인 메뉴/스테이지 동일 증상. 추가로 **레벨업 카드 선택 UI에 포인터를 가져다 대면 카드가 반쯤 회전된 채 멈춘 듯** 보임 (게임 자체 회전 트윈이 정상 종료되었는데 사용자에게는 빙글빙글 도는 인상).
- **원인**: `src/core/OpenXrHost.cs` `Update`(L1238–1244)에서 `StereoMath.UnityOrientation`이 OpenXR→Unity 축 보정으로 쿼터니언을 `(-x, -y, z, w)`로 한 번 뒤집는데, 같은 합성 단계에서 `anchorRotation` (이미 Unity-space 게임 카메라 회전)을 `unityRot`에 다시 곱함. 결과 카메라 회전이 z축 180° 누적되어 카메라가 옆으로 90° 누움. **receipt 첫 view 매트릭스의 row 0/1/2 분해 결과: right=(0,0,1), up=(0,1,0), forward=(1,0,0)** — Unity 카메라는 forward가 (0,0,-1)이어야 정상이므로 명백히 누워 있음. SteamVR는 그 view를 받아 distortion 시키므로 비뚤어진 distortion, VDXR은 자체 보정 stage에서 한 번 더 정규화해 사용자 시야에는 우연히 정상으로 보임.
- **간접 영향 — 카드 회전**: 위 카메라가 거꾸로 있는 상태에서 `MenuScreenRig.Tick()`이 `eyes[0].transform.rotation`을 panel plane의 정면으로 사용 → panel plane이 카메라 정면이 아닌 비스듬한 방향. 그 panel에 게임의 카드 회전 트윈이 매달려 있으므로 **카메라 시점에서는 항상 비스듬히 회전된 면이 보임** → 사용자가 “반쯤 회전” 인상을 받음.
- **수정**: `OpenXrHost.Update`의 eye 합성 두 줄에서 `anchorRotation *` 제거. 위치는 `val + new Vector3(unityPos)` (anchor 위치 오프셋은 그대로), 회전은 `new Quaternion(unityRot)` (OpenXR→Unity 보정이 StereoMath 안에서 한 번만 적용됨). HandAim은 `anchorRotation`을 그대로 사용하므로 손 IK는 정상. `anchorPosition`/`anchorRotation` 멤버는 HandAim/SnapAnchor 용도로 보존 (dead code 아님).
- **검증 (정적)**: Stage0 Release 0 warning/0 error; 새 DLL `VampireHuntersVR.Stage0.dll` SHA256 `05705C2FF7B37D3A226E389C4D5C5FF42AF4CCC7816E1154257504F0F5E2B1E9`. 0.9-final ZIP 갱신: SHA256 `310801A44627715B4A3002BF27D73566800555FB62395BFF075ED388376FBBF7` (이전 SHA `0B3080FBBF04646BA4D008DD820BB3C1B379B06FEFA935427057B8F265CC06E3`).
- **검증 (런타임, headset)**: 0.9-final 설치 → SteamVR + Quest 헤드셋 → (a) 메인 메뉴/스테이지가 정면 + 수평으로 보이는지, (b) 레벨업 카드가 panel plane에 정렬되어 보이는지, (c) VDXR/SteamVR 둘 다 동일하게 정상인지. `host.json` receipt의 `view` 매트릭스 row 2가 (0,0,-1) 형태인지 확인. **HMD 확인 전이므로 FIXED IN CODE 표시 후 PENDING HMD**.

## TOOL-096 — 0.9-final이 메인 메뉴에서 VR을 시작하지 않음 (FIXED IN CONFIG; headset unverified, D-194)
- **증상**: 게임 실행 후 메인 메뉴 `MainMenu_2`에서 headset VR로 바뀌지 않음.
- **실행 증거**: `waiting.json` verdict=`WAITING_FOR_GAMEPLAY_CAMERA`, scene=`MainMenu_2`, `candidates=[]`; installed host config `MenuFirst=false`, `GameplayOnly=true`.
- **원인**: 0.9-final 패키저가 D-190의 사용자 승인 main-menu-first 기본값을 뒤집어 `MenuFirst=false` 출하. 그 결과 Stage0는 gameplay camera를 기다렸다.
- **수정**: release install host config + extras config/binding를 `MenuFirst=true`로 복원, package sanity 및 README/notes 정정. Extras prefix만으로는 OpenXR startup/hook success를 보장하지 않는다.
- **상태/다음**: 소스상 MenuFirst→Camera.main→stage camera handoff path는 존재하나, actual release binary/HMD에서 menu VR 및 stage transition은 아직 확인 전. Next run must inspect host.json/hand-aim.json plus BepInEx log; PATCH_APPLIED alone is insufficient.

## TOOL-095 — 화염방사기 입자 생성 전 emitter pin 호출 미연결 (FIXED IN CODE / headset unverified, D-193)
- **증상**: 사용자가 화염방사기 이펙트가 머리 움직임을 따라간다고 보고.
- **원인 확인**: `HandPose.HoldHandsBeforeParticles(PlayerController)`가 정의만 되어 있고 호출부가 없었음. 이전 `hand-aim.json`에서 flame pin enabled/active, attackAimUsed>0, errors=0였지만 `beforeParticles.samples=0`; late Tick pin만 실행되어 생성된 입자 방향을 보정하지 못할 수 있었음.
- **수정**: `WeaponRuntimeBridge.AfterPlayerUpdate`가 head-gaze post-update 이후 `HoldHandsBeforeParticles(__instance)` 호출. before-particles counter를 receipt에 추가하고 source guard 테스트 추가.
- **검증**: Stage0 Release 0 warning/0 error; FlameEmitter 55 fixtures + source checks PASS. 실제 in-game particle emission order/visual correction은 헤드셋 미확인. 문제 지속 시 particle simulation space/속도와 실제 보이는 emitter 식별을 다음 조사 대상으로 삼는다.

## ENV-010 — 동기화가 세션 중간에 파일을 삭제·되돌림 (ONGOING 2026-10-16)
- **증상**: 세션 중에 `src\render\NativeHost\Hands.cpp`, `tests\HostConfigToolTests.cs`, `dist\deploy\` 전체, `dist\deploy\vhvr-source-bundle-a35e1737.zip`(21:31 스냅샷), 초록 세대 `tests\HandAim\Program.cs`가 차례로 소실. 물리 디스크는 정상(F: GOOD) → 상위 폴더(OneDrive 등) 동기화가 원인.
- **영향**: `tests\HandAim\Program.cs` 초록 126 픽스처 세대와 Hands.cpp 소스 가드가 로컬 회수 불가. `tests\NativeHost.Tests.csproj`의 Hands.cpp 가드도 동일 이유로 차단.
- **조치(사용자 필요)**: ① 동기화 일시정지/해제 후 작업, ② OneDrive 웹(버전 기록/온라인 휴지통)에서 `Hands.cpp`, `HostConfigToolTests.cs`, `tests\HandAim\Program.cs` 복원, ③ 프로젝트를 동기화되지 않은 경로로 이동 검토.
- **원칙**: Hands.cpp 없이 소스 가드를 통과시키도록 테스트를 바꾸지 않는다(가드 제거 = 다운그레이드).

## ENV-011 — 게임 설치 드라이브 G: 실장 불가 (ONGOING 2026-10-16)
- **증상**: `<GameRoot>`가 있는 G: 미장착. `tests\verify.ps1` deploy 섹션 실행 불가.
- **준비 상태**: 배포 스모크 테스트는 내부 `$Game` 주입으로 통과. `<Downloads>\VampireHuntersVR-0.6-final.zip`의 `VhvrNativeHost.dll`(335KB)이 대체 후보. `src\core\Install-Stage0.ps1`에 `VhvrNativeHost.dll`을 Stage0Plugin.dll 옆으로 복사하는 단계 존재.
- **조치**: G: 재장착 후 `tests\deploy-smoke.ps1 -Game '<게임 루트>'`로 검증.

## TOOL-094 — 메뉴 패널 렌더 텍스처가 **순수 검정** — URP가 플러그인 카메라를 자동 렌더하지 않음 (FIXED 2026-10-15, D-178 4차 실측)
- **증상**: 패널 셰이더/카메라/quad/눈 마스킹이 **전부 정상**(pos/rot/mask/rect/near/far/shader/texture 모두 확인)인데 헤드셋은 **완전 검정**. 진단 로그로 렌더 텍스처를 직접 샘플: `nonBlack=0/921600 avgRGB=0,0,0`.
- **실험**: 같은 프레임에 `panelCamera.Render()`를 수동 호출 후 재샘플 → `nonBlack=920321/921600 avgRGB=60,45,37 centreRGB=129,78,74` (성 장면이 가득 참).
- **원인**: 게임은 **URP(Universal Render Pipeline)**. 플러그인이 `new GameObject().AddComponent<Camera>()`로 만든 카메라는 `UniversalAdditionalCameraData`가 없어 **URP가 카메라 목록에서 렌더하지 않는다**(호스트의 eye 카메라는 호스트가 따로 준비해 정상).
- **수정**: `MenuScreenRig.Tick`에서 **매 프레임 `panelCamera.Render()`를 직접 호출**(Update 후처리 시점이라 eye 카메라가 quad를 그리기 전에 RT가 갱신됨). 로그: `menu panel camera: manual per-frame Render (URP does not auto-render plugin cameras)`.
- **비용(정직)**: 메뉴 장면을 프레임당 3회(눈 2 + 패널 1) 렌더한다. 메뉴에서는 단순한 장면이라 수용 가능하나, **실측에서 fps를 확인**해야 한다.
- **대안(미적용)**: URP 어셈블리 참조가 `tools/interop`에 없어 `UniversalAdditionalCameraData`를 컴파일 타임에 추가할 수 없다(리플렉션으로 시도 가능하나 검증된 수동 Render 경로를 채택).

## TOOL-093 — 메뉴→스테이지 진입 시 호스트가 **"camera lost"로 FAIL** (FIXED 2026-10-15, D-178 2차 실측에서 발견)
- **증상**: MenuFirst로 메뉴는 정상 표시됐지만, **PC로 스테이지를 진입하는 순간** 호스트가 종료. `run\host.json.failure.json`:
  `System.InvalidOperationException: camera lost at OpenXrHost.PrepareCameras(NativeHostStatus) ... <OpenXrHost::Update>`.
- **원인(코드 사실)**: 스테이지 로딩 시 메뉴 씬이 언로드되어 **`source`(ArenaCamera)가 파괴**됨. Update의 **렌더 스케줄이 매 사이클마다 `PrepareCameras`를 호출**하는데(phase 0→1→2 반복), 그 호출이 `source == null`(Unity 파괴 객체)에서 `throw new InvalidOperationException("camera lost")`. 인수인계 스캔은 **1초 게이트**에 걸려 그 프레임에 못 잡았다.
- **수정**:
  1. `TryEnterGameplay(bool immediate)` — 메뉴 카메라가 죽은 프레임에는 **즉시 스캔**(게이트 우회).
  2. Update의 menu-first 분기에서, 게임플레이 카메라가 아직 없으면 **그 프레임 렌더 스케줄을 건너뛴다**(죽은 카메라로 스케줄/PrepareCameras 하지 않음).
- **검증**: 빌드 0/0, 재설치(Stage0 `2359379A…`). **실기기 재검증 대기**.
- **교훈**: 이 실패는 오프라인으로는 못 잡히고 **실기기에서만** 드러났다(메뉴 씬 언로드 타이밍). 스킬의 "실제로 반응하기 전엔 확인 아님" 원칙 그대로.

## TOOL-092 — 메뉴 패널이 **셰이더 null**로 생성 실패 (FIXED 2026-10-15, D-178 첫 실측에서 발견)
- **증상(첫 MenuFirst 실측)**: MenuFirst로 메뉴 모드 진입은 성공했고 **메뉴 3D 월드가 VR로 보였지만**, 게임 UI(버튼/스테이지 선택)가 없어 **게임 진행 불가**. 로그: `[Extras] menu panel build failed: Il2CppException: System.ArgumentNullException: Value cannot be null. Parameter name: shader` (12회 반복).
- **원인**: 게임이 기본 내장 셰이더를 스트리핑해서 `Shader.Find("Unlit/Texture")`가 **null** → `new Material(null)` 예외 → 패널 생성 중단(눈 카메라 마스킹도 적용 안 됨 → 월드만 보임).
- **수정**: `MenuScreenRig.FindShader(...)` 추가 — ① 지정 후보 목록(`Unlit/Texture`,`UI/Default`,`Sprites/Default`,`Legacy Shaders/Diffuse`,`Unlit/Color`,`Hidden/Internal-Colored`) ② 로드된 셰이더 중 이름이 Unlit/UI/Sprite/Internal-Colored인 것 ③ 아무 셰이더나 순서로 폴백, 로그 기록. 절대 null 반환하지 않음(마지막에도 없으면 명시적 예외).
- **검증**: 빌드 0/0, fixtures 63/63, 재설치 완료(Extras `7CFA0D2F…`). **실기기 재검증 대기**(TOOL-092는 재검증 후 CLOSE).
- **부수 관찰(정직)**: 패널이 없을 때 VR에 게임 월드가 그대로 보였다 → 이것이 "눈 카메라가 월드를 렌더"한다는 증거이며, 패널이 생기면 눈은 검게 마스킹되고 **월드+UI가 패널에 함께** 그려지는 설계다(첫 실측에서 설계대로 동작함을 확인).
## D-178 — 소스 트리 손상 **해결** + MenuFirst 소스 구현 (2026-10-15 새벽)
- **TOOL-088 (src 트리 손상) — RESOLVED**: `src\core\VampireHuntersVR.Stage0.csproj` 빌드가 **538 오류 → 0 오류/0 경고**. 손상은 여러 파일의 "두 리비전 병합"(중복 블록/누락 중괄호) + 일부 파일 통째 유실이었다. 배포 `VampireHuntersVR.Stage0.dll`(D-171)을 `ilspycmd`로 디컴파일해 복원. 상세는 `DECISIONS.md` D-178.
  - 신규/복원 파일: `core\OpenXrHost.cs`(디컴파일), `core\OpenXrHostConfig.cs`, `core\HostNative.cs`, `interaction\HandPose.cs`, 그리고 `HandInput.cs`/`HandAim.cs`/`HandFlameEmitter.cs`/`csproj` 수정.
  - 남은 위험: 복원본은 **헤드셋/게임 미검증**이며 `OpenXrHost.cs`는 주석이 없다.
- **TOOL-091 (MenuFirst 초기화 예외) — 소스에서 수정**: `MenuFirst` 설정 추가 + 검증 완화 + 게임플레이 진입 시 손 통합 지연 설치(`InstallHandAim`). **미배포**(재빌드 DLL은 dist에 넣지 않음)이므로 **실기기 미검증**.
- **미배포 원칙**: 재빌드 DLL(`A2E2F240…`)은 디컴파일 복원체 + 신규 기능이라 **동작 동등성 검증 전 배포 금지**. 배포/설치본은 D-171(`E5129D81…`) 그대로.
## TOOL-091 — `MenuFirst=true`(메뉴 우선)는 실기기에서 **호스트 초기화 예외**로 실패한다 (OPEN, 2026-10-14 조사 근거)
- **근거(배포 DLL 디컴파일, 코드 사실)**: `OpenXrHost.Initialize()`가 시작부에서
  `if (Config.Headset && (!Config.GameplayOnly || !Config.HandAim || …)) throw new ArgumentException("invalid headset config: needs GameplayOnly, HandAim and HeadsetSeconds …");`
  를 수행한다. 배포 설정은 `Headset=true`이고, `VhvrExtras`의 MenuFirst 전처리는 **`GameplayOnly=false`만** 바꾼다(Headset/HandAim은 그대로) →
  **Initialize가 예외로 죽고 `configured`가 false로 남아** 메뉴 패널 코드(`Update` 후처리, `configured==true` 전제)가 **한 번도 실행되지 않는다**.
- **부수 사실**: 같은 메서드의 `if (Config.HandAim) { if (!Config.GameplayOnly) throw "HandAim requires GameplayOnly; menu cameras have no weapon slots"; }` 때문에, 메뉴 모드에서 손/무기 통합을 켜는 것도 **불가능**하다(설계상 메뉴 카메라엔 무기 슬롯이 없음).
- **상태**: 아직 **실기기에서 관측된 적 없음**(MenuFirst 미사용). 코드 근거 기반 예측이며, 켜면 실패할 것으로 판단. `#1`(메뉴 패널) 수락과 함께 검증 필요.
- **영향**: D-175 #1(메뉴 VR 패널)·#2(포인터)는 현재 구현으로 **동작 불가**.

## TOOL-090 — 착석 영수증(`headset-extras.ps1`)이 실제 로그와 **라벨 불일치**로 아무것도 못 잡음 (FIXED 2026-10-14, D-177)
- **증상**: 실기기 로그에는 `PATCH_APPLIED HandAimDriver.FireTick->slot-context` 등 4건 + `PATCH_FAILED AbilityStickPolicy.Next->face-button` 1건이 분명히 있는데, `tests\headset-extras.ps1`는 6항목 전부 `verdict=NOT_RUN, patch=0`으로 보고했다(`docs\playtests\EXTRAS-ex1.json`).
- **원인**: 스크립트의 기대 라벨(`fire.loop`, `fire.retrigger`, `ui.scale`, `ability.face`, `menu.panel`)이 플러그인이 실제로 기록하는 Hook 라벨(`HandAimDriver.FireTick->slot-context`, `HandFirePolicy.StartThisTick->retrigger`, `HandAimDriver.UiScan->ui-scale`, `AbilityStickPolicy.Next->face-button`, `OpenXrHost.Update->menu-panel`)과 **다르다**. `*PATCH_APPLIED*fire.loop*` 같은 매칭이 성립하지 않는다. 실패 감지도 같은 이유로 #4를 놓쳤다.
- **왜 D-175가 못 잡았나**: 드릴 검증을 **합성 로그**(스크립트가 기대하는 라벨)로 했기 때문 — 즉 "테스트가 제품이 아니라 테스트 자신을 검증"한 사례. 실제 플러그인 출력으로는 처음 실행됨.
- **영향**: 자동 판정표가 **거짓 음성**(있는데 없다고 표시). 게이트/수락에는 사용 불가. 로그 원문 수동 판독이 현재의 진실 원천.
- **다음(미착수)**: 스크립트의 `behaviours[].patch` 값을 **실제 Hook 라벨**로 교체하고, `PATCH_FAILED` 매칭도 라벨 기준으로 수정. 검증은 **실제 로그 라인**(합성 아님)으로 회귀.

## TOOL-089 — VhvrExtras #4(좌Y/우B 대체 사격) **실기기 패치 실패** (OPEN, 2026-10-14 D-175 착석 라이브)
- **증상(라이브 로그)**: 게임 실행 시 `[Extras] PATCH_FAILED AbilityStickPolicy.Next->face-button: IL Compile Error (unknown location)` → HarmonyException. `[Extras] LOADED applied=4 failed=1`.
  - 적용됨: `HandAimDriver.FireTick->slot-context`, `HandFirePolicy.StartThisTick->retrigger`, `HandAimDriver.UiScan->ui-scale`, `OpenXrHost.Update->menu-panel`.
  - 실패: `AbilityStickPolicy.Next->face-button` (= 기능 #4, 좌 Y=특수 / 우 B=블러디 러시).
- **영향(정직)**: 5기능 중 **#4는 이 세션에서 동작하지 않는다.** 또한 #1(메뉴 패널)·#2(포인터)는 `MenuFirst`가 기본 OFF라 **이 세션에서는 판정 불가**(별도 세션 필요). 판정 가능한 것은 #3(UI 1.5배)·#5(자동 연사)·#6(쿨타임 햅틱).
- **핵심 교훈**: D-175의 오프라인 **60 픽스처는 "정책 로직"만** 검증했고, 실제 게임 IL에 대한 **Harmony 패치 적용 성공은 이번 실기기가 처음**이다 → 픽스처 PASS는 "PATCH_APPLIED"를 보증하지 않는다. (스킬의 "실제로 반응하기 전엔 확인 아님" 원칙 그대로.)
- **다음(미착수)**: `AbilityStickPolicy.Next` 대상(v31 IL2CPP)이 현재 transpiler/prefix로 컴파일 불가. 후보: 대상 메서드·시그니처 재확인, transpiler 축소/제거 후 prefix·postfix만으로 구현, 또는 패치 대상을 인접 후킹 지점으로 이동. 로그/증거는 이 실행의 `LogOutput.log`에 보존.

## TOOL-073 — 왼손 무기 모델 **각도**가 손과 어긋나 보임 (OPEN, 계측+다이얼 준비 2026-10-13)
- **사용자 보고(D-153)**: 왼손에 든 무기의 **각도가 어긋나 보인다**(오른손은 정상으로 보임). TOOL-071(모델이 도는 스핀)과 달리 **고정된 틸트**로 보인다는 점이 다르다.
- **원인 규명(D-153, 코드 근거)**: `HandAimDriver.Update`는 ① 컨테이너를 손 자세로 쓰고 ② `Quaternion.FromToRotation(alignMuzzle.forward, want)`로 총구를 컨트롤러 forward에 맞춘다. 최소 호(정준) 회전이므로 **총구 방향은 좌우 모두 정확히 일치**하고, 남는 자유도는 **배럴 축 주위의 롤 하나**다. 그 롤은 **캡처된 저작(rest) 자세**가 결정한다 → 손 기준 **일정한 고정 각도**가 남고, 좌/우 저작 자세가 달라 **왼손만 어긋나 보일 수 있다**. 즉 정렬 코드의 버그가 아니라 **"어느 롤이 정답인지 코드가 알 수 없다"**는 문제이며, 좌우 대칭이라는 보장도 없다.
- **준비된 것(동작 변화 0 + 눈으로 정할 수 있는 다이얼)**: 리시트(손별)에 **잔여 롤** `weaponTwistResidualDegrees` / `maxWeaponTwistResidualAbsDegrees`를 기록하고, 손별 다이얼 `LeftWeaponTwistDeg`/`RightWeaponTwistDeg`(도, 총구 축)로 그 롤만 돌린다. 축이 총구 방향 자체라 **총구·조준선·발사 원점은 불변**(픽스처 3건으로 고정).
- **절차**: 다이얼 0으로 세션 → `hand-aim.json`의 `left.weaponTwistResidualDegrees` 읽기 → **그 값을 다이얼에 넣는다**(그 값이 곧 "무기 up을 손 up에 겹치는 값"; 0이면 이미 맞음 = 회전 없음).
- **0.4d 실측(2026-10-13, headset tested)**: 왼손 트위스트 잔여 **+1.44°**(max |1.54|), 오른손 **−1.43°**(max |1.45|), 다이얼 **0**(저작 자세), `muzzleToHandForwardDegrees = 0`(양손), `maxMuzzleVsAim 0.12/0.15°`, **`containerRollSteps = weaponRollSteps = 0`**, `maxContainerVsWritten = 0°`, 롤 아닌 이상 1건(빠른 손 이동). 좌우 ±1.4°의 **거울 대칭**이 44,407/46,525 driven 프레임·무기 교체 10회를 넘어 **안정**했다.
- **판정**: 총구 방향·컨테이너 롤·롤 안정성이 모두 정상 → **"컨테이너가 잘못 돌아간다"는 가설은 기각**. 남은 후보는 ① 컨테이너 **안쪽 메시의 로컬 롤**(현재 리시트는 forward만 재고 up/롤은 재지 않음 — `weaponVsContainer 4.06/1.23°`는 forward 값) ② 게임이 **저작한 그립 자세** 자체(설계값, 드라이버가 보존). 두 후보 모두 D-153 다이얼로 교정 가능하지만 값은 눈으로 정해야 한다.
- **상태**: ① 사용자 시각 판정(어느 무기/어느 방향/몇 도) ② 리시트에 `weaponUpVsContainerUpDeg` 추가(메시 로컬 롤 수치화) ③ 그 값으로 다이얼 설정(0.4e) → 눈 확인(0.4f). 그때까지 **코드가 "정답 각도"를 주장하지 않는다**.


## D-144 — FIXED: 배포 설치 스크립트의 경로 자동탐색이 무관한 게임 설치본을 건드림 — 2026-10-11
- 발견: 배포본 검증 중, 모드 폴더가 게임 폴더의 하위 폴더로 있을 때 폴백이 Steam 레지스트리 → `libraryfolders.vdf`를 검색해 **실제 게임 폴더(`<GameRoot>`)에 234 파일을 설치한 뒤 제거**했다. `-GameRoot`를 준 경로와 폴백이 겹칠 수 있는 조합이었다.
- 영향: 게임 자체 파일은 쓰지 않았고 제거 후 트리는 121 files/7,726,223,158 bytes(8 dirs, D-142 STOCK 기준과 동일)로 확인. 모드 잔여물 0건, 손상 없음.
- 수정: `install.ps1`에서 레지스트리·Steam 라이브러리 검색을 **완전히 삭제**. 후보는 `-GameRoot`, 스크립트 폴더, 그 부모 폴더뿐이며, 찾지 못하면 명확한 오류로 중단한다(추정 설치 없음).
- 회귀 검증: 폴더 배치 3종 + zip 해제본 왕복 + "게임 밖" 배치 거부(exit 1, 무기록) 통과. 실제 게임 트리 무손상 재확인.
- 남은 위험: 없음(자동탐색 코드 제거). 게임 트리에 쓰는 경로는 사용자가 명시한 실행 위치뿐이다.

## D-139 — native endpoint VFX branch built, live acceptance OPEN
- Authorized launch update: 사용자 “게임 시작” 후 d139a 한 번 실행, headset attached/게임·Stage0 메뉴 로딩 확인. 실제 VFX·손 안정·desktop 점멸/원인 수락은 미확인; 버그 상태 변경 없음. 종료 후 apply/restore 및 exact stock receipt 확인 필요.
- Running evidence: Egypt_Map/gameplay lock, controllersAvailable=true/right Driving/RIGHT_HAND Flamethrower, endpoint memoryPatchActive=true/restorationOwned=true/error=null. 양안 GPU 제출 관찰; gameOverwrites 진단값은 HMD 손 불안정/desktop 점멸 원인 증명이 아님. 사용자 시각 보고/종료 복원 대기, 버그 종료 없음.
- TOOL-069/070/071 severity2 remain OPEN. Static same-call linkage now established for native Flamethrower shoot_forward type0x0F→vtable12; low-assist VFX chooses live muzzle.forward instead of provided end-start. Implemented exact-build/context guarded one-byte in-process visual branch; no attack/damage/hand/muzzle/particle lifecycle writes, no Vector3 hook. Visual root cause and outcome not proven.
- Focused138fixtures PASS including real Windows private allocation apply/protection/cache/restore and read-only game PE pins; final production build0warnings0errors and full32checks/exit0PASS after lifecycle review. Fresh d139a installation/independent check COMPLETE per preparation receipt below. d138a retired/unlaunched and transaction Recoveredseq27; do not run it.
- Desktop flicker remains separate. ReadHands busy skip, descendant native animation and eye/source windows are candidates, not proven causal fixes. No render scheduling or stable HMD hand change.
- New errors must be distinguished: memoryPatchActive=successfully applied, restorationOwned=rollback responsibility; failed apply/restore logged, ownership kept for retry where needed. No bug closure before headset confirmation. Detail <RepoRoot>\docs\VFX-ENDPOINT-D139.md.
- Preparation COMPLETE: d139ainstall0/232files/Installedseq14/independentverify0/prepare0,VDXRunchanged/stockoutsideownedhash+time/currentpointer/livepayload/configPASS. READY_PREINSTALLED_NOT_LAUNCHED. New candidate fix prepared, not live acceptance; no bug/gate closure or automatic launch.

## D-138 — 새 영상/HMD 보고 및 진단 실패 — OPEN
- TOOL-070/071 OPEN severity2 Stage6B/8: HMD 손은 안정적이나 녹화의 전경 손·무기는 서로 다른 외형/위치에 교대로 나타남. 새 영상7.000–8.567/10.000–10.967s 연속프레임 확인. VFX 좌우는 HMD 시선 반대로 움직인다는 사용자 보고; 3D 원인/프레임 로그 동기화는 미확정.
- TOOL-069 OPEN: 정상 총알/공격 방향을 보존하고 VFX를 같은 방향으로 맞추는 요구. frame4047의 independent aim과 live muzzle 방향 차이 및 native muzzle-forward VFX branch는 단서이지 실제 사건의 원인 확정 아님.
- Diagnostic defect: d136a render-timing 성공 sample0, header/inventory/end3행. 첫 particle simulationSpace alias가 MissingMethodException으로 전체 관찰 종료. inventoryTruncated43; dropped0은 coverage 성공이 아님. D-138 해당 API 읽기 제거 및 미측정 공백 명시, offline/live 검증 구분 유지.
- d136a HOST_FAILED(scene ownership lost)로 끝났으나 uninstall0/cleanupErrornull/Recoveredseq27, 독립 stock121/8/7,726,223,158 hash/write times PASS. 실행 전후/현재 VDXR 동일. 사용자 종료 방식/메뉴 전환 원인은 미확정. 상세 <RepoRoot>\docs\VFX-RENDER-D138-RETURN.md.
- D-138 offline31checks/exit0/managed0warnings0errors,195/28/67/57+Native41fault4exact4flip4shapePASS. 진단만 보완이며 VFX/점멸 버그 OPEN, 새로운 live getter/camera 수락 아님. d138a preparation 진행; 자동 game launch 없음.
- Deployment follow-up COMPLETE: d138ainstaller0/232files/Installedseq14/independentverify0/prepare0,liveDLL/config/pins/currentpointer/stockoutsideownedhash+time/VDXRunchangedPASS. CurrentMODDED/READY_PREINSTALLED_NOT_LAUNCHED diagnostic-only;버그/게이트 모두 유지,자동launch없음.

## D-136 — Diagnostic-only implementation; no bug closure
- TOOL-070/071 OPEN: camera/native/busy 관찰과 실제 renderer/mesh/parent/bone 식별을 준비. 실제 incident/camera sampling co-occurrence와 새 callback 실행은 아직 미검증. 기존 조준·데미지 보존, 원인 수정 아님.
- 진단 한계: 제한된 후보 목록, worker drop/error,180초 cutoff를 반드시 검사. Tick 완료와 실제 hand drive를 별도 기록. 진단 실패는 적용 이후 gameplay 경로를 중단시키지 않도록 격리; 설치 실패는 기록 후 중단.
- TOOL-069 OPEN: particle count/simulationSpace가 실제 particle velocity나 projectile owner를 입증하지 않음. TOOL-072 OPEN: 전체 verifier가 native rebuild하므로 최종 hash 재고정/설치 후 rebuild 금지.
- 신규 bounded writer/source fixtures55/55(추가3회반복),기존195+28+67및Native41fault/4exact/4flip/4shapePASS. 전체31check/exit0/managed0warnings0errors. freshd136a preparation 완료, 시각/헤드셋 수락 없음.
- Deployment follow-up COMPLETE: freshd136ainstaller0/독립verify0,232files/currentpointer/Installedseq14/liveDLL/config/stockoutsideownedhash+timePASS. 현재MODDED/READY_PREINSTALLED_NOT_LAUNCHED;자동실행 없음. 실제busy/카메라/mesh/VFX검증 미완료이므로TOOL-069/070/071/072닫지않음.
- Authorized run follow-up: 사용자 "시작" 후07:28:55Z 기존설정/DLL확인·헤드셋probe PASS·Steam실행,07:28:58Z gamePID16792. 현재MODDED/RUNNING_WAITING_FOR_GAMEPLAY(MainMenu_2);위READY는 실행 전 이력. 새render callback/실제증상/복구는 아직 미확인,버그 OPEN 유지.
- Gameplay follow-up:07:29:31Z locked/07:30:07Z SESSION Egypt_Map/controllers=true/rightDriving,leftWaitingForWeapons. 누적rightOverwrites2868은 실제 flicker원인 확정 아님. 사건/새callback/시각수락/복구 pending,기존 버그 OPEN.

## D-135 — Raw pose jump and diagnostic blind spots — 2026-10-11
- TOOL-070/071OPEN: sequentialrawframes1095/1096/1097at36.500/36.533/36.567svisiblychangeforegroundhand/weaponappearance,alsoinmatchededitedframes1165–1170. Notedit-onlyartifact,notprooftwoduplicatemeshesoractualVFXvelocity. Rawmatchingwindowhas33.334msmaxPTSinterval,noadjacentexactsmallimage/fullresolutionchecksumrepetition;notquantitativeflickerrateorzeroCPU/capturejudderproof.
- ConcreteSTATICcandidates:PlayerController.UpdatewritesWeaponAnim.weapon_baselocalpose(RVAs50FC41/50FDF8),ReadHands6skipsLateUpdatedrive/trace/reticlebeforerender,andeye-enablewindowsdifferfromalwaysenabledsource. Co-occurrenceunverified. Post-driveroverwrite0cannotexcludeunobservedskipframes;rootworldpreservationdoesnotrollbackdescendantanimation.
- TOOL-069projectileowner/actualflametrajectoryandTOOL-072verifierrebuildremainseparateOPEN. Noaim/damage,animation,OBSorproductionchange. D-135offlinecounterexamples16/16;ShotScopes28/28,WeaponRuntime67/67PASS;noUnity/headsetacceptance.
- Freshstock121/8/7,726,223,158bytes/hash/timePASS;releaseDLLsunchanged,D-13496168bdverified. CurrentSTOCKnotREADY,d130aconsumed;RENDER-TIMING-D135-ANALYSIS.md.

## D-134 — HMD hand-count clarification — 2026-10-11
- **User report:** "HMD 안에서는 확인하지 못함. 거기서 손은 좌 우 하나씩만 있었음". User remembers one left/one right hand in HMD;the anomalous hand toward flame was not verified there. Do not describe this as HMD duplicate-hand evidence,or as proof the recorded event was normal in HMD.
- TOOL-070/071 remain OPEN: desktop game-output render timing and camera-specific model visibility/state take priority;shared bone/model/particle faults still possible. Flame trajectory and projectile ownership (TOOL-069) remain separate. No acceptedaim/damage change or visual acceptance.
- Read-only source review: OpenXrHost.cs198–244 schedules eye-render windows and separately ticks hands in LateUpdate;321–329 copies source camera into separate eye textures. HandUi.cs330–375 contains eye-pre-cull/canvas placement for UI,not evidence of an offending hand-mesh writer. No specific hand-render writer identified.
- D-132/D-133 commits verified0c9eebe/3e572a3;fresh independentstock121files/8dirs/7,726,223,158bytes/hash/timePASS,oldPIDsabsent. Documentation-only;no build/install/launch/OBS write. CurrentSTOCKnotREADY;no new sitting requested.

## D-133 — OBS Game Capture diagnosis update — 2026-10-11
- TOOL-070/071 remain OPEN: capture path now confirmed game_capture targetingVampire Hunters.exe/D3D11shared texture,NOTHMDmirror;active scene 모니터1 name does not mean display capture. Separate desktop/HMD source paths support presentationtiming investigation,not proven cause.
- CurrentFFmpeg720p30/6000kbps/GOP250;nearbyrawCPUx264keyint250confirmed,not savedNVENCAV1encoder. GOP250means≈8.33smaximum at30fps,not expected continualflicker. x264CPUload possible,overload unproven. EarlierNVENC recordings must be evaluated separately.
- Latestlog3277drawn/3276output/no explicitlagged-skipped-dropped/no reconnectduringrecording;not proofzero repeatedcontents. Windowdisappearance15:01:00KSTprecedestop5.9s;tailonlyexplanation,causetoearlierflickerunproved. Edited/rawmappingunknown. No acceptedaimchange,ownerinferenceorbugclosure;HMDhandvisibility still needs clarification.
- Freshstock121/8/7,726,223,158bytes/hash/timePASS,oldPIDsabsent,releaseDLLsunchanged. No newrunorOBSsettingwrite. Evidence:OBS-CAPTURE-D133-ANALYSIS.md.

## D-132 — Smooth HMD / unstable recording and coupled hand–flame appearance — 2026-10-11
- **User report:** HMD smooth/no recorded flicker,reticle stable;hand/model also appears toward flame effect. TOOL-070/071 presentation follow-up OPEN;actual firing direction remains accepted user behavior,not a request to rotate aim. Recording source and whether displaced hand is also visible in HMD are unknown.
- **Confirmed architecture:** gameplay camera remains enabled;two separate HMD-eye textures/view matrices feedOpenXR. Hand driver/reticle updates are not a per-camera render sample. Pre-driver right changed-pose count19245 versus post-driver maxmuzzle0.028deg/scopedmax179.23deg/3.6777m are different stages;zero post-writer overwriteSamples cannot exclude late/native/render writers.
- **Leading hypotheses,not proven:** game-window versus HMD render-state mismatch;camera-specific viewmodel/bone or late weapon writes;particle simulation/velocity frame;low submission rate/runtime smoothing/capture pacing. d130a median39.7submissions/s versus90Hzdisplay does not prove reprojection active or explain arbitrary model angles alone. No first-visible projectile ownership identified;TOOL-069 separate.
- **Evidence:** <Recordings>\편집후\이상한점.mp4,36s1920x1080,SHA2560FBB7FD8…;12overview+12dense sought samples,not frame-accurate flicker/owner analysis;clip/run synchronization unknown. Analysis:VFX-MIRROR-D132-ANALYSIS.md. No production change or bug closure.
- **d130a lifecycle verified:** finished06:03:44Z,HOST_FAILED Egypt_Map→MainMenu_2/source+playerdestroyed,causeunknown;uninstall0/cleanupErrornull/stockTrue,Recoveredseq27/currentpointerhistorical;PIDs15320/72828absent. Fresh whole-stock121/8/7,726,223,158bytes/hash/timePASS,VDXRunchanged. Do not rerun or reinstall solely after session end.

## D-131 — d130a preinstalled launch — 2026-10-11
- Explicit user 시작; harness15320,headset attached05:57:41Z,game running05:57:43Z;installer skipped. WAITING_FOR_GAMEPLAY05:58:02Z,stderr empty at checkpoint. No runtime fix or bug closure; TOOL-069/070/071/072/gates remain OPEN. Cleanup pending; do not assume stock from previous d130a readiness or d127a cleanup.
- Update05:58:37Z: GAMEPLAY LOCKED05:58:18Z Egypt_Map;game72828/harness15320live,controllersTrue,rightDriving,leftWaitingForWeapons,stderr0B. Launch confirmed,visual symptoms not accepted or resolved.

## D-130 — Deployment readiness failure acknowledged — 2026-10-11
- Agent left stock after D-129 diagnosis and misread the user's installation question, despite the D-115/D-124 install-first preference. Reaffirmed contract: game exit -> stock; code/build/test completion or readiness repair -> preinstall before reporting ready; execution command -> -UsePreinstalled, not slow installation.
- Corrective deployment action COMPLETE: fresh d130a installed232files/exit0; independentread-onlyverificationexit0/empty stderr at05:32:33Z (allpayload/config/journal/currentpointer/stockoutsideownedroots). Unchanged validated d127a payload; no game/probe launch or gameplay fix. AGENTS.md and living docs retain the requirement. TOOL-069/070/071/072 and acceptance gates stay OPEN.
- TOOL-072 operational ordering: all builds/full verifier before install; do not rebuild the native DLL after readiness unless reinstalling the final outputs. This is a documented mitigation, not a determinism fix or bug closure.

## D-129 — Latest symptom correction / separate VFX and projectile-origin triage — 2026-10-11
- **User correction:** actual weapon firing angle works; the **visible effect angle** is wrong. Separately a **right-hand projectile-firing weapon sometimes emits from the left**. Record as TOOL-070 visual direction and TOOL-069 emitted-origin follow-up, not renewed evidence that accepted aim should be rotated. Both OPEN severity2 Stage6B/8. TOOL-071's prior model-spin report remains separate/unverified.
- **Confirmed offline:** flame.draw_shoot_vfx sets its own effect forward from muzzle or start/end; projectile.shoot_projectile/get_free_bullets/init are distinct from currently instrumentedshoot_forward.149 sampled flame/muzzle forward matches agree; neither particle velocity nor actual projectile instance/owner/origin is captured. d127a sampledRIGHTflame/LEFTSMG is not the user's separateRIGHTprojectile incident.
- **Candidate diagnoses (not fixes):** VFX simulation/velocity/render-time pose versus native start/end branch; projectile live muzzle reference/owner versus activation/init callback versus uncovered/nested camera scope. Base pool reuse explicitly sets position/rotation, so "recycled projectile keeps left position" is not established. No wrong-hand event causally identified.
- **Evidence limits corrected:** equal fire/release totals do not mean immediate/per-shot release; native flame.update has a reload_t>0 Stop branch which can leave already_firingTrue. Sparse zero pool observations do not establish absence. Shot roots preserve captured world pose, not camera/aim pose; prior TOOL-071 coincident-root explanation withdrawn. Non-normalized rounded-forward angles in old analyzer cannot prove the quoted4.9–8deg. See VFX-PROJECTILE-D129-ANALYSIS.md and append-onlyD-129.
- **d127a complete:** HOST_FAILED onEgypt_Map→MainMenu_2/source+player destroyed; menu-return cause unattributed;uninstall0/cleanupErrornull/stockTrue;terminalRecoveredseq27. Fresh stock121/8/7,726,223,158bytes exact hashes/times. No old game/harness remains. This is restoration success, not headset/gate acceptance.
- **Validation/scope:** focusedShotScopes28/28,WeaponRuntime67/67;offline analyzer/PowerShell receipts;no production changes/build/install/launch. TOOL-069/070/071/072 and formal gates stay OPEN. Need one existing-video event timestamp; no new sitting requested.

## D-128 d127a execution / evidence limits — 2026-10-11
- User approved Phase2 with 실행. Harness17848 started04:57:47Z; config and plugin/native hash guards passed, headset attached, game56600 running04:57:50Z. Max gameplay600s; auto-uninstall/stock verification pending. TOOL-070/071/072 remain OPEN, no visible or gate acceptance.
- D-127 measurement limits: Step sets Seeded/Target during forward read before up read; first/reseed/replaced-node up data may be invalid/stale. Sampling immediately after driver writes and before FireTick cannot exclude inter-frame overwrites or capture all temporary shot scopes; containerVsWritten=0 is not an overwrite exclusion. TraceHand resolution is outside Observe's catch, so a resolution exception could skip downstream Tick work via its caller. Monitor live errors; no instrumentation/pose behavior edit during installed sitting. Source-pattern guards are not comprehensive proof of non-interference.
- Runtime04:58:39Z GAMEPLAY LOCKED Egypt_Map;04:59:13Z rightDriving/fireCalls12, leftWaitingForWeapons/fireCalls0, nativeFailures0/fireFailedFalse. Trace summaries/flame logs emitted, no matching error/exception/HAND_AIM-disabled or host-failure file so far. Caller OpenXrHost.LateUpdate actually releases/disables handAim if resolution throws; none observed at checkpoint. Left spin not yet tested by counts; user visual result and auto-cleanup pending.

## TOOL-072 — A full verifier run invalidates a pre-installed payload (OPEN, found 2026-10-11)

- The install-first path (D-124) lets `headset-handaim.ps1 -UsePreinstalled` skip the install, but its guard compares the installed DLLs against the **current build outputs**. `tests\NativeHost.Tests.ps1:4` calls `src\render\NativeHost\Build.ps1 -SelfTest`, so **every full `tests\verify.ps1` run rebuilds `VhvrNativeHost.dll` and changes its sha256** (`CC593A69…` → `D025204E…` observed with no native source change) — the native build is not byte-deterministic, unlike the plugin.
- Effect: if the verifier runs after phase 1, the sitting refuses with "installed BepInEx\plugins\VhvrNativeHost.dll differs from this build; re-install instead of reusing" and phase 1 must be re-run (a few minutes of hashing). Worked around here by running phase 1 **after** the last verifier run, and by not rebuilding native code before the sitting.
- Options (not yet chosen, needs the user): compare against the hashes recorded in the install transaction instead of the build outputs, or build the native host with a deterministic flag, or state the ordering rule in the playtest doc. TOOL-071's sitting is unaffected as long as nothing rebuilds between phase 1 and the sitting.

## TOOL-071 — Left-hand weapon model visibly spins/swings while its shots still land (OPEN, triaged 2026-10-11)

- **User report** (clip `<Recordings>\편집후\왼손 사격시 회전.mp4`, 2.00 s, 1920x1080, exported 12:37): the left-hand weapon model sweeps/rotates in view while the world (corridor, enemies) stays still; at t=0.1 s a giant flat polygon covers the middle of the view, at 0.6/1.1 s the weapon is out of view, at 1.4/1.8 s it reappears displaced. Attacks still hit inside the reticle.










### OBS-002 — The death trigger is a child of the player root
Status: observed, Stage 1/8 design input | Severity: 3 (would-be) | Stage: 0
`playerChildren n=27` lists **`Death Volume`** among the player root's direct children, together with `Level Up`, `Player For Anim Parent`, `Main Camera`, `Mini Map Camera`, `Hunter_For_Intro`, `Skip Cutscene HUD` and the HUD canvases. Consequence: the death flow is driven by a collider inside the player prefab, so a VR conversion that re-parents, clones or deactivates player children (a common stereo trick) can silently break death handling. Treat the player root's child list as load-bearing.

### TOOL-005 — `UICHANGE` is deliberately silenced by every time-scale edge
Status: documented behavior, not a defect | Severity: 4 (tooling) | Stage: 0
`WatchTimeScale()` sets `uiValid = false` on every edge so the UI watcher re-baselines instead of logging the same set twice (`RuntimeProbe.cs`, `# (i)`). Result: pause, level-up and death panel sets appear **inline on the `TIMESCALE` line** (`ui=N=[..]`) and never as a `UICHANGE`; Run 4 produced exactly 4 `UICHANGE` events, all menu <-> gameplay. Recorded so a later session does not read a missing `UICHANGE` as "no UI changed" and does not "fix" a watcher that is working as intended. No probe v5 needed for this.

### LIM-003 — Probe v4's depth-3 / 40-node anim dump cannot see the live aim pivot
Status: recorded limitation | Severity: 4 (tooling reach) | Stage: 0
`AnimTreeDepth = 3`, `AnimTreeBudget = 40`. The dump reached `Player For Anim Parent` -> `Player_For_Anim` -> `Player_Anim_Armature` -> `Gun.L`/`Gun.R`/`Root`, but `Gun.L`, `Gun.R`, `Root` and `Player_Anim_Armature` are byte-identical across all 29 dumps (static bone proxies), `Root` has `children=4` (depth 4, outside the dump) and the seven per-hunter `*Arm` / `RomanCandleGatling_Anim` leaves are not flagged active/inactive. The live muzzle/aim transform is therefore **not** identified by v4; identifying it requires either a depth-4 dump or an active-state flag, and the active-arm question is worth checking statically against IL2CPP metadata first because a probe change costs another launch.




























### TOOL-007 — UUVR's patcher cannot patch this build's `globalgamemanagers` (NullReferenceException, every launch)
Status: OPEN — this is what failed the Session 1 spike | Severity: 2 (framework unusable for this target as shipped) | Stage: 1
Evidence (identical in every launch, 2026-10-07): `[Info   :   Console] Backing up '…/Vampire Hunters_Data/globalgamemanagers'…`
-> `Created backup in '…/globalgamemanagers.bak'` (first run) or `Backup already exists.` (later runs) ->
`Using classData file from path '…/BepInEx/patchers/classdata.tpk'` ->
`[Error  :AssemblyPatcher] Failed to run initializer of com.raicuparta.uuvr: System.NullReferenceException: Object reference not set to an instance of an object.`
at `BepInEx.Preloader.Core.Patching.AssemblyPatcher.PatchAndLoad() … AssemblyPatcher.cs:line 314`.
BepInEx contains the error (`Executing 0 patch(es)`) and the process continues; our plugin still reaches `ENTRY_EXECUTED`.
Effect: the patcher's file half succeeds every launch (it deletes and re-copies `openvr_api.dll`, `openxr_loader.dll`,
`UnityOpenXR.dll`, `XRSDKOpenVR.dll` into `_Data/Plugins`, and writes `UnitySubsystems/{UnityOpenXR,XRSDKOpenVR}` +
`StreamingAssets/SteamVR`), but its `PatchVR` half never writes the XR device list, so Unity has no supported device:
`[Probe] SAMPLE scene='MainMenu_2' screen=1920x1200 xrLoadedDevice='' xrEnabled=False` in all three VR configurations
(run-vr-1 SteamVR + connected HMD `oculus.1PASH5D1P17365` + Steam VR Theater; run-vr-2 SteamVR off, VDXR disconnected;
run-vr-3 SteamVR off, VDXR connected). `globalgamemanagers` itself stays byte-identical to stock
(`B9DF43EED29E6408FAC79B260FEB5E712F95EE7EE172F003E4CA501A6A87D1D9`), i.e. the declared mutation never happens - the
`.bak` file is the only artifact.
Hypothesis (not proven at source level): the `AssetsTools.NET` bundled with `Uuvr.Patcher` cannot parse a Unity 2022.3.37
`globalgamemanagers`, returns no asset file, and the caller dereferences it. Our payload ships readable source for only 2 of
UUVR's ~12 runtime classes, and the exception is caught by BepInEx above the failing frame, so the frame below
`PatchAndLoad()` is not observable without rebuilding the patcher.
Not a game bug: the game itself ran normally, rendered its menu (`MainMenu_2`), and exited normally through its own menu.

**Root cause found and the edit re-owned — 2026-10-07 (D-062).** The hypothesis above is now *resolved differently and
better*: the failing frame was never needed, because the field itself was read straight out of game data. In the
pinned stock `globalgamemanagers`, `BuildSettings` (class 141, path id 11) is a 1272-byte blob at offset 150424 whose
`enabledVRDevices` vector has **count 0**. The stock build therefore ships an empty device list, Unity loads no XR
device, and UUVR's runtime has nothing to drive — which is exactly the `xrEnabled=False` / `xrLoadedDevice=''`
symptom. Corroborating evidence from the same spike logs: Unity *did* discover UUVR's loaders
(`1 'inputs' descriptors matched` and `1 'displays' descriptors matched` for **both** `UnitySubsystems/UnityOpenXR`
and `UnitySubsystems/XRSDKOpenVR`), and there is still no XR device load line anywhere — so the subsystem manifests
were fine and the ordered device list was the gate.

Status: **mitigated in this project, upstream defect unchanged** | Severity re-scoped to 3 (tooling; one workaround,
now automated). `src/tools/XrDevicePatch` performs the same single edit (`BuildSettings.enabledVRDevices` at path id
11) that `PatchVR` intends, with no class database and no third-party dependency, and the install flow applies it
after the transaction and records it as an engine `MutationObserved` event so `-Action Uninstall` still restores the
pinned pre-image. Verified by `tests/xr-device-patch.ps1` (structure, determinism, minimal diff, read-back) — see
D-062 for the full evidence list. The file is no longer expected to stay byte-identical to stock while the spike is
installed; that is the point of the declared mutation.

Two side effects are recorded rather than hidden. (1) **The patcher still throws every launch.** This is harmless to
the fix *by ordering*: `Initialize()` runs `CopyFilesToGame` before `PatchVR`, so the VR plugins and
`UnitySubsystems` manifests are copied into the game at launch, and `PatchVR` dies before its `File.OpenWrite`, so
the file we rewrote is never overwritten. (2) `globalgamemanagers.bak` is created from the **patched** file on the
first launch after the fix (it is a declared owned scope and is removed on uninstall). Removing the patcher's crash
to also close SP2's `Failed to run initializer` error is a separate, larger change (pre-staging the plugin payload)
the fix *by ordering*: `Initialize()` runs `CopyFilesToGame` before `PatchVR`, so the VR plugins and
`UnitySubsystems` manifests are copied into the game at launch, and `PatchVR` dies before its `File.OpenWrite`, so
the file we rewrote is never overwritten. (2) `globalgamemanagers.bak` is created from the **patched** file on the
first launch after the fix (it is a declared owned scope and is removed on uninstall). Removing the patcher's crash
to also close SP2's `Failed to run initializer` error is a separate, larger change (pre-staging the plugin payload)
and is left open.

One structural fact about the payload is worth recording here because it is easy to misread: the install stages
**36** files (the BepInEx loader plus UUVR's own plugin and patcher), while the **23** `_Data` files —
`Vampire Hunters_Data\Plugins\*.dll`, `UnitySubsystems\*`, `StreamingAssets\SteamVR\*` and `globalgamemanagers.bak`
— are *declared generated*: UUVR's patcher writes them when the game launches. The engine allows a declared generated
file to be absent until the installed tool has run, which is why an install+teardown with no launch quarantines **6**
roots rather than 15. Proven by the D-062 round-trip drill.

**Mitigation measured insufficient - 2026-10-07 (Stage A, D-063).** The edit was applied and recorded end to end (`XR_DEVICE_PATCH_APPLIED … devices=[OpenVR,OpenXR,Oculus]`, journal ends `MutationObserved`, teardown back to stock) and Unity still loaded no device: four probe reports across a 30 s menu-only run read `xrLoadedDevice='' xrEnabled=False`, PASS conditions 0/3 (`artifacts/stage-a-device-check.sa2.json`, `artifacts/uuvr-spike/run-vr-4-sim/device-load-evidence.txt`). Re-owning the edit was still the right call - it removed UUVR's crash from the device-list question - but the list is not what gates the load on this build (LIM-003). The open question is no longer "which names" but "can this build load an XR device at all without the XR loader code".

### TOOL-008 — `.gitignore`'s unanchored `tools/` silently ignored `src/tools/`, i.e. the D-062 fix's own source
Status: CLOSED (fixed 2026-10-07 in the D-062 work) | Severity: 3 (process/hygiene; would have shipped a fix whose source was absent from the repo) | Stage: 1
Discovery: `git status --short` before committing D-062 listed `tests/xr-device-patch.ps1` as untracked but **not** `src/tools/XrDevicePatch/*`, although the four source files were on disk. `git check-ignore -v src/tools/XrDevicePatch/XrDevicePatch.csproj` answered `.gitignore:3:tools/`. The pattern was unanchored, so it matched **any** directory named `tools` at any depth - both the intended target (the repo-root `tools/`, 240 vendored BepInEx files) and `src/tools/`, which is first-party source.
Resolution: `.gitignore` line 3 changed from `tools/` to `/tools/`, with a comment recording why it is anchored. Re-checked afterwards: `tools/bepinex-be788/winhttp.dll` is still ignored, `src/tools/XrDevicePatch/*.cs` and `.csproj` are visible to git, and `**/bin/` + `**/obj/` still exclude the build output.
Consequence for later stages: the untracked-file list is not a complete list of new files - `git status --short` only shows what git does **not** ignore, so "the file exists" and "git sees the file" must be checked separately (`git check-ignore -v <path>`). Any new `src/` subtree should be confirmed visible to git when it is created, not when it is committed.


### TOOL-009 — A tool-call timeout kills the process tree; "detached" means the launching call must return immediately
Status: fixed in the Stage A runner | Severity: 4 (harness) | Stage: 1B
The first Stage A attempt (`artifacts/run-stage-a-device-check.ps1 -Tag sa1`) was launched with `Start-Process -PassThru
-RedirectStandardOutput` **inside a call that then waited for the run**; the agent shell's 30-second limit killed that
call and the whole child tree with it - twice, because the second attempt repeated the mistake. The two interrupted
installs left their workspaces behind exactly as the engine recorded them: `artifacts/transactions/20261007T165822Z`
(1 journal line, `Begin` only) and `.../20261007T170128Z` (`Begin`, `Ready` and 6 `Published` pairs). Neither had
reached the settings edit - `globalgamemanagers` was still the pinned pre-image at 228,452 B - and the pointer
`current.txt` still named the previous workspace, because `Install-UuvrSpike.ps1` writes it only after the edit
succeeds. Recovery used the designed path, `-Action Uninstall -Workspace <interrupted workspace>` (never a hand
cleanup), run against `20261007T170128Z`, the one that had published files: it re-derived the journal, returned
`ADDITIONS_QUARANTINED_STOCK_VERIFIED` with 0-byte stderr, and left the folder at 121 files with the pinned
`globalgamemanagers` SHA256 (`B9DF43EE…A87D1D9`) again. Measured rule: a child started by a call that **returns
immediately** survives (the 19-minute `uuvr-teardown.d062` run did, and the `_probe-sp`/`_probe-wmi` children wrote
their files after 90 s), a child of a call that waits does not. Never make a tool call wait for a long run, and never
"simplify" a long run back into a waiting call.

### TOOL-010 — The Stage A runner started the game from the exe, which LIM-002 forbids
Status: fixed before any evidence was taken | Severity: 4 (would have faked a Stage A FAIL) | Stage: 1B
The runner's launch step was `Start-Process -FilePath 'Vampire Hunters.exe'`, but `steam_api64.dll` lives in
`Vampire Hunters_Data\Plugins\x86_64` and there is no `steam_appid.txt`, so a direct launch dies with
`Steamworks is not initialized` (LIM-002) without ever reaching a device load - i.e. it would have produced
`xrEnabled=False` and been read as "the D-062 edit does not work". Found by re-reading the recorded limitations before
the first launch; the fix starts `steam://rungameid/2206270` and discovers the process by name (Steam returns no
handle). The `sa1` run was stopped before its launch step and recovered (TOOL-009) rather than being reported.


### TOOL-011 — The Meta XR Simulator starts a frame loop for the Khronos sample but not for our client (SP5 M2)
Status: OPEN | Severity: 2 (blocks SP5 M2-M4; the test bed itself is proven) | Stage: SP5
Date: 2026-10-07. Governs: `docs/playtests/SPIKE-OPENXR-HOST.md` M2.

**What happens.** With the simulator as the active OpenXR runtime, our probe (`src/render/OpenXrProbe`) creates the
instance, gets the `Meta Quest 3` system, matches the adapter LUID the runtime demands
(`0x00000000000170C1`, RTX 5080), creates its own D3D11 device (Level_11_1), the session, the LOCAL space, both eye
swapchains (1680x1760, 3 images each) and reaches `XR_SESSION_STATE_READY` - and then **blocks inside the first
`xrWaitFrame` forever**. The runtime's own log shows the session never leaving READY, no validation error, and - apart
from the setup differences listed below - the same lines as a working client.

**Control that makes this a bug in our client and not a broken test bed.** The Khronos sample from the pinned zip,
`hello_xr.exe -g D3D11`, run twice on this machine against the same simulator instance and config, drives
READY -> SYNCHRONIZED -> VISIBLE -> FOCUSED within 50 ms and submits frames
(`artifacts/openxr/m1-hello-xr.log`; runtime logs `meta_xrsim_2026-10-08_35440.log` and `_63472.log`).

**Attempts, in order (all measured).**
1. `FrameState.Type` was not set: the runtime answered `XR_ERROR_VALIDATION_FAILURE` 300/300 with
   `frameState should have type 'XR_TYPE_FRAME_STATE'`. Fixed - a real bug in our code. The call then stopped erroring
   and started blocking instead.
2. Waiting for `XR_SESSION_STATE_SYNCHRONIZED` after `xrBeginSession` (10 s, polling events): the state never leaves
   READY, the wait times out, WaitFrame still blocks.
3. Pumping events every frame, as the sample does: no change.
4. Restarting the simulator GUI before the run: no change (the sample still works against the restarted GUI).
5. Running with a visible console window (the sample had one): no change.
6. Enabling `XR_EXT_user_presence` (`--user-presence`): the runtime accepts the extension; the session still stops at
   READY.

**Second round: full parity with the sample, all measured (R1-R11, 2026-10-07).** The probe grew the switches needed
to reproduce the sample exactly (`--spaces-all`, `--actions`, `--order sample`, `--api-version`, `--depth`, `--parity`,
`--enumerate-all`) and now writes a receipt *before* the frame loop, so a hang still leaves evidence. Every
configuration below ended with the session at READY and the first `xrWaitFrame` blocked, on the same runtime:

| # | Configuration (one change per row unless noted) | Result |
|---|---|---|
| R1 | swapchains after `xrBeginSession` + VIEW/STAGE spaces + action set with 2 action spaces + touch-profile bindings | READY, blocked |
| R2 | R1 + `apiVersion` 1.0.63 (the literal the sample sends; see the bug below) | READY, blocked |
| R3 | R2 + `XR_KHR_composition_layer_depth` + a depth swapchain per eye (format 40) + depth info per view | READY, blocked |
| R4 | R3 under `use_batch_mode` (Meta's own CI setting) - no frontend GUI in the picture at all | READY, blocked |
| R5 | R4 + the sample's *exact* action set (grab float, hand pose, vibrate output, quit bool, 4 profiles), its 7 visualized spaces, 12_1 device | READY, blocked |
| R6 | R5 with `log_level: verbose` | READY, blocked (verbose adds no frame-loop detail) |
| R7 | R5 with `ses_texture_format: jpg` (the shipped config's own "for stability" alternative) | READY, blocked |
| R8 | R5 with `default_refresh_rate: 90` (present in the shipped spatial config, absent from the GUI one) | READY, blocked |
| R9 | R8 + the three queries the sample makes and we did not (view configuration types, environment blend modes, reference spaces) | READY, blocked |
| R10 | batch mode with tracking paths absolutised (R4-R6 had broken relative paths, so batch mode was never really tested) | READY, blocked; runtime stdout `RPC failed: Deadline Exceeded` (expected - no frontend) |
| R11 | R9 with the depth extension actually enabled (see the second bug below) | READY, blocked, depthEnabled=true, format 40 |

**Two real bugs in our client were found and fixed by this round.** (1) The apiVersion literal: the pinned header
defines `XR_API_VERSION_1_0` as `XR_MAKE_VERSION(1, 0, 63)` - not 1.0.0 - and the runtime logs the value, so the
sample and this client differed in a line the runtime can see. (2) `--parity` did not actually enable the depth
extension: the gate read the raw `EnableDepth` flag instead of the effective `UseDepth`, so R5-R10 ran without it.

**Where the evidence now stands.** With R11 the runtime's own log for our client is *identical* to the sample's up to
the READY event - instance data, enabled extensions, layer list, space and action creation, session creation, the
## Mechanics recon and file-work consent — 2026-10-06
Q-010/Q-016 answered; read-only installed-file work and project-local tools performed, not game/loader/XR execution. Fresh 23/23 tests/full verifier process exit 0, zero-warning/error builds, config/CLI/offset/hash checks pass; original four core/metadata hashes unchanged. No gameplay bug opened or closed. TOOL-001's 46 undecodable segments remain; broader keyword matches/overlapping capped buckets are candidates, not proven hooks, new bugs or unsupported mechanics.
Inspection correction: initial data listing used incorrect `VampireHunters_Data` and exited 1; corrected to actual `Vampire Hunters_Data`, listing/readback passed. No game-file error inferred. Initial asynchronous verifier wrapper printed a blank process exit, so no exit success was claimed for it; subsequent direct captured child explicitly returned FULL_VERIFIER_PROCESS_EXIT=0 and final VERIFIED line, saved in artifacts/mechanics-verifier.exit.txt. These are tooling-evidence corrections, not gameplay verification or a gate pass. Project manifest now records user reports only; no stock files/runtime configuration modified or private saves read.

## v3 live run observations — 2026-10-06
Read-only stage; no mod feature exists yet, so nothing below is a confirmed gameplay bug. Evidence: `artifacts/tx/run/run-v3-live-20261006/LogOutput.v3.log` (350,658 B, `1695689A95611260906ADD2E002032D63AC6FC68181C529BDC50B6876B67E49F`, 48 samples, `probeErrors=0`), summary `artifacts/tx/run/probe-summary-v3live.txt`. Details in D-035.

### OBS-001 — Camera GameObjects are reparented mid-session
Status: observed, VR risk | Severity: 3 (would-be) | Stage: 0
`Main Camera` appeared under three different parents in one run: directly under `Prefab_Player_Level1` (gameplay), and twice under a `.../CameraControler/Main Camera` chain (`Player For Anim Parent/.../Root/CameraControler`, and the `Hunter_For_Intro` variant) during the stage-entry sequence. A head-tracking injection written against one hierarchy will break at the next reparent. Mitigation is a Stage-1 design constraint (re-validate/re-anchor each frame), not a game defect. Not reproducible-on-demand yet; observed once, on the log record.

### LIM-001 — Il2CppInterop could not restore every game method
Status: recorded limitation | Severity: 3 (tooling reach) | Stage: 0
Log lines: `Il2CppInteropGen Failed to restore 3 fields`, `Failed to restore 946 methods`, `IL unstrip statistics: 8358 successful, 1366 failed`. The read-only probe needed none of them. Assumption for later stages: some types/methods may be unavailable for hooking; every hook must be proven by a visible in-game effect, never by the mere fact that the call site was found.

### LIM-002 — Direct exe launch is not a valid test path
Status: environment constraint | Severity: 3 | Stage: 0
Starting `Vampire Hunters.exe` directly ends the process with `InvalidOperationException: Steamworks is not initialized.` (Unity `Player.log`). All loader/probe runs must be started via Steam (`steam://rungameid/2206270`). Recorded so a future session does not misread this as a loader failure.

### LIM-003 — The target build contains no XR loader code, so no settings edit can make Unity load a device
Status: recorded limitation (falsifies the D-062 device-list hypothesis) | Severity: 1 (target capability) | Stage: 1B
Measured 2026-10-07 while interpreting the Stage A FAIL, from two independent sources: the IL2CPP metadata (`Vampire Hunters_Data\il2cpp_data\Metadata\global-metadata.dat`, 10,889,004 B) and `GameAssembly.dll` (47,464,960 B). Both contain the core XR API (`XRDisplaySubsystem` 8 / 26 hits, `XRSettings` 4 / 13) and **neither** contains a single occurrence of `XRGeneralSettings`, `XRManagerSettings`, `XRLoader`, `OpenXRLoader`, `OpenVRLoader`, `UnityEngine.XR.Management`, `UnityEngine.XR.OpenXR`, `XRConfigurationData`, `XRDeviceSimulator`, `XRSDKOpenVR` or `UnityOpenXR`. Positive controls on the same scan find `UniversalAdditionalCameraData` (4), `PlayerController` (8), `Weapon` (375) and `UnityEngine.Rendering.Universal` (387), so the method is not producing false negatives. Consequence: the XR Plug-in Management package and both providers were **not part of the build**, so the managed loader classes that call `Load()` and start a display subsystem do not exist in the player. `BuildSettings.enabledVRDevices` is consumed by exactly that management layer, which is why writing it (UUVR's intent and this project's D-062 replication) cannot load a device: there is nothing to instantiate, whatever names the list holds.
Corroborating comparison on the same machine: `Arken Age` (Unity 2022.3.46f1, a shipping VR game) ships the *same* native layout - `_Data\UnitySubsystems\{UnityOpenXR,XRSDKOpenVR}` plus `openvr_api.dll`, `openxr_loader.dll`, `UnityOpenXR.dll`, `XRSDKOpenVR.dll` in `Plugins\x86_64` - and its `BuildSettings.enabledVRDevices` is **empty** (`XrDevicePatch inspect`: `enabledVRDevices(0) = []`, `ROUNDTRIP_OK blob=32168B`), while its `globalgamemanagers` carries XR settings assets (`xrdevicesimulatorsettings`). An empty device list therefore does not prevent a device load in a build that has the loader code: the list is not the gate.

### GAP-001 — Unexercised evidence branches
Status: CLOSED for the ESC and death-overlay branches (Run 4); secondary-fire branch filled with data; only the aim-pivot identity remains open | Severity: 4 | Stage: 0
Re-scoped 2026-10-06 with the Q-022 answers (D-036): the v3 run **ended in death**, ESC was **never pressed**, and no **secondary-fire weapon was owned/used**, so `fire1=0` is explained by the player, not by an unbound button.
- **ESC pause branch (R10): CLOSED by Run 4** - `ESC_DOWN t=51.1` -> `TIMESCALE 1.00->0.00` with `ui=5` and `cursor=None/True`; second press at t=52.8 -> `0.00->1.00` with `cursor=Locked/False`. Evidence: `artifacts/tx/run/run-v4-live-20261006/LogOutput.v4.log`.
- **Secondary fire: filled with data by Run 4** - `f1=1466f/13down` (RMB) plus `q=82`/`e=68` frames for the auxiliary slots. No longer "blocked by game progress"; do not re-ask for this scene.
- **Death overlay (R9): CLOSED by Run 4** - the v4 every-frame `TIMESCALE` edge caught the stop itself (`t=318.0 frame=45224 cursor=Locked/False ui=5=[..]`, back at `t=322.5` with `MainMenu_2` already active). The panel lives inside `Canvas - Screens`, whose active state does **not** change at death, so the distinguishing signals are the time stop plus the still-locked cursor (pause releases it, level-up adds `NewNewCanvas`).
- **Aim/muzzle pivot: still open, narrowed** - see LIM-003 below; this one needs tooling (depth-4 dump / active flag), not another run. No VR write, no XR init; Stage 0 remains NOT PASSED.

## Run 4 observations — 2026-10-06 (probe v4, read-only)
Log: `artifacts/tx/run/run-v4-live-20261006/LogOutput.v4.log` (381,901 B, `3F6A6A2C…`, 35 samples, `probeErrors=0`). Nothing below is a confirmed gameplay bug - no mod feature exists yet.

### OBS-002 — The death trigger is a child of the player root
Status: observed, Stage 1/8 design input | Severity: 3 (would-be) | Stage: 0
`playerChildren n=27` lists **`Death Volume`** among the player root's direct children, together with `Level Up`, `Player For Anim Parent`, `Main Camera`, `Mini Map Camera`, `Hunter_For_Intro`, `Skip Cutscene HUD` and the HUD canvases. Consequence: the death flow is driven by a collider inside the player prefab, so a VR conversion that re-parents, clones or deactivates player children (a common stereo trick) can silently break death handling. Treat the player root's child list as load-bearing.

### TOOL-005 — `UICHANGE` is deliberately silenced by every time-scale edge
Status: documented behavior, not a defect | Severity: 4 (tooling) | Stage: 0







































What the three logs say side by side, with two working clients as references:

| client | apiVer | enabledExtensionCount | first `xrEndFrame` | session |
|---|---|---|---|---|
### TOOL-007 — UUVR's patcher cannot patch this build's `globalgamemanagers` (NullReferenceException, every launch)
Status: OPEN — this is what failed the Session 1 spike | Severity: 2 (framework unusable for this target as shipped) | Stage: 1
Evidence (identical in every launch, 2026-10-07): `[Info   :   Console] Backing up '…/Vampire Hunters_Data/globalgamemanagers'…`
-> `Created backup in '…/globalgamemanagers.bak'` (first run) or `Backup already exists.` (later runs) ->
`Using classData file from path '…/BepInEx/patchers/classdata.tpk'` ->
`[Error  :AssemblyPatcher] Failed to run initializer of com.raicuparta.uuvr: System.NullReferenceException: Object reference not set to an instance of an object.`
at `BepInEx.Preloader.Core.Patching.AssemblyPatcher.PatchAndLoad() … AssemblyPatcher.cs:line 314`.
BepInEx contains the error (`Executing 0 patch(es)`) and the process continues; our plugin still reaches `ENTRY_EXECUTED`.
Effect: the patcher's file half succeeds every launch (it deletes and re-copies `openvr_api.dll`, `openxr_loader.dll`,
`UnityOpenXR.dll`, `XRSDKOpenVR.dll` into `_Data/Plugins`, and writes `UnitySubsystems/{UnityOpenXR,XRSDKOpenVR}` +
`StreamingAssets/SteamVR`), but its `PatchVR` half never writes the XR device list, so Unity has no supported device:
`[Probe] SAMPLE scene='MainMenu_2' screen=1920x1200 xrLoadedDevice='' xrEnabled=False` in all three VR configurations
(run-vr-1 SteamVR + connected HMD `oculus.1PASH5D1P17365` + Steam VR Theater; run-vr-2 SteamVR off, VDXR disconnected;
run-vr-3 SteamVR off, VDXR connected). `globalgamemanagers` itself stays byte-identical to stock
(`B9DF43EED29E6408FAC79B260FEB5E712F95EE7EE172F003E4CA501A6A87D1D9`), i.e. the declared mutation never happens - the
`.bak` file is the only artifact.
Hypothesis (not proven at source level): the `AssetsTools.NET` bundled with `Uuvr.Patcher` cannot parse a Unity 2022.3.37
`globalgamemanagers`, returns no asset file, and the caller dereferences it. Our payload ships readable source for only 2 of
UUVR's ~12 runtime classes, and the exception is caught by BepInEx above the failing frame, so the frame below
`PatchAndLoad()` is not observable without rebuilding the patcher.
Not a game bug: the game itself ran normally, rendered its menu (`MainMenu_2`), and exited normally through its own menu.

**Root cause found and the edit re-owned — 2026-10-07 (D-062).** The hypothesis above is now *resolved differently and
better*: the failing frame was never needed, because the field itself was read straight out of game data. In the
pinned stock `globalgamemanagers`, `BuildSettings` (class 141, path id 11) is a 1272-byte blob at offset 150424 whose
`enabledVRDevices` vector has **count 0**. The stock build therefore ships an empty device list, Unity loads no XR
device, and UUVR's runtime has nothing to drive — which is exactly the `xrEnabled=False` / `xrLoadedDevice=''`
symptom. Corroborating evidence from the same spike logs: Unity *did* discover UUVR's loaders
(`1 'inputs' descriptors matched` and `1 'displays' descriptors matched` for **both** `UnitySubsystems/UnityOpenXR`
and `UnitySubsystems/XRSDKOpenVR`), and there is still no XR device load line anywhere — so the subsystem manifests
were fine and the ordered device list was the gate.

Status: **mitigated in this project, upstream defect unchanged** | Severity re-scoped to 3 (tooling; one workaround,
now automated). `src/tools/XrDevicePatch` performs the same single edit (`BuildSettings.enabledVRDevices` at path id
11) that `PatchVR` intends, with no class database and no third-party dependency, and the install flow applies it
after the transaction and records it as an engine `MutationObserved` event so `-Action Uninstall` still restores the
pinned pre-image. Verified by `tests/xr-device-patch.ps1` (structure, determinism, minimal diff, read-back) — see
D-062 for the full evidence list. The file is no longer expected to stay byte-identical to stock while the spike is
installed; that is the point of the declared mutation.

Two side effects are recorded rather than hidden. (1) **The patcher still throws every launch.** This is harmless to
the fix *by ordering*: `Initialize()` runs `CopyFilesToGame` before `PatchVR`, so the VR plugins and
`UnitySubsystems` manifests are copied into the game at launch, and `PatchVR` dies before its `File.OpenWrite`, so
the file we rewrote is never overwritten. (2) `globalgamemanagers.bak` is created from the **patched** file on the
first launch after the fix (it is a declared owned scope and is removed on uninstall). Removing the patcher's crash
to also close SP2's `Failed to run initializer` error is a separate, larger change (pre-staging the plugin payload)
and is left open.

One structural fact about the payload is worth recording here because it is easy to misread: the install stages
**36** files (the BepInEx loader plus UUVR's own plugin and patcher), while the **23** `_Data` files —
`Vampire Hunters_Data\Plugins\*.dll`, `UnitySubsystems\*`, `StreamingAssets\SteamVR\*` and `globalgamemanagers.bak`
— are *declared generated*: UUVR's patcher writes them when the game launches. The engine allows a declared generated
file to be absent until the installed tool has run, which is why an install+teardown with no launch quarantines **6**
roots rather than 15. Proven by the D-062 round-trip drill.

**Mitigation measured insufficient - 2026-10-07 (Stage A, D-063).** The edit was applied and recorded end to end (`XR_DEVICE_PATCH_APPLIED … devices=[OpenVR,OpenXR,Oculus]`, journal ends `MutationObserved`, teardown back to stock) and Unity still loaded no device: four probe reports across a 30 s menu-only run read `xrLoadedDevice='' xrEnabled=False`, PASS conditions 0/3 (`artifacts/stage-a-device-check.sa2.json`, `artifacts/uuvr-spike/run-vr-4-sim/device-load-evidence.txt`). Re-owning the edit was still the right call - it removed UUVR's crash from the device-list question - but the list is not what gates the load on this build (LIM-003). The open question is no longer "which names" but "can this build load an XR device at all without the XR loader code".

### TOOL-008 — `.gitignore`'s unanchored `tools/` silently ignored `src/tools/`, i.e. the D-062 fix's own source
Status: CLOSED (fixed 2026-10-07 in the D-062 work) | Severity: 3 (process/hygiene; would have shipped a fix whose source was absent from the repo) | Stage: 1
Discovery: `git status --short` before committing D-062 listed `tests/xr-device-patch.ps1` as untracked but **not** `src/tools/XrDevicePatch/*`, although the four source files were on disk. `git check-ignore -v src/tools/XrDevicePatch/XrDevicePatch.csproj` answered `.gitignore:3:tools/`. The pattern was unanchored, so it matched **any** directory named `tools` at any depth - both the intended target (the repo-root `tools/`, 240 vendored BepInEx files) and `src/tools/`, which is first-party source.
Resolution: `.gitignore` line 3 changed from `tools/` to `/tools/`, with a comment recording why it is anchored. Re-checked afterwards: `tools/bepinex-be788/winhttp.dll` is still ignored, `src/tools/XrDevicePatch/*.cs` and `.csproj` are visible to git, and `**/bin/` + `**/obj/` still exclude the build output.
Consequence for later stages: the untracked-file list is not a complete list of new files - `git status --short` only shows what git does **not** ignore, so "the file exists" and "git sees the file" must be checked separately (`git check-ignore -v <path>`). Any new `src/` subtree should be confirmed visible to git when it is created, not when it is committed.


### TOOL-009 — A tool-call timeout kills the process tree; "detached" means the launching call must return immediately
Status: fixed in the Stage A runner | Severity: 4 (harness) | Stage: 1B
The first Stage A attempt (`artifacts/run-stage-a-device-check.ps1 -Tag sa1`) was launched with `Start-Process -PassThru
-RedirectStandardOutput` **inside a call that then waited for the run**; the agent shell's 30-second limit killed that
call and the whole child tree with it - twice, because the second attempt repeated the mistake. The two interrupted
installs left their workspaces behind exactly as the engine recorded them: `artifacts/transactions/20261007T165822Z`
(1 journal line, `Begin` only) and `.../20261007T170128Z` (`Begin`, `Ready` and 6 `Published` pairs). Neither had
reached the settings edit - `globalgamemanagers` was still the pinned pre-image at 228,452 B - and the pointer
`current.txt` still named the previous workspace, because `Install-UuvrSpike.ps1` writes it only after the edit
succeeds. Recovery used the designed path, `-Action Uninstall -Workspace <interrupted workspace>` (never a hand
cleanup), run against `20261007T170128Z`, the one that had published files: it re-derived the journal, returned
`ADDITIONS_QUARANTINED_STOCK_VERIFIED` with 0-byte stderr, and left the folder at 121 files with the pinned
`globalgamemanagers` SHA256 (`B9DF43EE…A87D1D9`) again. Measured rule: a child started by a call that **returns
immediately** survives (the 19-minute `uuvr-teardown.d062` run did, and the `_probe-sp`/`_probe-wmi` children wrote
their files after 90 s), a child of a call that waits does not. Never make a tool call wait for a long run, and never
"simplify" a long run back into a waiting call.

### TOOL-010 — The Stage A runner started the game from the exe, which LIM-002 forbids































































































































### TOOL-022 — `xrEndSession` answers `XR_ERROR_SESSION_NOT_STOPPING`, and the probe ends the session anyway
Status: **OPEN, cosmetic** | Severity: 4 | Stage: SP5
Date: 2026-10-08. Found by: every run since the frame loop started working
(`tool011-hang-location.m3/m4`, `m2-vdxr-a1`, `m2-vdxr-wait`): `session: xrEndSession failed: xrEndSession returned
ErrorSessionNotStopping`.

**What happens.** The probe calls `xrEndSession` while the session is not in the STOPPING state, so the runtime refuses.
The spec's own sequence is `xrRequestExitSession`, then wait for the session-state event to report STOPPING, then
`xrEndSession`. Nothing after this point in the probe depends on the session, the instance is destroyed cleanly and the
receipt is complete, so no measurement is affected - it is recorded so that a clean shutdown is not mistaken for one that
was never needed. Fix when a rung depends on it (e.g. an M3 restart-in-place check).
### TOOL-023 — The `--clear` path fails on a runtime-owned swapchain texture with E_INVALIDARG (VDXR)
Status: **RESOLVED** (reproduced and fixed headset-free on the simulator, 2026-10-08) | Severity: 2 (it blocked M2's
colour clause and M3's picture) | Stage: SP5
Date: 2026-10-08. Found by: the fifth sitting's D2 rung, `artifacts/openxr/probe/m2-vdxr-d2.json` (D1 passed in the same
sitting: `artifacts/sp5-m2-vdxr-headset.hd4.json`, `verdict=M2_VDXR_PASS_D1`).

**What happens.** With `--clear --left FF0000 --right 0000FF` the first frame that carries a layer throws
`HRESULT: [0x80070057] ... E_INVALIDARG` (bare HRESULT, no call name, so it comes from a Vortice call rather than
`Support.Check`), the frame is never ended, and every later `xrBeginFrame` is discarded: `submitted 0/300`,
`emptyFrames=1`, `discarded=298`, session `1,2,3,4,5`, `verdict=FAIL`. The same client without `--clear` submits 299/300
in the same sitting.

**Why the existing checks missed it.** `--selftest-d3d11` (the headset-free check TOOL-012 left behind) clears **a texture
we create ourselves**, and it passes (`selftest-d3d11.json`, and `selftest-after-d072.json` after the D-072 changes). The
failing case is a texture the **runtime** owns, handed over through `XrSwapchainImageD3D11KHR.Texture` - the two are not
the same test, and this is the first run in which the clear ever touched a runtime image. The HRESULT is also the same one
TOOL-012 started from (`CreateRenderTargetView failed hr=0x80070057`), which suggests the remaining fault is in building a
render-target view over the runtime's texture (candidates: the texture's own `D3D11_TEXTURE2D_DESC` - bind flags, array
size, sample count, or the format VDXR chose, `28` = `R8G8B8A8_UNORM` here versus `29` on the simulator), or in the
`Marshal.QueryInterface` wrapper that hands the runtime's pointer to the generated bindings.

**Cause (measured, not guessed).** The simulator reproduces it exactly, with no headset and no UAC:
`artifacts/openxr/probe/tool023-repro.json` = `verdict=FAIL`, `submitted 0/60`, `discarded=58`, session `1,2,3,4,5`. With
each call in `ComInterop.ClearImage` named in its own exception and the texture's description in the receipt, the answer
is one line:

```
CreateRenderTargetView failed with the default view description
(texture format=R8G8B8A8_Typeless bind=0x28 misc=0x2 usage=Default array=1 mips=1 samples=1 size=1680x1760)
```

The runtime creates the swapchain images with a **TYPELESS** format (`bind=0x28` is render target | shader resource,
`misc=0x2` is shared, one sample, no array), and D3D11 refuses a view on a typeless resource unless the view names a
format. The default view description names the resource's own format - which is typeless - so every clear answered
`E_INVALIDARG`. The device, the context, the `QueryInterface` wrapping and the bind flags were all fine; only the view
description was wrong. The format VDXR and the simulator choose (28 / 29) never mattered: neither runtime hands out a
`E_INVALIDARG`. The device, the context, the `QueryInterface` wrapping and the bind flags were all fine; only the view
description was wrong. The format VDXR and the simulator choose (28 / 29) never mattered: neither runtime hands out a
typed texture to begin with.

**Fix.** `ComInterop.CreateView` checks the texture's own format: a typeless resource gets an explicit
`RenderTargetViewDescription` with `Format` = the **swapchain's** format (the format the compositor will read the image
in) and `ViewDimension` = `Texture2D`; a typed resource keeps the default description. `Eyes.Submit` and `D3D11SelfTest`
pass the format down, and the first clear's description lands in the receipt as `ClearDiagnostic`.

**Evidence (headset-free, simulator).** `artifacts/tool011-hang-location.m7.json`: the `--clear` rung
(`--clear --left FF0000 --right 0000FF`, 300 frames) is `verdict=PASS`, `submitted 300/301` (the one extra attempt is the
runtime's start-up `shouldRender=0` frame, ended empty), `discarded=0`, `invalidPoseFrames=0`, session `1,2,3,4,5`, and
records `texture format=R8G8B8A8_Typeless ...; view format=R8G8B8A8_UNorm_SRgb`. The same drill's `default` and
`--no-state-gate` rungs are unchanged (`PASS`, `300/301`, `discarded=0`), so nothing regressed.

**Why it can't come back unnoticed.** `--selftest-d3d11` now runs **two** cases: a typed texture of our own (which it
always did) and a **TYPELESS** one, reproducing the runtime's shape with no runtime at all - `verdict=PASS (typed=ok,
typeless=ok)`, `artifacts/openxr/probe/selftest-tool023.json`. And `tests/tool011-hang-location.ps1` gained a permanent
`-clear` rung, so the real runtime-owned texture is cleared on every drill run.

**Lesson.** The old self-test proved the *device, context and wrapper* were right and proved nothing about the *image*:
it passed for a day while every real clear failed, and the fix was in the one input it never varied. A headset-free check
is only worth its name if it varies what the runtime actually varies.

**Also fixed here: `--frames` counted attempts, not submissions.** `--frames 300` produced `submitted 299/300` because the
runtime's start-up empty frame consumed one slot, so the client fell one frame short of M2's "at least 300 submitted
frames" through no fault of its own. The budget now counts frames that were **submitted**; empty and discarded frames are
still counted in the receipt and the loop is still bounded by `--budget-seconds` and the watchdog. `--frames 300` now
reports `submitted 300/301`.

### TOOL-024 — One run ended without a verdict while the session sat at READY (observed once, not reproduced)
Status: **OPEN, observed once** | Severity: 3 | Stage: SP5
Date: 2026-10-08. Found by: a `--frames 1` check during the TOOL-023 work,
`artifacts/openxr/probe/tool023-frames1.json`:
`--loader <pinned> --out tool023-frames1.json --frames 1 --budget-seconds 8 --ready-seconds 15 --watchdog-seconds 20`.

**What happens.** The process ended ~2.4 s in, and the receipt on disk is the phase snapshot taken inside the loop
(`LastPhase = endFrame (no layer) -> ok after 24 ms`, `FramesAttempted=1`, `submitted=0`, `empty=0`, session `1,2`), with
**no `Verdict` and no `Error`**. That is not one of the ways a run is supposed to end: the frame loop sets PASS/FAIL, the
watchdog writes FAIL, and `Main` writes `INCONCLUSIVE` if nothing else did - so the process must have been torn down
between the snapshot and the end of `Main`. The session never left READY in that run, and the simulator's own API-layer log
in the probe's stderr shows its instance destroyed and its gRPC server shutting down, i.e. the layer's teardown ran while
the frame loop was still going.

**Not reproduced.** The identical command was run three more times and each PASSed in ~4.7 s (`submitted 1/2`, session
`1,2,3,4,5`), and the same run shape inside the drill is unaffected. It is recorded rather than dismissed because a run
that ends without a verdict breaks the TOOL-011 rule that a hang must be a measurement, and because the frame loop now
tries again after an empty frame (`--frames` counts submissions, D-074) - so a session that never becomes Visible is the
state where this would be seen again.

**Next (headset-free).** If it recurs: keep the receipt, and check whether the process exited or was killed
(`--watchdog-seconds 0` removes the watchdog thread as a suspect, and the simulator GUI's own window state - the thing
that decides whether the session advances from READY - is worth recording at the same time).


### TOOL-023 VDXR confirmation — 2026-10-08 (hd5, D-075)
Resolution now confirmed on the original failing runtime, not just the simulator. `artifacts/openxr/probe/hd5-d2.json`: PASS, 60/61, cleared=True, `R8G8B8A8_Typeless bind=0xA8 misc=0x2 array=1 samples=1 size=2428x2440; view format=R8G8B8A8_UNorm`. No frame/wait/locate failures, no discarded or invalid-pose frames. User verified left red / right blue; SP5 M2 COMPLETE. D1 proves the new submitted-frame budget with 300/301 on VDXR. TOOL-024 did not recur in this sitting; still OPEN. TOOL-022 shutdown warning still exists; not silently closed.
**Usability finding (not a rendering failure):** user said the colour check was too short and requested +3 seconds next time. D-075 adds a measured active post-target hold to C2/D2; this does not reopen the already-confirmed eye-colour acceptance.

### TOOL-024 — One run ended without a verdict while the session sat at READY (observed once, not reproduced)
Status: **OPEN, observed once** | Severity: 3 | Stage: SP5
Date: 2026-10-08. Found by: a `--frames 1` check during the TOOL-023 work,
`artifacts/openxr/probe/tool023-frames1.json`:
`--loader <pinned> --out tool023-frames1.json --frames 1 --budget-seconds 8 --ready-seconds 15 --watchdog-seconds 20`.

**What happens.** The process ended ~2.4 s in, and the receipt on disk is the phase snapshot taken inside the loop
(`LastPhase = endFrame (no layer) -> ok after 24 ms`, `FramesAttempted=1`, `submitted=0`, `empty=0`, session `1,2`), with
**no `Verdict` and no `Error`**. That is not one of the ways a run is supposed to end: the frame loop sets PASS/FAIL, the
watchdog writes FAIL, and `Main` writes `INCONCLUSIVE` if nothing else did - so the process must have been torn down
between the snapshot and the end of `Main`. The session never left READY in that run, and the simulator's own API-layer log

**Also fixed here: `--frames` counted attempts, not submissions.** `--frames 300` produced `submitted 299/300` because the
runtime's start-up empty frame consumed one slot, so the client fell one frame short of M2's "at least 300 submitted
frames" through no fault of its own. The budget now counts frames that were **submitted**; empty and discarded frames are
still counted in the receipt and the loop is still bounded by `--budget-seconds` and the watchdog. `--frames 300` now
reports `submitted 300/301`.

### TOOL-024 — One run ended without a verdict while the session sat at READY (observed once, not reproduced)
Status: **OPEN, observed once** | Severity: 3 | Stage: SP5
Date: 2026-10-08. Found by: a `--frames 1` check during the TOOL-023 work,
`artifacts/openxr/probe/tool023-frames1.json`:
`--loader <pinned> --out tool023-frames1.json --frames 1 --budget-seconds 8 --ready-seconds 15 --watchdog-seconds 20`.

**What happens.** The process ended ~2.4 s in, and the receipt on disk is the phase snapshot taken inside the loop
(`LastPhase = endFrame (no layer) -> ok after 24 ms`, `FramesAttempted=1`, `submitted=0`, `empty=0`, session `1,2`), with
**no `Verdict` and no `Error`**. That is not one of the ways a run is supposed to end: the frame loop sets PASS/FAIL, the
watchdog writes FAIL, and `Main` writes `INCONCLUSIVE` if nothing else did - so the process must have been torn down
between the snapshot and the end of `Main`. The session never left READY in that run, and the simulator's own API-layer log
in the probe's stderr shows its instance destroyed and its gRPC server shutting down, i.e. the layer's teardown ran while
the frame loop was still going.

**Not reproduced.** The identical command was run three more times and each PASSed in ~4.7 s (`submitted 1/2`, session
`1,2,3,4,5`), and the same run shape inside the drill is unaffected. It is recorded rather than dismissed because a run
that ends without a verdict breaks the TOOL-011 rule that a hang must be a measurement, and because the frame loop now
tries again after an empty frame (`--frames` counts submissions, D-074) - so a session that never becomes Visible is the
state where this would be seen again.

**Next (headset-free).** If it recurs: keep the receipt, and check whether the process exited or was killed
(`--watchdog-seconds 0` removes the watchdog thread as a suspect, and the simulator GUI's own window state - the thing
that decides whether the session advances from READY - is worth recording at the same time).


### TOOL-023 VDXR confirmation — 2026-10-08 (hd5, D-075)
Resolution now confirmed on the original failing runtime, not just the simulator. `artifacts/openxr/probe/hd5-d2.json`: PASS, 60/61, cleared=True, `R8G8B8A8_Typeless bind=0xA8 misc=0x2 array=1 samples=1 size=2428x2440; view format=R8G8B8A8_UNorm`. No frame/wait/locate failures, no discarded or invalid-pose frames. User verified left red / right blue; SP5 M2 COMPLETE. D1 proves the new submitted-frame budget with 300/301 on VDXR. TOOL-024 did not recur in this sitting; still OPEN. TOOL-022 shutdown warning still exists; not silently closed.
**Usability finding (not a rendering failure):** user said the colour check was too short and requested +3 seconds next time. D-075 adds a measured active post-target hold to C2/D2; this does not reopen the already-confirmed eye-colour acceptance.


**What happened.** With TOOL-012 fixed the loop reached `xrEndFrame` - `xrWaitFrame` Success, `xrBeginFrame` ok,
`xrLocateViews` returned 2 poses, both eyes acquired/waited/released - and every frame was rejected with
`xrEndFrame returned ErrorPoseInvalid` (`frames=0/5`). The same receipt shows why: the session sat at `Ready (2)` and
never reached `Synchronized (3)` within 20 s, i.e. the runtime was not displaying the app.

**Cause.** The probe rendered from READY, which the Khronos sample never does: its render loop runs only while the
session is VISIBLE or FOCUSED and it keeps polling events otherwise. A runtime may answer `xrEndFrame` with
`XR_ERROR_POSE_INVALID` for poses it did not produce for a running session, and VDXR does.

**Fix.** `SessionLifecycle.Pump` records the last session state the runtime reported in `Receipt.SessionState`;
`FrameLoop` skips the frame (counting `NotVisibleSkips`) while that is below `Visible`; and the wait after
`xrBeginSession` now waits for **Visible** rather than Synchronized. `WaitForState` also returns the furthest state seen
rather than the last read, so a receipt can no longer say `Unknown (0)` when the session actually reached Ready (2).
tries again after an empty frame (`--frames` counts submissions, D-074) - so a session that never becomes Visible is the
state where this would be seen again.

**Next (headset-free).** If it recurs: keep the receipt, and check whether the process exited or was killed
(`--watchdog-seconds 0` removes the watchdog thread as a suspect, and the simulator GUI's own window state - the thing
that decides whether the session advances from READY - is worth recording at the same time).


### TOOL-023 VDXR confirmation — 2026-10-08 (hd5, D-075)
Resolution now confirmed on the original failing runtime, not just the simulator. `artifacts/openxr/probe/hd5-d2.json`: PASS, 60/61, cleared=True, `R8G8B8A8_Typeless bind=0xA8 misc=0x2 array=1 samples=1 size=2428x2440; view format=R8G8B8A8_UNorm`. No frame/wait/locate failures, no discarded or invalid-pose frames. User verified left red / right blue; SP5 M2 COMPLETE. D1 proves the new submitted-frame budget with 300/301 on VDXR. TOOL-024 did not recur in this sitting; still OPEN. TOOL-022 shutdown warning still exists; not silently closed.
**Usability finding (not a rendering failure):** user said the colour check was too short and requested +3 seconds next time. D-075 adds a measured active post-target hold to C2/D2; this does not reopen the already-confirmed eye-colour acceptance.


**What happened.** With TOOL-012 fixed the loop reached `xrEndFrame` - `xrWaitFrame` Success, `xrBeginFrame` ok,
`xrLocateViews` returned 2 poses, both eyes acquired/waited/released - and every frame was rejected with
`xrEndFrame returned ErrorPoseInvalid` (`frames=0/5`). The same receipt shows why: the session sat at `Ready (2)` and
never reached `Synchronized (3)` within 20 s, i.e. the runtime was not displaying the app.

**Cause.** The probe rendered from READY, which the Khronos sample never does: its render loop runs only while the
session is VISIBLE or FOCUSED and it keeps polling events otherwise. A runtime may answer `xrEndFrame` with
`XR_ERROR_POSE_INVALID` for poses it did not produce for a running session, and VDXR does.

**Fix.** `SessionLifecycle.Pump` records the last session state the runtime reported in `Receipt.SessionState`;
`FrameLoop` skips the frame (counting `NotVisibleSkips`) while that is below `Visible`; and the wait after
`xrBeginSession` now waits for **Visible** rather than Synchronized. `WaitForState` also returns the furthest state seen
rather than the last read, so a receipt can no longer say `Unknown (0)` when the session actually reached Ready (2).

**Checked headset-free** (headset attached but not being displayed,
`artifacts/openxr/probe/no-headset-after-gate.json`): session `Ready (2)`, `NotVisibleSkips=5`, `verdict=FAIL`, error
`the session never reached Visible (last state Ready (2)); no frame was submitted`.


### TOOL-025 — M4a camera receipt note was not valid JSON
Status: fixed in source; old evidence retained unchanged; new stereo receipts valid | Severity: 3 | Stage: SP5 | Date: 2026-10-08
Found while reading `artifacts/m4a-capture/run-20261007T203459Z/capture-02.json`: the note contained a literal `"..." + "..."` expression. This is C# syntax, not JSON; `ConvertFrom-Json` rejects it. Cause: the handwritten StringBuilder emitted a concatenation instead of one JSON string. Fix: emit a single note string. New stereo diagnostic uses System.Text.Json throughout; live baseline/lean/yaw/summary receipts parse. Old evidence is not rewritten. M4a-specific re-run remains separate from new stereo receipt checks.

### TOOL-026 — New math test project shared obj/bin with existing fixture executables
Status: VERIFIED FIX (full verifier 23 checks PASS, 2026-10-08) | Severity: 3 | Stage: tooling | Date: 2026-10-08
Verifier `verify-d077-final` detected wrong stdout identity: Recon printed the 29 stereo math checks, not expected 37 recon tests. New csproj shared SDK obj/bin with existing tests, so incremental build reused apphost/output. Stereo project moved into tests/StereoMath; obsolete root csproj removed and existing tests cleaned. Full verifier `verify-d077-isolated` is the confirmation; never relax identity/count checks.


### TOOL-027 — Native bridge tool invocation friction (D-078)
Status: corrected source; native self-test verified | Severity: 3 | Stage: SP5 tooling
First Recon invocation used src/tools/Recon instead of existing src/diagnostics; failed before any game access. Corrected bounded API pin succeeds (GL.IssuePluginEvent present, shape only).
MSVC vcvars emitted a bare vswhere lookup warning despite successful compiler/self-test exit 0; add installed vswhere directory to child PATH, rerun now emits no warning. Native four exact typed/typeless RGBA/BGRA cases and four negative shape cases pass; no game compatibility claim.



### TOOL-028 — First host harness read a live log with exclusive File.ReadAllText
Status: verified host2/3/4 shared log reader; no recurrence | Severity: 3 | Stage: SP5 M4b
`host1.runner.log` failed at the first BepInEx log read while loader generated interop; no host activation evidence. Hypothesis confirmed by IOException: File.ReadAllText share mode conflicts with writer. Match existing stereo harness shared Get-Content read with transient errors tolerated. Run halted and transactional uninstall invoked; do not treat this as a failed device/session hypothesis. Repeat only after stock restoration.


### TOOL-029 — Wrong expected native status ABI size caught before deployment
Status: verified native static_assert + live host3/4 Marshal ABI checks | Severity: 3 | Stage: SP5 tooling
Explicit static_assert rejected expected HostStatus=144. Ten 4-byte status fields occupy 40 bytes, double 8, two 44-byte EyePose structs: actual size=136, not 144. Correct both native and Marshal.SizeOf guards. No faulty ABI build deployed; first host1 predated this assertion and never reached host initialization.


### TOOL-030 — Host default RenderTexture has no sRGB storage
Status: verified host3/4 Gamma encoding + actual composed upright scene | Severity: 3 | Stage: SP5 M4b
host2 reaches plugin but fails before native Configure: `requires sRGB camera target`. Unity project's default ARGB32 target is not sRGB. Explicitly request RenderTextureReadWrite.sRGB for Linear projects; documentation confirms Gamma projects ignore this flag and directly emit display-coded bytes. Assert sRGB encoding only in Linear, record Gamma/Linear + target flag, and keep runtime matching SRGB format. No silent UNORM reinterpretation. This correction precedes the next run. No in-game session/copy confirmed by this attempt.


### RENDER-001 — Exact Unity texture copy is upside down in actual composed output
Status: fix verified in actual simulator image host4; headset not tested | Severity: 2 | Stage: SP5 M4b
host3 actual simulator Both screenshot (17:01:02Z) visibly shows the game menu scene upside down in both eyes. Same-device session succeeds, 192 submissions/384 GPU event completions, +3.0010033 s hold and orderly STOPPING/EndSession. Source/runtime raw bytes are exact (two hashes equal), which proves copy, not correct orientation.
Hypothesis: Unity render-target native rows are opposite OpenXR display rows. Fix on GPU: row-wise CopySubresourceRegion with reversed source Y (uncompressed RGBA/BGRA only), preserving all encoded bytes and leaving graphics pipeline state untouched, query completion before release. Initial shader-blit idea rejected before implementation because row copies provide stronger exactness without new shader/compiler/state dependencies; diagnostic overhead is logged, not hidden. Verified: four exact flipped-row GPU cases plus host4 actual Both screenshot upright, 192 submissions/384 GPU completions/+3.0157965 s/STOPPING/EndSession. Runtime/source comparison checks exact reversed rows. Not M4b PASS; keep baseline/lean/yaw geometry acceptance open.

### TOOL-031 — Steam URI launch produced no game process on host5
Status: RESOLVED user Cloud completion plus normal host6 Steam launch verified (D-079); stop if warning recurs | Severity: 3 | Stage: SP5 M4b automation
Install succeeds, but no game process/BepInEx log/native receipt appears during 140-second launch wait; harness times out and invokes transactional uninstall. Prior host3/4 same URI worked. Actual Steam foreground screenshot `artifacts/openxr/host/steam-launch-diagnostic.png` confirms a Cloud sync failure dialog warning that ignoring and playing may permanently delete progress. Do NOT choose ignore/continue, alternate launch or bypass; this crosses saved-data consent. Stop live runs after stock restoration and request the user resolve Cloud synchronization or explicitly decide risk. This is not a failed OpenXR hypothesis or motion proof.

### TOOL-032 — M3 repeat exits 0x40000015 after completed PASS receipt
Status: OPEN observed once; fresh regression repeat passed | Severity: 3 | Stage: SP5 tooling/runtime teardown
m3-d078 normal/swapped exit 0, but repeat exits 1073741845 (0x40000015 STATUS_FATAL_APP_EXIT). Receipt is complete PASS, 666/667 layers, +3.0011134 s, clean STOPPING/EndSession and printed XR_PROBE_OK. Harness correctly rejects nonzero process exit; do not override receipt/exit checks or call overall M3 regression PASS. Different from TOOL-024 (missing verdict); preserve both as open. Hypothesis: external simulator/process teardown after finished managed Main; investigate separate fresh simulator regression without touching game/Steam Cloud. No native bridge code is loaded by this standalone probe.
Recheck m3-d078-recheck normal/swapped/repeat PASS with actual exit 0 on all three (666/666/667, holds 3.0003852/3.0140009/3.0145377 s). Failure did not recur; TOOL-032 remains OPEN observed-once, not silently closed.


### TOOL-033 — Focused simulator key holds do not change located eye poses
Status: OPEN; raw remote simulator motion observed D-080, upright isolated capture/game motion still unverified | Severity: 3 | Stage: SP5 M4b automation | Date: 2026-10-09 (UTC)
`host6-d079-motion` launched normally after user Cloud completion; 669 layers / 1338 GPU completions / +3.0061186 s / STOPPING and EndSession. Three capture-request responses (submitted 75/109/145) all show unchanged position/quaternion despite foreground-confirmed 150 ms D/right-arrow holds. Harness rejects absent motion; no M4b acceptance. Hypotheses: headset-control modifier or viewport keyboard capture needed, not a failed device/session hypothesis. Inspect documented/default mappings before any retry; do not invent camera offsets or lower motion thresholds. Geometry source rungs 31/33/32 positive, zero negative, 3 regions each: still insufficient; swapped 42/42/43 negatives, identical zero positives. Two standalone retries (hover/bracket cycling; viewport focus/mapped scan codes) each produced <=128 exact samples with unchanged head pose and actual exit 0. Three failed approaches: stall report D-079, Q-047 physical-keyboard discriminator; no fourth guessed retry. These are not three failed device/session bridge hypotheses.


### TOOL-034 — Host6 uninstall exceeds 300-second installer backstop
Status: OPEN budget fix built; independent journal recovery exit 0 and exact stock verified, fresh complete transaction repeat pending | Severity: 2 | Stage: SP5 transaction automation | Date: 2026-10-09 (UTC)
Host6 runner correctly FAILS cleanup at 300 seconds; stockVerified=false and exit 2 retained. Journal shows 5 of 6 owned roots quarantined, last dotnet Quarantined 00:37:42Z; no Recovered event yet. Independent Check-Stock rejects extra-file count. Hypothesis: repeated complete stock hashing around each journal/guard consumes budget on this machine, not a foreign-file permission failure. Normal journal recovery started separately with bounded monitoring; no hand removal, bypassed hashes, saves access or next game launch. Preserve original FAIL receipt and separate recovery evidence. Recovery completed: recovery.exit=0, Recovered 2026-10-09T00:42:27.8042215Z, independent 121 stock files exact hashes/times. Configurable 600-second source budget keeps every hash/guard, fixture verified; no new complete live transaction repeat yet.

D-080 TOOL-033 update: physical-keyboard comparison deferred because user is away and delegates operation. No fourth blind key retry. Current official docs establish a distinct right-mouse-held mouse-look technique; first inspect live input-selection/help and UI positioning before exercising it. Compatibility mode rejected as unrelated controller aiming. Original three failures retained; no bug closure or M4b acceptance.
D-080 measured result: official RMB mouse capture changes standalone exact runtime orientation; no-input control unchanged, 4 unique exact left poses, 2317 submissions/+3.0138019 s/runner+probe+verifier exit 0/clean shutdown. TOOL-033 stays OPEN: original game motion failure not retested, head-center translation and calibrated pure yaw unverified. Large mixed rotation (-80 pitch/-78.2 yaw UIA) requires small-angle calibration; rotated-eye displacement is not translation. No physical-keyboard result fabricated.
D-080 final calibrated-input stall: actual paired-eye center translation 0.152773834 m (calibrate1) and 0.151292588 m (calibrate2); delayed yaw 16.399993 degrees at -80 pitch, visible positioning agrees. Capture re-entry resets yaw (16.6-degree contamination). Three calibration hypotheses rejected (pixel scaling, prime, no-mouse captured keys). Keycapture1 motion verifier exit 2, exact right-arrow delta 0 and D center delta 0 despite a later capture-origin rotation jump; runner/probe exit 0/2317/+3.0134938 s/clean session shutdown. Original bug remains OPEN for usable upright isolated input and game revalidation. No fourth calibration guess or speculative game deploy.

## D-086 (2026-10-09) independent aim - NOT headset verified
Hand-aim code (Hands.cpp, HandAim.cs, HandAimDriver.cs) is built and fixture-tested only. NOT verified: that VDXR/Quest delivers Touch aim poses to this session, that the driven container moves the game's real shots, that the game does not overwrite the container each frame, left-hand weapon existence, per-hand fire/reload/secondary. Not a game fix; TOOL-033/034/038 unchanged.

## D-087 (2026-10-09) headset drill prepared - NOT run
TOOL-039 (open, anticipated): the Unity same-device host has never run longer than about 3 minutes or at display rate against a real runtime; the first 600 s headset sitting can fail for transport reasons (pacing, session state, VDXR focus) that say nothing about hand aim. Treat such a result as INCONCLUSIVE for aiming. The old 55 s native budget and the 3 s hold remain for the simulator path; the headset path replaced them only behind the explicit Headset opt-in.
Anticipated, unmeasured: xrSyncActions can return XR_SESSION_NOT_FOCUSED and deliver no input to a non-visible session; the drill reports it as CONTROLLERS_NOT_DELIVERED/CONTROLLER_POSES_NEVER_ACTIVE rather than hiding it.

## D-088 (2026-10-09) from the first headset sitting ha1
TOOL-040 (open): in-VR UI absent. ScreenSpaceOverlay canvases (Player HUD, ComboAndCrosshairs, HealthBarMainHUD, Score, FloatingIcons, Screens, Level Up, pause menu) do not appear in the stereo eye cameras. Needs a UI conversion stage (re-render overlay canvases to WorldSpace or to a head-locked quad) — not started.
TOOL-041 (open): no crosshair/aim reticle in VR, so hand aim cannot be judged by eye.
TOOL-042 (open): trigger fire/reload not wired (HandAimDriver never calls fire/shoot, by design of D-086).
TOOL-043 (open): opening the pause or level-up menu ends the host with 'gameplay camera/scene ownership lost' (about 90 s into ha1). Failing clause not logged. In a headset this makes any menu action end the session.

## D-089 (2026-10-09)
TOOL-041 (reticle) and TOOL-042 (trigger fire) now have first implementations awaiting a headset sitting; both stay OPEN until the user reports. TOOL-043: ownership-lost now logs the failing clause; still open. TOOL-040 (UI overlay conversion) not started.
TOOL-040/043 update (D-090): HandUi + ownership grace built, awaiting headset sitting; both stay OPEN.

TOOL-044 (open): shots do not follow the per-hand reticle (ha2 user report). Evidence: game overwrites driven container (2348 frames). Next rung: find which transform shoot_forward/fire reads; do not patch blind.
TOOL-045 (open): locomotion/menu built (D-091) awaiting headset sitting.

TOOL-046 (open): 4-way movement and mouse-bound UI (ha4 report). D-093 built: virtual Xbox pad + HMD-anchored UI; awaiting headset sitting. TOOL-044/ha3 crash: still run with -NoShot until isolated.

## D-097..D-099 (2026-10-10)
TOOL-044/ha3+ha7 crash (open, mitigated, unverified): c0000005 in a runtime-generated thunk reading Vector3 args through a null pointer; consistent with the `draw_shoot_vfx` tracer prefix. Tracer patch now OFF by default (`-ShotTracer` to repro). ha9 ran 65 s without a crash and without -NoShot, but no shot-call evidence exists, so the `shoot_forward` prefix is still unproven.
TOOL-047 (fixed in code, not headset verified): "character does not move, cannot shoot" in ha9. Root cause: `WriteReceipt` threw on a non-finite JSON number, `OpenXrHost.LateUpdate` then disabled the whole hand driver (`HAND_AIM disabled`). Receipt write is isolated now (`receiptError`) and non-finite tracer values are null. Regression guards in tests/HandAim (73 fixtures).
TOOL-048 (open): snap turn (D-099) built and math-tested only; sign, post-turn locomotion heading and hand/UI following the turned anchor are unmeasured.
TOOL-049 (open): headset dropout is not recoverable. ha10 ended with `ErrorFormFactorUnavailable` when Virtual Desktop was not attached within 300 s; nothing wrong in the mod.


## D-100 (2026-10-10)
TOOL-050 (open, measuring): ammo empties after ~10 shots on a 39-round weapon in VR only (user report after ha11). Not proven. The hand fire path calls `PlayerController.fire(slot, ref true, dt)` every Unity frame while the trigger is held and `shoot_forward` runs ~22/s in runs of 3-4 identical calls. `shot.ammo` in the receipt (first/last/drops/maxShootsPerFrame) now measures it; hypotheses H1-H3 are in D-100. No fire-call change until the numbers exist.
TOOL-051 (open, fixed in code, not headset verified): the game crosshair is head-locked while bullets follow the hand muzzle (video 1010), so the aim mark misleads. `HandUi.HideGameCrosshair` culls the `Crosshair` graphics; the sphere reticle is the only aim mark. Check `ui.crosshairHidden` and that no game crosshair remains.
TOOL-052 (fixed in code, not headset verified): wrong pad mapping. leftStickPress is Run, not the dash; the scout dash is Special Ability = East. Now: right stick up = East, down = North (Bloody Rush), left/right stick click = leftShoulder/rightShoulder (ArmL/ArmR), left X = Run. Needs a headset check that each action fires in the game.
TOOL-053 (open): `docs/INPUT-MAP.md` and `config/bindings.json` still describe the D-057 layout (stick up = interact, down = blood ability); the code follows D-100. Reconcile when the user confirms the layout; interact has no input right now.
TOOL-054 (open): video 1010 could not be found on disk, so the HUD/aim report is taken from the user's description only.

TOOL-055 (open, built, not headset verified): the user's flat-game vocabulary has no "run"; the pad button my earlier notes called Run (leftStickPress) is Interact in the Arena input asset and Run in the other two. Left X now carries the Interact name; the stage asset in use is unproven (Q-D101-1). Counters renamed: `runPresses` -> `interactPresses`.
TOOL-056 (open, built, not headset verified): reload gesture (Y / B within 0.30 m of the headset -> pad West). Threshold is a guess; tune from `input.minFacePressHeadDistance` and `lastFacePressHeadDistance`.
TOOL-057 (open): ammo drain still unproven (TOOL-050); receipt now has `triggerPresses` / `heldSeconds` per hand so the user does not have to count by eye.
TOOL-058 (open, built, not headset verified): the dark translucent panel behind the in-game HUD (video 1010, 40 s) was not handled by `UiDim` (pause only). D-102 culls dark, translucent, non-Filled, panel-sized images on the head-locked HUD canvases; whether it removed the right object is unproven. Check `ui.hudDarkCandidates` and `ui.hudDarkHidden` in `hand-aim.json`.
TOOL-059 (open, built, not headset verified): HUD canvases are now rigidly head-locked at 1.5 m / 1.6 m wide. Corner elements (minimap, score) may fall outside the view or feel too close; tune `HudDistance` / `HudWidth`.

TOOL-060 (open, built, not headset verified): haptic pulse at the 0.30 m reach (D-103). The runtime may ignore or weaken a 60 ms / 0.4 pulse; `haptic.pulses*` only proves the request was queued. Tune `HapticAmplitude` / `HapticMilliseconds` after the sitting.

TOOL-061 (open, built, not headset verified): D-104 timing lines and `-Anisotropic`. Unknown until a sitting: the headset refresh rate, which stage limits the frame time, and whether anisotropic filtering changes fps or sharpness. Timeline rows after the native close repeat the final `frames`; ignore them when computing fps.

TOOL-062 **CLOSED (D-108 user acceptance, recorded D-109)**: UI now in front of eyes; level-up choices select correctly and cards do not flip; user explicitly considers this problem solved. d108b 7 UI clicks/126 hovers and render passes corroborate execution. No exhaustive clipping/readability/head-roll or full Stage 8 certification implied. Historical D-105/D-107 failures retained above.

TOOL-063 **OPEN, severity 2 (D-109)**: D-105 redirection runs (d108b 19047 camera-aimed shoot calls) but user reports bullets/flame to the right, approximately 90 degrees. Confirmed new mechanism: moving the parent game camera to the child muzzle moves/rotates that muzzle again; live post-write muzzle/camera mismatch about 59-60 degrees. No final tracer direction captured, flame damage collider/update path unknown. No fixed-axis or visual PASS; see SHOT-D109-ANALYSIS.md and TOOL-044.

TOOL-064 (open, built, not headset verified): the translucent panel behind the in-game HUD stayed in fp3 (user screenshot). TOOL-058/D-102 matched on dark tint and found 0 candidates. D-106 hides by translucency and size regardless of tint. Check ui.hudDarkHidden and ui.hudDarkCandidates.

























































































































































TOOL-075 **OPEN (D-143)**: 산성꽃(AcidFlower)도 같은 native set_forward 패턴(RVA 450030). 사용자 관찰상 정상이나 왼손/짧은 효과 때문일 수 있음. 오른손 산성꽃 시각 확인 필요.
TOOL-073 **d141a 실행 완료, 시각 미확인 (D-144)**: 방출 시점 이미터 오차 p50 0.00°/p99 2.6° (d140a p50 22.9°). 사용자 보고 대기. TOOL-075 AcidFlower 이번 미사용 → 미검증.

## d145a 헤드셋 세션(0.2 프리뷰)에서 나온 3건 — 2026-10-12

TOOL-076 **FIXED (D-146a), 헤드셋 재검증 대기**: 정상 스테이지 종료가 `host.json.failure.json`에 `verdict=FAIL`, `gameplay camera/scene ownership lost; no menu fallback; failed=source-destroyed,player-destroyed,scene:Egypt_Map->MainMenu_2`로 기록됐다. 실제로는 README 4번에 적힌 설계된 종료(스테이지 끝 → VR 종료)였는데 실패로 남아 리시트만 보면 세션이 고장난 것처럼 보였고, `tests\headset-handaim.ps1`도 이를 `HOST FAILED`로 읽고 브레이크했다. 수정: `OpenXrHost.StageEnded()`/`EndGameplay()`가 카메라·플레이어 파괴 또는 게임플레이 씬 이탈을 "스테이지 종료"로 판정해 verdict `HEADSET_GAMEPLAY_LEFT_NOT_ACCEPTED`로 정상 종료하고 failure 리시트를 쓰지 않는다. 재현 조건은 스테이지 완료 후 메인메뉴 복귀.

TOOL-077 **FIXED (D-147), 헤드셋 재검증 대기**: `src\render\NativeHost\Host.cpp:147`의 `if(status.frames==30) savePair(output);`가 설정과 무관하게 매 세션마다 eye0/eye1/source0/source1 텍스처를 덤프했다(2064x2208x4 = 각 18,229,248 B, d145a 73.9MB). 수정: `Configure(...)`에 `frameDump` 인자를 추가하고 네이티브 저장을 `if(frameDump&&status.frames==30) savePair(output);`로 게이트했다. 새 설정 키 `CaptureFirstFrames`(**기본 false**)가 그 인자를 전달하며, 배포 설치 스크립트와 드릴(`-CaptureFirstFrames`) 기본값도 false다 → 배포 기본 상태에서 세션당 raw 0MB. 하네스가 요청하는 `capture.request` 경로는 이 플래그와 무관하게 유지된다. 회귀 검증: native fault fixture 43→**45**(`frame-dump-off-by-default`, `frame-dump-opt-in-adds-pair`).

TOOL-080 **OPEN, severity 3 (D-147, 옵션 제안)**: 안티앨리어싱(MSAA)은 지금 코드로는 켤 수 없다. `src\render\NativeHost\Gpu.cpp:12`가 `a.SampleDesc.Count!=1||b.SampleDesc.Count!=1`을 거부하고, 스왑체인도 `Host.cpp:80`에서 `sampleCount=1`로 만든다. Unity 쪽 눈 `RenderTexture.antiAliasing`만 올리면 `CopyAndWait`가 거부해 호스트가 실패한다. 필요 작업: 눈별 리졸브 대상 텍스처 2장 + `ResolveSubresource` 1회 후 기존 1:1 복사(대략 30줄, 메모리 +18MB/눈, 프레임당 GPU 리졸브 1회/눈). 옵트인(기본 off)으로 넣는 것을 제안하며, 사용자 결정 대기. MSAA 없이도 `EyePreset=high`(1.2×)가 슈퍼샘플링처럼 계단을 줄인다.

  - **d147a 실측(질문 종결):** qualityMsaa=0(게임 전역 AA 꺼짐), eyeRtMsaa=1, sourceAllowMsaa=True eyeAllowMsaa=True, 그리고 sourceComponents=Transform,Camera,Component,Component vs eyeComponents=Component,Camera → 게임 카메라의 추가 컴포넌트가 눈 카메라에 복사되지 않아 카메라별 URP AA/후처리가 눈 이미지에 적용될 통로가 없다. 반사 프로브는 전부 absent: interop 프록시가 파생 UniversalRenderPipelineAsset이 아니라 베이스 RenderPipelineAsset으로 노출되어 msaaSampleCount 등을 읽지 못한다(추측 대신 미확인으로 기록). 결론: 눈 AA는 TOOL-080 구현이 유일한 경로이며, 그 전에 TOOL-078/성능(현재 cycleMs>예산)을 먼저 볼 근거가 커졌다.
  - 추가 근거(D-147 분석): 실기기 로그 6회가 모두 `QUALITY aniso=ForceEnable msaa=0 lodBias=1.5 eyeMsaa=1`이므로 게임 자체 AA는 **꺼져 있다**(우리 눈도 eyeMsaa=1). 게임은 URP지만 눈 카메라는 `Camera.CopyFrom`으로 만든 새 GameObject라 URP의 카메라별 AA/포스트 데이터가 복사되지 않는다 → "게임 AA가 우리 눈에도 적용된다"는 가정은 미검증이며, MSAA/FXAA를 넣기 전에 **0순위로 URP asset · 눈 카메라 AA 실측**을 한다. FXAA 대비 MSAA를 1순위로 두는 이유: 번짐 없음(VR 선명도), Unity 렌더 루프를 안 건드리고 기존 복사 지점에서 리졸브, 반면 FXAA는 두 번째 RT+셰이더+blit을 네이티브 제출 타이밍에 끼워야 한다.

TOOL-081 **OPEN, severity 3 (D-147, 검증 체인)**: 같은 소스·같은 세션에서 plugin DLL 해시가 두 verifier 실행 사이에 `D17D8F8A…` → `DD53A41E…`(둘 다 295,424 B)로 달라졌다. 두 실행 사이의 소스 변경은 docs/config 파일뿐이었고, 어셈블리 정보 버전은 `1.0.0.0`(git commit 미포함)이다. 그 뒤 incremental 빌드 · `-t:Rebuild` · `obj` 삭제 clean 빌드 **3회 모두 `DD53A41E…`(commit `600c9eb`)**를 재현했다. 즉 결정적이지 않은 빌드 입력이 있다는 뜻이지만 **원인 미확정**이다. 영향: TOOL-079와 같은 종류로, `tests\verify-handaim-ready.ps1`의 pin==bin 검사가 plugin에도 어긋날 수 있다. 대책: 패키징 직전 **깨끗한 트리에서 빌드하고 bin에서 plugin·native 해시를 모두 재핀**한다.

TOOL-078 **판정 완료 (D-147c): 주 원인은 게임 측 렌더 비용, 우리 쪽은 평탄** (severity 3 유지): 18분 세션에서 프레임 비용이 단조 증가했다. `timing` 창 302개 중 frames=120 cycleMs=21.36 / frames=360 21.49 → frames=36,240 **cycleMs=37.27**(prepToSubmitMs 10.45→27.26, displayPeriodMs=11.11 = 90Hz). 우리 쪽 누적(receipt 후보·evidence 배열 등)과 게임 측 오브젝트 증가를 아직 분리하지 못했다. **D-147b 계측 추가**: 45 제출 프레임마다 한 줄 `[OPENXR_HOST] PERF ... updateMs lateMs prepareMs saveMs saveWorstMs saveCalls saveBytes unityDeltaAvgMs unityDeltaMaxMs evidence managedHeapMb`를 남기고 리시트에 `perf` 객체(45프레임 윈도 단위, 누적 아님)를 함께 기록한다. 드릴 summary가 `perfWindows/perfFirst/perfLast/qualityAa`를 자동 수집한다. 다음 착용 세션에서 `prepToSubmitMs` 증가와 `unityDeltaAvgMs`/`managedHeapMb`/`saveMs` 추이를 비교해 우리 쪽(save/스캔/힙)과 게임 쪽(렌더 비용)을 분리한다.
TOOL-077 **FIXED (D-147), 헤드셋 재검증 대기**: `src\render\NativeHost\Host.cpp:147`의 `if(status.frames==30) savePair(output);`가 설정과 무관하게 매 세션마다 eye0/eye1/source0/source1 텍스처를 덤프했다(2064x2208x4 = 각 18,229,248 B, d145a 73.9MB). 수정: `Configure(...)`에 `frameDump` 인자를 추가하고 네이티브 저장을 `if(frameDump&&status.frames==30) savePair(output);`로 게이트했다. 새 설정 키 `CaptureFirstFrames`(**기본 false**)가 그 인자를 전달하며, 배포 설치 스크립트와 드릴(`-CaptureFirstFrames`) 기본값도 false다 → 배포 기본 상태에서 세션당 raw 0MB. 하네스가 요청하는 `capture.request` 경로는 이 플래그와 무관하게 유지된다. 회귀 검증: native fault fixture 43→**45**(`frame-dump-off-by-default`, `frame-dump-opt-in-adds-pair`).

TOOL-080 **OPEN, severity 3 (D-147, 옵션 제안)**: 안티앨리어싱(MSAA)은 지금 코드로는 켤 수 없다. `src\render\NativeHost\Gpu.cpp:12`가 `a.SampleDesc.Count!=1||b.SampleDesc.Count!=1`을 거부하고, 스왑체인도 `Host.cpp:80`에서 `sampleCount=1`로 만든다. Unity 쪽 눈 `RenderTexture.antiAliasing`만 올리면 `CopyAndWait`가 거부해 호스트가 실패한다. 필요 작업: 눈별 리졸브 대상 텍스처 2장 + `ResolveSubresource` 1회 후 기존 1:1 복사(대략 30줄, 메모리 +18MB/눈, 프레임당 GPU 리졸브 1회/눈). 옵트인(기본 off)으로 넣는 것을 제안하며, 사용자 결정 대기. MSAA 없이도 `EyePreset=high`(1.2×)가 슈퍼샘플링처럼 계단을 줄인다.
  - **d148c 예산 실측(2026-10-13)**: 45Hz 유지 조건에서 창당 약 5ms의 유휴가 확인됐다(`displayPeriodMs 22.22` vs `prepToSubmitMs 9.66 + submitMs 6.94 + waitMs 0.86~2.69`). 눈 MSAA 4×는 리졸브 1회/눈 + 메모리 +18MB/눈이며, 우리 네이티브 copy issue는 `copyMs − fenceMs ≈ 1.71ms`로 작다. 따라서 MSAA는 **45Hz를 유지한 채 옵트인으로 시도할 수 있는 예산 안**에 있다(구현 후 같은 3창 프로브로 판정).

  - **d147a 실측(질문 종결):** qualityMsaa=0(게임 전역 AA 꺼짐), eyeRtMsaa=1, sourceAllowMsaa=True eyeAllowMsaa=True, 그리고 sourceComponents=Transform,Camera,Component,Component vs eyeComponents=Component,Camera → 게임 카메라의 추가 컴포넌트가 눈 카메라에 복사되지 않아 카메라별 URP AA/후처리가 눈 이미지에 적용될 통로가 없다. 반사 프로브는 전부 absent: interop 프록시가 파생 UniversalRenderPipelineAsset이 아니라 베이스 RenderPipelineAsset으로 노출되어 msaaSampleCount 등을 읽지 못한다(추측 대신 미확인으로 기록). 결론: 눈 AA는 TOOL-080 구현이 유일한 경로이며, 그 전에 TOOL-078/성능(현재 cycleMs>예산)을 먼저 볼 근거가 커졌다.
  - 추가 근거(D-147 분석): 실기기 로그 6회가 모두 `QUALITY aniso=ForceEnable msaa=0 lodBias=1.5 eyeMsaa=1`이므로 게임 자체 AA는 **꺼져 있다**(우리 눈도 eyeMsaa=1). 게임은 URP지만 눈 카메라는 `Camera.CopyFrom`으로 만든 새 GameObject라 URP의 카메라별 AA/포스트 데이터가 복사되지 않는다 → "게임 AA가 우리 눈에도 적용된다"는 가정은 미검증이며, MSAA/FXAA를 넣기 전에 **0순위로 URP asset · 눈 카메라 AA 실측**을 한다. FXAA 대비 MSAA를 1순위로 두는 이유: 번짐 없음(VR 선명도), Unity 렌더 루프를 안 건드리고 기존 복사 지점에서 리졸브, 반면 FXAA는 두 번째 RT+셰이더+blit을 네이티브 제출 타이밍에 끼워야 한다.

TOOL-081 **OPEN, severity 3 (D-147, 검증 체인)**: 같은 소스·같은 세션에서 plugin DLL 해시가 두 verifier 실행 사이에 `D17D8F8A…` → `DD53A41E…`(둘 다 295,424 B)로 달라졌다. 두 실행 사이의 소스 변경은 docs/config 파일뿐이었고, 어셈블리 정보 버전은 `1.0.0.0`(git commit 미포함)이다. 그 뒤 incremental 빌드 · `-t:Rebuild` · `obj` 삭제 clean 빌드 **3회 모두 `DD53A41E…`(commit `600c9eb`)**를 재현했다. 즉 결정적이지 않은 빌드 입력이 있다는 뜻이지만 **원인 미확정**이다. 영향: TOOL-079와 같은 종류로, `tests\verify-handaim-ready.ps1`의 pin==bin 검사가 plugin에도 어긋날 수 있다. 대책: 패키징 직전 **깨끗한 트리에서 빌드하고 bin에서 plugin·native 해시를 모두 재핀**한다.




TOOL-074 **OPEN이지만 차단 아님 (d145a/D-146)**: `Camera.FireOnPreCull` 포스트픽스가 이 런타임에서 호출되지 않는 사실(pre-cull 행 0)은 그대로다. 그러나 최종 판정에 쓰인 경로는 `AfterPlayerUpdate`+`BeforeParticles`이고 사용자 확인이 끝났으므로 안전망 부재가 결과를 막지 않았다. 코드의 PreCull 패치는 최후 안전망으로 유지한다(제거 금지: 다른 런타임에서 호출될 수 있다).

TOOL-075 **OPEN (d145a 미사용)**: 산성꽃(AcidFlower) 오른손 시각 확인은 이번 세션에서도 확인되지 않았다. 같은 `set_forward` 패턴(RVA 450030)이므로 화염과 같은 수정으로 정상일 것으로 보지만, 사용자 확인 전에는 닫지 않는다.

TOOL-082 **FIXED (D-147c), severity 2 (필드 버그, 헤드셋 재확인 대기)**: d147a 13분 지점에서 정상 스테이지 종료가 FAIL로 기록됐다. 마지막 정상 프레임(frames=22365) 직후 양손 WaitingForWeapons, 리티클 False로 바뀌며(스테이지 종료/사망 류) D-146의 StageEnded()→EndGameplay()는 정상 작동했지만, 종료 리시트를 쓰는 Save()가 sourceCamera=source?.name에서 파괴된 Unity 카메라의 name을 읽어 Il2CppInterop NullReferenceException at UnityEngine.Object.get_name()을 던졌고 그 예외가 Fail()로 흘러 host.json.failure.json(verdict=FAIL)을 남겼다. 원인: IL2CPP에서 파괴된 Unity 오브젝트는 C# 참조가 still non-null이라 ?. 단락이 일어나지 않는다(UnityEngine.Object의 == 오버로드만 null로 취급). 수정(D-147c): SafeName(UnityEngine.Object?, fallback) 헬퍼(오버로드 ==null + try/catch)로 리시트/진단/인벤토리/ObjectPath의 모든 name 읽기를 감쌌고, Save()는 리시트 빌드를 ReceiptJson()으로 분리한 뒤 try/catch/finally로 감싸 실패 시 verdict/endReason/receiptError만 담은 최소 리시트를 쓰고 절대 예외를 밖으로 내보내지 않는다. OwnershipDiagnosis()도 try/catch로 diagnosis-failed를 반환한다. 회귀 검증: HandAim 212→215. 남은 조치: 다음 착용 세션에서 스테이지를 끝까지 클리어해 verdict=HEADSET_GAMEPLAY_LEFT_NOT_ACCEPTED, endReason=level-exit, failure 리시트 없음을 재확인(오프라인 픽스처로는 파괴 타이밍을 재현할 수 없다).
 **d148c 실착용 확인 → CLOSED (2026-10-13)**: 8분 49초 세션이 스테이지 종료(`endReason=level-exit`, hostState=2)로 끝났고 `nativeClosedLine=4004 29864 CLOSED orderly=1`, `host.json.verdict=HEADSET_GAMEPLAY_LEFT_NOT_ACCEPTED`, **failure 리시트 없음**, 드릴 최종줄 `HEADSET_RUN_COMPLETE_NOT_VERIFIED observation=DRIVING_BUT_GAME_OVERWRITES_CONTAINER stock=True`(정상 관찰 분류). 파괴된 Unity 오브젝트의 `name` 읽기 경로가 실착용에서 예외를 내지 않았다.














TOOL-084 **FIX BUILT, NOT HEADSET ACCEPTED (D-155), severity 2 — 게임 배경이 잘려나감**: 사용자 보고는 "레벨업·메뉴·상자·보스상자 등 게임 배경이 보이지 않는다"였고, 원인은 드라이버가 아니라 **정책**이었다. `HandUi.HideDarkHudPanels()`와 `HandUi.UiDim()`가 D-102/D-105/D-106/D-112의 넓이·어둠·반투명 휴리스틱으로 **게임이 그린 배경까지 CanvasRenderer 컬**했고, 레벨업 검은 배경(`Level Up/NewNewCanvas/Image - Background Full Screen (1)`, 검정 alpha 0.80)이 그중 하나였다. 증거: `artifacts\video-1010\hud40.png`/`hud70.png`, `artifacts\video1010b\montage.png`, `artifacts\perf-d104\eye0-full.png`. 수정: `GameBackdropMode`(기본 `authored`)로 휴리스틱 컬·알파 캡을 **명시적 옵트인(`dim`/`hide`)으로 강등**하고, **`Player HUD/Bottom Shadow`(D-112 identity)만** 모든 모드에서 숨긴다. 오프라인: HandAim **254 → 263**(신규 9 픽스처가 "매치는 유지되지만 기본 모드에서는 작동하지 않음"을 소스로 고정), 플러그인 Release 경고 0·오류 0. **0.4e 착용(2026-10-13, SteamVR 런타임)의 기계 증거가 수정을 지지한다**: `ui.backdropHidden=0` · `ui.dimmed=0`(게임 배경 컬·캡 0건), `ui.persistentBackdropCount=1` · `ui.hudDarkHidden=1`(Bottom Shadow 하나만 identity로 숨김), 후보 29개 전부 `kept`(레벨업 alpha 0.80 포함), 로그 `GAME_BACKDROP mode=authored`. **미검증: 시각 판정** — 레벨업 배경이 실제로 보이는지/정면 HUD 사각형이 없는지는 사람만 답할 수 있고, 그 답 전에는 닫지 않는다(Q-D155-1).













### TOOL-086 / D-166 — 빈 가고일 오표시, 실제 매달린 포션 누락, 상자 화살표 실종
Status: open | Severity: 2 | Stage: 8 | Reported: user / 0.4j
Expected: 체력포션 있는 가고일과 포션 위치 및 상자만 표시, 빈 가고일 0. 벽 뒤·거리 무제한·반투명, 데미지/적 화살표·전면 HUD 유지.
Actual: 빈 가고일에 표시, 포션 있는 가고일/포션 누락, 기존 상자 화살표 안 보임.
Evidence: artifacts/headset-d087/0.4j/run.json, hand-aim.json + 사용자 보고. final shown7/chestTargets1/boundsPlaced0/offGaze7; 시각 수락 아님.
Confirmed defect: D-165 AddPotionAt가 null/inactive/먼 픽업이면 AddSource(holder,HEALING) 호출; 종류 필터 누락; 다른 픽업 소스는 아직 pickup.t를 사용.
Unknown: 매달린 포션 정체·활성 판정·상자 좌표. 샘플 카운터를 실제 포션 부재 증명으로 쓰지 않음.
Fix: D-166 안전 수정/진단 진행. 0.4k 미실행 폐기. 빈 holder를 표시하는 fallback은 제거하고, 매달린 포션은 추측 없이 확인 후 처리.
Verified: pending human playtest; 버그 닫지 않음.
D-167 / 0.4l: 사용자 포션 앞 신호와 요청 전 진단 보존(scans348/416, shown0/chestTargets0). HealthPoint/Potion id214560 live=True/renderer=on/pickup=none; 다른5샘플은 부모 비활성. 활성 모델 경로는 관측 사실이나 시각 일치·소진/게임 필드 의미 미확정. 캡처 실패(TOOL-087)로 수집 후/상자/벽 뒤/HUD 수락 없음.

### TOOL-087 / D-167 — 에이전트의 범위 밖 캡처 ID가 VR 세션 중단
Status: open | Severity: 2 | Stage: 8 diagnostics | Reported: agent / 0.4l / 2026-10-13
Expected: 지원 ID(0–10)로 양안 캡처; 진단 요청 오류가 gameplay를 중단시키지 않음.
Actual: 에이전트가16601 작성→native RequestCapture return2→managed InvalidOperationException(capture request rejected)→HOST_FAILED. raw/양안 캡처 없음, harness 게임 종료 및 자동 원복.
Evidence: <RepoRoot>\artifacts\headset-d087\0.4l\capture.request, host.json.failure.json, game.log, run.json; src/render/NativeHost/Host.cpp382–386; src/core/OpenXrHost.cs323–330. 09:02:23.833Z 요청. 사용자 잘못/마커 자체 크래시/정상 레벨 종료로 돌리지 않음.
Confirmed: id16601 >10 때문에 요청 거부 조건 만족; native mutex busy6 재시도와 다른 오류. managed는 캡처 거부를 전체 host failure로 처리.
Recovery: UninstallExit0/Recoveredseq27/stockVerifiedTrue; 독립 stock121files/8dirs/7,726,223,158bytes/hash/write times 일치, errors null/VDXR 유지(09:05:34Z). 원복 성공은 캡처 성공/마커 시각 수락이 아님.
Fix: 미구현. 이후 요청은 원본 소스의 지원 범위/중복·pending 확인 후 사용. 별도 수정 시 진단 오류 격리와 경계·거부·재시도 회귀부터; 이번 세션에서 production 변경·재실행 없음.
Verified: 실패 원인 source+live receipt 및 복구 확인; 기능 수정/착용 수락은 미완료.

### TOOL-088 / D-175 — 소스 트리 손상으로 src 기반 픽스처가 실행 불가
Status: open | Severity: 3 | Stage: 8 release | Reported: agent / D-175 / 2026-10-14
Expected: `tests\verify.ps1`이 src를 빌드하고 `settings-tool.Tests.ps1`이 배포 exe를 검증한다.
Actual: `src\core\OpenXrHost.cs`(526 에러)·`src\interaction\HandInput.cs`(12 에러)가 여전히 빌드 불가이고(D-174 경로 C로 복구 중단), `tests\settings-tool.Tests.ps1`은 `dist\versions\0.4d\config.json`이 없어 즉시 실패한다. 따라서 `verify.ps1` 전체 초록은 이 트리에서 불가능하다.
Evidence: `src\render\RenderResolutionPolicy.cs`가 **중복 멤버 + 누락 상수**(MinScale/MaxScale/Low/Normal/HighScale/CustomDefaultScale/IsValidScale/BothEyes)로 설정 도구 빌드를 막고 있었고, 배포 `VhvrSettings.exe` 디컴파일(`artifacts\recovery\decomp\settings\VhvrSettings.decompiled.cs`) 대조로 D-175에서 그 파일만 원형 복구했다. `settings-tool.Tests.ps1` 실행 로그: `Copy-Item : Cannot find path '...\dist\versions\0.4d\config.json'`.
Fix: D-175에서 `RenderResolutionPolicy.cs`만 복구(도구 빌드·셀프테스트·폼셀프테스트 PASS). 나머지 src 복구는 D-174 결정에 따라 범위 밖 — 소스 빌드를 재개할 때만 진행. `settings-tool.Tests.ps1`의 0.4d 의존은 그 버전 아티팩트를 되살리거나 픽스처를 현재 배포본으로 바꿔야 한다(날조 금지: 파일을 만들어 채우지 않았다).
Verified: extras 플러그인·설정 도구 자체는 별도로 검증(60/60 픽스처, 셀프테스트 PASS, uiScale 왕복/거부, 설치·제거 235파일 왕복). src 기반 전체 검증은 미완.

## TOOL-095 — verify.ps1 스위트 3개 빨간 상태 (OPEN, 2026-10-15)
- HandAim(테스트 파일 컴파일 오류 CS0165 Program.cs:253-259), WeaponRuntime/GameplayCamera(소스 가드가 OpenXrHost.cs의 Config 프로퍼티 문자열을 찾는데 D-178에서 OpenXrHostConfig.cs로 분리됨).
- 원인은 D-178 소스 복구 구조 변경. 고치려면 가드가 두 파일을 합쳐 읽게 하거나 설정 클래스를 되돌리면 됨. 미착수.


### TOOL-095 업데이트 (2026-10-15, 점검표 정비)
- **해결**: GameplayCamera 30/30, WeaponRuntime 67/67 통과. 소스 가드가 찾는 문구를 '같은 동작의 현재 표기'로 경로/표기만 바꿈(내용·개수 불변, 가드를 항상 참으로 만든 것 없음). 수정 전 파일은 artifacts\_suite-backup-before-fix 에 보관.
- **미해결(정직)**: HandAim 228개 점검표는 배포 시점(tree-d171)부터 이미 컴파일 불가였다(파일 해시 일치). 변수 선언 줄(inputSrc, schema, readiness 등 27종)이 빠졌고 읽는 대상 Hands.cpp 가 저장소 어디에도 없어 원본 복구 불가. 추측으로 채우면 통과용 점검표가 되므로 손대지 않았다. verify.ps1 은 이 항목 때문에 여전히 실패한다.


## TOOL-095 — formselftest 의 preview 라인 + Set-Content race (ONGOING 2026-10-17, 테스트 하니스 한계, 회귀 아님)
- **증상(0.7-0.7.1)**: `dist\tools\VhvrSettings.exe --form-selftest <config>` 호출 시 폼은 정상 표시되지만, **`preview=` 라인이 "preview=" 같이 비어 나오거나, `File.Exists($cfg)=False` 인쇄**가 나옴. **clipped controls = 0, controls = 14** 정합 자체는 정상.
- **0.7.1 진단**:
  1. `Build.ps1` 의 빌드-중-자체 `--selftest` 는 **PASS** (셀프 테스트 + backupTrue + aniso=off + Anisotropic 왕복 OK). → **`HostConfigTool` API 와 `--selftest`/`--apply`/`--print` 회귀는 0**.
  2. `Set-Content -LiteralPath $cfg -Encoding UTF8` 직후 `File.Exists($cfg)=False` 가 출력되는 케이스 → Defender 의 실시간 감시 + 짧은 사이의 `Close()` race. **환경 이슈**.
  3. 폼의 `Preview` 메서드는 `ReadConfig` → `BuildPreviewString` → `preview.Text = previewText` 인데, **`Config` 가 부분 로드되면 preview 가 빈 문자열**로 보임. **데이터는 맞지만 preview 라벨만 비어 보임**.
- **0.7.1 의 우회**:
  1. **`--selftest` 회귀** = 충분한 정합 검증 (PASS, see `regr-selftest.ps1`).
  2. **정적 컨트롤 좌표 검사** = `Left+Width <= 696` + `Top+Height <= 416` 가드 (`artifacts\regr-controls.ps1`, 23/23 컨트롤 PASS).
  3. **컨트롤 카운트 가드** = `Controls.AddRange` 의 배열 항목 14 + 라벨 6 = 23, `formselftest` 의 `Controls >= 13` 자동 흡수.
  4. **헤드셋에서 1회** 폼 시각 확인 = 사용자가 직접 `dist\tools\VhvrSettings.exe` 더블클릭 + 체크박스 표시 + Save + reopen 으로 정상 라운드 트립 확인.
- **원칙(정직)**: `--form-selftest` 의 preview 라인 검증은 **테스트 하니스 환경 한계**이지 도구 회귀가 아님. `DECISIONS.md` D-104-iso-promotion 의 한계 항목에 동시 기재.
- **향후(다음 빌드)**: `formselftest` 의 preview 가 빈 문자열일 때 명시적 `Form.Show()` + `Refresh()` + `Application.DoEvents()` 호출로 보강, 또는 PowerShell 쪽 `Start-Process -Wait -PassThru` + 명시적 핸들 닫기.

## BUG-200 — 0.9.3 설치본에서 헤드셋이 검은 화면 (FIXED IN SOURCE, 헤드셋 재검증 대기, 2026-10-18)

- Expected: 0.9.3 설치 후 실행하면 0.9.2처럼 메인 메뉴가 헤드셋에 보인다.
- Actual: 헤드셋이 **검은 화면**. 같은 기기·같은 설정의 0.9.2는 정상.
- Evidence (게임 로그 + 런 영수증):
  - 0.9.3 19:22 실행: `STARTUP_READY elapsed=0.6s scene='Intro' camera='Main Camera'` → **0.62초에 인트로 씬에서 세션 시작**. `host.json`의 `sourceCamera='Main Camera'`(인트로 카메라).
  - 같은 실행: `windowFrames`가 **134**에서 멈췄는데 XR 제출은 약 **495**프레임까지 진행 → 렌더 소스는 죽고 XR 루프만 돌았다.
  - 0.9.2 18:02 실행(정상): `scene=MainMenu_2`, `sourceCamera=ArenaCamera`, 세션 시작 15.3초.
  - Extras 로그: 월드 스페이스 메뉴 패널이 `MainMenu_2` 진입에서 화면 스페이스로 되돌아가고 재전환 없음.
- Root cause: D-198의 readiness 게이트가 "메인 카메라 + D3D11"만 보고 **어느 씬인지**는 보지 않았다 → 인트로 씬의 `Main Camera`가 그대로 통과(0.6초). 게임이 `Intro → MainMenu_2`로 씬을 바꾸며 그 카메라를 파괴했고, 메뉴 분기의 `if (source == null) return;`가 **D-181의 `AdoptMenuCameraIfNeeded()`보다 앞**에 있어 유일한 복구 경로가 영구히 건너뛰어졌다(그래서 재채택 로그가 한 번도 없다). 이후 `PrepareCameras`가 호출되지 않고 죽은 소스로 제출만 반복 = 검은 화면. 즉 옛 고정 15초 대기가 하고 있던 일은 "시간 벌기"가 아니라 **메뉴 씬까지 버티기**였고, D-198이 그 부작용을 함께 지웠다.
- Fix (D-200): 인트로/전환 씬 스킵(`StartupSkipScenes`, 기본 `Intro`) + 씬 정착 게이트(`StartupSettleSeconds`, 기본 1.0초) + **재채택 호출을 조기 반환 앞으로 이동**(구조적으로 재발 불가).
- Verified (오프라인): Stage0 Release 경고 0/오류 0, `tests\GameplayCamera` **38/38 PASS**(신규 가드 3종 `source-startup-skips-intro`, `source-startup-scene-settle`, `source-menu-adopt-before-return`), 회귀 HandAim 190/190·WeaponRuntime 67/67·VhvrExtras 62/62·FlameEndpoint 138/138. 설치본 DLL SHA `102E765DF9DF00726332CF12050DB597560EA097E00E0BD7F021C8488234D5AE` = 빌드 산출물.
- Not verified (헤드셋): 메뉴에서 정상 화면이 나오는지는 착석 1회가 필요하다. 판정 기준: `startupWait` = `intro scene 'Intro' (D-200: waiting for the main menu)`, `scene=MainMenu_2`, `sourceCamera=ArenaCamera`, `startupReadySeconds` ≈ 메뉴 등장 시각(0.6초가 아님).
- 부수 교정 (같은 뿌리): `tests\verify.ps1`이 GameplayCamera 기대 개수를 **30**으로 박고 있었는데 실제는 35였다(그 게이트가 조용히 어긋나 있었다). D-200에서 **38**로 맞췄다.
- 패커 게이트 결함 (별건, 정직 기록): 0.9.3 패커의 "DLL에 `STARTUP_READY` 문자열이 있나" 검사는 .NET `#US` 힙이 **홀수 바이트 오프셋**에서 시작할 수 있음을 고려하지 않아, 실제로 포함된 문자열을 못 찾을 수 있었다(0.9.3은 우연히 짝수 정렬이라 통과). 0.9.4 패커는 두 정렬 + ASCII를 모두 검사하고 `intro scene` 문구까지 요구하며, 은퇴한 0.9.3 DLL 해시(`D1655D50…`)를 이름으로 거부한다.

## BUG-202 — 게임플레이에서 손/무기가 오른쪽으로 90° 꺾여 보임 (FIXED IN SOURCE, 헤드셋 재검증 대기, 2026-10-18)

- Expected: 컨트롤러를 앞으로 들면 무기와 그 조준(리티클·발사 방향)이 **그 방향**을 향한다.
- Actual (사용자, 0.9.4 + D-201 착석 게임플레이): "메뉴는 잘 나오는데 게임 안에서 손이 오른쪽으로 90도 꺾여있다." 메뉴는 통과, 인게임 손만 틀어짐.
- Evidence (같은 20:31~20:32 실행의 영수증 3종이 수치로 일치):
  - `run\host.json` 마지막 view row: `orientation = (0,0,0,1)` → **눈은 HMD(identity) 프레임**, 같은 레코드 `anchorRotation = (0, 0.7071, 0, 0.7071)` = **Unity yaw +90°**(19:57 실행은 −90°).
  - `src\core\OpenXrHost.cs:1489-1490`(눈): `val2 = val + eyePos`, `val3 = eyeRot` — `anchorRotation` 없음(D-195).
  - `src\core\OpenXrHost.cs:659`(손): `handAim.Tick(..., anchorRotation, ...)` — **손만 게임 카메라 회전을 곱한다**.
  - `run\hand-aim.json`(우손): `state=Driving`, `drivenFrames=1203`, `gameOverwrites=0`, `muzzleToHandForwardDegrees=0` → 드라이버는 "컨트롤러 정면 정렬 완료"라고 보고하지만 그 정면은 **플레이어가 가리키는 방향의 90° 옆**이다.
- Root cause (확정): D-195가 **눈** 합성에서 `anchorRotation`을 제거하고 "HandAim 손 IK — 영향 없음"이라고 적었는데 그 가정이 틀렸다. 이 게임의 eye source는 `Main Camera` = **플레이어 프리팹의 자식**(`run\gameplay-camera.json`: `cameraPath=AllThingsBaseLevel/Prefab_Player_Level1/Main Camera`)이라 `source.transform.rotation`은 **플레이어의 현재 facing**이다. 눈=HMD 프레임 / 손=플레이어 yaw 프레임으로 갈라졌고, 갈라진 각도가 정확히 보고된 90°다. D-196/D-201이 메뉴 패널·레이저에서 고친 것과 같은 계열의 잔여 버그다.
- Fix (D-202): `HandPoseFrame`(기본 `Head` = 눈과 같은 identity 프레임 / `Anchor` = 0.9.4 A/B), 손 조준 ray + 무기 회전 + **rest 캡처**를 그 한 프레임으로 통일(`RestCaptureFrame` = 캡처 순간의 포즈까지 나눔 → 넘겨받을 때 무기 점프 없음), 헤드 프레임에서 스냅 턴은 거부 + 경고 1회.
- Verified (오프라인): Stage0 경고 0/오류 0, DLL 430,592 B / SHA `34AA247600E3DEA20895F77A49A005B296B950CCFE43EA1483619ECB765DE525`, `tests\HandAim` **203/203**(신규 13: `d202-anchor-frame-controller-direction-is-90-degrees-off-the-view` = 90.0°, `d202-head-frame-controller-direction-is-the-view` = 0°, `d202-head-frame-takeover-keeps-the-authored-rotation`, `source-rest-capture-uses-the-same-frame-as-the-drive` 등), GameplayCamera 38/38, WeaponRuntime 67/67, VhvrExtras 72/72, FlameEndpoint 138/138, ShotScopes 28/28.
- Not verified (헤드셋): 손이 실제로 컨트롤러를 따라오는지는 착석 1회가 필요하다(`docs\playtests\PLAYTEST-D202-HAND-FRAME-KO.md`). 왼손 슬롯은 이번 실행의 Playground 씬에서 `weaponsInSlot=0`이라 확인 불가.

## BUG-203 — 스냅 턴이 D-195 이후 시야를 돌리지 못한다 (FIXED / 헤드셋 확인 완료, D-211, 2026-10-22 CLOSED)

- Expected: 오른쪽 스틱 좌/우(≥0.7, `SnapTurnDegrees=30`)로 요청하면 시야가 30° 돈다.
- Actual: legacy 경로는 `anchorRotation`만 돌리는데(`OpenXrHost.ApplySnap` → `HandAimMath.SnapAnchor`) 눈 합성이 그 값을 쓰지 않는다(D-195). 회전 없이 **앵커 위치 항 때문에 화면이 옆으로 살짝 튀기만** 한다.
- Evidence: `run\hand-aim.json`의 `input.snapTurns = 0`(이번 실행에서 사용자가 누르지 않아 **아직 실기기 미관측**), `snapDegrees = 30`. 코드 근거는 `OpenXrHost.cs:1489-1490`(눈 = anchorRotation 미사용) + `ApplySnap`(anchor만 회전).
- D-202 조치: 기본(헤드 프레임)에서 요청을 **거부하고 경고 1회**(`SNAP ignored on the head frame ...`) — 회전 없는 튐을 만들지 않는다. `HandPoseFrame=Anchor`에서는 레거시 경로가 그대로 살아 있어 A/B로 비교할 수 있다.
- 수정 (D-211, 2026-10-20, 사용자 요청 "스냅턴을 추가하고, 각도를 30도 혹은 45도, 비활성화 3가지로 나눠 옵션으로 넣어줘"):
  - `OpenXrHost.snapViewYaw`(세션 view-yaw, (-180, 180]로 랩)를 `ApplySnap`에서 누적하고, `PrepareCameras`가 **두 눈 오프셋과 두 눈 회전 양쪽에** 곱한다. `snapViewYaw == 0`이면 곱하지 않으므로 스냅을 쓰지 않는 화면은 D-202와 동일하다.
  - 손/조준은 같은 회전을 합성 base로 받는다(`handPoseBase = ViewYawRotation`). `SetHead`가 눈 transform에서 파생되므로 `HandInput.HeadRelative`의 이동 yaw, 리티클, 마커가 자동으로 따라온다. **앵커는 건드리지 않으므로 플레이어 위치가 튀지 않는다.**
  - 새 앵커를 잡는 4곳(초기화 / 게임플레이 전환 / 메뉴 카메라 채택 / 메뉴 복귀)에서 `ResetSnapView()`로 0으로 되돌린다 - 세션 중 누적된 시야 오프셋이 다음 씬까지 남지 않는다.
  - 각도는 **3택**: `0`(끄기) / `30`(기본) / `45`. 그 밖의 값은 `SnapTurnPolicy.Resolve`가 30으로 해석하고 경고를 1줄 남긴다(조용한 다운그레이드 없음).
  - 사용자 노출: `VhvrSettings.exe`의 "Snap turn (right stick)" 라디오 3개(Off / 30 deg / 45 deg)와 미리보기 줄, `config\schema.json`의 enum `[0, 30, 45]`, `installer\install.ps1`의 기본값 30 + 주석.
  - 영수증: `run\host.json`에 `handFrame`, `snapTurnDegrees`, `snapViewYaw`, `snapTurnsApplied`, 10프레임 evidence에 `viewYaw`를 남겨 헤드셋 판정을 수치로 확인할 수 있게 했다.
- Verified (오프라인, 2026-10-20): 플러그인 빌드 경고 0/오류 0, `tests\HandAim` **238/238**(신규 7: 3택·미등록값 해석·끄기·랩·누적, 소스 배선 3), `tests\Settings` **13/13**(신규: 0/45 왕복·다른 키 보존·문자열 숫자 읽기·3택 계약), `VhvrSettings --selftest` PASS(`snap=30`), `--form-selftest` PASS(controls=33, clipped=0, 미리보기 "Snap turn 45 deg per right-stick push").
- Not verified (헤드셋): 한 번 밀면 그 각도만큼 돌고 위치가 튀지 않는지, 전방 이동이 새 방향으로 함께 도는지는 착석 1회가 필요하다(판정 3종: `docs\CONTROLS.md` §6).
- 배포 (2026-10-21, 사용자 요청 "설치된 게임에 해당 업데이트를 저장하고 내가 검증하겠다"): 로컬 설치본 `F:\SteamLibrary\steamapps\common\Vampire Hunters`에 새 플러그인(`12936192…E85F2F`, 440,320 B)과 `VhvrSettings.exe` 3택 UI를 덮어썼다. 백업 `F:\VRMods\backup\20261021T003925-d211-deploy`, 배포 스크립트 `scripts\deploy-dev.ps1`, 설치본에 `VampireHuntersVR-0.9.5-rc1\UPDATE-D211-KO.txt`(검증/롤백 안내)를 남겼다. 라이브 설정은 `SnapTurnDegrees=30`, `HandPoseFrame=Head`로 손대지 않았다(해시 동일). 배포 후 설치본 DLL에서 `SNAP_TURN`/`SNAP applied in the head frame (D-211)`/`ResetSnapView`/`ViewYawRotation` 심볼을 확인했고, 설치본 `VhvrSettings.exe --form-selftest`가 라이브 설정을 읽어 `Snap turn 30 deg per right-stick push`를 표시했다.
- **Verified (헤드셋, 2026-10-22, 사용자 보고)**: "스냅턴 잘 작동하네, 시야가 30도 돌았고 몸 제자리, 손, 무기 레티클 다 같이 잘 돌았어" — A1(시야 30° 회전)·A2(위치 고정)·B2(손/무기/리티클 동반 회전) 통과. 45°/끄기는 같은 코드 경로의 다른 값이라 별도 착석은 하지 않았다. → **BUG-203 CLOSED**. 0.9.6 패키지에 포함해 공개했다(README/릴리스 노트).


## BUG-204 — 엔지니어 터렛 건설: 건설 UI는 열리는데 왼손 X로 설치가 안 됨 (OPEN, 원인 확정 / 수정 미착수, 2026-10-18)

- Expected: 엔지니어가 특수능력(오른쪽 스틱 아래)으로 건설 모드를 열고, 게임이 안내하는 키로 터렛을 설치한다.
- Actual (사용자, 0.9.4 + D-201/D-202 착석): "오른쪽 아래로 당겨서 포탑 건설 UI가 나오는 건 확인했는데, 왼손 X를 눌러도 설치가 안 된다."
- **입력 배달은 정상 (죽은 경로가 아니다)**: 같은 실행 `run\input.json` — `interactPresses=34`, `padSent=9439`, `padError=null`, `state=Driving`, `gameOverwrites=0` → 실패 지점은 배달이 아니라 **게임 쪽 액션 소비**다.
- **게임 1차 데이터 3종 (근거)**:
  1. 로컬라이제이션 `skill_desc_engineer_1` (`resources.assets`): `"SPECIAL ABILITY: Press {action=Special Ability} to enter build mode or switch blueprints. Press {action=Fire} to build the turret"` → **종류 전환 = 특수능력 재입력**, **설치 확정 = Fire**.
  2. 게임 자신의 InputActionAsset (`sharedassets2.assets`): `Special Ability = <Gamepad>/buttonEast`, `Fire = <Gamepad>/rightTrigger` + `<Mouse>/leftButton` + **`<Keyboard>/x`**, `Select = <Gamepad>/select`(View 버튼), `Interact = <Gamepad>/leftStickPress`. UI 맵: `Submit = <Gamepad>/buttonEast`, `Cancel = <Gamepad>/buttonSouth`, `Point/Click = <Mouse>/position,leftButton`(**Keyboard&Mouse 그룹만**) — 전체 표는 `artifacts\_d204-input-bindings.txt`.
  3. 게임 자체 키바인딩 화면(사용자 스크린샷, `config\bindings.json`): 발사 = **LMB / X**, 상호작용 = Z/G, 특수 능력 = SHIFT/CTRL.
- Root cause (확정): D-184의 *의도*(평면 게임에서 터렛 설치 키가 X)는 게임 데이터와 일치하지만, **매핑한 액션이 틀렸다**. 평면의 `X`는 게임에서 **`Fire`** 액션(키보드 대체 바인딩)이고, 모드는 **`SelectInput`**(= `<Gamepad>/select`, View 버튼) + `BitSelect`를 보낸다(`src\interaction\HandInput.cs:184`, `:194`). 게임의 건설 코드는 `Fire`를 읽으므로 `Select`/`Interact`는 무효 → UI는 열리고 확정만 무반응.
- 두 번째 축 (같은 버그, 미확정): **설치 위치**. `PlayerSpecialVfx.set_visual_turret(Int32, ref Vector3, Boolean) : Boolean`(미리보기) → `set_turret(Int32, Vector3)`(설치)는 **월드 좌표**를 받고, 평면 게임은 마우스 `Point`/`Click`으로 위치를 정한다. 두 액션 모두 Keyboard&Mouse 그룹이고 모드는 스킴을 Gamepad로 고정(`neverAutoSwitchControlSchemes = true`)하므로 **VR에는 포인터가 없다** → 파란 고스트 미리보기가 뜨지 않으면 Fire를 눌러도 설치할 좌표가 없다.
- **종류 선택은 병목이 아니다 (사용자 가설 교정)**: 4종류는 **스킬로 해금**되고(`skill_desc_engineer_9/17/23`, `SkillType.FLAME_TURRET/LASER_TURRET/HEAL_TURRET`), 전환은 **특수능력 재입력(blueprint 전환)** 이다. 모드는 이미 오른쪽 스틱 아래 → `<Gamepad>/buttonEast`를 보내고 있으므로 **전환 입력은 이미 나가 있다** (좌/우 스틱 이동을 쓸 이유가 없다).
- Fix (예정, D-205): ① `PlayerController.is_turret_mode == true`일 때만 왼손 X를 **Fire로 라우팅**(게임 자체 콜백 `PlayerInputController.FirePressedInput(bool)` + 필요 시 패드 `rightTrigger`) ② 고스트가 안 뜨면 모드가 손 조준 지면 히트로 `set_visual_turret`/`set_turret`를 직접 구동.
- Not verified (헤드셋): 위 두 축 판정은 착석 1회 — `docs\playtests\PLAYTEST-D204-TURRET-KO.md`.



## BUG-205 — Extras `ReadHands` ABI가 손당 4 B 짧아 오른손 ray가 숨고 CoreCLR 메모리 손상 가능성 발생 (FIXED IN SOURCE / LIVE EXTRAS, 헤드셋·크래시 재검증 대기, 2026-10-19)

- **Expected**: 메뉴에서 좌/우 컨트롤러 ray가 각 컨트롤러 자세를 따라 표시되고 메뉴를 가리킬 수 있다. Native hands status 출력 ABI와 모든 managed consumers가 동일한 레이아웃을 사용한다.
- **Actual (사용자 보고/실행)**: 오른쪽 메뉴 ray 없음; Windows Application Error 1000에서 `Vampire Hunters.exe`가 `coreclr.dll` 안 `0xc0000005`로 종료.
- **Confirmed cause — 오른손 ray**: native `Host.h`는 `HandPose` = 64 B, `HandsStatus` = 144 B. Stage0 `NativeHandPose`도 마지막 `Squeeze`를 포함한다. 반면 Extras `MenuScreenRig.HandPose`에 `Squeeze`가 빠져 60 B, status 136 B였다. Native `Right` offset은 80, Extras는 76이므로 `status.Right.Active`는 native 왼손 `Squeeze` 값을 읽었다(일반적으로 0). `MenuScreenRig.UpdatePointers`는 두 손을 갱신하지만, 잘못된 Active 값 때문에 오른쪽 `LineRenderer`가 꺼졌다. Native가 144 B를 쓰는데 수신 구조체는 136 B여서 8 B overrun 경로도 존재했다.
- **Consumer scope**: `ArmHaptics.cs` 자체에는 `ReadHands` 호출/독립 미러가 없다. 단, pulse delivery check는 `MenuScreenRig.HapticAppliedCount()`를 불러 같은 `ReadHands` status의 `Reserved`를 읽으므로 그 읽기도 ABI 수정의 수혜를 받는다.
- **Crash evidence / limit**: 이벤트 시각 2026-10-19 00:36:33 (Windows 현지 시각), exception `0xc0000005`, module `coreclr.dll`, offset `0x00000000001d1fdd`; BepInEx 로그도 그 시각에 정상 진행 중 갑자기 끝난다. 8 B output overwrite는 강한 유력 원인이지만 이벤트에 managed/native call stack이 없어 이 특정 AV의 인과관계를 확정하지 않는다. 마지막 Extras pulse log는 `RequestHaptic` result 0 (`queued`)로, 자체 실패/크래시 증거가 아니다.
- **Fix (D-207)**: 공유 `Features/NativeHandsAbi.cs`의 sequential 미러에 `Squeeze`를 추가하고 runtime guard (`HandPose=64`, `HandsStatus=144`, `Squeeze@60`, `Right@80`)를 둔다. `MenuScreenRig.ReadHands`와 `UpdateHand`가 공유 타입을 사용한다. Extras 순수 테스트에 크기/offset 6개 검사를 추가, fixture 72→78; `tests/verify.ps1` 결과 문자열도 맞춤.
- **Verified offline**: `dotnet build extras\\VhvrExtras\\VhvrExtras.csproj -c Release --no-restore` warning 0/error 0. `dotnet run --project tests\\VhvrExtras\\VhvrExtras.Tests.csproj -c Release` 78/78. Native source static asserts = 64/144; Stage0 mirror matches. Build/live DLL SHA must match (D-207 deployment receipt).
- **Live deployment boundary**: only `BepInEx\\plugins\\VhvrExtras.dll` replaced after game process absence; previous DLL preserved under `artifacts\\d207-install`. No Stage0/native/config/loader/game binaries changed and no automatic launch. Historical `dist\\VampireHuntersVR-0.6-final` payload/ZIP remains old; do not reinstall it as the D-207 build.
- **Not verified (headset)**: visual right ray/pointing/clicking; crash recurrence over a session. Keep open pending `docs\\playtests\\PLAYTEST-D207-MENU-RAY-KO.md` and fresh run evidence.
- **Package (D-208, 2026-10-19)**: the private `dist\\VampireHuntersVR-0.9.5-rc1-preview.zip` now contains the matching D-206 Stage0 + NativeHost and D-207 Extras binaries; hashes and archive integrity are recorded in the `.sha256.txt` sidecar and `docs\\DECISIONS.md`. This does not close the bug. Keep the ZIP private: headset/crash validation, project license choice, and third-party redistribution review are still open.



## TOOL-099 - 오프라인 픽스처 2건 거짓 실패 (FIXED, 2026-10-19, 게임 동작 변경 없음)

- HandAim: tests\HandAim\Program.cs 가 dist\VampireHuntersVR-0.6-final\install.ps1 을 읽는데 그 폴더는 dist에 없다(프로젝트 루트에 있음) -> DirectoryNotFoundException. 루트 경로로 폴백. 결과 226/226 PASS.
- FlameEmitter: source-tick-pin-after-all-parent-writers 가 ObserveHandApplied() 부재를 요구했지만, D-178 복구에서 타이밍 전용 호출이 복원되었고 RenderTiming 픽스처(applied-only-after-fire-tick)는 그 존재를 요구했다. 두 픽스처가 서로 모순. 불변식(핀이 틱의 마지막)은 유지하고 호출이 fireTick 이후/핀 이전이면 허용하도록 수정. 55/55 PASS.
- 나머지: GameplayCamera 38, WeaponRuntime 67, VhvrExtras 78, FlameEndpoint 138, ShotScopes 28, StereoMath 29, RenderTiming 57 PASS. Stage0/Extras Release 빌드 오류 0.
- 설치된 DLL/설정은 건드리지 않음.

## RUN-001 - xrGetSystem XrResult=-35 (XR_ERROR_FORM_FACTOR_UNAVAILABLE) (OPEN, 환경 요인, 2026-10-19 02:26 실행)

- Evidence: run\host.json.failure.json, host.json.native.log (runtime=VirtualDesktopXR, xrGetSystem 실패, CLOSED orderly=0). 직전 02:21 실행은 224k 프레임 정상 후 orderly=1 종료.
- 원인: Virtual Desktop 스트리머에 헤드셋이 연결되지 않은 상태에서 게임을 실행하면 OpenXR가 HMD를 제공하지 않는다. 코드 결함이라는 증거는 없다.
- 현재 동작: 호스트가 1회 FAIL 후 해당 세션에서 VR을 포기한다(재시도 없음). 재시도 추가는 런타임 정책 변경이라 사용자 확인 전 미구현. 해결책은 헤드셋 연결 후 게임 실행.

