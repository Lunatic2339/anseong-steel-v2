# M2-03 보스 공격 막기 · 피하기 · 조종사 로봇 HP

- 마일스톤: M2 / 담당: 선우 / 브랜치: `feat/m2-03-guard-dodge-pilot-hp`
- 선행: M2-02, M1-06(가드 판정)
- 기한(제안): 11/02

## 목표
보스 공격의 "명중 순간"에 서버가 가드·회피를 판정해, 막으면 피해 0 + 긴 빈틈, 못 막으면 조종사 로봇이 피해를 입고 연계가 끊긴다. 조종사 로봇 HP가 생기고, 위기 피해 감소가 들어간다.

## 배경
- `01_combat_rules.md` "가드와 회피"(대응·예고·성공·실패 표, 회피는 명중 0.6초 전부터의 입력만, 반격 기회 1.5초 / 성공 3.0초, 위기 피해 감소), "이동"(두 스틱 같은 방향: 크기 ≥ 0.5, 방향 차이 25° 이내), "외골격 피드백", 진동 우선순위(피격 > 부스트 > 연계 성공 > 명중 > 가드, 최소 간격 0.09초).
- `05_boss_patterns.md` "조종사 로봇"(HP 100, 위기 피해 감소: 50% 이상 1.0 → 20% 이하 0.5, 토글).
- `03_reuse_from_old_repo.md`: `BossCombatTarget.cs`(보스 쪽 가드 수신) → 공유 로봇 상태로, 좌/우 가드 = 좌/우 조종사. `BossAlternatingCombo.cs` 순간 점 판정 → sweep. `BossOverheadSmash.cs` → 맨손 클립, `IsSword` 분류 수정. 피할 버그 5·6.
- M1-06 `GuardRules`(사람 손 기준 가드 판정). 넓게 잡는 가드도 인정해야 한다(선우 10/17).

## Claude가 할 일
1. 가드 상태 보고: 각 팔의 가드 여부를 owner가 매 틱 쓴다(지금은 호스트가 두 팔 모두, M3에서 PC-B가 오른팔). 서버는 보고된 값만 읽는다.
2. 순수 클래스 `DefenseRules`: 명중 순간에 기술 대응과 가드·회피 상태를 비교해 결과(막음 / 한 팔만 막음(강공격 실패) / 피함 / 맞음). 좌우 연타는 타마다 들어오는 쪽 팔. 일반 펀치의 좌/우를 실제로 구분.
3. 순수 클래스 `DodgeRules`: 두 보조 스틱이 같은 좌/우 방향(크기·각도 기준)이고, 명중 0.6초 전 이후 입력일 때만 회피. 미리 옆에 서 있던 위치는 인정하지 않는다. 회피 성공 시 로봇 루트를 옆으로 약 0.4초 미끄러뜨리는 임시 이동(협동 이동 전체는 M3).
4. 순수 클래스 `PilotHealth`: HP 100, 위기 피해 감소(토글 + 시작 HP·끝 HP·최소 배율 Inspector), HP 0이면 `CombatCue.PilotDefeated`. 서버가 계산하고 `NetworkVariable`로 복제.
5. 결과 연결: 맞으면 연계 종료(`ComboBreak.Damaged`), 반격 기회 1.5초 / 막거나 피하면 3.0초를 상태기계 Opening 길이에 넘긴다. 내려찍기 양팔 가드 성공 시 보스 경직 0.8초.
6. 이벤트 추가: `GuardBlock`, `GuardFail`, `Dodge`, `PilotHit`(피해량). 듣는 쪽: 소리(`SFX_Guard`·`SFX_RobotHit`), 진동 우선순위 큐(0.09초 간격 포함, M1-05에서 미룬 것), 조종석 짧은 흔들림(카메라는 거의 안 흔듦), 안내판 "막음/맞음/피함 + 이유".
7. 외골격 자리: 예고 시작 `P`, 가드 접촉 `R` 300ms를 보낼 자리에 이벤트만 걸어 둔다(시리얼 연결은 별도 작업).
8. 조종사 HP 표시: 조종석 콘솔에 막대 하나(HUD 아트는 M4).
9. EditMode 테스트: 대응별 결과, 한 팔만 막은 강공격, 회피 시간 창·방향 불일치, 위기 피해 감소 경계(50%·20%·최소 배율), 연타 4타.

## 사람이 할 일 (헤드셋)
- [ ] 왼손·오른손 펀치를 각각 그쪽 팔로 막으면 피해 0, 반대 팔로 막으면 맞는다.
- [ ] 내려찍기는 양팔이어야 막힌다. 한 팔이면 맞는다.
- [ ] 돌진은 두 스틱을 같은 방향으로 밀어야 피한다. 미리 밀고 있었으면 인정 안 됨.
- [ ] 맞으면 조종사 HP가 줄고 연계가 끊기고 진동·소리가 난다. HP가 낮을수록 덜 준다.
- [ ] 화면에서는 막은 것 같은데 맞았다고 나온 경우를 적는다.

## 하지 말 것
- 패턴 고르기·페이즈(M2-04), 패배 재시작 연출(M4), 협동 이동(M3), 외골격 시리얼.

## 완료 기준
- 5개 기술 모두 막기·피하기·맞기가 판정되고 조종사 HP에 반영된다(헤드셋 확인). 테스트 통과.

## 결과 (작업 후 Claude가 작성)
- **이어서 확인**: M2-02 위에 쌓았다(`feat/m2-01-host-server-path` → `feat/m2-02-boss-attack-state-machine` → 이 브랜치). 01·02를 먼저 확인한다. 이 작업만 따로 볼 수 없다(보스 공격이 있어야 막기를 시험할 수 있음).
- 바뀐 파일:
  - 새로: `Runtime/Combat/PilotHealth.cs`, `DodgeRules.cs`, `DefenseReporter.cs`, `PlayerHitFeedback.cs`, `Runtime/Boss/DefenseRules.cs`, `Runtime/UI/PilotHealthBar.cs`, `Tests/EditMode/DefenseTests.cs`
  - 고침: `CombatAuthority.cs`(가드·스틱 보고, `ResolveBossStrike`, 조종사 HP), `CombatSnapshot.cs`(조종사 HP 복제, 보고 통로에 가드·스틱), `CombatServer.cs`(가드·스틱 RPC, 스틱은 sbyte로), `BossServer.cs`(명중 순간 → 판정 → 빈틈 길이), `CombatTypes.cs`(사건 `GuardBlock`·`GuardFail`·`DodgeStart`·`Dodge`·`PilotHit`·`PilotDefeated` 끝에 추가), `CombatCuePlayer.cs`(진동 우선순위: 피격 > 부스트 > 연계 > 명중 > 가드, 같은 손 최소 간격 0.09초, 피격은 두 손), `CombatCueSet.cs`(새 기본 줄: `SFX_Guard`·`SFX_GuardStrain`·`SFX_DodgeSlide`·`SFX_DodgeBurst`·`SFX_RobotHit`·`SFX_AlarmLowHP`), `CombatDirector.cs`(조종사 HP·회피 설정, 조절 메뉴 "위기 피해 감소" 토글), `TestGuidePanel.cs`(막음/맞음/피함 + 이유, 기체 HP), `CombatSetup.cs`(보고·미끄러짐·HP 막대 붙이기)
- 정한 것:
  - 회피는 "두 스틱이 같은 좌/우로 **새로** 맞은 순간"을 기록하고, 그 순간이 명중 0.6초 전~명중 사이면 인정. 계속 밀고 있던 것은 새 시작이 아니라 인정 안 됨.
  - 회피 스틱: 1인 양팔은 각 손 스틱(왼손 = 왼쪽 조종사), 2인은 그 조종사의 이동 손.
  - 회피 미끄러짐은 회피 동작 시작(`DodgeStart`) 때 옆으로 10m·0.4초(임시, 성공 여부와 무관). 각 PC가 같은 이벤트로 움직인다(로봇 위치 복제는 M3).
  - 조종사 HP 0이면 보스가 새 공격을 시작하지 않는다. 패배 연출·재기동은 M4. `R`로 재시작.
  - 외골격 자리: 예고 시작 `P` = `BossTelegraph`, 가드 접촉 `R` = `GuardBlock` 이벤트를 듣는 컴포넌트를 붙이면 된다(M3-04).
- 사람이 할 일(Unity): Rebuild Robot + Pilot Input, Build M1 Main → 저장 → 새 `.meta`·`PilotHealthBar_Fill.mat`·프리팹·`CombatCues.asset` 커밋.
- 사람이 확인할 것(헤드셋): 지시서 위 5개. 숫자 1~5로 기술을 부른다(02). 돌진(5)은 두 스틱을 같은 쪽으로 명중 직전에 민다.
- 남은 문제 / 제안:
  - 진동 우선순위는 손마다. 피격이 두 손으로 울리는 동안 가드 진동은 끼어들지 않는다.
  - 조종석 흔들림은 1.5cm(멀미 때문에 작게). 헤드셋에서 크기 확인 필요(`PlayerHitFeedback` Shake Meters).
  - 가드 보고는 바뀔 때만 보내므로 M3 지연이 크면 명중 순간 판정이 화면과 다를 수 있다. 그 경우 "화면에서는 막은 것 같은데 맞음"을 적어 주면 명중 순간을 지연만큼 늦게 판정하는 방법을 넣는다.
