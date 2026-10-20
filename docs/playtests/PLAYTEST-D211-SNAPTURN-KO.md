# PLAYTEST — D-211 스냅 턴 3택 (헤드셋 1회, 2026-10-20)

> 목적: **오른쪽 스틱 좌/우 스냅 턴이 시야를 실제로 돌리는지**, 각도가 **30° / 45° / 끄기** 세 가지로 동작하는지, 그리고 **위치가 튀지 않고 전방 이동이 새 방향으로 함께 도는지** 확인한다(BUG-203 수정).
> 소요: 게임 실행 3회(설정 3가지) 또는 1회 + 설정 변경 2회, 헤드셋 착용 약 5~8분. 준비물: 컨트롤러 2개.

## 배포 기록 (2026-10-21, 이 PC)

- 설치본: `F:\SteamLibrary\steamapps\common\Vampire Hunters`
- 덮어쓴 파일: `BepInEx\plugins\VampireHuntersVR.Stage0.dll`(440,320 B, SHA-256 `129361927619F42C3A625D99EB85EB04B781DF981690C104D17FB4F35BE85F2F`), `VampireHuntersVR-0.9.5-rc1\VhvrSettings.*`(3택 UI), 같은 폴더의 `install.ps1` / `README-KO.md` / `SETTINGS-README-KO.md`
- 백업: `F:\VRMods\backup\20261021T003925-d211-deploy` (같은 상대 경로로 되돌리면 rc1 상태 복구)
- 배포 스크립트: `scripts\deploy-dev.ps1 -GameRoot '<게임 폴더>'` (덮어쓰기 전 자동 백업 + 매니페스트 해시 갱신)
- 안내문: 설치본 `VampireHuntersVR-0.9.5-rc1\UPDATE-D211-KO.txt`
- 라이브 설정: `SnapTurnDegrees=30`, `HandPoseFrame=Head` (배포 전과 해시 동일, 손대지 않음)

## 준비 (헤드셋 없이, 1분)
1. 게임을 끈다.
2. 설치본 DLL이 방금 빌드한 것과 같은지 확인한다.
   ```powershell
   Get-FileHash '<GameRoot>\BepInEx\plugins\VampireHuntersVR.Stage0.dll' -Algorithm SHA256
   # 기대: 129361927619F42C3A625D99EB85EB04B781DF981690C104D17FB4F35BE85F2F  (440,320 B)
   ```
3. `VhvrSettings.exe`를 열어 **Snap turn (right stick)** 이 `30 deg (default)`인지 확인한다. (또는 `BepInEx\config\vhvr-openxr-host.json`의 `"SnapTurnDegrees": 30`)

## A. 30° (기본) — 핵심 4항목
스테이지를 시작하고 서 있는 상태에서 확인한다.

| # | 확인할 것 | 판정 |
| --- | --- | --- |
| A1 | 오른쪽 스틱을 **오른쪽**으로 한 번 밀면 시야가 **한 번에 30° 정도 오른쪽**으로 도는가 | 예 / 아니오 — 몇 도(추정) |
| A2 | 같은 동작에서 **화면이 옆으로 튀지 않는가**(위치가 이동하지 않는가) | 예 / 아니오 |
| A3 | 왼쪽으로 밀면 **반대 방향**으로 같은 각도만큼 도는가 | 예 / 아니오 |
| A4 | 스틱을 **누르고 있는 동안** 계속 돌지 않고, 놓았다가 다시 밀어야 다음 칸이 되는가 | 예 / 아니오 |

## B. 이동과 손 (같은 세션)
| # | 확인할 것 | 판정 |
| --- | --- | --- |
| B1 | 스냅 후 **왼쪽 스틱 앞으로** 밀면 새로 본 방향으로 이동하는가 | 예 / 아니오 |
| B2 | 스냅 후 **무기/리티클**이 컨트롤러가 가리키는 방향을 그대로 유지하는가(시야와 따로 놀지 않는가) | 예 / 아니오 |
| B3 | 메뉴(일시정지) 패널 레이저가 스냅 후에도 정상인가 | 예 / 아니오 |

## C. 45° 와 끄기 (설정만 변경, 리빌드 없음)
게임을 끄고 `VhvrSettings.exe` → **Snap turn** → `45 deg` → 저장 → 게임 실행.

| # | 확인할 것 | 판정 |
| --- | --- | --- |
| C1 | 한 번 밀 때 **30°보다 확실히 큰** 각도(45°)로 도는가 | 예 / 아니오 |
| C2 | `Off`로 저장하고 실행하면 오른쪽 스틱 좌/우가 **아무 일도 하지 않고** 화면도 튀지 않는가 | 예 / 아니오 |

## D. 영수증 (헤드셋 없이 확인, 1분)
```powershell
$run = "$env:LOCALAPPDATA\VampireHuntersVR\run"
$j = Get-Content "$run\host.json" -Raw | ConvertFrom-Json
$j.handFrame; $j.snapTurnDegrees; $j.snapViewYaw; $j.snapTurnsApplied
Select-String '<GameRoot>\BepInEx\LogOutput.log' -Pattern 'SNAP applied in the head frame|SnapTurnDegrees='
$j.evidence | Where-Object { $_.viewYaw -ne $null } | Select-Object -Last 3 eye,viewYaw
```
- **합격 기준**: A1~A4 모두 예 + B1~B3 예 + C1·C2 예, `snapTurnDegrees`가 고른 값(30/45/0), `snapTurnsApplied`가 민 횟수만큼 증가, 로그에 `SNAP applied in the head frame (D-211)` 1회.
- **실패 시 보고**: 실제로 돈 각도(추정) + `snapViewYaw` + `snapTurnsApplied` + `moveYawDelta`(`run\hand-aim.json`의 `input.moveYawDelta`) + 어느 항목이 아니오인지 그대로 붙여준다.
- **주의**: 이 판정은 **시야 회전과 제자리 고정**만 본다. 적중/데미지, 게임 자체 조준은 대상이 아니다.

## 보고 형식 (이대로 붙여주면 됨)
```
A1 ___ A2 ___ A3 ___ A4 ___ / B1 ___ B2 ___ B3 ___ / C1 ___ C2 ___
snapTurnDegrees=___  snapViewYaw=___  snapTurnsApplied=___  moveYawDelta=___  (특이사항 한 줄)
```
