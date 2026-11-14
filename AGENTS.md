# 안성제철 새 기반 작업 지침

- README.md와 docs/code-reuse-review.md부터 읽는다. 이 저장소는 아직 게임 프로젝트가 아닌 UPM 선별 패키지다.
- 기획 원본: C:/Users/anthony/Documents/Codex/2026-11-13/new-chat/outputs/START-HERE.md. 연결된 현재 기획·변경 이력·map-layout-options.md를 읽는다. docs/planning은 2026-11-14 사본이며 최신 원본과 비교한다.
- 3안(VR 메카+PC 드론),4안(VR 메카+PC 관제)을 동등하게 검토. 35m는 현재 작업 기준, 약10분은 선호 길이이며 절대 제한이 아니다.
- 사용자 확정과 어시스턴트 제안을 구분. 문서 저장뿐 아니라 채팅으로 실제 내용과 근거를 설명한다.
- ../anseong-steel-v2는 실제 기존 프로젝트 원본. 별도 변경 요청 없이 수정하지 않는다. 원격 게시/푸시를 추론하지 않는다.
- 소스 이전은 docs/source-manifest.json의 출처·원본 해시와 변경 이유를 추적한다. 이 패키지와 기존 Anseong.Runtime를 동시에 로드해 동일 타입을 중복시키지 않는다.
- 씬/프리팹/리그를 이전할 때에는 Unity에서 검증한다. 40개 기반 테스트 통과를 VR/네트워크/게임 출시 검증으로 확대하지 않는다.
- 기획을 수정하면 원본 outputs와 docs/planning 사본 및 SNAPSHOT.json을 함께 갱신한다.
