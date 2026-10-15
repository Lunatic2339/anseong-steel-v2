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
  - `Assets/_Anseong/Runtime/Pilot/PilotInput.inputactions` (새로 만듦. 맵 `Left Hand`·`Right Hand`, 액션 Position·Rotation·Tracking State)
  - `Assets/_Anseong/Runtime/Pilot/TrackingStateRules.cs`: trackingState 플래그 해석 (순수 클래스)
  - `Assets/_Anseong/Runtime/Pilot/TrackedPoseProbe.cs`
  - `Assets/_Anseong/Editor/SceneSetupMenu.cs`: 메뉴 **Anseong → Setup → Add Tracking Probes**
  - `Assets/_Anseong/Tests/EditMode/TrackingStateRulesTests.cs`
  - `ProjectSettings/Packages/com.unity.ai.assistant/` 삭제 (패키지 제거 후 남은 설정, 선우 승인)
- 지시서와 다르게 한 것:
  - XRI 3.3.2의 **XR Origin (VR)** 메뉴는 `Camera Offset` + `Main Camera`만 만들고 손 오브젝트를 만들지 않는다. 그래서 메뉴가 `Camera Offset` 아래에 `Left Hand Probe`·`Right Hand Probe`를 직접 만든다(각각 10cm 큐브 자식, 콜라이더 없음). 같은 손 액션이 연결된 프로브가 이미 있으면 건너뛴다.
  - "XRI Default Input Actions"는 Starter Assets 샘플 안에만 있다(임포트 금지). 그래서 프로젝트 전용 `PilotInput.inputactions`를 만들었다. M1의 그립·트리거도 이 파일에 추가한다.
- XRI 기본 입력 액션의 손 자세 경로 (`Samples~/Starter Assets/XRI Default Input Actions.inputactions`, 맵 `XRI Left` / `XRI Right`):
  - Position: `<XRController>{LeftHand}/pointerPosition`, `<XRController>{LeftHand}/devicePosition`, `<XRHandDevice>{LeftHand}/devicePosition` (+ Vector3Fallback 컴포지트)
  - Rotation: `<XRController>{LeftHand}/pointerRotation`, `<XRController>{LeftHand}/deviceRotation`, `<XRHandDevice>{LeftHand}/deviceRotation` (+ QuaternionFallback)
  - Tracking State: `<XRController>{LeftHand}/trackingState`, `<XRHandDevice>{LeftHand}/trackingState`
  - Is Tracked: `<XRController>{LeftHand}/isTracked`
  - 오른손은 `{LeftHand}` → `{RightHand}`. 머리(`XRI Head`)는 `<XRHMD>/centerEyePosition`·`centerEyeRotation`·`trackingState`.
  - `PilotInput`은 이 중 **`<XRController>{Left|RightHand}/devicePosition`·`deviceRotation`·`trackingState`만** 쓴다. pointer(조준) 자세가 아니라 grip(주먹) 자세가 펀치 판정에 맞고, 같은 액션에 두 자세를 섞지 않기 위해서다. 손 추적(`XRHandDevice`)은 쓰지 않는다.
- 사람이 확인할 것:
  - [ ] Unity 콘솔에 컴파일 에러 없음, Test Runner(EditMode)에서 `TrackingStateRulesTests` 5개 통과.
  - [ ] 메뉴 실행 후 Hierarchy: `XR Origin (VR)/Camera Offset/Left Hand Probe`, `Right Hand Probe`. 각 `TrackedPoseProbe`에 액션 3개가 연결됨. 메뉴를 한 번 더 실행해도 중복으로 생기지 않음. Ctrl+Z로 되돌릴 수 있음.
  - [ ] 헤드셋: 고개를 돌리면 시야가 따라옴, 양손 큐브가 컨트롤러 손잡이 위치를 따라옴(초록), 추적이 끊기면 빨강.
  - [ ] 색 변화: Quest는 시야 밖 컨트롤러 위치를 IMU로 추정해 계속 "추적 중"으로 보고할 수 있다. 등 뒤로 숨겨도 색이 안 바뀌면 컨트롤러를 내려놓아 절전시키거나 전원을 꺼서 확인한다.
  - [ ] PC-B에서 같은 확인.
  - PC-A Play 결과 (2026-10-16, **Air Link**로 확인. USB 3.0 케이블 없음):
    - 테스트 `TrackingStateRulesTests` 5/5 통과. 씬에 프로브 2개, PilotInput 액션 3개씩 연결됨. Scene List는 `Main`만 활성(SampleScene은 비활성으로 남아 있음).
    - 선우가 헤드셋으로 확인: 고개를 돌리면 시야가 따라옴, 양손 큐브가 컨트롤러를 따라옴, 프레임 끊김 없음(대략).
    - 시야 밖이나 등 뒤에서도 Quest가 컨트롤러 추적을 유지해서 빨강 전환은 보지 못했다. 배터리를 빼서 확인할 수 있다.
  - `Editor.log` XR·OpenXR 요약 (PC-A Play):
    - 런타임 Oculus 1.208.0, System "Meta Quest 3", Orientation/Position Tracking 둘 다 True, OpenXR API 1.1.53.
    - 요청한 기능: Oculus Touch Controller Profile, Meta Quest Touch Plus Controller Profile. 런타임 미지원 확장 0개.
    - Single Pass Instanced, 눈당 1824×1968, RGBA8 LDR. 세션 IDLE→READY→SYNCHRONIZED→VISIBLE→FOCUSED 정상 진입·종료.
    - 경고 3줄, 모두 Meta 런타임 내부 IPC 메시지라 무시 가능: `ipc_GetClientStateHandle FAILED: InputModalityManifestState`(시작), `waitForOverlapped: I/O FAILED` 109·233(종료 시 파이프 닫힘). XR 에러 없음.
- 남은 문제 / 제안:
  - 유선 Link용 USB 3.0 케이블이 없어서 PC-A를 Air Link로 확인했다. 시연은 유선 Link로 하므로 케이블 2개(PC-A·PC-B용)를 준비하고, 유선으로 다시 확인해야 한다.
  - `ProjectSettings/EditorBuildSettings.asset`의 `m_configObjects`에 `com.unity.dt.app-ui` 참조가 남아 있다. AI Assistant를 제거하고 남은 것으로 보인다. Scene List를 정리할 때 함께 확인하면 좋겠다(텍스트로 직접 고치지 않았음).
  - `Assets/Scenes/SampleScene.unity`가 Scene List에 남아 있다. `Main`만 남긴 뒤 SampleScene을 지울지 결정해야 한다.
