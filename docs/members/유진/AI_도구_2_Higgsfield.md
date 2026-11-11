# 유진 — Higgsfield로 영상 만들기

[← 유진 처음으로](README.md) · 전체 도구 표: [13 AI 도구](../../design/13_ai_tools.md)

Higgsfield는 글이나 사진을 넣으면 이미지·짧은 영상을 만드는 웹 도구입니다. 유진님은 **영상 네 가지**(이펙트 재료, 게임 안 시네마틱, 대기 화면, 홍보)를 만듭니다. 컨셉 이미지는 Meshy에 바로 넣는 성재님이 맡습니다(선우 11/11). 같은 계정(Higgsfield **Plus**)을 성재님이 11/18까지 이미지에 쓰니, 영상은 **11/19부터** 만듭니다. 한 달 구독이라 **12/08 전에 모든 영상을 받아 둡니다**. 영상은 크레딧을 많이 쓰니 **낮은 설정으로 짧게 시험한 뒤** 만듭니다.

## 공통

- 선우에게서 계정을 받고, 다운로드할 때 **워터마크가 없는지** 확인합니다(있으면 팀 단톡방에).
- 프롬프트 앞에 [스타일 한 줄](../../design/13_ai_tools.md#3-모두-지킬-것)을 붙입니다. 실제 게임·애니·영화 이름은 쓰지 않습니다.
- 사람 얼굴 사진을 넣지 않습니다(팀원도 본인 허락 없이는 안 됨).
- 다 만든 것은 드라이브 `안성제철_영상` 폴더 + `SOURCES` 문서에 `파일 / Higgsfield / 날짜 / 프롬프트` 한 줄.

## ⓪ 이펙트 재료 영상 (11/19 시험 1개 → 11/25 6개)

게임 안 이펙트(펀치 불꽃, 드론 폭발 등)는 Unity 파티클이라 AI가 바로 만들어 주지는 못합니다. 대신 **Higgsfield로 폭발·불꽃·연기를 짧은 영상으로 만들면**, 코드 담당이 그 영상을 그림 64장 묶음(플립북)으로 바꿔 파티클에 붙입니다. 진짜 같은 불·연기는 이 방법이 가장 빠릅니다.

꼭 지킬 것(안 지키면 게임에 못 씀)
- **배경은 완전한 검정**, 땅·하늘·건물 없이 효과만. 프롬프트에 `isolated on pure black background, no ground, no environment`.
- **카메라 고정**(움직이면 안 됨), 효과가 화면 **가운데**에서 시작해 화면 밖으로 안 나가게.
- 2~3초 안에 터지고 **다 사라져서 검정으로 끝남**. 글자·로고 없음.
- MP4로 받아 드라이브 `안성제철_영상/VFX`에, 이름은 아래 표대로.

| 파일 | 게임에서 | 프롬프트 예시 |
|---|---|---|
| `VFX_HitSpark.mp4` | 펀치·검 명중 불꽃 | `burst of bright orange metal sparks, isolated on pure black background, static camera, slow motion` |
| `VFX_Explosion.mp4` | 드론 격추, 보스 무기 부서짐 | `fiery explosion with dark smoke, isolated on pure black background, static camera, centered` |
| `VFX_Smoke.mp4` | 쓰러짐·박힘 먼지, 부서진 뒤 연기 | `thick grey smoke puff rising and fading, isolated on pure black background, static camera` |
| `VFX_Thruster.mp4` | 팔꿈치 부스터 불꽃 | `blue-white rocket thruster flame jet, side view, isolated on pure black background, static camera` |
| `VFX_MuzzleFlash.mp4` | 포 쏠 때 | `heavy cannon muzzle flash, short bright burst, isolated on pure black background, static camera` |
| `VFX_Electric.mp4` | 해킹 잡음, 필살기 충전 | `crackling blue electric arcs, isolated on pure black background, static camera` |

광선·레이저·예고 발광·드러날 때 외곽선은 영상이 아니라 코드(선·발광 재질)로 만듭니다. 11/19에 `VFX_HitSpark` 하나로 먼저 시험해 코드 담당이 게임에 붙여 보고, 되면 나머지를 만듭니다.

## ① 게임 안 시네마틱: 오프닝·엔딩 (1차 11/25, 확정 12/01)

활용 계획서의 "게임 내 시네마틱 영상"입니다. VR에서 눈앞 전체를 영상으로 덮으면 멀미가 나서, **조종석 앞 큰 화면에 트는 영상**으로 만듭니다(두 조종사가 같이 봄).

| 파일 | 언제 | 길이 | 내용(대본 기준 예시) |
|---|---|---|---|
| `Video_Opening.mp4` | 판 시작 → 기체 점검 전 | 20~40초 | 외계에서 보낸 기계가 제철소에 내려앉음 → 경보 → 정비고에서 우리 로봇이 깨어남 |
| `Video_Ending.mp4` | 승리 → 결과판 전 | 20~30초 | 쓰러진 보스의 머리 빛이 꺼짐 → 불타는 제철소 위로 해가 뜸 → 우리 로봇 실루엣 |

만드는 법은 아래 ② 대기 화면 영상과 같습니다(게임 캡처를 넣어 Image to Video → 이어 붙이기 → MP4). 글자·소리는 넣지 않습니다(대사는 사령관 목소리, 음악은 `BGM_Opening`·`BGM_Ending`이 게임에서 깔림). 보스는 기계라 생물처럼 울부짖는 장면은 넣지 않습니다.

## ② 대기 화면 영상 (12/01) → 정비고 화면 + 부스 모니터

게임 시작 전 정비고 판과 부스 모니터에 반복해서 트는 영상입니다. 지나가는 사람이 "저게 뭐지" 하고 오게 만드는 것이 목적입니다.

1. 선우에게 **게임 화면 캡처 3~5장**을 받습니다(보스 정면, 제철소 마당, 우리 로봇 팔).
2. Higgsfield의 이미지 → 영상(Image to Video)에 캡처를 넣고 카메라 움직임을 고릅니다(천천히 다가가기, 아래에서 올려다보기). 게임 그림을 넣어야 게임과 같아 보입니다.
3. 5~10초 클립 4~6개를 만들어 무료 편집기(Windows 기본 Clipchamp 등)로 이어 붙입니다. 끝과 처음이 자연스럽게 이어지게.
4. 내보내기: **MP4(H.264), 1920×1080, 30fps, 30~60초, 50MB 이하**, 글자 넣지 않기(제목은 코드가 띄움), 소리 없이.
5. 이름 `Video_Lobby.mp4` → 드라이브 + 팀 단톡방 `[올림]`. 선우가 레포 `Assets/_Anseong/Art/Video/`에 넣습니다.

장면 예: 어두운 제철소에 쇳물 빛 → 멀리서 보스 머리 빛이 켜짐 → 정비고 문이 열리며 우리 로봇 팔 → 두 로봇이 마주 섬.

## ③ 홍보 영상 (12/08) → 전시 소개·SNS

60~90초. M6 테스트(11/27~12/03) 때 찍은 **실제 플레이 영상**(헤드셋 화면 + 조종사 모습)이 중심이고, Higgsfield 장면은 앞뒤와 사이에 넣습니다. 조종사 얼굴이 나오면 본인 허락을 받습니다. 드라이브에만 둡니다(레포에 안 넣음).

## 마감
| 날짜 | 할 일 |
|---|---|
| 11/19 | 이펙트 재료 `VFX_HitSpark` 시험 1개 |
| 11/25 | 이펙트 재료 6개, 오프닝·엔딩 1차, 대기 화면 영상 시험 1개(10초) |
| 12/01 | `Video_Opening.mp4`, `Video_Ending.mp4`, `Video_Lobby.mp4` |
| 12/05 | 홍보 영상에 넣을 Higgsfield 장면 다 받기 |
| 12/08 | 홍보 영상 편집 끝 |
