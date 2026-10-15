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
- 바뀐 파일:
- 사람이 확인할 것:
- 남은 문제 / 제안:
