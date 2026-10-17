# M0-04 조종석 눈높이 비교

- 마일스톤: M0 / 담당: 선우 / 브랜치: `feat/sunwoo-eye-height`
- 선행: M0-02, M0-03 병합

## 목표
헤드셋을 쓰고 **로봇 머리 높이**와 **가슴 상단** 두 조종석 위치를 비교해 하나로 확정한다.

## 배경
- `docs/design/02_architecture.md`의 "시점과 스케일".
- XR Origin은 확대하지 않는다(사람 크기 그대로). 로봇·보스는 65m.
- 가슴 조종석이면 로봇의 가드 주먹이 눈보다 위에 와서 보스를 가릴 가능성이 있다. 이것을 실제로 본다.

## Claude가 할 일
1. `Assets/_Anseong/Runtime/Pilot/CockpitSeat.cs`: 로봇 본(Transform) 기준 오프셋으로 좌석 위치를 정의하는 컴포넌트. XR Origin을 그 좌석에 붙인다(부모 지정 또는 매 프레임 위치 동기화 중 하나를 고르고 이유를 적는다. 카메라 지연이 생기면 안 됨).
2. 좌석 후보 두 개: `Head`(로봇 눈 근처), `ChestTop`(가슴 상단). 개발용으로 왼손 메뉴 버튼(InputActionReference)으로 전환. 전환 코드는 개발 빌드에서만 동작.
3. 로봇 팔을 **고정 가드 자세**로 세워두는 임시 방법 제공(에디터 메뉴로 본 회전 적용 또는 가드 포즈 클립). 이 작업에서는 IK를 만들지 않는다.
4. 보스(같은 모델, 색만 다르게)를 정면 40m 거리에 세운다(조정 가능).
5. 조종석 바닥·창틀 대용 간단한 박스 몇 개(2.1m 간격 좌석 두 개 표시).

## 사람이 할 일
- [ ] Sandbox 씬에서 Play → 헤드셋 착용.
- [ ] 두 좌석을 전환하며 확인: 보스 전신이 보이는지, 가드 주먹이 보스를 가리는지, 아래를 볼 때 높이감, 멀미.
- [ ] **MSAA 비교**: `Assets/Settings/PC_RPAsset`을 Inspector에서 열어 Anti Aliasing(MSAA)을 Disabled ↔ 4x로 바꿔 가며 헤드셋으로 본다(파일을 텍스트로 고치지 않는다). 볼 것: 40m 앞 보스 윤곽선의 계단 현상·반짝임, 프레임 끊김. 4x에서 끊김이 없으면 4x로 둔다.
- [ ] 결정한 좌석과 MSAA 값을 총괄 채팅에 알려준다(스크린샷 있으면 함께).

## 하지 말 것
- 팔 IK, 펀치, 네트워크.
- 렌더텍스처 중계 화면.

## 완료 기준
- 두 좌석을 헤드셋으로 비교했고, 하나를 골랐다.
- 고개를 돌려도 시야 지연·떨림이 없다.

## 결과 (작업 후 Claude가 작성)
- XR Origin을 붙이는 방식: **매 프레임 위치 동기화**. 로봇 계층은 약 61.55배 확대되어 있어 본의 자식으로 두면 XR Origin도 확대되고, 본의 기울기·흔들림이 카메라를 흔든다. 그래서 확대되지 않은 `Cockpit` 오브젝트(`CockpitSeat`)를 따로 두고, Animator 평가 뒤(`LateUpdate`, 실행 순서 10000)에 본 위치 + 오프셋(m)으로 옮긴다. 회전은 로봇 정면의 yaw만 따른다. XR Origin은 `Cockpit`의 자식(좌/우 좌석 ±1.05 m)이라 카메라 자세는 TrackedPoseDriver가 렌더 직전에 그대로 갱신하고, 지연이 생기지 않는다.
- 측정 (Build Eye Height Test 로그, 키 1.6 m 가정): Head 본 55.8 m, Head_end 60.1 m, Spine 42.6 m, 어깨 52.4 m. 기본 가드 자세에서 주먹 55.4 m(로봇 앞 8.7 m).
  - **Head 좌석** 눈 57.5 m → 주먹이 눈보다 약 2 m 아래.
  - **ChestTop 좌석** 눈 52.4 m(어깨 높이) → 주먹이 눈보다 약 3 m 위. 지시서 배경의 우려대로 주먹이 보스를 가릴 가능성이 있다.
  - 보스는 루트 간 40 m. 보스 머리(약 60 m)를 보는 각도는 Head 좌석에서 거의 수평, ChestTop에서 약 10° 위.
- 바뀐 파일:
  - `Runtime/Pilot/CockpitSeatMath.cs` — 좌석 자세 계산(yaw만, 오프셋 m 단위, 좌우 좌석 간격), `PilotSide` enum. 테스트 `Tests/EditMode/CockpitSeatMathTests.cs` 6개.
  - `Runtime/Pilot/CockpitSeat.cs` — 좌석 후보 배열(`Head`=Head 본, `ChestTop`=Spine 본 + 오프셋), XR Origin 좌석 배치, 좌석 전환(`#if UNITY_EDITOR || DEVELOPMENT_BUILD`, 시작 시 액션에 연결된 컨트롤 목록을 Console에 출력), 좌석별 숨김 렌더러(`hideWhileSeated`, 그림자만 남김), 선택 시 좌석 기즈모(초록=현재, 노랑=다른 후보).
  - `Runtime/Pilot/PilotInput.inputactions` — 맵 `Dev`, 액션 `Switch Seat`: 왼손 메뉴 버튼(`<XRController>{LeftHand}/{MenuButton}`), 예비로 왼손 X 버튼(`primaryButton`), 에디터용 `Tab` 키.
  - `Editor/EyeHeightSandboxMenu.cs` — 메뉴 3개:
    - **Anseong → Sandbox → Build Eye Height Test**: 새 씬. 플레이어 로봇(원점, +Z 정면)과 보스(40 m 앞, 180° 회전, 단색 재질)를 65 m로 세우고 양쪽 팔을 고정 가드 자세로 만든다(애니메이터 끔). 모든 SkinnedMeshRenderer에 `updateWhenOffscreen`을 켠다(아래 1차 확인 참고). 좌석 초기 오프셋을 본 위치에서 계산하고, 정점 절반 이상이 Head·Head_end 본에 묶인 플레이어 머리 렌더러를 Head 좌석의 숨김 목록에 넣는다. `Cockpit` 아래에 바닥판·좌석 표시 2개(간격 2.1 m)·콘솔·창틀 박스를 만든다.
    - **Attach XR Origin To Cockpit**: XR Origin을 `Cockpit`의 자식으로 옮기고 연결, Tracking Origin = Floor, 카메라 far 2000 m.
    - **Apply Guard Pose To Selection**: 선택한 로봇에 가드 자세를 다시 적용.
  - `Editor/RobotSandboxUtility.cs` — 65 m 맞춤(bounds 측정 + `HeightFit`)을 두 샌드박스 메뉴가 같이 쓰도록 `ScaleSandboxMenu.cs`에서 옮김(동작 같음).
  - `Art/Boss/Boss_Tint.mat` — 보스 구분용 URP Lit 단색(어두운 빨강, 텍스처 없음). 메뉴가 생성.
- Claude가 확인한 것 (Unity 배치 모드): 컴파일 에러·경고 0, EditMode 테스트 22/22 통과, Build Eye Height Test 실행 로그 정상(위 측정값). 헤드셋 확인은 하지 않았다.
- 사람이 확인할 것:
  - [x] Build Eye Height Test → XR Origin (VR) 추가 → Attach XR Origin To Cockpit → `Sandbox_EyeHeight.unity` 저장 (선우).
  - [x] Scene 뷰: 로봇 얼굴이 보스 쪽(+Z)을 본다 (선우).
  - 1차 헤드셋 확인 (선우, 2026-10-17): 문제 3개를 발견해 수정했다.
    - 좌석 전환이 안 됨 → 아래 2차 확인에서 원인 확인.
    - Head 좌석에서 로봇 머리 메시가 시야를 막음. → Head 좌석에 앉아 있는 동안 자기 머리 렌더러를 숨김(그림자만).
    - 보스 손가락이 보였다 안 보였다 함. 원인: 스킨 메시의 컬링 bounds가 T자세 기준이라 가드 자세로 옮긴 손 조각이 bounds 밖에 있어, 시선에 따라 컬링됨. → `updateWhenOffscreen` 켬.
  - 2차 확인 (선우, 2026-10-17): 머리 렌더러 11개를 숨김. 좌석 전환이 여전히 안 됨 → 원인: Build 메뉴가 액션 참조를 찾은 **뒤에** `NewScene`을 호출해서, 사용되지 않는 에셋을 내릴 때 참조가 사라짐(씬에 `switchSeatAction: {fileID: 0}`으로 저장됨). 새 씬을 만든 뒤에 찾도록 메뉴를 수정했고, 저장된 씬에는 액션이 연결되어 있다. 수정한 메뉴로 씬을 처음부터 다시 만들어 보지는 않았다.
  - 3차 확인 (선우, 2026-10-17): 두 좌석을 전환하며 비교 완료.
    - [x] 좌석 전환: Console 로그 `좌석 전환: ChestTop/Head` 4회, 모두 **왼손 X 버튼**(`MetaQuestTouchPlusControllerOpenXR/primarybutton`). 메뉴 버튼으로 전환된 로그는 없다.
    - [x] 보스 전신·가드 주먹·높이감·멀미·지연을 두 좌석에서 비교 → **Head 좌석으로 결정**.
  - MSAA: 이번 작업에서는 정하지 않고 **성능 확인(M5) 때 결정**한다(선우, 2026-10-17). 지금 씬은 가벼워서 비교 의미가 작고, Inspector 설정 하나라 코드에 영향이 없다. `PC_RPAsset`은 Disabled(`m_MSAA: 1`) 그대로. 그 전에 헤드셋 확인할 일이 있으면 4x를 잠깐 켜 보고 끊김이 없는지만 본다.
  - [ ] 결정한 좌석(Head)과 MSAA 보류를 총괄 채팅에 알린다.
- 결정: **Head 좌석** (선우, 2026-10-17). 다만 실제 팔 조작·보스 공격을 보지 못한 상태의 결정이다. M1 팔 IK와 보스 공격 1종이 들어간 뒤 ChestTop과 한 번 더 비교하고, 조종석 아트를 시작하기 전에 확정할 것을 제안한다(좌석 전환은 개발 빌드에 계속 남긴다).
- 남은 문제 / 제안:
  - 모델에 Spine1/Spine2·Neck·Shoulder 본이 없다(몸통은 Hips·Spine·Head만). ChestTop은 Spine 본에서 +8.25 m 위로 잡았다. 팔 IK(M1)에서 어깨(쇄골) 본이 없는 점을 고려해야 한다. 본 이름을 바꾸면 Mixamo 클립 바인딩이 깨지므로 이름은 그대로 두는 것을 제안.
  - 좌석 오프셋·가드 방향·보스 거리는 초기값이다. 정한 좌석의 오프셋을 M1 조종석 작업에서 프리팹 기본값으로 옮긴다.
  - 팔 IK(M1)도 스킨 메시를 bind 자세 밖으로 움직이므로 같은 컬링 깜빡임이 생긴다. `updateWhenOffscreen`(렌더러 291개라 CPU 비용 있음) 또는 bounds 확대를 메시 병합(A-02) 이후에 정할 것을 제안.
  - 실제 게임에서도 머리 조종석이면 자기 로봇 머리를 숨기거나 조종석 형상으로 바꿔야 한다. 이번 `hideWhileSeated`를 M1 조종석에서 그대로 쓸 수 있다.
  - Head 좌석 눈(57.5 m)은 어깨(52.4 m)보다 약 5 m 위다. `02_architecture.md`의 "눈이 어깨 근처라서 각도가 보존된다"는 전제와 차이가 있으므로, M1 팔 매핑에서 눈–어깨 높이차를 보정할지 확인이 필요하다.
  - Quest Link에서 왼손 메뉴 버튼은 앱으로 입력이 오지 않는 것으로 보인다(X 버튼만 동작). 개발용 좌석 전환은 X 버튼을 쓰고, 이후 메뉴 버튼을 게임 입력에 쓰지 않는 것을 제안.
  - 좌석 전환 액션(`Dev` 맵)은 개발용이다. 시연 빌드에서는 좌석 전환 코드가 컴파일되지 않는다.
