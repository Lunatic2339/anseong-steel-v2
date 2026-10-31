# M2-02 보스 공격 데이터 · 상태기계 · 애니메이션

- 마일스톤: M2 / 담당: 선우 / 브랜치: `feat/m2-02-boss-attack-state-machine`
- 선행: M2-01. D-01 보스 패턴 표(유진, 10/27)가 있으면 그 값으로, 없으면 05 제안값으로
- 기한(제안): 11/01

## 목표
보스가 "쉬는 중 → 예고 → 명중 순간 → 회복 → 빈틈"을 돌며 기술 5개를 쓸 수 있게 한다. 서버만 상태를 계산하고 "기술 + 시작 틱"을 복제해, 각 PC가 같은 순간부터 클립을 재생한다. **빈틈·경직 중에만 피해**가 들어간다.

## 배경
- `docs/design/05_boss_patterns.md` "보스 한 번의 공격 흐름", "기술표", "보스가 맞았을 때", "구현 메모".
- `01_combat_rules.md` "보스" 표, 피할 버그 5(일반 공격이 항상 오른팔 가드 요구).
- `03_reuse_from_old_repo.md`에서 이 지시서가 가져갈 것(옛 레포, 로직만 다시 씀):
  - `BossCombatController.cs` Windup→Active→Recovery, Stun, Dead → 서버 전용, 서버 시각, 디버그 telegraph·미사일 경로 제거
  - `BossAttackDefinition.cs` → 기술 4종(+좌우 펀치)으로 축소, `IsSword` 분류 수정
  - `BossAnimationDriver.cs` → 서버 상태 + 시작 틱, 클라이언트 CrossFade, Stagger·Death 재생
  - `BossAnimationEvents.cs`, `Patterns/BossAttackPattern.cs`·`BossHeavyPunchPattern.cs`·`BossGrab.cs`(패턴 베이스), `BossHeavyPunch.cs`(애니 이벤트 시점 1회 판정)
- 아트 교체 구조(M1-03): 클립은 Robot Art Settings 칸 + 비면 기본값. 경로를 코드에 적지 않는다.

## 클립이 아직 없을 때 (중요)
우선 2 클립(두 손 내려찍기·돌진·무릎)은 11/12에 온다. M2는 그 전에 끝나야 하므로 **기술마다 임시 클립 칸**을 두고 나중에 칸만 바꾼다.

| 기술 | 진짜 클립 | 임시 |
|---|---|---|
| 왼손 펀치 | `Boss_PunchLeft`(A-01 우선 1) | 없으면 `AlternatingCombo` 왼손 구간. 모델이 Generic이면 Mirror가 안 되니 먼저 확인 |
| 오른손 펀치 | `Punch` | – |
| 좌우 연타 | `AlternatingCombo` | – |
| 두 손 내려찍기 | `Boss_SlamBoth` | `GroundCast` |
| 돌진 | `Boss_Charge` | `Walk` 2배속 |
| 무릎 | `Boss_Kneel` | `Idle` 정지 |
| 크게 밀려남 | `Boss_Knockback` | `Stagger` 전체 |

옛 레포 `AS/_BossPrototype/Animation/CombatSource/`의 `AlternatingCombo`·`Death`·`GroundCast`를 `Art/Robot/Animation/Source/`로 가져온다(M1-05가 `Stagger`를 가져온 방식).

## Claude가 할 일
1. `BossAttackDefinition`(ScriptableObject, 기술 하나 = 에셋 하나): 클립 칸, 예고·명중 순간·끝(초), 대응(LeftGuard / RightGuard / BothGuard / AlternateGuard / Dodge), 실패 피해, 성공 시 빈틈, 연타 타수·간격. 기본값 = 05 기술표. 에셋 5개는 메뉴가 만든다.
2. 순수 클래스 `BossAttackStateMachine`: 상태 Rest / Telegraph / Strike / Recovery / Opening / Staggered / Defeated, 서버 시각으로 진행. "명중 순간" 이벤트(연타는 타마다) 발생. 연계가 진행 중이면 다음 공격 시작을 최대 2.4초 미룸(05).
3. 피해 받기: `CombatResolver`가 보스 상태를 물어 Opening·Staggered일 때만 피해. 아니면 새 이벤트 `CombatCue.Blocked`(피해 0, 막힘 소리, 연계 차례는 그대로).
4. `BossAnimationDriver`: 서버가 `(기술, 시작 서버 틱)`을 복제 → 각 PC가 경과 시간만큼 앞으로 당겨 CrossFade. 맞았을 때 반응(잽 = 없음, 훅 = Stagger 앞 0.6초, 부스트 = 밀려남)도 같은 방식.
5. 예고 신호 자리: `CombatCue`에 `BossTelegraph`(종류: 일반·강·가드 불가) 추가. 소리·발광은 이벤트만 듣게(지금은 팔 발광 임시 재질 하나).
6. 명중 순간 측정 도구(에디터): 클립에서 보스 주먹 뼈 속도가 가장 큰 프레임을 찾아 "명중 순간" 후보를 Console에 출력. D-01 표가 비어 있을 때 쓰고, 값은 결과 칸에 적는다.
7. 시험용 단축키(`#if UNITY_EDITOR || DEVELOPMENT_BUILD`): 숫자 1~5로 기술 하나를 바로 쓰게.
8. EditMode 테스트: 상태 순서·시간, 연계 중 대기, 빈틈 밖 피해 0, 연타 타 이벤트 개수.

## 사람이 할 일 (헤드셋)
- [ ] 1~5 키로 기술마다 예고 → 공격 → 빈틈이 보이고, 빈틈에만 HP가 준다.
- [ ] 쉬는 중에 치면 막힘 소리(또는 안내판 "막힘")가 나고 연계는 끊기지 않는다.
- [ ] 임시 클립이 뼈가 꼬인 자세로 나오지 않는다(나오면 기술 이름을 적는다).

## 하지 말 것
- 조종사 피해·가드 판정 연결(M2-03), 패턴 고르기·페이즈(M2-04), 보스 이동. 새 기술 추가.

## 완료 기준
- 서버 상태기계로 기술 5개가 클립과 함께 돈다. 빈틈에만 피해. 테스트 통과.

## 결과 (작업 후 Claude가 작성)
- **이어서 확인**: M2-01 위에 쌓았다(브랜치 `feat/m2-01-host-server-path` → 이 브랜치). M2-01을 먼저 확인·병합한 뒤 이것을 본다.
- 바뀐 파일:
  - 새로: `Runtime/Boss/BossAttackTypes.cs`(기술·대응·예고 등급·상태 enum, `BossAttackSpec` 05 제안값), `BossAttackDefinition.cs`(ScriptableObject), `BossAttackStateMachine.cs`(순수), `BossServer.cs`(서버 두뇌, 동작 복제, 1~5 시험 키), `BossAnimationDriver.cs`(각 PC 재생), `BossTelegraphTint.cs`(예고 임시 발광), `Editor/BossAttackMenu.cs`, `Tests/EditMode/BossAttackStateMachineTests.cs`
  - 고침: `CombatTypes.cs`(사건 `Blocked`·`BossTelegraph`·`BossStrike`·`BossOpening`을 **끝에** 추가, `Code`·`Amount` 칸), `CombatResolver.cs`(`BossVulnerable`: 빈틈·경직 아니면 `Blocked`, 연계 그대로), `CombatCueSet.cs`(Code·Amount 거르기, 새 기본 줄: 막힘 `SFX_Guard`, 예고 `SFX_WarnNormal`·`SFX_WarnHeavy`·`SFX_Siren`, 명중 순간 `SFX_BossSwing`), `CombatSetup.cs`(기존 소리 표에 빠진 줄 추가, 보스 컨트롤러에 기술·쓰러짐 상태, 보스에 NetworkObject·두뇌·재생·발광), `RobotPrefabMenu.cs`, `RobotArtSettings.cs`(보스 쓰러짐 클립 칸), `BossReactionDriver.cs`(공격 중엔 휘청 안 함), `TestGuidePanel.cs`(막힘·예고·빈틈 줄), `NetCombatTypes.cs`
- 정한 것:
  - 시간 기준: "예고" = 공격 시작 → 첫 명중 순간. 연타는 그 뒤 0.7초마다. "회복" = 마지막 명중 → 빈틈 시작(기본 1.0초, 강·돌진 1.2초, 클립 보고 조정).
  - 연타 첫 타는 오른쪽(05에 없음, 제안).
  - 대응 판정(M2-03)이 없으면 모든 타를 실패로 쳐서 빈틈 1.5초. 피해는 아직 없다.
  - **쉬는 중에도 맞는 설정(`BossServer` Rest Is Opening) 기본 켬**: 기술을 스스로 고르는 M2-04 전까지는 보스가 늘 쉬는 중이라, 끄면 아무 펀치도 안 들어간다. M2-04에서 끈다. 05대로 막힘을 보려면 Inspector에서 끄고 1~5로 기술을 쓴 뒤 빈틈에 친다.
  - 클립: v2에는 `Punch`·`Walk`·`Idle`·`Stagger`만 있어 임시로 펀치·연타·내려찍기 = `Punch`, 돌진 = `Walk`. 옛 레포 `AlternatingCombo`·`GroundCast`·`Death`는 클라우드에서 가져올 수 없다(옛 레포 접근 범위 밖). 선우 PC 옛 레포 `AS/_BossPrototype/Animation/CombatSource/`에서 `Art/Robot/Animation/Source/`로 복사해 커밋하면 기술 에셋 클립 칸·Robot Art Settings 쓰러짐 칸에 넣는다. Generic 모델이라 Mirror가 안 될 수 있어 왼손 펀치도 지금은 오른손 `Punch`.
- 사람이 할 일(Unity):
  1. Rebuild Robot + Pilot Input(Sandbox_Pilot), Build M1 Main → 저장. `Data/Boss/` 에셋 5개와 새 .cs·폴더의 `.meta`, 바뀐 `Robot_Boss.controller`·프리팹·`CombatCues.asset`을 커밋.
  2. **Anseong → Boss → Measure Strike Moments** 결과(기술별 손 최고 속도 시각)를 총괄 채팅에 올려 주면 예고·회복 값을 맞춘다.
- 사람이 확인할 것(헤드셋): 지시서 위 3개 그대로. 막힘 확인은 위 설정을 끄고.
- 남은 문제 / 제안:
  - 예고 발광은 몸 전체(팔만 따로는 M5). URP Lit 색만 덮는다.
  - 맞았을 때 반응(훅 = 휘청 앞 0.6초, 부스트 = 휘청 전체)은 지금처럼 전투 이벤트로 각 PC가 튼다(이벤트가 모든 PC에 같이 가므로 시작 틱 복제는 생략). 밀려남(뒤로 10m)은 M2-04.
