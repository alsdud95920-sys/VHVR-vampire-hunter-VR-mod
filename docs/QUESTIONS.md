# Questions

## D-207 / 0.9.5-rc1 — 메뉴 ray, 안정성, 게시 권한 (2026-10-19)
- **Q-D207-HEADSET / OPEN (착석 필요)**: 수정된 64/144 B ABI 뒤 메뉴에서 오른쪽 ray가 컨트롤러를 따라 보이고 버튼을 누르는가? `docs\\playtests\\PLAYTEST-D207-MENU-RAY-KO.md` 기준으로 확인. 자동 fixture/`PATCH_APPLIED`만으로 시각 PASS 처리하지 않는다.
- **Q-D207-CRASH / OPEN (세션 관찰 필요)**: ABI 수정 뒤 `Vampire Hunters.exe`가 `coreclr.dll` `0xc0000005`로 다시 종료되는가? 8 B native write overrun은 강한 후보이나 현재 이벤트에는 스택이 없어 인과관계 미확정. 재발 시 Application Error 1000 시각과 `BepInEx\\LogOutput.log` 마지막 구간 보존.
- **Q-D207-PACKAGE / ANSWERED (local RC only)**: `dist\\VampireHuntersVR-0.9.5-rc1-preview.zip` carries Stage0/NativeHost/Extras from the verified D-206/D-207 installed builds (SHA pinned in its sidecar). Historical 0.6-final and 0.9.4 archives remain unchanged. This answers artifact assembly only; not headset acceptance or permission to distribute.
- **Q-D207-PUBLISH / PARTIALLY ANSWERED by D-209**: the user selected GPL-3.0-or-later for first-party source. Exact third-party redistribution obligations and notices remain unaudited; do not upload or redistribute either package or source copy until that review is complete.
- **Q-RC-HEADSET / OPEN**: D-202 hand pose/snap-turn limitation and D-206 controller/turret actions still need their headset playtests; package/fixtures cannot close these gates.


## D-201 — 메뉴 화면/레이저를 HMD 정면 프레임으로 (0.9.5 후보, 2026-10-18)
- **Q-D201-HEADSET / OPEN (착석 필요)**: 메뉴 패널이 **정면 중앙**에 있고 레이저가 **컨트롤러가 가리키는 방향**을 향하는가? 판정: 로그 `menu ui: panel frozen at world (...) headYaw=.. panelYaw=.. headPanelDelta=0.0deg`, 헤드셋 육안.
- **Q-D201-RECENTER / OPEN**: "양쪽 스틱 동시 클릭" 리센터가 그 게임 메뉴 조작과 충돌하지 않는가(스틱 클릭이 다른 기능에 쓰이면 다른 조합으로 바꾼다 — 코드 1줄).
- **Q-D201-FRAME / OPEN (A/B 1회)**: `[MenuScreen] PointerHeadFrame=false`(0.9.4 동작)와 `true`(수정본) 중 어느 쪽이 VR에서 자연스러운가. SteamVR에서 다르게 보이면 그 값을 실험한다(D-197과 별개 축).
- **Q-D201-WORLD / OPEN**: 패널은 HMD 프레임으로 정렬되지만 **게임 월드(아레나 배경)** 는 여전히 HMD 프레임이다(게임 카메라 기준 90° 회전된 채 보일 수 있음). 사용자가 배경의 방향까지 게임 기준으로 맞추길 원하면 그때 눈 합성에 anchor를 넣는 대안(D-201에서 기각: SteamVR 뒤집힘 재발 위험)을 다시 검토한다.
- **Q-D201-GATE / OPEN (user)**: `tests\verify.ps1`이 `tests\Transaction.Tests.ps1`(160~194행 유실, ENV-012) 때문에 65행에서 중단된다. (a) 유실 구간 원본 복원을 먼저 할지, (b) 집계기가 실패를 기록하고 **계속 진행**하도록 바꿀지 결정 필요. 현재 상태로도 exit 1로 실패하므로 "조용한 통과"는 아니지만, 이후 게이트가 실행되지 않는 것은 사실이다.

## D-200 — VR 시작을 인트로 이후로 (0.9.4)
- **Q-D196-OPENXRHOST-WAIT / ANSWERED BY RECEIPT (2026-10-18)**: 그 질문의 판정 기준은 "`startupReadySeconds`가 1~3초면 정상, 10초 이상이면 게임 로딩 병목"이었다. 실제 0.9.3 영수증은 **0.59초**였고 화면은 검은색이었다 → 그 지표 **하나로는 정상을 판정할 수 없다**. **어느 씬에서 통과했는지(`scene`)** 가 함께 필요하다. D-200이 `scene`·`startupWait`·`STARTUP_SKIP`를 함께 남기도록 고쳤다.
- **Q-D200-HEADSET / OPEN (착석 필요)**: 0.9.4에서 인트로 동안 VR이 시작되지 않고(그 구간은 평면 화면), 메인 메뉴가 뜬 뒤 헤드셋에 정상 화면이 나오는가? 판정: `run\host.json`의 `startupWait` = `intro scene 'Intro' (D-200: waiting for the main menu)`, `scene=MainMenu_2`, `sourceCamera=ArenaCamera`, `startupReadySeconds` ≈ 메뉴 등장 시각.
- **Q-D200-SETTLE / OPEN**: `StartupSettleSeconds=1.0`이 이 게임에 충분한가(1초 미만으로 스쳐 지나가는 전환 씬이 있는가). 부족하면 값을 올린다(허용 0..10).
- **Q-D200-INTRO-NAME / OPEN**: 스킵해야 할 인트로/스플래시 씬 이름이 정말 `Intro` 하나인가(게임 업데이트로 바뀔 여지). 이름이 바뀌면 정착 게이트가 2차 방어로만 남는다.
- **Q-D200-CONFIG-RESET / OPEN (user)**: 릴리스 `install.ps1`이 재설치 시 설정 파일을 템플릿으로 덮어쓰는 동작을 "사용자 편집값 자동 백업 후 복원"으로 바꿀지 결정 필요(현재는 수동 백업).

## D-194 — 메인 메뉴 VR 시작 버그
- **Q-D194-HEADSET / OPEN**: 설정이 `MenuFirst=true`로 복원된 재설치본에서 HMD가 `MainMenu_2`를 보여주는가? 메뉴 표시와 stage-start handoff를 각각 확인; 로그상 PATCH_APPLIED만으로 PASS 처리하지 않음.
- **Q-D194-HANDOFF / OPEN**: stage 선택 후 실제 gameplay camera 획득, `hand-aim.json` 생성 및 hand integration 설치가 되는가? 실패 시 `host.json.failure.json`과 BepInEx startup log 수집.
- **Q-D194-CONFIG-BACKUP / OPEN**: user-edited config values를 backup하여 되돌릴 수 있는지 설치 전에 확인; manifest 변경 파일 삭제 금지.

## D-193 — 0.9-final flame effect
- **Q-D193-MAINMENU / ANSWERED BY LOG**: 이전 run은 `WAITING_FOR_GAMEPLAY_CAMERA` at `MainMenu_2`, candidates empty. Release regression came from `MenuFirst=false`; D-194 restores true.


## D-193 — 0.9-final 화염 이펙트 복구
- **Q-D193-FLAME / HEADSET**: 게임플레이에서 flamethrower firing stream이 총구/손을 따르는가? HMD만 회전할 때 head-follow처럼 끌리는가? 기존 로직상 문제는 입자 전 `HoldHandsBeforeParticles` 호출 누락이었고 이제 Stage0 `PlayerController.Update` 후처리에 연결됨. Receipt의 `beforeParticlesCalls`와 `flameEmitterPin.stats.beforeParticles.samples`로 호출 여부 확인; 카운터 증가만으로 시각 PASS를 주장하지 않음.
- **Q-D193-OTHER-HEADSETS / DEFERRED BY USER**: Quest 외 기기 지원 조사/구현은 다음 작업으로 미룸.

## D-175 — 5기능 구현 완료(패치 플러그인), 남은 것은 착석 판정 — 2026-10-14
- Q-D174-1 **ANSWERED / agent(구현, 착석으로 확정 필요):** 좌 `Lower`(Y)=특수(Special), 우 `Lower`(B)=블러디 러시로 확정해 구현. 반대라는 관찰이 나오면 cfg `AbilityFace`만 뒤집으면 된다(코드 수정 없음).
- Q-D174-2 **ANSWERED / agent(구현):** 0.5초 재발사는 **느린 무기만**으로 좁혔다 — `Weapon.attributes.base_fire_rate <= RetriggerMaxFireRate`(기본 2/s). 연사 무기는 게임 케이던스 그대로. 착석에서 체감 확인 필요.
- Q-D174-3 **ANSWERED / agent(구현):** HUD **전체** 1.5배(요청 문구 그대로) — `HandAimDriver.UiScan`이 만든 각 `UiCanvas.Width`에 배율 적용. 배율은 설정 도구의 `uiScale`(0.5~3.0)로 바꿀 수 있다.
- Q-D174-4 **ANSWERED / agent(구현):** 패널 1.5 m·40°(가로) 헤드락, 검은 배경(눈 카메라 검정 + 패널 레이어만 마스킹), 높이 고정(리센터링이 담당), 클릭 = 트리거(라이저→게임 EventSystem 포인터 이벤트). D-172 "검은 패널" 원인(overlay 캔버스를 패널 카메라로 재지정하지 않음)은 코드로 방지.
- Q-D174-5 **ANSWERED / agent(구현):** 기존 `dist\VampireHuntersVR-0.6-final` 번들에 `payload\BepInEx\plugins\VhvrExtras.dll`로 추가(0.7 재포장 없음). install/uninstall은 payload 폴더를 재귀 열거하므로 매니페스트가 자동으로 포함한다. zip+sha256 재생성.
- Q-D174-6 **ANSWERED / agent(구현):** 햅틱은 **(b) 발사 가능해진 순간** 기준으로 구현. 게임이 쿨타임 종료를 노출하지 않으므로 슬롯 `FireCalls` 증가(실제 발사) 시점부터 `1/base_fire_rate` 타이머를 돌리는 **모델링**이며, 재장전 완료를 읽은 값이 아니다(정직).
- Q-D175-1 **OPEN / user:** 실험용 `MenuFirst=true` 세션을 허용할지. 켜면 메인 메뉴가 VR 패널로 보이지만 **그 세션은 손/무기 통합이 설치되지 않는다**(평면 스테레오 릴레이) — Stage0 호스트의 `GameplayOnly=false` 경로 때문이며, 무결 절환은 소스 빌드 몫이다.
- Q-D175-2 **OPEN / 헤드셋 필요:** 메뉴 레이저 클릭이 게임 EventSystem에 닿는지(마우스 포인터 기반 `InputModule`인지). 닿지 않으면 다음 수단은 플러그인의 가상 패드 주입이다.
- Q-D175-3 **OPEN / 헤드셋 필요:** HUD 1.5배가 화면에서 잘리지 않는지(특히 레벨업 카드 3장), 그리고 `Weapon.attributes.base_fire_rate`가 실기기에서 갱신/유효한 값인지(쿨타임 햅틱 주기와 자동 연사 판정이 여기에 의존).
- Q-D175-4 **OPEN / user:** 0.7 재포장 시점(지금은 0.6-final 번들에 플러그인 추가 방식). zip 이름/sha 정책 결정 필요.

## D-174 — extras 패치 플러그인(5개 요청) 진행 기록 — 2026-10-14
- Q-D174-1 **OPEN / 헤드셋 필요:** #4의 버튼 매핑이 맞는지 — **좌 `Lower`(Y) → 특수능력(SpecialAbilityInput)**, **우 `Lower`(B) → 블러디러시(BloodyRushInput)** 로 구현했다. 사용자 표현은 "Y/B로 대체사격"이 하나뿐이라, 두 능력의 좌우 배정과 "대체사격"이 어느 쪽인지 착용 확인이 필요하다. 반대면 설정 대신 한 줄 교체로 뒤집는다.
- Q-D174-2 **OPEN / 헤드셋 필요:** #5의 0.5초 재발사가 **모든 무기**에 걸려 있다(무기별 반자동 판별 없음). 자동 연사 무기에서 체감상 문제가 없는지, 아니면 특정 무기에서만 켜야 하는지 확인 필요. 실제 연사율은 게임 쿨다운이 제한하므로 "빨라진다"기보다 "따로 누르지 않아도 나간다"가 기대 효과다.
- Q-D174-3 **OPEN / user:** #3 UI 1.5배의 범위 — HUD 전체(체력바·점수·쿨타임·아이콘)를 1.5배로 키우는 것인지, 레벨업 선택창 같은 특정 패널만인지 지정 필요. 현재 배포본은 `UiVerticalOffset`(세로 위치)만 있고 배율 개념이 없다.
- Q-D174-4 **OPEN / user:** #1 메뉴 패널 사양 확정 필요 — 패널 크기(시야각 기준), 거리(1.5 m 고정으로 해석), 높이 고정값, 검은 배경 처리, 컨트롤러 포인터의 클릭 방식(트리거/스틱). D-172에서 시도했다가 "메뉴가 검게 나온" 원인(패널 RT가 overlay/UI 캔버스를 못 담음)은 규명돼 있어 재발 방지 조건은 알고 있다.
- Q-D174-5 **OPEN:** 배포 반영 방식 — `VhvrExtras.dll`을 기존 `dist\VampireHuntersVR-0.6-final`에 추가할지, 새 버전(0.7)으로 재포장할지 결정 필요. install/uninstall 매니페스트(파일 해시 목록)와 stock 검증 스크립트도 함께 갱신해야 한다.
- Q-D174-6 **OPEN / 헤드셋 필요:** #2 쿨타임 햅틱의 트리거 시점 — "쿨타임이 차면"이 (a) 발사 직후 재장전/쿨다운 시작인지 (b) 발사 가능해진 순간인지. 무기 cooldown 필드를 읽어야 하며, 좌/우 어느 손에 어떤 무기가 있는지도 확정 후 구현한다.

## D-147 — P1-3 옵트인 구현 + 해상도 프리셋 구현, MSAA는 결정 대기 — 2026-10-13
- Q-D147-P1-3 **ANSWERED / user:** 세션당 raw 텍스처 덤프는 **옵트인 플래그로 남긴다**. → `CaptureFirstFrames`(기본 `false`) 구현 완료. 배포 기본 상태에서 raw 파일 0장.
- Q-D147-RES **ANSWERED / user(방향) + agent(구현):** 해상도 프리셋을 만든다. **2064x2208은 한쪽 눈 기준**이다(양쪽 합계 4128x2208, raw 파일 18,229,248 B = 2064×2208×4). 제시한 3840x2160 / 3072x1728 / 4800x2700은 16:9(평면·양쪽 합계식) 숫자라 눈 하나에 그대로 넣으면 눈 비율 0.935가 1.778로 바뀌고, 컴포지터가 늘어난 그림을 받는다. 그래서 **눈 비율을 유지하는 배율 방식**으로 구현: `low` 0.8× = 1651x1766/눈, `normal` 1.0× = 2064x2208/눈(기본 = 기존과 완전 동일), `high` 1.2× = 2477x2650/눈, `custom` = `EyeWidth`/`EyeHeight` 그대로(512~4096 클램프).
- Q-D147-RES-SCALE **OPEN / user:** `high`를 라벨대로 1.2×로 둘지, 제시 숫자의 비율(4800/3840 = **1.25×** → 2580x2760/눈)로 맞출지. 한 줄 변경이면 된다.
- Q-D147-AF **ANSWERED / agent:** 비등방성 필터링은 **이미 옵션**이다(`Anisotropic`, D-104, 기본 `false` → 켜면 `ForceEnable` + limit 16). 정직한 한계: 게임이 그리는 텍스처 샘플링에는 영향이 있지만, 우리 눈 텍스처는 밉맵 없는 1:1 GPU 복사라 이 옵션이 눈 이미지를 다시 샘플링하지는 않는다.
- Q-D147-MSAA **OPEN / user (결정 필요):** 안티앨리어싱(MSAA)은 가능하지만 **네이티브 수정이 필요하다**(TOOL-080). 현재 `Gpu.cpp:12`가 MSAA 소스를 거부하고 스왑체인도 `sampleCount=1`이라, Unity 쪽 `antiAliasing`만으로는 호스트가 실패한다. 눈별 리졸브 대상 + `ResolveSubresource` 후 기존 복사 경로(대략 30줄, +18MB/눈, 프레임당 리졸브 1회). 옵트인(기본 off)으로 넣는 것을 제안. **지금 할지 / 0.3 재포장·재검증 뒤로 미룰지** 결정 필요.
- Q-D147-MSAA-VS-FXAA **analysis / user 요청(작업은 나중):** 결론은 **이 프로젝트에는 MSAA가 FXAA보다 유리**, 다만 0순위는 "게임 자체 AA를 먼저 확인". 근거: ① 실기기 6회 로그가 모두 `QUALITY aniso=ForceEnable msaa=0 lodBias=1.5 eyeMsaa=1` → 게임은 **AA가 아예 꺼져 있고 AF는 이미 ForceEnable**(D-104 `Anisotropic` 옵션은 이 게임에서 사실상 무효, 해는 없음). ② 게임은 URP(`UniversalRenderPipelineAsset` 확인)이고 우리 눈 카메라는 `Camera.CopyFrom`으로 만든 새 GameObject라 **URP의 카메라별 `UniversalAdditionalCameraData`(FXAA/포스트프로세스/렌더타입)가 복사되지 않는다** → 소스 카메라 AA가 우리 눈에 적용된다고 가정 금지, 측정 우선(URP asset msaa/눈 로그 1줄 + frame-grab 비교). ③ FXAA는 공간 필터라 **선명도를 깎는다**(VR에서 조준선·원거리 텍스처·HUD가 뭉개짐)이고 시간적 shimmer는 못 고친다. ④ FXAA를 우리가 넣으려면 두 번째 RT(+18MB) + 셰이더 + 눈마다 blit을 **네이티브 제출 타이밍 안에** 끼워야 해서, 지금 가장 불안정한 렌더 스레드 동기화를 새로 건드린다(엔지니어링 비용이 "싸다"는 통념과 반대). ⑤ MSAA는 지오메트리 엣지/알파컷만 고치지만 번짐이 없고, 리졸브가 이미 있는 `Gpu.cpp` 복사 지점에 들어가 Unity 렌더 루프를 안 건드린다. ⑥ 가장 이득 대비 비용이 좋은 건 **이미 구현된 슈퍼샘플링 `EyePreset=high`**(엣지+텍스처 shimmer 동시 개선, 번짐 없음, 코드 0줄, 대가는 +44% 픽셀)이며, 그 전에 **TOOL-078(18분에 cycleMs 21.36→37.27ms, 90Hz 예산 11.11ms를 이미 초과)**을 먼저 분리해야 한다. TAA는 shimmer엔 최고지만 VR 고스팅/멀미 위험이 커서 비추천.
- Q-D147-MENUVR **ANSWERED / user:** 메뉴에서 VR 재진입(P3-8)은 **나중으로 보류**한다.
- Q-D147-REPACK **OPEN:** 0.3 재포장 + 헤드셋 재검증 1회. 현재 `dist\VampireHuntersVR-preview-0.2.zip`은 D-146 이전 빌드라 배포 금지. D-147은 네이티브(Configure 시그니처)와 플러그인을 모두 재빌드하므로 0.3은 두 해시 모두 새로 핀한다.
- Q-D147-PERF/AA **진행 (D-147b, 사용자 승인):** 제안 순서대로 진행. ① URP/AA 실측 로깅(`QUALITY_AA` 2줄 + 리시트 `quality`: pipeline 타입, asset 반사 프로브(msaaSampleCount/supportsHDR/antialiasing/postProcessingEnabled — 못 읽으면 `absent`/`error`로 정직 표기), `QualitySettings.antiAliasing`, 눈 RT MSAA, 소스/눈 카메라 `allowMSAA`, 소스 카메라 vs 눈 카메라 컴포넌트 목록) ② TOOL-078 계측(`[OPENXR_HOST] PERF` 윈도 + 리시트 `perf`) ④ `/Brepro`(TOOL-079 **FIXED**) 구현 완료. ③ MSAA는 ①의 실측값(특히 `eyeComponents`에 URP 카메라 데이터가 있는지)과 ②의 비용 추이를 본 뒤 판단. ⑤ 0.3 재포장 → 헤드셋 1회에서 ①② 데이터를 수집한다.
## D-146 — d145a 실측 완료, 다음 단계 항목 재검토 대기 — 2026-10-12
- Q-D145-SESSION **ANSWERED / user:** 무제한 세션 실증(1,132초 무종료). 종료 경로는 스테이지 종료 / `stop.request` / 렌더 정지 워치독.
- Q-D145-HEADSET **ANSWERED / user:** `d145a` 착용 실행 완료. 게임 정상 작동, 탄도·화염 정상. 스테이지 종료가 FAIL로 기록된 건 D-146a에서 수정.
- Q-D146-DEFER **OPEN / user (재검토):** ① 세션당 73.9MB raw 덤프 옵트인화 ② 18분 후 프레임 20→37ms 원인 ③ 해상도 프리셋 ④ 메뉴에서 VR 재진입 ⑤ **0.3 재포장 + 헤드셋 재검증 1회**(현재 `dist\VampireHuntersVR-preview-0.2.zip`은 D-146 이전 빌드).
- Q-D146-NEXT **OPEN / user:** 위 항목의 우선순위 결정.

## D-145 — B안 구현 완료, 헤드셋 재검증 1회 필요 — 2026-10-11
- Q-D144-SHIP **ANSWERED / user:** **B안 전부** 선택 — 세션 무제한 + OpenXR 런타임 자동감지 + RenderTimingDiagnostics 제거.
- Q-D144-WATCHDOG **ANSWERED / agent (기록):** wall-time 워치독은 유한 세션(60~900)에서만 유지하고, 무제한 세션에는 **프레임 정지 감시 60초**를 남긴다(프레임이 멈추는 것이 사람이 복구할 수 없는 유일한 조건). DECISIONS D-145.
- Q-D144-RUNTIME **ANSWERED / agent (기록):** 등록된 모든 OpenXR 런타임 허용(시뮬레이터는 항상 허용). Virtual Desktop만 실기기 검증됨을 README와 패키지 경고에 명시.
- Q-D145-SESSION **OPEN / user:** 세션 자동 종료가 없어졌다. 종료는 사용자가 `stop.request` 파일 생성 또는 게임 종료/스테이지 종료로 한다. 60초 무프레임이면 자동 종료된다.
- Q-D145-HEADSET **OPEN / user:** 새 payload(`VhvrNativeHost 6162A025…`, `VampireHuntersVR.Stage0 50204473…`)는 **헤드셋 재검증 1회가 필요하다.** 절차: `docs\playtests\PLAYTEST-D145-UNLIMITED-RUNTIME-KO.md`, 실행 명령은 그 카드 참조. 오프라인 검증은 32단계 전부 PASS.

## D-144 — 배포 형태 결정 대기 (A안 vs B안) — 2026-10-11
- Q-D144-SHIP **ANSWERED (위 D-145):** 사용자가 B안을 선택했다. A안(0.1 zip, 600초, VD 전용)은 폐기하고 0.2 패키지로 대체.
- Q-D144-WATCHDOG **ANSWERED (위 D-145):** 무프레임 감시를 유지하는 쪽으로 결정.
- Q-D144-RUNTIME **ANSWERED (위 D-145):** VD 외 런타임도 허용하되 미검증으로 표기.
- 비용 명시: B안은 네이티브 DLL 재빌드 → 해시 변경 → **헤드셋 재검증 1회 필수**(사용자 착용 필요). A안은 추가 사용자 시간 0.

## D-139 — endpoint correction candidate; no new headset request
- Q-D139-LAUNCH **AUTHORIZED / RUNNING:** 사용자 “게임 시작”. 준비 재검사0 후 d139a/DisableVSync/UsePreinstalled 한 번 실행;headset attached/game PID13656/Stage0/MainMenu_2 확인. 플레이 후 방향·손 안정·편안함 보고 및 종료/복구 receipt 대기. 기존 no-launch 문구는 승인 전 이력; 재실행 금지.
- Q-D139-LIVE follow-up: Egypt_Map/gameplay lock/컨트롤러·양안 제출/endpoint memoryPatchActive=true,error=null 관찰. 실제 화염 방향·양손·제로길이·시각 수락 및 종료 복원 receipt는 여전히 OPEN.
- Q-D139-ENDPOINT **ANSWERED static:** native endpoint is passed to Flamethrower.draw_shoot_vfx after attack processing. Low-assist fallback discards it in favor of muzzle.forward. Weapon.target_hit not published in inspected shoot_forward; no speculative post-shot field/raw proxy write.
- Q-D139-LIVE **OPEN agent/human later:** actual native branch execution/effect identity, particle direction, both hand flames and zero-length endpoint behavior; visible result remains unverified. Next explicit launch only after fresh readiness.
- Q-D139-DESKTOP **OPEN agent:** stable HMD hands preserved; renderer timing cause remains unproven. No new capture/headset sitting requested now.
- Q-D139-PREP **COMPLETE:** d138a retired safely without launch(Recoveredseq27)/independentexactstockPASS. Final32checks/exit0/138/195/28/67/57/managed0warnings0errorsPASS. d139ainstaller0/232files/Installedseq14/independentverify0/prepare0/pins+config+stockoutsideownedtimes+VDXR+pointerPASS;READY_PREINSTALLED_NOT_LAUNCHED. Next explicitlaunchonly -Tag d139a -DisableVSync -UsePreinstalled;now no headset request/auto launch/oldtagreuse.

## D-138 — 새 보고 수집 완료, 추가 헤드셋 요구 없음
- Q-D136-HUMAN **ANSWERED / user:** HMD 손 안정적, 녹화에서 조준/VFX 방향으로 손·무기 교대 표시. 상하는 손을 따르고 좌우 VFX는 HMD 시선 반대. 정상 총알 방향에 VFX를 맞출 것. 새 영상 <Recordings>\편집후\여전함.mp4 확인 완료; 시각 버그 수락 아님.
- Q-D135-BUSY **OPEN / agent:** d136a 진단이 시작 직후 API 오류로 종료되어 busy/camera 동시성 증거 없음. first serial1≠sample1. 추가 사용자 세션 없이 실패 원인과 소스 보완/회귀 수행.
- Q-D136-PARTICLES **OPEN / agent:** simulationSpace alias live API 불일치; D-138 미측정 이유를 명시하고 이 읽기를 제거. 실제 velocity/ownership는 여전히 미측정, 강제 공간 변경 없음.
- Q-D134-MATCH **OPEN / agent:** 이번 HMD 안정 보고는 유효하지만 edited PTS↔Unity↔HMD 프레임 동기화 아님. 새 동기 영상 요구 없이 기존 증거부터 분석.
- d136a 종료 및 exact stock 복구 확인; 소모된 tag 재실행 금지. 현재 진단만 보완하며 VFX fix 주장 없음; 최신 준비 완료는 Q-D138-PREP 참조.
- Q-D138-AUTO **COMPLETE offline:**31check/exit0/managed0경고0오류,195/28/67/57및Native fixturesPASS. 새 d138a 설치/독립 검증은 아래 Q-D138-PREP에서 완료 확인. 지금 테스트 참여나 실행 명령을 요구하지 않는다.
- Q-D138-PREP **COMPLETE:** d138a설치0/232files/Installedseq14/독립verify0/prepare0/hash+config+stockoutsideownedtime+VDXR+pointerPASS. CurrentMODDED/READY_PREINSTALLED_NOT_LAUNCHED. 진단판이지 VFXfix아님;다음명시실행만 -Tag d138a -DisableVSync -UsePreinstalled. 지금 추가 헤드셋 요구 없음.

## D-136 — 자동 검증/진단 준비 승인, 최종 테스트에서만 연락
- Q-D136-WORK **ANSWERED / user:** 필요한 검증은 에이전트가 수행하고 중간 보고 생략. 기존 계약에 따라 build/test 후 가역 설치 가능; 별도 실행 명령 없이는 게임 실행 불가.
- Q-D135-BUSY **OPEN:** 새 result/frame/seq/actual-drive/native/camera 관찰을 준비했으나 실제 사건 기록 없음. 사용자에게 중간 헤드셋 세션 요청 없음.
- Q-D134-MATCH **OPEN:** matched HMD/desktop 증거 및 Unity clock↔video alignment 없음. 다음 단일 최종 테스트에서 짧은 정지/손 동작으로 비교; 현재 입증 주장 없음.
- Q-D136-PARTICLES **OPEN / agent:** simulationSpace/count 관찰은 실제 velocity/ownership가 아님. 공간/속도 강제 변경 금지.
- Q-D136-AUTO **COMPLETE offline:** full31checks/exit0,new55plus3repeatandexisting195/28/67/native41fault/4exact/4flip/4shapePASS. freshd136a preparation은 별도 독립 확인으로 READY 판정; 아래Q-D136-PREP참조.
- Q-D136-PREP **COMPLETE:** d136ainstaller0/232files/independentverify0/Installedseq14/livepayload+config+stockoutsideownedhash/time/currentpointerPASS,VDXRunchanged,no launch. 현재MODDED/READY_PREINSTALLED_NOT_LAUNCHED. 실행args -Tag d136a -DisableVSync -UsePreinstalled;소모된d130a금지.
- Q-D136-HUMAN **IN PROGRESS:** 사용자 "시작" 명령 후07:28:55Z 헤드셋probe PASS/Steam실행,07:28:58Z 게임 확인,07:29:31Z GAMEPLAY LOCKED/07:30:07Z SESSION Egypt_Map/controllers=true/rightDriving/leftWaitingForWeapons. 2–3분 관찰은 진단용이며 수정/릴리스 수락 아님. script:PLAYTEST-D136-FINAL-KO.md. 결과와 종료 후 원본 복구 미확인.

## D-135 — Decoded content correspondence established — 2026-10-11
- Q-D133-RAW **ANSWERED / agent analysis:** full sequential2160edited/3240rawframes;all editedframesmap r=512+ceil(e/2),zero formula mismatches,maxcentralMAE1.161/255. Raw1095–1097/36.500–36.567s correspondsedited1165–1170/19.416667–19.500s;visuallymatched. Export settings unknown;not aUnityframeclock mapping.
- Q-D134-MATCH **OPEN / narrowed evidence gap:** raw/editedidentityresolved;matchedHMDandrawPTS→Unityrenderframe/busy/meshidentityremainunverified. No new headset session requested.
- Q-D135-BUSY **OPEN / agent-only:** didnativebusy=6skiphandcorrectionduringthese rawposechanges,andwhichcamerasrendered? Currentreceiptslackskip/frameIDs. Candidate,notrootcause;futuretelemetryrequiresseparateauthorization/freshpreparation.

## D-134 — HMD hand visibility clarified — 2026-10-11
- **User answer:** "HMD 안에서는 확인하지 못함. 거기서 손은 좌 우 하나씩만 있었음". In HMD, user remembers one hand per side and did not verify the anomalous hand toward the flame. No duplicate-hand report in HMD;not proof of absence at the same recorded event or normal HMD flame direction.
- Q-D134-MATCH **OPEN / agent-only evidence gap; narrowed D-135:** edited/raw decoded content mapping resolved;matched HMD/game-output camera event and Unityrenderclock remain unknown. Default: desktop presentation investigation first;do not declare recording-only rootcause. No new headset session requested.

## D-133 — OBS settings inspected — 2026-10-11
- Q-D133-HAND **ANSWERED 2026-10-11 / D-134:** HMD had one left and one right hand;user did not verify the anomalous hand toward flame there. Shared anomaly not confirmed;recording-only absence not proven. No new sitting.
- Q-D133-RAW **ANSWERED 2026-10-11 / D-135:** decoded raw-source/edited content mapping established r=512+ceil(e/2). Raw720p30→edited1080p60repeatedcontent;rawPTS≠synchronizedUnityrenderclock. Do not assign editedtimestampsdirectly to runtimeevents. Detail:RENDER-TIMING-D135-ANALYSIS.md.

## D-132 — Recording path discriminators — 2026-10-11
- Q-D132-CAPTURE **ANSWERED 2026-10-11 / D-133:** OBS Game Capture; user permits settings inspection. Active 모니터1 scene contains 게임 캡쳐 2 targeting Vampire Hunters.exe; OBS D3D11 shared texture acquisition confirmed. Game output, NOT HMD mirror or monitor capture despite scene name. Current FFmpeg/720p30/GOP250; nearby raw confirms CPU x264. Detail: OBS-CAPTURE-D133-ANALYSIS.md. No new sitting.
- Q-D132-HAND **ANSWERED 2026-10-11 / D-134:** user remembers one hand per side in HMD and did not verify the anomalous hand toward flame there. Preserve this limited observation rather than claim HMD VFX normality or synchronized absence.
- Q-D132-REPORT **ANSWERED:** user says all recordings flicker/stutter while HMD is smooth,reticle stable;new36s1920x1080 clip supplied. No measured flicker frequency or clip/run synchronization from sought samples. Preserve accepted firing behavior;analysisVFX-MIRROR-D132-ANALYSIS.md.
- Q-D131-CLEANUP **COMPLETE:** existing d130a ended06:03:44Z,HOST_FAILEDscene/playerownershiploss,causeunknown;uninstall0/cleanupErrornull/stockTrue,Recoveredseq27;fresh independentstock121files/8dirs/hash/timePASS,oldPIDsabsent,VDXRunchanged. Tag consumed,currentSTOCKnotREADY;no automatic reinstall merely after session ended.

## D-131 — d130a execution authorized — 2026-10-11
- Q-D131-START **ANSWERED:** user said 시작; launch once using preinstalled d130a. Harness15320 started05:57:40Z,headset attached05:57:41Z,game running05:57:43Z;installation skipped,settings/DLL checks passed. WAITING_FOR_GAMEPLAY05:58:02Z; no repeated launch permission needed.
- User already wears headset. Next human step is in-game Play -> stage -> start; not installation or re-wearing. Visual results and final cleanup remain pending. No new VFX/projectile fix in this payload.
- **Update05:58:37Z:** GAMEPLAY LOCKED05:58:18Z Egypt_Map;game72828/harness15320 live,controllersTrue,rightDriving,leftWaitingForWeapons. Setup already complete; no repeat instruction. Visual acceptance/cleanup remain pending.

## D-130 — Install-first contract reaffirmed — 2026-10-11
- Q-D130-PREP **ANSWERED by user:** game end -> exact stock restoration; coding/build/tests complete -> validated reversible reinstallation before ready report. User already wears headset for an execution command; never start the slow installation then. Existing authorization covers preparation, not automatic game launch.
- Q-D130-READY **COMPLETE, agent-verified:** d130a preinstalled,installer0/232files; independent verification0/empty stderr/all payload+config+stock outside owned roots/journal/current pointer PASS05:32:33Z. d127a remains completed and must not be reused. Launch command: <RepoRoot>\tests\headset-handaim.ps1 -Tag d130a -DisableVSync -UsePreinstalled. No sitting started; no repeat install approval needed.
- Q-D129-EVENT remains OPEN for diagnosis only. Do not repeatedly request it in response to a deployment instruction or confuse it with an installation prerequisite.

## D-129 — Requirement corrected / event provenance — 2026-10-11
- Q-D129-ANGLE **ANSWERED by user:** actual firing angle normal; visible effect angle wrong. Preserve accepted aiming/damage behavior; not full weapon-family/gate acceptance.
- Q-D129-CROSSHAND **ANSWERED symptom / OPEN provenance:** right-hand projectile-firing weapon sometimes appears to launch from left. Unknown affected weapon/event. Existing d127aRIGHTflamethrower/LEFTSMG samples cannot substitute for the separate report.
- Q-D129-EVENT **OPEN, existing-video only:** timestamp in `<Recordings>\편집후\전체영상.mp4` showing a right-hand projectile's first appearance at left; weapon name if known. Why: many simultaneous weapons/effects prevent ownership from single frames. Default: unresolved, no new headset sitting or speculative patch.
- Q-D128-PLAYTEST **ENDED / NOT_FORMALLY_VERIFIED:** saved runHOST_FAILED on menu ownership loss,finished05:03:55Z;cleanup succeeded, fresh independentstock pass; old PIDs absent. No same-tag relaunch. Prior checkpoints below are historical.
- Q-D128-TRACE **REVIEWED, limits retained:** L12857/R19091 samples no sampled trace errors;post-writer zero overwriteSamples cannot rule out other writers/scopes. Per-shot maxima178.78954deg/3.717299m are different observables;first drift precedes left weapon availability. No emitted projectile or particle-velocity acceptance.
- **Agent-only unresolved:** live muzzle reference ancestry; emitted object/owner/init/next-frame motion; FX native branch,simulation/velocity/render timing and reload_t during off samples. Proposed reference-only instrumentation is not installed;noVector3 detours. Detail: VFX-PROJECTILE-D129-ANALYSIS.md.

## D-128 d127a launch — 2026-10-11
- Q-D127-SITTING **ANSWERED**: Phase1 installed (c7cf86d); user now said 실행. Phase2 started harness17848 at04:57:47Z, game56600 running04:57:50Z; no repeat permission/duplicate launch. Await GAMEPLAY LOCKED and written playtest step6 result: weapon name, spin/disappearance, barrel roll vs swing, hits, onset on fire/level-up/equip. Default: unavailable weapon or unobserved symptom = NOT TESTED, not PASS.
- Q-D128-TRACE **OPEN, agent review before interpreting results**: exclude initial/reseed up-axis artifacts; post-driver sampling does not observe all overwrites; node-resolution exceptions lie outside Observe's catch. Read runtime logs and exact sample context; zero overwriteSamples/errors is not proof of non-interference or a root cause. No pose/firing change in this launch.
- Q-D128-PLAYTEST **IN PROGRESS**: GAMEPLAY LOCKED04:58:39Z Egypt_Map; initial rightDriving, leftWaitingForWeapons. Step6 waits for actual left weapon acquisition; no immediate new sitting needed. Trace summaries emitted, no matching exception/host failure as of04:59:13Z. Caller catch disables handAim if resolution throws; monitor that explicitly. Written visual report and cleanup pending.
## D-116 user return — 2026-10-11
- Q-D116-FLAME **partially answered; source triaged**: user says flame attack reach is expected, but muzzle-tip origin and sustained-stream presentation are not. Source identifies a `Flamethrower.vfx_flames_transform` parented under `Muzzle Flash Spawn`; static method shapes and d114a samples do not establish that this is the scene/target VFX seen in the clip. Selected d114a scope samples found no active owned flame projectile collider, but do not enumerate continuously. Still unknown: visible effect owner, native spawn/call order, particle simulation/lifetime, and relation to aiming proxy. No production fix is justified; preserve native reach/effect lifecycle pending stronger evidence.
- Q-D116-CROSSHAND **partially answered; source and receipt triaged**: right-hand weapon in the user's report is the SMG. Source scoping/restore is slot-local. Sparse d114a shot samples show the firing slot's plugin-held latch true for every logged shot, sometimes with both hands held; session samples cannot be matched to the separate edited clip or prove the projectile emitter/trigger hardware. Still unknown: exact left weapon, left trigger state, reload/burst-tail context, event time and actual emitted hand. Ask together at the next natural pause; do not demand an immediate headset sitting.

















































character.

Q-036 OPEN 2026-10-07 (D-058, coverage, blocks nothing): the menu shows **수동 레벨 업 (Manual level up) = T**, an action
the mod's map does not carry at all and the user never mentioned. Default: leave it to the game's own UI (the level-up
screen already exists) and do **not** invent a VR binding; if the user wants it in VR, it becomes a new action with a new
decision, not a silent addition.

Q-037 OPEN 2026-10-07 (D-059, game-side, blocks nothing): the user asked for **per-hand reload** — a **double tap** of the
left **Y** reloads the left hand, a double tap of the right **B** reloads the right hand — and guessed the game's code might
have to be modified ("아마 게임 코드를 수정해야 할지도 몰라"). It does not have to be: the assembly already exposes reload
**per weapon object** (`Weapon.reload()`, `force_reload(int)` and `instant_reload(float, bool)`, all public virtual, with
`SecondaryWeapon.force_reload(int)` overriding), while the flat key path is the single `PlayerController.reload_all_weapons()`
behind R. So the gesture is data and the per-hand call is the mod's own business. What is **not** proven: that the weapon
object the mod holds is the hand's active slot weapon at that moment, what `force_reload`'s `int` selects, and whether the
game accepts a single-weapon reload at all. Default: `reload_off`/`reload_dominant` on the double taps, calling that hand's
own weapon; if that fails in the headset, the recorded fallback is the game's single R reload (`reload_all_weapons()`) — a
downgrade written down in D-059, never silent. Stage 6B measures it.

Q-038 OPEN 2026-10-07 (D-062, XR loader, blocks the Stage 1B device-list choice): the patched
`BuildSettings.enabledVRDevices` is an **ordered** list, and the default is `OpenVR, OpenXR, Oculus` — upstream
UUVR's order plus `OpenXR`. What is **not** proven is what Unity does when the first entry cannot initialise: does it
fall through to the next entry, or does the first entry decide and the rest stay unused? The spike's own evidence
does not answer it — with an empty list nothing was attempted — and no document read so far settles it. This matters
because `OpenVR` needs SteamVR to be running, while the headset-free verification path (the Meta XR Simulator at
`<MetaXR>`) is an **OpenXR** runtime: if Unity does not fall through, `OpenVR` first means no device loads even
though `OpenXR` is listed. Default: keep the three-name list (it also covers a plain `OpenVR` SteamVR session, which
is what the user's Quest 3 setup actually is) and, if Stage A shows no device load, re-run with `--devices OpenXR`
alone — which separates "wrong name" from "no fallback" without touching the headset. Recorded as an assumption, not
as a fact.

Q-039 OPEN 2026-10-07 (D-062, process, blocks Stage A scheduling): the headset-free device-load check cannot be
started by the agent. The simulator is only usable as the active OpenXR runtime through
`<MetaXR>\activate_simulator.ps1`, which writes `HKLM:\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` and therefore
self-elevates, and the agent's shell is not elevated while UAC is on (`EnableLUA=1`,
`ConsentPromptBehaviorAdmin=5`). The currently active runtime is instead Virtual Desktop's VDXR
(`C:\Program Files\Virtual Desktop Streamer\OpenXR\virtualdesktop-openxr.json`, with `VirtualDesktop.Streamer`
running), which is usable unattended but is **not** a substitute: without a connected headset it is not expected to
provide an HMD form factor, so a failure there would be inconclusive rather than a verdict. Default: the user runs
`activate_simulator.ps1` interactively when Stage A is scheduled and `deactivate_simulator.ps1` afterwards (it
restores `PreviousActiveRuntime`); the agent does not attempt the elevation, and no run is reported as evidence when
`after` = `<MetaXR>\meta_openxr_simulator.json`, `PreviousActiveRuntime` = VDXR and `ok=true`. The simulator itself
is then started **non-elevated** (an elevated GUI with a non-elevated game is a known IPC trap) and its own state
reads "XR Simulator runtime enabled" (`artifacts/stage-a-simulator.png`). The first attempt failed only because the
shell the user had opened was *not* elevated (`"elevated": false` in the receipt); that attempt is preserved rather
than deleted. `deactivate_simulator.ps1` restores VDXR when Stage A work ends.

Q-040 ANSWERED 2026-10-07 (user decision, D-064): **option (a)** - the port continues by driving the headset
ourselves (own OpenXR session, per-eye render-to-texture, submit to the runtime). The text that follows is the question
as it stood, kept because it records why (c) was the default until the user decided and what "much larger project"
meant in practice. Originally: OPEN 2026-10-07 (D-063, target capability, blocks every further VR step on this game): can *any* VR runtime be
made to work on this build? LIM-003 shows the player contains no XR loader code, so Unity's own XR path is closed and
no settings edit reopens it. What remains untested and undecided: (a) driving the headset outside Unity's XR system -
our own OpenXR session plus per-eye rendering from the game's cameras into an OpenXR swapchain, i.e. a runtime that
composes and submits frames itself instead of asking Unity to; (b) choosing a different target game that ships the XR
loaders; (c) stopping the VR port for this target. Nothing is scheduled and nothing is being built on an assumption:
the default is (c) until the user decides, because (a) is a different and much larger project than the UUVR spike.

Q-041 OPEN 2026-10-07 (D-064, scope, not blocking): **how much of the game is in scope for VR?** The route chosen in
(a) makes a head-tracked stereo view with the game's own flat controls the natural first milestone, and everything else
(hands, holsters, reload, IK, in-VR menus) is the Stage 3-9 work the skill defines. The user has not said whether they
want the full conversion or only "play it in VR with the headset looking around while I use keyboard and mouse".
*Why it matters:* it decides whether SP5 is a milestone on the way to the full port (default) or the destination.
*Default if unanswered:* continue towards the full conversion, with SP5 as milestone 1, and revisit after M4.

Q-042 ANSWERED 2026-10-07 (user decision, D-066): **option (b)** - spend one bounded headset sitting on M2 against
VDXR, under the rules pre-committed in `docs/playtests/SP5-M2-VDXR-HEADSET.md` and executed by
`tests/sp5-m2-vdxr-headset.ps1`, so the block is attributed to the simulator or to our client with evidence. M2 stays
BLOCKED, the simulator stays M2's host, and no PASS is claimed by this decision: whether a PASS on VDXR counts as M2 is
option (d), which stays open until this run has a result. The text that follows is the question as it stood, kept
because it records what each option meant and why (b) comes before (d).

Q-042 OPEN 2026-10-07 (D-065, blocking for SP5 M2 and therefore for every VR milestone after it): **the headless
runtime has stopped us, and the next rung needs the headset - how do you want to spend that?** SP5's M2 is blocked by
`docs/BUGS.md` TOOL-011: our own OpenXR client reproduces the Khronos sample's session setup in every measurable
respect (eleven configurations, R1-R11) and the Meta XR Simulator still never starts a frame loop for it, while the
sample's session advances within one display period on the same runtime. The pre-committed SP5 rules say headset time
starts at M4, so nothing was tried on the headset. The options as they stand:
(a) **park SP5 at M2** - nothing further happens on M2 until you have headset time for something else anyway (the
    cheapest, and the block is fully recorded);
(b) **spend headset time on M2** - run the same probe against VDXR (the runtime this machine normally uses) with the
    headset on, which is the first configuration in which the session can reach FOCUSED and frames can actually be
    submitted - it is also the runtime the finished mod will run on, so this is the rung most likely to pay off.
    **Measured 2026-10-07:** without the headset, VDXR answers `xrGetSystem` with `XR_ERROR_FORM_FACTOR_UNAVAILABLE`
    (`artifacts/openxr/probe/m2-vdxr.json`), so this option needs the headset on and connected - and the first thing a
    headset session must show is `xrGetSystem` returning a system;
(c) **swap the simulator build** - the simulator is 207.0.0.18.123 here; a different Meta XR Simulator release may not
    have the stall (needs you to download it, no headset);
(d) **re-scope SP5 to the real runtime only** - drop the simulator as a milestone host and let M2's receipt come from a
    headset session, which changes the pre-committed M2 definition and therefore needs your explicit say-so.
    *(Clarified 2026-10-07, after the question was asked back: (b) and (d) need the same physical work - headset on,
    probe run against VDXR - and differ only in what is written down. (b) keeps `SPIKE-OPENXR-HOST.md` as the standard
    and adds a measurement outside it, so M2 stays open until the simulator can be made to work; (d) rewrites that
    document, so the simulator is no longer M2's host and its PASS condition and receipt path change with it - and, by
    cascade, so do M3's and M4's, whose PASS conditions are "a screenshot of the simulator window". (b) first is the
    cheaper order: its result is exactly the evidence (d) would be decided on, and if VDXR stalls too then the fault is
    in our client and can keep being debugged headset-free on the simulator.)*

*Why it matters:* every later VR milestone (M3 composed eyes, M4 the game's camera in them, and all of Stages 2-10)
sits behind M2, so this decides whether the port advances and with whose hardware.
*Default if unanswered:* (a) - the work stays parked at M2 with the block recorded, nothing is assumed, and no headset
time is asked for.

Open questions remaining: **Q-033, Q-036, Q-037, Q-041 and Q-043** - all five defaulted, and none of them now holds up headset
time (Q-042 was the only one that did, and it was answered 2026-10-07 by D-066; Q-043 offers one more sitting but defaults
to "not now"). Q-024 through Q-028 were answered earlier, Q-030 by D-056, **Q-029/Q-031/Q-032 by D-057**, **Q-034 by D-058**,
and **Q-035 by D-059**. Nothing else is outstanding: the remaining Stage 1 work is agent-side, and the gate run
itself (the UUVR spike re-run per `docs/playtests/PLAYTEST-1B-SPIKE-RERUN.md`, which supersedes the first spike's
`FAIL_NO_STEREO`) is the only user action there.


D-056 also did two things beyond the fire buttons. (1) **The two-hand link is deleted** ("투핸드 링크 없애라"): per-hand independent aim (your Q-024 choice) is the only mode now, `two_hand_link` no longer exists as an action or a gesture, `manifest.aimDesign.modesRemoved` records why, and `docs/TWO-HAND-AIM.md` no longer describes a link, a support point or a `TwoHandMode`. The grip is therefore not a link control either. (2) **The physical face-button layout was corrected**: on a Quest Touch controller **A and X are the LOWER buttons and B and Y the UPPER ones** - the D-052/D-053 prose had that reversed while the bindings were already right, so nothing moved; only the words.

### Q-043 — Spend one more short headset sitting on M2, now that the session-state gate is fixed?
*Asked 2026-10-07, after the two sittings of D-066.* The client now does what the sample does: it waits for the session
to be **Visible** and only then submits frames. The two runs so far show VDXR starting the frame loop for our client
(`xrWaitFrame` Success, `xrBeginFrame` ok) but the session never became Visible, so no frame was submitted - and a
headset attached but not being displayed reproduces exactly that (`Ready (2)`, `NotVisibleSkips=5`).
(a) **yes, one short sitting (about 3 minutes, C ladder)** - and it is worth it now for a reason the first two sittings
    did not have: **the C2 rung makes the run visible.** With `--clear --left FF0000 --right 0000FF` a PASS looks like
    solid **red in the left eye and blue in the right**, so the result is a picture you can confirm rather than a JSON
    file I read to you. Precondition, now precise: **you must be seeing the desktop in the headset** (VD streaming) when
    you say ready - both receipts so far show the session reaching only `Ready (2)`, never `Visible (4)`, which is what
    "VDXR is not displaying this app" looks like, and black in the headset is exactly that condition;
(b) **not now** - what is already in hand (the TOOL-011 stall is the simulator's, and our client drives a real runtime's
    frame loop) is enough for the moment, and the port waits at M2 with everything recorded;
(c) **switch M2's host first** - take option (d) of Q-042 (re-scope M2 to the real runtime) before spending the sitting,
    so that the same run also counts as M2's receipt;
(d) **try the simulator again** - no headset needed, and the new gate will now report "never reached Visible" there too,
    which measures the simulator's own behaviour instead of a blocked call.

*Why it matters:* M2 is the gate in front of M3, M4 and Stages 2-10, and one short sitting is the cheapest way to learn
whether our client can put frames on a real runtime's screen.
*Status 2026-10-07 (D-068):* **the headset is temporarily unavailable** (user at work), so (a), (c) and (d) cannot run
now and this question is parked with M2. The user's instruction for this period is to work on what does not need the
headset, which is **M4a** (D-069): the gameplay camera captured inside the game, flat, with its pixels written out.
*Default if unanswered:* (b) - the work stays at M2 with the block recorded, and no headset time is asked for.

### Q-044 — If the in-process route stays blocked, should the port become a two-process "host" (a Unity VR player that displays the game's frames)?
*Asked 2026-10-08 (D-070).* Recorded because the user opened a Unity project carrying Meta's XR stack on this machine, and
because it is the one fallback that depends on neither LIM-003 (the game carries no XR loader code) nor TOOL-011 (the
simulator never gives our client a frame).
(a) **no - stay on D-064 (a)** *(default)*: the port owns its OpenXR session inside the game process, renders the game's
    camera per eye and submits to the runtime; the Unity project stays a test client only;
(b) **yes, as the shipping architecture**: the mod renders the game's camera into a shared D3D11 texture and a small Unity
    player (Meta XR SDK) owns the headset, the stereo compositing and later the in-VR UI, while the game keeps its flat
    controls for the first milestone;
(c) **yes, but only as insurance**: build the host only if M2 is still blocked after the Unity third-client experiment.
*Why it matters:* (b) is a real VR experience of this game that needs no XR code inside the game at all, at the cost of an
extra process, a frame hand-off and latency - and it is not what the user asked for.
*Default if unanswered:* (a), with (c) available once the third-client experiment has a result.

Q-043 update 2026-10-08 (D-071): its purpose sharpened, its cost unchanged. The third client (Unity + Meta XR SDK) drives
this simulator's frame loop, so the simulator stall is our client's bug and not the test bed's; a VDXR PASS therefore
*is* M2, and the sitting is now a check of the fixed client rather than a tie-breaker between client and runtime. It stays
parked while the headset is unavailable, and the root-cause work it would confirm is headset-free (`docs/BUGS.md`
TOOL-011).


### Q-045 — After the headset-free fix, what should the next sitting be?
*Asked 2026-10-08 (D-072).* M2's stall is fixed and the same client submits 299/300 frames headset-free on the simulator
(`artifacts/tool011-hang-location.m4.json`). The user says the headset is available now, and this session's route
(headset-free measurement) is finished.
(a) **run the D ladder on VDXR now** (`tests/sp5-m2-vdxr-headset.ps1 -Tag hd4 -Ladder D -HeadsetShows desktop`) *(default)*:
    one short sitting confirms the fixed client on the user's own runtime and, if D1 passes, D2's "red left, blue right"
    closes M2's last clause - M2 PASS, and M3/M4b open;
(b) **finish M2's colour clause headset-free first** on the simulator with `--clear` (the D3D11 path whose self-test
    already passes), then sit once for the VDXR confirmation;
(c) **skip M2's confirmation and start M3** (photograph the composed eyes) on the simulator, treating the simulator PASS as
    M2 under D-071 - faster to a picture, but the user's own runtime stays unconfirmed until later;
(d) **go back to M4a** (the headset-free gameplay-camera capture) and leave M2 as it stands.
*Why it matters:* (a) spends ~2 minutes of headset time for the strongest evidence; (b) spends none but delays the VDXR
answer; (c) risks building M3 on a client the user's runtime has not yet shown; (d) parks the VR path where it was.
*Default if unanswered:* (a), because the rules are already pre-committed and the expected result is a PASS.

Q-043 update 2026-10-08 (D-072): **answered by the fix.** The sitting it asked for is still worth taking, but its question
("is it the simulator or us?") is settled - it was us, twice (TOOL-020, TOOL-021). What the sitting now buys is the VDXR
confirmation in Q-045 (a), not a tie-breaker.

Q-045 update 2026-10-08 (D-073/D-074): **option (b) is done, and it changed what (a) costs.** The colour clause was finished
headset-free on the simulator: TOOL-023's `E_INVALIDARG` was the runtime handing out TYPELESS swapchain images, reproduced
without a headset, fixed, and now proven by a permanent `-clear` rung (`submitted 300/301`, `discarded=0`) plus a typeless
case in `--selftest-d3d11` (D-074). D-073 also removed a 20 s-per-run wait that could never succeed, so the same D ladder
that took 76.2 s now takes ~12 s. That makes (a) the only thing left to close M2, and it is a **confirmation of a write that
already works** rather than a first attempt: `-Tag hd5 -Ladder D -HeadsetShows desktop`, with the user reporting one line
(red left / blue right). Rules and all four outcomes are pre-committed in
`docs/playtests/SP5-M2-VDXR-HEADSET.md` ("Sixth sitting"), so (b) is closed and the default now stands as (a).



### Q-045 final answer — 2026-10-08 (hd5, D-075)
**CLOSED.** User said "헤드셋 착용했다", so option (a) ran once on build `9861b63`: D1 PASS 300/301, D2 PASS 60/61, harness `M2_VDXR_PASS_D2`, stock verified. Exact human response: **"왼쪽 빨강, 오른쪽 눈 파랑 맞았는데, 너무 짧았긴 해, 다음부터 3초 더 늘려"**. This closes M2's last colour/VDXR clauses; no eye-order ambiguity remains for this sitting. M3's composed-eye image remains separate and headset-free.
**Preference recorded:** future visual runs add 3 seconds after the baseline submitted-frame target while continuing the OpenXR frame loop (`--hold-seconds 3` on C2/D2); no extra endurance or preflight delay. No repeat headset request is needed to validate this duration change.























    player (Meta XR SDK) owns the headset, the stereo compositing and later the in-VR UI, while the game keeps its flat
    controls for the first milestone;
(c) **yes, but only as insurance**: build the host only if M2 is still blocked after the Unity third-client experiment.
*Why it matters:* (b) is a real VR experience of this game that needs no XR code inside the game at all, at the cost of an
extra process, a frame hand-off and latency - and it is not what the user asked for.
*Default if unanswered:* (a), with (c) available once the third-client experiment has a result.

Q-043 update 2026-10-08 (D-071): its purpose sharpened, its cost unchanged. The third client (Unity + Meta XR SDK) drives
this simulator's frame loop, so the simulator stall is our client's bug and not the test bed's; a VDXR PASS therefore
*is* M2, and the sitting is now a check of the fixed client rather than a tie-breaker between client and runtime. It stays
parked while the headset is unavailable, and the root-cause work it would confirm is headset-free (`docs/BUGS.md`
TOOL-011).


### Q-045 — After the headset-free fix, what should the next sitting be?
*Asked 2026-10-08 (D-072).* M2's stall is fixed and the same client submits 299/300 frames headset-free on the simulator
(`artifacts/tool011-hang-location.m4.json`). The user says the headset is available now, and this session's route
(headset-free measurement) is finished.
(a) **run the D ladder on VDXR now** (`tests/sp5-m2-vdxr-headset.ps1 -Tag hd4 -Ladder D -HeadsetShows desktop`) *(default)*:
    one short sitting confirms the fixed client on the user's own runtime and, if D1 passes, D2's "red left, blue right"
    closes M2's last clause - M2 PASS, and M3/M4b open;
(b) **finish M2's colour clause headset-free first** on the simulator with `--clear` (the D3D11 path whose self-test
    already passes), then sit once for the VDXR confirmation;
(c) **skip M2's confirmation and start M3** (photograph the composed eyes) on the simulator, treating the simulator PASS as
    M2 under D-071 - faster to a picture, but the user's own runtime stays unconfirmed until later;
(d) **go back to M4a** (the headset-free gameplay-camera capture) and leave M2 as it stands.
*Why it matters:* (a) spends ~2 minutes of headset time for the strongest evidence; (b) spends none but delays the VDXR
answer; (c) risks building M3 on a client the user's runtime has not yet shown; (d) parks the VR path where it was.
*Default if unanswered:* (a), because the rules are already pre-committed and the expected result is a PASS.

Q-043 update 2026-10-08 (D-072): **answered by the fix.** The sitting it asked for is still worth taking, but its question
("is it the simulator or us?") is settled - it was us, twice (TOOL-020, TOOL-021). What the sitting now buys is the VDXR
confirmation in Q-045 (a), not a tie-breaker.

Q-045 update 2026-10-08 (D-073/D-074): **option (b) is done, and it changed what (a) costs.** The colour clause was finished
headset-free on the simulator: TOOL-023's `E_INVALIDARG` was the runtime handing out TYPELESS swapchain images, reproduced
without a headset, fixed, and now proven by a permanent `-clear` rung (`submitted 300/301`, `discarded=0`) plus a typeless
case in `--selftest-d3d11` (D-074). D-073 also removed a 20 s-per-run wait that could never succeed, so the same D ladder
that took 76.2 s now takes ~12 s. That makes (a) the only thing left to close M2, and it is a **confirmation of a write that
already works** rather than a first attempt: `-Tag hd5 -Ladder D -HeadsetShows desktop`, with the user reporting one line
(red left / blue right). Rules and all four outcomes are pre-committed in
`docs/playtests/SP5-M2-VDXR-HEADSET.md` ("Sixth sitting"), so (b) is closed and the default now stands as (a).



### Q-045 final answer — 2026-10-08 (hd5, D-075)
**CLOSED.** User said "헤드셋 착용했다", so option (a) ran once on build `9861b63`: D1 PASS 300/301, D2 PASS 60/61, harness `M2_VDXR_PASS_D2`, stock verified. Exact human response: **"왼쪽 빨강, 오른쪽 눈 파랑 맞았는데, 너무 짧았긴 해, 다음부터 3초 더 늘려"**. This closes M2's last colour/VDXR clauses; no eye-order ambiguity remains for this sitting. M3's composed-eye image remains separate and headset-free.
**Preference recorded:** future visual runs add 3 seconds after the baseline submitted-frame target while continuing the OpenXR frame loop (`--hold-seconds 3` on C2/D2); no extra endurance or preflight delay. No repeat headset request is needed to validate this duration change.


## D-086 independent-aim implementation request - 2026-10-09
User: '양손독립조준구현해줘'. Asked how to proceed with hard gates open; user chose soft gate (code now, verification UNVERIFIED). Q-047/TOOL-033, controller input, headset, shot-direction, left-hand weapon ownership and 14-bank mapping remain open. Next human-needed step is a headset session (TWO-HAND-AIM item 10) enabling HandAim in the host config; no other intake question newly needed.

## D-087 headset readiness - 2026-10-09
User: headset can be connected soon; prepare so verification starts immediately. Prepared: `tests/headset-handaim.ps1` and the Korean card `docs/playtests/PLAYTEST-6B-HANDAIM-KO.md`. Only human actions needed: charge/connect the headset through Virtual Desktop, sit, then follow the card; report H1-H7 as normal/abnormal/not seen. Q-047/TOOL-033/M4b remain open and are not part of this sitting.

## D-099 overnight work - 2026-10-10
User went to sleep and delegated the rest ("자러갈테니 필요한 작업 처리해놔"). Done without a headset: receipt isolation, snap turn, 73 fixtures, docs, commit. Open for the user next time (Virtual Desktop must be connected BEFORE the run is started): run ha11 per `docs/playtests/PLAYTEST-D099-MOVE-SNAP-SHOT-KO.md`. Not asked/decided: snap angle (default 30, `-SnapTurnDegrees`), whether VR should start from the menu screen, whether to request 90 Hz.

## D-100 - 2026-10-10 (user went to work, only short inputs for a while)
User report after ha11: (1) from some moment the aim point is off; headset recording shows the HUD following the eyes while shots go elsewhere, game recording shows the HUD following the hand (video 1010(3), not found on disk). (2) New keys: right stick A = jump; right stick up = special ability (scout dash); stick clicks = left/right robot arm; all as Xbox pad. (3) A 39-round weapon empties after ~10 shots in VR but not on desktop. Done without a headset: mapping from the game's own action asset, crosshair hide, ammo measurement, 88 fixtures, docs. Questions for the user (non-blocking): Q-D100-1 where should interact (Z) and blood ability live now that stick up/down are Special/Bloody Rush? Default: unwired. Q-D100-2 where is video 1010(3)? Default: continue from the description.


## D-101 - 2026-10-10 (user at work, short inputs)
User answers: video is in `<Recordings>\편집후`; the game has only dash (per character, the scout dash = Special Ability), jump and basic move; left X should be Interact; reload = Y / B pressed beside the headset; the user cannot count the ammo.
- Q-D101-1 (open, headset): does left X do anything visible in a stage (open a door / pick up / talk), or does it make the character run faster? The game input asset binds that pad button to Interact in one asset and Run in the other two; the receipt cannot tell which one the stage uses.
- Q-D101-2 (open, headset): how close does the controller have to be for the reload to feel right? Read `input.minFacePressHeadDistance` after the sitting; the default is 0.30 m.
- Q-D101-3 (open): is the tooltip "interact" the same Z key shown in the game keybinding menu (INPUT-MAP says Z / G)? The keyboard side is not touched by the mod.
User answers (D-102): minimap, score and combo stay visible; only the dark translucent effect behind the HUD panel is removed; the whole existing HUD is locked to the head.
- Q-D102-1 (open, headset): is the dark effect gone, and did anything else disappear (health bar background, blood gauge background, dash icons)? Read `ui.hudDarkCandidates`; entries marked HIDDEN were culled.
- Q-D102-2 (open, headset): is the head-locked HUD comfortable at 1.5 m and 1.6 m wide, and are the minimap and score inside the view?

User request (D-103): a short haptic pulse when a hand enters 0.30 m of the headset.
- Q-D103-1 (open, headset): is the pulse felt, on the right hand only, once per entry, and is it too weak / too strong? Compare with `haptic.pulsesLeft` / `pulsesRight` in `hand-aim.json`.
- Q-D103-2 (open): should the pulse also fire when the reload actually starts (Y / B pressed in the zone)? Not added; one pulse per entry only.

User request (D-104): higher frame rate and a sharper image.
- Q-D104-1 (open, user): what refresh rate, streaming resolution, bitrate and codec does Virtual Desktop use (card rows V1..V3)? They bound what the game side can gain.
- Q-D104-2 (open, headset): does `-Anisotropic` make the floor and walls less smeared, and does it cost frame rate (compare `timingMedianFps` fp1 vs fp2)?
- Q-D104-3 (open, evidence first): is eye MSAA worth adding? Needs proof of which texture `GetNativeTexturePtr` returns for a multisampled RenderTexture, then a native change and its fixtures.

- Q-D104-4 (open, headset): fp3 = VD preset back to Ultra, eye target `-EyeWidth 2688 -EyeHeight 2880`: is it sharper, and what are `cycleMs` / `submitMs`?
- Q-D104-5 (open, user): same Ultra settings with SSW Disabled or Auto: what is the application's real `cycleMs` without SSW?

- Q-D104-4 (answered by measurement, fp3): the 2688x2880 eye target did not change the frame rate. Sharpness answer is still the user's: is fp3 sharper than fp1, same Ultra stream?
- Q-D104-6 (open, agent): split submitMs into copy-issue time and fence-wait time, then decide whether overlapping prepare of frame N+1 with the copy of frame N is feasible. Q-D104-5 (SSW off) is answered: the cycle stays about 22.8 ms.

- Q-D105-1 (open, headset): level-up screen: is it centred in front of the view at 1.0 m / 1.4 m wide, are the cards readable (not rotated), is the black backdrop gone, and does the ray hover and click a card?
- Q-D105-2 (open, headset): do bullets now leave along the controller for the whole run (not only the first shots)? Note the time of any moment they stop following.
- Q-D105-3 (open, user): the earlier agreement about the UI in front of the HMD is not in the docs. Did it also cover the pause menu and game-over screen (they still follow the heading with a dead zone)?

- Q-D106-1 (open, headset): during play, is the translucent panel behind health / level / counter gone while the minimap, score, combo and bars stay? If something else vanished, report what.







































































































**CLOSED.** User said "헤드셋 착용했다", so option (a) ran once on build `9861b63`: D1 PASS 300/301, D2 PASS 60/61, harness `M2_VDXR_PASS_D2`, stock verified. Exact human response: **"왼쪽 빨강, 오른쪽 눈 파랑 맞았는데, 너무 짧았긴 해, 다음부터 3초 더 늘려"**. This closes M2's last colour/VDXR clauses; no eye-order ambiguity remains for this sitting. M3's composed-eye image remains separate and headset-free.
**Preference recorded:** future visual runs add 3 seconds after the baseline submitted-frame target while continuing the OpenXR frame loop (`--hold-seconds 3` on C2/D2); no extra endurance or preflight delay. No repeat headset request is needed to validate this duration change.


## D-086 independent-aim implementation request - 2026-10-09
User: '양손독립조준구현해줘'. Asked how to proceed with hard gates open; user chose soft gate (code now, verification UNVERIFIED). Q-047/TOOL-033, controller input, headset, shot-direction, left-hand weapon ownership and 14-bank mapping remain open. Next human-needed step is a headset session (TWO-HAND-AIM item 10) enabling HandAim in the host config; no other intake question newly needed.

## D-087 headset readiness - 2026-10-09
User: headset can be connected soon; prepare so verification starts immediately. Prepared: `tests/headset-handaim.ps1` and the Korean card `docs/playtests/PLAYTEST-6B-HANDAIM-KO.md`. Only human actions needed: charge/connect the headset through Virtual Desktop, sit, then follow the card; report H1-H7 as normal/abnormal/not seen. Q-047/TOOL-033/M4b remain open and are not part of this sitting.

## D-099 overnight work - 2026-10-10
User went to sleep and delegated the rest ("자러갈테니 필요한 작업 처리해놔"). Done without a headset: receipt isolation, snap turn, 73 fixtures, docs, commit. Open for the user next time (Virtual Desktop must be connected BEFORE the run is started): run ha11 per `docs/playtests/PLAYTEST-D099-MOVE-SNAP-SHOT-KO.md`. Not asked/decided: snap angle (default 30, `-SnapTurnDegrees`), whether VR should start from the menu screen, whether to request 90 Hz.

## D-100 - 2026-10-10 (user went to work, only short inputs for a while)
User report after ha11: (1) from some moment the aim point is off; headset recording shows the HUD following the eyes while shots go elsewhere, game recording shows the HUD following the hand (video 1010(3), not found on disk). (2) New keys: right stick A = jump; right stick up = special ability (scout dash); stick clicks = left/right robot arm; all as Xbox pad. (3) A 39-round weapon empties after ~10 shots in VR but not on desktop. Done without a headset: mapping from the game's own action asset, crosshair hide, ammo measurement, 88 fixtures, docs. Questions for the user (non-blocking): Q-D100-1 where should interact (Z) and blood ability live now that stick up/down are Special/Bloody Rush? Default: unwired. Q-D100-2 where is video 1010(3)? Default: continue from the description.


## D-101 - 2026-10-10 (user at work, short inputs)
User answers: video is in `<Recordings>\편집후`; the game has only dash (per character, the scout dash = Special Ability), jump and basic move; left X should be Interact; reload = Y / B pressed beside the headset; the user cannot count the ammo.
- Q-D101-1 (open, headset): does left X do anything visible in a stage (open a door / pick up / talk), or does it make the character run faster? The game input asset binds that pad button to Interact in one asset and Run in the other two; the receipt cannot tell which one the stage uses.
- Q-D101-2 (open, headset): how close does the controller have to be for the reload to feel right? Read `input.minFacePressHeadDistance` after the sitting; the default is 0.30 m.
- Q-D101-3 (open): is the tooltip "interact" the same Z key shown in the game keybinding menu (INPUT-MAP says Z / G)? The keyboard side is not touched by the mod.
User answers (D-102): minimap, score and combo stay visible; only the dark translucent effect behind the HUD panel is removed; the whole existing HUD is locked to the head.
- Q-D102-1 (open, headset): is the dark effect gone, and did anything else disappear (health bar background, blood gauge background, dash icons)? Read `ui.hudDarkCandidates`; entries marked HIDDEN were culled.
- Q-D102-2 (open, headset): is the head-locked HUD comfortable at 1.5 m and 1.6 m wide, and are the minimap and score inside the view?

User request (D-103): a short haptic pulse when a hand enters 0.30 m of the headset.
- Q-D103-1 (open, headset): is the pulse felt, on the right hand only, once per entry, and is it too weak / too strong? Compare with `haptic.pulsesLeft` / `pulsesRight` in `hand-aim.json`.
- Q-D103-2 (open): should the pulse also fire when the reload actually starts (Y / B pressed in the zone)? Not added; one pulse per entry only.

User request (D-104): higher frame rate and a sharper image.
- Q-D104-1 (open, user): what refresh rate, streaming resolution, bitrate and codec does Virtual Desktop use (card rows V1..V3)? They bound what the game side can gain.
- Q-D104-2 (open, headset): does `-Anisotropic` make the floor and walls less smeared, and does it cost frame rate (compare `timingMedianFps` fp1 vs fp2)?
- Q-D104-3 (open, evidence first): is eye MSAA worth adding? Needs proof of which texture `GetNativeTexturePtr` returns for a multisampled RenderTexture, then a native change and its fixtures.

- Q-D104-4 (open, headset): fp3 = VD preset back to Ultra, eye target `-EyeWidth 2688 -EyeHeight 2880`: is it sharper, and what are `cycleMs` / `submitMs`?
- Q-D104-5 (open, user): same Ultra settings with SSW Disabled or Auto: what is the application's real `cycleMs` without SSW?

- Q-D104-4 (answered by measurement, fp3): the 2688x2880 eye target did not change the frame rate. Sharpness answer is still the user's: is fp3 sharper than fp1, same Ultra stream?
- Q-D104-6 (open, agent): split submitMs into copy-issue time and fence-wait time, then decide whether overlapping prepare of frame N+1 with the copy of frame N is feasible. Q-D104-5 (SSW off) is answered: the cycle stays about 22.8 ms.

- Q-D105-1 (open, headset): level-up screen: is it centred in front of the view at 1.0 m / 1.4 m wide, are the cards readable (not rotated), is the black backdrop gone, and does the ray hover and click a card?
- Q-D105-2 (open, headset): do bullets now leave along the controller for the whole run (not only the first shots)? Note the time of any moment they stop following.
- Q-D105-3 (open, user): the earlier agreement about the UI in front of the HMD is not in the docs. Did it also cover the pause menu and game-over screen (they still follow the heading with a dead zone)?

- Q-D106-1 (open, headset): during play, is the translucent panel behind health / level / counter gone while the minimap, score, combo and bars stay? If something else vanished, report what.

- Q-D140-1 (open, user): 이펙트 재고정(이미터를 매 프레임 총구 회전으로 덮어쓰기) 구현 진행 승인? D-139 end-start 메모리 패치는 기본 OFF로 되돌려도 되는가? 문제가 보이는 화면은 HMD인가 데스크톱 창인가?

- Q-D140-1 **ANSWERED (2026-10-11)**: 구현 승인, D-139 패치 유지, 방식 B 유지, 이펙트는 공격이 향하는 곳과 같은 곳에. (HMD/데스크톱 어느 화면인지는 답 없음 - 다음 플레이 보고 때 확인.)
- Q-D142-1 **OPEN**: d140a 플레이에서 화염이 공격 방향과 같은 곳에 나왔는가? 좌우/상하 반대 회전은 사라졌는가? 어느 화면(HMD/데스크톱)인가? (이미터 수치상 Tick/busy 보정은 확인됨, 시각은 사용자만 판정.)
- Q-D142-1 **ANSWERED (2026-10-11)**: 여전히 같다(머리 움직이면 화염만 좌우 이동). 녹화의 손 2개 문제는 해결. 산성꽃은 정상처럼 보임(왼손이라 그럴 수도). 화면 종류 답 없음.
- Q-D143-1 **OPEN**: 다음 플레이에서 (1) 화염방사기 오른손 사격 중 머리만 좌우로 돌릴 때 이펙트가 고정되는가 (2) 산성꽃을 오른손에 들었을 때도 효과 범위에 맞는가 (3) HMD/데스크톱 어느 화면인가.
- Q-D143-1 **진행(D-144)**: d141a 실행됨. 사용자 보고 대기: 머리를 돌릴 때 화염 이펙트가 고정되는가 / 산성꽃 테스트 / 화면 종류.

## D-148 (2026-10-13) — FPS 레버 질문, 데이터로 정리
- **Q-D148-RENDERFRAMES**: RenderFrames=2가 왜 fps를 깎는가? → **답(실측)**: 같은 해상도에서 prepToSubmitMs 중앙이 fp1(1)=8.5ms vs d147a(2)=17.0ms, 중앙 cycle 22.53 vs 30.10ms. 엔진 프레임 1회를 더 렌더하고 마지막 것만 복사하기 때문. **→ 결정(2026-10-13): 기본값을 1로 변경**(OpenXrHost·드릴·설치 스크립트·스키마·README 반영, 2·3은 A/B용 유지).
- **Q-D148-FENCE-VS-ENDFRAME**: 남은 submitMs(7~12ms)는 게임 GPU 대기인가 VD 제출인가? → **D-148 계측으로 분해 시작**(d148a 세션에서 판정).
- **Q-D148-MONITOR-VIEW**: VR 중 모니터용 소스 카메라 렌더가 얼마를 먹는가? → **D-148 프로브로 측정**(120프레임 창 3개, d148a).
- **Q-D148-RESOLUTION**: 해상도를 낮추면 fps가 오르나? → **답(실측)**: 눈 픽셀 1.45배(fp3)가 submitMs만 +2.5ms, 중앙 cycle 불변 → 해상도는 병목이 아니다. low(0.8×)의 기대 이득 ≈1.5ms.
- **Q-D148-90HZ**: 90Hz(11.11ms) 도달 가능한가? → **답(부분)**: submitMs 바닥값(7~12ms)이 직렬로 붙어 있어 현 구조로는 불가. 렌더/제출 **오버랩(더블 버퍼)**이 유일한 경로이며 큰 작업(사용자 결정 대기).

### D-148b (2026-10-13) — d148c 착용 세션 결과로 정리
- **Q-D148-FENCE-VS-ENDFRAME → ANSWERED (d148c, 188창)**: `fenceMs` 중앙 5.11 vs `endFrameMs` 중앙 **0.09** = **57×**. 남은 submitMs는 **게임 측 Unity GPU 대기**이고 VD 제출은 무시 가능. 우리 네이티브 copy issue는 `copyMs − fenceMs ≈ 1.71ms`.
- **Q-D148-MONITOR-VIEW → ANSWERED (d148c, MonitorCostProbe)**: 소스 카메라를 끈 창(f360)이 기준(f240)/복원(f480) 대비 `prepToSubmitMs −1.24ms`, `submitMs −0.92ms`, `fenceMs −0.69ms`, `waitMs +3.28ms`. **모니터 뷰 가격 ≈1.2ms이고 `cycleMs`는 불변(22.24 vs 22.34)** → 모니터 뷰를 없애도 fps는 오르지 않는다.
- **Q-D148-90HZ → ANSWERED(정정)**: 45fps 상한의 원인은 계산이나 오버랩 부족이 아니라 **VD 스트림 주기 45Hz**다(`displayPeriodMs 22.22` = `cycleMs` 중앙 22.76, `waitMs` 0.86~2.69ms 유휴). 즉 **오버랩 없이도 45Hz를 유지할 여유가 약 5ms 남아 있고**, 90Hz는 **VD 스트림/헤드셋 주기를 먼저 올려야** 시험할 수 있다. 오버랩(더블 버퍼)은 그 다음 단계 판단으로 내려간다.
- **Q-D148b-1 (user, 필수)**: 8분 49초 플레이 체감 — 끊김이나 멀미가 있었는가? 프레임이 눈에 띄게 나빠진 시점이 있었는가? 선명도는 fp1/normal과 비교해 어떤가?
  - **부분 답(2026-10-12, 사용자)**: **중반에 고개를 돌리면 화면이 약간 늦게 따라오는 느낌**이 있었다(끊김 표현은 없음). 이는 fps가 아니라 **지연(motion-to-photon)** 신호다. 리시트에는 프레임 비용만 있고 지연 계측이 없다: 중반 창은 오히려 가장 빨랐다(`cycleMs` 22.64, `waitMs` 1.34 유휴, `fenceMs` 4.97). 따라서 지연은 (a) VD 무선 인코딩/전송 + 헤드셋 디코딩, (b) SSW 합성, (c) 45Hz에서 프레임 간격 22.8ms의 조합으로 보이며 **귀속 미확정**이다.
  - **필요한 계측(제안, 미구현)**: 네이티브 `xrWaitFrame`이 주는 `predictedDisplayTime`과 제출 시각의 차(= 제출이 표시 시점보다 얼마나 앞섰는가), 그리고 포즈 샘플 시각→제출 시각을 `poseToSubmitMs`/`submitAheadMs`로 한 줄에 남기면 "예측 대비 늦게 냈는가"를 숫자로 가른다. SSW off 세션에서 이 값이 좋아지는지도 함께 본다.
- **Q-D148b-2 (user)**: Virtual Desktop 스트림 주기를 올려볼 것인가(45 → 60/72/90Hz)? 무선 대역에서 끊김이 없는 주기가 어디까지인가?
  - **답(2026-10-12, d148e: SSW off + 90Hz 요청)**: `displayPeriodMs`가 238창 전부 **11.11(90Hz)** 로 유지됐고 `cycleMs` 중앙 **18.13ms(55.1fps)**, `waitMs≈0` — 즉 **요청을 받아냈지만 90fps를 못 내고 55fps에서 앱이 한계**가 됐다. 상한이 VD에서 앱으로 넘어왔으므로, 다음 선택은 "주기를 더 올리기"가 아니라 **앱 비용을 7ms 줄이기**(오버랩/저해상도/모니터 뷰 제거)다. 남은 사용자 확인: SSW off 상태에서 화면이 끊겨 보이는가(55fps 변동 52~59), 그리고 fp1/45Hz 대비 선명도·지연 체감.
  - **추가 답(2026-10-12, d148f: EyePreset=low)**: 저해상도(픽셀 −36%)에서 GPU 항목만 줄고(`fenceMs 4.69→4.29`) 전체 `cycleMs`는 18.13→**20.26ms**로 증가했다 → **해상도는 레버가 아니다**. 새 계측으로 미계측분은 `gapMs`(렌더 스레드 회전, 4.58ms ≈ 23%)로 확정. 단 스테이지가 달라 세션 전체 fps 비교는 교란이다.

- **Q-D148b-3 (agent, open)**: 직렬 대기를 줄이는 두 레버 중 어느 것을 먼저 할 것인가?
  - ⓐ **이른 prepare 디스패치**: `Submit` 직후 다음 `Prepare`를 즉시 발행해 `gapMs`(4.6ms = 사이클의 23%) 회수를 시도한다. 변경이 작고(관리형 phase 기계 + 필요하면 네이티브 디스패치), 새 계측(`gapMs`)으로 효과를 바로 측정할 수 있다. 실패하면 원인도 즉시 보인다(엔진이 제출 직후 렌더 스레드를 붙잡고 있으면 회수 불가로 판명).
  - ⓑ **오버랩/더블 버퍼**: 엔진이 N+1을 렌더하는 동안 N을 복사·제출. 잠재 이득이 가장 크지만(엔진 8~10ms와 우리 제출 5.7ms의 겹침) 눈 렌더타깃·스와프체인·동기화 재설계가 필요하다.
  - 권장: **ⓐ를 먼저**(작은 비용으로 `gapMs`의 성질을 규명) → 결과에 따라 ⓑ 착수 여부 결정.
  - **결정(2026-10-12, 사용자)**: **ⓑ 오버랩 바로 착수**. 단 OpenXR 제약(`xrBeginFrame(N+1)`은 `xrEndFrame(N)` 이후)으로 "제출 미루기"는 불가하므로, 통제 가능한 실제 손실인 **`fenceMs`(4.3ms 스핀)만** 먼저 회수하는 **지연 증명(opt-in `DeferredGpuWait`)** 을 구현했다(D-148b4, 기본 OFF, 32/32 PASS). d149a 착용에서 `fenceMs`→~0/`cycleMs` 감소로 이득을 확인하면 승격, 아니면 `gapMs`/엔진 프레임을 겨냥한 더 깊은 오버랩(ⓐ 재검토 + 엔진 프레임 겹침)으로 간다.

- **Q-D148b-4 (agent, open)**: d149a(지연 증명)에서 이득이 확인되면 기본값을 ON으로 승격할 것인가?
  - 승격 조건(사전 등록): ⓐ `fenceMs`가 4.3 → ~0으로 감소, ⓑ `cycleMs`가 같은 폭(≈4ms) 감소(≈55 → 65+ fps), ⓒ 사용자가 이미지 깨짐/찢김을 보고하지 않음, ⓓ `nativeFailures=0`·정상 종료·stock 원복. 하나라도 어긋나면 기본 OFF 유지 + 원인 기록.
- **Q-D148b-3 (user)**: 45Hz 유지 상태에서 ① MSAA(TOOL-080, 눈 AA 없음 해결) ② `EyePreset=high`(1.2×, 선명도) ③ 곧바로 0.3 재포장(TOOL-081 재핀 포함) 중 우선순위는?
- **"PC가 부족한가 vs VD 때문인가" 정리(2026-10-13, d148c vs fp1 vs fp3 실측)**:
  | 세션 | 눈 해상도 | 런타임이 요청한 주기(`displayPeriodMs`) | 실제 `cycleMs` | `waitMs` | 판정 |
  | --- | --- | --- | --- | --- | --- |
  | fp1 (2026-10-10) | 2064x2208 | 22.22 대부분(SSW가 90→45 반감) | 22.53 | 2.45 | 45Hz 요청, 우리 작업 ~16ms |
  | fp3 (2026-10-10, SSW off) | 2688x2880 | **11.11 고정(90Hz 요청)** | **22.79** | **0.02** | **우리가 늦음 = 그때는 PC/구조가 한계** |
  | d148c (오늘) | 2064x2208 | **22.22 고정(45Hz 요청)** | 22.76 | **0.86~2.69** | **VD 주기가 상한, PC는 창당 ~5ms 유휴** |
  - 따라서 답: **오늘 43.9fps는 VD가 45Hz를 요청한 결과이고 PC 계산은 병목이 아니다.** 그러나 그 사실이 "PC가 90Hz를 낸다"는 뜻은 아니다 — 눈 AA/선명도 작업량까지 포함한 우리 프레임 작업은 `prepToSubmitMs 9.66 + submitMs 6.94 ≈ 16.6ms`이므로 **60Hz(16.67ms)는 아슬아슬하게 가능, 90Hz(11.11ms)는 불가**하다(fp3가 90Hz 요청에서 22.8ms로 늦은 것이 그 증거).
  - 파생 결론(선명도): fp1 사용자 불만(계단 현상)의 원인은 눈 2064x2208 < VD 스트림 해상도(업스케일 ~1.3×)다. fp3 실측에서 픽셀 +45%가 `submitMs +2.8ms`였고 `cycleMs`는 불변이었다 → **`EyePreset=high`(2688x2880)는 45Hz 예산(22.2ms)에 16.6+2.8≈19.4ms로 들어간다** → **45fps를 잃지 않고 계단을 줄일 수 있다**(측정 1회로 확인 필요). MSAA 4×(리졸브 + 눈 메모리 +18MB/눈)도 같은 예산에서 시험 가능.













































































- 사용자 보고: 0.4c는 **찢김·깜빡임 없음** → ⓒ 축 충족으로 보고 ①(둘 다 기본 ON)을 실행했다. 부분 승격(PrepareAhead만)은 필요 없었다.
- 승격 방식(중요): **배포 설정**에서 true(`config\schema.json` default=true, `dist\preview\install.ps1`이 true 명시). **내부 기본값(C#/C++)은 false 유지** — 키가 없는 설정이 지연 증명을 **암묵적으로** 켜지 않게 하는 fail-safe이며, 드릴은 스위치가 없으면 false를 명시해 대조군을 보존한다.
- 롤백: 사용자가 `vhvr-openxr-host.json`에서 두 키를 false로 바꾸면 즉시 이전 경로. A/B는 드릴 스위치를 빼면 된다(플래그만 다른 같은 페이로드).

### Q-D153-1 (agent→user, 0.4d 착용 절차·보고 항목) — 무엇을 보고해야 하나
- 준비(에이전트 실행): `artifacts\d150-analysis\Prepare.ps1 -Tag 0.4d -PrepareAhead -DeferredGpuWait -EyePreset high` → **이미 실행 완료**: `READY_PREINSTALLED_NOT_LAUNCHED files=232 plugin=26DC0589 native=F7FDF2D6 prepareAhead=ON deferredGpuWait=ON perfProfile=off eyePreset=high leftTwist=0 rightTwist=0`, `install.exit=0`, `verify.exit=0`, stderr 0B, 트랜잭션 포인터 → `0.4d`. 설치된 눈 해상도는 **2477×2650/눈**(+44.0% 픽셀).
- 착용(사용자): 위 결과의 LAUNCH 줄 그대로 — `tests\headset-handaim.ps1 -Tag 0.4d -DisableVSync -RenderFrames 1 -EyePreset high -PrepareAhead -DeferredGpuWait -UsePreinstalled -HeadsetWaitSeconds 900`. VD SSW off + 90Hz, `Maze_Small`.
- **보고 ①** 선명도: `high`(+44% 픽셀)가 실제로 선명해졌는가, fps 손해가 체감되는가. (판정은 리시트의 `cycleMs`/`fenceMs`를 0.4c 12.42ms/0과 비교.)
- **보고 ②** 왼손 각도: 왼손 무기가 여전히 어긋나 보이는가. 어긋나면 `hand-aim.json`의 **`left.weaponTwistResidualDegrees`** 값을 알려주면 그 값을 다이얼에 넣은 **0.4e**를 준비한다(오른손은 `right.weaponTwistResidualDegrees`). 0이면 이미 맞다는 뜻이다.
- **보고 ③** 승격 기본값에서 부드러움이 0.4c와 같은가(80fps 근처), 찢김/깜빡임이 다시 나타나지 않는가.


### Q-D153-1 답 (2026-10-13, 0.4d 착용 완료) — 영수증이 답한 것 / 아직 사람만 답할 수 있는 것
- **실행 결과(사실)**: `HEADSET_RUN_COMPLETE_NOT_VERIFIED`, `endReason=level-exit`, `stock=True`(121 files/7,726,223,158 bytes 일치), 295창, `cycleMs 13.70` = **73.0fps**, `fenceMs 0` 전 창, 실패 0, 양손 모두 `Driving`(fire 913/626), 무기 교체 armL/armR 각 10회.
- **승격 확인**: `PREPARE_AHEAD on` · `DEFERRED_GPU_WAIT on` · `RESOLUTION preset=high effective=2477x2650` · `WEAPON_TWIST left=0.0 right=0.0` — D-153 승격이 코드 기본값을 건드리지 않고 **배포 설정 경로로** 실제 동작했다.
- **`-EyePreset high` 판정**: 매칭 버킷 **+1.7~+2.2ms**(9/12), 전체 중앙값 +1.28ms → **공짜가 아니다.** 사전 등록 규칙에 따라 **배포 기본은 `normal` 유지**(이미 그렇다), `high`는 선택 옵션.
- **왼손 각도**: `twistResidual left +1.44° / right −1.43°`, `muzzleToHand 0°`, `rollSteps 0`, `containerVsWritten 0°` → **롤/트위스트 문제가 아니다.** 남은 후보는 ① 컨테이너 안쪽 메시의 로컬 롤(현재 리시트가 up/롤을 재지 않는다) ② 저작 그립 자세.

### Q-D154-1 (agent→user, 0.4d 체감 보고 + 다음 한 수) — 세 가지만 알려주면 다음 팔을 확정합니다
- **① 찢김/깜빡임**: 승격 기본값(지연 증명)에서 다시 나타났는가? (0.4c에서 "없음"이었으므로 승격의 안전 확인)
- **② 선명도 vs 부드러움**: `high`(+44% 픽셀)가 눈에 띄게 선명한가? 그 대가로 **약 10fps(80.5→73)** 를 내줄 가치가 있는가? (기본은 `normal`로 두고, 원하시면 개인 설정으로 `high`를 유지하는 방식)
- **③ 왼손 각도**: **어느 무기**에서, **어느 방향으로**(예: 총구가 왼쪽으로 기울어 보임/손잡이가 위로 돌아가 보임), **몇 도쯤** 어긋나 보이는가? 어긋나 보인다면 ① 리시트에 메시 로컬 롤(`weaponUpVsContainerUpDeg`)을 추가한 **0.4e**로 수치를 재고 ② 그 값으로 다이얼을 설정한 **0.4f**로 눈으로 확인하는 2단계를 준비합니다(오른손은 건드리지 않습니다).


### Q-D155-1 (agent→user, 0.4e 착용 보고) — 백드롭 복원이 실제로 보이는지 두 가지만
- **배경은 코드·오프라인으로만 검증됐다.** 착용 전에는 "복원했다"고 주장하지 않는다.
- **보고 ①**: **레벨업 화면**에서 카드 뒤의 **검은 배경이 돌아왔는가?** (돌아온 것이 너무 어둡고 답답하면 `BepInEx\config\vhvr-openxr-host.json`의 `GameBackdropMode`를 `"authored"` → `"dim"`으로 바꾸면 재빌드 없이 D-096 중간값(알파 캡만, 컬 없음)으로 내려간다.)
- **보고 ②**: **플레이 중 정면 HUD 사각형(반투명 띠)이 여전히 없는가?** — Bottom Shadow는 `HideHudBackdrop=true`로 그대로 숨긴다. 이번 변경으로 **인게임 HUD의 다른 반투명 패널**(점수판 등)도 함께 살아났을 수 있으므로, 그중 **시야를 가리는 것이 있으면 이름/위치**를 알려주면 그 하나만 다시 숨기는 규칙을 추가한다.
- 참고: 세션 리시트 `host.json`의 `gameBackdropMode`/`hideHudBackdrop`과 `hand-aim.json`의 `ui.backdropCandidates`(HIDDEN/kept 목록)가 이 판정의 근거 데이터다.
- **착용 준비 상태(2026-10-13)**: `artifacts\headset-d087\0.4e\prepare.json = READY_PREINSTALLED_NOT_LAUNCHED`(`verifiedUtc 2026-10-13T04:17:47Z`, `runtimeUnchanged true`, `gameBackdropMode authored`). 준비 도중 SteamVR이 활성 OpenXR 런타임 슬롯을 가져가 `Runtime changed/missing.`으로 한 번 막혔고(TOOL-085), **사용자 승인으로 그 한 값만 VDXR로 복구**(D-156, `artifacts\d155-analysis\runtime-restore.json`)한 뒤 설치 없이 준비 검증만 재실행해 READY를 받았다. 위 두 질문은 그대로 유효하다.
- **0.4e 착용 결과(2026-10-13, 아직 사용자 답 없음)**: 세션 자체는 정상 완료(`run.json verdict=HEADSET_RUN_COMPLETE_NOT_VERIFIED`, `stockVerified=true`, `endReason=level-exit`, 11,790 프레임). **기계 측 증거는 요청대로 동작했음을 보여준다**: `ui.backdropHidden=0`·`ui.dimmed=0`(게임 배경 컬·알파 캡 0건), `ui.hudDarkHidden=1`(Bottom Shadow 하나만 숨김), 후보 29개 전부 `kept`(레벨업 검은 배경 alpha 0.80 포함). ⚠ 단 **이 세션은 SteamVR 런타임으로 돌았다**(`vrserver`가 13:47:55에 슬롯을 가져감; 네이티브 로그 `runtime=SteamVR/OpenXR in Meta compatibility mode`) → 성능 수치는 VDXR과 비교 불가이며, **시각 판정(①②)은 런타임과 무관하게 유효**하다.
- **보고 ① 보강 질문**: 레벨업 화면을 이번 세션에서 실제로 **보셨는지**(레벨업이 떴는지)와, 떴다면 카드 뒤 **검은 배경이 이전(잘려나감) 대비 돌아왔는지**.
- **보고 ② 보강 질문**: 플레이 중 **정면 HUD 사각형(반투명 띠)이 없는 상태가 유지**됐는지. 이번엔 게임 HUD의 다른 반투명 패널도 살아났으므로, **시야를 가리는 패널이 보였다면 그 이름/위치**(예: 점수판, POI 화살표 등).

### Q-D157-1 (agent→user, 다음 착용 보고) — 월드 앵커 아이콘이 실제로 보이는지 네 가지만
- **아이콘은 코드·오프라인으로만 검증됐다.** 착용 전에는 "보인다"고 주장하지 않는다(D-157).
- **보고 ①(존재)**: 플레이 중 **상자/포션(회복·파워업) 위에 색 쿼드가 떠 있는가?** 안 보이면 `hand-aim.json`의 `worldMarkers`(`livePickups/liveChests/candidates/shown`)가 원인을 가른다 — `livePickups=0`이면 이 레벨에 픽업이 없었던 것이고, `candidates>0, shown=0`이면 `MarkerMaxCount`/거리 문제다.
- **보고 ②(벽 통과)**: 그 쿼드가 **벽/바닥 뒤에서도 보이는가?** (깊이 무시가 의도다. 벽 앞에서만 보이면 셰이더 문제다.)
- **보고 ③(시선 링)**: 상자를 **등지고 서면** 시선 주변 링에 어두운 화살표가 뜨고, **몸을 돌리면 그 방향이 맞는가?** (링은 시선축 40°, 시선 앞 2 m.)
- **보고 ④(소음)**: **미니맵/점수판에 쿼드가 끼어들었는가?**, 아이콘이 **너무 많거나/커서/작아서** 거슬리는가? 많으면 `MarkerKinds`를 줄이고, 개수는 `MarkerMaxCount`, 거리는 `MarkerMaxDistance`(5~200 m)로 조절한다(재빌드 불필요).
- 참고: `MarkerKinds`는 이 빌드의 정확한 `PickUp.type` 이름 + 예약 토큰 `CHEST`만 받는다(대소문자 구분). 다른 종류(골드·경험치·유물 등)를 보고 싶으면 그 이름을 그대로 추가하면 된다 — 알 수 없는 이름이면 게임이 뜨지 않는다(호스트가 throw).
- 세션 리시트: `hand-aim.json`의 `worldMarkers.mode/kinds/wantedKinds/maxDistance/maxCount/scans/livePickups/liveChests/candidates/shown/offGaze/quads/shader/sample/error`, `host.json`의 `worldMarkers/markerKinds/...`, 로그 `[OPENXR_HOST] WORLD_MARKERS mode=...`.
- **종결(2026-10-13)**: Q-D155-1은 **더 이상 종결 조건이 아니다** — 사용자가 "TOOL-84는 닫아, 해결되었으니 더 건들지 않는다"고 명시했다(TOOL-084 CLOSED).

### Q-D157-2 (agent→user, **요구사항 확정**, 2026-10-13) — 상자·포션을 **입체 화면에서 실제 위치에** 표시
- **사용자 요구 원문 취지**: "내가 원하는건 VR 화면에서 **상자와 포션의 위치를 찾기 쉽게** 만드는거다. **평면 화면에 보물상자 표시를 하지 말고**, **입체 화면으로 실제 상자가 있는 위치에 상자가 있다고 표시**하는거지."
- **필수 제약**: ⓐ **데미지/적 화살표는 숨기지 않는다**(`Canvas - FloatingIcons/DemageArrowsParent/ArrowHud(Clone)`, 5개) ⓑ 평면 화면(Screen-space/정면 HUD 틀)에 상자 아이콘을 그리는 접근은 **금지** — 위치 표현은 반드시 **월드 앵커**(대상의 실제 좌표, 머리를 따라오는 빌보드) ⓒ 찾기 쉬워야 하므로 벽 뒤에서도 보여야 한다(깊이 무시 유지).
- **POI 용어 정리(사용자 질문)**: POI = Point Of Interest(관심 지점). 이 게임에서는 `PointsOfInterest`(MonoBehaviour)가 레벨의 **POI 스폰 지점**(`poi_spawns`/`all_points_of_interest`)을 관리하며 주기적으로 골드·경험치·회복·게이트 같은 픽업을 스폰하고, 그 방향을 **평면 HUD 화살표**(`POIArrowHud(Clone)`, `TargetHUDArrow.target`이 가리키는 월드 대상)로 보여준다. 즉 POI는 "게임이 이미 알고 있는 '가 볼 만한 곳'"이고, 그 목록이 곧 월드 아이콘의 재료다.
- **판정 방법(사람)**: 상자/포션이 보이는 위치에 아이콘이 **그 자리에 떠 있는지**, 몸을 돌리면 아이콘이 **세계에 고정**되어 있는지(머리를 따라오는 틀이 아니라), 포션 옆에 서면 아이콘이 **포션 위**에 있는지.
- **D-159 정정(0.4g 착용 직후 사용자 피드백, 필수)**: "포션은 **석상 같은 것에 달려서** 나오는데 그 **석상에 아이콘이 표시돼서 불편했다** → **포션 위치에만** 아이콘을 표시할 것. **상자 아이콘도 필요하다**." → 판정 기준 추가: ⓐ 아이콘이 **석상(스폰 설비)에는 절대 붙지 않는다** ⓑ **활성 포션이 없는 석상은 아무것도 표시하지 않는다** ⓒ 아이콘은 **실제 포션 픽업 위**에 뜬다 ⓓ **상자 아이콘**이 있다.
- **D-161 정정(0.4h 착용 직후 사용자 피드백, 필수)**: "상자도 포션도 나오지만 **석상에 붙어있는 포션은 표시가 안 되었다 → 이것도 표시되게**. 포션과 상자 모두 **거리에 상관없이** 표시하고, **반투명한 화살표**로 변경." → 최종 판정 기준: ⓐ **석상에 붙은 포션까지** 모두 표시 ⓑ **거리 무관** 전부 표시 ⓒ 마커가 **반투명 화살표**이고, 시야 밖 링에서는 **돌아야 할 방향**을 가리킨다 ⓓ 상자 화살표가 뜬다 ⓔ 데미지/적 화살표는 그대로.
- **D-163 정정(0.4i 착용 직후 사용자 피드백, 필수)**: "포션은 있었다(**벽에 걸린채** 있었을 뿐)", "**벽 뒤에선 안보여**", "상자 화살표는 좋은데 **포션은 좀 위**에 있어서 **포션 바로 위로**, 그리고 **벽 뒤에서도 거리 넘어서도** 보이게". → 최종 판정: ⓐ **벽 뒤에서도 보인다**(리시트 `zTestForced=true` 여부로 1차 확인) ⓑ **포션 화살표가 포션 모델 바로 위**에 있다(리시트 `boundsPlaced`/`statueSample`의 top) ⓒ **보관함에 걸린 포션도 표시**된다 ⓓ 한 자리에 화살표가 **하나만** 뜬다(`positionDuplicatesDropped`) ⓔ 거리 무관 표시 유지.







- **ANSWERED / receipt**: 빈 가고일 화살표 0 (가고일6/빈5/포션1→shown=1, 포션 소진/부재→6/6/0→shown=0). 상자 구간 chestTargets1→shown1. 벽 통과 플래그(zTestForced/blendForced) true, 거리 무제한.
- **ANSWERED / user (2026-10-13)**: "포션·상자 화살표가 정확한 위치에 보였고, 빈 가고일에는 없었고, 벽 뒤·멀리서도 보였다 (성공)". ⓐ 위치 ⓑ 빈 가고일 0 ⓓ 벽 뒤 ⓔ 원거리 가시성 판정 완료. ⓒ 상자 화살표 위치와 ⓕ 데미지/적 화살표·정면 HUD 보존은 다음 실행에서 확인할 미확인 항목. 사용자 `나 포션앞` 진단에 HealthPoint/Potion id214560 활성/renderer on/PickUp 없음, 다른 5샘플은 부모 비활성. 실제 모델과의 시각 일치·소진 판정·상자는 OPEN. 에이전트 캡처ID16601(허용0–10) 오류로 세션 중단/캡처 없음. 자동 원복 확인 완료(Uninstall0/Recovered27/stock121files/hash+time/VDXR 유지); 지금 추가 착용 요청/자동 재실행·재설치 없음. 이름 기반 마커 수정 금지.

### Q-D170-1 — 해상도·MSAA 기본값과 착용 판정 (agent→user, 2026-10-13)
- **전제(agent)**: "게임 해상도"를 **눈당 렌더 해상도**(`EyePreset`/`EyeWidth`/`EyeHeight`)+**눈 이미지 MSAA**(`EyeMsaa`)로 해석해 구현했다. 평면 창 해상도를 원했다면 알려주면 그 방향으로 다시 맞춘다.
- **OPEN / user**: ⓐ MSAA 기본값 — off(기본, 수용된 경로) / 4x(권장 A/B) / 2x(저비용) / 8x(고비용) 중 무엇을 기본으로 둘지. ⓑ 착용 1회를 `-EyeMsaa 4` 단독으로 볼지, off↔4x A/B로 볼지. ⓒ `EyePreset`(해상도)과 MSAA를 함께 올릴지(비용이 곱해짐).
- **판정 기준(헤드셋)**: 눈 가장자리 계단이 줄었는가, fps/`cycleMs`가 45Hz 예산을 유지하는가, 마커·벽 통과·상자 등 기존 수용 항목에 회귀가 없는가. 샘플 수가 실제로 4가 아니면 sit이 **실패해야 정상**(조용한 off 금지).
- **비용(미실측)**: 눈당 리졸브 1회 + 단일샘플 텍스처 2장(2064×2208 ARGB32 ≈ 36 MB).

## D-104-iso-promotion / 0.7.1 — 메뉴 스틱 스크롤 보류(Q-MenuScroll)

- **Q-MenuScroll (2026-10-17, OPEN / 헤드셋 + 코드 필요):** 메뉴 화면에서 컨트롤러 스틱 위/아래 -> 마우스 휠 스크롤 효과(A 안). 0.7.1 에서 정직 보류 결정. 해제 조건 3개:
  1. 0.7 의 Stage0 플러그인에 `controllers=NOT_IMPLEMENTED` 명시 — 컨트롤러 입력 경로(스틱 axis) 자체가 없음. 네이티브 호스트(`VhvrNativeHost.dll`, 335 KB) 또는 우리 `OpenXrHost.cs` 에 OpenXR action set 정의 + axis -> C# 이벤트 전달 경로 추가 필요.
  2. 메뉴 패널(네이티브 호스트가 띄우는 패널)이 `UnityEngine.UI.ScrollRect` 기반인지 확인 — 미확인. `ScrollRect` 면 1:1 매핑, 아니면 `SendInput(MOUSEEVENTF_WHEEL)` Win32 경로(B 안)로 fallback.
  3. `INPUT-MAP.md` D-057 점유(right stick up = interact, down = blood ability) 와의 충돌 해결. 메뉴 모드에서는 점유 우회(우선순위 게이트), 게임플레이 모드에서는 D-057 점유 유지.
- **대안**: 사용자가 B 안(Win32 SendInput) 또는 C 안(하이브리드) 를 선택하면 0.7.1 이후 빌드에서 A 안 대신 직접 진행 가능. 다만 A 안의 1번 조건(컨트롤러 axis 이벤트 경로)은 어느 경로든 동일하게 필요.

- **Q-FovScale (2026-10-17, OPEN / 헤드셋 + 양쪽 코드 필요):** OpenXR 런타임(`xrLocateViews`)이 보고한 4 각도(렌더 FOV) 를 사용자 설정으로 좁혀 over-render 영역을 자르고 GPU 픽셀 수를 줄이는 안. 0.7.1 에서 정직 보류 결정. D-186-FovScale (docs/DECISIONS.md) 참고. 해제 조건 5개:
  1. **검은 띠 방지**: `OpenXrHost.cs` 의 `StereoMath.Projection(...)` 2 곳과 C++ `Host.cpp:72` 의 `v.fov=views[eye].fov` 가 **동일한 scale** 로 양쪽 4 각도에 곱해져야 함. C# 만 좁히면 가장자리가 검게 표시됨
  2. **ABI 안전**: `OpenXrHostConfig` 에 `public float FovScale { get; set; } = 1.0f;` POCO 추가 + C++ `Host` struct 에 `float fovScale=1.0f` 추가 (struct 자체는 lock, struct 외부 필드라 ABI 무영향) + `HostNative.SetFovScale(float)` P/Invoke 신규 또는 `Configure(...)` signature 확장
  3. **0.7.1 호환**: 기존 `vhvr-openxr-host.json` 에 `FovScale` 키가 없을 때 기본값 1.0 으로 동작 → 0.7.1 사용자 영향 없음 (이미 검증된 D-104-iso-promotion 의 `Anisotropic` 키 패턴 그대로)
  4. **headset 측정**: 데스크탑 headset-free 환경에서 fps/시각 효과 측정 불가. 0.7.1 PLAYTEST-D104 와 함께 (1) `OpenXrProbe.Receipt.Fov` 로 4 각도 캡처, (2) 시각적 over-render 영역 측정, (3) fps baseline → 그 데이터로 0.8.0-dev 의 기본값 (1.0 vs 0.95) 결정
  5. **이득 정직 추정**: FovScale 0.90 ≈ ~22% 픽셀 감소, 0.80 ≈ ~44% (1.1× over-render 가정, 표시 FOV 1.0× 와 비교). **현재 VDXR 45Hz 상한(d148b/d148d)이 잠근 환경이면 fps 자체는 안 오르고 GPU 시간 단축만** — VSynced 헤드셋의 특성. 120Hz/비 VSynced 환경에서만 fps 점프 효과
- **대안**: 사용자가 (a) "지금 강제 Stage 3 (구현) 진행" 또는 (b) "PLAYTEST 후 결정" 중 선택. (a) 선택 시 5개 해제 조건을 한꺼번에 풀어야 함 (C# + C++ + install + VhvrSettings.exe + OpenXrProbe.Receipt.Fov 표시). (b) 선택 시 0.7.1 PLAYTEST 의 부수 데이터로 처리.
- **현재 0.7.1 의 다음 빌드 우선순위는 Q-MenuScroll (메뉴 스틱 ↑/↓ → 마우스 스크롤) 와 formselftest preview 보강 (TOOL-095)** — Q-FovScale 은 그 다음.



## D-196 / D-197 — 0.9.2 ray 90° fix 빌드

- Q-D196-MENU-RAY **OPEN (2026-10-18):** 0.9.1에서 시도한 A안(identity quaternion, view와 ray 둘 다 anchorRotation 빼기) 가 실패 — ray가 panel 우측 90° 꺾인 채로 남음. 0.9.2는 gameplay ray path(`HandAimDriver.Update` → `HandAimMath.ToWorld(origin, Rotation(anchorRotation), handSample, ...)`) 와 동일하게 menu ray도 `anchorRotation = source.transform.rotation`(= ArenaCamera의 Unity-space rotation, OpenXR→Unity 보정 + 게임 카메라 회전 포함)을 곱하도록 MenuScreenPatch.cs:150 `AnchorRotation(__instance)` 복귀. **VDXR에서 menu 조준 레이저가 panel 정면을 가리키는지 확인** (좌/우 90° 틀어짐 사라짐). SteamVR 거꾸로는 D-197 별도.
- Q-D196-OPENXRHOST-DUMP **OPEN (2026-10-18):** `OpenXrHost.cs:1283-1288` evidence dump 필드(`anchorRotation = new[] { x, y, z, w }`)가 0.9.1 + 0.9.2 빌드에 들어갔다. 0.9.2 receipt으로 실제 값 받아 `anchorRotation` 정체(= source.transform.rotation 가설) 확인 후 0.9.3에서 필드 제거.
- Q-D197-RUNTIME-ORDER **OPEN (2026-10-18):** SteamVR을 나중에 켜고 게임을 실행하면 view가 거꾸로/좌우 반전되어 보임. SteamVR을 먼저 켜고 게임 실행하면 정상. 우리 view matrix는 OpenXR LH 표준(-Z forward)에 수치상 정확 — D-195 패치 이전 receipt (un\previous\host.json)과 비교해도 view row 3은 동일한 [0, 0, -1, ...] 형태. 0.9-final DLL로 두 시나리오 모두 재현됨 → D-195 패치와 무관. 추정: SteamVR OpenXR runtime이 VDXR의 projection/view를 받아 자체 mirror 처리할 때 어긋남. 사용자 워크어라운드: **SteamVR을 먼저 켠 다음 게임 실행**. v0.9.1, v0.9.2 모두 미수정 (SteamVR-only path 추가는 회귀 위험). 다음 receipt (0.9.2 + VDXR) 으로 ray 90° 사라짐 확인 후, SteamVR mirror layer를 별도 task로 분석할지 결정.

---
---

## D-198 — 시작 15초 VR 지연 제거 (고정 warm-up → readiness 게이트)

- Q-D198-STARTUP-SPLIT **OPEN (2026-10-18):** 시작 지연의 실제 병목을 두 축으로 분리한다. (A) **Unity 준비 지연** = 게임이 메인 카메라를 만드는 시각, (B) **OpenXR 런타임 init 지연** = 네이티브 호스트가 session RUNNING 까지 가는 시간. D-198 이전에는 `Time.unscaledTime < 15f` 고정 게이트가 (A)(B)를 모두 가려 구분이 불가능했다.
  - **해제 조건**: 0.9.3 (또는 D-198 DLL) 설치 후 1회 실행 receipt 에서 `host.json` 의 `startupReadySeconds`(게이트 통과 초; 0 = 대기 없음), `startupWait`(막힌 이유), 그리고 첫 제출 프레임 시각(`evidence` 첫 entry `frame` + `status.Frames` 추이) 비교. `startupReadySeconds` 가 1~3초면 (A) 정상·수정 성공. `startupReadySeconds ≥ 10` + `startupWait = "no main camera yet scene='MainMenu_2'"` 면 **(A) 가 진짜 병목**(게임 로딩) → 우리가 줄일 축이 아님. `startupReadySeconds` 는 작은데 첫 제출이 늦으면 **(B) 네이티브 init** → `VhvrNativeHost.dll` 축 별도 조사.
  - **추가 확인**: `waiting.json` 의 `startupWait.cameraAttempts`(게이트가 카메라를 몇 번 찾았는지)와 `budgetSeconds`. 예산(180 s) 소모 전에 통과하면 정상.
- Q-D198-ADOPT-RACE **OPEN (2026-10-18, 낮은 우선순위):** 게이트가 너무 일찍 열려 `Camera.main` 이 **씬 전환 중의 임시 카메라**(예: 페이드/로딩용)일 경우 초기화가 그 카메라를 캡처할 수 있다. 현재는 D-181 `AdoptMenuCameraIfNeeded()` 가 다음 프레임에 실제 카메라로 재채택하므로 기능 회귀는 없을 것으로 보지만 **실기기 관찰 전엔 확인이 아니다**. 해제 조건: 메뉴 진입 직후 panel/eye 가 정상 카메라 기준인지 육안 + `MENU_FIRST menu camera adopted` 로그가 초기화 직후 불필요하게 반복되지 않는지 확인.
- Q-D198-WARMUP-ORIGIN **OPEN (조사):** `Time.unscaledTime < 15f` 가 들어간 원래 근거(어떤 실패를 막으려 했는지)가 15초라는 상수로만 남아 있고 문서에 이유가 없다. 해제 조건: 초기 Stage(SP5) 세션의 실패 로그/영수증에서 "카메라 없음" 최초 관측 시각을 찾아 15초의 출처를 기록. 못 찾으면 "근거 없는 상수였다"로 종결하고 D-198 결정에 남긴다(이미 그 취지로 기록됨).

## D-202 — 인게임 손/조준 합성 프레임 (BUG-202)

- Q-D202-HEADSET **OPEN (2026-10-18, 최우선):** 헤드 프레임 수정이 실제로 손을 컨트롤러에 붙이는지 확인한다. 해제 조건: 착석 1회에서 (1) 앞으로 든 컨트롤러를 무기가 **같은 방향**으로 따라온다 (2) 리티클이 그 앞에 있고 컨트롤러를 좌우로 돌리면 같이 돈다 (3) `run\hand-aim.json`의 `right.muzzleToHandForwardDegrees ≈ 0`이면서 **눈에 보이는 방향과도 일치**한다 (4) 로그에 `HAND_FRAME Head (D-202: ...)` 1회. 판정 기준 문서: `docs\playtests\PLAYTEST-D202-HAND-FRAME-KO.md`.
- Q-D202-ROLL **OPEN (2026-10-18):** 새 rest 수식이 **캡처 순간의 손 자세를 나누므로** 무기 롤이 "손의 상대 회전"을 따른다. 해제 조건: 영수증 `left/right.weaponTwistResidualDegrees`의 절대값이 5° 이하이면 다이얼 0 유지, 그보다 크면 `LeftWeaponTwistDeg`/`RightWeaponTwistDeg` A/B 1회로 값을 정하고 D-202 부기에 기록.
- Q-D202-AB **OPEN (2026-10-18, 원인 확정용):** 판정이 "아니오"면 `vhvr-openxr-host.json`에 `"HandPoseFrame": "Anchor"`로 1회 실행해 **0.9.4와 같은 90°가 재현되는지** 본다. 재현되면 원인 확정이 강화되고, 재현되지 않으면 프레임 외의 두 번째 원인이 있다는 뜻이다.
- Q-D202-LEFTHAND **OPEN (2026-10-18):** 왼손 슬롯은 Playground 씬에서 `weaponsInSlot = 0`, `state = WaitingForWeapons`였다(무기가 없어 검증 불가). 해제 조건: 왼손 슬롯에 무기가 실제로 들어가는 구간(스테이지/무기 교체)에서 왼손도 컨트롤러를 따라오는지 확인.
- Q-D202-SNAP **OPEN (BUG-203):** 헤드 프레임에서 스냅 턴은 **동작하지 않는다**(요청 거부 + 경고 1회). 해제 조건: (a) 요청 시 `SNAP ignored on the head frame ...`가 **1회만** 뜨고 시야가 **튀지 않는지** 확인, (b) 이후 BUG-203 수정(눈 합성 view-yaw 오프셋)을 구현해 30° 회전·무튐을 확인.
- Q-D202-MOVE **OPEN (2026-10-18, 낮은 우선순위):** `HandInput.HeadRelative`의 `delta = YawOf(headRot) - YawOf(source.transform.rotation)`는 `ApplyAutonomousGaze`가 게임 카메라 회전을 헤드 회전으로 덮어쓰기 때문에 상시 0에 가깝다(영수증 `input.moveYawDelta = 0`). 이동이 헤드 방향 기준인지 게임 카메라 방향 기준인지를 사용자 체감으로 확인(앞으로 밀 때 보는 방향으로 가는가).


## D-204 / D-205 — 엔지니어 터렛 건설 (BUG-204)

- Q-D204-FIRE **OPEN (2026-10-18, 최우선):** 건설 모드에서 **주 발사(트리거)** 를 당기면 터렛이 설치되는가? 게임 문구상 확정 입력은 `Fire`이고, 모드의 발사는 필드(`firePressing`)로만 나간다 → 트리거로 설치되면 게임이 필드를 읽는 것이고, 안 되면 액션 콜백(`FirePressedInput`)이 필요하다. 해제 조건: 착석 1회에서 (a) 트리거만으로 설치 → 수정은 "왼손 X → Fire 라우팅" 하나, (b) 미설치 → `FirePressedInput` + 패드 `rightTrigger` 경로 추가.
- Q-D204-GHOST **OPEN:** 건설 모드에서 **파란 고스트 터렛**이 월드에 보이는가? = 위치 소스(마우스 `Point`)가 VR에서 죽어 있는지 판정. 해제 조건: 보이면 위치 축은 정상, 안 보이면 D-205 수정 방향 2(모드가 월드 좌표 공급)로 간다.
- Q-D204-TYPES **OPEN:** 사용자가 본 "4종류"는 어디인가(레벨업 스킬트리 vs 건설 HUD)? 실제 보유 종류는? 게임은 종류를 스킬로 지급한다(`SkillType.FLAME_TURRET/LASER_TURRET/HEAL_TURRET`, `skill_desc_engineer_9/17/23`). 해제 조건: 착석 1회에서 오른스틱 아래를 반복 입력 → HUD 아이콘 또는 피드백 토스트(`feedback_flame_turrent` / `feedback_laser_turrent` / `feedback_heal_turrent`)가 바뀌는지 확인.
- Q-D204-OVERLAP **OPEN:** 왼손 X를 **건설 모드에서만** Fire로 돌릴 때 상호작용(`Interact`/Z)과 겹침이 문제 없는가? 해제 조건: 건설 모드 중 상호작용이 필요한 오브젝트(상자·문·회수 가능한 터렛)가 없음을 확인. 터렛 회수는 게임에 `remove_turret(int)`·`PlayerTowers.remove_tower_by_id(int)`가 있으므로 별도 입력이 필요할 수 있다.

