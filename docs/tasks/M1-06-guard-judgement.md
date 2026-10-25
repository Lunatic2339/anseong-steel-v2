# M1-06 가드 판정과 표시

- 마일스톤: M1 / 담당: 선우 / 브랜치: `feat/sunwoo-guard`
- 선행: M1-03 병합 (M1-05와 동시에 해도 됨)
- 기한(제안): 10/27

## 목표
팔마다 가드 자세인지 판정하고, 조종석 안에 표시한다. 화면에 보이는 로봇 팔 자세와 판정이 일치하는지 사람이 확인할 수 있게 한다. 보스 공격은 M2라서 이번에는 판정과 표시만 만든다.

## 배경
- `docs/design/01_combat_rules.md` "가드와 회피": 주먹이 가드 영역(보정한 가드 위치 ±25cm, 얼굴 앞) 안 + 전완이 수직에서 40° 이내 + 추적 유효. 화면 자세와 판정은 따로 확인.
- 피할 버그 6(가드 = "보정 가드보다 15cm 위"만 봄). 기존 판정은 쓰지 않고 새로 만든다.

## Claude가 할 일
1. 순수 클래스 `GuardRules`: 사람 손 기준(보정값, 몸 정면 yaw)으로 팔별 가드 여부와 이유(영역 밖, 각도 초과, 추적 무효). 양팔 가드 = 두 팔 모두 가드. 수치는 Inspector.
2. 전완 방향은 손 자세가 아니라 IK 결과의 로봇 전완으로도 계산해서 둘을 비교 기록한다(화면 자세와 판정이 어긋나는지 보기 위해).
3. 조종석 콘솔에 왼팔·오른팔 가드 표시등 두 개(발광 사각형). 중간발표에서 보여줄 것이라 개발 빌드 전용이 아니다. 판정 이유 표시는 개발 빌드에서만.
4. `GuardLog`: 10Hz로 팔별 가드 여부·이유·손 오프셋·전완 각도(손 기준, 로봇 기준) CSV. `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
5. EditMode 테스트: 영역 경계, 각도 경계, 추적 무효, 양팔.

## 사람이 할 일 (헤드셋)
- [ ] 팔마다 편한 가드 자세 10번: 표시등이 켜진다.
- [ ] 주먹을 내리거나 옆으로 벌리거나 전완을 눕히면 꺼진다.
- [ ] 화면의 로봇 팔이 막는 자세로 보이는데 표시등이 꺼진 경우(또는 반대)를 적는다.
- [ ] CSV를 총괄 채팅에 올린다.

## 하지 말 것
- 보스 공격과의 충돌, 피해 감소(M2). 외골격 가드 저항.

## 완료 기준
- 헤드셋에서 가드 자세와 표시등이 일치(사람 확인). 테스트 통과.

## 결과 (작업 후 Claude가 작성)
- 브랜치: `feat/m1-06-guard-judgement`(M1-05 브랜치 위). PR은 M1-05 병합 뒤.
- 구조: `GuardDriver`(플레이어 Arms)가 팔마다 `GuardRules`를 돌리고, 켜짐·꺼짐 때 `GuardChanged` 이벤트를 낸다. 표시등과 안내판은 이 결과만 본다. M2의 막기 소리(`SFX_Guard`)·진동·외골격은 이 이벤트를 듣는 컴포넌트로 붙인다.
- 판정: 사람 손(몸 이동 보정 적용)이 보정 가드 위치 ±25cm(몸 기준 상자) 안 + 얼굴보다 5cm 앞 + 전완이 수직에서 40° 안 + 추적 유효. 가드 중엔 3cm·5°만큼 넓혀 경계에서 깜빡이지 않게 했다.
- 사람 전완 방향: 보정한 펀치(도달) 자세에서 전완 = 몸 정면이라고 보고, 그 뒤 컨트롤러가 돈 만큼 돌린다(컨트롤러 축을 가정하지 않음). 로봇 전완 = IK 결과의 팔꿈치 → 손목. 판정에 쓸 쪽은 `GuardDriver → Settings → Forearm Source`(Human 기본, Robot, Either).
- 바뀐 파일: `Runtime/Combat/GuardRules.cs`·`GuardDriver.cs`·`GuardLog.cs`(새), `Runtime/UI/GuardIndicator.cs`(새), `Runtime/UI/TestGuidePanel.cs`(가드 줄·이유), `Runtime/Pilot/RobotArmDriver.cs`(`ForearmDirection`), `Editor/CombatSetup.cs`(가드 판정·표시등 생성, 씬 연결), `Editor/SceneSetupMenu.cs`, `Editor/RobotSandboxUtility.cs`(`ConsoleTopFront`), `Tests/EditMode/GuardRulesTests.cs`(10개), `docs/폴더_지도.md`
- 사람이 확인할 것: Rebuild Robot + Pilot Input → 새 `.meta`와 `Art/Debug/GuardLight.mat` 커밋. 위 "사람이 할 일" 4개. 표시등이 안 켜지면 안내판의 이유를 보고, "팔뚝이 누움"이 계속 나오면 Forearm Source를 Robot이나 Either로 바꿔 비교. CSV는 Console의 `[GuardDriver] 가드 기록:` 경로.
- 남은 문제 / 제안: 표시등은 기본 조종석 상자의 콘솔 위에 만든다. Robot Art Settings에 조종석 모델을 넣으면 위치가 안 맞을 수 있어 `Cockpit → GuardIndicator`를 Inspector에서 옮긴다. 양팔 가드(`BothGuarding`)는 값만 있고 표시는 M2 강공격 때.
