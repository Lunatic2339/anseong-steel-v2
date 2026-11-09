# M4-01 한 판 흐름

- 마일스톤: M4 / 담당: 선우(코드는 Claude) / 브랜치: `feat/m4-01-match-flow`
- 기준: `docs/design/10_game_scenario.md` 대본(11/09 선우 "대충 정해진 대로 개발 시작"), `07_milestones.md` M4 표(대본 기준 7개안). 대본·07 새 안은 `claude/project-thread-o3noxy` 브랜치에 있고 아직 develop 전.
- 선행: M3-03(협동 이동, develop 병합됨). develop에서 땀(11/09).
- **프로토타입**(선우 11/04): 장면이 끝까지 이어지는 뼈대만. 장면 안 내용은 뒤 지시서가 채운다.

## 목표
운영자가 시작만 하면 대기 → 기체 점검 → 등장 → 드론 → 접근 → 근접전 3구간(사이 상실 2번) → 필살기 → 결과 → 재시작이 끝까지 이어진다. 패배하면 재기동 후 그 구간 처음부터 1회, 두 번째 패배면 결과판.

## Claude가 할 일
1. 순수 `Flow/MatchFlowRules`: 장면 13개(대본 0~10 + 재기동), 넘어가는 신호, 시간으로 끝나는 장면(등장 10초, 상실 8초, 재기동 4초, 결과 15초), 재기동 1회.
2. 순수 `Flow/MatchStats`: 결과판 숫자(걸린 시간, 막기 성공률, 드론 격추, 최고 연계, 재기동 횟수).
3. `Flow/MatchFlow`(Combat Server에, 서버만 판단): 장면 + 시작 시각 복제. 전투 사건 → 신호, 장면마다 보스 멈춤/풀기, 사령관 대사, 새 전투 이벤트 `MatchScene`.
4. 아직 없는 것은 자리만: 드론(M4-04) 전에는 장면 3을 8초 뒤 넘김. 보스 무기(M4-02) 전에는 보스 HP 페이즈 전환(60·25%)을 "무기 부서짐", 무릎(10%)을 "핵 부서짐"으로 씀.
5. `UI/MatchFlowPanel`: 눈앞 위쪽 글자판(장면·할 일·남은 시간·사령관 대사·결과판). M4-06 HUD가 대신할 자리.
6. 운영자 조작(키보드 없이, B 메뉴 "판"): 판 시작, 다음 장면(시험용, 싸움 중이면 보스 무기를 바로 부숨), 자동 시작, 결과 뒤 자동 재시작. 재시작은 있던 "판 재시작"·두 스틱 1초.
7. 테스트 `MatchFlowTests`.

## 사람이 할 일
헤드셋을 쓴 사람은 키보드·마우스를 건드리지 않는다.
- [ ] 1인 양팔: Play → 위쪽 글자판 "장면 1 · 기체 점검" → 보정 → 둘 다 스틱 앞으로 → 등장(10초) → 드론 자리(8초) → 접근(보스가 걸어옴) → 근접전.
- [ ] 근접전에서 보스 HP 60%·25%에 상실 장면(8초, 보스 멈춤) → 다음 구간, 10%에 필살기 → 결과판 숫자가 나온다.
- [ ] 일부러 맞아 기체 HP 0 → "재기동 중" 4초 → 같은 구간, HP 가득. 한 번 더 지면 결과판 "작전 실패".
- [ ] B 메뉴 "판 → 다음 장면"으로 장면을 빨리 넘길 수 있다. 결과판에서 두 스틱 1초 → 처음부터(이전 판 상태 없음).
- [ ] 빌드 2대(나중에): 두 PC 글자판 장면이 같다.

## 하지 말 것
- 장면 안 내용(보스 무기·무기·드론·정비고·HUD·맵 연출)은 M4-02~07.

## 결과 (작업 후 Claude가 작성)
- 바뀐 파일:
  - 새로: `Runtime/Flow/MatchFlowRules.cs`, `Runtime/Flow/MatchStats.cs`, `Runtime/Flow/MatchFlow.cs`, `Runtime/UI/MatchFlowPanel.cs`, `Editor/MatchFlowMenu.cs`, `Tests/EditMode/MatchFlowTests.cs`
  - 고침: `Boss/BossBrain.cs`(`MovementHeld`·`AttacksHeld`·`InMeleeRange`), `Combat/CombatTypes.cs`(`MatchScene` 이벤트, 끝에 추가), `Combat/CommanderLines.cs`(대본 번호 6·18·20·23·25·46~49), `Combat/CombatCueSet.cs`(대사 소리 자리), `Editor/NetworkSessionMenu.cs`(판 흐름도 붙임), `docs/폴더_지도.md`
- 사람이 할 일(Unity):
  1. 코드 받고 `error CS` 확인. Test Runner에서 `MatchFlowTests`.
  2. `Sandbox_Pilot`에서 **Rebuild Robot + Pilot Input**(또는 **Add Match Flow**) → Ctrl+S. `Main`은 Build M1 Main → Ctrl+S.
  3. 새 `.cs`의 `.meta`, 바뀐 씬을 커밋.
- 사람이 확인할 것: 위 "사람이 할 일".
- 남은 문제 / 제안:
  - 컴파일은 클라우드에서 못 돌려 확인 못 함. `error CS`가 나오면 알려 주면 바로 고친다.
  - 재기동 때 기체 HP만 채운다. "그 구간 처음부터"의 보스 쪽(무기 내구도 되돌리기)은 M4-02에서 무기로 바꿀 때 넣는다.
  - 근접전 전에 보스 HP를 60% 아래로 깎으면 첫 상실이 건너뛰어질 수 있다(HP 다리 한계). M4-02에서 무기로 바꾸면 없어진다.
  - "자동 시작"·"자동 재시작" 메뉴 값은 호스트(PC-A) 것만 쓴다.
  - 자동 재시작은 서버만 초기화한다(보정은 그대로). 전시 운영에서 쓸지 정하면 다듬는다.
  - 사령관 대사 번호표 전체 정리(1~49)는 M4-06. 지금은 새 번호만 더했고 기존 1~16은 그대로.
