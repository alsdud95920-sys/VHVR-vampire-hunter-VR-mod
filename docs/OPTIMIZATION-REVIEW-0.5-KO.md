# 최적화 리뷰 — 0.5 preview (미적용 브랜치 `0.5-preview`)

> **규칙**: 이 문서의 수치는 0.4(설치본)와 0.3V(d150a)에서 **측정된 것만** 씁니다. 0.5의 코드는 `master`에 병합되지
> 않았고, 0.4 착용 결과가 나오기 전에는 어떤 개선 수치도 주장하지 않습니다.

## 1. 먼저: 우리 코드가 프레임에서 차지하는 몫(측정)

d150a(0.3V)의 PERF 창 536개를 창 크기로 나눈 값:

| 항목 | median | p90 | max |
| --- | --- | --- | --- |
| `lateMs/frame` (hand tick 전체) | **0.465 ms** | 1.42 ms | 2.85 ms |
| `updateMs/frame` | 0.054 ms | — | — |
| `prepareMs/frame` (창당 0.52 ms 합계) | 0.007 ms | — | — |

18.5 ms 사이클의 **약 2.5%**. 즉 **우리 코드를 아무리 잘라도 fps는 오르지 않는다.** 남은 의미는 두 가지뿐이다:
① 꼬리(p90 1.42 / max 2.85 ms) = **끊김·1% low**, ② 할당 churn(관리 힙 1.9 GB, GC 134/100/8).
그래서 0.5는 "fps를 올린다"가 아니라 **① 귀속을 측정 가능하게 만들고 ② 공짜인 것만 줄인다**로 범위를 정했다.

## 2. 이번에 적용한 것(브랜치에만 있음, 행동 동일 주장은 픽스처로 검증)

| # | 항목 | 근거 | 기대 효과 | 위험 |
| --- | --- | --- | --- | --- |
| F-01 | **hand tick 구간 계측**(`TickProfile`, opt-in 기본 OFF) | 위 표의 0.465/1.42/2.85 ms를 **어느 구간이 쓰는지 모른다** | 다음 착용 1회로 16개 구간(read/gaze/scan/left/right/…/pin)의 ms를 영수증에 남김 | 없음(측정 전용, 꺼지면 `default` 스코프 = 비용 0) |
| F-02 | **핫패스 배열 제거**(`ToWorldInto`/`ForwardInto`/`DriveRotationInto`/스칼라 `AngleDegrees` + 슬롯 재사용 버퍼) | 손마다 프레임당 `float[]` 약 10개 생성(코드 검사) | 프레임당 약 12~16개 할당(≈500 B) 제거, gen0 압력 감소 | 낮음: 배열 형태가 **정본**으로 남고, 신규 픽스처가 **동일 결과**(`into-*`)와 **동일 예외**를 단언 |
| F-03 | **머즐 조회 캐시**(`CachedMuzzle`) | 프레임당 `GetComponentsInChildren<Weapon>` **4회**(2손 × 2호출) + 각 호출 배열 할당 | 프레임당 4회 네이티브 순회/할당 제거(히트 시 60프레임에 1회, 미스 시 10프레임에 1회) | 중간: 같은 컨테이너 안에서 무기만 1:1 교체되고 자식 수가 그대로면 최대 1초 stale 가능 → `container` 변경/자식 수 변경 시 즉시 무효화, 영수증의 `muzzlePath`로 관찰 가능 |
| F-04 | **컨테이너 변경 시 머즐 캐시 무효화** | `Assign`이 컨테이너를 바꿀 때 `MuzzleValid=false` | 1초 창을 줄임 | 없음 |

검증: **HandAim 237 → 252**(신규 15: `into-*` 동일성 5, 예외 동일성 5, 카운트 1, 소스 가드 4). 플러그인 빌드 경고 0.
브랜치 전체 검증은 `tests\verify.ps1`(32/32)로 확인한다.

## 3. 적용하지 않은 것과 이유(정직)

| 후보 | 왜 지금 안 하나 |
| --- | --- |
| **UI 매프레임 쓰기 억제**(`UiTick`의 `UiPlace/UiDim/UiFlattenScan/UiMeasureLevelUp`) | 꼬리(p90)의 유력한 용의자지만 **HUD/레벨업 배치를 건드리는 코드**다. 값을 캐시해 "같으면 안 쓴다"로 바꾸면 이론상 동일하지만, 먼저 F-01로 **UI 구간이 실제로 꼬리인지** 측정해야 한다(측정 없이 최적화하면 회귀 위험만 늘어난다). |
| **`Scan()`의 `Resources.FindObjectsOfTypeAll<Weapon>()`** | 초당 1회뿐이고, 이 함수는 **슬롯 소유권 안전장치**다(공유/모호 컨테이너 fail-closed). 비용 대비 위험이 맞지 않는다. |
| **스테레오 단일 패스(씬 3패스 → 1~2패스)** | 게임에 `Unity.XR.Management`/`Unity.XR.OpenXR` 플러그인이 **없다**(interop 확인). 커스텀 셰이더 인스턴싱 지원도 미검증 → R&D 항목. |
| **D3D12 전환** | 런타임은 `XR_KHR_D3D12_enable`을 제공하지만, 우리 네이티브 복사 경로가 **D3D11 전용**이라 재설계가 필요하다. |
| **포비티드/VRS** | VDXR이 해당 확장을 **제공하지 않는다**(D-150 실측) → 불가. |
| **GC 강제/힙 축소** | 관리 힙 1.9 GB는 게임 몫이고, 우리가 `GC.Collect`를 부르면 프레임이 더 튄다. 대신 **주파수 약수 고정**(부드러움)이 사용자 선택 항목이다. |

## 4. 다음 착용에서 쓸 것

```powershell
# 0.5 빌드를 설치한 뒤(브랜치에서 Prepare), 한 세션:
tests\headset-handaim.ps1 -Tag 0.5 -DisableVSync -RenderFrames 1 -EyePreset normal -PrepareAhead -DeferredGpuWait -TickProfile -UsePreinstalled -HeadsetWaitSeconds 600
```

- `TickProfile`이 켜지면 매 네이티브 창마다 `[OPENXR_HOST] TICK_PROFILE samples=… totalMsPerFrame=… read=… ui=…`가 남고,
  영수증 `tickProfile.sections`에도 들어간다 → **꼬리(p90 1.42 ms)의 범인을 실측으로 지목**한 뒤에만 F-05(UI 쓰기 억제)를 검토한다.
- 0.5는 **0.4의 순수 비교 대상이 아니다**(구조는 같고 우리 비용만 다름). 0.4와 0.5의 `cycleMs` 차이는 노이즈 범위일 것이며,
  판정 축은 `lateMs/frame` 분포와 TICK_PROFILE 구간이다.

## 5. 검증 결과(브랜치, 실측)

- 플러그인 빌드: 경고 0 / 오류 0.
- HandAim: **252 passed**(0.4의 237 + 신규 15).
- `tests\verify.ps1`: **32/32 PASS, stderr 0B**(게임이 stock인 상태에서 실행).
- 네이티브: 변경 없음(52 유지, `VhvrNativeHost.dll` 해시 `F7FDF2D6` 동일 — D-150에서 네이티브를 건드리지 않았기 때문).
- **소스 가드 2건 갱신(정직 기록)**: `HandAimDriver`의 busy/rejected 조기 반환에 `EndProfile(frameT0);`가 추가되어
  `tests\FlameEmitter`의 `source-busy-and-rejected-reads-pin` 문자열을 새 텍스트로 갱신했다(핀 동작 자체는 불변:
  여전히 `ReapplyLastHandPose()` + `PinFlameEmitters(Busy)` 뒤에 반환한다). 또한 리시트 라인에 `tickProfile=`이
  붙어 D-147/D-150의 문자열 가드 2건(`source-d147-quality-aa-probe`, `source-perf-profile-wired`)도 갱신했다.
