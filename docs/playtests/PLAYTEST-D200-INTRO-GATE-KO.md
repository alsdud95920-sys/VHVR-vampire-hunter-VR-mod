# PLAYTEST D-200 — 인트로 이후 VR 시작 (0.9.4) — 착석 1회

- 대상 빌드: `VampireHuntersVR.Stage0.dll` SHA256 `102E765DF9DF00726332CF12050DB597560EA097E00E0BD7F021C8488234D5AE` (428,032 B)
- 설치 위치: `BepInEx\plugins\VampireHuntersVR.Stage0.dll` (교체 완료, 해시 일치 확인)
- 설정: `BepInEx\config\vhvr-openxr-host.json` — `Headset=true`, `MenuFirst=true`, `StartupSkipScenes="Intro"`, `StartupSettleSeconds=1.0`, `GameplayWaitSeconds=600`

## 목적
0.9.3에서 헤드셋이 검은 화면이던 문제(D-200)가 **"인트로 동안 VR을 시작하지 않고, 메인 메뉴에서 시작"** 하는 방식으로 해결됐는지 **착석 1회**로 판정한다.

## 준비 (착석 전, 모니터에서)
1. OpenXR 런타임 활성 확인 — `HKLM:\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` (Virtual Desktop 등).
2. 게임 완전 종료 확인(작업 관리자에 `Vampire Hunters` 없음).
3. 직전 영수증 보관 — `%LOCALAPPDATA%\VampireHuntersVR\run` → `run-before-d200`로 복사(선택).
4. 로그 비교를 원하면 `BepInEx\LogOutput.log` 백업 후 삭제(선택).

## 절차 (약 3분)
1. 게임 실행 → **인트로 동안 헤드셋에는 아무것도 나오지 않아야 한다**(모니터에는 인트로 재생).
   - 인트로에서 VR이 시작하면 **실패**(0.9.3 증상 재발).
2. 인트로가 끝나고 메인 메뉴가 뜨면 **1초 이내에 VR 화면**(메뉴 패널 + 컨트롤러 레이저)이 나타난다.
3. 트리거로 메뉴를 클릭해 스테이지를 1회 시작하고, 게임플레이에서 손/무기 통합이 붙는지 확인(1분 내 종료해도 됨).
4. (선택) ESC로 메뉴 복귀 1회.

## 판정 기준 (영수증)
`%LOCALAPPDATA%\VampireHuntersVR\run\host.json`:
- `startupWait` = `intro scene 'Intro' (D-200: waiting for the main menu)` → 게이트가 인트로에서 대기했다.
- `scene` = `MainMenu_2`, `sourceCamera` = `ArenaCamera` → 메뉴 카메라를 채택했다(인트로 카메라가 아님).
- `startupReadySeconds` ≈ 메뉴 등장 시각(0.9.3의 **0.6초가 아님**).
- `windowFrames`가 계속 증가(0.9.3은 **134**에서 멈췄다), `evidence`의 frame 진행.

`BepInEx\LogOutput.log`:
- `[OPENXR_HOST] STARTUP_SKIP scene='Intro' StartupSkipScenes=Intro (D-200: ...)` 가 1회.
- `[OPENXR_HOST] STARTUP_READY elapsed=... scene='MainMenu_2' camera='ArenaCamera' waited="intro scene 'Intro' ..."`.

## 실패 시 다음 조치
- VR이 인트로에서 시작 → `StartupSkipScenes`(현재 `Intro`)에 로그의 실제 `scene=` 값을 추가.
- 메뉴에서 화면이 안 나옴 → `startupReadySeconds`/`scene` 확인, `STARTUP_WAIT ... budget=` 소진 로그와 `waiting.json`의 `reason` 회수.
- 여전히 검은 화면 → 되돌리기: `backups\D-200-20261018T104445Z\VampireHuntersVR.Stage0.dll-pre-D200.dll` 을 `BepInEx\plugins\VampireHuntersVR.Stage0.dll` 로 복사.

## 결과 기록 (사용자 기입)
- [ ] 1. 인트로 동안 VR 미시작
- [ ] 2. 메뉴에서 VR 화면 정상
- [ ] 3. 스테이지 진입 시 손/무기 정상
- 메모:
