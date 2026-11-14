# 안성제철 — 새 기반 선별 저장소

2026-11-14 / 기존 코드 검토 및 선택적 이전 단계

**현재는 플레이 가능한 게임 프로젝트가 아니라, 검토 문서와 검증된 기반 코드의 로컬 저장소입니다.** Unity Hub에서 이 폴더를 게임 프로젝트처럼 열지 않습니다. 기존 씬과 리그의 좌우 협동 가정을 새 프로젝트에 다시 끌고 오지 않도록 작은 UPM 패키지부터 분리했습니다.

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

## 다음 추천

3안(VR 메카+PC 드론)과4안(VR 메카+PC 관제)을 같은 깊이로 유지합니다. 먼저 양팔 VR/PC 지원 역할을 분리하고, 임시35m 리그와 출격판 하나에서 양쪽 입력·동기화·재시작을 확인합니다. 이후 Z2 갑각·Z3 크레인을 확장합니다. 기존 전체 프로젝트 빌드와 VR/두 기기 테스트는 아직 하지 않았습니다.
