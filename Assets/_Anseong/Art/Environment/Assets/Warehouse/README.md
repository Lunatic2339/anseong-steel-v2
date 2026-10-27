# Warehouse — 파괴 가능한 공장 창고

배치할 에셋: Warehouse.prefab. Unity Project 창에서 이 파일을 씬으로 끌어 놓는다.
경로: Assets/_Anseong/Art/Environment/Assets/Warehouse/Warehouse.prefab

- Generated/: 조각 메시와 재질. Runtime/: 파괴 동작. Editor/: 생성/검증 도구.
- 원본 절차 생성 건물, 폭48m/깊이32m, 처마24m/지붕32m. 로봇 기준65m.
- Sandbox_RobotFall.unity에서 Space: 낙하, R: 초기화.
- 낙하 데모: 첫 접촉 후 약80%가0.46초 내 변형/분리, 나머지는 이어지는3초 동안 순차 파괴.
- 왼쪽 정면 패널3개는 내부로 말려들고, 인접 외벽은 변형/탈락함.
- 131개 부재 중39개 손상/92개 유지로 검증. 실제 게임 로봇 충돌 및 네트워크 연결은 별도.
- Unity 6000.3.26f1, URP, Input System. 헤드셋 성능 검증은 아직 하지 않음.
- Generated/Runtime 폴더와 모든 .meta 파일을 함께 유지한다.

E-01 / 윤주 / yunju 브랜치. 출처: 상위 Environment/SOURCES.md.
