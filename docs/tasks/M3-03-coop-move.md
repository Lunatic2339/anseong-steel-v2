# M3-03 협동 이동 · 몸 자동 회전 · 걷기 클립

- 마일스톤: M3 / 담당: 선우(코드는 Claude), 2인 검증 성재 / 브랜치: `feat/m3-03-coop-move`
- 선행: M2-03(회피 스틱 보고·DodgeRules), M2-04(보스 이동). develop에서 땀(11/04). M3-01·02와 겹치는 파일은 `NetworkSessionMenu.cs` 한 곳(다른 줄).
- 기한(제안): 11/10

## 목표
두 조종사가 보조 스틱을 같은 방향으로 밀 때만 로봇이 묵직하게 한 걸음씩 접근·후퇴하고, 좌/우로 회피한다. 보스가 옆으로 돌아가면 로봇 몸이 천천히 돈다. 두 PC 화면에서 로봇 위치가 같다.

## 배경
- `01_combat_rules.md` "이동": 크기 ≥ 0.5, 방향 차이 25° 이내일 때만, 한 명이 놓으면 정지. 앞 = 근접 거리까지 접근, 뒤 = 사거리 밖 후퇴, 좌/우 = 회피(약 0.4초). 자유 이동 없음. 몸은 보스가 정면 ±20° 밖일 때만 초당 30° 이하.
- `06_feel_and_sound.md` "이동": 둘이 같은 방향 → 삑삑 `SFX_MoveConfirm`. 카메라는 늦추지 않는다(멀미).
- M2-03: 스틱은 이미 `DefenseReporter` → 서버(`SubmitStick`), 회피 시작은 `DodgeRules`. 회피 미끄러짐은 `PlayerHitFeedback`의 임시 코드(각 PC가 따로 옮김).
- `02_architecture.md` "플레이어 로봇 몸": 접근·후퇴·회피는 루트를 코드로 0.4초 안팎 이동 + 다리에만 걷기·옆걸음 클립.

## Claude가 할 일
1. 순수 `Combat/CoopMoveRules`: 앞/뒤 판정, 다음 한 걸음 정하기(접근 26 m까지, 후퇴 60 m까지, 한 걸음 8 m·0.4초, 걸음 사이 0.1초, 회피 10 m 뒤 0.8초 쉼), 몸 회전(20° 넘으면 시작, 2° 안에서 멈춤, 30°/초).
2. `Net/RobotMotionSync`(Combat Server에): 서버가 스틱으로 한 걸음을 정해 `NetworkVariable`로 복제, 모든 PC가 같은 서버 시각으로 같은 곡선 재생. 몸 각도도 서버가 계산·복제(클라이언트는 같은 속도로 따라 돎). 판 재시작하면 처음 자리. `PlayerHitFeedback`의 임시 미끄러짐은 끔.
3. 걷기: 플레이어 전용 Animator(대기 + 다리만 걷는 층). 골반·몸통·머리는 대기 그대로라 조종석이 걷기로 흔들리지 않는다. 옆걸음은 M4.
4. 전투 이벤트 `MoveStart` + 소리 표 `SFX_MoveConfirm`.
5. 조절 메뉴 "협동 이동": 한 걸음 거리·시간, 몸 자동 회전, 걷기 클립, 확인 칸 3개.
6. EditMode 테스트 `CoopMoveRulesTests`.

## 사람이 할 일
헤드셋을 쓴 사람은 키보드·마우스를 건드리지 않는다. 조절은 B 메뉴.
- [ ] 1인 양팔: 두 스틱을 같이 앞으로 → 한 걸음씩 다가가다 근접 거리에서 멈춤. 뒤로 → 물러나다 멈춤. 한 스틱만 → 안 움직임.
- [ ] 두 스틱 같이 좌/우 → 회피(옆으로 미끄러짐). 앞뒤 걸음 도중에 밀어도 바로 옆으로 간다. 보스 돌진을 피할 수 있다(M2-03 확인 그대로). 안 먹으면 Console `[Move]` 줄(회피 입력 버림·두 스틱 방향이 달라 무시)을 본다.
- [ ] 움직일 때 멀미가 없다. 다리만 걷고 시점이 위아래로 흔들리지 않는다.
- [ ] 보스 고정 시점: 앞뒤·회피 중에도 보스가 늘 정면. 회피는 보스 둘레로 옆구르기(거리 그대로, 돌아오지 않음). B 메뉴 "협동 이동 → 보스 고정 시점"으로 끄고 켤 수 있다.
- [ ] 판 재시작(두 스틱 1초 누르기 또는 B 메뉴 "판 → 판 재시작") → 처음 자리·방향.
- [ ] 빌드 2대(성재와): 둘이 같이 밀 때만 움직이고, 두 PC에서 로봇 위치·방향이 같다.

## 하지 말 것
- 옆걸음 클립 교체(M4), 비네팅(M5), 자유 이동.

## 완료 기준
- 1인 양팔과 빌드 2대에서 협동 이동·회피·몸 회전이 위처럼 된다(사람 확인). 테스트 통과.

## 결과 (작업 후 Claude가 작성)
- 바뀐 파일:
  - 새로: `Runtime/Combat/CoopMoveRules.cs`, `Runtime/Net/RobotMotionSync.cs`, `Editor/PlayerMotionSetup.cs`, `Tests/EditMode/CoopMoveRulesTests.cs`
  - 고침: `Combat/CombatTypes.cs`(`MoveStart`), `Combat/CombatCueSet.cs`(기본 소리 한 줄), `Combat/PlayerHitFeedback.cs`(`SlideEnabled`), `Editor/RobotPrefabMenu.cs`(플레이어 Animator 한 줄), `Editor/NetworkSessionMenu.cs`(RobotMotionSync), `docs/폴더_지도.md`
- 사람이 할 일(Unity):
  1. 코드 받고 `error CS` 확인.
  2. `Sandbox_Pilot`에서 **Rebuild Robot + Pilot Input**(프리팹에 새 Animator, 세션에 이동) → Ctrl+S. `Main`은 Build M1 Main → Ctrl+S.
  3. 새로 생긴 `Art/Robot/Animation/Robot_Player.controller`, `Robot_Player_Legs.mask`, 새 `.cs`의 `.meta`, 프리팹·씬을 커밋.
  (소리 표 `Audio/CombatCues.asset`의 `MoveStart` 줄은 Rebuild가 빠진 기본 줄로 채운다. `SFX_MoveConfirm` 파일이 없으면 무음.)
- 사람이 확인할 것: 위 "사람이 할 일" 6개.
- 남은 문제 / 제안:
  - 프로토타입(선우 11/04): 판정·느낌은 나중에 다듬는다.
  - 스틱 판정을 "두 스틱 사이 25°"에서 "스틱 하나하나가 그 축에서 45° 안 + 같은 쪽"으로 바꿈. 엄지로 옆을 밀면 두 스틱이 30~50° 벌어져 오른쪽 회피가 거의 안 먹었다(11/04 기록). 01 문서의 25°는 이 기준으로 고쳐야 함.
  - Add Network Session이 제철소 맵(Sandbox_ReferenceLayout)도 붙인다(선우 11/04: 앞으로 맵 위에서 테스트). 따로 붙이려면 Add Steelworks Map.
  - BossBrain NullReference: M2-06 고침(71adc79)을 가져오고, 세션을 다시 시작해 판정 객체가 바뀌면 다시 묶게 함.
  - 회피는 옆구르기처럼 옆에 남는다(선우 11/04). M2-06의 임시 "회피 뒤 제자리로"는 `PlayerHitFeedback` 미끄러짐 안에 있어서, 이 작업이 미끄러짐을 끄면(`SlideEnabled = false`) 같이 꺼진다. M2-06과 합칠 때 `PlayerHitFeedback`이 겹치면 `SlideEnabled` 속성을 남기고 OnCue의 `if (!slide …) return;`을 지킨다.
  - 근접 거리 26 m·후퇴 60 m·한 걸음 8 m는 처음 값. 보스는 30 m까지 걸어오므로(M2-04) 접근을 안 해도 싸움이 된다. 한 판 길이를 보고 조절.
  - 다리 마스크는 뼈 이름(`LeftUpLeg`·`LeftLeg`·`LeftFoot`·`LeftToeBase` 등)으로 고른다. 모델이 바뀌면 메뉴를 다시 돌리면 된다.
  - 이동 중에도 보스 공격·펀치 판정은 그대로다(막을지는 헤드셋 확인 뒤).
