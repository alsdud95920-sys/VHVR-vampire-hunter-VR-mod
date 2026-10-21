# 버전 기록 (tracked) — 0.3V 기준점 / 0.4 / 0.5

`dist/*`는 `.gitignore` 대상이라 패키지/백업은 로컬에만 있습니다. 이 문서는 **추적되는 기록**으로,
기준점(0.3V)의 해시·측정값·소스 커밋과 다음 버전 계획을 남깁니다. 로컬 백업 위치: `dist\versions\`.

## 0.3V — 기준점 (d150a 실측 빌드, 2026-10-13 동결)

| 항목 | 값 |
| --- | --- |
| 실행 태그 | `d150a` |
| 소스 커밋 | `25cbec7` (트리 dirty 0) |
| 플러그인 | `C50D1BCA5769163C810BF502DEA119477B96BF68CF54F63CBD26A08BF19C2826` |
| 네이티브 | `F7FDF2D61F0EB8703672704216CA1FC08B75FD9D999D468E94546E8576E74BDC` |
| 로더 | `66946EA6E9CC2649E299EC412521C5EAFE56C635D05055097FE4DB81E11932DF` |
| 설정 | `C02094080DCFC63899FE0D140ED316BEEF62138E61585914457FBA7A34BBA8D2` (`PrepareAhead=true`, `DeferredGpuWait=false`) |
| 스테이지 | `Maze_Small`, 201 창 / 24,120 프레임, 레벨 종료, `CLOSED orderly=1`, 실패 0, stock 원복 |
| 기준값 | `cycleMs 18.50` = **54.1 fps**, `gapMs 0.00`, `prepMs 11.04`, `submitMs 7.33`, `fenceMs 5.63`, `prepCpuMs 46.09`, `submitCpuMs 14.19`, 첫 20창 65.4 fps |

복구 절차·정확한 바이트: `dist\versions\0.3V\`(`payload\`, `pins.json`, `timing-d150a.json`, `RESTORE-KO.md`).
**주의**: 0.3V는 대조군입니다 — 다시 돌려도 54 fps 재현이지 90 fps가 나오지 않습니다(D-149 판정: 기본값 OFF).

## 0.4 — 이번 완성 작업 (설치 완료, 착용 대기)

| 항목 | 값 |
| --- | --- |
| 소스 커밋 | `d2acb30` (**clean tree**, dirty 0) |
| 플러그인 | `C37E1F9DBDE7F196C25EB3DE491114BBDF7163141DD0CD7561CA2F21DEF7D5E9` |
| 네이티브 | `F7FDF2D61F0EB8703672704216CA1FC08B75FD9D999D468E94546E8576E74BDC` (= 0.3V와 동일, D-150은 관리 코드만 변경) |
| 설정 | `5893F7B936E201E4DB9A191BF3E6508B8D02780B7ACC74B9766EDBC1C71B31D2` (`PrepareAhead=true`, `DeferredGpuWait=true`, `PerfProfile=off`, `Output` = 태그 폴더) |
| 상태 | 게임에 **설치됨**(트랜잭션 `artifacts\headset-d087\0.4c\transaction`, 태그 `0.4c`) → **착용 완료: 80.5fps** |

**측정 결과(0.4c 착용, 2026-10-13)**: `Maze_Small`, 384 타이밍 창 / 47,480 프레임 / 600초, `CLOSED orderly=1`·실패 0·`stock=True`.
- 전체 `cycleMs 12.42` = **80.5fps** (0.3V 54.1fps 대비 **+49%**), 첫20 **11.74ms/85.2fps**, 마지막20 13.64/73.3
- `fenceMs 0` 전 구간, `submitMs 1.95`, `gapMs 0`, `aheadPrepares 120/창`
- 매칭 버킷(9개 × 3000프레임) `d_cycleMs −3.87 ~ −7.30`(전부 개선) → 세션 변동 아님
- 90Hz 목표(11.11ms)까지 **잔여 1.3ms**. 승격(기본 ON)은 **사용자 체감 보고**만 대기(D-152 / Q-D152-1).

- 내용: D-149 레버 1+2(이른 프레임 준비, CPU 분해 계측) + **D-150 엔진 품질 다이얼 `PerfProfile`**(opt-in, 기본 `off`)
  + VDXR 확장 실측 기록. 착용 팔은 **둘 다 ON**.
- 검증(오프라인): 네이티브 **52/52**, HandAim **237**(D-150에서 +8), 전체 **32/32 PASS**, stderr 0B.





















































주의: 이 설치 이후 **재빌드하지 않는다** — 설치본과 현재 빌드 산출물이 같아야 `-UsePreinstalled`가 통과한다(TOOL-072).

## 0.4e — 백드롭 정책 축소(레벨업·메뉴·상자 배경 복원, D-155) (착용 대기)

| 항목 | 값 |
| --- | --- |
| 방식 | 0.4d의 바이트·측정을 그대로 두고, **게임 배경을 되살리는 설정**(`GameBackdropMode=authored`)으로 준비 |
| 설정 | 위 0.4d 설정 + `GameBackdropMode=authored` (**HUD Bottom Shadow는 `HideHudBackdrop=true`로 계속 숨김**) |
| 상태 | 코드+오프라인 완료 — 플러그인 Release 경고 0·오류 0, HandAim **254 → 263**, `verify-config.ps1` PASS, PS AST 4/4, schema JSON OK |
| 미검증 | **착용 미검증(HEADSET PENDING)**: 레벨업 검은 배경 복원 + 정면 HUD 사각형 부재는 사람만 확인 가능(Q-D155-1) |

- 배경이 너무 어두우면 **재빌드 없이** `GameBackdropMode`를 `dim`(D-096 알파 캡만, 컬 없음)으로 내린다. `hide`는 D-155 이전 컬 정책(대조군).
- 세션 리시트로 판정: `host.json`의 `gameBackdropMode`/`hideHudBackdrop`, `hand-aim.json`의 `ui.backdropCandidates`(HIDDEN/kept), 로그 `GAME_BACKDROP mode=`.
- 착용 명령(예): `tests\headset-handaim.ps1 -Tag 0.4e -DisableVSync -RenderFrames 1 -GameBackdropMode authored -PrepareAhead -DeferredGpuWait -UsePreinstalled -HeadsetWaitSeconds 900` (준비는 표준 래퍼 `Prepare.ps1 -Tag 0.4e -PrepareAhead -DeferredGpuWait`).


## 명명 규칙






















































































## D-166 — 0.4k 폐기 / 0.4l 안전 수정·진단 후보 (2026-10-13)
- 0.4k는 `RETIRED_NOT_LAUNCHED`; 빈 가고일 fallback 때문에 최신 요구 불충족. transaction Recovered(seq27), 별도 Check-Stock:121files/8dirs/7,726,223,158bytes/hash/write times 일치. 미실행.
- 빈/비활성/비포션 holder fallback 제거, 4 m 추정 제거, 모든 픽업 소스 Component.transform 통일, AddSource의 종류/활성 최종 필터, 프레임 배치 중 비활성 소스 제외. 상자·재질·전면 HUD·데미지/적 화살표 경로의 기존 동작 유지.
- 진단: 기존 7개 자식 중 7번째도 포함, 최대96nodes/depth4 하위 탐색, 활성/위치/id/typed pickup/renderer. 이름만으로 포션 분류하지 않음.
- 최종:313 HandAim PASS / managed Release0warnings0errors / full32/32 exit0. 0.4l **READY_PREINSTALLED_NOT_LAUNCHED**(232files, Installedseq14, installer0/verify0/stderr0B), plugin24152491…/nativeF7FDF2D6… live/build/pin 일치. CHEST,HEALING,BOSS_CHEST / distance0 / count32 / VDXR 유지. 준비 이후 rebuild/fullverify 없음.
- **매달린 포션의 정체·존재 판정/조건부 가고일 표시와 상자 시각 복구는 미확정. 완성판·배포판이 아닌 안전 수정+진단 후보.** 원인 확정 전 추측 패치는 중단한다.
- **D-167: 위 READY는 실행 전 이력**. 사용자 `실행`으로0.4l 1회 소비, Maze_Small/controller/양안 제출 확인. 포션 앞 진단(scans348→416/shown0, 활성 HealthPoint/Potion id214560/PickUp 없음) 보존. 에이전트ID16601(허용0–10) 캡처 요청 실수로HOST_FAILED/양안 캡처0. 자동 원복 완료09:05:34Z:Uninstall0/Recovered27/stock121files/8dirs/7,726,223,158bytes/hash+write times 일치/errors null/VDXR 유지. 현재STOCK/NOT_READY; 같은 태그 실행·즉시 재설치 없음. 추가 production 변경/빌드 없음.

## 0.7 — Quality 설정 제거, MSAA 설치 기본값 노출 (D-188)

| 항목 | 값 |
| --- | --- |
| 소스 변경 | 0.6 코드에서 `GraphicsQuality` 다이얼(POCO + 5개 메서드 + UI 행 + CLI 플래그 + GRAPHICS_QUALITY 로그 + install.ps1의 `GraphicsQuality = -1`) 완전 제거 |
| 추가 노출 | `install.ps1` 초기값에 `EyeMsaa = 4` 박기(0.6 코드에는 있었지만 0.6 빌드 install/README에는 빠져 있던 항목), `SETTINGS-README-KO.md`에 MSAA 절 신규 추가 |
| 결정 | `docs/DECISIONS.md` D-188 (D-187은 별도 주제, 188로 부여) |
| 빌드 | `dotnet build` 0 warnings 0 errors (`VhvrSettings.dll` 35,840 B, `VampireHuntersVR.Stage0.dll` 419,328 B) |
| 셀프 테스트 | `Settings.Tests.exe` 8/8 PASS, `src\settings\Build.ps1` publish + `--selftest` exit 0, install dry-run 236 files installed → 236 files uninstalled 왕복 |
| 0.6 보존 | `dist\VampireHuntersVR_0.7.zip`(이름만 0.7, 내용물은 0.6 사본) + sha → `dist\versions\0.6-final\`에 보존 |
| 새 패키지 | `dist\VampireHuntersVR-0.7.zip` 35,614,827 B, sha256 `7BBE9198CF7F855DCAED7D370FCFB1810C9EB851B8885A4538FC4CF12A763036` → `dist\VampireHuntersVR-0.7.sha256.txt` |
| 미검증 | **헤드셋 미검증(HEADSET PENDING)** — 0.6과 동일 운영, 코드/문서/빌드까지는 완성 |
| 알려진 한계 | `vhvr-openxr-host.json`에 남아있는 옛 `GraphicsQuality` 키는 `System.Text.Json`이 알 수 없는 키로 침묵 폐기(파일 로딩은 유지, 값은 무시). MSAA 1/2/4/8 외 값은 도구 + 호스트 모두 거부하고 shipped 4로 폴백. |

## 0.7.1 — VhvrSettings.exe 에 이등방성 필터링 스위치 노출 (D-104-iso-promotion, 2026-10-17)

| 항목 | 값 |
| --- | --- |
| 출발 | 0.7 zip(sha256 7BBE9198…, 35,614,827 B) 그대로 — 0.7 zip 내부의 install.ps1 가 이미 Anisotropic = False 를 박고 있었으므로 설치 경로는 무수정 |
| 핵심 결정 | 호스트 코드(OpenXrHost.cs:879)는 0.7 부터 이미 Config.Anisotropic 분기를 가짐. **0.7.1 은 사용자 도구(VhvrSettings.exe)에 이 키를 노출하는 것이 전부** — 호스트/네이티브/DLL 변경 0 |
| 소스 변경 | src\settings\HostConfigTool.cs (DefaultAnisotropic 상수 + ReadAnisotropic/ApplyAnisotropic + BoolOf 헬퍼 1개), src\settings\SettingsForm.cs (_aniso CheckBox 1개 + LoadConfig/Preview/Save 3줄), src\settings\Program.cs (CLI --aniso/--no-aniso + --print의 aniso 라인 + --selftest의 Anisotropic 왕복 검증) |
| UI 배치 | Left=22 Top=178 Width=360 Height=20 — 클라이언트 영역 700x420 안(오른쪽 696/아래 416 한도). 컨트롤 14개, ormselftest 가드 Controls >= 13 자동 흡수 |
| 기본값 | **OFF** (D-104 정책 + 0.7 의 install.ps1 와 정합). 켜면 QualitySettings.anisotropicFiltering = ForceEnable (16x), Texture.SetGlobalAnisotropicFilteringLimits(16,16) |
| 빌드 | dotnet build 0 경고 0 오류. VhvrSettings.dll 35,840 B, VhvrSettings.exe 162,304 B (사이즈 변동 0) |
| 회귀 검증 | --selftest PASS (seed Anisotropic=false → 왕복 후 Anisotropic=false 확인), --apply --aniso 후 CURRENT_ANISO 16x, --apply --no-aniso 후 CURRENT_ANISO off, --print 출력에 niso=16x/niso=off 라인 추가, **정적 컨트롤 좌표 검사** PASS(23개 위치 지정 컨트롤 모두 클립 0). **--form-selftest 는 PASS 가드 일부만(클립 0 / 컨트롤 23)** — 이 테스트는 0.6 부터 알려진 Defender/Set-Content race 환경 이슈가 있어, 0.7.1 의 회귀가 아닌 **테스트 하니스 한계**로 분류(아래 한계 항목 참조) |
| 미검증 | **헤드셋 미검증** — 0.7.1 은 도구 변경만, 호스트 코드 0줄. fps 영향 미측정 (D-104 의  거의 공짜 가정은 미확정) |
| 알려진 한계 | (1) --form-selftest 의 preview 라인 검증은 rtifacts\form-test-...vhvr-openxr-host.json 파일이 Set-Content 직후 사라지는 환경 race 로 미통과. **대체 검증**으로 정적 좌표 검사 + --selftest 회귀 사용. (2) 이등방성 필터의 실제 fps 비용은 미측정(다음 PLAYTEST-D104 라운드에서 확정). (3) 메뉴 스크롤(A 안, 스틱 ↑/↓ → 마우스 휠) 보류, Q-MenuScroll (docs/QUESTIONS.md) 로 등록 |

## 0.9.4 — 인트로 씬에서는 VR을 시작하지 않도록 고정 (D-200, 2026-10-18)

| 항목 | 값 |
| --- | --- |
| 출발 | 0.9.3 설치본의 검은 화면 보고 → 원인 확정(인트로 씬에서 세션 시작 → 씬 전환으로 카메라 파괴 → 재채택 경로가 조기 반환에 영구히 가려짐) |
| 소스 변경 | `src\core\OpenXrHostConfig.cs` 키 2개(`StartupSkipScenes` 기본 `Intro`, `StartupSettleSeconds` 기본 `1.0`), `src\core\OpenXrHost.cs`(시작 씬 스킵 + 정착 게이트 + `STARTUP_SKIP` 1회 로그 + 검증 2줄 + `STARTUP_READY`에 `waited='…'` 추가 + **재채택 호출을 조기 반환 앞으로 이동**), `config\schema.json`(`x-host-tunables` 2키) |
| 도구/테스트 | `tests\GameplayCamera\Program.cs` 가드 3종, `tests\verify.ps1` 기대 개수 **30 → 38**(오래된 값 교정), 릴리스 템플릿 `install.ps1`·`ModNotes.txt`(2벌), 신규 `artifacts\pack-0.9.4.ps1`(은퇴 0.9.3 DLL 해시 거부 + 문자열 게이트의 두 정렬 검사) |
| 빌드 | `dotnet build` 경고 0 오류 0. `VampireHuntersVR.Stage0.dll` **428,032 B**(0.9.3: 426,496 B), SHA256 `102E765DF9DF00726332CF12050DB597560EA097E00E0BD7F021C8488234D5AE` |
| 오프라인 검증 | `tests\GameplayCamera` **38/38** PASS(신규 3), HandAim 190/190, WeaponRuntime 67/67, VhvrExtras 62/62, FlameEndpoint 138/138, `config\schema.json` 파싱 OK, 편집 PS 4개 AST OK |
| 라이브 설치 | 게임 폴더의 `BepInEx\plugins\VampireHuntersVR.Stage0.dll` 교체(설치본 SHA = 빌드 SHA). 직전 0.9.3 DLL은 `backups\D-200-20261018T104445Z\`에 보존. **사용자 설정 보존**을 위해 `vhvr-openxr-host.json`은 초기화하지 않고 2키만 추가(EyePreset custom·EyeScale 1.5·EyeWidth 1920·EyeHeight 1080·GameplayWaitSeconds 600·uiScale 1·FovScaleX 0.85 그대로) |
| 새 패키지 | `dist\VampireHuntersVR-0.9.4-final.zip` **35,641,669 B**, SHA256 `124C5FF469FC93D92903FA5FD2C34AAD204A62A5C79C031534CB3E6CE7952538` → `dist\VampireHuntersVR-0.9.4-final.sha256.txt` |
| 미검증 | **헤드셋 미검증** — 인트로 동안 VR이 시작되지 않고 메뉴에서 정상 화면이 나오는지는 착석 1회로만 확정. VR 시작이 이전보다 **늦어지는 것이 의도된 수정**이다 |
| 알려진 한계 | ① 릴리스 `install.ps1`은 재설치 시 설정 파일을 템플릿으로 덮어쓴다(사용자 EyeScale 등은 사전 백업 필요 — 이번 배포는 DLL 교체로 회피). ② 인트로 씬 이름이 게임 업데이트로 바뀌면 `StartupSkipScenes`도 갱신해야 한다(정착 게이트가 2차 방어). ③ 정착 게이트는 최소 1초를 더한다(`StartupSettleSeconds=0`이면 D-198 동작). |

## 0.9.6 — 스냅 턴 3택 (끄기 / 30° / 45°) 실제 시야 회전, 헤드셋 확인 (2026-10-22)

| 항목 | 값 |
| --- | --- |
| 소스 커밋 | `005781e` (D-211) + `047a24f` / `36a7b46` (배포 기록) |
| 플러그인 | `VampireHuntersVR.Stage0.dll` 440,320 B / SHA256 `129361927619F42C3A625D99EB85EB04B781DF981690C104D17FB4F35BE85F2F` |
| 네이티브 | 0.9.5와 동일 (`VhvrNativeHost.dll` 339,456 B / `E6C9BFD41E1E536087C945563F4751FC3EF2C8A7426F6F1D5438B32A9B3DC516`) — 변경 없음 |
| Extras | 0.9.5와 동일 (`VhvrExtras.dll` 54,272 B / `B4AD024D6908D52C76FA8F054A34D40E4C0ED9780C045731EA8B3F044F2FA3C1`) — 변경 없음 |
| 설정 도구 | `VhvrSettings.dll` 45,056 B (스냅 턴 라디오 3개 + 미리보기), `--selftest` / `--form-selftest` PASS |
| 오프라인 검증 | `tests\HandAim` 238/238, `tests\Settings` 13/13, 플러그인 빌드 경고 0/오류 0 |
| 헤드셋 검증 | **통과** — 30° 회전, 몸(위치) 고정, 손·무기·리티클 동반 회전. 45°/끄기는 미착용(같은 경로의 다른 값) |
| 패키지 | `dist\VampireHuntersVR-0.9.6.zip` **35,666,676 B**, SHA256 `25FC057BB9B20FB7C1E23AF8E80C59ED9B39733A997DC5896E38E680EA8344E7` (`scripts\pack-release.ps1 -Version 0.9.6`, 0.9.5 ZIP 기반). 설치→237파일→제거 왕복 PASS |
| 배포 | 로컬 설치본은 D-211 배포본 그대로(패키지 DLL과 동일 SHA). GitHub Release `v0.9.6` — [릴리스](https://github.com/alsdud95920-sys/VHVR-vampire-hunter-VR-mod/releases/tag/v0.9.6) id `419911553` (draft 아님), ZIP digest `sha256:25fc057bb9b20fb7c1e23af8e80c59ed9b39733a997dc5896e38e680ea8344e7` = 로컬 해시 |



## 0.9.5 (라이브 설치 완료 / 헤드셋 미검증 / zip 미생성) — 메뉴 화면·레이저를 HMD 정면 프레임으로 통일 (D-201, 2026-10-18)

| 항목 | 값 |
| --- | --- |
| 출발 | 0.9.4 착석 보고: "시야가 오른쪽으로 90도 꺾여 있음" + "UI ray가 이상함" → 원인 확정(패널이 게임 카메라 포즈로 동결 + 레이저가 다른 프레임) |
| 소스 변경 | **extras만**: `Features\MenuScreenRig.cs`(`TryGetLiveHeadPose` 신규, 배치 로그에 headYaw/panelYaw/headPanelDelta, `Recenter`, `ConsumeRecenterRequest`, 리센터 코드), `Patches\MenuScreenPatch.cs`(패널 입력을 라이브 헤드 포즈로 교체 + `PointerAnchor`), `Policies\PoseMathPolicy.cs`(HeadFrameRotation/MenuPointerAnchor/YawDegrees/ForwardAngleDegrees), `ExtrasConfig.cs`(2키). **Stage0 DLL·config 무변경** |
| 새 설정 | `[MenuScreen] PointerHeadFrame=true`(false = 0.9.4 동작, A/B용), `[MenuScreen] RecenterChord=true`(양쪽 스틱 동시 클릭 = 리센터) |
| 빌드 | extras Release 경고 0 / 오류 0. `VhvrExtras.dll` **53,760 B** SHA256 `52E6707290D430F25615CF50EFAA7ADA3013DD21527FB7861D999BFDDB1CA533` (0.9.4: 49,664 B `09F017418D8E3F72…`) |
| 오프라인 검증 | `tests\VhvrExtras` **72/72**(신규 10 — 0.9.4 실측 수치를 회귀 fixture로 고정), GameplayCamera 38/38, HandAim 190/190, WeaponRuntime 67/67, FlameEndpoint 138/138, FlameEmitter 55/55, StereoMath 29/29 |
| 라이브 설치 | 게임 폴더 `BepInEx\plugins\VhvrExtras.dll` 교체(설치본 SHA = 빌드 SHA). 직전 DLL은 `backups\D-201-20261018T111028Z\` 보존 |
| 미검증 | **헤드셋 미검증** — 판정: 로그 `menu ui: panel frozen at world (...) headPanelDelta=0.0deg` + 메뉴가 정면 중앙 + 레이저가 컨트롤러 방향 |
| 게이트 결함 | `tests\verify.ps1`이 `tests\Transaction.Tests.ps1`(160~194행 유실) 파서 오류로 65행에서 중단 → 이후 게이트 미실행(ENV-012). 이번 검증은 스위트 직접 실행으로 대체 |
| 패키지 | `dist` zip **미생성** — 프로젝트 규칙(헤드셋 통과 후 확정)에 따라 착석 확인 뒤 0.9.5로 패키징한다 |

