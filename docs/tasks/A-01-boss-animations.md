# A-01 로봇 애니메이션 수집 (Mixamo, 보스 + 플레이어 몸)

- 마일스톤: M2·M4 준비 / 담당: 유진 / 브랜치: `yujin`
- 선행: 없음(바로 시작 가능). Unity 임포트는 M0-03 병합 후.

## 목표
`docs/design/04_art_spec.md`의 "애니메이션 목록"(보스 표와 플레이어 몸 표) 중 **필요** 표시된 클립을 Mixamo에서 받아 모은다. 우선 1은 10/29, 우선 2는 11/12까지.

## 배경
- 우리 로봇은 Mixamo 뼈대라서 Mixamo 클립을 그대로 쓸 수 있다. 모델링·리깅 지식은 필요 없다.
- 이미 있는 클립(Idle, Punch, Stagger, AlternatingCombo, Death, Walk, Turn)은 다시 받지 않는다.

## 사람이 할 일 (유진)
1. [mixamo.com](https://www.mixamo.com) 에 Adobe 계정으로 로그인한다.
2. 오른쪽 위 캐릭터는 기본 캐릭터 그대로 둬도 된다(Without Skin으로 받으면 뼈 움직임만 저장됨).
3. Animations 탭에서 목록의 검색어로 찾고, 미리보기에서 **거대 로봇이 하기에 묵직하고 읽기 쉬운 동작**을 고른다. 후보가 여러 개면 2~3개 받아도 된다.
4. 오른쪽 슬라이더로 속도(Overdrive)·팔 간격(Character Arm-Space)을 필요하면 조절한다.
5. Download: **Format = FBX for Unity(.fbx), Skin = Without Skin, Frames per Second = 30**, 이동하는 클립은 **In Place 체크**.
6. 파일 이름을 `Boss_<동작>.fbx`(플레이어 전용은 `Pilot_<동작>.fbx`)로 바꾼다. 예: `Boss_PunchLeft.fbx`, `Boss_Knockback.fbx`, `Boss_SlamBoth.fbx`, `Boss_Charge.fbx`, `Boss_Kneel.fbx`, `Boss_WalkBack.fbx`.
7. 아래 둘 중 하나로 넘긴다.
   - Git을 쓸 수 있으면: 이 레포를 clone → `Assets/_Anseong/Art/Robot/Animation/Source/Mixamo/`에 넣고 `yujin` 브랜치에 커밋·푸시 → 팀 단톡방에 `[올림]`([GitHub 작업 규칙](../GitHub_작업_규칙.md)).
   - 어려우면: 드라이브 `안성제철_애니메이션` 폴더에 올리고 팀 단톡방에 링크. 코드 담당(선우)의 Claude Code 세션이 넣는다.
   - 레포의 `Assets/_Anseong/Art/Robot/Source/BossUpdatedCompatible.fbx`를 Mixamo에 올릴 필요는 없다. 기본 캐릭터로 받아도 우리 뼈대에 맞는다.
8. 기존 레포(`Lunatic2339/anseong-steel`, develop)의 `AnseongSteel/Assets/_BossPrototype/Animation/CombatSource/GroundCast.fbx`를 Mixamo 미리보기나 Unity에서 보고, **두 손 내려찍기**로 쓸 만한지 의견을 적는다.

## Claude가 할 일 (파일을 받은 세션)
1. 클립을 `Assets/_Anseong/Art/Robot/Animation/Source/Mixamo/`에 넣는다. M0-03의 임포트 규칙(`RobotModelPostprocessor`)이 애니메이션 FBX에도 적용되는지 확인한다.
2. 각 클립의 길이(초)와 루프 여부를 결과 칸에 표로 적는다.

## 하지 말 것
- 이미 있는 클립을 다시 받기. 손가락 애니메이션. With Skin으로 받기(파일이 커짐).

## 완료 기준
- 우선 1의 **필요** 클립(왼손 펀치, 크게 밀려남)이 들어옴 — 10/29.
- 우선 2의 **필요** 클립(두 손 내려찍기, 돌진, 무릎 꿇기, 뒤로 걷기, 플레이어 옆걸음 좌/우)이 들어옴 — 11/12.

## 결과 (작업 후 작성)
- 받은 클립:
- 사람이 확인할 것:
- 남은 문제 / 제안:
