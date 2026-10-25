# 03 기존 레포에서 참고할 것

기존 레포: GitHub `Lunatic2339/anseong-steel`, 로컬 `C:\Users\anthony\Desktop\Capstone\anseong-steel`.

- 기준 브랜치는 `develop`(10/15). 10/15 로컬 미커밋 작업은 `backup/local-1015` 브랜치에 있다(`ControllerArmFollower.Practice.cs`, 개선된 `ArmReachCalibration.cs` 등).
- 아래 경로는 기존 레포 루트 기준이다. `AS/` = `AnseongSteel/Assets/`.

**언제 가져오나**: "언제" 칸에 지시서 번호가 있으면 그 지시서에 원본 파일이 이미 적혀 있다. "M2(예정)"처럼 마일스톤만 있는 것은 아직 지시서가 없으니, 그 마일스톤 지시서를 쓸 때 이 표에서 배정한다. 10/17 기준 v2에 들어온 것은 `BossUpdatedCompatible.fbx`와 `Idle`·`Walk`·`Punch`.fbx뿐이다(옛 레포와 같은 파일).

**원칙**: 파일을 통째로 복사하지 않는다. 여기 적힌 로직과 줄만 읽고 새 구조(네임스페이스, asmdef, 서버 권한, Inspector 수치)로 다시 쓴다. 예외는 "그대로 가져가도 됨"으로 표시한 순수 함수 파일뿐이다.

## 그대로 가져가도 되는 순수 로직

| 파일 | 줄 | 쓸 곳 | 언제 |
|---|---:|---|---|
| `VR_test/Assets/Scripts/MechaArmStability.cs` | 68 | `Core`: 팔 끝 도달 완화(SoftDistance), 팔꿈치 방향 안정화(StableHint) | M1-03 |
| `AS/_Project/RobotPlay/Runtime/IntegratedLanRules.cs` | 20 | `Net`: 시퀀스 wrap 비교, IPv4 검증 | M3(예정) |
| `AS/_BossPrototype/Runtime/BossMovement.cs` | 213 | `Boss`: 유진 원본 이동. 서버 전용으로. `MoveUp/Down` → `MoveForward/Back` | M2-04 |
| `AS/_BossPrototype/Runtime/BossRotation.cs` | 175 | `Boss`: 유진 원본 회전(가속·제동). 143행 새 요청마다 RotationStopped 발생 → 깜빡임 수정 | M2-04 |
| `AS/_BossPrototype/Runtime/BossHeavyPunch.cs` | 171 | `Boss`: 애니메이션 이벤트 시점 1회 판정 | M2-02 |
| `AS/_Project/HudUI/Runtime/SuitDamageState.cs`, `SuitWireframe.cs`, `SuitHudDetail.cs` + `Resources/HudUiLines.shader`, `HudUiText.shader` | 37·67·97 | `UI`: 부위 손상 와이어프레임 HUD | M4(예정) |
| `AS/_BossPrototype/Runtime/BossAnimationEvents.cs` | 12 | `Boss`: 애니메이션 이벤트 전달 | M2-02 |
| `AS/_BossPrototype/Runtime/Combat/Patterns/BossAttackPattern.cs`, `BossHeavyPunchPattern.cs`, `BossGrab.cs` | 12·9·11 | `Boss`: 패턴 베이스 | M2-02 |
| `AS/_Project/02_Art/PilotV04/Runtime/PilotLocalView.cs` | 30 | `Pilot`: 로컬 파일럿 머리 숨김 | 파일럿 몸을 넣을 때 |
| `AS/_Project/RobotPlay/Runtime/MechaVRBossTarget.cs` | 34 | `Combat`: 피격 대상 마커. 팔 라벨만 int로 | M1-05 참고 |

## 로직만 참고해 다시 쓸 것

| 로직 | 원본 | 새 구조에서 바꿀 점 | 언제 |
|---|---|---|---|
| 잽/훅 궤적 분류 | `AS/_Project/01_Scripts/01_Core/PunchTrajectoryRecognizer.cs` (상태머신 93-136, Evaluate 160-208) | 공격자 PC에서 실행. 문자열 reason → enum. 순수 클래스 + 테스트 | M1-04 |
| 연계·부스트·가드 규칙 | `AS/_Project/RobotPlay/Runtime/RobotCombat.cs` Hit 132-171, Boost 82-88 | 서버는 `Hit(side, kind, t)`만 받음. 클래스 기본값 = 기획 초기값. 미사용 필드 제거. 가드 판정은 새로(01_combat_rules) | M1-05, 가드 M1-06 |
| 개인 팔 도달 매핑 | `ArmReachCalibration.cs` — **`backup/local-1015` 버전** (`MapRadius` 69-103, `MapPractice`, `DistanceForBend` 64-67) | 거의 그대로. 네트워크 모드에서도 켬 | M1-02 |
| 팔 IK | `AS/_Project/01_Scripts/04_VR/ControllerArmFollower.Practice.cs` — **`backup/local-1015`에만 있음** (bind-frame 재구성 41-42·136-137, 내부구 94-105, swivel 제한·히스테리시스 185-193, 손목 156-165) | 이 방식을 기본 solver로. partial·진단 프로퍼티 정리. develop의 `ControllerArmFollower.cs` 123-124 누적 회전 방식은 쓰지 않음 | M1-03 |
| 팔꿈치 힌트 | `ControllerArmFollower.cs` `ControllerElbowHint` 209-228, `RigidWristConstraint.cs` 13-28 | swivel 선택 참고 | M1-03 |
| 주먹 포즈 | `AS/_Project/01_Scripts/04_VR/DefaultFistPose.cs` | Mixamo 본 이름 탐색은 에디터 쪽으로 | M1-03 |
| XR 포즈 읽기 | `AS/_Project/01_Scripts/04_VR/ArmMotionInput.cs` 204-219 | 위치·회전·추적 상태를 같은 장치에서 묶어 읽기만 참고 | M1-02 |
| 보정 UX | `AS/_Project/RobotPlay/Runtime/RobotPilotInput.cs` 216-243 (어깨 → 가드 → 도달, 단계마다 그립 놓기) | 기체 점검 흐름으로 | M1-02 |
| 그립 회전·knuckle 오프셋 | `VR_test/Assets/Scripts/MechaVRHandTarget.cs` 121-187 | 본 이름은 Inspector 참조로 | M1-03 |
| 보스 접촉 정지 | `AS/_Project/RobotPlay/Runtime/MechaVRBossContact.cs` | 보스 bounds 브로드페이즈 추가. 루트 직접 이동 금지. 부위는 이름 문자열 대신 컴포넌트로 | M1-05 |
| 보스 공격 상태기계 | `AS/_BossPrototype/Runtime/Combat/BossCombatController.cs` (Windup→Active→Recovery, Stun, Dead) | NetworkBehaviour, 서버만. 서버 시간. 디버그 telegraph 제거. 미사일 경로 제외 | M2-02 |
| 보스 공격 데이터 | `AS/_BossPrototype/Runtime/Combat/BossAttackDefinition.cs` | enum을 계획한 4개 기술로 축소. `IsSword` 분류 수정 | M2-02 |
| 보스 애니메이션 | `AS/_BossPrototype/Runtime/BossAnimationDriver.cs` | 서버 상태 + 시작 틱 복제, 클라이언트가 CrossFade. Stagger·Death 재생 추가 | M2-02 |
| 좌우 연타 판정 | `AS/_BossPrototype/Runtime/Combat/Patterns/BossAlternatingCombo.cs` | 순간 점 판정 → sweep | M2-03 |
| LAN 접속 | `AS/_Project/RobotPlay/Runtime/RobotPlaySession.cs` 184-224(승인·직결), 274(소유 검사), 326-330(협동 이동 공식) | 이 부분만. 나머지 클래스 구조는 버림 | M3(예정) |
| 진동 우선순위 큐 | `AS/_Project/RobotPlay/Runtime/IntegratedFeedback.cs` 7-14 `PunchHapticQueue` | 컨트롤러 진동 + ESP32 앞단 | M1-05 |
| 검·방패 | `VR_test/Assets/Scripts/MechaVRSwordEquip.cs`, `MechaVRShieldDeploy.cs`, `Weapons/CapstoneShield/ShieldDeployment.shader` | 검방패 페이즈 진행 시. 입력은 InputActionReference, 충돌체 추가 | 11/19 결정 후 |
| 보스 쪽 가드 수신 | `AS/_BossPrototype/Runtime/Combat/BossCombatTarget.cs` | 공유 로봇 상태로. 좌/우 가드 = 좌/우 조종사 | M2-03 |
| 내려찍기 | `AS/_BossPrototype/Runtime/Combat/Patterns/BossOverheadSmash.cs` | 맨손 클립으로. `IsSword` 분류 수정 | M2-03 |
| 파일럿 몸 IK | `AS/_Project/02_Art/PilotV04/Runtime/PilotReviewRig.cs` | `[ExecuteAlways]` 제거, 검수 기능 분리 | 파일럿 몸을 넣을 때 |
| 테스트 사양 | `AS/_Project/RobotPlay/Editor/RobotCombatChecks.cs`, `IntegratedRulesChecks.cs`, `AS/_Project/01_Scripts/04_VR/Editor/ArmMotionValidation.cs` Core() 40-92, `VR_test/Assets/Editor/MechaVRBossContactTests.cs` | NUnit EditMode/PlayMode 테스트로 옮김 | M1-02·03·04 |

## 반복하면 안 되는 버그

1. 자기 팔을 서버 echo로 그림 (`RobotPlaySession.cs:313-314`).
2. 팔 자세를 50Hz Reliable RPC로 전송 (`RobotPlaySession.cs:266`).
3. 빠른 펀치 샘플을 서버가 버림 (`RobotPlaySession.cs:282-283`).
4. LAN에서 손 회전·개인 보정 꺼짐 (`RobotPilotInput.cs:87`, `RobotPlaySession.cs:391, 409`).
5. 보스 일반 공격이 항상 오른팔 가드 요구, 회피는 |x| ≥ 0.5만 봄 (`IntegratedBossEncounter.cs:54, 59`).
6. 가드 = "보정 가드보다 15cm 위"만 봄 (`RobotCombat.cs:111`).
7. 이동 스틱이 펀치하는 손 (`RobotPilotInput.cs:201`).
8. 0.25초 입력 끊김에 연계 초기화 (`RobotPlaySession.cs:296-297`).
9. IK 상위 팔 회전 누적 → roll 드리프트 (`ControllerArmFollower.cs:123-124`).
10. 보스 Stun·Dead가 Idle만 재생, 코어 노출 0초로 미발동.
11. 콕핏 바깥 화면 끊김: 밉맵 없는 2048² 렌더텍스처 3장 + 외부 카메라 3대 추가 렌더.
12. 검증·smoke 코드가 빌드에 포함, 런타임 `UnityEditor` 참조, `[InitializeOnLoad]` 빌더가 씬을 자동으로 덮어씀.

## 모델

- 보스·플레이어 공용: v026 Mixamo 메카. 후보 `AS/_BossPrototype/Models/BossUpdatedCompatible.fbx`(애니메이션 호환 변환본) 또는 `AS/_Project/02_Art/01_Models/mecha-finger-rig-mixamo-v026dsadsfadf.fbx`(원본). ChatGPT가 정리한 사본은 `C:\Users\anthony\Desktop\Capstone\anseong-steel\클로드용 모델전수\`.
- 보스 애니메이션 소스: `AS/_BossPrototype/Animation/{Idle,Walk,Punch,TurnLeft,TurnRight}.fbx`, `Animation/CombatSource/*.fbx` 15개(Mixamo Without Skin). 베이크된 `Combat/*.anim` 152MB는 가져오지 않는다.
  - v2에 있음: `Idle`·`Walk`·`Punch`(M0-03). `Stagger`는 M1-05 5단계에서 가져온다. `TurnLeft`·`TurnRight`와 나머지 `CombatSource`(AlternatingCombo·Death·GroundCast·OverheadSmash 등)는 M2 지시서에서 필요한 것만 배정한다.
- 조종석: `AS/_Project/02_Art/CockpitV02/` 또는 `AS/CockpitVariants/{A_CloseArc,B_RearDome,C_SideDome}` 중 하나. 중계 화면 재질은 버림.
- 파일럿: `AS/_Project/02_Art/PilotV04/AS_Pilot_v04_QuestCandidate.fbx`(+.meta, Humanoid).
- 검·방패: `VR_test/Assets/Weapons/CapstoneSword`, `CapstoneShield`.
- 배경: `ArtSource/Background/MechaAirport_Natural_Environment.unitypackage`.
- 실제 보스 디자인 BossV11은 리그가 없고 116만 폴리곤이라 이번 일정에서는 외형 참고만.
