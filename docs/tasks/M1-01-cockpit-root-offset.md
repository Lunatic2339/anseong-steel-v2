# M1-01 조종석을 로봇 루트에 고정 + 플레이어 로봇 프리팹

- 마일스톤: M1 / 담당: 선우 / 브랜치: `feat/sunwoo-cockpit-root`
- 선행: M0-04 병합(완료). 다른 M1 작업보다 먼저.
- 기한(제안): 10/20

## 목표
조종석(XR Origin)을 애니메이션되는 머리 뼈가 아니라 **로봇 루트 기준 고정 오프셋**에 붙이고, 대기 클립이 도는 플레이어 로봇을 프리팹 하나로 만든다. 이후 M1 작업은 모두 이 프리팹 위에서 한다.

## 배경
- `docs/design/02_architecture.md` "시점과 스케일", "플레이어 로봇 몸".
- M0-04 결과 칸: Head 좌석 눈 57.5m, 좌우 좌석 ±1.05m, `hideWhileSeated`(머리 렌더러 11개), `updateWhenOffscreen` 문제.
- 로봇이 약 61.55배라 클립에서 머리가 1cm 움직이면 카메라가 약 60cm 움직인다. 그래서 뼈를 따라가면 안 된다.

## Claude가 할 일
1. `CockpitSeat`/`CockpitSeatMath`를 바꾼다: 좌석 위치 = 로봇 루트 + 고정 오프셋(m, 확대되지 않은 값). 회전은 로봇 루트의 yaw만. 좌석 후보 `Head`·`ChestTop`은 루트 기준 오프셋으로 다시 정의하고, 기본값은 M0-04 측정값(Head 눈 57.5m, ChestTop 52.4m)에서 계산한다. 개발용 좌석 전환(왼손 X 버튼)은 그대로 둔다.
2. 머리 숨김: 숨길 렌더러를 Inspector 목록으로 받는다(이름 문자열로 찾지 않음). 목록을 채우는 일은 에디터 메뉴가 한다(M0-04와 같은 기준: 정점 절반 이상이 Head·Head_end 뼈).
3. 맞았을 때 흔들림을 넣을 자리만 만든다: `CockpitSeat`에 작은 추가 오프셋 입력(기본 0). 실제 흔들림은 M2.
4. 에디터 메뉴 **Anseong → Setup → Build Player Robot Prefab**: 65m로 맞춘 로봇 + Animator(`Robot_Idle.controller`, 대기 클립 재생) + `Cockpit`(좌석 두 개) + 머리 숨김 목록 → `Assets/_Anseong/Prefabs/Robot_Player.prefab`. 같은 방식으로 `Robot_Boss.prefab`(보스 색 `Boss_Tint.mat`, 조종석 없음)도 만든다.
5. 컬링: IK가 팔을 바인드 자세 밖으로 옮기므로 메뉴가 두 프리팹의 SkinnedMeshRenderer에 `updateWhenOffscreen`을 켠다(임시). 최종 방법(bounds 확대 등)은 A-02 병합 후 정한다고 결과 칸에 남긴다.
6. EditMode 테스트: 루트가 회전·이동해도 좌석이 yaw만 따르는지, 루트가 기울어도(pitch/roll) 좌석이 수평인지.

## 사람이 할 일
- [ ] 메뉴 실행 → `Sandbox_EyeHeight` 또는 새 샌드박스 씬에 두 프리팹 배치, XR Origin 연결(M0-04의 Attach 메뉴) → 씬 저장.
- [ ] 헤드셋: 대기 클립이 도는데 시야가 흔들리지 않는다. 자기 로봇 머리가 보이지 않는다(그림자는 보임). 좌석 전환이 X 버튼으로 된다.
- [ ] 보스 손가락 깜빡임이 없다.

## 하지 말 것
- 팔 IK, 입력, 펀치. `Main.unity` 수정(M1-07에서).
- 피격 흔들림 구현.

## 완료 기준
- 헤드셋에서 로봇이 대기 클립을 재생하는 동안 카메라가 흔들리지 않고, 머리 메시가 시야를 막지 않는다.
- 두 프리팹이 메뉴 한 번으로 다시 만들어진다.

## 결과 (작업 후 Claude가 작성)
- 작업 브랜치: `claude/m1-cockpit-input-x0vygh` (M1-02와 같은 브랜치, 커밋은 따로).
- 방식: 조종석 위치 = 로봇 루트 + 고정 오프셋(m), 회전 = 루트 yaw만. 애니메이션 본을 전혀 읽지 않는다. `Cockpit`은 `Robot_Player` 루트(배율 1)의 자식이고, 매 `LateUpdate`(실행 순서 10000)에 월드 자세를 다시 맞추므로 루트가 기울어도 수평을 유지한다.
- 바뀐 파일:
  - `Runtime/Pilot/CockpitSeat.cs` — 좌석에서 `bone` 필드를 없애고 오프셋을 루트 기준으로 바꿈. 기본값 Head 바닥 55.9 m, ChestTop 50.8 m(M0-04 눈 57.5·52.4 m − 키 1.6 m). 피격 흔들림 자리 `additionalOffset`(Inspector, 기본 0) + `AdditionalOffset` 프로퍼티. 숨김 목록·X 버튼 좌석 전환은 그대로.
  - `Runtime/Pilot/CockpitSeatMath.cs` — 계산은 같고 입력 이름·설명만 루트 기준으로.
  - `Tests/EditMode/CockpitSeatMathTests.cs` — 2개 추가(루트 이동+yaw를 따름, 루트가 pitch/roll로 기울어도 좌석이 수평이고 높이가 기울지 않음).
  - `Editor/RobotPrefabMenu.cs` (새 파일) — **Anseong → Setup → Build Player Robot Prefab**. 미리보기 씬에서 조립하므로 열린 씬은 건드리지 않는다. `Prefabs/Robot_Player.prefab`(65 m 모델 + `Robot_Idle.controller` Animator(루트 모션 끔, AlwaysAnimate) + `Cockpit`(좌석 2개, 머리 숨김 목록, 임시 바닥·콘솔·창틀 박스)), `Prefabs/Robot_Boss.prefab`(같은 모델·대기 클립 + `Boss_Tint.mat`, 조종석 없음). 두 프리팹의 모든 SkinnedMeshRenderer에 `updateWhenOffscreen`을 켠다(임시).
  - `Editor/PilotSandboxMenu.cs` (새 파일) — **Anseong → Sandbox → Build Cockpit Root Test**: 새 씬에 두 프리팹(보스 40 m 앞, 180°)과 바닥.
  - `Editor/RobotSandboxUtility.cs` — 좌석 오프셋 계산, 머리 렌더러 찾기, 조종석 박스, 보스 색, 액션 찾기를 메뉴들이 같이 쓰도록 `EyeHeightSandboxMenu.cs`에서 옮김. `EyeHeightSandboxMenu.cs`는 이 도우미를 쓰게 정리(동작 같음, 좌석 오프셋만 루트 기준).
- Claude가 확인한 것: 이 환경에는 Unity가 없어 **컴파일·EditMode 테스트·메뉴 실행을 하지 못했다.** 순수 계산(`CockpitSeatMath` 테스트 8개)만 Unity 수학을 흉내 낸 스텁으로 따로 돌려 통과를 봤다. 새 `.cs`의 `.meta`는 Unity가 만든 뒤 커밋해야 한다.
- 사람이 확인할 것:
  - [ ] Unity를 열고 Console에 `error CS`가 없는지, 새 `.cs`·`Prefabs` 폴더의 `.meta`가 생겼는지 → 같이 커밋.
  - [ ] Test Runner → EditMode 전체 통과.
  - [ ] **Anseong → Setup → Build Player Robot Prefab** → Console 로그: 높이 65 m 근처(경고 없음), 본 높이가 M0-04와 같은지(Head 55.8 m, 어깨 52.4 m), 숨길 머리 렌더러 11개. 한 번 더 실행해도 같은 프리팹이 다시 만들어지는지.
  - [ ] **Anseong → Sandbox → Build Cockpit Root Test** → XR Origin (VR) 추가 → **Attach XR Origin To Cockpit** → `Scenes/Sandbox/Sandbox_Pilot.unity`로 저장.
  - [ ] Play(에디터): Scene 뷰에서 로봇이 대기 클립을 재생하고, `Cockpit`이 움직이지 않는다. `Robot_Player`를 Scene 뷰에서 옮기거나 Y축으로 돌리면 조종석이 따라오고, X·Z축으로 기울이면 조종석은 수평을 유지한다.
  - [ ] 헤드셋: 대기 클립이 도는 동안 시야가 흔들리지 않는다. 자기 로봇 머리가 보이지 않는다(그림자는 보임). X 버튼으로 좌석 전환.
  - [ ] 헤드셋: 보스 손가락 깜빡임이 없다.
- 1차 헤드셋 확인 (선우, 2026-10-17): 프리팹 로그 정상(65 m, 본 높이 M0-04와 같음, 머리 렌더러 11개). 서서 보면 정면이 보인다. 그러나 **Head 좌석은 머리 뒤 링 위치, ChestTop 좌석은 사실상 얼굴 위치**였다. 원인: 좌석을 기본 자세에서 쟀는데 대기 클립에서는 몸이 더 낮다. → 메뉴가 대기 클립을 16번 재생한 평균 자세에서 좌석을 재도록 수정(본 자세는 원래대로 되돌림). 다시 확인 필요.
- 남은 문제 / 제안:
  - 기존 `Sandbox_EyeHeight.unity`의 `CockpitSeat`는 오프셋이 본 기준으로 저장되어 있어 이제 조종석이 땅 높이로 내려간다. 그 씬을 다시 쓰려면 **Build Eye Height Test**로 다시 만든다(M1부터는 `Sandbox_Pilot` 사용 제안).
  - `updateWhenOffscreen`은 렌더러 291개라 CPU 비용이 있다. 최종 방법(bounds 확대 등)은 A-02 병합 후 정한다.
  - 대기 클립이 엉덩이를 위아래로 움직여도 조종석은 따라가지 않으므로, 클립에 따라 몸통이 조종석 바닥을 뚫고 보일 수 있다. 헤드셋 확인 때 같이 본다.
  - 프리팹 저장 위치 `Assets/_Anseong/Prefabs/`는 CLAUDE.md 폴더 표에 없다. 표에 넣을지 팀 회의에서 정할 것.
