# D-109 — 사격이 오른쪽으로 꺾이는 원인 분석

## 범위와 판정
- 사용자: D-108 UI가 눈앞에 나오고 레벨업 선택이 잘 되며 카드가 뒤집히지 않음. 이번 UI 문제 TOOL-062는 사용자 수락으로 CLOSED. 전체 Stage 8/VR 수락을 뜻하지 않음.
- 사용자: 마지막 빌드에서 총알이 오른쪽으로 발사됨. 화염방사기 모델은 조준점을 추적하지만 공격 범위는 오른쪽, 체감 약 90도.
- **확인된 결함:** 자식 총구에 맞추려고 부모 카메라를 이동/회전해 그 총구 자체를 다시 움직임. 원본 카메라의 자식 무기 컨테이너가 이 보정에 동반 회전한다. 실제 d108b 사격 로그는 보정 후 방향 불일치를 기록한다.
- **미확인:** 정확한 탄착 벡터, 화염 공격 콜라이더의 실시간 자세/부모, 고정 90도 축 차이의 존재. 화염방사기 개별 호출과 일반 사격 호출을 기존 로그로 분리할 수 없음.
- 분석 전용 테스트/문서만 추가. 제품 소스/플러그인, 런타임, 게임 설정은 변경하지 않음. 이번 작업에서 게임을 실행하지 않음.

## 실제 빌드와 종료 확인
`artifacts/headset-d087/d108b/run.json`: plugin SHA256 BF7BFC3395EDC8FEE475AFFDD84CEF6947581D3A7329EE709D8AFFB45212BADB. 현재 Release DLL 해시 일치.
GAMEPLAY LOCKED=2026-10-10T14:48:42.3906332Z. 이후 Maze_Small -> MainMenu_2에서 기존 카메라/플레이어 소유권 가드로 HOST_FAILED. 이는 UI/사격 수락 판정과 별개이며 입증된 네이티브 크래시가 아님.
하네스/게임 종료 확인. InstallExit=UninstallExit=0, stockVerified=true, cleanupError=null. stock.log는 121 files / 8 directories, 해시/쓰기 시각 복구 확인. VDXR runtimeBefore=runtimeAfter. 이미 종료했으므로 stop.request나 중복 실행 불필요.

## 1. 부모 카메라를 조준시키는 보정의 부작용
실제 구현 연결:
- `src/interaction/HandAimDriver.cs:128-136`: 무기를 source.transform 아래에서 찾고 **source의 직계 자식 컨테이너**를 구동 대상으로 선택.
- `HandAimDriver.cs:194-211`: 컨테이너를 회전해 첫 총구의 forward를 컨트롤러 방향에 맞춤.
- `src/interaction/HandReticle.cs:23-25`: 같은 첫 총구의 position/forward로 조준점 계산. 이는 실제 발사 판정의 측정값이 아님.
- `src/interaction/HandFireTrigger.cs:37-44`: source 카메라의 자세를 총구 position/forward로 바꾼 뒤 player.fire 호출, 종료 시 카메라 복원.
- `src/interaction/HandShot.cs:113-129`: shoot_forward 진입에서 살아 있는 muzzle Transform을 인자로 대입하고, **같은 부모 카메라를 또 총구 방향으로 변경**. finalizer는 부모만 복원.

카메라 세계 회전 C, 카메라에 대한 총구 상대 회전 L이면 총구 세계 회전은 C*L. 이를 원하는 방향 D로 맞춘 상태에서 카메라를 D로 바꾸면 총구는 D가 아니라 **D*L**이 된다. 부모 변경은 자식의 localRotation/localPosition을 보존하기 때문.
예: 카메라 0도 / 총구 상대 +90도면 원래 총구 +90도. 카메라를 +90도로 돌리면 자식 총구 +180도. 한 번 더 live 총구에 맞추면 카메라 +180도 / 총구 +270도. 이 예의 90도는 재현용 값이지 실제 게임에서 측정된 상수가 아니다. 위치 변경도 같은 구조로 자식 오프셋을 다시 더해 발사 원점을 이동시킬 수 있다.
FireTick과 PrefixShoot가 중첩되면 두 번, 게임 타이머에서 shoot_forward만 실행하면 한 번 발생할 수 있다. 실제 호출별 중첩 깊이는 기존 로그로 확정 불가.

## 2. 실제 로그가 보정 실패를 보여 줌
`artifacts/headset-d087/d108b/game.log` 초기 [SHOT] 행:
```
muzzleFwd=(0.84,-0.55,-0.02) camFwd=(0.34,-0.42,0.84) camToMuzzleDeg=60.4
muzzleFwd=(0.34,-0.42,0.84) camFwd=(-0.61,-0.09,0.79) camToMuzzleDeg=60.4
```
이 행의 muzzleFwd/camFwd는 부모를 변경한 **후** 읽는다. 코드가 의도한 일치가 실제로 안 된다. 다음 행의 muzzleFwd가 앞 행의 camFwd와 같은 패턴도 있다. 다만 프레임/무기 ID가 없어 동일 발사의 중첩인지 별도 발사인지는 확정할 수 없다.
`camToMuzzleDeg`는 카메라 변경 **전** 계산값으로, 최종 탄착 오차나 고정 축 차이가 아니다. 기존 maxCameraToMuzzleDeg=159.22182도 실제 총알이 159도 틀어졌다는 뜻이 아님.

최종 hand-aim.json:
- shootCalls=19047, overrides=19047, cameraAimedShots=19047, argNull=19047, shot.error=null.
- fireCalls=11691, fireFailed=false. 진입/보정 실행은 확인되지만 올바른 발사 방향을 입증하지 않는다.
- 하네스 관측 중 양손 muzzleToHandForward 최대 약 0.028도. LateUpdate 구동 후 값이라 사격 순간 부모 변경의 부작용을 잡지 못한다.
- tracerPatched=false, tracers=0. 실제 탄도 측정 없음. 기존 Vector3 IL2CPP detour 크래시 때문에 tracer를 다시 켜지 않음.

## 3. 화염방사기에는 별도 공격 경로 확인이 필요
설치된 interop를 기존 read-only Recon으로 조사:
- Flamethrower : Weapon, shoot(bool), update(float), draw_shoot_vfx(Vector3, Vector3, float, int).
- Flamethrower.vfx_flames : ParticleSystem, vfx_flames_transform : Transform.
- Flamethrower_Projectile.projectile_collider : CapsuleCollider, OnTriggerEnter/Exit, burning_enemies.
- Flames / Flames_Projectile_V2도 OnTriggerEnter/Exit와 burning_enemies를 가짐.

즉 화염 효과, 공격 영역, 모델 총구가 모두 같은 Transform이라고 가정할 수 없다. `shoot_forward` 하나의 보정만으로 연속 화염 공격 전체를 덮는다고 단정할 수 없다. 단, 이는 **타입/멤버 구조만 확인**한 결과이며 게임 네이티브 함수의 실제 호출 관계나 피해 판정 코드를 증명하지 않는다.

## 권장 수정 방향 — 아직 미구현
1. 컨트롤러로 계산한 손별 월드 aim position/rotation을 사격 전에 독립 스냅샷으로 보관. 모델 Transform을 발사 원천으로 반복해서 읽지 않음.
2. shoot_forward의 Transform 인자는 움직이는 카메라 자식 총구 대신 부모 없는 손/무기별 aim proxy로 제공하는 방안 검증.
3. 카메라 의존 경로를 당장 유지해야 한다면 카메라 변경 전 모든 해당 손 컨테이너의 월드 자세를 저장하고 변경 후 유지, 예외/중첩/양손 호출에서 역순 복원. proxy만 추가하고 부모 회전을 그대로 두면 모델/화염 공격 영역 문제는 남을 수 있음.
4. 무기별 ID/타입/슬롯/프레임/중첩 깊이, 보정 전후 총구 및 독립 aim 방향/원점 기록. 화염의 vfx_flames_transform과 실제 projectile_collider 세계 자세/축/부모를 별도로 측정.
5. 축 차이가 실측될 때만 무기별 보정값 도입. 전역 -90도 회전이나 조준점만 이동하는 임시 처방은 금지.
6. 단발/연사와 화염, 양손 동시/분리, 머리와 손 방향 불일치, 예외 복원 테스트 후 짧은 헤드셋 벽 조준/화염 피해 영역 테스트로 수락. 현재 TOOL-044/063은 OPEN.

## 재현/검증
`tests/ShotParentAnalysis/ShotParentAnalysis.csproj`: 기존 순수 회전 수학으로 부모 변경 시 자식 오프셋 중복, 중첩, 부모 복원에 따른 시각적 은폐, 월드 자세 유지 대조군, 원점 이동, 실제 로그 벡터를 재현. 20/20 PASS, exit=0, stderr 비어 있음. 실제 반올림 로그 벡터 각도 재계산은 60.113도/60.427도. 기존 HandAim 156/156도 재실행 PASS. Unity나 게임 실행/탄착 수락이 아님. 실행 기록은 `artifacts/shot-d109-analysis/math.*`.
타입 구조 조사: `artifacts/shot-d109-analysis/shape/assembly-recon.json`, `flame-shape/assembly-recon.json`. 필터 제한/스텁 본문 한계를 유지. 콘솔 출력을 -First로 중단한 첫 조사 명령은 완료 exit를 입증하지 않으므로, 직접 DLL 재실행 exit=0 기록으로 대체.