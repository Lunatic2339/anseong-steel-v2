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
- 바뀐 파일:
- 사람이 확인할 것:
- 남은 문제 / 제안:
