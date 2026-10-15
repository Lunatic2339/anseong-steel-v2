# M0-02 XR 기본 씬과 두 PC 헤드셋 확인

- 마일스톤: M0 / 담당: 선우 (+PC-B 담당 팀원 한 명) / 브랜치: `feat/sunwoo-xr-basic-scene`
- 선행: M0-01 병합

## 목표
`Main.unity`를 만들고, **PC 두 대 모두**에서 Quest 3 Link로 머리와 양손 컨트롤러가 추적되는 것을 헤드셋으로 확인한다.

## 배경
- `docs/design/02_architecture.md`의 "장비".
- XR Interaction Toolkit은 XR Origin과 입력 액션만 쓴다(상호작용 컴포넌트는 쓰지 않음).

## Claude가 할 일
1. `Assets/_Anseong/Runtime/Pilot/TrackedPoseProbe.cs`: 컨트롤러 하나의 위치·회전·추적 상태를 Input System 액션(`InputActionReference`)으로 읽어 자기 Transform에 적용하고, 추적이 끊기면 자식 렌더러 색을 바꾸는 작은 컴포넌트. 디버그 표시는 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
2. `Assets/_Anseong/Editor/SceneSetupMenu.cs`: 메뉴 **Anseong → Setup → Add Tracking Probes**. 선택한 씬의 XR Origin을 찾아 왼손·오른손 아래에 10cm 큐브 + `TrackedPoseProbe`를 붙인다. 이미 있으면 중복 생성하지 않는다. (씬 파일은 사람이 저장한다.)
3. XRI 기본 입력 액션 에셋에서 왼손/오른손 position·rotation·trackingState 액션 경로를 찾아 결과에 적는다.
4. 사람이 Play한 뒤 `Editor.log`의 XR·OpenXR 경고를 요약한다.

## 사람이 할 일
- [ ] File → New Scene → **Basic (URP)** → `Assets/_Anseong/Scenes/Main.unity`로 저장.
- [ ] Hierarchy 우클릭 → **XR → XR Origin (VR)** 추가. Main Camera가 따로 남아 있으면 삭제.
- [ ] 메뉴 **Anseong → Setup → Add Tracking Probes** 실행 → 씬 저장.
- [ ] File → Build Profiles(또는 Build Settings)에서 Scene List에 `Main`만 남긴다.
- [ ] Quest 3를 USB로 연결 → 헤드셋에서 Link 켜기 → Unity Play.
- [ ] 헤드셋으로 확인: 고개를 돌리면 시야가 따라옴, 양손 큐브가 컨트롤러를 따라옴, 컨트롤러를 등 뒤로 숨기면 큐브 색이 바뀜.
- [ ] **PC-B**에서도: 레포 clone → Unity 6000.3.26f1로 열기 → Meta Quest Link 앱을 OpenXR 런타임으로 지정 → 같은 확인.

## 하지 말 것
- 네트워크, 로봇 모델, IK.
- XRI 샘플(Starter Assets) 임포트.

## 완료 기준
- 두 PC 모두 헤드셋에서 머리·양손 추적이 보인다.
- 프레임이 끊기지 않는다(대략 확인, 수치는 M5).

## 결과 (작업 후 Claude가 작성)
- 바뀐 파일:
- 사람이 확인할 것:
- 남은 문제 / 제안:
