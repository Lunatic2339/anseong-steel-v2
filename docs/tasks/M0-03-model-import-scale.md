# M0-03 로봇 모델 임포트와 65m 스케일

- 마일스톤: M0 / 담당: 선우 (+성재) / 브랜치: `feat/sunwoo-model-import`
- 선행: M0-01 병합

## 목표
보스·플레이어 공용 v026 메카를 새 프로젝트에 깨끗하게 들여오고, Idle 애니메이션이 정상으로 재생되는 65m 크기 배치를 샌드박스 씬에서 확인한다.

## 배경
- `docs/design/03_reuse_from_old_repo.md`의 "모델".
- 기존 프로젝트에는 같은 메카가 두 종류 있다. 어느 쪽이 Mixamo 애니메이션과 바로 맞는지 **이 작업에서 확인**한다.
  - A: `AS/_BossPrototype/Models/BossUpdatedCompatible.fbx` (애니메이션 호환을 위해 Blender로 재출력한 것, 기존 프로젝트는 bindpose를 별도 메시 291개로 보정해서 씀)
  - B: `AS/_Project/02_Art/01_Models/mecha-finger-rig-mixamo-v026dsadsfadf.fbx` (원본, 손가락 리그 포함)
- 기존 레포 로컬 경로: `C:\Users\anthony\Desktop\Capstone\anseong-steel` (`AS/` = `AnseongSteel/Assets/`). ChatGPT 사본: `...\anseong-steel\클로드용 모델전수\`.
- 애니메이션 소스: `AS/_BossPrototype/Animation/Idle.fbx`, `Walk.fbx`, `Punch.fbx` (각 22MB, 메시 포함).

## Claude가 할 일
1. A와 B의 FBX(**.meta 제외**)와 필요한 텍스처를 `Assets/_Anseong/Art/Robot/Source/`에 복사한다. 기존 재질·CompatibleSkins·프리팹·씬은 가져오지 않는다.
2. `Idle.fbx`, `Walk.fbx`, `Punch.fbx`를 `Assets/_Anseong/Art/Robot/Animation/Source/`에 복사한다.
3. `Assets/_Anseong/Editor/RobotModelPostprocessor.cs`(AssetPostprocessor): `Art/Robot/` 아래 FBX에 일관된 임포트 설정 적용 — Generic 리그, Animation Type, 재질 임포트 방식, 애니메이션 FBX는 메시를 쓰지 않도록 설정. 설정값은 상단 상수로 모은다.
4. `Assets/_Anseong/Editor/ScaleSandboxMenu.cs`: 메뉴 **Anseong → Sandbox → Build Scale Test**. 새 씬(사람이 저장)에 다음을 만든다:
   - 모델 A와 B를 나란히 배치하고, 각각 렌더러 bounds 높이를 측정해 **높이 65m**가 되도록 스케일 계산·적용, 측정값을 Console에 출력.
   - 각 모델에 Animator + Idle 클립 재생용 최소 컨트롤러(에디터 스크립트로 생성).
   - 크기 비교 기준물: 1.8m 사람 기둥, 4m 트럭 박스, 30m 건물 박스.
5. 두 모델의 SkinnedMeshRenderer 수, 재질 수, 삼각형 수를 측정하는 메뉴 **Anseong → Sandbox → Report Robot Stats**를 만들고 결과를 결과 칸에 적는다.

## 사람이 할 일
- [ ] **Build Scale Test** 실행 → `Assets/_Anseong/Scenes/Sandbox/Sandbox_Scale.unity`로 저장.
- [ ] Play: A와 B 중 Idle이 **메시 찢어짐·뒤틀림 없이** 재생되는 쪽을 고른다. 둘 다 정상이면 B(원본)를 우선.
- [ ] 에디터 Scene 뷰에서 65m 높이와 기준물 비율이 그럴듯한지 본다.
- [ ] 고른 모델을 Claude에게 알려준다 → Claude가 고르지 않은 쪽 파일을 삭제.

## 하지 말 것
- 메시 병합·텍스처 아틀라스(별도 작업, 성재·윤주).
- 조종석, XR 시점, IK.
- 152MB 베이크 `.anim` 복사.

## 완료 기준
- 고른 모델 하나가 65m 크기로 Idle 재생, 메시 깨짐 없음.
- 렌더러·재질·삼각형 수가 기록됨.

## 결과 (작업 후 Claude가 작성)
- 선택한 모델: **A `BossUpdatedCompatible.fbx`** (선우, Play에서 확인). B는 삭제.
  - 근거: FBX 노드를 읽어 보면 A와 Idle/Walk/Punch는 단위 m, `Armature` 스케일 1로 구조가 같다. B는 단위 cm에 `Armature` 스케일 100이라, Idle 재생 시 몸이 사라졌다(라벨만 보이고 메시는 안 보임). 두 모델 모두 Idle 바인딩 경로는 33/33 일치했으므로 경로가 아니라 Armature 스케일 차이가 원인으로 보인다.
  - 측정: 임포트 직후 렌더러 높이 1.056 m → 배율 61.550 → 65 m, 바닥 보정 3.480 m.
- 바뀐 파일:
  - `Art/Robot/Source/BossUpdatedCompatible.fbx`
  - `Art/Robot/Source/Textures/texture-000~121.png` 122장 (모델이 같은 이름으로 외부 참조. 기존 `.fbm`과 `Surface/Textures` 사본은 md5 동일 → 한 벌만)
  - `Art/Robot/Animation/Source/{Idle,Walk,Punch}.fbx`, `Art/Robot/Animation/Robot_Idle.controller` (메뉴가 생성)
  - `Editor/RobotModelPostprocessor.cs` — Generic, Avatar 자체 생성, 스케일 1, 모델은 Material Description 재질(InPrefab)·애니메이션 끔, 애니메이션 FBX는 재질 없음·블렌드셰이프 끔·메시/렌더러 제거, 클립 이름 = 파일 이름, Idle/Walk 루프
  - `Editor/ScaleSandboxMenu.cs` — `Anseong → Sandbox → Build Scale Test`, `Report Robot Stats`
  - `Runtime/Core/HeightFit.cs` + `Tests/EditMode/HeightFitTests.cs` — 65m 배율·바닥 보정 계산
  - `Scenes/Sandbox/` 폴더
- 로봇 통계 (Report Robot Stats): _사람이 실행 후 기록_
- 사람이 확인할 것:
  - [x] Unity 임포트 후 Editor.log에 `error CS` 없음 (B 삭제·메뉴 정리 후 재확인 필요).
  - [ ] EditMode 테스트 `HeightFitTests` 통과.
  - [ ] `Idle.fbx` 등 애니메이션 FBX를 펼쳤을 때 Mesh 서브에셋이 없음.
  - [x] Play에서 A의 Idle 재생 (선우 확인).
  - [ ] Build Scale Test 다시 실행 → `Sandbox_Scale.unity` 저장. Scene 뷰에서 65m와 1.8m·4m·30m 기준물 비율.
  - [ ] Report Robot Stats 결과를 Claude에게 전달.
- 남은 문제 / 제안:
  - **화면 계단 현상**: `Assets/Settings/PC_RPAsset`의 MSAA가 꺼져 있다(`m_MSAA: 1`). 렌더 스케일은 1. VR(PC 렌더링)에서는 MSAA 4x가 보통이므로 켤지 총괄 채팅에서 결정 제안. 이 작업 범위 밖이라 바꾸지 않았다.
  - 텍스처 122장 대부분이 32~512px라 65m 크기에서 가까이 보면 흐릴 수 있다. 메시 병합·아틀라스 작업(성재·윤주) 때 함께 검토.
  - FBX 4개가 약 88MB로 일반 git에 들어간다(각 50MB 이하). B는 히스토리에 남는다. 저장소 크기가 부담되면 Git LFS 도입을 총괄 채팅에서 결정.
  - 애니메이션 FBX 메시는 `OnPostprocessModel`에서 지운다. 서브에셋이 남으면 Mixamo "Without Skin" 재다운로드로 바꾸는 것을 제안.
