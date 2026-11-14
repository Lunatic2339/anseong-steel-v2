# 안성제철 — 새 기반 선별 저장소

2026-11-14 / 기존 코드 검토 및 선택적 이전 단계

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

## 다음 추천 (Z1 구현 이후)

3안(VR 메카+PC 드론)과4안(VR 메카+PC 관제)을 같은 깊이로 유지합니다. 구현한 작은 출격판 장면을 실제 두 사람이 확인하고 VR 도달·지원 시야·협동 인과를 검증합니다. 정식 팔 IK와 보정으로 임시 팔을 교체한 뒤 Z2 갑각·Z3 크레인을 확장하는 것을 추천합니다. 실제 HMD/두 물리 PC 테스트는 아직 하지 않았습니다.
