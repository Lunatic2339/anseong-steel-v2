# Smokestack — 산업용 배기 굴뚝

- Smokestack.prefab: 높이80m 단일 굴뚝. 붉은 하부 받침, 회색 테이퍼 중공관, 녹슨 이음부.
- Smokestack_Group.prefab: 높이70/80/90m 3기 묶음. 각 기둥은 개별 파괴 가능.
- 외부 모델을 복사하지 않고 참고 이미지의 일반적인 형상을 절차 생성한 초안.
- 파괴: ApplyImpact(월드 충돌점, 월드 힘 방향). 맞은 높이의 이음부에서 한 번 끊어지고, 위쪽 전체는 단일 복합 Rigidbody로 묶여 한 덩어리로 낙하하며 하부는 유지.
- 모델은 내부적으로6개 구간이지만 파괴 때 위쪽을 하나의 물리 덩어리로 합쳐 두 동강만 나게 함. 분리 위치는 기존 이음부 중 선택하며 임의 위치에 구멍을 뚫거나 철판을 찢는 방식은 아님.
- 가동 중 상단의 옅은 배기 효과. 파손 때 회갈색 먼지와 파손부 잔류 배기가스 약3초. 물 분출은 없음.
- 운영 여부 operating을 Inspector에서 끄면 정상 배기와 파손 배기 누출 없이 먼지만 남음.
- 굴뚝에는 보통 배기가스가 흐르며 수분을 포함할 수 있음. 가시 연기의 색/농도는 실제 연결 설비와 연료에 따라 다름. 본 에셋은 일반 배기 굴뚝으로 가정한 시각 연출.
- 출처 근거: https://www.epa.gov/stationary-sources-air-pollution/industrial-commercial-and-institutional-boilers-and-process-0
- 시험 씬: Assets/_Anseong/Scenes/Sandbox/Sandbox_Smokestack.unity. Play 후 Space: 단일 굴뚝 파괴, R: 초기화.
- 메뉴 Anseong/Art/Build Smokestack Asset: 생성. Anseong/Sandbox/Validate Smokestack: 재생 검증.
- Unity6000.3.26f1, URP, Input System. 실제 로봇/네트워크 이벤트 통합과 헤드셋 성능 확인은 별도.
- Generated, Runtime 및 모든 .meta 파일을 프리팹과 함께 유지.

