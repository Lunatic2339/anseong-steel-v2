# M1-03 로봇 팔 IK (1인 양팔)

- 마일스톤: M1 / 담당: 선우 / 브랜치: `feat/sunwoo-arm-ik`
- 선행: M1-01, M1-02 병합
- 기한(제안): 10/24

## 목표
조종사의 양손을 65m 로봇의 두 팔로 옮긴다. 대기 클립 위에 팔 IK를 덮어쓰고, 주먹은 고정 포즈. 중간발표의 첫 장면("로봇 팔이 내 손을 따라옴")이 이것이다.

## 배경
- `docs/design/02_architecture.md` "시점과 스케일"(팔 매핑 공식, 눈–어깨 높이차, 쇄골 뼈 없음), "플레이어 로봇 몸", "팔 IK 요구사항".
- `03_reuse_from_old_repo.md`:
  - **그대로 가져가도 됨**: `VR_test/Assets/Scripts/MechaArmStability.cs` (SoftDistance, StableHint)
  - 팔 IK `ControllerArmFollower.Practice.cs` — **`backup/local-1015`에만 있음** (bind-frame 재구성 41-42·136-137, 내부구 94-105, swivel 제한·히스테리시스 185-193, 손목 156-165). 이 방식을 기본 solver로.
  - 팔꿈치 힌트 `ControllerElbowHint` 209-228, `RigidWristConstraint.cs` 13-28
  - 주먹 포즈 `DefaultFistPose.cs`, 그립 회전·knuckle 오프셋 `MechaVRHandTarget.cs` 121-187
  - 피할 버그 9(상위 팔 회전 누적 → roll 드리프트).

## Claude가 할 일
1. `Runtime/Core/`에 `MechaArmStability` 이식(네임스페이스만 바꿈).
2. 순수 클래스 `TwoBoneArmSolver`: 해석적 2본 IK, 매 프레임 바인드 포즈 기준 절대 재구성, 도달 밖은 지수형 수렴, 너무 가까우면 내부구, swivel은 컨트롤러 회전에서 연속 각도로 고르고 범위 제한 + 히스테리시스. 쇄골 뼈 없이 상완부터 푼다.
3. 순수 클래스 `ArmMapping`: `로봇 주먹 목표 = 로봇 어깨 + (손 − 보정한 사람 어깨) × (로봇 팔 길이 ÷ 개인 팔 길이)`. 몸 정면 yaw 기준(M1-02). **어깨 높이 보정값**(눈–어깨 높이차 보정)을 Inspector에 노출, 기본 0.
4. `Runtime/Pilot/RobotArmDriver.cs`: Animator 평가 뒤 `LateUpdate`에서 IK 적용. 손목은 그립 중 전완에 고정, 그립을 놓으면 제한된 회전. 손가락은 주먹 고정. 뼈 참조는 Inspector, 채우는 일은 에디터 메뉴(Mixamo 이름 탐색은 에디터 쪽).
5. 팔 반응(가속·감속) 값을 Inspector에 노출하되 기본은 즉시 따라옴. 카메라는 절대 지연시키지 않는다.
6. `Robot_Player.prefab` 메뉴(M1-01)에 두 팔 드라이버를 붙이도록 갱신. 1인 양팔 모드 = 왼손→왼팔, 오른손→오른팔.
7. EditMode 테스트: 도달 한계, 내부구, 팔꿈치 180도 뒤집힘 없음(손을 머리 위·몸 뒤로 지나가는 궤적), 2분 원 그리기 시뮬레이션 후 roll 드리프트 0, 기존 `ArmMotionValidation.cs` 경우.

## 사람이 할 일 (헤드셋)
- [ ] 양팔이 손을 따라온다. 지연이 느껴지면 적는다.
- [ ] 손을 머리 위, 몸 뒤, 반대쪽 어깨 쪽으로 보낼 때 어깨·팔꿈치가 휙 뒤집히지 않는다.
- [ ] 2분 동안 크게 원을 그린 뒤에도 팔이 비틀려 있지 않다.
- [ ] 팔을 끝까지 뻗으면 로봇 팔도 부드럽게 펴지고 덜컹거리지 않는다.
- [ ] 가드 자세에서 주먹이 보스를 가리지 않는다. 어색하면 어깨 높이 보정값을 바꿔 보고 고른 값을 결과에 적는다.
- [ ] 그립을 쥐면 손목이 고정되고, 놓으면 조금 꺾인다.

## 하지 말 것
- 펀치 분류·명중, 가드 판정, 네트워크 전송. 몸통 휘청.

## 완료 기준
- 헤드셋에서 위 확인 항목을 모두 통과(사람이 확인한 것만).
- 테스트 통과, 90fps 근처에서 끊김이 없다(대략).

## 결과 (작업 후 Claude가 작성)
- 작업 브랜치: `claude/m1-cockpit-input-x0vygh` (PR #13 병합 뒤 최신 main에서 다시 시작).
- 정한 것(지시서에 없던 부분):
  - **매핑은 두 가지를 넣고 Inspector에서 고른다(선우, 10/17).** 기본 `Linear` = 지시서 공식(로봇 어깨 + (손 − 사람 어깨) × 로봇 팔 ÷ 개인 팔). 사람 가드 위치가 그대로 커진다. `GuardMatched` = M1-02 `ArmCalibration.Map`(사람 가드 → 정해 둔 로봇 가드, 뻗을수록 로봇 정면). 어깨 높이 보정은 `Linear`에서만 쓴다.
  - `Linear`는 개인 팔 길이의 85%까지 거의 그대로 비례하고, 끝까지 뻗은 손(개인 팔 길이)은 로봇 팔이 3° 굽은 거리, 더 뻗으면 2° 굽은 거리로 지수형 수렴한다(M1-02 `MapRadius` 재사용). 솔버도 3° 굽힘부터 2°까지 한 번 더 완화해서 어떤 목표든 팔이 일직선 특이점에 닿지 않는다.
  - **주먹 회전 기준 = 도달 보정 때의 손 회전(펀치 자세, 손등 위·엄지 안쪽).** 그 자세가 "로봇 주먹이 정면으로 뻗고 손등이 위"이고, 그 뒤로 돌린 만큼 주먹도 돈다. 컨트롤러 축 가정이 없다. 보정 안내 문구도 "펀치 자세로 뻗기"로 바꿨다.
  - 그립 = 손목 고정. 놓으면 손목이 최대 60° 꺾이고 30° 비틀린다(기존 Practice 값). 바뀔 때 튀지 않게 약 0.17초에 걸쳐 바뀐다.
  - 보정이 끝나기 전에는 팔을 건드리지 않는다(대기 클립 그대로, 손가락만 주먹). 추적이 끊기면 마지막 자세에 멈춘다.
  - 기존 `ControllerElbowHint`(훅 궤적이면 팔꿈치를 옆으로 들어 올림)를 `ElbowLiftHint`로 옮겨 기본으로 켰다. Inspector에서 끌 수 있다.
  - 솔버의 속도 제한(swivel 180°/초, 아래팔 비틀림 180°/초, 굽힘면 90°/초)은 기존 Practice 값 그대로다. 어깨 → 손 방향 제한만 180°에서 720°/초로 올렸다(펀치 지연 방지, 어깨 근처 뒤집힘 방지용으로만 남김).
  - `MechaArmStability.StableHint`는 옮겼지만 쓰지 않는다(Practice 방식의 swivel 선택이 그 역할을 한다). `SoftDistance`에 시작·한계 비율을 받는 판을 추가했다.
  - **허리 회전 추가(선우, 10/17, 1차 헤드셋 확인 뒤).** 지시서 "하지 말 것"의 몸통 휘청과는 별개로, 허리를 써서 감았다 치는 동작을 넣기로 했다. 허리 추적 장치가 없어 1인 양팔 모드에서 두 손 선이 가드 때보다 돈 각도 × 0.5 + 고개 yaw × 0.2로 어림잡고(최대 ±30°, 0.06초 따라감), 로봇 `Spine`을 루트 위쪽 축으로 돌린다. 오른손을 뒤로 빼면 오른쪽으로 감기고, 오른손을 뻗으면 왼쪽으로 돈다. 팔 매핑은 사람 어깨도 같은 각도로 돈 것으로 보고 잰다(허리를 돌리며 뻗은 손이 로봇에서도 같은 자세). 2인 모드는 두 조종사 손이 다른 PC에 있어 끄고 M3에서 정한다. 조종석은 머리 뼈를 따라가므로 허리를 돌리면 시점도 옮겨진다(멀미 확인 항목). **취사선택용(선우)**: Inspector `Twist Enabled`를 끄면 허리 회전 전과 같은 동작이고, 코드째 빼려면 `git revert 75ab51a`(허리 회전은 이 커밋 하나에만 있다).
- 바뀐 파일:
  - `Runtime/Core/MechaArmStability.cs` — 기존 레포에서 옮김(네임스페이스 `Anseong.Core`, 비율 지정 `SoftDistance` 추가).
  - `Runtime/Pilot/TwoBoneArmSolver.cs` — `ArmBind`(바인드 포즈 기준값), `ArmPose`, `ArmSolverSettings`(Inspector 수치), 해석적 2본 IK. 기존 `ControllerArmFollower.Practice.cs` 방식을 Transform 없이 다시 씀: 바인드 기준 절대 재구성(41-42·136-137), 내부구(94-105), swivel ±105° + 히스테리시스(185-193), 손목(156-165).
  - `Runtime/Pilot/ArmMapping.cs` — `ArmMappingMode`, `TryLinear`, `TryGuardMatched`, `FistLocal`, `GuardDelta`, `ElbowLiftHint`.
  - `Runtime/Pilot/RobotArmDriver.cs` — 팔마다 하나. 실행 순서 100(Animator 뒤, 조종석 전) LateUpdate에서 위팔·아래팔·손 회전을 덮어쓰고 손가락을 주먹으로 고정. 매핑 방식, 어깨 높이 보정(기본 0), GuardMatched 로봇 가드 위치, 팔 반응(`responseSeconds`·`maxTargetSpeed`, 기본 0 = 즉시), 솔버 수치를 Inspector에 노출. IK 시작 때 `[RobotArmDriver]` 로그(팔 길이·배율, 개발 빌드만).
  - `Runtime/Pilot/PilotCalibrationFlow.cs` — 도달 단계 안내 문구만 "펀치 자세(손등 위, 엄지 안쪽)로".
  - `Runtime/Pilot/TorsoTwist.cs` — 허리 yaw 추정(`EstimateYaw`), 고개 yaw(`HeadYawFromBody`), 허리 회전에 따른 사람 어깨 이동(`ShoulderShift`). `TorsoTwistSettings`(Inspector 수치).
  - `Runtime/Pilot/TorsoTwistDriver.cs` — 실행 순서 90(팔 드라이버 전) LateUpdate에서 `Spine` yaw를 덮어쓴다. 클립이 허리를 안 써도 회전이 쌓이지 않게 원래 값을 기억한다. `Robot_Player > Arms`에 붙고, 끄기는 `Twist Enabled`.
  - `Editor/RobotArmSetup.cs` — 두 팔 드라이버 생성, Mixamo 뼈 이름 탐색, 기존 `DefaultFistPose.Configure`를 옮긴 주먹 포즈 계산(계산 뒤 손가락은 원래 회전으로 되돌려 프리팹 바인드 포즈를 바꾸지 않음), 씬의 드라이버에 입력·보정 연결.
  - `Editor/RobotPrefabMenu.cs` — Build Player Robot Prefab이 `Robot_Player` 아래 `Arms`에 왼팔·오른팔 드라이버를 붙인다.
  - `Editor/SceneSetupMenu.cs` — Add Pilot Input이 씬의 팔 드라이버에도 입력·보정을 연결한다.
  - 테스트: `TorsoTwistTests`(7: 가드에서 0, 뒤로 뺀 손 쪽으로 감김·좌우 대칭, 고개 비율·한계, 미보정, 고개 yaw 부호, 어깨 이동, 허리 보정 매핑), `TwoBoneArmSolverTests`(11: 목표 도달·뼈 방향, 왼팔 팔꿈치 바깥 아래, 도달 밖 단조 수렴, SoftDistance 연속, 내부구, 손목 고정·풀림 한계, 머리 위·몸 뒤·반대쪽 어깨 궤적에서 뒤집힘 없음, 2분 원 그리기 뒤 roll 드리프트 0, 몸 회전 불변, swivel 히스테리시스), `ArmMappingTests`(10: 선형 배율·방향, 끝까지 뻗음, 단조, 어깨 높이 보정, 몸 정면 yaw, 미보정, 가드 맞춤, 주먹 회전 기준, 기존 `ArmMotionValidation` 팔꿈치 힌트 2경우).
- Claude가 확인한 것: Unity가 없어 **컴파일·Test Runner는 못 돌렸다.** 순수 클래스와 테스트(기존 포함 63개)는 Unity 수학 스텁으로 따로 컴파일해 모두 통과했다. 위팔 회전을 누적 방식으로 바꾸면 roll 드리프트 테스트가 실패하는 것도 확인했다(127°). `RobotArmDriver`·에디터 메뉴는 Unity에서 처음 컴파일된다.
- 사람이 확인할 것:
  - [ ] Unity Console에 `error CS` 없음, 새 `.cs` `.meta` 커밋, EditMode 테스트 전체 통과.
  - [ ] **Anseong → Setup → Build Player Robot Prefab** → Console에 `[RobotPrefabMenu] 팔 드라이버: Left 팔: 위팔 … / Right 팔: …` 로그, `[RobotArmSetup]` 경고 없음(손바닥 판별 경고가 나오면 알려 주기).
  - [ ] `Sandbox_Pilot`을 열고 **Anseong → Setup → Add Pilot Input** → "팔 드라이버 2개에 입력을 연결" 로그 → 씬 저장.
  - [ ] Play → 보정 3단계(도달은 펀치 자세) → `[RobotArmDriver] Left/Right 팔 IK 시작` 로그(배율 숫자 확인). 아래는 헤드셋에서:
  - [ ] 양팔이 손을 따라온다. 지연이 느껴지면 적는다.
  - [ ] 손을 머리 위, 몸 뒤, 반대쪽 어깨 쪽으로 보낼 때 어깨·팔꿈치가 휙 뒤집히지 않는다.
  - [ ] 2분 동안 크게 원을 그린 뒤에도 팔이 비틀려 있지 않다.
  - [ ] 팔을 끝까지 뻗으면 로봇 팔도 부드럽게 펴지고 덜컹거리지 않는다.
  - [ ] 가드 자세에서 주먹이 보스를 가리지 않는다. 어색하면 `Arms`의 드라이버 두 개에서 **Shoulder Height Offset**(양수 = 주먹이 내려감)을 바꿔 보고 고른 값을 여기 적는다. **Mapping Mode**를 GuardMatched로 바꿔 비교해 본다.
  - [ ] 그립을 쥐면 손목이 고정되고, 놓으면 조금 꺾인다.
  - [ ] 주먹 손가락이 손바닥 쪽으로 접혀 있다(손등 쪽이면 손바닥 판별 문제).
  - [ ] 대기 클립의 손목 180° 뒤집힘이 IK가 켜진 뒤에는 없다.
  - [ ] 오른손을 뒤로 뺐다 뻗으면 로봇 허리가 오른쪽으로 감겼다 왼쪽으로 돈다(왼손은 반대). 너무 크거나 작으면 `Torso Twist Driver`의 Hand Weight·Max Degrees를 바꿔 보고 고른 값을 적는다.
  - [ ] 허리가 돌 때 시점이 같이 옮겨져 어지럽지 않은지. 어지러우면 Head Weight를 0으로, 그래도 어지러우면 Twist Enabled를 끄고 적는다.
- 1차 헤드셋 확인 (선우, 2026-10-17): 첫 Play에서 팔이 움직이지 않음 → 드라이버의 Input·Calibration 연결이 비어 있었음. 비어 있으면 시작할 때 씬에서 한 번 찾고 못 찾으면 경고하도록 수정. 그 뒤 양팔이 손을 따라 잘 움직임. 어색한 점: ① 허리를 써서 팔을 뒤로 뺐다 치는 동작이 로봇에 안 들어감(몸통은 대기 클립 그대로) ② 위팔이 많이 돌 때 어깨 장갑이 몸통을 많이 파고듦. 어깨 장갑이 위팔 뼈와 같이 도는 것으로 보임(선우). 쇄골 뼈가 없어 코드로는 못 막으므로 **A-02에서 어깨 장갑 웨이트를 Spine 위주(위팔은 조금)로** 다시 칠하자고 총괄에 전달.
- 2차 헤드셋 확인 (선우, 2026-10-17): 허리 회전이 들어감(아주 살짝, 세기는 Hand Weight·Max Degrees로 조정). 주먹 방향과 끝까지 뻗기는 괜찮음. **머리 위·몸 뒤에서 팔 뒤집힘, 2분 원 그리기 꼬임은 확인 못 함** → 선우 결정으로 게임플레이 테스트(M1-04 이후) 중에 보고 고친다.
- 남은 문제 / 제안:
  - 팔 IK는 손 입력을 Update에서 읽고, 카메라는 렌더 직전 자세를 쓴다. 지연이 느껴지면 손 입력도 렌더 직전에 다시 읽는 방법을 검토한다.
  - 기존 `ArmMotionValidation`의 펀치 분류 경우(잽·훅·대각선)는 M1-04에서 옮긴다.
  - `Prefabs/` 폴더가 CLAUDE.md 폴더 표에 없다(M1-01부터). 표에 넣을지 정할 것.
  - 로봇 고개가 조종사 고개를 따라 도는 기능(제안, 선우 10/17 보류): 머리 뼈만 돌리고 조종석 방향은 그대로 둬야 한다(두 번 돌면 멀미). 2인 모드에서 누구 고개를 따를지 정해지지 않아 M3에서 함께 정한다. 넣을 때는 토글로 두고, 끄면 지금과 같아야 한다.
