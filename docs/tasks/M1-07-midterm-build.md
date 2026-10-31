# M1-07 중간발표 빌드 통합

- 마일스톤: M1 → 중간발표(10/30) / 담당: 선우 / 브랜치: `feat/m1-07-midterm-build`
- 선행: M1-05, M1-06 병합. 들어오면 함께 넣을 것: E-01 1차(`Env_Steelworks.prefab`, 10/28), S-01 1차 소리(10/28)
- 기한(제안): 10/29 (영상은 10/29 밤까지)

## 목표
`Main.unity`에 M1 결과를 모아 1인 양팔 모드로 처음부터 끝까지 돌리고, 중간발표 시연 영상을 찍는다.

## 배경
- `docs/design/00_plan.md` "중간발표 시연 범위": 로봇 팔이 손을 따라옴, 잽·훅 → 보스 HP·경직·효과음, 가드 표시, 제철소 1차, 임시 색 구분.
- 빼는 것: 2인 네트워크, 보스 공격, 필살기, 출격 연출.

## Claude가 할 일
1. 에디터 메뉴 **Anseong → Setup → Build Midterm Main**: `Main.unity`에 `Robot_Player`, `Robot_Boss`(40m 앞), XR Origin 연결, `Env_Steelworks`(없으면 바닥판), 조명. 씬은 사람이 저장.
2. 흐름: 시작 → 보정(M1-02) → 전투(보스 항상 빈틈) → 운영자 키보드 `R`로 재시작(HP·연계·보정 모두 초기화. 이전 판 상태가 남지 않음).
3. S-01 1차 소리를 `Audio/SFX/`에 넣고 M1-05 소리 자리에 연결(받은 파일 기준, 없는 것은 무음). 출처는 `Audio/SOURCES.md`.
4. Windows 빌드 설정 확인(PC Standalone, OpenXR). 빌드는 사람이 한다.
5. 시연 순서 체크리스트(1분 30초 안팎)를 결과 칸에 적는다: 보정 → 팔 흔들기 → 가드 표시 → 잽·훅 → 잽–잽–훅–부스트 → 재시작.

## 사람이 할 일
- [ ] 메뉴 실행 → `Main.unity` 저장 → 에디터 Play와 빌드 실행 둘 다 확인.
- [ ] 시연 순서를 3번 연속 돌려도 끊김·오류가 없다.
- [ ] PC 모니터 미러 화면을 녹화해 시연 영상 1~2분 확보. 녹화 도구를 정한다.
- [ ] 영상을 유진(P-01 발표 자료)에게 팀 단톡방으로 넘긴다.

## 하지 말 것
- 새 기능. 버그 수정 외의 리팩터링. 네트워크.

## 완료 기준
- `Main.unity`에서 1인 양팔 모드로 보정부터 재시작까지 3번 연속 돈다(헤드셋 확인).
- 중간발표 영상이 있다.

## 결과 (작업 후 Claude가 작성)
- 바뀐 범위(선우 10/31): 중간발표가 지나 영상은 뺐다. 브랜치 `feat/m1-07-main-integration`. 맵은 윤주 `Sandbox_ReferenceLayout`을 넣는다(선우 10/31 "맵넣어봐"). 소리(S-01)는 아직 레포에 없어 무음.
- 바뀐 파일:
  - `Editor/MainSceneMenu.cs`(새): Anseong → Setup → **Build M1 Main**. 여러 번 돌려도 된다(있는 것은 두고 연결만 다시).
  - `Runtime/Flow/EnvironmentLoader.cs`(새): Play 때 맵 씬을 additive로 불러오고, 맵의 카메라 3개·오디오 리스너를 끄고, 맵 스크립트(부서지는 건물 조각 약 4700개가 매 프레임 돎)를 멈추고, 맵에 해가 있으면 Main 해를 끈다. 모두 토글.
  - `Runtime/Flow/SessionRestart.cs`(새): 키보드 R → 보스 HP·연계·부스트 초기화 + 보정 처음부터.
  - `Editor/EyeHeightSandboxMenu.cs`: Attach XR Origin을 Main 메뉴에서도 부를 수 있게. `Editor/CombatSetup.cs`: 보스 근접 거리 당기기를 Main에서도.
  - `Art/Environment/**`, `Scenes/Sandbox/Sandbox_ReferenceLayout.unity`: `yunju` 브랜치(10/28 c5ce880)에서 맵 씬이 참조하는 파일만 679개(약 59MB). 뺀 것: `Mobile_RPAsset` 설정 변경, 데모 스크립트(FactoryDemo·SmokestackDemo·RobotFallDemo), 에셋별 빌더·샌드박스 씬, 로봇 원본 FBX(develop에 이미 같은 GUID로 있음).
- 사람이 확인할 것:
  - [ ] `git pull` → Unity 컴파일 오류 없음(새 `.cs`의 `.meta`, `Runtime/Flow` 폴더 `.meta`는 Unity가 만듦 → 선우가 커밋).
  - [ ] **Anseong → Setup → Build M1 Main** → Ctrl+S. Console 마지막 줄에 맵·재시작·씬 목록이 나옴.
  - [ ] Play: 맵이 보이고 화면이 맵 카메라로 바뀌지 않음, Console `[EnvironmentLoader] … 불러옴`.
  - [ ] 헤드셋 프레임이 버팀(맵 건물 2279개 인스턴스). 무거우면 알려 주기.
  - [ ] 보정 → 팔 흔들기 → 가드 표시등 → 잽·훅 → 잽–잽–훅–부스트 → R → 다시 보정부터. 3번 연속.
- 시연 순서(1분 30초 안팎): 보정(정면 뻗기 → 가드) → 팔 흔들기 → 가드 올려 표시등 → 잽·훅 → 잽–잽–훅–부스트로 HP 깎기 → R 재시작.
- 남은 문제 / 제안:
  - 맵 스크립트는 네임스페이스가 `CoastalFactory`(규칙 `Anseong.<폴더>` 아님)이고 asmdef가 따로 있다. 부서짐(M5)에서 쓸 때 정리.
  - 맵 조각의 충돌 콜백은 스크립트를 꺼도 온다. 로봇이 맵 건물(150 m 밖)에 닿을 일이 M1에는 없지만 M2 이동 때 확인.
  - 맵을 고치면 윤주 브랜치 → 같은 방식으로 다시 가져온다. 맵 씬 이름이 바뀌면 `MainSceneMenu.EnvironmentScenePath` 한 줄.
  - Windows 빌드 설정(PC Standalone, OpenXR)은 레포 ProjectSettings 기준으로 확인 필요(선우).
