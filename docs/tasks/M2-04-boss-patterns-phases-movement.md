# M2-04 보스 패턴 · 3페이즈 · 거리와 이동

- 마일스톤: M2 / 담당: 선우 / 브랜치: `feat/m2-04-boss-patterns-phases`
- 선행: M2-03
- 기한(제안): 11/04

## 목표
보스가 스스로 기술을 골라 쓰고, HP 60%·25%·10%에서 페이즈가 바뀌며 새 기술을 먼저 보여준다. 근접 거리 밖이면 걸어오고, 부스트에 밀려나면 다시 걸어온다. HP 10%에서 무릎을 꿇는다.

## 배경
- `05_boss_patterns.md` "페이즈별 순서"(첫 공격, 비율, 쉬는 시간, 연속 금지), "페이즈 전환"(경계 아래로 안 내려감, 포효 2초 무적, 조종사 HP 60% 미만이면 60%로 회복), "보스가 맞았을 때"(부스트 → 뒤로 약 10m → 다시 걸어옴), "거리", "끝" 1단계.
- `00_plan.md` 페이즈 표(누적형).
- `03_reuse_from_old_repo.md`: 유진 원본 `BossMovement.cs`(서버 전용, `MoveUp/Down` → `MoveForward/Back`), `BossRotation.cs`(가속·제동, 143행 새 요청마다 `RotationStopped` → 깜빡임 수정).
- M1-05 메모: Sandbox는 보스를 26m(팔 19.4m × 0.95 + 몸통 반지름)에 둠. 근접 거리는 이 작업에서 정한다.

## Claude가 할 일
1. 순수 클래스 `BossPatternPicker`: 페이즈별 첫 공격·비율·쉬는 시간(범위)·연속 금지 규칙(같은 손 3연속, 내려찍기 연속, 강공격 연속, 돌진 3번에 1번, 2페이즈 3번 안에 연타 한 번). 시드를 정할 수 있게(재현용). 표는 ScriptableObject `BossPhaseTable` 하나, 기본값 = 05.
2. 순수 클래스 `BossPhaseRules`: HP 경계에서 멈춤(넘친 피해 버림), 진행 중 공격이 끝나면 전환 시작, 포효 2초 무적(`CombatCue.Blocked`), 조종사 HP 회복값(기본 60, 05 메모대로 50과 비교할 수 있게 Inspector). 이벤트 `PhaseChanged`(번호), `BossKneel`.
3. 거리: 근접 거리 값을 정해 결과 칸에 근거(팔 길이·보정 사거리)와 함께 적는다. 근접 밖이면 공격하지 않고 걷기(`Walk`), 조종사 쪽으로 회전. 이동·회전은 서버만, 위치는 `NetworkTransform`(또는 루트 위치 복제).
4. 부스트 명중 → 밀려남 클립 + 뒤로 약 10m(서버가 루트 이동) → 다시 걸어와 다음 공격.
5. HP 10% → 진행 중 공격 끝 → 무릎 클립(임시 `Idle` 정지) 후 유지, 공격 안 함. 필살기는 M2-05.
6. 사령관 음성 자리: `PhaseChanged`·새 기술 첫 등장에 대사 이벤트(파일이 없으면 무음, 안내판에 글자). 대사 문구는 S-01 제안(11/05 전 팀 회의에서 확정).
7. 기록: 판마다 페이즈별 걸린 시간·받은 피해·가드 성공률을 CSV 한 줄(`#if UNITY_EDITOR || DEVELOPMENT_BUILD`). 한 판 길이(목표 보스전 4~7분) 조정용.
8. EditMode 테스트: 비율(시드 고정 1,000회), 연속 금지, 첫 공격, 경계 멈춤, 회복, 무릎 진입.

## 사람이 할 일 (헤드셋)
- [ ] 1페이즈부터 10%까지 혼자 진행해 본다. 페이즈마다 새 기술이 먼저 나온다.
- [ ] 큰 연계 한 번으로 페이즈를 건너뛰지 않는다.
- [ ] 부스트에 밀려난 보스가 다시 걸어와 싸운다. 걸어오는 동안 이상하게 미끄러지지 않는다.
- [ ] CSV의 걸린 시간을 총괄 채팅에 올린다(짧으면 피해량·쉬는 시간을 조정).

## 하지 말 것
- 필살기·쓰러짐(M2-05), 페이즈 전환 화면 연출·HUD(M4), 구조물 파괴(M5).

## 완료 기준
- 1인 양팔로 1페이즈부터 무릎까지 끊김 없이 간다(헤드셋 확인). 테스트 통과.

## 결과 (작업 후 Claude가 작성)
- **이어서 확인**: M2-03 위에 쌓았다(01 → 02 → 03 → 이 브랜치). 01~03을 먼저 확인한다.
- 바뀐 파일:
  - 새로: `Runtime/Boss/BossPatternPicker.cs`, `BossPhaseRules.cs`, `BossPhaseTable.cs`, `BossFightStats.cs`, `BossBrain.cs`, `Runtime/Combat/CommanderLines.cs`, `Tests/EditMode/BossPhaseTests.cs`
  - 고침: `BossHealth.cs`(HP 바닥 `Floor`: 넘친 피해 버림), `BossAttackTypes.cs`·`BossAttackStateMachine.cs`(걷기·포효·밀려남·무릎 상태와 `Hold`), `BossAnimationDriver.cs`(그 상태 재생), `CombatServer.cs`(서버 쪽 사건 듣기 `ServerCue`, 사건이 사건을 낳으면 이어서 보냄), `CombatAuthority.cs`·`CombatSnapshot.cs`(보스 페이즈 복제), `CombatTypes.cs`(사건 `PhaseChanged`·`BossKneel`·`CommanderLine`·`BossKnockback` 끝에 추가), `CombatCueSet.cs`(포효·밀려남 소리, 사령관 대사 `VO_Commander_07~16` 줄), `TestGuidePanel.cs`(페이즈, 사령관 글자), `RobotArtSettings.cs`(걷기·포효·밀려남·무릎 클립 칸), `BossAttackMenu.cs`·`CombatSetup.cs`(페이즈 표 에셋, NetworkTransform, BossBrain, 컨트롤러 상태)
- 근접 거리: **30 m**(플레이어 루트 → 보스 루트). 근거: 로봇 팔 19.4 m × 다 뻗음 0.97 + 주먹 구 반지름 약 1.2 m + 판정만 허용 4 m의 절반 + 보스 몸통 반지름 7.8 m ≈ 29.8 m(M1-05·`CombatSetup.PullBossToMelee`와 같은 식). 34 m(30 + 여유 4)보다 멀어지면 걸어온다.
- 정한 것:
  - 무릎 중에도 펀치는 맞지만 HP는 10%에서 멈춘다(필살기만 끝냄, M2-05가 바닥을 푼다).
  - 포효 중 펀치는 `Blocked`. 페이즈 전환은 공격·경직이 끝나면 바로(빈틈 중이면 빈틈을 끊는다).
  - 밀려남은 부스트 성공 때마다(05 "처음 성공 때 사일로 부숨"은 파괴 기능 M5). 10 m, 1.5초.
  - 사령관 대사: 첫 공격 7, 첫 빈틈 8, 무릎 9, 첫 내려찍기 11, 첫 연타 12, 첫 돌진 13, 2페이즈 14, 3페이즈 15(문구는 `CommanderLines.cs`, 11/05 전 회의에서 확정).
  - 비율: 연속 금지 규칙 때문에 실제 비율이 표와 조금 다르다(3페이즈 단발 펀치 약 43%, 돌진·내려찍기 약 15%). 테스트에 그 범위를 적었다.
- 기록: `Logs/Anseong/logs/bossfight_<날짜시각>.csv` 판마다 한 줄(결과, 전체·페이즈별 초, 페이즈별 받은 피해, 가드·회피 성공률, 공격 수). 재시작·종료 때도 남는다.
- 사람이 할 일(Unity): Rebuild Robot + Pilot Input, Build M1 Main → 저장 → `Data/Boss/BossPhaseTable.asset`, 새 `.meta`, 프리팹·컨트롤러·`CombatCues.asset` 커밋.
- 사람이 확인할 것(헤드셋): 지시서 위 4개. 조절 메뉴 "보스" 묶음에서 스스로 공격을 끄면 M2-02처럼 숫자 키로 시험할 수 있다.
- 남은 문제 / 제안:
  - 보스 걷기는 루트를 직접 옮기고 Walk 클립을 튼다. 발이 미끄러져 보이면 걷는 속도(조절 메뉴 "보스")를 클립 보폭에 맞춘다.
  - 플레이어 로봇은 아직 움직이지 않으므로(협동 이동 M3) 걸어오기는 밀려난 뒤·회피 미끄러짐 뒤에만 보인다.
