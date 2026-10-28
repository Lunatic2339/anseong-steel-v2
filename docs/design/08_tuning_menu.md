# 08 인게임 조절 메뉴: 뺀 항목과 되살리는 법

헤드셋 조절 메뉴(오른손 B, `UI/TuningMenu`)에는 **선우가 지금 눈으로 보고 정할 값만** 둔다(선우 10/25: 메뉴 칸이 길면 헷갈림).
뺀 항목도 **값은 Inspector에 그대로** 있고, 코드는 git에 남아 있다. "○○ 다시 조정하고 싶다"고 하면 아래 표의 줄을 그대로 되살린다.

## 지금 메뉴에 있는 것 (10/25)

| 묶음 | 컴포넌트 | 항목 |
|---|---|---|
| 묵직함 | `Pilot/RobotArmDriver` | 뻗은 팔은 팔꿈치 고정, 작은 손 떨림 무시(켜기, 크기 cm), 따라가는 방식(경로 가속/부드럽게), 경로 가속 출발 속도·가속도, 부드럽게: 팔 반응 시간, 전완 돌아가는 속도(도/초) |
| 허리 | `Pilot/TorsoTwistDriver` | 허리 회전, 머리·조종석은 정면 유지, 손 비율, 최대 각도 |
| 조종석 흔들림 | `Pilot/CockpitSeat` | 붙이는 방식(완전 고정/좌우 회전만/위치만), 보정 때 정면·가운데 맞춤, 위치만: 따라가는 비율 |
| 조종석 비우기 | `Pilot/CockpitClearZone` | 조종석 안 로봇 몸 숨김(켜기), 비울 폭·높이·앞뒤 길이(m) |
| 보정 | `Pilot/PilotCalibrationFlow` | 보정 순서(2단계: 정면 뻗기→가드 / 3단계 이전), 보정 뒤 정면 잽으로 맞춤 |
| 공격 보정 | `Combat/CombatDirector` | [확인] 2개, 공격 보정 켜기, 보정 방식(판정만/끌어당기기), 허용 거리(잽·어퍼컷 / 훅), 끌려가는 시간 |
| 연계·부스트 | `Combat/CombatDirector` | 다음 명중 제한, 부스트 입력창 |
| 가드 (06 브랜치) | `Combat/GuardDriver` | 06 지시서대로 |

목록과 선택지는 끝에서 멈춘다(반대쪽으로 넘어가지 않음, 선우 10/25).

## 뺀 것

| 묶음 | 뺀 항목 (키) | 컴포넌트 | 뺀 커밋 |
|---|---|---|---|
| 몸 이동 | [확인] 3개, 몸 이동 보정 `body.enabled`, 높이도 따라감 `body.followHeight` | `Pilot/PilotCalibrationFlow` (ITunable째) | `2d6f957` |
| 그립 | [확인] `input.grip` | `Pilot/PilotHandInput` (ITunable째) | `2d6f957` |
| 펀치 인식 | [확인] 6개, 다음 펀치 준비 방식 `punch.rearmMode` | `Combat/PunchClassifierDriver` (ITunable째) | `2d6f957` |
| 묵직함 | 판정을 로봇 주먹에 맞춤 `punch.waitForRobotFist` | `Combat/PunchClassifierDriver` | `2d6f957` |
| 묵직함 | 주먹 최대 속도 `arm.maxTargetSpeed`, [확인] 4개 | `Pilot/RobotArmDriver` | `2d6f957`, `3d5a3e3` |
| 손목 | [확인] 2개, 손목 고정 `arm.wristLockMode`, 고정 손목 꺾임 `arm.lockedWristPitch` | `Pilot/RobotArmDriver` | `2d6f957` |
| 팔 매핑 | [확인] 2개, 매핑 방식 `arm.mappingMode`, 어깨 높이 보정 `arm.shoulderHeightOffset` | `Pilot/RobotArmDriver` | `3d5a3e3` |
| 허리 | [확인] 2개 | `Pilot/TorsoTwistDriver` | `3d5a3e3` |
| 조종석 흔들림 | [확인] 3개 | `Pilot/CockpitSeat` | `3d5a3e3` |
| 연계·부스트 | [확인] 3개 | `Combat/CombatDirector` | `3d5a3e3` |
| 보스 HP | [확인] 2개, 보스 HP 다시 채우기 `boss.reset` | `Combat/CombatDirector` | `3d5a3e3` |
| 소리·진동 | [확인] 2개, 컨트롤러 진동 `cue.haptics` | `Combat/CombatCuePlayer` (ITunable째) | `3d5a3e3` |
| 보스 반응 | [확인] 2개, 대기로 돌아오는 섞기 `boss.fadeOut`, 휘청 길이 `boss.hitStagger` | `Boss/BossReactionDriver` (ITunable째) | `3d5a3e3` |
| 펀치 인식 기준값 | 이동 거리·속도·호/현 같은 인식 임계값 | `Combat/PunchClassifierDriver` | M1-04(PR #22). CSV로 Claude가 맞추는 값이라 되살리지 않는 것이 원칙 |

## 되살리는 법 (Claude 세션용)

1. 뺀 커밋의 바로 앞 코드를 본다: `git show <커밋>^:Assets/_Anseong/Runtime/<컴포넌트>.cs` 에서 `AddTuning` 부분.
2. 그 `list.Add…` 줄을 지금 `AddTuning`에 다시 넣는다. "(ITunable째)"인 컴포넌트는 `class … : MonoBehaviour, ITunable`과 `AddTuning` 메서드도 되살린다.
3. **키는 예전 그대로** 쓴다. 그러면 `tuning.txt`에 남아 있던 예전 값이 다시 적용된다(원치 않으면 메뉴에서 A로 처음 값).
4. 이 표에서 그 줄을 "지금 메뉴에 있는 것"으로 옮긴다.
