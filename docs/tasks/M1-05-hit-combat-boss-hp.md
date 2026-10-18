# M1-05 주먹 명중 + 전투 규칙 + 보스 HP·휘청

- 마일스톤: M1 / 담당: 선우 / 브랜치: `feat/sunwoo-hit-boss-hp`
- 선행: M1-03, M1-04 병합
- 기한(제안): 10/27

## 목표
분류된 펀치가 보스에 실제로 닿으면 피해를 한 번 주고, 보스 HP가 줄고 훅에 휘청한다. 잽–잽–훅 연계와 부스트 규칙도 이 작업에서 순수 클래스로 넣는다. 중간발표의 핵심 장면이다.

## 배경
- `docs/design/01_combat_rules.md` "공격" 판정 순서, "잽–잽–훅 연계", "부스트".
- `docs/design/05_boss_patterns.md` "보스가 맞았을 때", 중간발표 빌드는 보스 공격 없음(항상 빈틈, 맞으면 경직).
- `docs/design/02_architecture.md` "누가 무엇을 계산하나": 명중은 때린 쪽이 판정해 `HitReport(side, kind, quality, clientTime, strikeId)`로 서버에 보고.
- `03_reuse_from_old_repo.md`: 연계·부스트 `RobotCombat.cs` Hit 132-171, Boost 82-88. 보스 접촉 `MechaVRBossContact.cs`(bounds 브로드페이즈, 부위는 컴포넌트로). 진동 큐 `IntegratedFeedback.cs` 7-14 `PunchHapticQueue`.
- 휘청 클립: 기존 레포 `AS/_BossPrototype/Animation/CombatSource/Stagger.fbx`(옛 레포에 이미 있는 클립이라 A-01에서 다시 받지 않음. v2에는 아직 없으므로 5단계에서 가져온다).

## 선우 결정 (10/18, 지시서 작성 뒤)
- **공격 보정**: 종류(잽·훅·어퍼컷)만 맞게 인식되면 주먹이 안 닿아도 보스 쪽으로 끌어당겨 맞힌다. 아래 1번 "주먹 접촉"은 "종류 인식 + 보스가 보정 범위·방향 안"으로 바꾸고, 실제 접촉 방식도 Inspector 토글로 남긴다. 끌어당기려면 휘두르는 도중 중간 판정이 필요하다(M1-04 `PunchClassifier`는 동작 끝에만 결과를 냄). 01 규칙 문서도 이 PR에서 고친다.
- **어퍼컷**(10/17): M1-04에서 분류 추가, 01에 제안값(피해 4%, 연계 밖). `BossHealth`에 넣는다.
- **팔꿈치 부스터 추가타**(10/17): 부스트 추가 피해는 이 작업에서, 부스터 효과(불꽃 등)는 아래 "하지 말 것"대로 M5.

## 네트워크에 대해 정한 기본값
M1은 NGO 없이 한 PC에서 돈다. 다만 나중에 다시 쓰지 않도록 **명중 보고는 지금부터 `HitReport` 구조체로** 만들고, 받는 쪽(`CombatAuthority`)은 지금은 로컬 호출, M3에서 서버 RPC로 바꾼다. 규칙 클래스는 하나만 둔다(1인 모드 전용 규칙을 따로 만들지 않음).

## Claude가 할 일
1. 주먹 접촉: 로봇 주먹 knuckle 위치에 트리거 구, 보스에 `BossHurtZone` 컴포넌트(몸통·머리 등). bounds로 먼저 거르고 접촉. 한 `strikeId`는 한 번만 명중.
2. 판정 순서대로 연결: 입력 유효 → 그립 → 분류 → 접촉 → `HitReport` → 피해 확정 → 연계·부스트 갱신. `StrikeLog`의 명중 열을 채운다.
3. 순수 클래스 `ComboRules`: 잽(A) → 잽(B) → 훅(A), 제한 2.4초, 같은 팔 연속 잽 0.15초, 종료 조건(헛침, 차례 아닌 공격, 시간 초과, 직접 피해). 부스트는 마지막 훅 명중 뒤 0.45초 안에 그립 유지 + 트리거. 기본값 = 기획 초기값, Inspector 노출. 쓰지 않는 필드는 옮기지 않는다.
4. 순수 클래스 `BossHealth`: 피해 잽 3% / 훅 5% / 부스트 +6%. HP 0이면 멈춤(쓰러짐은 M2).
5. 보스 반응: 잽 = 효과만, 훅 = `Stagger` 앞부분 약 0.6초, 부스트 = `Stagger` 전체(밀려남 클립은 A-01 이후). 옛 레포의 `Stagger.fbx`를 `Art/Robot/Animation/Source/`로 복사하고(`.meta`는 Unity가 만들게 두고 같은 커밋에 넣는다) 보스 Animator에 추가.
6. 보스 HP 막대: 보스 위 월드 공간 막대(단순 사각형 두 개). HUD 아트는 M2 이후.
7. 소리·진동 자리: 명중 시 `SFX_PunchHit`·`SFX_HookHit`, 헛침 `SFX_PunchWhoosh`(파일이 없으면 무음으로 넘어감, S-01 1차가 오면 M1-07에서 연결). 컨트롤러 진동은 우선순위 큐로 짧게.
8. EditMode 테스트: 연계 순서·시간 초과·차례 위반, 부스트 창, 중복 명중 무시, HP 계산.

## 사람이 할 일 (헤드셋)
- [ ] 그립 잽이 보스에 닿으면 HP가 줄고, 같은 동작으로 두 번 깎이지 않는다.
- [ ] 훅이 닿으면 보스가 휘청한다.
- [ ] 그립 없이 치면 피해가 없다. 헛치면 피해 없음.
- [ ] 잽–잽–훅 순서로 치면 연계 단계가 Console에 찍히고, 훅 뒤 0.45초 안에 트리거를 누르면 부스트가 된다.
- [ ] 명중할 때 컨트롤러가 짧게 떨린다.

## 하지 말 것
- 보스 공격·패턴·페이즈(M2). NGO 연결(M3). 외골격 명령(H-01 펌웨어 후). 슬로 연출·팔꿈치 부스터 효과.

## 완료 기준
- 헤드셋에서 위 항목을 모두 확인.
- CSV에 명중 여부가 남는다. 테스트 통과.

## 결과 (작업 후 Claude가 작성)
- 구조: 판정은 `CombatDirector`(플레이어 Arms) 한 곳에서 하고, 결과는 전투 이벤트(`CombatCue`: 그립 잠금·발사·명중·헛침·연계 단계·연계 끊김·부스트 준비·부스트·부스트 놓침·보스 HP 0)로만 내보낸다. 소리·진동(`CombatCuePlayer` + `Audio/CombatCues.asset`), 보스 휘청(`BossReactionDriver`), HP 막대, 안내판은 이벤트만 듣는다. 외골격·애니메이션·이펙트는 듣는 컴포넌트를 하나 더 붙이면 된다. 지시서의 `CombatAuthority`는 순수 클래스 `CombatResolver`(M3에서 서버가 같은 클래스를 씀) + `CombatDirector.Hit`(M3에서 RPC 자리)로 만들었다.
- 바뀐 파일:
  - 순수 규칙 + 테스트: `Runtime/Combat/CombatTypes.cs`, `ComboRules.cs`, `BoostWindow.cs`, `BossHealth.cs`, `AttackAssist.cs`, `CombatResolver.cs`, `FistContact.cs`, `PunchClassifier.cs`(휘두르는 도중 판정 `Peek`), `Tests/EditMode/CombatRulesTests.cs`, `PunchClassifierTests.cs`
  - 연결: `Runtime/Combat/CombatDirector.cs`, `FistHitProbe.cs`, `CombatCueSet.cs`, `CombatCuePlayer.cs`, `PunchClassifierDriver.cs`·`StrikeLog.cs`(CSV 명중 열), `Runtime/Pilot/RobotArmDriver.cs`(주먹 목표 보정 자리 `TargetModifier`), `Runtime/UI/TestGuidePanel.cs`(보스 HP·연계·부스트·명중 줄)
  - 보스: `Runtime/Boss/BossTarget.cs`, `BossHurtZone.cs`, `BossReactionDriver.cs`, `BossHealthBar.cs`, `Art/Robot/Animation/Source/Stagger.fbx`(옛 레포 그대로 복사)
  - 메뉴: `Editor/CombatSetup.cs`(새), `RobotPrefabMenu.cs`, `RobotArmSetup.cs`, `SceneSetupMenu.cs`, `RobotArtSettings.cs`(보스 휘청 클립 칸)
  - 문서: `01_combat_rules.md`(공격 보정, 어퍼컷 연계 유지, 헛침·CSV 명중 열), `폴더_지도.md`
- 사람이 확인할 것:
  - `Anseong → Setup → Rebuild Robot + Pilot Input` → 씬 저장 → 새로 생긴 `.meta`(Stagger.fbx, 새 .cs, `Robot_Boss.controller`, `Audio/CombatCues.asset`, HP 막대 재질)를 커밋. Console에 `error CS`가 없는지.
  - 위 "사람이 할 일" 항목 + 공격 보정: 보스 쪽으로 대충 쳐도 주먹이 묵직하게 끌려가 맞는지, `CombatDirector → Assist → Enabled`를 끄면 실제로 닿아야만 맞는지.
  - 보스 휘청이 이상한 자세(뼈가 안 맞음)로 나오지 않는지.
- 남은 문제 / 제안:
  - Sandbox 씬에서는 메뉴가 보스를 플레이어 앞 26 m(제안값 = 팔 19.4 m × 0.95 + 몸통 반지름)로 당긴다. 04의 40 m에선 보정 사거리(27 m) 밖이다. 근접 거리는 M2(05)에서 확정.
  - `Stagger.fbx`는 옛 레포의 Generic 클립이라 보스 뼈 이름·경로가 다르면 휘청이 안 보인다. 그 경우 A-01 클립으로 Robot Art Settings 칸을 채운다.
  - 소리 파일이 아직 없어 무음. S-01 파일을 `Audio/`에 `SFX_PunchHit_01.wav`처럼 넣고 메뉴를 다시 돌리면 연결된다.
  - 컨트롤러 진동은 센 진동 우선만 한다. 01의 "컨트롤러별 최소 간격 0.09초"·피격 우선순위는 피격이 생기는 M2에서.
