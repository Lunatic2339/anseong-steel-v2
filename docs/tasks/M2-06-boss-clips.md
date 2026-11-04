# M2-06 유진 보스 클립을 보스 기술에 연결

- 마일스톤: M2 / 담당: 선우 / 브랜치: `feat/m2-06-boss-clips`
- 선행: M2-05 (PR #35 병합)
- 요청: 선우 11/04 "05검증하고나서 06으로 한번 합쳐보지뭐"

## 목표
보스가 임시 클립(펀치 하나·걷기) 대신 유진 브랜치(`origin/Eugene`, 11/04 "이전 보스 애니메이팅 가져옴")의 동작으로 기술을 쓴다. 왼손 펀치는 왼손으로, 연타는 좌우로, 내려찍기는 두 손으로 보인다.

## 배경
- 유진 클립 22개는 유진 샌드박스 프리팹(`BossV13FinalAnimation`)용이라 뼈 경로가 `Visual/Mecha/Armature/mixamorig:...`이고, 손에 새 손 리깅 뼈(`HF_...`)와 검(`PreviewGreatsword`) 곡선이 섞여 있다. 우리 보스 모델(`BossUpdatedCompatible.fbx`)은 `Armature/mixamorig:...`이고 뼈 이름은 같다(Hips·Spine·팔·손가락·다리, 쇄골 없음).
- 왼손 펀치 클립은 없다(04_art_spec "왼손 펀치 필요").
- 유진 브랜치의 V13 모델·8K 텍스처·손 리깅·샌드박스 씬은 이 작업 범위가 아니다(아트 교체는 A-02/A-03, 성능 예산 확인 뒤).

## Claude가 한 일
1. 유진 클립 7개만 원본으로 가져옴: `Art/Boss/Animation/Source/` 에 Idle, Walk, Stagger, Death, Punch, Combat_AlternatingCombo, Combat_OverheadSmash (`.anim`, 합계 약 7MB). `.meta`는 Unity가 새로 만든다(유진 샌드박스와 GUID가 겹치지 않게).
2. 메뉴 **Anseong → Boss → Import Boss Clips (Yujin)** (`Editor/BossClipImport.cs`):
   - 뼈 이름으로 경로를 우리 모델에 다시 잇고, 못 찾는 곡선(HF_ 손 뼈, 검)은 버린다. 골반 이동은 모델 골반 높이 비율로 맞춘다.
   - 오른손 펀치를 좌우 대칭으로 구워 왼손 펀치를 만든다(각 뼈가 처음 자세에서 돈 만큼을 짝 뼈에서 가져와 거울에 비춤).
   - 결과: `Art/Boss/Animation/Boss_Idle·Walk·Stagger·Death·PunchRight·PunchLeft·AlternatingCombo·SlamBoth.anim`.
   - 기술 에셋 재생 속도: 클립 속 주먹이 처음 크게 빨라지는 순간이 예고 끝(첫 명중)에 오도록 `clipSpeed`를 맞춘다(0.5~2.5배). 클립 칸을 직접 채운 기술은 건드리지 않는다.
3. 칸이 비어 있으면 이 클립을 쓰게 연결: 기술 에셋(`BossAttackMenu.FallbackClip`), 보스 대기·걷기·휘청·쓰러짐(`RobotArtSettings`), 보스 컨트롤러 대기(`RobotPrefabMenu`). 칸에 직접 넣은 클립이 먼저다.
4. 돌진(Charge)·포효·무릎·크게 밀려남은 맞는 클립이 없어 지금처럼 임시(걷기·대기·휘청)로 둔다.

### 06 확인 중 받은 보완 (선우 11/04)
5. 필살기 "펀치 판정이 나야 되는 게 아니고 그냥 팔만 뻗으면": 준비 = 정면으로 85% 뻗음 + 추적만(버튼은 `FinisherSettings.needButtons`, 기본 끔). 보스가 무릎 꿇은 동안 서버가 펀치 명중·헛침·부스트를 받지 않는다(`CombatAuthority`).
6. "이펙트가 제대로 안 보이니 필살기 때는 멀리 떨어져서": 무릎 꿇으면 보스가 2.5초 동안 90 m까지 뒤로 물러난다(`BossBrain` 필살기 거리, 조절 메뉴 "보스").
8. "이동한 상태로는 펀치 판정이 안 됨": 회피 미끄러짐(10 m 옆으로)이 돌아오지 않아 보스가 정면에서 벗어났었다. 이제 옆에서 0.4초 머문 뒤 0.8초에 걸쳐 제자리로 돌아온다(`PlayerHitFeedback` "회피 뒤 제자리로", 끄면 예전처럼 남음). 돌진 안내 글자에 "부딪히기 직전에"를 붙였다(명중 0.6초 전 안에 새로 민 것만 회피로 인정).
7. "보스 공격마다 뭘 봐야 하는지 띄워놔, 색으로만 구분하니 모르겠음": 눈앞 위쪽 큰 글자 `UI/BossAttackCallout`. 예고 때 "좌우 연타 4번 / 오른팔부터 번갈아 막기", 연타 중 "2/4 → 왼팔", 결과 "막음!·맞음 -6%·피함!", 빈틈 "지금 치기!", 무릎 "두 팔을 정면으로 쭉 뻗고 버티기". 글자 색 = 세기(노랑 일반, 주황 강공격, 빨강 막기 불가). 문구는 `Boss/BossAttackGuide`(테스트 있음). 예고 사건에 타 수(ComboStep)를 실어 보낸다.

## 사람이 할 일
1. `git pull` → Unity에서 **Anseong → Boss → Import Boss Clips (Yujin)** → Console에 클립 8개와 기술별 속도 줄이 나오는지.
2. **Anseong → Setup → Rebuild Robot + Pilot Input** → Ctrl+S → Play. (조절 메뉴 "보스 시험" = 다섯 기술 돌아가며. 안내 글자는 Add Pilot Input이 새로 만듦)
3. 헤드셋에서 볼 것:
   - [ ] 보스 대기·걷기가 유진 동작으로 보인다(몸이 꼬이거나 땅에 박히지 않음).
   - [ ] 왼손 펀치는 **왼손**, 오른손 펀치는 오른손으로 친다(왼손 펀치 = 대칭으로 구운 것이라 어색하면 알려 주기).
   - [ ] 주먹이 닿는 순간과 막기 판정(가드 표시) 순간이 대략 맞는다.
   - [ ] 연타가 좌우로, 내려찍기가 두 손으로 보인다.
   - [ ] 공격마다 눈앞 위쪽에 큰 글자로 무슨 공격·어느 팔로 막을지가 뜨고, 연타 중 다음 팔, 막으면 "막음!"이 뜬다.
   - [ ] 보스가 무릎 꿇으면 뒤로 물러나고, 버튼 없이 두 팔만 정면으로 뻗어도 충전 → 광선이 한눈에 보인다. 뻗는 동안 펀치 명중·헛침이 안 뜬다.
   - [ ] 회피(두 스틱 같은 쪽) 뒤 로봇이 제자리로 돌아오고, 그 뒤 펀치가 다시 맞는다.
   - [ ] 쓰러짐(필살기 뒤)이 유진 Death 동작.
4. 만들어진 `.meta`(Source 폴더·클립·Animation 폴더)와 `Boss_*.anim`, 바뀐 `BossAttack_*.asset`(속도)를 커밋·푸시.

## 결과 (작업 후 Claude가 작성)
- 바뀐 파일: `Editor/BossClipImport.cs`(새), `Runtime/UI/BossAttackCallout.cs`(새), `Runtime/Boss/BossAttackGuide.cs`(새), `Tests/EditMode/BossAttackGuideTests.cs`(새), `Runtime/Boss/BossServer.cs`·`BossBrain.cs`, `Runtime/Combat/PlayerHitFeedback.cs`·`FinisherRules.cs`·`FinisherReporter.cs`·`CombatAuthority.cs`·`CommanderLines.cs`·`CombatTypes.cs`, `Runtime/UI/TestGuidePanel.cs`, `Editor/SceneSetupMenu.cs`, 테스트(`FinisherTests`·`CombatAuthorityTests`), `docs/design/08_tuning_menu.md`, `Editor/BossAttackMenu.cs`, `Editor/RobotArtSettings.cs`, `Editor/RobotPrefabMenu.cs`, `Editor/RobotSandboxUtility.cs`(Hips 이름), `Runtime/Boss/BossAttackDefinition.cs`(툴팁), `Art/Boss/Animation/Source/*.anim`(유진 원본 7개), `docs/폴더_지도.md`.
- 클라우드에서 컴파일 못 함 → Editor.log `error CS` 확인 필요.
- 남은 문제/제안:
  - 걷기 클립의 골반 앞뒤 이동이 남아 있으면 반복할 때 보스가 툭 뒤로 돌아갈 수 있다. 보이면 걷기만 골반 앞뒤를 지우는 옵션을 붙인다.
  - 펀치 원본이 5.1초로 길어 속도를 올리면 가볍게 보일 수 있다(메카 기준 "묵직하게"). 그러면 속도 대신 기술 에셋 예고 시간을 늘리는 쪽을 제안.
  - 돌진·포효·무릎·밀려남 클립은 유진에게 요청 필요(04_art_spec 표).
  - 조종사 클립 몸 기준 확인 끝나면 M2-05의 임시 좌우 표시 구는 꺼도 된다(BossTelegraphTint "표시 구").
