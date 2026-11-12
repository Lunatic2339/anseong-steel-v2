# SteelTruck — 배경용 철강 운송 트럭

배치할 파일: SteelTruck.prefab. 위치 Y=0, Scale=1. 앞 방향 +Z.
기본 단위는1m. 길이16.08m, 높이3.68m, 차체 폭2.55m, 사이드미러 포함 폭3.08m.
65m 로봇 대비 트럭 높이는 약1/18, 전체 길이는 로봇 키의 약1/4.
실제 특정 차종이나 도로 법규/중량 설계를 복제한 모델은 아닌 배경용 모델.

철강 코일3개를 실은 평판 트레일러. 회색 캐빈, 6축 바퀴, 코일 받침 및 간단한 고정띠.
단일 MeshFilter/MeshRenderer, 삼각형3228개, 공유 재질4개, 텍스처0개.
BoxCollider1개. Rigidbody·런타임 스크립트·파괴·주행·애니메이션 없음.
여러 대 배치할 때 같은 프리팹을 복제하면 모델/재질 자원을 공유함. 복제마다 별도 모델 파일을 만들지 않음.
밀집 배치의 실제 VR 성능은 배치 수와 전체 씬에 따라 별도 확인 필요.

SteelTruck.asset와 네 재질 및 .meta를 프리팹과 함께 유지.
Editor/는 제작 도구이며 빌드 실행 중에는 동작하지 않음.
시험 씬: Assets/_Anseong/Scenes/Sandbox/Sandbox_SteelTruck.unity.
생성 메뉴: Anseong/Art/Build Steel Truck.
Unity6000.3.26f1 / URP. 외부 모델·텍스처 사용 없이 직접 절차 생성.
