# 안성제철 — 새 기반 선별 저장소

2026-11-14 / 35m 전체3구역 맵 1차 배치

**현재 주 작업은 맵 설계·제작입니다.** Unity Hub에서 `Game` 폴더를 Unity6000.3.26f1로 열고 `Assets/Map35/Scenes/Steelworks35m.unity`를 선택하세요. 별도 실행 파일은 `Builds/Map35/AnseongMap35.exe`입니다. 건물9개·원경·메카/드론/보스 경로·관제6시점 포함16카메라를 비교하는 공간 배치 뷰어입니다. 최종 아트와 전체 게임 구현은 다음 단계입니다.

- [전체 맵 배치·역할별 화면·실행 방법](docs/planning/map-unity-build.md)
- [Unity 조감도](docs/validation/map35/view-00.png), [평면도](docs/validation/map35/view-01.png), [검사 범위](docs/validation/map35/README.md)
- 왼쪽 카메라 버튼 또는 Tab으로 시점 전환. R 동선, L 표식, F 관제 화각, V 예약 공간, P 판/코일 전후 상태.
- 전체 보기에서 우클릭+WASD, Q/E로 자유 관찰. Space는 경로 추적 표식이며 게임 보행이 아닙니다.
- `Anseong → Map35` 메뉴로 생성/빌드. 재생성은 생성 장면의 수동 편집을 덮어쓰므로 아트 작업은 복제 장면이나 생성 코드에 반영합니다.

## 보존한 Z1 협동판

**현재는 Z1 출격판 협동 검증 장면을 실행할 수 있습니다.** Unity Hub에서는 저장소 루트가 아니라 `Game` 폴더를 Unity6000.3.26f1로 여세요. Windows 실행 파일은 `Builds/Z1/AnseongZ1.exe`입니다. 기본 도형·임시 팔로 역할/협동을 검증하는 단계이며 최종 게임 외형은 아닙니다.

- [실행 방법·3안/4안 조작·검증 결과](docs/z1-coop-prototype.md)
- Unity 장면: `Game/Assets/Z1/Scenes/Z1Coop.unity`
- 결과: EditMode56/56, Windows 빌드, 3안·4안 각 두 프로세스 자동 협동 완료. 실제 VR 실기는 미검증.

- [기존 코드 검토와 재사용 결정](docs/code-reuse-review.md)
- [129개 파일 전체 분류](docs/code-inventory.csv)
- [기획 읽기 시작점 — 2026-11-14 사본](docs/planning/START-HERE.md)
- [검증 결과와 재현 방법](docs/validation/README.md)
- [가져온 파일 출처와 SHA-256](docs/source-manifest.json)

## 현재 포함한 코드

`Packages/com.anseong.foundation`: HeightFit, MaterialSlotNames, HandDeadzone, TrackingStateRules, CockpitClearZoneMath와 기존 테스트5파일. Unity6000.3.26f1의 별도 프로젝트에서 **40개 테스트 통과**. 원문 코드는 바꾸지 않았고 패키지/어셈블리만 분리했습니다.

기존 Unity 프로젝트는 옆의 `../anseong-steel-v2`에 그대로 있습니다. 코드 기준 커밋은 `16b077539572050e121aa31828fa4ef45622730e`입니다. 새 저장소에는 원격 저장소를 연결하지 않았습니다.

## 기획 원본

`C:/Users/anthony/Documents/Codex/2026-11-13/new-chat/outputs`가 현재 원본입니다. 이 저장소의 `docs/planning`은 날짜가 기록된 사본이며 `SNAPSHOT.json`으로 출처/해시를 확인합니다. 다음 기획 갱신 때 원본을 수정하고 사본을 함께 동기화합니다. 두 위치에서 독립적으로 다른 결정을 만들지 않습니다.

## 현재 우선순위 — 맵 설계·제작

사용자 정정에 따라 전체3구역 맵의 공간·동선·시야와 외형 제작을 최우선으로 둡니다. 기존 협동 검증판은 보존하고, 정식 IK/전투/네트워크 완성을 맵 착수의 선행 조건으로 두지 않습니다.

전체3구역 입체 배치를 만들었고, 다음 추천은 건물·설비·원경의 기본 외형 → Z1 대표 시점의 목표 시각 품질 → Z2·Z3 확장입니다.3안 드론 동선과4안 관제 시야를 항상 함께 확인합니다. [맵 제작 순서](docs/planning/map-layout-35m.md)를 읽으세요. 공간 배치 완료를 최종 아트·VR 성능·출시 품질 달성으로 취급하지 않습니다.
