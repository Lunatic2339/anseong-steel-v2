# HarborCrane — 철강 화물용 항만 크레인

HarborCrane.prefab: 레일식 포털 구조와 격자 붐의 일반 화물 항만 크레인 초안.
65m 로봇 기준 높이 약41m. +X바다/붐 방향, Z레일 방향, Y0바닥, Scale1.
기존 안벽의 폭10m 작업면 안에 폭 약8m 하부를 배치하는 구성. 모듈 중심은 안벽 모서리에서 육지쪽으로 약5m.
전면 붐 돌출 약26m, 후크는 지상 약10m. 특정 장비/정격하중/구조 설계를 복제·검증한 모델 아님.

높은 운전실, 기계실, 후방 무게추, 지지탑, 격자 붐, 와이어와 후크, 난간/점검사다리 포함.
단일 공유 메시·재질4개·텍스처0개. 상자 충돌체4개는 하부 기둥용 근사체.
Rigidbody/런타임 스크립트/회전/하역 애니메이션/파괴 없음. 여러 대 배치하는 배경용 고정 모델.
기둥 위 통로와 붐은 캐릭터가 올라가는 용도가 아니며 충돌체 없음.
실제 VR 성능은 전체 씬에서 확인 필요. .asset/재질/.meta를 함께 유지.
미리보기: Sandbox_HarborCrane.unity. Unity6000.3.26f1/URP. 외부 모델/텍스처 없이 절차 생성.

Revision 2: thicker portal legs and boom chords, reinforced joints, wheel bogies, window frames, machinery louvres and panels, counterweight slabs, maintenance stairs and walkway, sheaves and hook safety stripes. 3516 triangles, one shared mesh and four materials. 256px procedural paint weathering map. Height 40.95m; bounds 35.42 x 40.95 x 9.40m. Static background prop, no operation/destruction. Texture UVs overlap; detailed unique Painter texturing requires UV preparation.
