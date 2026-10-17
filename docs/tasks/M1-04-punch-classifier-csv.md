# M1-04 잽·훅 궤적 분류 + CSV 기록

- 마일스톤: M1 / 담당: 선우 / 브랜치: `feat/sunwoo-punch-classify`
- 선행: M1-02 병합 (M1-03과 동시에 해도 됨. 헤드셋 확인은 M1-03 뒤가 편함)
- 기한(제안): 10/25

## 목표
그립을 쥐고 휘두른 손 궤적을 잽·훅·해당 없음으로 나누고, 동작 하나하나를 CSV로 남겨 수치를 조정할 수 있게 한다.

## 배경
- `docs/design/01_combat_rules.md` "공격"(잽·훅 기준값, 재장전, 추적 유실, 판정 순서).
- `03_reuse_from_old_repo.md`: 잽/훅 분류 `PunchTrajectoryRecognizer.cs` (상태머신 93-136, Evaluate 160-208). 문자열 reason → enum. 테스트 사양 `RobotCombatChecks.cs`, `ArmMotionValidation.cs` Core() 40-92.
- 피할 버그 3(빠른 펀치 샘플을 버림).

## Claude가 할 일
1. 순수 클래스 `PunchClassifier`: 사람 손 궤적(보정한 몸 정면 기준, m 단위)을 받아 `Jab`/`Hook`/`None`과 이유 enum, 측정값(좌우폭/전진, 직진도, 바깥 준비, 안쪽 스윕, 호/현)을 낸다. 기준값은 `01_combat_rules.md` 초기값, 모두 Inspector에서 조정. 판정은 로봇 크기가 아니라 **사람 손 기준**으로 한다(기준값이 cm 단위라서).
2. 재장전: 친 뒤 손이 중립 반경 12cm 안에 0.15초 머물러야 다음 공격. 동작마다 `strikeId`를 붙인다(한 동작 = 한 번).
3. `AttackIntentGate`(M1-02)와 연결: 그립 없음·추적 무효면 분류는 하되 공격 아님으로 표시.
4. 샘플은 프레임마다 다 쓴다(빠른 펀치 샘플을 버리지 않음).
5. `StrikeLog`: 동작마다 한 줄. 열 = 시각, 팔, 종류, 이유, 측정값 5개, 그립, 추적, `strikeId`, 명중 여부(M1-05가 채움, 지금은 비움). 파일은 `Application.persistentDataPath/logs/strikes_<날짜시각>.csv`. 쓰기는 메인 스레드를 막지 않게 모아서. `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 안에서만.
6. 개발용 표시: 분류 결과마다 로봇 주먹 근처 표시 색이 잠깐 바뀜(잽/훅/없음). Console에도 한 줄.
7. EditMode 테스트: 합성 궤적(곧은 잽, 바깥→안 훅, 짧은 훅, 비스듬한 잽, 재장전 안 된 연속 동작, 그립 없음, 추적 유실).

## 사람이 할 일 (헤드셋)
- [ ] 손마다 잽 20번, 훅 20번을 친다. 인식이 틀린 동작은 몇 번째였는지 적는다.
- [ ] 그립 없이 휘두르면 공격으로 세지 않는다.
- [ ] CSV 파일을 총괄 채팅에 올린다(기준값 조정에 씀).

## 하지 말 것
- 주먹 접촉, 보스 피해, 연계, 부스트(M1-05). 가드(M1-06).

## 완료 기준
- 헤드셋에서 잽·훅 각각 손당 20번 중 18번 이상 맞게 인식(제안값. CSV를 보고 기준값을 조정한 뒤 다시 잼).
- CSV가 남고 열이 위와 같다.

## 결과 (작업 후 Claude가 작성)
- 브랜치: `claude/m1-cockpit-input-x0vygh`(이 작업 스레드에 지정된 브랜치, 위 `feat/sunwoo-punch-classify` 대신).
- 정한 것:
  - 좌표는 보정한 몸 기준(어깨 원점, +Z 몸 정면, m), **중립 = 보정한 가드 위치**. 보정이 끝날 때마다 다시 잡는다. 허리 회전·머리 위치와 무관하게 사람 손으로만 판정.
  - 공격 = 잽·훅·어퍼컷 + **휘두르는 내내** 공격 의도(그립 + 추적, `AttackIntentGate`). 중간에 그립을 놓으면 공격 아님. 추적이 끊기면 그 자리에서 동작을 끝내 "추적 0"으로 남기고, 돌아와도 중립부터 다시 센다.
  - 렉으로 샘플이 0.15초 넘게 비거나 손이 15 m/s 넘게 튀면 그 동작만 버린다(결과 없음). 그 밖의 샘플은 프레임마다 전부 쓴다.
  - **어퍼컷 추가(선우 결정, 10/17)**: 가장 낮은 곳에서 18cm 이상, 앞으로 나간 것 이상 위로 올려치고 좌우로는 덜 움직이면 어퍼컷. 01 규칙에 제안값으로 적음(피해 4%, 연계 밖). 잽·훅보다 먼저 본다.
  - 짧은 훅(바깥 준비 10cm 미만)은 훅이 아니다. 거의 곧게 나가면 잽으로 잡힐 수 있다(규칙상 맞음).
  - CSV 열: 지시서 열 + `rise_m`(어퍼컷 판정값) + `head_forward_m`(동작 중 머리가 앞으로 간 거리, 기록용. 선우가 "훅에서 머리가 앞으로 간다"고 해서 크기를 보려고 넣음). 시각은 게임 시작 뒤 초.
- 바뀐 파일:
  - `Runtime/Combat/PunchClassifier.cs`(순수 분류기, 설정, 결과), `PunchClassifierDriver.cs`(팔마다 분류, `StrikeCompleted` 이벤트, 표시 구, Console), `StrikeLog.cs`(개발용 CSV, 백그라운드 쓰기)
  - `Runtime/Pilot/PilotCalibrationFlow.cs`: `TryGetHeadPosition` 추가
  - `Editor/RobotArmSetup.cs`: Build Player Robot Prefab이 `Arms`에 `PunchClassifierDriver`와 손마다 표시 구(`StrikeIndicator_Left/Right`, 충돌체 없음)를 붙이고, 재질 `Art/Debug/StrikeIndicator.mat`(URP Unlit)을 처음 한 번 만든다. Add Pilot Input이 입력·보정을 연결한다.
  - `Tests/EditMode/PunchClassifierTests.cs`(15개: 곧은 잽, 바깥→안 훅, 반대 방향 훅, 짧은 훅, 비스듬한 잽, 낮은 잽, 어퍼컷, 느린 가드 올리기, 재장전 안 된 연속 동작, 그립 없음, 중간에 그립 놓음, 추적 유실, 렉, NaN, CSV 열)
  - `docs/design/01_combat_rules.md`(어퍼컷 제안 행), `docs/폴더_지도.md`
- Claude가 확인한 것: Unity가 없어 **컴파일·Test Runner는 못 돌렸다.** 순수 클래스와 테스트(기존 포함 78개)는 Unity 수학 스텁으로 따로 컴파일해 모두 통과(잽·훅·어퍼컷은 30/60/90/120Hz, 양팔). 드라이버·에디터 메뉴는 Unity에서 처음 컴파일된다.
- 사람이 확인할 것:
  - [ ] `git pull` → Unity 컴파일 에러 없음 → Test Runner EditMode 전부 통과.
  - [ ] **Build Player Robot Prefab** 다시 실행 → 로그 끝에 `펀치 분류 + 주먹 표시`, 양손 주먹 옆에 흰 구. Sandbox_Pilot에서 **Add Pilot Input** 다시 실행.
  - [ ] Play → 보정 → Console에 `[PunchClassifierDriver] 펀치 기록: <경로>`(첫 동작 때) 와 동작마다 `[Punch] …` 한 줄. 헤드셋에서:
  - [ ] 손마다 잽 20, 훅 20, 어퍼컷 10. 틀린 동작은 몇 번째였는지 적기(구 색: 잽 파랑, 훅 주황, 어퍼컷 보라, 해당 없음 회색).
  - [ ] 그립 없이 휘두르면 Console `공격 X`.
  - [ ] 가드를 빠르게 올릴 때 어퍼컷으로 잡히는지(잡히면 `Min Uppercut Rise`나 `Depart Up Speed`를 올림).
  - [ ] 표시 구 크기·위치가 거슬리지 않는지.
  - [ ] CSV 파일(위 경로)을 총괄 채팅에 올리기.
- 남은 문제 / 제안:
  - 팔꿈치 부스터 추가타(선우 요청, 10/17)는 M1-05 범위(연계 마지막 훅 명중 뒤 트리거)라 M1-05에서 한다. `StrikeCompleted`가 그 입력이 된다.
  - 공격 버튼은 지금 그립. 선우가 "특정 버튼을 누른 채"라고 함(10/17). 그립이 아닌 다른 버튼이면 `PilotHandInput`의 판정 입력만 바꾸면 된다.
  - 훅에서 시점이 앞으로 쏠린다면(선우, 10/17) 사람 몸이 앞으로 가는 것과 별개로 조종석이 로봇 머리 뼈를 따라가는 것(Follow Weight)과 허리 회전이 겹친 것일 수 있다. CSV `head_forward_m`로 사람 쪽 크기를 먼저 보고, 시점 쪽이면 Follow Weight를 낮춰 비교.
  - 표시 구 재질 경로는 개발용이라 Robot Art Settings가 아니라 메뉴 코드 상수(`Boss_Tint.mat`과 같은 방식).
