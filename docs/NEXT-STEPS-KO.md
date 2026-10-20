# 다음에 할 일 (사용자) — D-194 메인 메뉴 VR 복구; 헤드셋 실기기 확인 대기

## 지금 PC에 설치된 상태
- 게임 폴더에 **시험용 빌드**가 설치돼 있을 수 있음 (D-189 직전의 그 상태). 게임은 **꺼진 상태**, 자동 실행 안 함.
- **현재 작업 대상 release**: `<RepoRoot>\dist\VampireHuntersVR-0.9-final.zip` — D-193 화염 핀 수정 + D-194 메인메뉴 VR 기본값(`MenuFirst=true`) 복구 + **D-195 SteamVR 시야 뒤집힘/카드 회전 패치(Stage0 anchor rotation 중복 적용 제거)**. 크기 35,635,824 bytes; SHA256 `310801A44627715B4A3002BF27D73566800555FB62395BFF075ED388376FBBF7` (이전 SHA `0B3080FBBF04646BA4D008DD820BB3C1B379B06FEFA935427057B8F265CC06E3`). 현재 설치본에는 **D-195 패치가 아직 반영되지 않음** — 게임에 다시 install 명령을 돌리면 새 Stage0 DLL로 교체됨. 이전 ZIP은 `dist\VampireHuntersVR-0.9-final.zip.bak-preD195`로 백업됨. 게임은 실행하지 않았음. 헤드셋에서 메인메뉴 VR/스테이지 전환/SteamVR 정합/카드 정렬 확인 대기.
- 이전 zip: `dist\VampireHuntersVR-0.6-final.zip` / `-0.7.zip` / `-0.7.1.zip` / `-0.8.zip` — 모두 보존.

## 내가 해야 할 일 (우선순위 순)
0. **(중요) D-195 패치를 게임에 적용** → 새 0.9-final ZIP의 Stage0 DLL이 게임 `BepInEx\plugins\VampireHuntersVR.Stage0.dll`로 들어가야 함. install 명령을 한 번 더 돌려 새 SHA(`05705C2F...`)로 교체 (또는 `BepInEx\plugins\VampireHuntersVR.Stage0.dll`만 수동 교체). 그 다음에 실행.
1. **[실행] 말씀하기** → 에이전트가 게임을 1회 켠다. (자동 실행하지 않음)
2. **헤드셋에서 확인** (한 번에 모아서):
   - [ ] 메뉴 UI가 VR에 **고정**되어 보이는가 (머리를 따라오지 않음)
   - [ ] 위치 **2 m 뒤 / 크기 3.67 m 폭** 이 적당한가 (다르면 "더 위/왼쪽/멀리/크게" 알려주기)
   - [ ] **레이저**가 보이는가 (안 보이면 그 사실만 알려주면 됨)
   - [ ] 트리거로 **버튼이 눌리는가**
   - [ ] **스틱으로 리스트 스크롤**이 되는가 (너무 빠르면 "느리게", 너무 느리면 "빠르게" — config ScrollSpeed 조정)
   - [ ] **화염방사기 이펙트**가 총구/손을 따라가는가 — HMD만 좌우로 돌릴 때 이펙트가 머리 방향으로 끌려가지 않는지 확인. 결과는 구현 PASS가 아니라 실기기 판정.
   - [ ] 메뉴 뒤에 **검은 배경이 나오지 않는지** 확인 (검은 backdrop은 제거됨; `MenuBlackout`은 호환용 ignored 키)
   - [ ] 스테이지 시작 시 **UI가 사라지고 VR 게임플레이로 전환**되는가
3. **게임플레이에서 확인** (D-177 기능):
   - [ ] **자동 연사**: 크로스보우 등을 트리거 **누르고 있으면** 0.6초마다 계속 발사되는가 (놓으면 멈추는가)
   - [ ] **로봇팔 햅틱**: 좌팔→왼손 / 우팔→오른손 진동이 오는가
   - [ ] HUD가 읽을 만한 크기인가 (작으면 ScalePaused=2.0 또는 [Ui] Scale 조정)
4. **끝나면 "끝났어"** 라고 말하기 → 에이전트가 로그·영수증 확인 + 결과 문서화 + 필요 시 `uninstall` 로 원상복구.
5. **결정 필요**:
   - [ ] **헤드셋에서 in-game 손 UI ray 가 보이는지 확인** (D-189 의 1차 검증 게이트)
   - [ ] **검은 배경(EDF6 근사)** 이 마음에 드는지 — 아니면 진짜 backbuffer 캡처 방식 착수 여부 (VhvrNativeHost C++ 소스 확보가 선행돼야 함)
   - [ ] **보조사격(Y/B)**: 이미 구현됨 (WeaponRuntimeBridge secondary/alt fire = Y/B, D-186). 별도 착수 불필요 — 동작 확인만.
- **MenuFirst 배포 기본값**: D-194에서 0.9-final을 `MenuFirst=true`로 되돌렸어. 이전 D-193 안내(`false`)는 잘못된 값이었고, 이미 설치 설정도 true로 수정했어.

## 알아두면 좋은 것
- **되돌리기(원상복구)**: `<RepoRoot>\dist\VampireHuntersVR-0.6-final\uninstall.ps1 -GameRoot "<GameRoot>"`
- **설정 도구**: 배포 번들의 `VhvrSettings.exe` (install.bat과 같은 폴더) — FOV는 이제 슬라이더(X/Y), In-VR UI/MSAA/Anisotropic 포함.
- **메뉴 관련 config**: `BepInEx\config\local.vampirehuntersvr.extras.cfg` — MenuBlackout(검은 배경), ScrollSpeed(스크롤 속도), ScalePaused(메뉴 배율), MenuFirst, DistanceMeters, HorizontalFovDeg.
- **문서**: `docs\DECISIONS.md`(D-179~D-192), `docs\BUGS.md`, `docs\TODO.md`, `docs\PHASE_PLAN.md`
- **헤드셋 연결**: 실행 전에 Virtual Desktop을 켜고 헤드셋을 연결해 둘 것.

## 정직한 한계 (기억해 둘 것)
- 화염방사기 BeforeParticles 호출은 코드에 연결되고 source guard가 있지만, **헤드셋에서 실제 입자/시각 효과가 고쳐졌는지는 아직 검증되지 않음**.
- OpenXR controller binding은 Quest Touch 프로필 기준이며, 다른 기기는 이번 범위가 아님.
- 메뉴 ray/스크롤, 화염 emitter, Stage0 DLL이 헤드셋에서 함께 동작하는지 확인 필요. `hand-aim.json`에서 `beforeParticlesCalls` 및 `flameEmitterPin.stats.beforeParticles.samples`가 증가하는지 확인.

## 게임 실행 규칙 (사용자 지시 2026-10-15)
- 게임은 **Steam 경유로만 실행**한다. exe 직접 실행은 켜지지 않는다.
- 명령: `Start-Process 'steam://rungameid/2206270'` (AppID 2206270, Steam: C:\Program Files (x86)\Steam\steam.exe)
- 여전히 사용자가 '실행'이라고 말할 때만 켠다.