# M1-02 조종사 입력과 개인 보정(기체 점검 최소판)

- 마일스톤: M1 / 담당: 선우 / 브랜치: `feat/sunwoo-pilot-input`
- 선행: 없음(M1-01과 동시에 해도 됨)
- 기한(제안): 10/21

## 목표
양손의 위치·회전·추적 상태·그립·트리거·스틱을 한 장치에서 묶어 읽고, 어깨 → 가드 → 도달 순서로 개인 보정값을 저장한다. 팔 IK(M1-03)와 펀치 판정(M1-04)이 이 값을 쓴다.

## 배경
- `docs/design/01_combat_rules.md` "입력", "공격"의 추적 유실 규칙.
- `docs/design/00_plan.md` "한 판의 흐름" 2번 기체 점검.
- `03_reuse_from_old_repo.md`:
  - XR 포즈 읽기 `ArmMotionInput.cs` 204-219 (같은 장치에서 묶어 읽기만 참고)
  - 개인 팔 도달 매핑 `ArmReachCalibration.cs` **`backup/local-1015` 버전** (`MapRadius` 69-103, `MapPractice`, `DistanceForBend` 64-67). 거의 그대로.
  - 보정 UX `RobotPilotInput.cs` 216-243 (어깨 → 가드 → 도달, 단계마다 그립 놓기)
  - 피할 버그 4(LAN에서 손 회전·보정 꺼짐), 7(이동 스틱이 펀치하는 손), 8(0.25초 끊김에 연계 초기화).
- M0-02 결과: `PilotInput.inputactions`는 `devicePosition`·`deviceRotation`·`trackingState`만 쓴다. 메뉴 버튼은 Link에서 앱으로 오지 않으므로 쓰지 않는다.

## Claude가 할 일
1. `PilotInput.inputactions`에 손별 `Grip`, `Trigger`, `Stick` 액션 추가.
2. `Runtime/Pilot/PilotHandInput.cs`: 손 하나의 자세·추적·그립·트리거·스틱을 같은 프레임에 읽어 구조체로 내놓는다. 담당 팔(`PilotSide`)과 이동 손(반대 손)을 구분한다. 1인 양팔 모드에서는 양손이 모두 담당 팔이고 스틱은 둘 다 이동 입력이다.
3. 순수 클래스 `AttackIntentGate`: 그립 유지 + 추적 유효일 때만 공격 의도. 추적이 끊겼다 돌아오면 그립을 한 번 놓기 전까지 무효. 짧은 끊김이 연계를 끊지 않도록 "무효"와 "연계 초기화"는 분리한다(연계는 M1-05).
4. 순수 클래스 `ArmCalibration`: 위 기존 로직을 새 구조로. 저장값 = 어깨 위치, 가드 위치, 도달 거리, 개인 팔 길이, **몸 정면 yaw**(고개를 돌려도 팔 정면 기준이 돌지 않게). 값은 그 판 동안만 메모리에 둔다(재시작하면 초기화).
5. `Runtime/Pilot/PilotCalibrationFlow.cs`: 어깨 → 가드 → 도달 단계, 단계마다 그립 놓기. 1인 양팔 모드는 양손을 같은 단계에서 동시에 보정한다. 진행 안내는 개발용으로 Console 로그 + 선택 시 기즈모(구 모양)만. 관객용 안내 화면은 M4.
6. EditMode 테스트: `AttackIntentGate`(유실 → 복구 → 그립 놓기 전까지 무효), `ArmCalibration`(기존 레포 `ArmMotionValidation.cs` Core() 40-92의 경우를 옮김).

## 사람이 할 일
- [ ] 샌드박스 씬에서 Play → 헤드셋으로 보정 3단계를 양손으로 해 본다.
- [ ] Console에 나온 보정값(어깨 높이, 팔 길이 등)이 실제 몸과 대략 맞는지 본다. 두 번 보정하면 값이 바뀐다.
- [ ] 고개를 옆으로 돌린 채 보정해도 몸 정면 yaw가 몸 쪽을 가리킨다.

## 하지 말 것
- 팔 IK, 펀치 분류, 관객용 안내 UI, 보정값 파일 저장.
- 이동(협동 이동은 M2).

## 완료 기준
- 헤드셋에서 양손 보정 3단계를 끝까지 할 수 있고, 보정값이 Console에 나온다.
- 테스트 통과.

## 결과 (작업 후 Claude가 작성)
- 바뀐 파일:
- 사람이 확인할 것:
- 남은 문제 / 제안:
