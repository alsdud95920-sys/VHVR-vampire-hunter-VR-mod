# D-136 — 검증된 진단 빌드, 실제 사건의 원인은 아직 미확정
Date: 2026-10-11 | Mode: agentic | Stages6B/8 soft follow-up | NOT_HEADSET_ACCEPTANCE

## 검증 결과
- `1a48aaae7d6fe84d447ba537d7ad2e9f06e6d0d2`는 정확히 6개 문서 커밋이다. 최초 worktree clean을 확인하고 이어 작업했다.
- D-135 `final-verification.json`, decode/probe/showinfo 행/gray bytes, 테스트 exit/count/build/stock 로그를 실제로 읽어 재검증했다. 영상 입력 두 SHA256 동일,2160/3240 decode 행,2160 대응식 `r=512+ceil(e/2)` 오류0,maxcentralMAE1.161. 원본36.466667/36.500000/36.533333 closeup와 연속 contact sheet에서 전경 무기/팔 급변이 보인다. 손 mesh가 두 개라는 증거나 particle velocity는 아니다.
- D-135 fake timing16/16 재실행. 전체 검사 final exit0/31개 check receipt,각 `Exit==Expected`;37recon/18settings,195HandAim/28ShotScopes/67WeaponRuntime 포함. 신규55render-timing(실제 writer+소스 검사) 및 추가 실행3회 모두 PASS. Native41fault/4exact/4flipped/4shape PASS; XR runtime/game launch를 수행한 테스트가 아니다.
- 최종 managed build0warnings/0errors. 외부 영상 분석 helper의 기존net6EOL/nullable15warnings 및 이미지 Fontconfig warnings는 숨기지 않는다. full verifier stderr는 비었고, CLI 인수 없음/게임 내부 output거절 테스트의 stderr/예상 exit2/1은 의도된 음성 테스트다.
- full verifier가 Native DLL을 다시 빌드하므로 최종 managed/native hash는 d130a와 달라졌다. 최종 release를 새 태그로 설치한 뒤 재빌드하지 않는다. TOOL-072 전체 개선은 미완료이며 기존 함정을 우회하지 않고 순서를 준수했다.

## 추가한 관찰 (수정 아님)
1. `ReadHands` 직후 result/frame/seq/last successful sequence를 기록한다. busy6/rejected2 조기 return은 그대로다.
2. Tick 완료와 왼/오른손 실제 `DrivenFrames` 증가를 구별한다. busy frame의 post-driver 진단 공백을 메우되 보정을 강제로 실행하지 않는다.
3. 기존 native Player.Update prefix/finalizer의 gaze 실행 횟수/순서/예외 처리는 보존한다. 전후 transform snapshot만 추가했다.
4. source/left/right camera만 `FireOnPreCull`/`FireOnPreRender`의 Camera 참조 postfix로 관찰한다. 설치 성공만으로 실제 callback/hook 확인을 주장하지 않는다. Vector3 값 인수 detour는 사용하지 않는다.
5. camera/mount/nativeWeaponBase/weapon/muzzle/flame/renderer/rootbone/candidatebone/emitter의 실제 ID, parent/path,world/local pose를 기록한다. renderer sharedMesh ID/name/rootBone과 제한된 bones 목록을 같이 기록한다.
6. particle `simulationSpace`와 count/playing은 읽기만 한다. 실제 velocity,custom-space transform,projectile ownership는 측정하지 않았다. 소스의 deprecated read-only alias는 pinned interop에서 빌드 확인했으며 관련 경고만 좁게 억제했다.

## 비용과 증거 한계
- 첫 관찰부터180초,24transform/16renderer/8particle/skin당4candidatebones. 모든 객체를 포괄하지 않으며 enumeration 순서에 따른 누락 가능. `inventoryTruncated`를 반드시 검사한다.
- Unity getter/JSON은 main thread에서 수행한다. worker는 문자열만 받고 Unity 객체를 건드리지 않는다.512queue,100000rows,256MiB,65536char/row. `TryAdd`는 queue공간/디스크/native mutex를 기다리지 않는다. CPU/GC 부하 자체는 실제 플레이 전 정량 미검증.
- byte cap/queue-full/error는 누락이며,serial gaps/header/end와 receipt errors/drops를 먼저 검사한다. 종료 marker의 writtenSoFar는 비동기 drain 전 값이다. 강제 종료는 마지막 flush를 잃을 수 있다. 완전한 커버리지/무오류를 추정하지 않는다.
- `hostPhase/submitted/renderUntil`은 host의 LateUpdate에서 전달한 마지막 snapshot이다. native Player.Update 관찰에서는 이전 LateUpdate 상태일 수 있다. 실제 camera ID/frame/enable 상태와 함께 해석하고 native render-thread timing의 정확한 동기화로 주장하지 않는다.
- videoPTS↔Unityframe,matchedHMD↔desktop clock은 여전히 미동기화다. UTC만으로 프레임 정밀 일치라 하지 않는다. native phase 증거는 game clock 안에서 비교한다.

## 보존 및 배포 계약
- 조준/데미지/발사 cadence,animation,particle space,source/eye render scheduling,native ABI/source,OBS/OpenXR runtime은 변경하지 않았다.
- `RenderTimingDiagnostics=true` typed schema/harness/installer flag; `-NoRenderTimingDiagnostics`로 비활성화 가능. VR 설정 메뉴 연결은 기존대로 미구현이며 수락을 주장하지 않는다.
- fresh `d136a`,transaction/payload/config/hash와 stock outside owned roots를 독립 확인한다. d130a는 소모됨. 자동 game launch/probe 없음;사용자 실행 명령에만 `<RepoRoot>\tests\headset-handaim.ps1 -Tag d136a -DisableVSync -UsePreinstalled` 한 번 실행한다.
- **독립 확인 완료:** installer0/232files/GameLaunchedfalse,Installedseq14/currentpointer,liveDLL+config+build+pins,원본 영역hash/write times,VDXR불변 PASS. 현재MODDED/READY_PREINSTALLED_NOT_LAUNCHED. 정확한 해시는 최신PHASE_PLAN및d136a/payload-pins.json/prepare.json참조. 설치 뒤 추가 productionbuild/fullverify없음.
- 종료 후 기존 harness가 가역 uninstall과 전체121파일/8폴더/7726223158bytes의 원본hash/write times 복구를 검사한다. 설치 후 full verifier/rebuild 금지.
- 사람 참여는 `<RepoRoot>\docs\playtests\PLAYTEST-D136-FINAL-KO.md`의2–3분 배치 테스트만. 이것은 진단 빌드의 최종 참여 체크이며 **버그 수정 완료/릴리스 최종 수락 테스트가 아니다**. TOOL-069/070/071/072와 정식게이트 OPEN 유지.