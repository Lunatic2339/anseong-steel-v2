# Tetrapod — 해안 호안용 콘크리트 소파블록

Tetrapod.prefab: 단일 블록, Scale1, 바닥Y0. 4방향 테이퍼 다리.
Tetrapod_ShoreStrip.prefab: 약20m 길이에24개를 회전/중첩한 호안 배치 초안. +X가 바다 방향, 길이는Z방향.
기존 Sandbox_Yunju의 바다는 +X, 육지 경계X112.5m, 수면Y-3m.
외곽 호안 배치 시 이 기준으로 위치/높이를 조정할 것. 하역/접안 부두 전면은 비워둘 것.
현재 기본 맵을 변경하거나 전체 해안에 자동 배치하지 않았음.

단일 공유 메시, 회색/젖은 색 재질 공유, 텍스처0, Rigidbody/Collider/런타임 스크립트0.
플레이어가 밟는 용도가 아닌 바깥 배경용. 실제 구조/중량/방파제 공학을 검증한 모델 아님.
배치 묶음은 시각적 초안이므로 실제 파도 해석이나 시공 지침으로 사용하지 않음.
사진은 일반 형상 참고용, 외부 모델/텍스처 다운로드 없이 절차 생성.
용도 참고: https://westcountryconcreteproducts.co.uk/products/tetrapods/
미리보기: Sandbox_Tetrapod.unity. Unity6000.3.26f1/URP.
프리팹과 .asset/재질/.meta 파일을 함께 유지.

검증: 단일360삼각형, 외형3.86x3.27x3.86m, 배치묶음24개. 단일 원본 메시를 공유.
