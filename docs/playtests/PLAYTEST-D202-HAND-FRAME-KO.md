# PLAYTEST — D-202 인게임 손/조준 프레임 (헤드셋 1회, 2026-10-18)

> 목적: **게임 안에서 손/무기가 컨트롤러가 가리키는 방향으로 나오는지** 확인한다(0.9.4의 "오른쪽으로 90° 꺾임" 수정).
> 소요: 게임 실행 1회, 헤드셋 착용 약 3~5분. 준비물: 컨트롤러 2개.

## 준비 (헤드셋 없이, 30초)
1. 게임이 꺼져 있는지 확인한다.
2. 설치본 DLL 해시가 빌드 산출물과 같은지 확인한다.
   ```powershell
   Get-FileHash '<GameRoot>\BepInEx\plugins\VampireHuntersVR.Stage0.dll' -Algorithm SHA256
   # 기대: 34AA247600E3DEA20895F77A49A005B296B950CCFE43EA1483619ECB765DE525  (430,592 B)
   ```
3. `BepInEx\config\vhvr-openxr-host.json` 에 `"HandPoseFrame":  "Head"` 가 있는지 확인한다(없으면 코드 기본값이 Head).

## A. 게임플레이 (핵심 4항목)
스테이지를 시작해 무기를 들고 확인한다.

| # | 확인할 것 | 판정 |
| --- | --- | --- |
| A1 | 컨트롤러를 **앞으로** 들면 무기가 **앞**을 향하는가 (좌/우 90°로 꺾여 있지 않은가) | 예 / 아니오 — 어느 쪽으로 몇 도 |
| A2 | 컨트롤러를 **좌우로** 돌리면 무기 방향이 **같이** 도는가 | 예 / 아니오 |
| A3 | 무기를 넘겨받는 순간(무기 교체·스테이지 시작)에 **무기가 튀지 않고** 자연스럽게 손에 붙는가 | 예 / 아니오 |
| A4 | 무기가 **손에 붙어 있는 위치**(팔 길이 안)에 있고 몸에서 떨어져 있지 않은가 | 예 / 아니오 |

## B. 조준/발사
| # | 확인할 것 | 판정 |
| --- | --- | --- |
| B1 | 리티클이 **총구 방향**(가리키는 방향) 앞에 있는가 | 예 / 아니오 |
| B2 | 발사하면 탄/이펙트가 **가리키는 방향**으로 나가는가 | 예 / 아니오 |
| B3 | 무기 **기울기(롤)** 가 이상하게(비스듬히) 보이지 않는가 | 예 / 아니오 — 몇 도 |

## C. 회귀 (D-201 메뉴 + 기존 수용 항목)
| # | 확인할 것 | 판정 |
| --- | --- | --- |
| C1 | 메뉴가 여전히 **정면 중앙**에 있는가 | 예 / 아니오 |
| C2 | 메뉴 레이저로 버튼을 누를 수 있는가 | 예 / 아니오 |
| C3 | 게임플레이에서 스틱 이동/점프/기술/사격 입력이 이전과 같은가 | 예 / 아니오 |
| C4 | **(알려진 미구현)** 오른쪽 스틱 좌/우 스냅 턴이 **아무 일도 하지 않고 화면이 튀지도 않는가** | 예 = 정상(BUG-203 상태) / 아니오면 특이사항 |

## D. 실패 시 A/B 1회 (리빌드 없음)
판정이 "아니오"일 때만:
1. 게임을 끈다.
2. `BepInEx\config\vhvr-openxr-host.json` 에서 `"HandPoseFrame":  "Head"` → `"Anchor"` 로 바꾼다(백업 먼저).
3. 같은 A1~A2를 확인한다. **Anchor에서 0.9.4와 같은 90°가 다시 나오면** 원인(프레임) 확정이 강화되고, Head에서는 정상이어야 한다.
4. 끝나면 `"Head"` 로 되돌린다.

## E. 영수증 (헤드셋 없이 확인, 1분)
```powershell
$run = "$env:LOCALAPPDATA\VampireHuntersVR\run"
Select-String "$run\host.json" -Pattern 'anchorRotation' | Select-Object -First 1
$j = Get-Content "$run\hand-aim.json" -Raw | ConvertFrom-Json
$j.right.state; $j.right.restCapturedWithPose; $j.right.muzzleToHandForwardDegrees; $j.right.weaponTwistResidualDegrees
$j.left.state; $j.left.weaponsInSlot
Select-String '<GameRoot>\BepInEx\LogOutput.log' -Pattern 'HAND_FRAME|SNAP ignored'
```
- **합격 기준**: A1~A4 모두 예 + B1·B2 예 + C1~C3 예 + 로그 `HAND_FRAME Head (D-202: ...)` 1회.
- **실패 시 보고**: 꺾인 각도(추정 도) + `muzzleToHandForwardDegrees` + `weaponTwistResidualDegrees` + Anchor A/B 결과를 그대로 붙여준다.
- **주의**: 이번 수정은 **손/조준의 방향 프레임**만 바꾼다. 탄이 적에게 맞는지(게임 자체 조준/데미지)는 이 판정의 대상이 아니다.

## 보고 형식 (이대로 붙여주면 됨)
```
A1 ___ A2 ___ A3 ___ A4 ___ / B1 ___ B2 ___ B3 ___ / C1 ___ C2 ___ C3 ___ C4 ___
muzzleToHand=___  twistResidual=___  leftState=___  HAND_FRAME=___  (특이사항 한 줄)
```
