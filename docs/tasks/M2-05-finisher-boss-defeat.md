# M2-05 합동 필살기 · 보스 쓰러짐 · M2 통합 확인

- 마일스톤: M2 / 담당: 선우 / 브랜치: `feat/m2-05-finisher-boss-defeat`
- 선행: M2-04
- 기한(제안): 11/05 (M2 완료일)

## 목표
무릎 꿇은 보스에게 두 팔을 정면으로 뻗고 컨트롤러 버튼을 모두 누르면 1.5초 충전 → 광선 3초 → 보스가 쓰러진다. 1인 양팔 모드로 **혼자 보스 HP 0까지** 한 판을 끝내는 것이 M2 완료 기준이다.

## 배경
- `01_combat_rules.md` "필살기": 팔 펴짐 ≥ 85%, 그립·트리거·A/B 또는 X/Y(메뉴·시스템 버튼 제외) 누른 채 유지, 양쪽이 0.5초 이내 시작, 1.5초 충전, 한 명이 놓으면 리셋, 광선 3초.
- `05_boss_patterns.md` "끝: 무릎 → 필살기 → 쓰러짐"(시간 제한 없음, 20초 실패 시 다시 안내, 성공 → `Death` → 결과).
- `00_plan.md` 한 판 흐름 5단계. 1인 양팔 모드는 한 사람이 두 컨트롤러를 모두 맡는다.

## Claude가 할 일
1. 순수 클래스 `FinisherRules`: 팔별 준비(펴짐 비율, 버튼 전부, 추적 유효), 두 팔 시작 시간 차 ≤ 0.5초, 충전 1.5초, 놓으면 리셋, 성공 → 광선 3초 → 완료. 수치 Inspector. 팔 펴짐 비율은 보정한 도달 거리 기준(M1-02).
2. 각 팔의 준비 상태는 owner가 보고, 서버가 판정해 이벤트 `FinisherCharge`(0~1), `FinisherFire`, `FinisherReset`, `BossDefeated`를 보낸다.
3. 임시 연출: 충전 중 두 팔 발광이 점점 세짐 + 가슴에서 보스로 직선 광선(LineRenderer, 재질은 설정 에셋). 진짜 VFX는 M5(유진 VFX 소스).
4. 성공 → 보스 `Death` 클립 → 3초 뒤 `CombatCue.EncounterWon`. 결과 화면은 M4이므로 지금은 안내판 "승리 + 걸린 시간"과 `R` 재시작.
5. 20초 동안 성공 못 하면 안내(사령관 대사 이벤트 + 안내판 "두 팔을 뻗고 버튼을 모두 누르세요").
6. 외골격 자리: 필살기 발사 순간 `X`(해제) 이벤트.
7. M2 통합 확인용 체크리스트를 결과 칸에 적는다(아래 "사람이 할 일"을 그대로 쓸 수 있게).
8. EditMode 테스트: 버튼 하나 빠짐, 펴짐 부족, 시작 차 0.5초 경계, 중간에 놓으면 리셋, 충전 완료 시각.

## 사람이 할 일 (헤드셋, M2 완료 확인)
- [ ] 무릎 꿇은 보스에게 두 팔을 뻗고 버튼을 모두 누르면 충전 → 광선 → 쓰러짐.
- [ ] 버튼 하나를 놓으면 충전이 처음부터 다시.
- [ ] **처음부터 끝까지 혼자 3판**: 보정 → 1페이즈 → 2페이즈 → 3페이즈 → 무릎 → 필살기 → 쓰러짐 → `R` 재시작. 끊김·오류 없이, 이전 판 상태가 남지 않는다.
- [ ] 판마다 걸린 시간(M2-04 CSV)을 적는다. 목표는 보스전 4~7분.

## 하지 말 것
- 결과 화면·출격 연출·HUD(M4), 진짜 VFX·히트스톱(M5), 2인 판정 검증(M3에서 PC-B로).

## 완료 기준
- 1인 양팔 모드로 혼자 보스 HP 0까지 3판 연속(헤드셋 확인). M2 완료.

## 결과 (작업 후 Claude가 작성)
- **이어서 확인**: M2-04 위에 쌓았다(01 → 02 → 03 → 04 → 이 브랜치). M2 다섯 브랜치는 따로 확인할 수 없다. 01부터 차례로 병합·확인한다.
- 바뀐 파일:
  - 새로: `Runtime/Combat/FinisherRules.cs`(순수 규칙·수치), `FinisherReporter.cs`(팔 주인 PC가 준비를 보고), `FinisherVisual.cs`(충전 발광·광선 임시 연출), `Tests/EditMode/FinisherTests.cs`
  - 고침: `CombatTypes.cs`(사건 `FinisherCharge`·`FinisherReset`·`FinisherFire`·`EncounterWon` 끝에 추가), `CombatSnapshot.cs`(`ICombatLink.SubmitFinisherReady`), `CombatAuthority.cs`·`CombatDirector.cs`(필살기 규칙·수치 칸 "필살기"), `CombatServer.cs`(`FinisherReadyRpc`), `BossBrain.cs`(무릎 → 필살기 켬, 20초마다 다시 안내, 광선 끝 → 바닥 풀고 HP 0 → 쓰러짐 → 3초 뒤 승리), `CombatCueSet.cs`(충전·발사·쓰러짐 소리 줄), `CombatCuePlayer.cs`(충전·발사 진동 두 손), `TestGuidePanel.cs`(무릎 안내, 충전·풀림·발사, 승리 + 걸린 시간), `PilotHandInput.cs`·`PilotCalibrationFlow.cs`·`CockpitSeat.cs`·`TuningMenu.cs`(그립·트리거를 쥔 손의 X·Y·B는 개발 버튼으로 안 씀: 필살기 버튼과 겹침), `CombatSetup.cs`(Arms에 `FinisherReporter`, 로봇 루트에 `FinisherVisual` + `FinisherBeam` 광선, `Art/Debug/FinisherBeam.mat`)
- 정한 것:
  - 준비 = 사람 손이 보정한 도달 거리의 85% 이상 + 정면에서 35° 안 + 그립·트리거(60% 이상)·A·B(왼손 X·Y) 모두 + 추적 유효. 판정은 팔 주인 PC, 바뀔 때만 서버로.
  - 충전 시작 시각은 늦게 뻗은 팔 기준. 0.5초 창 밖이면 한 팔을 내렸다 다시 뻗어야 한다. 발사 뒤에는 놓아도 된다.
  - 쓰러짐 연출은 M2-02의 Death 클립(`RobotArtSettings.bossDeathClip`). 승리 알림까지 3초(`BossBrain` "필살기·쓰러짐").
  - 외골격 X(즉시 해제) 자리 = `FinisherFire` 사건(M3 외골격 연결에서 듣는다).
  - 시험 키(에디터·개발 빌드): **F**를 누르고 있으면 두 팔 준비.
- 사람이 할 일(Unity): Rebuild Robot + Pilot Input, Build M1 Main → 저장 → 새 `.meta`, `Art/Debug/FinisherBeam.mat`, 프리팹, `CombatCues.asset` 커밋.
- 사람이 확인할 것(M2 통합, 헤드셋):
  - [ ] Play → 보정 → 보스가 걸어와 공격(1페이즈 펀치) → 가드·빈틈 반격.
  - [ ] 60% → 포효 → 2페이즈(첫 공격 내려찍기, 양팔 가드). 25% → 3페이즈(첫 공격 돌진, 두 스틱 같은 방향 회피).
  - [ ] 10% → 무릎 → "둘이 함께 팔을 뻗어라" 안내 → 두 팔 정면으로 뻗고 버튼 전부 → 1.5초 동안 로봇이 밝아짐 → 광선 3초 → 보스 쓰러짐 → 3초 뒤 "승리 + 걸린 시간".
  - [ ] 충전 중 버튼 하나를 놓으면 "충전 풀림"으로 처음부터.
  - [ ] 20초 가만히 있으면 사령관 안내 다시.
  - [ ] 버튼을 쥔 채 X·Y·B를 눌러도 자리 바꾸기·재보정·조절 메뉴가 안 열린다.
  - [ ] R 재시작 → 보스 HP·페이즈·조종사 HP·발광이 모두 처음으로. 혼자 3판 연속.
  - [ ] 판마다 `Logs/Anseong/logs/bossfight_*.csv` 시간 확인(목표 보스전 4~7분).
- 남은 문제 / 제안:
  - 광선 굵기·끝 높이는 보스 크기 기준 제안값(6 m, 40 m). 이상하면 로봇 루트의 `FinisherVisual`에서 바꾼다. 진짜 VFX는 M5.
  - 발광은 플레이어 로봇의 SkinnedMeshRenderer 전부에 덮는다(팔만 따로 나누지 않음).
  - 2인일 때 각 PC는 자기 팔만 보고한다. 실제 두 PC 확인은 M3.
