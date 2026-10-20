# D-138 — 여전함.mp4 / HMD와 녹화의 차이
Date: 2026-10-11 | Mode: agentic | Stage 6B/8 reopened | NOT_A_VISUAL_FIX

## 요구와 관찰
- 사용자: HMD의 손은 이상 없이 안정적이다. 녹화에서는 손/무기가 조준 방향과 이펙트 방향에 번갈아 나타난다. 상하는 손을 따르나, 좌우 이펙트는 HMD 시선의 반대 방향으로 움직인다. **정상 총알의 방향과 데미지를 보존하고 이펙트를 총알 방향에 맞출 것.**
- 입력 `<Recordings>\편집후\여전함.mp4`, SHA256 `8C6D607095FE58077B9984C232AA8AFF944F54738DF36C33E76012CECFE1A128`.
- 1280×720 H.264, 30fps, 835 video frames / 27.833333s; container 28.949s는 오디오를 포함하며 추가 영상 프레임이 아니다. 전체 순차 decode exit0, 64×36 gray 1,923,840 bytes = 835 frames. 원본 영상은 변경하지 않았다.
- 순차 contact sheet: frames210–257 (7.000–8.567s)와300–329 (10.000–10.967s), overview. 작은/크고 높게 나타난 전경 손·무기가 인접 프레임에 번갈아 보이고 화염이 총구와 떨어진 옆 방향에 보인다. 이는 2D 외형 관찰이지, 두 mesh의 존재나 3D 파티클 속도/실제 총알 소유권 증명이 아니다.
- HMD 안정성은 사용자의 관찰로 기록한다. 이 편집 영상에는 동기화된 HMD 영상/Unity frame marker가 없어 녹화 프레임을 로그의 특정 Unity frame에 임의 대응시키지 않는다.

## 이번 실행과 복구
- `<RepoRoot>\artifacts\headset-d087\d136a\run.json`: finished07:33:50Z, observation HOST_FAILED, Egypt_Map→MainMenu_2/source/player destroyed. 메뉴 전환 이유/사용자 종료 방식은 미확정. harness exit0는 복구 성공이지 gameplay gate PASS가 아니다.
- uninstall exit0, cleanupError null, stockVerified true. Journal terminal Recovered sequence27; current pointer는 이 **복구된** transaction을 가리키며 설치 상태가 아니다. d136a 소모됨.
- 이번 세션에서 독립 `<RepoRoot>\artifacts\devtools\Check-Stock.ps1` exit0: 121files / 8directories / 7,726,223,158bytes, pinned hashes와 write times 일치. 실제 게임/probe/이전 harness 프로세스 없음. VDXR registry 경로는 실행 전후 및 현재 동일.
- D-136 구현 커밋 `5e9e8352db95aac86060e18d399be7690da8f71f` 확인; 이후 다섯 문서의 실행 중 업데이트는 이번 세션 시작 시 미커밋 상태였다.

## 진단 실패 — offline PASS와 live coverage를 구분
- render-timing.jsonl은 **header, inventory, end의 3행뿐이며 sample 행은 0**이다. end.samples=1은 serialization 전에 증가한 시도 번호이지 성공한 sample 수가 아니다.
- 시작07:29:30.6038242Z, end07:29:30.7147468Z. `MissingMethodException: UnityEngine.ParticleSystemSimulationSpace MainModule.get_simulationSpace()` 때문에 시작 직후 관찰이 중단됐다. writerError null / dropped0 / accepted3 / written3은 파티클 읽기 실패나 커버리지 공백을 상쇄하지 않는다.
- 최초 inventoryTruncated43. actual camera callback, busy와 점멸의 동시성, descendant overwrite, particle velocity/space는 이 파일로 판정 불가. 설치 성공 및55개 writer/source tests PASS를 live getter 검증으로 해석하지 않는다.

## 발사와 VFX 경로 단서
- 동일 게임 frame4047의 SHOT_SCOPE: aimFrame4045, hand proxy fwd(-0.87,0.28,0.42), muzzle/vfx fwd(-0.97,0.03,0.23). post/before-restore에서도 차이가 유지된다. 이전 업데이트의 독립 aim과 살아 있는 총구가 달라질 수 있다는 런타임 단서다. 실제 영상 사건의 원인/총알 측정으로 과장하지 않는다.
- 정규화한 **로그의 소수점2자리 방향벡터** 비교는 해당4개 phase 모두 약18.93도다. 분석된88개 scope/outside 행 중5도 초과4행은 같은 사건의4개 phase이며4번의 독립 오류가 아니다. 그 외 sparse 행의 일치로 전체 시간의 정상 동작을 추정하지 않는다. Offline angle self/orthogonal fixtures와 분석 exit0, receipt <RepoRoot>\artifacts\d138-analysis\analysis-receipt.json.
- pinned native static evidence: Flamethrower.draw_shoot_vfx RVA46BD70은 낮은 aim_assist 분기에서 muzzle_flash_spawn.forward(필드1B8)를 읽고 vfx_flames_transform(2D0).forward를 쓴다. 다른 분기에서는 end-start를 사용한다. shoot_forward는 별도로 override transform을 사용하는 분기가 있다. 라이브 분기 선택과 실제 파티클 속도는 아직 미확인.
- 현재 WorldPoseScope는 weapon root의 world pose만 보존한다. 자손의 원래 애니메이션 자세까지 보존하거나 VFX를 독립 hand aim에 맞추는 기능은 아니다.

## Stage brief / 좁은 변경
Goal: 깨진 read-only 진단을 복구하고 정상 총알/조준을 바꾸지 않은 채 원인 증거를 보존.
Unknowns: native VFX branch, actual velocity, matched camera/Unity/video timing, offending renderer/bone writer.
Hypothesis: live muzzle와 published aim의 차이 및 서로 다른 render windows가 증상에 기여할 수 있다. 단순 yaw 부호 반전은 검증되지 않았다.
Plan: 실패한 simulation-space API 읽기를 제거하고 측정 불가 이유를 header/sample에 명시 → 회귀/전체 build tests → 새 태그로 진단만 가역 준비. 손/무기/애니메이션/파티클 좌표는 추측 수정하지 않는다.
Verify: simulation-space API 호출 부재 및 명시적 증거 공백 fixture, 기존 hand/shot/runtime/writer 검사. 라이브 카메라 callback/시각 수락은 미검증 유지.
Risk/cost: simulationSpace는 측정하지 못한다. 이 fallback은 D-138에 공개 기록하며 나머지 read-only 샘플로 완전한 particle 분석을 주장하지 않는다. 새 테스트를 지금 요구하지 않고 자동 게임 실행도 하지 않는다.

## 다음 실제 VFX 수정의 조건
정상 총알/공격이 사용하는 최종 ray/endpoint를 동일 발사 사건의 VFX에 사용해야 한다. HMD yaw를 임의로 반전하거나 총알을 현재 VFX 방향으로 돌리는 것은 반대의 수정이다. 기존 Vector3 detour는 D-097 native crash 이력이 있어 재활성화하지 않는다. 별도의 renderer timing 문제는 desktop과 HMD를 구분해 다룬다. TOOL-069/070/071/072와 정식 게이트는 OPEN.

## 이번 offline 검증
- 전체 verifier exact-process exit0,31/31check receipts PASS at07:44:37Z,managed build0warnings0errors. HandAim195/195,ShotScopes28/28,WeaponRuntime67/67,RenderTiming57/57(새2개 unsupported-getter/explicit-gap guard 포함),Native41fault+4exact+4flip+4shape PASS. 전체 verifier stderr0B. 실제 game/runtime/visual 수락 아님.
- 변경 production source는 HandRenderTiming.cs뿐이며 ReadHands early-return, hand driving, shot scopes, cadence/damage, render scheduling은 그대로다. 기존57개 fixture 중 Unity 런타임 getter를 실행하는 검사는 없으므로 라이브 작동을 추정하지 않는다.
- 새 d138a는 기존 d136a config의 Output만 바꿔 가역 설치/독립 검사 진행. 설치 이후 rebuild/fullverify 없음, game/probe 자동 실행 없음. 완료 receipt 전 READY 판정 없음.
- **준비 완료:** installer0/232files/Installedseq14at07:46:53Z/independentverify0/prepare0/empty stderr. LiveDLL/config/pins/currentpointer/stockoutsideownedhash/write times/VDXR불변 PASS. CurrentMODDED/READY_PREINSTALLED_NOT_LAUNCHED(d138a). Native source변경 없으나 verifier rebuild로 hash변경,최종 pins는 prepare.json/payload-pins.json. 새로운 VFXfix가 아니며 지금 실행/헤드셋 테스트를 요구하지 않는다.
- 최종 build/live/pins3종 동일 재확인, 이미 빌드된57fixture 실행파일만 추가3회 PASS(재빌드 없음/설치payload불변). 모든 반복 stderr0B;git diff --check PASS. 게임/probe/설치·검증 worker 종료 확인. 실제 VFX 방향 보정과 손 점멸 수정은 아직 하지 않았다.