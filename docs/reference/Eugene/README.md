# Eugene — 이전 보스 최종 참고본

보관일: 2026-11-04 / 작업자: Eugene

[`BossSteelV13_Legacy/`](BossSteelV13_Legacy/)는 **이전 레포에서 진행했던 최종 외형·애니메이션 연결 상태의 참고본**이다. 최근 손 리깅 수정본과 외형 및 동작을 비교할 때 이전 기준으로 사용한다. 새 프로젝트에 통합 완료된 게임 에셋이라는 의미는 아니다.

## 출처와 보존 범위

- 이전 레포 위치: `AnseongSteel/Assets/_BossSteelV13/`
- 로컬 원본: `D:/CAPSTONE1/anseong-steel/AnseongSteel/Assets/_BossSteelV13/`
- 폴더 내부의 FBX, 메시, 텍스처, 재질, 프리팹, 테스트 씬, Editor 코드, README 및 `.meta`를 모두 그대로 복사했다.
- 총 27개 파일의 상대 경로와 SHA-256을 대조해 원본과 동일함을 확인했다. 설명은 이 상위 문서에만 추가했고, 보관 폴더 내부는 수정하지 않았다.

## 팀원이 볼 파일

- `BossSteelV13_Legacy/MechaSteelV13.fbx`: 이전 최종 모델.
- `BossSteelV13_Legacy/SteelV13_*.png`, `SteelV13.mat`: 최종 표면 텍스처와 재질 설정.
- `BossSteelV13_Legacy/BossSteelV13.prefab`, `BossSteelV13Test.unity`: 이전 보스 구성과 애니메이션 연결 상태를 확인할 기준.
- `BossSteelV13_Legacy/README.txt`: 당시 적용 및 확인 내용 원문.

## 애니메이션 참고 시 주의

이 폴더만으로 이전 테스트 씬을 독립 실행할 수는 없다. 애니메이션 클립·Animator Controller·스크립트 등은 이전 레포의 `_BossPrototype` 및 다른 에셋을 참조하며, 이번 복사에는 그 외부 의존성이 포함되지 않았다. 실제 이전 동작을 확인하려면 이전 프로젝트에서 `Assets/_BossSteelV13/BossSteelV13Test.unity`를 연다.

현재 위치는 Unity `Assets` 밖의 참고 자료 폴더이므로 새 프로젝트에서 자동 임포트되거나 실행되지 않는다. 새 게임에 적용할 때는 필요한 의존성과 기존 GUID 충돌을 검토해 별도로 통합한다.
