# PLAYTEST — D-201 메뉴 화면/레이저 정렬 (헤드셋 1회, 2026-10-18)

> 목적: VR이 시작될 때 **메뉴가 HMD 정면 중앙**에 있고, **레이저가 컨트롤러가 가리키는 곳**을 향하는지 확인한다.
> 소요: 게임 실행 1회, 헤드셋 착용 약 3~5분. 준비물: 컨트롤러 2개(스틱 클릭 사용).

## 준비 (헤드셋 없이, 30초)
1. 게임이 꺼져 있는지 확인한다.
2. `BepInEx\plugins\VhvrExtras.dll` 의 SHA256 이 `52E6707290D430F25615CF50EFAA7ADA3013DD21527FB7861D999BFDDB1CA533` (53,760 B) 인지 확인한다.
   ```powershell
   Get-FileHash '<GameRoot>\BepInEx\plugins\VhvrExtras.dll' -Algorithm SHA256
   ```
3. `BepInEx\LogOutput.log` 을 지우거나 이름을 바꿔 이번 실행만 남긴다(선택).

## A. 메뉴 (1회 실행, 핵심 4항목)
게임을 실행하고 헤드셋을 쓰면 인트로 동안은 평면, **메인 메뉴가 뜨면 VR로 전환**된다(D-200).

| # | 확인할 것 | 판정 |
| --- | --- | --- |
| A1 | 메뉴 화면이 **정면 중앙**에 있는가 (고개를 돌리지 않고 바로 앞) | 예 / 아니오 — 어느 쪽으로 몇 도 |
| A2 | 화면이 **기울지 않고 수평**인가 | 예 / 아니오 |
| A3 | 레이저가 **컨트롤러가 가리키는 방향**으로 나가는가 (손을 돌리면 같이 도는가) | 예 / 아니오 |
| A4 | 레이저 끝으로 **메뉴 버튼을 누를 수 있는가** (트리거) | 예 / 아니오 |

## B. 리센터 (양쪽 스틱 동시 클릭)
| # | 확인할 것 | 판정 |
| --- | --- | --- |
| B1 | 고개를 90° 돌린 상태에서 양쪽 스틱을 **동시에** 누르면 화면이 **지금 보는 방향 앞**으로 다시 오는가 | 예 / 아니오 |
| B2 | 리센터가 게임 메뉴 조작(스크롤/선택)을 방해하지 않는가 | 예 / 아니오 |

## C. 회귀 (기존 수용 항목)
| # | 확인할 것 | 판정 |
| --- | --- | --- |
| C1 | 메뉴에서 **스테이지 진입**이 되고, 그때 캔버스가 게임으로 돌아가는가(메뉴 UI가 화면에 남지 않는가) | 예 / 아니오 |
| C2 | 게임플레이에서 **손/무기/조준**이 이전과 같은가 (이번 수정 대상 아님 — 회귀 확인) | 예 / 아니오 |

## D. 영수증 (헤드셋 없이 확인, 1분)
```powershell
# 1) 패널 배치 로그 — headPanelDelta=0.0deg 이면 정면
Select-String 'BepInEx\LogOutput.log' -Pattern 'panel frozen|recenter|menu pointer frame|no live eye sample'
# 2) 실행 영수증
$j = Get-Content "$env:LOCALAPPDATA\VampireHuntersVR\run\host.json" -Raw | ConvertFrom-Json
$j.startupWait; $j.scene; $j.sourceCamera; $j.evidence[0].anchorRotation
```
- **합격 기준**: `headPanelDelta=0.0deg`(또는 1° 이내) + A1~A4 모두 예.
- **실패 시**: `headPanelDelta`가 90 근처면 배치 입력이 다시 어긋난 것이므로 그 로그를 그대로 붙여 보고한다. A3만 실패면 `[MenuScreen] PointerHeadFrame=false` 로 바꿔 1회 더 실행(A/B).
- **리센터가 게임과 충돌하면**(B2 아니오): `[MenuScreen] RecenterChord=false` 로 끄고 보고한다.

## 보고 형식 (이대로 붙여주면 됨)
```
A1 ___ A2 ___ A3 ___ A4 ___ / B1 ___ B2 ___ / C1 ___ C2 ___
headPanelDelta=___  PointerHeadFrame=___  (특이사항 한 줄)
```