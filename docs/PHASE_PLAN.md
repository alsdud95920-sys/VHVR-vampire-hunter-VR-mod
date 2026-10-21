## Status — Vampire Hunters VR 0.9.6 — 2026-10-22
Mode: agentic | Gate: 소스 공개(GPL-3.0-or-later) ✅ | D-211 헤드셋 확인 ✅ | 헤드셋 수락(전체 항목) OPEN

✅ **0.9.6 공개**: 0.9.5 구성에 D-211 스냅 턴(끄기/30°/45°)을 더했다. 헤드셋에서 30° 회전·제자리 고정·손/무기/리티클 동반 회전 확인 → BUG-203 CLOSED. NativeHost·Extras는 0.9.5와 바이트 동일.

🚧 **오프라인 빌드 완료·실기기 미검증**: D-202 손 자세, D-206 터렛 입력, D-207 오른손 메뉴 ray/클릭. fixture·로그는 헤드셋에서 보이는 동작을 증명하지 않는다.

⚠️ **크래시 미확정**: 이전 `coreclr.dll` `0xc0000005` 종료는 ABI 버퍼 오버런 후보가 있으나 인과관계 미증명; 수정 뒤 안정성 관찰도 미완료.

✅ **소스 공개 허용 (D-210)**: 자체 소스는 GPL-3.0-or-later(D-209)로 GitHub에 공개한다. 게임 파일·BepInEx/런타임 바이너리·게임에서 생성한 interop은 저장소에 올리지 않는다. 작성자 로컬 경로는 플레이스홀더로 치환했다.

▶ **Next**: 남은 착석 항목(D-202/D-206/D-207, 45°/끄기)을 기록하고, 장시간 세션에서 크래시 재발 여부를 관찰한다.

---


## Status — 2026-10-19 (D-207) — 메뉴 오른손 레이 + coreclr 크래시: Extras 손 ABI 미러 8바이트 누락
Mode: agentic | Gate: 오프라인 수정·배포 ✅ | 헤드셋 시각 확인 🚧 | 크래시 인과관계 최종 확인 🚧

✅ **원인 확정 (오른손 ray)**: 네이티브 `HandPose=64 B`에 `Squeeze@60`을 추가했지만 `MenuScreenRig`은 60 B 미러를 유지했다. 그 결과 네이티브 `Right` 시작(80)이 Extras의 `Right` 시작(76)과 달라 `Right.Active`가 왼손 squeeze를 읽었다(보통 0). `ReadHands` 반환 구조체도 136 B로, native 144 B 출력보다 8 B 작았다. `ArmHaptics`는 `ReadHands`를 직접 부르진 않지만 `HapticAppliedCount()`로 같은 잘못된 status를 읽어 delivery 확인에 사용했다.

✅ **수정·오프라인 검증·라이브 설치**: Extras에 네이티브와 같은 64/144 B ABI 미러와 크기/오프셋 fail-fast guard 추가. `VhvrExtras.Tests` **78/78**, Stage0/native source와 offset 교차 확인, Extras Release 빌드 경고 0/오류 0. `verify.ps1`의 옛 72개 고정 식별 문구도 갱신. 설치본/build SHA `B4AD024D6908D52C76FA8F054A34D40E4C0ED9780C045731EA8B3F044F2FA3C1`, 54,272 B. 기존 DLL SHA `52E6707290D430F25615CF50EFAA7ADA3013DD21527FB7861D999BFDDB1CA533`는 `artifacts\\d207-install\\20261018T155649Z\\`에 두 사본으로 보존; 설치 영수증 `receipt.json`.

⚠️ **크래시**: Windows Application Error 1000 기록은 `coreclr.dll` / `0xc0000005`, 2026-10-19 00:36:33 (Windows 현지 시각). 같은 읽기-출력 버퍼의 8바이트 과소할당은 메모리 손상과 일치하는 강한 후보지만, 이벤트에는 호출 스택이 없어 이번 크래시의 인과관계는 아직 증명되지 않았다. 마지막 Extras 로그의 pulse는 `RequestHaptic -> 0 (queued)` 정상 반환이며 원인으로 단정하지 않음.

🚧 **헤드셋 미확인**: 새 메뉴 ray의 오른손 표시/가리킴 및 장시간 세션에서 크래시 재발 여부는 아직 보지 않았다. 코드/fixture 통과는 인게임 동작이나 크래시 해결을 증명하지 않는다.

▶ **Next**: 게임을 한 번 실행해 `docs\\playtests\\PLAYTEST-D207-MENU-RAY-KO.md`의 한 자리 체크를 수행하고, 오른손 ray와 메뉴 클릭 및 세션 결과를 보고한다. 크래시가 재발하면 새 `Application Error 1000` 시각과 직전 `LogOutput.log`를 보존한다.

---


## Status — 2026-10-18 (D-202) — 인게임 손/조준 90° 꺾임 = 손만 게임 카메라 프레임을 곱하고 있었다
Mode: agentic | Gate: 헤드셋 수락 OPEN (D-201 메뉴 통과 · D-202 손 미검증) | BUG-203 스냅 턴 OPEN | D-197 SteamVR mirror OPEN

✅ 메뉴 수정 통과 (D-201, 사용자 확인)   사용자: "메뉴는 잘 나오는데" — 로그도 일치(`headYaw == panelYaw`, 즉 `headPanelDelta = 0.0deg`)하고, **리센터(양 스틱) 실제 사용 3회**가 기록됐다(`menu ui: recenter (both thumbsticks) re-placed the panel in front of the head`) → D-201의 A1/B1은 통과로 판정.

✅ 원인 확정 (BUG-202)   같은 20:31~20:32 실행의 영수증이 수치로 일치: 눈 `view.orientation = (0,0,0,1)`(D-195) vs 손 합성 `anchorRotation` = **yaw +90°**(19:57은 −90°) vs `muzzleToHandForwardDegrees = 0`. D-195가 적은 "HandAim 손 IK 영향 없음"이 틀렸고, 갈라진 두 프레임의 각도가 곧 사용자가 본 90°다.

✅ D-202 소스·설치   신규 config `HandPoseFrame`(기본 `Head` = 눈과 같은 프레임 / `Anchor` = 0.9.4 A/B) + 손 조준·무기 회전·**rest 캡처**를 한 프레임으로 통일 + 헤드 프레임에서 스냅 턴 거부(경고 1회). Stage0 **경고 0/오류 0**, DLL **430,592 B** / SHA `34AA247600E3DEA20895F77A49A005B296B950CCFE43EA1483619ECB765DE525`(**설치본 SHA = 빌드 SHA**, 직전 `102E765D…`는 `backups\D-202-*` 보존). 회귀: HandAim **203/203**(+13), GameplayCamera 38/38, WeaponRuntime 67/67, VhvrExtras 72/72, FlameEndpoint 138/138, ShotScopes 28/28.

🚧 헤드셋 미검증   손이 실제로 컨트롤러를 따라오는지는 착석 1회로만 확정된다 → `docs\playtests\PLAYTEST-D202-HAND-FRAME-KO.md`. **이 빌드에서 스냅 턴은 동작하지 않는다**(BUG-203, 헤드 프레임에서 거부 + 경고).

▶ Next   ① 착석 1회 → A/B 판정(Q-D202-HEADSET) ② BUG-203 헤드 프레임 스냅 턴 구현 ③ 공개 소스 트리 재생성 후 첫 커밋(로컬 git 신원 대기) ④ ENV-012(verify.ps1 aggregator aborts at the Transaction suite) 복구.

❓ Need from you   ① 착석 1회(약 5분). ② 공개 트리 커밋용 git 신원(이름/이메일) 결정.


## Status — 2026-10-18 (D-200) — 0.9.4 시작 씬 게이트: 인트로에서는 VR 시작 금지
Mode: agentic | Gate: 헤드셋 수락 OPEN | D-197 SteamVR mirror OPEN | D-200 헤드셋 재검증 PENDING

✅ 원인 확정   0.9.3 검은 화면 = `Intro` 씬에서 세션 시작(0.6초, `sourceCamera='Main Camera'`) → `MainMenu_2` 전환에서 그 카메라 파괴 → 메뉴 분기의 조기 반환이 D-181 재채택을 영구히 가려 복구가 없음 → `windowFrames` 134에서 정지한 채 XR은 495프레임 제출. 0.9.2는 15.3초·`ArenaCamera`라 정상이었다.

✅ D-200 소스·릴리스 적용   `StartupSkipScenes`(기본 `Intro`) + `StartupSettleSeconds`(기본 1.0) + `AdoptMenuCameraIfNeeded()`를 조기 반환 **앞으로** 이동. Stage0 **경고 0/오류 0**, DLL 428,032 B / SHA `102E765DF9DF00726332CF12050DB597560EA097E00E0BD7F021C8488234D5AE`. GameplayCamera **38/38**(신규 `source-startup-skips-intro`·`source-startup-scene-settle`·`source-menu-adopt-before-return`), 회귀 HandAim 190/190·WeaponRuntime 67/67·VhvrExtras 62/62·FlameEndpoint 138/138.

✅ 라이브 설치   게임 폴더 Stage0 DLL 교체(설치본 SHA = 빌드 SHA). 직전 0.9.3 DLL은 `backups\D-200-20261018T104445Z\` 보존. 사용자 설정(EyePreset custom·EyeScale 1.5·uiScale 1·FovScaleX 0.85·GameplayWaitSeconds 600)은 유지하고 새 키 2개만 추가.

✅ 0.9.4 배포본   `dist\VampireHuntersVR-0.9.4-final.zip` 35,641,669 B, SHA256 `124C5FF469FC93D92903FA5FD2C34AAD204A62A5C79C031534CB3E6CE7952538`.

🚧 헤드셋 미검증   "인트로 동안 VR 미시작 → 메뉴에서 정상 화면"은 착석 1회로만 확정된다. 수정의 부작용으로 **VR 시작이 이전보다 늦어진다**(의도).

▶ Next   ① 착석 1회 → `run\host.json`의 `startupWait`/`scene`/`sourceCamera`/`startupReadySeconds` 확인 ② D-197 SteamVR mirror ③ 공개 트리 첫 커밋(로컬 git 신원 설정 후).

❓ Need from you   착석 1회. 그리고 릴리스 재설치 시 설정 파일 덮어쓰기 정책(Q-D200-CONFIG-RESET) 결정.

## Status — 2026-10-18 (D-198/D-199) — 0.9.3 배포본 + 공개 소스 트리
Mode: agentic | Gate: 헤드셋 수락 OPEN | D-197 SteamVR mirror OPEN | D-198 체감 확인 PENDING

✅ D-198 적용·설치·해시 일치   Stage0 고정 15초 warm-up 제거 → 적응형 readiness 게이트(`StartupReady()`: 메인 카메라 + D3D11 존재 시 그 프레임에 초기화, 대기는 `GameplayWaitSeconds` 예산 + `STARTUP_WAIT`/`waiting.json` 영수증 + `STARTUP_READY elapsed=` 1회 로그). 설치본 SHA `D1655D505DBA136110471955B1AC2DF67753C882701AC295A91DD2F854C21175` = 빌드 산출물 = 0.9.3 스테이지. 소스 가드 4종 PASS, GameplayCamera 35/35.

✅ 0.9.3 배포본 완료   `dist\VampireHuntersVR-0.9.3-final.zip` (35,639,241 B, 254 엔트리, SHA256 `66125C770C82710E62355F4233BD6CF5EFBF9C56C0D59CF98B8CA684079BFA24`). 게임 원본 파일 0건, 플러그인 해시가 빌드 산출물과 일치.

✅ 공개 소스 트리 생성 (D-199)   `<RepoRoot>` — 266 파일 / 4,455,136 B, GPL-3.0(`LICENSE` 35,149 B, gnu.org 원문). 게임 파생 IL2CPP interop·BepInEx 바이너리 0건, 작성자 로컬 경로 0건(문서·스크립트 63개 살균, 플레이스홀더 뜻은 `docs\PUBLISHING.md` 에 공개). 공개 트리에서 `tests\GameplayCamera` 빌드 + 35/35 PASS(D-198 가드 포함)로 자기완결성 확인.

🚧 헤드셋 미검증   D-198 체감(시작 즉시 VR 전환 여부)과 D-197 SteamVR mirror 는 사용자 실행 1회가 필요하다. VR 수락 게이트는 여전히 OPEN.

▶ Next   ① 사용자 실행 1회 → `run\host.json` 의 `startupReadySeconds`/`startupWait` 확인(1~3초면 정상, 10초 이상이면 게임이 카메라를 안 만든 것) ② 공개 트리 `git init` + 첫 커밋(로컬 git 신원 미설정 → 사용자 설정 후) ③ D-197 SteamVR mirror 조사.

❓ Need from you   실행/착석 1회. 공개 트리 게시 여부와 `docs\`(역설계 조사 노트 포함) 게시 범위 결정.


## Status — Vampire Hunters VR 0.9-final — D-195 SteamVR view-flip + card rotation patch
Mode: agentic | Gate: headset acceptance OPEN | D-193 visual flame gate OPEN | D-195 HMD re-verify PENDING

✅ Confirmed failure mechanism   0.9-final install defaults set `MenuFirst=false`, so at `MainMenu_2` Stage0 waited for a gameplay camera (`WAITING_FOR_GAMEPLAY_CAMERA`, candidates empty) and did not initialize the OpenXR host. This contradicted the explicit D-190 requirement for VR at the main menu.

✅ Corrected and packaged   Restored `MenuFirst=true` for the 0.9-final installer, Extras binding/config, and installed host config; corrected stale README/ModNotes, removed premature host-config mutation from the Extras prefix. Extras Release build 0 warnings/errors; Extras fixtures 62/62 pass. ZIP guard and archive contents pass (254 entries). SHA256 `0B3080FBBF04646BA4D008DD820BB3C1B379B06FEFA935427057B8F265CC06E3`.

🚧 Headset verification OPEN   No game launch after correction. Main menu VR and stage-start handoff remain unproven until user-run headset session. Prior launch log/receipt proves only wait-at-menu regression.

▶ Next   User: say "실행" when ready; agent starts through Steam, then checks menu VR and stage camera handoff receipts/logs.

⬜ Deferred   Non-Quest controller profiles, per user's request.

✅ Recovery note   Earlier `uninstall.ps1 -Force` had reported removing 237 files, but the current installation was later present again with a new run log. Rechecked its active manifest and updated only the mod config/DLL; previous user-edited config files were backed up first. Exact origin of the intervening install isn't assumed.

✅ D-193 offline   FlameEmitter 55 fixtures PASS; Stage0 Release build 0 warnings/errors; Extras and Settings Release previously 0 warnings/errors. D-194 Extras Release build 0 warnings/errors, 62 policy fixtures PASS; ZIP current and SHA-verified.

✅ Package   `dist\VampireHuntersVR-0.9-final.zip` (35,635,823 bytes; SHA256 `0B3080FBBF04646BA4D008DD820BB3C1B379B06FEFA935427057B8F265CC06E3`).


✅ Built and tested   D-175: `extras\VhvrExtras` 빌드 경고0/오류0 (`VhvrExtras.dll` 39,936 bytes, SHA256 `EF8B9735…`), `tests\VhvrExtras` 60/60 PASS, `dist\tools\VhvrSettings.exe` 재빌드(셀프테스트/폼셀프테스트 PASS, `--ui-scale` 왕복·거부 확인), 페이로드에 플러그인 포함.
🔧 Built, not yet accepted   요청 5기능 전부: 후킹 적용(PATCH_APPLIED) + 오프라인 정책 60픽스처, 그러나 **동작은 미검증**.
🚧 In progress   없음(구현·패키징 완료, 착석 대기). `MenuFirst`는 기본 OFF — 켜면 그 세션은 손/무기 통합이 설치되지 않는 평면 릴레이.
⬜ Not started   착석 수락 1회: ① 메뉴 패널(검정+1.5m) ② 레이저 선택 ③ HUD 1.5배 ④ 느린 무기 자동 연사 ⑤ 좌Y/우B 기술 ⑥ 쿨타임 햅틱.
❌ Dropped approach   소스 복구(D-174 경로 C에서 중단), `MenuFirst` 기본 활성화(게임플레이 hand aim 손실), 무기별 반자동 하드코딩 표.
❓ Need from you   착석 1회 시간(6항목 배치). 실험용 `MenuFirst=true` 허용 여부(그 세션은 손/무기 없음).
🐞 Open bugs   TOOL-086(착용 수락 대기). TOOL-088 신규: src 트리 손상으로 src 기반 픽스처는 여전히 실패(D-175에서 `RenderResolutionPolicy.cs`만 복구).
▶ Next   **실측 준비 완료 (READY_PREINSTALLED_NOT_LAUNCHED).** 재빌드 Stage0(MenuFirst) `090ADF4E…` + Extras `32F9C600…` 설치, cfg `MenuFirst=true`. 착석 1회로 `docs\playtests\PLAYTEST-MENUFIRST-KO.md` 7항목 판정 → 통과 후 배포파일 확정. 로그 `MENU_FIRST gameplay camera acquired` 확인.

### D-178 — 소스 복구 + MenuFirst 소스 구현 (2026-10-15 새벽, 자율)
- **소스 복구**: `src\core` 빌드 **538 오류 → 0 오류/0 경고**. 배포 D-171 DLL을 `ilspycmd`로 원본 복원(OpenXrHost/OpenXrHostConfig/**HostNative** 신규, **HandPose** 신규, HandInput/HandAim/HandFlameEmitter/csproj 수정). TOOL-088 해결.
- **B 실현 불가 확정**: `Initialize()` 일회성 + 손 통합이 GameplayOnly 경로 전용 → 재호출/트랜스파일은 고위험.
- **MenuFirst 구현**: `MenuFirst` 설정, 검증 완화, `Update`에서 게임플레이 진입 시 `TryEnterGameplay()`→`InstallHandAim()`→`GameplayOnly=true`.
- **미배포**: 재빌드 `A2E2F240…`(411,648B)는 dist에 넣지 않음. 배포/설치본 = D-171 `E5129D81…` 불변. 회귀: extras 63/63 PASS.
- **한계(정직)**: 복원본 게임/헤드셋 미검증, `OpenXrHost.cs` 주석 없음, `InstallHandAim` 의도적 중복, extras MenuFirst 전처리 정리 필요.

### D-177 — 자동연사/로봇팔 햅틱/영수증 수정 구현 (2026-10-14)
- **#5 자동연사**: 무기별 판별 대신 **모든 총기**에 트리거 유지 중 **0.6초 간격** 발사 신호(`SemiAutoFirePolicy`, `RetriggerSeconds` 0.5→0.6, `RetriggerMaxFireRate` 제거). 홀드 시작은 타이머만, 게임이 이미 발사한 틱은 타이머 재시작(연사 무기 중복 없음).
- **#2 햅틱**: **로봇팔 기준**(좌팔↔좌손, 우팔↔우손), `ArmLPresses`/`ArmRPresses` 증가 = 발사. 쿨다운 **모델링** `[AbilityHaptics] ArmCooldownSeconds`(기본 0.5).
- **오프라인**: 빌드 0/0, `VhvrExtras.dll` **38,912** `32F9C600…`, fixtures **63/63**(verify 기대 60→63), zip **250/35,594,914** `96C7A7DC…`, 게임 재설치 235. TOOL-090은 실제 로그로 재검증.
- **미검증(정직)**: 전부 헤드셋 미확인. 로봇팔 쿨다운은 모델링, 자동연사는 알약 소모(D-110) 상호작용 미확인. #4 보류(TOOL-089).
- **소스 복구 실측**: 오류 538개가 `OpenXrHost.cs`(526)·`HandInput.cs`(12) **2개 파일 구문 손상**. `ilspycmd` 디컴파일 참조 확보 → 복구 추정 **1~2일**.

### 준비 점검 — 착석 전 재설치 (2026-10-14, D-175 후속)
- **발견(NOT READY)**: 게임 폴더에 구식 페이로드(Stage0/NativeHost/openxr_loader만, 해시는 dist와 일치)가 남아 있었고 **`VhvrExtras.dll`(5기능)이 미설치**. `headset-extras.ps1 -DryRun` 실패로 확인.
- **조치(번들 경로, 되돌림 가능)**: `uninstall.ps1` → **removed 234 files** + 스톡 복원 확인(모드 소유 루트 0) → `install.ps1` → **INSTALLED 235 files**(+1=`VhvrExtras.dll`). OpenXR 런타임 VDXR 자동 채택.
- **검증**: 4종 플러그인 설치본=배포본 해시 MATCH(`VhvrExtras.dll` 39,936 bytes sha256 `EF8B9735…`), manifest 0.6-final, `headset-extras.ps1 -DryRun` 정상(6항목 NO_LOG — 거짓 PASS 없음).
- **남은 한계(정직)**: `headset-handaim.ps1 -UsePreinstalled`의 EntryTransaction 페이로드에 `VhvrExtras`가 없고 `Install-Stage0`은 사전 상태가 정확한 스톡일 것을 요구 → **두 경로 상호배타적**. D-175 착석은 번들 경로 + 수동 Steam 실행이며, 자동 모니터/스톡복원은 없어 정리는 `uninstall.ps1`. 자동 하네스를 쓰려면 `VhvrExtras.dll`을 EntryTransaction에 편입하는 별도 작업 필요(미착수, 사용자 결정).
- **READY_PREINSTALLED_NOT_LAUNCHED.** 게임 미런치. 영수증: `docs\playtests\PREP-EXTRAS-KO.md`.

### D-169 — 0.4m 실행: 빈 가고일 표시 0, 포션/상자 화살표 동작 (2026-10-13)
- 사용자 `실행` 1회. headset PASS 18:48:08, gameplay lock 18:48:49, gamePID 47836/harnessPID 59220, 세션 약 2분(Maze_Small), endReason=level-exit, UninstallExit0, stockVerified, VDXR 유지.
- **핵심 증거**: `statues=6, statuesWithoutPotion=5, hangingPotions=1, candidates=1, shown=1`(HEALING@ring, pos 91.7,5.5,100.1, holder `Gargoyle (4)`) ↔ 포션이 없을 때 `statuesWithoutPotion=6, hangingPotions=0, shown=0`. 상자 구간에서 `chestTargets=1, shown=1, CHEST@ring`.
- 즉 **빈 가고일은 한 번도 표시되지 않았고**, 표시 개수가 실제 포션/상자 존재를 따라갔다.
- 미확정: 사람이 화살표를 어떻게 보았는지(위치·벽 뒤·원거리·HUD). 이름/카운터만으로 시각 수락을 주장하지 않는다.

#### D-169 — 0.4m 실행: 빈 가고일 표시 0, 포션/상자 화살표 동작 (2026-10-13)
- 사용자 `실행` 1회 실행. headset attached 18:48:08Z, gameplay lock 18:48:49Z, gamePID47836/harnessPID59220, Maze_Small 약2분, endReason=level-exit, UninstallExit0, stockVerifiedTrue(121files/8dirs/7,726,223,158bytes/hash+write times), runtimeBefore=runtimeAfter=VDXR, error/cleanupError null. 캡처 요청 없음(TOOL-087 회피).
- **핵심 증거(직접 관측한 상태들)**: ⓐ `statues=6, statuesWithoutPotion=5, hangingPotions=1, candidates=1, shown=1`(sample `HEALING@ring`, potionSample `hanging-model#HEALING#at=Potion#pos=91.7,5.5,100.1#r=1#holder=Gargoyle (4)`) ⓑ 포션이 없어진 뒤 `statues=6, statuesWithoutPotion=6, hangingPotions=0, candidates=0, shown=0` ⓒ 상자 구간 `chestTargets=1, candidates=1, shown=1`(sample `CHEST@ring`).
- 따라서 D-166의 요구(빈 가고일 0, 실제 포션/상자만)는 **기계적으로 충족**됐다: 가고일 6개 중 표시는 언제나 실제 포션이 있는 1개뿐이었고, 포션이 없으면 0이었다.
- 리시트 확인: `zTestForced=True`, `blendForced=True`, `shader=Hidden/Internal-Colored`, `shape=arrow`, alpha0.5/ring0.35, `distanceLimited=False`, `maxDrawDistance=100`, `sizeCapped=False`, error null.
- **미확정(정직)**: 사람이 화살표를 어떻게 보았는지(포션 바로 위인지, 벽 뒤에서 보였는지, 먼 거리에서 보였는지, 상자 화살표 위치, 데미지 화살표/전면 HUD 유지)는 아직 사용자 보고가 없다. 카운터/샤더 플래그만으로 시각 수락을 주장하지 않는다.
- 다음: 사용자 시각 보고를 받아 이 항목들을 판정하고, 어긋난 대상·위치가 있으면 그 증거로만 수정한다. TOOL-086은 보고 전까지 OPEN.
- **READY_PREINSTALLED_NOT_LAUNCHED**: 232files / plugin `594A77E0B9D53FBBF9863DB35AC29B7BB95BD13050013905B8C8108A6DB1590E` / native `F7FDF2D61F0EB8703672704216CA1FC08B75FD9D999D468E94546E8576E74BDC` / VDXR 유지. Installed seq14, 독립 verify PASS, live=build=pin, gameLaunched=false, source f995415.
- 설정: CHEST,HEALING,POWER_UP,BOSS_CHEST / 거리0(무제한) / count32 / PrepareAhead+DeferredGpuWait / EyePreset normal.
- 실행 명령(사용자 지시 후에만): `& '<RepoRoot>\tests\headset-handaim.ps1' -Tag 0.4m -DisableVSync -RenderFrames 1 -EyePreset normal -PrepareAhead -DeferredGpuWait -UsePreinstalled -HeadsetWaitSeconds 900`
- 확인: 포션 걸린 가고일=포션 위 초록 화살표 / 빈 가고일 화살표 없음 / 포션 먹으면 사라짐 / 상자 위 노란 화살표 / 벽 뒤·멀리서도 보임 / 시야 밖은 링 방향 화살표 / 데미지 화살표·HUD 그대로. 캡처 요청 없음(TOOL-087).

### D-167 — 0.4l 실행·포션 앞 진단 및 캡처 요청 실수 (2026-10-13)
- 사용자 `실행`으로 1회 승인. 08:58:16Z headset PASS/Steam 실행, gamePID74928/harnessPID51508, 08:58:53Z Maze_Small gameplay lock, 양안 제출·controllersAvailable=True 확인. 재설치/재빌드/런타임 변경 없음.
- `나 포션앞` 신호 후 `<RepoRoot>\artifacts\headset-d087\0.4l\evidence-potion-20261013-180148`에 요청 전 hand/host 진단 보존. scans348→416, shown0/chestTargets0. HealthPoint/Potion id214560 live=True/renderer=on/pos135.47,5.47,57.68/pickup=none; 다른 5개 샘플은 부모 HealthPoint 비활성으로 live=False. 이름/renderer만으로 실제 식별 또는 소진 신호를 확정하지 않음.
- **캡처 실패는 에이전트 요청 실수**:09:02:23.833Z `capture.request`에16601 작성; native RequestCapture 허용0–10(Host.cpp385), return2→managed capture request rejected→HOST_FAILED. 요청 미소비, raw/양안 캡처0. 09:02:29Z harness 자동 제거 시작→09:05:34Z 완료:UninstallExit0/Recoveredseq27/stockVerifiedTrue/121files/8dirs/7,726,223,158bytes/hash+write times 일치/errors null/VDXR 유지. 원복 성공은 캡처 성공 아님. 수집 후·상자·벽 뒤·HUD 시각 수락 미수행.
- **0.4l CONSUMED_NOT_READY**:아래 READY는 실행 전 이력이며 실행 명령은 폐기. 추가 실행 승인 없이 새 세션을 시작하지 않는다.









## Stage 1 — Camera and 6DoF
- [ ] Camera/player mapping proven by visible response
### D-078 M4b host brief (2026-10-08)
Goal: bind OpenXR to the game's actual D3D11 device and copy both game-camera targets into acquired runtime-owned images, with GPU completion before release.
Unknowns: runtime adapter match; retained Unity render callback ordering; format family and orientation; live composed scene disparity.
Hypothesis: ID3D11Texture2D::GetDevice obtains the retained Unity device; typed Windows SDK interfaces avoid TOOL-012's invented vtable slots. GL.IssuePluginEvent runs native copies on Unity's render thread. An explicit D3D11_QUERY_EVENT bounds GPU completion before OpenXR release.
Plan: build a small first-party native bridge using the already pinned official OpenXR SDK headers/loader and installed Windows SDK; self-test the exact copy/query path; transactionally deploy opt-in host; capture actual simulator Both output with current-run progress and provenance; add live eye pose/render scheduling and baseline/lean/yaw proof before acceptance.
Verify: native self-test + build/config/lifecycle checks, runtime receipt >=120 composed game frames plus measured +3 s hold, independent matrices and actual simulator captures, request-exit -> STOPPING -> end-session, stock hashes/times restored.
Risk: native failure can crash the game; all waits bounded, backup transaction retained, global VDXR unchanged, no private saves or anti-cheat/DRM work. A copy/session receipt alone is not M4b PASS.

### hd5 acceptance — 2026-10-08
- Build under test: `9861b63`. Harness `artifacts/sp5-m2-vdxr-headset.hd5.json`: `M2_VDXR_PASS_D2`, 19.2 s measured whole-command wall time (the prior ~12 s was an estimate), stock verified (121 files).
- Evidence preserved as `artifacts/openxr/probe/hd5-d1.json` and `hd5-d2.json` so later generic rung paths cannot overwrite this sitting's receipts.
- D1: `VirtualDesktopXR`, Quest 3, two 2428x2440 views, format 28, required adapter matched, 300 submitted / 301 attempted, one empty startup frame, zero discarded/invalid-pose/frame/wait/locate failures, session READY -> FOCUSED.
- D2: 60 submitted / 61 attempted, `ImagesCleared=True`, zero failures/discards/invalid poses, typeless texture with explicit `R8G8B8A8_UNorm` RTV accepted. Each successful submitted frame passed `xrEndFrame`.
- User's exact observation: **"왼쪽 빨강, 오른쪽 눈 파랑 맞았는데, 너무 짧았긴 해, 다음부터 3초 더 늘려"**.
- [x] M2 own client, HMD system, two views, swapchain format and valid submitted layers.
- [x] M2 >=300 successful submissions, confirmed on VDXR after D-074.
- [x] M2 clear path succeeds and user verifies LEFT RED / RIGHT BLUE.
- [x] M3 composed-eye screenshot: D-076 `artifacts/openxr/probe/m3-auto1/summary.json` PASS; actual simulator Both viewport, normal/swapped/repeat with real exit codes and +3 s active hold. Runtime registration unchanged. M3 is not Stage 1 passed.
- [ ] M4b game-camera stereo in runtime images (not proven by diagnostic flat colours).
- D-075: future C2/D2 visual rungs retain 60 baseline submissions and then actively submit for **3 extra seconds** (`--hold-seconds 3`). No idle sleep, assumed refresh-rate conversion, new headset request, or change to D1/preflight duration.




## Status — 2026-10-08 (D-072): **M2 PASSES headset-free on the simulator** — the stall was our own client, twice

Mode: agentic | Consent: relaxed for reversible game work (D-068) | Headset: available (the user says they can wear it).
Route chosen for this session: **headset-free measurement first** (the user picked it), which then found the fix.

- **The measurement the last session planned was built into the client instead of run once.** Every phase of the first
  frame is now written into the receipt **on disk as it happens** (`Receipt.LastPhase` + `FrameLoop.Phase`), so a killed
  process still says where it stopped, and `--watchdog-seconds N` makes a run that stops progressing write that receipt
  and exit by itself. New drill: `tests/tool011-hang-location.ps1` (headset-free, UAC-free, `XR_RUNTIME_JSON`).
- **TOOL-011 is RESOLVED.** The client never hangs in `xrWaitFrame` on the simulator: `waitFrame -> Success after 5-20 ms`
  and the session walks `READY -> SYNCHRONIZED -> VISIBLE -> FOCUSED` **while the loop runs**. Two real bugs in our client
  were the cause, both now fixed:
  1. **TOOL-020** — TOOL-013's gate did `continue` *before* `xrWaitFrame`, so while the session was below VISIBLE the
     client called **no frame function at all** (C1: `300/300` skipped) — and `XR_FRAME_DISCARDED` (a *success* code) was
     counted as a fatal failure, which is what ended the A and B ladders after five frames.
  2. **TOOL-021** — `Eyes.Submit` took its `CompositionLayerProjectionView` **by value**, so every submitted layer carried
     a zero pose and a **null swapchain**: `xrEndFrame` answered `XR_ERROR_POSE_INVALID`, the frame was never ended, and
     every following `xrBeginFrame` was discarded. This is also the real cause of TOOL-013 (whose stated cause was wrong)
     and the reason VDXR rejected frames on the headset runs.
- **Result, headset-free, on the Meta XR Simulator** (`artifacts/tool011-hang-location.m4.json`): `verdict=PASS`,
  `submitted 299/300`, `emptyFrames=1`, `discarded=0`, `invalidPoseFrames=0`, session `1,2,3,4,5`, both with and without
  `--no-state-gate`. The one empty frame is the first, ended with no layer while the session was still READY.
- **M2's own rule** (`docs/playtests/SPIKE-OPENXR-HOST.md`) also wants `frames>=300` and two eyes cleared to different
  colours, so M2 is **PASSED on the simulator's frame-loop clause** and one rung short of its full receipt: the colour
  check needs `--clear` (the D3D11 path, TOOL-012) and the simulator GUI to show the eyes. M2 is **no longer blocked**;
  it is one confirmation rung from complete.
- **Harness bug that cost a sitting: TOOL-019.** `$ladder` and `[ValidateSet()]$Ladder` are the same PowerShell variable
  (names are case-insensitive), so the fourth sitting died at parameter validation before its first rung. Fixed
  (`$rungs`), and the headset-free check now extracts and runs the real table statement for A/B/C/D.
- **Also recorded: TOOL-022** (cosmetic) — `xrEndSession` answers `XR_ERROR_SESSION_NOT_STOPPING`; the probe ends the











## Stage 4 — Movement and comfort
Scope (D-013; Q-015 resolved): retain snap turn only; omit smooth/continuous turn, teleport, vignette/tunneling and optional anti-nausea fades. This is user-selected scope, not a technical fallback. Ordinary stick movement, physical head/body movement, collision/boundary and required transition handling remain; no implementation yet.
- [ ] Ordinary stick locomotion and snap-only turn; configurable snap angle and tested repeat/re-arm behavior
- [ ] Physical movement/crouch/collision; head pose preserved when snap turn changes player yaw
- [ ] User-reported double jump and dash preserve game movement/cooldown/upgrade rules, collision and free head pose; remaining special movement researched, not assumed absent
- [ ] Seated/standing, recenter, boundary respected and required transitions handled; no optional anti-nausea overlays
- [ ] Headset gate

## Stage 5 — Physics hands
- [ ] Frame-independent collision, grab/throw recovery
- [ ] Actual world interactions inventoried and supported
- [ ] Headset gate

## Stage 6 — Weapons and combat (6A–6D)
- [ ] All 14 simultaneous weapon slots preserved, bank mapping explicitly agreed
- [ ] Weapon/ability inventory: muzzle, hit ray/projectile, reload, upgrade flow
- [ ] Q/E auxiliary weapons, primary fire and secondary fire remain distinct (do not conflate auxiliary use with secondary shot); hand aim/origin and cooldown/auto-fire/reload rules verified
- [ ] Frenzy-like blood ability activation, meter/duration/effects and return to normal handled; input/haptics/UI and camera ownership tested without inventing mechanics from names
- [ ] Holsters calibrated; aim independent of head; recoil never moves head


























































































































Session recheck after gear intake (2026-10-06): verifier rerun exit 0 with the same 18/18 tests, zero-warning/error builds and partial recon counts. ZIP bytes/SHA256 independently match DEPENDENCIES.json. Direct invocation of verify-config.ps1 followed by a sentinel printed AFTER_CONFIG_CHILD_REACHED=True: its exit 0 did not skip the invoking script's following commands in the tested PowerShell environment. The full verifier reached its final VERIFIED line after report/hash checks. No code changes or VR gate pass. At that gear-intake recheck, additional PC inventory had NOT been authorized or performed.

Subsequent consented inventory (2026-10-06): user accepted the offered read-only PC/VR scope. Observed Windows 11 Pro build 26200 x64, Ryzen 7 9800X3D, RTX 5080 plus AMD iGPU, installed 32 GiB RAM, Streamer 1.34.22, SteamVR installed/build 25330290, and VDXR default registration for 64/32-bit with both runtime DLLs present. User result confirmation pending. No settings changes, game/mod launch, injection, saves/private documents inspection or XR session test. Stage 0 remains NOT PASSED.

Inventory confirmation follow-up (2026-10-06): user answered "네" confirming presented PC/VR results. Fresh read-only stdout confirms Windows/CPU/GPU/RAM, Streamer version and both VDXR registrations/DLLs. Original consent boundaries unchanged. Fresh full verifier completed in a separately captured process with FULL_VERIFIER_PROCESS_EXIT=0 and final VERIFIED line; tests 18/18, zero-warning/error builds, expected CLI rejection exits and unchanged four game hashes. Logs: `<RepoRoot>\artifacts\confirmation-verifier.stdout.log` and stderr counterpart. Initial direct tool timeout is not a pass; the separate completed run is the evidence. Config child sentinel reached with exit 0, pinned ZIP size/hash match. No loader installed or Stage 0 gate passed. Next: current Vampire Hunters monitor launch and target mod status.

Resume and target baseline answer (2026-10-06): read designated Wizard and project documents; independently confirmed starting HEAD d18cdfd and clean worktree. Read existing confirmation-verifier output: tests 18/18 and FULL_VERIFIER_PROCESS_EXIT=0; no fresh test run claimed. User answered that Vampire Hunters launches normally and no mods are installed on Vampire Hunters. Record as user reports only; no agent game launch, installation, settings changes or private saves inspection. Stage 0 remains NOT PASSED. Next: remaining wizard screen 2 workflow details, then remaining intake/prerequisites before any deployment.

Workflow/preference answer (2026-10-06): starting HEAD d202692/clean confirmed. User reports no coding knowledge, experienced VR play, all operation on the same PC, and no need for anti-nausea/user convenience features. Record confirmed answers, defer test duration and clarify optional comfort scope (Q-015) without inferring smooth-turn defaults or deleting quality/safety requirements. Documentation only; no fresh build/test run, game execution, install, settings/config change or private data inspection. Existing 18/18/full exit 0 remains historical preparation evidence; Stage 0 NOT PASSED.

Snap-only scope answer (2026-10-06): starting HEAD a7d9eb4/clean confirmed. User selected snap turn only; Q-015 resolved, D-013 records actual implementation exclusions rather than disabled defaults. Stage 4/9 and README scope aligned; ordinary movement/core quality retained. Documentation only; no fresh tests/build, turn code/config, game/XR launch, installation, PC settings change or private saves inspection. Stage 0 NOT PASSED; next remaining screen 3 mechanics, not Stage 4 implementation.

Mechanics/consented game-file work (2026-10-06): starting HEAD 2992586/clean confirmed. Q-010 records double jump/dash, Q/E auxiliaries, blood ability and primary/secondary fire; Q-016 authorizes direct file inspection/work. Extended project-local bounded reader with first-occurrence string offsets and seven capped keyword buckets; user reports stored separately in manifest, no invented classes or VR bindings. Fresh full verifier process exit 0, tests 23/23, both builds zero warnings/errors, config JSON/mirrors and CLI exits 2/1 correct; offsets/hash/truncation checks pass. Real metadata: 97,424 segments, 3,920 distinct matching candidates (broader filter, not new game content), 46 undecodable. Bucket counts: jump 32, dash 41, auxiliaries 38, blood 508, fire 133, secondary 95, camera 79; groups overlap/include false matches, latter four sample-capped at 64. Four game hashes unchanged; no loader/game/XR execution/stock writes/private saves inspection. Stage 0 NOT PASSED; no hook confirmed. Evidence: artifacts/mechanics-verifier.stdout.log, stderr counterpart, mechanics-verifier.exit.txt and verification.json; reports local/uncommitted. Next screen 4 posture/hand, then gates/backups/policy/pinning.

Live-run recon (2026-10-06): starting HEAD a8e5e3b with two modified files (RuntimeProbe.cs, csproj). DPI-aware
client capture/input delivered a working click path into real gameplay; the deployed probe DLL hash equals the
project Release build. Read-only extraction of the live log (`ProbeSummary.ps1`, committed checklist
`docs/playtests/PLAYTEST-0-RUNTIME.md`) yields D-033: gameplay look yaw/pitch live on the Main Camera's own local
rotation, the player root stays at yaw 135, the game rewrites the camera transform on up to ~90% of frames, the
Mini Map Camera is an orthographic RenderTexture camera that must not be stereoized, and the time-stopped panels
drop `timeScale` to 0 and release the cursor without changing the canvas count. No VR write, no XR init, no stereo,
head or controller output; no gameplay bug opened. Stage 0 remains NOT PASSED. Open items for the next probe: the
component that writes the camera each frame, the active HUD/time-stopped canvases (v2 shows only the first 12 canvas
lines, all inactive WorldSpace weapon-ammo canvases), and player movement/aim state. Tooling corrections kept:
`ProbeSummary.ps1` first failed on a `Measure-Object` property that lived one level down, and its sub-line regexes
needed `\s+` because the probe indents child lines; both fixed and re-run, no results inferred from the failed runs.

Probe v3 deployed and ready for a plain run (2026-10-06): the game folder was returned to stock, the full verifier
then finished green (`ENTRY_PLAN_VERIFIED: stock=121; loader=228; proposed=229`), and the v3 build was reinstalled
through the transaction engine (`ADDITIONS_INSTALLED`, 229 files, workspace `20261006T131418Z`; deployed hash equals
the Release build). D-034 records the details, including two tooling corrections caught before any result was used:
the first v3 build printed `MonoBehaviour` for every component, and a direct exe launch dies with
- [ ] Head pose late-applied, no game roll/shake/recoil overwrite
- [ ] Correct scale, lean/crouch and all game states incl. death/transitions
- [ ] Dash/jump/blood-ability camera ownership: inspect candidate FOV_TO_ADD_ON_DASH and CameraShake; retain free head pose, no game-driven FOV/roll/shake override
- [ ] No-headset checks + headset gate (the Stage 0 criterion-9 comfort check now runs as PLAYTEST-1 SP9)

### Stage 1 spike brief — UUVR first (D-038; runs only after the Stage 0 gate passes)
Goal: prove or disprove, with visible in-headset evidence, whether the maintained Unity VR framework UUVR
(`Raicuparta/uuvr` v0.3.1 `uuvr-il2cpp-legacy`, plugin id `raicuparta.uuvr-legacy`, GPL-3.0) gives this Unity
2022.3.37f1 IL2CPP build real stereo and 1:1 head tracking, before any from-scratch stereo work is committed.
D-047 corrected the artefact: v0.4.0 is Mono-only, and the only IL2CPP build UUVR ever published targets the legacy
(Unity 2019 or earlier) generation, so generation mismatch is a pre-registered risk rather than a surprise.
Unknowns: does UUVR accept this build (its patcher targets BepInEx be.674, this project pins be.788)? Does its
camera takeover survive the menu (`ArenaCamera`), level-up and death states already mapped in Stage 0? Does the
game's URP chain tolerate its per-eye path? Whether the game stays playable flat with VR off (consent granted by D-043;
behavior still to be observed).
Pre-check already answered (D-040, read-only, no install): this build's metadata **retains the XR module**
(`XRModule`, `XRSettings` with the `StereoRenderingMode` variants, `XrDevice.DisableAutoXRCameraTracking`,
`SetStereoViewMatrix`, `Camera.get_stereoTargetEye`, `InputTracking`), so the API surface UUVR reflects over is
largely intact; `InputTracking.GetLocalRotation` was the one symbol not found by the ASCII identifier scan and is the
first thing the spike must settle. Also fixed in D-040: UUVR applies the head pose as **rotation only** and supplies
no per-eye math of its own, so it shortens the stereo/pose work (~25-35% of Phase 1, at the riskiest end) but
changes none of Stage 1's camera-ownership work.
Hypotheses: H1 the il2cpp-legacy UUVR build injects and creates a stereo camera without a loader conflict, since both are
BepInEx 6 IL2CPP plugins (the loader line differs: UUVR was built against be.692, this project pins be.788); H2 its pose
driver and this project's late-applied pose goal are compatible enough to hand head-tracking to UUVR and keep
hands/IK/weapons here; H0 it fails on this specific game.
Plan: the expected file set is already frozen (`config/uuvr-spike-payload.json`, D-047) and the transaction engine now
supports it (scoped roots, declared generated files, one declared stock mutation restored by recovery; D-048) -> run
`tests/preflight-entry.ps1` read-only -> install -> graded runs (menu only, then one gameplay run) -> restore to stock and
re-verify 121 files. Any UUVR modification - e.g. if the missing `GetLocalRotation` lookup has to be patched over the
surviving XR symbols - is recorded as a modification with notices (D-042), never a silent disable or stub.
Verify: the pre-committed verdict rules in D-038, plus PLAYTEST entries written before the headset session.
Risk: UUVR overwrites its own files on every game start and deletes five named VR plugin DLLs (all absent on this
install, so no effect observed here); GPL-3.0 copyleft no longer blocks bundling (D-042: the mod will be shared complete
with source, so a copyleft outbound licence is accepted and Stage 10 only fixes the licence text, notices and
source-availability duty); the spike must not block or replace this project's own Stage 0 evidence and reversible-removal
guarantees.
Authorization: granted - D-043 (Q-023 closed by agent decision under the user's delegation) allows install, run and
removal plus the `_Data/Plugins/x64` writes for this one spike, strictly after the Stage 0 gate passes and only with the
transaction manifest and verifier updated before installation so `stock=121` stays provable.

**Precedent library (D-041; licenses checked live 2026-10-06, not from memory).** The classes stay apart because reuse
rights differ:
- **MIT - reusable with attribution:** `DrBibop/RoR2VRMod` (Unity IL2CPP + BepInEx; its `VREnabler` copies
  `UnityOpenXR.dll` and `openxr_loader.dll` into `_Data/Plugins/x86_64` and writes the `UnityOpenXR`
  `UnitySubsystemsManifest.json` - the file-drop recipe for a build that shipped without XR plugin files, which is this
  build's situation per D-040; its SteamVR "Desktop Game Theatre off" requirement is a real trap); `Raicuparta/nomai-vr`
  (6DoF + full motion controls; camera-ownership and UI-redirect patterns).
- **Copyleft - reusable because the project will be shared source-included (D-042):** `whateverusername0/VRTRAKILL`
  (Unity IL2CPP, AGPL-3.0) and UUVR (GPL-3.0). Keep notices, record origin/hash/SPDX in `docs/DEPENDENCIES.json`, state
  modifications; the outbound licence becomes copyleft (working candidate AGPL-3.0) and is fixed at Stage 10.
- **Study-only - all rights reserved:** `xPrinny/GunfireRebornVRMod` (Unity IL2CPP first-person roguelite, no licence
  field at all). An absent licence is not copyleft, so D-042 does not unlock it.
- **Index:** `Rhiyo/flat2vr-list` - verify each entry live, it changes. Shipped Unity VR games (Subnautica-class) are
  precedent, not source. None of this answers the two open Stage 0 static items or moves the gate.

## Stage 2 — Stereo
- [ ] Native per-eye path attempted; alternatives logged in DECISIONS
- [ ] Runtime projections, IPD, culling, URP shadows/reflections/post effects correct per eye
- [ ] Frame capture and scale/depth headset gate

## Stage 3 — XR runtime, actions, simulator
- [ ] OpenXR primary viability tested; fallback only by decision
- [ ] Both hands (right-handed only, D-051), action rebinding, dominant/off-hand routing, haptics
- [ ] Distinct actions for jump/double jump, dash, Q/E auxiliary uses, the blood ability, `interact`, and primary/보조발사 (the flat RMB alt-fire) fire; actual routing/hold/release semantics researched before bindings assigned (D-058: the game's own menu labels these 발사/점프/혈능력/보조 발사 and the interact verb 상호작용 = Z; 특수 능력 and 수동 레벨 업 are revealed but UNMAPPED, Q-035/Q-036)
- [ ] D-057 fire split: each trigger carries that hand's main fire (`fire_left_hand`/`fire_primary`, **trigger alone** — no gate) and its 보조발사 (`fire_secondary_off`/`fire_secondary_dominant`, selected by the upper face button Y/B) with per-press latching, and a selector press or release mid-burst fires nothing by itself; both squeezes are unassigned and must do nothing; which slot a 보조발사 touches is a Stage 6B question
- [ ] D-057 buttons and stick: `jump` on right A, `dash` on left X (plain buttons — no threshold), Q/E on the two stick presses (Q tap vs a 1.0 s bug-report hold on the left), `interact` on the right stick **pushed up**, `blood_ability` on it **pulled down**, and `turn` on the stick X as a **30° snap** (thresholds/re-arms from `tuning.interactFromRightStickUp` / `tuning.bloodAbilityFromRightStickDown` / `tuning.snapTurnFromRightStickX`; signs confirmed before the numbers mean anything)
- [ ] Verified current interaction-profile paths and desktop simulator regression
- [ ] Headset gate

## Stage 4 — Movement and comfort











































































































## Sub-stage SP5 — XR-less OpenXR host (opened 2026-10-07, D-064)

#### SP5 brief
Goal: this project - not Unity - owns the OpenXR session and puts the game's own rendering into the runtime's eyes.
Unknowns: whether the runtime's frame loop will start for our client (TOOL-011); whether a runtime-owned swapchain
image can be rendered into from Unity (M4).
Hypotheses: H1 the pinned loader can be vendored and driven from a plain .NET client (M1/M2 - proved); H2 Unity's own
D3D11 device can be obtained through `RenderTexture.GetNativeTexturePtr` without hooking DXGI (M4); H3 the game camera
can be rendered per-eye into the runtime's images through `CreateExternalTexture` (M4).
Plan: M1 sample probe -> M2 our own client -> M3 photograph the composed eyes -> M4 the game's camera inside them.
Verify: `docs/playtests/SPIKE-OPENXR-HOST.md`, headset-free throughout.
Risk: the simulator's frame loop (TOOL-011) and the D3D11OnVulkan compositor - the runtime serves D3D11 through its
Vulkan compositor, so a device on the wrong adapter would fail; the required LUID is matched and that part is measured.

Status:
- ✅ **M1 PASS 2026-10-07** - the Khronos sample from the pinned zip connects to the simulator runtime with D3D11,
  reports an HMD system and 2 views (VERIFICATION.md).
- 🚧 **M2 blocked 2026-10-07** - loader, instance, system, 2 views at 1680x1760, required adapter LUID match, own
  D3D11 device, session, LOCAL space, 9 formats and 2 swapchains x 3 images all measured working; the frame loop does
  not start for our client (TOOL-011), so no frames are submitted.
  **Eleven measured configurations of full parity with the sample** (its exact spaces, four actions over four
  profiles, swapchain order, depth extension and depth swapchains, apiVersion 1.0.63, a 12_1 device, the three
  capability queries we were skipping) plus Meta's `use_batch_mode` CI setting, `ses_texture_format: jpg`,
  `default_refresh_rate: 90`, verbose logging and a fresh GUI: all end at READY with the first `xrWaitFrame` blocked.
  The probe now writes its receipt *before* the frame loop, so a hang leaves evidence (`m2-r1.json` … `m2-r11.json`),
  and the round found and fixed two real bugs in our client (apiVersion literal; `--parity` not enabling depth).
  No headset-free lever is left; the next rung needs the headset and is therefore **Q-042**, not an agent action.
  (Measured 2026-10-07 for Q-042: the same probe against VDXR with no headset attached gets
  `XR_ERROR_FORM_FACTOR_UNAVAILABLE` from `xrGetSystem`, so that rung really does need the headset on.)
- ▶ **Q-042 answered 2026-10-07 (D-066): option (b)** - one bounded headset sitting on M2 against VDXR, under rules
  pre-committed in `docs/playtests/SP5-M2-VDXR-HEADSET.md` and executed by `tests/sp5-m2-vdxr-headset.ps1`
  (preflight -> wait up to 2 min for the headset to appear -> ladder A1 minimal, A2 `--parity`, A3 = A2 with a longer
  budget, stopping at the first PASS -> whole-tree stock check). `--user-presence` is off the ladder because VDXR does
  not offer `XR_EXT_user_presence`: 31 instance extensions measured headset-free first with the probe's new
  `--extensions` switch (`artifacts/openxr/probe/vdxr-extensions.json`). **M2 stays BLOCKED and the simulator stays
  M2's host** - this run attributes the stall (TOOL-011) to the simulator or to our client, and option (d) decides
  afterwards whether a VDXR PASS would count as M2. The harness was dry-run headset-free before the sitting and caught
  one bug in itself (the no-headset detection matched the OpenXR constant spelling instead of the client's
  `ErrorFormFactorUnavailable`, which walked the ladder with no headset attached); the corrected run ends
  `NO_HEADSET_ATTACHED`, ladder untouched, stock verified.
- 🚧 **M2 headset runs 2026-10-07 (D-066 option (b)): two short sittings, still no submitted frame.** Run 1 answered the
  question the sitting existed for: **the TOOL-011 stall does not reproduce on VDXR** - `xrWaitFrame` returns Success in
  3 ms with `shouldRender=1`, `xrBeginFrame` succeeds - so the blocked frame loop belongs to the simulator, not to our
  client. That run then failed in our own D3D11 code (TOOL-012, the shim's vtable slots; fixed and checked headset-free
  by `--selftest-d3d11`, which passes). Run 2 reached `xrEndFrame` and was answered `ErrorPoseInvalid` with the session
  stuck at `Ready (2)` - TOOL-013: our probe submitted frames while the app was not being displayed. The frame loop now
  gates on Visible, like the sample, and the wait after `xrBeginSession` waits for Visible; the
  headset-attached-but-not-displayed case now reports `NotVisibleSkips=5` and "the session never reached Visible"
  instead of submitting invalid frames. **No frame has been submitted yet, M2 stays BLOCKED, and option (d) is still
  open** - a third sitting is a user decision (**Q-043**), and its precondition is now precise: Virtual Desktop must be
  actively showing the desktop in the headset, not merely connected.
- ⬜ M3, M4 not started (M3 has a proper hook waiting: the runtime offers
  `XR_METAX1_simulator_compositor_output_capture`, measured in its extension list, so the composed eyes can be
  captured by API instead of by screenshot).
- Delivered in this sub-stage so far: `src/render/OpenXrProbe/` (first-party OpenXR client: instance, system, views,
  adapter match, device, session lifecycle, swapchains, frame loop, receipts), the loader pin in `DEPENDENCIES.json`,
  and `artifacts/devtools/Focus-Window.ps1` (a diagnostic helper, not part of the mod).



































































































































































































































































































































































































































































## Status update (2026-10-11, D-144) — 미리보기 배포 패키지 완성
- 완료: `dist\VampireHuntersVR-preview-0.1.zip` (33.82MB, 244 엔트리, sha256 `5F9696A9…`) + `.sha256.txt`. install/uninstall(.bat + .ps1, 수동 복사/붙여넣기 방식, 경로 자동탐색 없음) + `README-KO.md` + `THIRD-PARTY-NOTICES.txt` + `licenses\` 4종 + `payload\`(고정 검증 바이너리 233 파일).
- 검증(오프라인, zip 해제본 그대로): 가짜 게임 폴더 3가지 배치(게임 폴더 안 하위폴더 / 내용물 직접 복사 / 게임 밖)에서 `install.bat`·`uninstall.bat` 왕복 통과. 정상 설치 234 파일, 제거 후 게임 트리 원본만 남음, 예상 밖/개조 파일은 삭제 거부(–Force로만 진행). 실제 게임 트리 121 files/7,726,223,158 bytes 무손상.
- 미해결(사용자 결정 필요): **A안**(현 상태 배포: 600초, VD 전용) vs **B안**(세션 상한 제거 + OpenXR 런타임 자동감지 + 진단 옵션 제거 = `Host.cpp` 3곳·`OpenXrHost.cs` 4곳·config 수정 + 네이티브 재빌드 + 테스트 ~8건 수정 + 헤드셋 재검증 1회). TOOL-073/074/075는 그대로 OPEN.

## Status update (2026-10-11, D-145) — B안 구현 + 재포장 완료, 헤드셋 재검증 대기
- 완료: 세션 무제한(`HeadsetSeconds 0`), OpenXR 런타임 자동감지(레지스트리 ActiveRuntime + 모든 실제 런타임 허용), RenderTimingDiagnostics 계측과 관련 파일/테스트 전부 제거. 네이티브 재빌드(`6162A025…`) + 플러그인 재빌드(`50204473…`), `artifacts\release-0.2\pins.json`에 재핀.
- 오프라인 검증: full verifier **32/32** PASS, native 43/43(신규 2), HandAim 197/197(신규 2), 나머지 전부 PASS, Release 경고 0·오류 0.
- 배포: `dist\VampireHuntersVR-preview-0.2.zip`(33.81MB, 243 엔트리, sha256 `EC71BEF2…`) + `.sha256.txt`. 0.1 zip 삭제. 가짜 게임 폴더 3배치 왕복 + zip 해제본 왕복 통과, 실제 게임 트리 121 files/7,726,223,158 bytes 무손상.
- 다음(사용자 착용 1회): `docs\playtests\PLAYTEST-D145-UNLIMITED-RUNTIME-KO.md` 카드대로 `d145a` 태그 헤드셋 실행 → 무제한 세션/정상 종료/런타임 로그/조작 회귀 확인. TOOL-073/074/075는 그대로 OPEN.




































































## Status update (2026-10-12, D-148b3) — ① 계측 확장 + ② EyePreset=low A/B 실행 결과
- **코드(D-148b2, 커밋 `6f65be4`)**: 타이밍 줄에 `prepBodyMs/handsMs/pumpMs/gapMs/logMs/prepares` 추가(측정 전용, ABI 불변). native 45/45, HandAim **227/227**, 전체 검증 **32/32 PASS**(stderr 0B), 네이티브 재현성 유지(`EE69008C…`, 328,704 B), 핀 갱신(plugin `704DC2D7…`).
- **착용(d148f, `Maze_Small`, 246창, 전부 90Hz 요청)**: `cycleMs` 중앙 **20.26ms = 49.4fps**, `CLOSED orderly=1`, stock 121/8 원복, `nativeFailures=0`.
- **① 항등식 닫힘**: `gapMs 4.58` + `prepToSubmitMs 9.82` + `submitMs 5.69`(+`prepBody 0.03`/`hands 0.02`/`log 0.05`) ≈ `cycleMs` 잔차 ≤0.05ms → **미계측 3.7ms = `gapMs`(렌더 스레드 회전/디스패치) = 사이클의 23%**. HandsSync/Pump/Log flush 가설 전부 기각.
- **② 해상도는 레버 아님**: 픽셀 −36%에서 GPU 항목만 감소(`fenceMs 4.69→4.29`, `submitMs 6.40→5.69`), 전체 사이클은 증가(장면 부하 `prepToSubmitMs 9.82`). 초반 20창 17.08(d148e) vs 16.95ms(d148f)로 사실상 동일. 단 스테이지가 달라 전체 fps 비교는 교란 — 통제 A/B는 후순위.
- **우리 CPU/프레임 0.55ms**(d148e 1.01), 엔진 `unityDeltaAvgMs 4.90`, 힙 +37MB/분(gen2 ±0).
- **다음 후보**: ⓐ 이른 prepare 디스패치(`gapMs` 회수, 작음) → 결과로 ⓑ 오버랩/더블 버퍼(큼) 판단. (별개) TOOL-081 재핀 + 0.3 재포장.
- 사용자 체감: 화면 괜찮음/지연 못 느낌/선명도 나쁘지 않음(d148e 기준) + d148f 저해상도 체감 보고 대기.

## Status update (2026-10-12, D-148b4) — ⓑ 오버랩 착수: submit 펜스 지연 증명(opt-in) + d149a 준비
- **결정**: d148f가 해상도를 레버에서 제외했고(GPU 항목만 반응), `gapMs` 4.6ms는 Unity 렌더 스레드 소관이므로, 통제 가능한 실제 손실 `fenceMs`(4.3ms GPU 완료 스핀)를 회수하는 **지연 증명 오버랩**을 구현했다. OpenXR 제약(`xrBeginFrame(N+1)`은 `xrEndFrame(N)` 이후) 때문에 "제출 미루기"는 불가능 → 제출/릴리스는 같은 프레임, **완료 증명만 다음 Submit으로 이동**(아이당 쿼리 핑퐁).
- **안전**: 증명은 이동만 하고 사라지지 않음. 증명 불가 시 조용히 통과하지 않고 실패(세션 −1), `Close()`는 스왑체인 파괴 **전에** 드레인. 기본값 **OFF** → 0.3 경로 불변.
- **검증**: 네이티브 **49/49**(신규: 지연-1프레임 순서·파괴 전 드레인·옵트인 게이트·증명 실패 치명성), HandAim **228**, 전체 **32/32 PASS, stderr 0B**. 픽스처에 케이스 이름 출력 개선.
- **사용자 체감 반영**: `EyePreset=low`는 선명도 저하 + 드랩 악화 → **기각, normal 확정**.
- **d149a 준비**: `artifacts\d149a-analysis\Prepare.ps1`(EyePreset normal, `DeferredGpuWait=true`, 600초 예산, `-ExpectDeferredGpuWait` 게이트). 착용 규칙(사전 등록): `fenceMs`→~0 & `cycleMs` ≈4ms 감소면 승격 검토, 변화 없으면 GPU 큐가 페이싱 → 엔진 프레임과의 더 깊은 오버랩.










































































































































































































































## Status update (2026-10-13, D-170) — 해상도·MSAA를 고르는 설정 exe, 눈 MSAA 네이티브 경로 → **헤드셋 1회 대기**
- **사용자 지시**: "게임 해상도 설정 및 MSAA on/off 만들기 및 설정을 선택할 수 있게 exe 파일로 만드는 작업". 마커/HUD는 무변경.
- **해석**: 해상도 = **눈당 렌더 해상도**(`EyePreset`/`EyeWidth`/`EyeHeight`), MSAA = **눈 이미지의 `antiAliasing`**(게임 AA는 우리 눈에 오지 않음, D-147 실측). exe는 이 4키만 편집한다.
- **눈 MSAA 구현(기본 off = 수용된 경로 무변경)**: 눈 RenderTexture `antiAliasing=EyeMsaa`(0→1) → 네이티브 `SetEyeMsaa`(init 전) → init에서 **실제 텍스처 샘플 수 검증** → 눈당 단일샘플 리졸브 대상 생성 → `Submit`에서 `ResolveSubresource` 후 **기존 exact copy**(카피가 받는 텍스처는 항상 단일샘플). 불일치는 fail-loud.
- **설정 exe**: `dist\tools\VhvrSettings.exe`(162,304 bytes) — 프리셋/기준 크기/ MSAA 선택, 적용될 눈 크기 미리보기, 4키만 변경+나머지 보존, 타임스탬프 `.bak` 백업, 정책 범위 밖 값 거부, 게임 실행 중 경고. CLI(`--print/--validate/--apply/--selftest`)로 헤드셋 없이 검증.
- **오프라인**: 네이티브 selftest 실제 하드웨어 MSAA 리졸브 2케이스+거부 6케이스 PASS, fault 52→55, HandAim 317→319, Settings 18→26, 신규 settings-tool 테스트, 전체 검증기 재실행.
- **다음(헤드셋 1회)**: `-EyeMsaa 4`(권장) 또는 off/4x A/B — 준비 태그와 실제 실행 명령은 PHASE_PLAN 최상단에만 적는다. 판정: BSP 로그 `EYE_MSAA requested=4 unityAntiAliasing=4`, 리시트 `eyeMsaa=4`+`quality.configuredEyeMsaa=4`, `HOST_...` 실패 없음(샘플 수 불일치면 sit이 실패해야 정상), 눈 가장자리 계단 감소, fps/타이밍(`cycleMs`) 변화, 기존 수용 항목(마커·벽 통과·상자) 회귀 없음. 실행은 사용자 `실행` 이후에만.

## Status update (2026-10-17, D-186-FovScale) — 렌더 FOV 축소(over-render 자름) 정직 분석 + 0줄 변경 보류 → **0.8.0-dev 의 Q-FovScale 로 등록**
- **사용자 요청**: "내 HMD보다 넓은 시야를 렌더링하는것 같아, 이 부분을 줄여서 FPS와 해상도에 이득을 얻을 수 있을까?"
- **사실 (0.7 코드)**: 렌더 FOV 의 출처 = `xrLocateViews` 가 보고한 값. `Host.cpp:60-62` 가 C# 로 4 각도 그대로 전달, `Host.cpp:72` 가 `v.fov=views[eye].fov` 로 compositor 에 그대로 제출, `OpenXrHost.cs` 의 `StereoMath.Projection(...)` 2 곳이 그 값을 projection matrix 로 변환 — **어디에도 scale 곱셈 0 줄**. `OpenXrHostConfig` POCO 30+ 필드 중 FOV 키 0 개, `vhvr-openxr-host.json` config 키 0 개.
- **over-render 의 정체**: OpenXR 표준 동작 (lens distortion + barrel margin + IPD margin). Meta native 는 거의 0, SteamVR/VDXR 은 1.1~1.2×. 사용자가 "넓다"고 느낀 것은 표준.
- **이득 정직 추정 (1.1× over-render 가정)**: FovScale 0.90 ≈ ~22% 픽셀 감소, 0.80 ≈ ~44%, 0.70 ≈ ~61%. **fps 향상은 VSynced 헤드셋에선 점프(45→72), 비 VSynced 면 ~22% 단축**. **d148b/d148d 의 VDXR 45Hz 상한이 잠근 환경이면 fps 자체는 안 오르고 GPU 시간 단축만** → MSAA on, 해상도 1.4× 같은 다른 비용의 여유.
- **5 위험 (정직)**: (1) 검은 띠 — C# 만 좁히고 C++ 가 그대로 제출하면 가장자리 검게 표시. → C# C++ 양쪽 sync 필수. (2) compositor FOV sync — ABI 가 양쪽 4 각도를 동일하게 좁혀야 함. (3) C++ ABI 안전 — `HostStatus`(136) / `EyePose`(44) 는 `static_assert` lock, struct 자체엔 못 손대지만 `Host` 외부 struct 에 `float fovScale` 추가는 무영향. (4) headset 없으면 검증 불가 — 데스크탑 headset-free 환경. (5) 0.7.1 호환 — `FovScale` 키 없는 JSON 기본값 1.0 으로 처리하면 무영향 (D-104-iso-promotion 의 `Anisotropic` 키 패턴).
- **결정 (0줄 코드 변경)**: 호스트 / OpenXrHostConfig / VhvrSettings.exe / Host.cpp 4 곳 모두 손대지 않음. D-186-FovScale 결정을 docs/DECISIONS.md 에 정식 기록, Q-FovScale 을 docs/QUESTIONS.md 에 등록 (해제 조건 5개). 다음 PLAYTEST (0.7.1) 에서 (1) `OpenXrProbe.Receipt.Fov` 로 4 각도 캡처, (2) 시각적 over-render 영역 측정, (3) fps baseline → 그 데이터로 0.8.0-dev 의 기본값 (1.0 vs 0.95) 결정. **0.7.1 의 다음 빌드 우선순위는 Q-MenuScroll (메뉴 스틱 ↑/↓ → 마우스 스크롤) 와 formselftest preview 보강 (TOOL-095) 유지** — Q-FovScale 은 그 다음.


## Status update (2026-10-18, D-198) — 시작 직후 15초 VR 지연 제거: 고정 `Time.unscaledTime < 15f` → readiness 게이트 → **헤드셋 1회 대기**

- **사용자 지시**: "시작하자마자 VR로 전환 안되고 한 15초 기다려야 VR 전환되는데, 그건 왜그런거야?" → "시작하자마자 VR로 시작되게 못하나?"
- **원인 (확정)**: `src\core\OpenXrHost.cs` `Update()` 첫머리 `if (Time.unscaledTime < 15f) return;`. `Time.unscaledTime` = 게임 프로세스 시작 이후 누적 초(씬 로드로 리셋 없음) → **게임 시작 후 15초 동안 `Initialize()` 미호출**. 사용자가 기다린 15초의 정체가 이것이다.
- **수정**: 매 프레임 `StartupReady()` 게이트 — 카메라 존재 + `SystemInfo.graphicsDeviceType == Direct3D11` 확인, 충족 즉시 초기화. 대기 시 1초 주기 `[OPENXR_HOST] STARTUP_WAIT <reason> elapsed/budget` + `waiting.json`(`WAITING_FOR_STARTUP_CAMERA`) 영수증, 예산은 `Config.GameplayWaitSeconds`(기본 180 s), 통과 시 `STARTUP_READY elapsed=X.Xs` 1회. `host.json` 에 `startupReadySeconds` / `startupWait` 두 평면 필드 추가.
- **오프라인 검증**: Stage0 Release **0 warning/0 error**; 신규 소스 가드 4개(`source-no-fixed-warmup`, `source-readiness-gate`, `source-bounded-startup-wait`, `source-startup-wait-receipt`) 포함 `GameplayCamera` **35/35**, `WeaponRuntime` **67/67** PASS.
- **설치**: 게임 폴더 `VampireHuntersVR.Stage0.dll` 교체 — **최종 SHA `D1655D505DBA136110471955B1AC2DF67753C882701AC295A91DD2F854C21175`**(426,496 B, 18:49:25) 이며 현재 소스 재빌드와 SHA 일치 확인. 이전 DLL(2026-10-17 원본, 421,376 B, SHA `0A2A9FAF…`)과 중간 빌드(`.intermediate-18F991C0`)는 `backups\D-198-20261018T184654671\` 보존. `VhvrExtras.dll`·`vhvr-openxr-host.json`·`VhvrNativeHost.dll` **무변경**.
- **다음(헤드셋 1회)**: 게임 실행 → (1) VR 이 뜨는 데 걸린 체감 초, (2) 로그 `STARTUP_READY elapsed=`, (3) `run\host.json` `startup.readySeconds`/`waitReason`, (4) `Evidence` 첫 프레임 시각. 판정: `readySeconds ≤ 3` = 수정 성공(게임 준비 즉시 VR). `readySeconds ≥ 10` + `waitReason` = `no main camera yet` = **게임 로딩이 병목**(우리 축 아님, Q-D198-STARTUP-SPLIT 로 종결). 기존 수용 항목(메뉴 panel ray, 마커, 카드 회전) 회귀 없음 확인. 실행은 사용자 `실행` 이후에만.


## Status update (2026-10-18, D-201) — 메뉴 화면·레이저를 HMD 정면 프레임으로 통일 → 헤드셋 1회 대기

- **사용자 지시**: "VR모드가 시작되면 내 시야가 오른쪽으로 90도 꺾여 있거든 ... UI ray가 이상한데" → "VR 모드가 시작될 때 HMD 정면의 화면 위치를 중앙으로 설정해".
- **원인 (수치 확정)**: extras가 패널을 `eyes[0].transform`에서 배치하는데 `Camera.CopyFrom`이 transform까지 복사하므로(Unity 문서), **호스트의 첫 eye 포즈 적용 전**에 배치하면 게임 카메라(ArenaCamera, Unity yaw −90°) 포즈가 그대로 동결된다(D-178 "한 번 배치 후 동결"). 로그 `menu ui: panel frozen at world (74.29, 34.00, 43.75)` = `anchorPosition + 2 m × (−1,0,0)`, 같은 실행 영수증의 `view` row2 = `(0, 0, -1)` → **정확히 90°**. 메뉴 레이저도 `anchorRotation`(게임 카메라 회전)을 곱해 같은 90° 어긋남이 있었다.
- **수정 (extras만, Stage0/config 무변경)**: ⓐ `MenuScreenRig.TryGetLiveHeadPose()`(네이티브 status, `Frames > 0` 요구)로 패널을 **라이브 헤드 포즈 + anchorPosition** 에 배치(라이브 포즈 없으면 배치 보류 + 로그) ⓑ 배치 로그에 `headYaw`/`panelYaw`/`headPanelDelta` 추가(영수증 판정용) ⓒ 레이저 프레임을 HMD(identity)로 통일 — `[MenuScreen] PointerHeadFrame`(기본 true, false = 0.9.4 동작) ⓓ 리센터 — `[MenuScreen] RecenterChord`(기본 true, 양쪽 스틱 동시 클릭).
- **오프라인 검증**: extras Release 0경고 0오류; `VhvrExtras.dll` 53,760 B SHA256 `52E67072…`; VhvrExtras **72/72**(신규 10; `verify.ps1` 기대값 62→72). 직접 실행 회귀: GameplayCamera 38/38, HandAim 190/190, WeaponRuntime 67/67, FlameEndpoint 138/138, FlameEmitter 55/55, StereoMath 29/29 — 모두 PASS.
- **설치**: 게임 폴더 `BepInEx\plugins\VhvrExtras.dll` 교체(백업 `backups\D-201-20261018T111028Z\VhvrExtras.dll-pre-D201.dll`).
- **게이트 결함 (별건, 정직)**: `tests\verify.ps1`은 65행 `transaction-fixture-tests`에서 **파서 오류로 중단**된다 — `tests\Transaction.Tests.ps1`의 160~194행이 빈 줄로 유실(모든 사본이 같은 해시 `09A62C91B96FD46F`, 2026-10-14 이후 상태). 그 결과 verify.ps1의 **이후 게이트(plugin-build, preflight-entry, verify-bindings, extras 개수 등)는 실행되지 않는다**. verify.ps1은 exit 1로 실패하므로 조용히 통과하지는 않지만, 이번 변경 검증은 위 스위트를 **직접** 실행해 대체했다. `ENV-012`로 등록.
- **다음(헤드셋 1회)**: `docs\playtests\PLAYTEST-D201-MENU-CENTER-KO.md` — A1 메뉴 정면 중앙, A2 수평, A3 레이저 방향, A4 클릭, B1 리센터, D 로그 `headPanelDelta=0.0deg`. 통과 시 0.9.5 패키지 확정, 불일치 시 `PointerHeadFrame` A/B 1회.

