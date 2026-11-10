# M4~M7 밑바탕 (한 브랜치)

- 마일스톤: M4~M7 / 담당: 선우(코드는 Claude) / 브랜치: `feat/m4-m7-skeleton` → PR 하나로 `develop`
- 왜 한 브랜치: 선우 11/10 "브랜치 너무 쪼개면 골치 아프니까 M7까지 밑바탕 싹 다 짜 놔". M4-01 지시서(`M4-01-match-flow.md`)는 그대로 두고, M4-02~07·M5·M6·M7 밑바탕을 이 문서 하나로 묶는다.
- 기준: `docs/design/10_game_scenario.md` 대본(확정 사항 1~14, 특히 6 보스 새 클립 없음·10 코드와 있는 에셋만), `07_milestones.md`, `12_scenario_work_split.md`.
- **전부 프로토타입**(선우 11/04): 도형·글자·빈 소리로 한 판이 끝까지 돌게. 팀원 결과물은 칸(프리팹·소리·효과 표)에 넣기만 하면 바뀐다.

## 합친 브랜치
develop, `feat/m2-06-boss-clips`, `feat/m3-02-pose-sync`, `feat/m3-04-exo-serial`, `feat/m3-05-two-pc-build`, `feat/m4-01-match-flow`, 대본 문서(`claude/project-thread-o3noxy`).
안 합침: `feat/m1-09-comparison-arms`(RobotArtSettings 충돌, 비교용이라 보류), `feat/m3-02-session-prefab`(오래되어 갈라짐).

## 만든 것

| 번호 | 내용 | 주요 파일 |
|---|---|---|
| M4-02 보스 무기·원거리 | 무기 3개(팔뚝 포 → 가슴 장갑 → 핵, 내구도 30 = HP 70·40·10 경계), 금 칸, 부서지면 떨어짐, 보복(포 → 돌진, 장갑 → 두 손 내려찍기), 접근 중 주먹 포탄, 120m 등장, 머리 빛, 재기동 때 그 무기 처음으로 | `Boss/BossWeaponRules`, `BossWeaponVisuals`, `BossBrain`, `BossPhaseTable`, `Combat/BossHealth` |
| M4-03 우리 무기 | 왼 방패/스캐너, 오른 검/포(장면이 정함), 상실 2번에 떨어뜨림, 방패 하나로 양쪽 막기(왼팔 가드 + 덮는 쪽), 무기 들면 아무 세 번 번갈아 = 연계 | `Flow/WeaponRules`, `Combat/PlayerWeaponVisuals`, `GuardRules`, `Boss/DefenseRules`, `CombatAuthority` |
| M4-06 HUD·사령관 | 한쪽에만 보이는 표시(공격 방향 = 오른쪽, 드론 방향 = 왼쪽, 해킹된 쪽 가림), 사령관 대사 번호표 1~49 + 소리 자리 | `UI/HudRules`, `UI/PilotHud`, `Combat/CommanderLines`, `CombatCueSet` |
| M4-04 드론 | 숨어서 쏘기(스캐너로 3초 드러냄 → 포로 격추, 보스 쏘면 튕김), 근접전 금 1·2개 때 끼어들기, 달려들기(주먹), 해킹(동료 손으로 떼기), 60초 철수 | `Combat/DroneRules`, `Flow/DroneDirector` |
| M4-05 기체 점검 | 보정 → 철판 앞 관절 시험(번갈아 3번 + 트리거) → 방패 막기 연습 3번 → 스캐너·포 설명 → 같이 밀어 출격 | `Flow/CheckupRules`, `Flow/CheckupDirector` |
| M4-07 맵 연출 | 크레인 고리 쏘면 코일 → 보스 휘청·피해, 돌진 피하면 컨베이어 다리에 4초 박힘, 맨손 첫 부스트로 사일로 처박기 | `Flow/MapEvents` |
| M5-01 손맛 | 히트스톱(보스 Animator만, 카메라·우리 팔은 안 늦춤), 외골격 신호(방패 막기·포 반동·피격·해킹 무거움, 필살기·해킹 풀림에 해제) | `Boss/BossHitstop`, `Haptics/ExoCueRules` |
| M5-02 소리 | 새 사건 소리 기본 줄(드론 사격·스캔·폭발, 포, 해킹, 장착, 맵, 장면) → 메뉴가 기존 표에 더함 | `Combat/CombatCueSet` |
| M5-03 효과 | 효과 표 에셋(사건 → 유진 VFX 프리팹, 비면 색 구) + 재생기 | `Combat/CombatEffectSet`, `CombatEffectPlayer`, `Art/VFX/CombatEffects.asset`(메뉴가 만듦) |
| M5-04 아트 교체 | 코드 없음. 칸만: 우리 무기 프리팹 4칸(`PlayerWeaponVisuals`), 보스 무기(`BossWeaponVisuals`), 드론·철판(`DroneDirector`·`CheckupDirector` 재질), 맵 물체(`MapEvents` 3칸), 효과 표, 소리 표 | – |
| M6 테스트 | 판마다 결과 한 줄 CSV(호스트), 테스트 기록표 | `Flow/MatchStats`(`MatchLogRow`), `docs/운영/테스트_기록.md` |
| M7 동결 | 리허설·당일 체크리스트 | `docs/운영/전시_당일_체크리스트.md` |

테스트: `BossWeaponRulesTests`, `WeaponRulesTests`, `HudRulesTests`, `DroneRulesTests`, `CheckupRulesTests`, `CombatEffectSetTests`, `ExoRulesTests`(M5 신호), `MatchFlowTests`(판 기록 줄).

## 사람이 할 일 (Unity, 순서대로)
1. `git pull` → Unity 저장 → `Editor.log`에서 `error CS`. **클라우드에서 컴파일을 못 돌려서 첫 컴파일 오류가 있을 수 있다.** 나오면 그대로 붙여 주면 고친다.
2. Test Runner → EditMode 전부.
3. `Anseong → Boss → Import Boss Clips (Yujin)` → `Build Player Robot Prefab` → `Sandbox_Pilot`에서 `Rebuild Robot + Pilot Input` → `Add Match Flow` → Ctrl+S. Main은 `Build M1 Main` → Ctrl+S.
4. 새로 생긴 `.meta`, `Art/Debug/BossWeaponShape.mat`, `Art/VFX/CombatEffects.asset`, 바뀐 `Audio/CombatCues.asset`, 씬을 커밋.

## 사람이 확인할 것 (1인 양팔, 헤드셋)
- [ ] 판 시작 → 기체 점검: 보정 → 철판 앞에서 번갈아 3번 + 트리거 → 철판 날아감 → 방패 들고 공 3번 막기 → 스캐너·포 → 둘 다 스틱 앞 → 출격.
- [ ] 등장: 보스가 120m에서 머리 빛. 드론 장면: 왼손 스캐너로 훑으면 드론이 드러나고 오른손 트리거로 격추, 보스를 쏘면 튕김.
- [ ] 접근: 보스가 걸어오며 주먹 포탄. 근접전: 왼 방패로 양쪽 막기, 검으로 아무 세 번 번갈아 = 연계.
- [ ] 보스 무기 금 1·2칸에 드론 끼어들기, 두 번째에 "크레인 고리를 쏴라" → 고리 쪽을 쏘면 코일 떨어지고 보스 휘청.
- [ ] 팔뚝 포가 부서지면 떨어지고 보복 돌진 → 상실 장면에 우리 포·스캐너 떨어짐. 장갑 구간에서 돌진을 피하면 컨베이어에 박힘. 핵 구간 맨손 첫 부스트에 사일로로 밀림.
- [ ] 해킹 드론: 붙은 팔 쪽 HUD가 가려지고, 다른 손을 대면 떨어짐.
- [ ] 명중·부스트·무기 부서짐에 색 구 효과, 보스만 잠깐 멈춤(카메라는 안 멈춤). B 메뉴 "연출 → 히트스톱"으로 끄고 켬.
- [ ] 결과판 뒤 `Logs/Anseong/logs/matches_*.csv`에 한 줄.
- [ ] 2대(성재와): 두 PC 장면·무기·드론이 같다. HUD 공격 방향은 오른쪽 PC에만, 드론 방향은 왼쪽 PC에만.

## 결과 (작업 후 Claude가 작성)
- 남은 문제 / 제안:
  - 컴파일 미확인(클라우드). 첫 `error CS` 수정이 필요할 수 있다.
  - 윤주 맵 크레인·컨베이어·사일로를 `MapEvents` 칸에 넣어야 맵 물체가 움직인다(비면 임시 원통·흔들림 없음).
  - 부스트 슬로 0.3초는 히트스톱(부스트 0.15초)으로 대신했다. 헤드셋에서 보고 늘린다.
  - 외골격 새 신호 세기는 임시값. 고건과 M5에 맞춘다.
  - `feat/m1-09-comparison-arms`는 합치지 않았다. 필요하면 따로.
