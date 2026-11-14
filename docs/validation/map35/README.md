# 35m 전체맵 — 생성·렌더링·공간 검사

2026-11-14. Unity6000.3.26f1 / Built-in / Windows 개발 빌드. 최종 아트·VR·게임 완주 검증이 아니다.

## 결과와 재현

`Game`에서 `Anseong/Map35/Generate and Build Viewer`를 실행한다. 배치 장면은 `Assets/Map35/Scenes/Steelworks35m.unity`, 뷰어는 저장소 루트 `Builds/Map35/AnseongMap35.exe`. 생성 시 이 폴더의 캡처/좌표/검사 기록을 갱신한다. 씬을 수동 편집했다면 재생성 전에 복제한다.

- `view-00.png` 조감, `01` 평면, `02~04` 메카 Z1/Z2/Z3 근접, `05~07` 드론 각 구역.
- `08~13` K0/K1/K2/K2b/K3/K3b, `14` 메카 Z2 진입, `15` 메카 Z3 접근.
- `view-index.csv`: Unity xyz와 원근 카메라의16:10 수평/수직 화각. 01은 직교이므로 해당 행 화각은 사용하지 않는다.
- `geometry-check.txt`: 메카 중심선470m, 정적 외곽249개 위치의 검사 대상 건물 접촉 없음. 드론 주 경로 반경1.5 구체의 검사 대상 접촉 없음.
- `sightlines.csv`: 지정 점의 화각/선 가림 결과. K1 미는 면은 판에, K3 코일 가장자리는 횡빔에 가린다. 각각 K0/K3b로 보완한다. 기존 K1 위치 z=-20은 정비고 벽에 가린다.
- `build.log`, `viewer-smoke.log`, `xr-reopen.log`: 최종 생성/빌드·실행 시작·프로젝트 재열기 설정 확인 기록.

자동 실행 시작 확인: `AnseongMap35.exe -batchmode -nographics --map-smoke -logFile <로그 경로>`. `MAP35_VIEWER_READY views=16 route=470` 후 종료0. 이는 카메라 버튼을 사람이 눌러 확인하거나 VR로 실행한 결과가 아니다. 이미지16장은 Editor 카메라 렌더로 생성한다.

## 공간 검사 한계

메카 상자는12×34.8×16, 중심높이17.6. 각 선분의 진행 방향으로 놓되 연속 회전을 검사하지 않는다. 건물 계층의 높이3m 초과 콜라이더만 검사하며 보스·판·파편·소품·펼친 팔은 제외한다. 드론은 주 경로만 검사하고 선택 D6는 제외한다. 보스 경로는 단계적 진행이 필요하며 움직이는 보스와의 충돌은 이번 자동 검사에 없다.

시선은 표적 지정 점 하나의 검사이며 전체 표적 실루엣이나 UI 가독성을 보증하지 않는다. 물리 콜라이더는 실제 단순 형상에 맞춰 구성했다. 특히 비균등 확대된 구체의 SphereCollider는 가장 큰 축 기준으로 과도하게 커지므로 보스 타원 형상은 정적 MeshCollider로 교체했다.

## 빌드 중 발견한 기존 설정 문제

기존 Z1 생성기가 XRGeneralSettings를 독립 main asset으로 만들었고, Unity XR 관리 패키지가 재시작 시 per-target 컨테이너로 자동 변환하면서 Manager 참조가 빠졌다. OpenXR 빌드 콜백에 NullReferenceException이 발생했다. Z1Build의 설정 준비를 PrepareXR로 분리하고 공식 로컬 패키지 API의 CreateDefaultSettingsForBuildTarget/CreateDefaultManagerSettingsForBuildTarget을 사용해 sub-asset으로 생성하도록 보정했다. 일반·매니저 자동 시작은 계속 false이며 명시적 VR 역할만 초기화한다. 기존 협동 런타임 로직·Z1 장면은 수정하지 않았다. 수정 뒤 빌드와 새 Editor 프로세스 재열기로 확인한다.
