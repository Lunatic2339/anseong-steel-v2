# A01 고로 지원 구역

`PlantBlock_A01_Ironmaking.prefab`을 Unity Scene으로 드래그해서 배치합니다.
작업동과 고로는 부지의 가로·세로 축에 맞췄고, 차량 진입로는 곡선입니다.
이 변경은 통합맵에 배치하지 않은 재사용 프리팹입니다.

- 작업동: `A01_Workshop_Painter.png`, `A01_Workshop_PainterUV.asset`
- 고로: `A01_Furnace_Painter.png`, `A01_Furnace_PainterUV.asset`
- 바닥: `A01_Concrete_Overhead.png`

Painter에서 내보낸 두 텍스처는 RGB에 색, 알파에 광택을 담습니다.
각 텍스처는 해당 UV 메시 및 재질과 함께 사용합니다. Unity에서 프리팹을 사용할 때 Painter 설치나 별도 텍스처 연결이 필요하지 않습니다.
