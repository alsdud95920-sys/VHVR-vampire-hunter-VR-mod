# Vampire Hunters VR 화면 설정 (VhvrSettings.exe, 0.7.1)

이 프로그램은 `BepInEx\config\vhvr-openxr-host.json`의 다음 키만 고칩니다.
**화면 크기**(`EyePreset`/`EyeWidth`/`EyeHeight`/`EyeScale`), **In-VR UI 크기**(`uiScale`), **눈당 MSAA**(`EyeMsaa`), **이등방성 필터링 16x**(`Anisotropic`), **렌더 FOV 배율**(`FovScaleX`/`FovScaleY`), **스냅 턴**(`SnapTurnDegrees`). 그 외 키는 건드리지 않습니다.

> 0.6에서 잠깐 끼어 있던 `GraphicsQuality` 다이얼(Unity 품질 레벨 강제 지정)은 **0.7에서 제거**됐습니다(D-187). 이제 게임 자체의 품질 설정(Steam 실행 시 고른 값 / PlayerPrefs)을 그대로 사용합니다.

## 실행
- `VhvrSettings.exe`를 더블클릭합니다. **`install.bat`과 같은 폴더**에 있습니다. (같은 폴더의 `.dll`/`.deps.json`/`.runtimeconfig.json`이 함께 있어야 합니다)
- 설정 파일을 자동으로 찾습니다: **`install.bat`이 남긴 `vhvr-game-root.txt`(설치한 게임 폴더)** → exe 옆 → 게임 루트 → `BepInEx\config\`. 못 찾으면 `Find...`로 직접 고르세요. (`install.bat`을 먼저 실행해야 자동으로 찾습니다.)
- **게임을 끄고** 바꾸세요. 설정은 게임을 시작할 때 한 번만 읽습니다.

## 고르는 값 1 — 화면 크기 (그림 크기 = 양쪽 눈 합계)
| 선택 | 눈 하나당 | 양쪽 눈 합계 | 프레임당 픽셀 |
|------|-----------|--------------|----------------|
| Low 0.8 | 1536x864 | 3072x1728 | 2.7 MP |
| Normal 1.0 (기본) | 1920x1080 | 3840x2160 | 4.1 MP |
| High 1.2 | 2304x1296 | 4608x2592 | 6.0 MP |
| Custom 0.5-2.0 | 배율 × 1920x1080 | 배율 × 3840x2160 | 자동 표시 |

큰 값 = 더 선명하지만 프레임이 떨어집니다. `EyePreset` / `EyeWidth` / `EyeHeight` / `EyeScale` 4개 키에 매핑됩니다.

## 고르는 값 2 — In-VR UI size (VR 안의 HUD/글자 배율)
- 범위 **0.50–3.00**, 기본 **1.50**.
- 해상도가 아니라 VR 안 UI 크기이며, `VhvrExtras.dll` 패치 플러그인이 `vhvr-openxr-host.json`의 `uiScale`을 읽어 HUD 전체에 곱합니다(플러그인 설정 파일의 `UiScale`보다 우선).
- 키는 `uiScale`(소문자, 1개). `VhvrSettings.exe`로 바꾸지 않으면 install.ps1이 처음에 `1.00`으로 씁니다.

## 고르는 값 3 — Eye MSAA (눈당 안티에일리어싱)
- **1 = off**, **2 / 4 / 8 = 그만큼 샘플**. 1/2/4/8 외의 값(예: 3, 5, 16)은 거부하고 shipped 값 4로 유지합니다.
- 기본 **4** = 출시 기본값. 키는 `EyeMsaa`. **`install.ps1`이 첫 설치에서 항상 `EyeMsaa = 4`를 박아 둡니다**(옛 설정파일에 이 키가 없어도 shipped 4로 동작, 있어도 첫 실행 값이 4로 일관).
- 더 많은 샘플 = 가장자리가 매끄러워지지만 GPU 비용이 늘어납니다. (호스트는 1·2·4·8 외 값이면 로그 + shipped 4로 폴백합니다 — 조용히 바꾸지 않습니다.)

## 고르는 값 4 — 이등방성 필터링 16x (Anisotropic filtering)
- **off** 또는 **on(16x)**. 키는 `Anisotropic`(bool, 소문자 키).
- **기본 OFF** — D-104 의 옵트인 정책. **`install.ps1`이 첫 설치에서 항상 `Anisotropic = $false`를 박아 둡니다**.
- on 으로 켜면 비스듬한 벽/바닥의 텍스처(멀리 있는 표면)가 선명해집니다. GPU 텍스처 유닛 단계 비용(거의 없음). **fps 비용은 아직 측정하지 않았습니다**(첫 PLAYTEST-D104 에서 측정 예정).
- 0.7 의 호스트(`OpenXrHost.cs:879`)는 이미 이 키를 받습니다 — 0.7.1 은 **사용자 도구(VhvrSettings.exe UI/CLI)** 로 노출하는 것이 전부입니다. 호스트 코드는 변경되지 않았습니다.

## 고르는 값 5 — 스냅 턴 (right stick, D-211)
- **Off / 30 deg (기본) / 45 deg** 세 가지뿐입니다. 키는 `SnapTurnDegrees`(숫자 0 / 30 / 45).
- **Off**면 오른쪽 스틱 좌/우가 아무 일도 하지 않습니다. **30/45**면 한 번 밀 때 그 각도만큼 시야가 돌고, 스틱을 놓았다가 다시 밀어야 다음 칸입니다.
- 회전은 **제자리**에서 일어납니다(플레이어 위치 불변). 앞으로 밀면 새 방향으로 이동합니다.
- 목록에 없는 값을(예: 60, 문자 `"45"`) 파일에 직접 넣으면 프로그램은 shipped 값 **30**으로 표시하고, 호스트도 30으로 동작하며 로그에 경고 1줄을 남깁니다. 저장하면 0/30/45 중 하나만 씁니다.
- VR 안에서 조작하기 전에 **양 스틱 클릭으로 리센터**를 먼저 하세요(메뉴 패널이 정면에 옵니다).

## 저장 동작
- `Apply`를 누르면 **저장 전에** `vhvr-openxr-host.json.<시각>.bak` 백업을 만듭니다.
- 이 프로그램은 **`EyePreset`/`EyeWidth`/`EyeHeight`/`EyeScale` + `uiScale` + `EyeMsaa` + `Anisotropic` + `FovScaleX`/`FovScaleY` + `SnapTurnDegrees`** 만 바꾸고 나머지는 그대로 둡니다. In-VR UI size를 건드리지 않으면 `uiScale`도 안 건드립니다.
- 범위를 벗어난 값(눈당 512–4096 밖, 배율 0.5–2.0 밖, UI 0.5–3.0 밖, MSAA 1·2·4·8 밖)은 **거부**합니다(조용히 바뀌지 않음).
- 옛 설정(눈당 2064x2208)을 쓰던 파일이면 "Use 1920x1080 base" 버튼으로 새 기준(양안 3840x2160)으로 옮길 수 있습니다.

## 명령줄(스크립트용)
```
VhvrSettings.exe --config <경로> --print
VhvrSettings.exe --config <경로> --validate
VhvrSettings.exe --config <경로> --preset high --apply
VhvrSettings.exe --config <경로> --preset custom --scale 1.5 --apply
VhvrSettings.exe --config <경로> --ui-scale 1.5 --apply      (In-VR UI size, 0.5–3.0)
VhvrSettings.exe --config <경로> --msaa 4 --apply             (눈당 MSAA, 1·2·4·8, 0.7.1+)
VhvrSettings.exe --config <경로> --aniso --apply              (이등방성 필터링 ON, 16x, 0.7.1+)
VhvrSettings.exe --config <경로> --no-aniso --apply           (이등방성 필터링 OFF, 0.7.1+)
VhvrSettings.exe --selftest <폴더>                            (파일 왕복 자체 점검)
VhvrSettings.exe --config <경로> --form-selftest              (창을 400 ms 띄워 로드/잘림 점검)
```
`--form-selftest`는 **설정 파일 경로가 필요합니다**(`--config` 또는 인자). 경로가 없으면 로드 실패로 `SELFTEST FAIL ... loaded=False`(exit 2)를 냅니다 — 조용히 통과하지 않습니다.
> 0.7 의 본문은 "`--msaa` CLI는 아직 노출하지 않았습니다"라고 적혀 있었지만, **`--msaa`는 0.7 부터 이미 노출되어 있었습니다**(`(int)ParseDouble(...)` 로 정수만 받음, 1/2/4/8 외 값은 호스트가 shipped 4 로 폴백). 0.7.1 은 이 문장을 정직하게 지우고, `--aniso` / `--no-aniso` 를 같은 방식으로 추가했습니다.

## 미확인(정직)
- 눈 이미지 비율은 **16:9**입니다. 헤드셋 눈 시야 비율(Quest 3 ≈ 0.94)과 달라 런타임이 늘려 표시할 수 있습니다.
- 각 배율의 실제 프레임 비용은 **아직 측정하지 않았습니다**(첫 착용에서 측정 예정).
- MSAA의 1/2/4/8 차이가 0.7에서 실제 FPS에 어떤 영향을 주는지 **아직 측정하지 않았습니다**(첫 착용에서 측정 예정).
