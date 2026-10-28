# 안성제철 (anseong-steel-v2)

두 조종사가 65m 거대 로봇의 팔을 하나씩 맡아 보스 한 기를 쓰러뜨리는 협동 VR 맨손 전투. 5인 캡스톤, **공개 시연 2026-12-11**.
**게임 전체를 관통하는 개념: 복싱 게임이 아니라 육중한 로봇을 조종하는 메카 게임.** 로봇 동작은 빠릿빠릿하지 않고 묵직하게(시점은 늦추지 않음). 모든 기능·수치·애니메이션·소리는 이 기준으로 정한다(`06_feel_and_sound.md` 맨 위).

이 파일은 모든 Claude Code 세션이 시작할 때 읽는다. 짧게 유지하고, 자세한 내용은 아래 문서를 읽는다.

## 먼저 읽을 문서

| 문서 | 내용 |
|---|---|
| `docs/design/00_plan.md` | 시연 범위, 한 판 흐름, 일정, 팀 분담 |
| `docs/design/01_combat_rules.md` | 전투 규칙과 초기 수치 (펀치·연계·부스트·가드·회피·보스) |
| `docs/design/02_architecture.md` | PC 2대 네트워크 구조, 시점·스케일, 외골격 |
| `docs/design/03_reuse_from_old_repo.md` | 기존 레포에서 참고할 로직(파일:줄)과 피해야 할 버그 |
| `docs/design/04_art_spec.md` | 아트 기준(뼈대·크기·성능 한도·재질 슬롯), 보스 애니메이션 목록, 아트 마감 |
| `docs/design/05_boss_patterns.md` | 보스 공격 흐름·기술표·페이즈별 순서·전환 (제안값) |
| `docs/design/06_feel_and_sound.md` | 조작감 컨셉: 공격·이동 동작별 소리·진동·외골격 |
| `docs/design/07_milestones.md` | 마일스톤별 지시서(예정 번호)·완료 기준·팀원 입력. 다 끝내면 게임 완성 |
| `docs/design/08_tuning_menu.md` | 헤드셋 조절 메뉴에 지금 있는 것·뺀 것·되살리는 법 |
| `docs/design/09_calibration.md` | 조종사 보정 설계(2단계: 정면 뻗기 → 가드, 어떤 기기·체격이든 정면 = 정면) |
| `docs/tasks/` | 작업 지시서. **한 세션 = 지시서 하나 = PR 하나** |
| `docs/폴더_지도.md` | 폴더·파일 위치와 연결 지도. **폴더나 중요한 파일(메뉴, 설정 에셋, 결과물 넣는 곳)을 추가·이동·삭제한 세션은 같은 커밋에서 이 지도도 고친다** |
| `docs/members/` | 조원별 지시서 원본(사람별 폴더, 주제별 파일. 한 파일 한 주제, 150줄 넘으면 나눔). 팀원에게 요청이 생기면 그 사람 파일의 "다른 작업에서 온 요청"에, 팀원 문제 보고는 `선우/README.md`에 적는다(규칙: `docs/members/README.md`) |

## 고정된 결정

- Unity **6000.3.26f1**, URP, OpenXR + XR Interaction Toolkit(XR Origin·입력 액션만), Input System, Netcode for GameObjects.
- 실행은 **PC 2대 + Quest 3 Link(PC 렌더링)**. Quest 단독(Android) 빌드는 하지 않는다.
- 쓰지 않는 것: Meta XR SDK, Movement SDK, Unity Gaming Services·Relay·Lobby·Vivox, VR Multiplayer 템플릿, Animation Rigging. 추가하려면 먼저 총괄 채팅에서 결정한다.
- 네트워크: PC-A = 호스트 + 왼쪽 조종사, PC-B = 클라이언트 + 오른쪽 조종사. Unity Transport 직결, 포트 7777, 2명까지만 승인.
  - **자기 팔은 로컬 입력으로 즉시 그린다.** 서버 echo로 그리지 않는다.
  - 팔·머리 자세는 비신뢰(Unreliable) 전송, 받는 쪽은 보간.
  - 펀치 명중은 때린 쪽 PC가 판정해서 서버에 보고. 서버는 연계 차례·반격 기회·중복만 검사.
  - 보스 AI·HP·페이즈는 서버만 계산하고 상태 + 시작 틱을 복제.
- **1인 양팔 모드**를 항상 유지한다. 한 PC가 두 팔을 모두 로컬로 맡는 모드이며 개발 검증과 시연 당일 대체용이다.
- 외골격: 각 PC가 자기 ESP32를 USB 시리얼로 제어(`System.IO.Ports`, Api Compatibility Level = .NET Framework). 외골격이 없어도 게임은 끝까지 돌아가야 한다.

## 폴더

```
Assets/_Anseong/
  Runtime/   (asmdef: Anseong.Runtime)  하위: Core, Pilot, Combat, Boss, Net, Haptics, Flow, UI
  Editor/    (asmdef: Anseong.Editor)
  Tests/EditMode/ (asmdef: Anseong.Tests.EditMode)
  Art/{Robot,Boss,Cockpit,Pilot,Environment,Weapons}  Audio/
  Scenes/Main.unity, Scenes/Sandbox/Sandbox_<이름>.unity
Hardware/Exo/firmware/   ESP32 펌웨어 (Unity 밖)
```

네임스페이스는 `Anseong.<폴더>` (예: `Anseong.Combat`).

## 코드 규칙

- 규칙·수학은 MonoBehaviour와 분리된 순수 클래스로 만들고 `Tests/EditMode`에 NUnit 테스트를 붙인다.
- 조정할 수치는 `[SerializeField]` + `[Tooltip]`으로 Inspector에 노출한다. 클래스 기본값 = 기획 문서의 초기값.
- 헤드셋을 쓰고 조절할 수치는 인게임 조절 메뉴에도 넣는다(선우 10/25). 컴포넌트가 `Anseong.Core.ITunable`을 구현해 `AddTuning`에서 항목을 등록하면 `UI/TuningMenu`(오른손 B)에 나온다.
- 금지: `[InitializeOnLoad]`, `RuntimeInitializeOnLoadMethod`, 런타임 `OnGUI` 디버그 UI, 런타임 `Shader.Find`, 매 프레임 `GetComponent`·`Find*`, 문자열 오브젝트 이름이나 숫자 레이어 하드코딩, 런타임 코드의 `UnityEditor` 참조.
- 디버그·검증 코드는 `Editor/` 또는 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 안에만 둔다.
- 시간은 네트워크 코드에서 `NetworkManager.ServerTime`을 쓴다. `Time.time`을 서버 판정에 쓰지 않는다.

## Unity 작업 규칙

- `.unity`, `.prefab`, `.meta`, `ProjectSettings/*`를 텍스트로 직접 고치지 않는다. 씬·프리팹 구성이 필요하면 **에디터 메뉴 스크립트**(`Editor/`)를 만들고 사람이 실행한다.
- `Scenes/Main.unity`는 선우만 수정한다. 다른 사람은 `Scenes/Sandbox/`에서 작업하고 프리팹으로 넘긴다.
- 새 파일의 `.meta`는 Unity가 만든다. 만들어진 `.meta`는 반드시 같은 커밋에 넣는다.
- 컴파일 확인: Unity를 켜둔 상태에서 저장 → `%LOCALAPPDATA%\Unity\Editor\Editor.log` 끝부분에서 `error CS`를 찾는다.
- **VR 동작은 사람이 헤드셋으로 확인한 것만 완료로 친다.** Claude는 "헤드셋에서 확인됨"이라고 쓰지 않는다. 확인할 항목을 목록으로 남긴다.

## Git

- **작업 기준 브랜치는 `develop`**(선우 10/28). `main`은 안정(백업) 버전이라 검증된 시점에만 선우가 `develop`→`main`으로 병합한다. `main`·`develop`에 직접 커밋하지 않는다. 코드 작업 브랜치는 `develop`에서 따고 `feat/m<마일스톤>-<지시서 번호>-<내용>`(예: `feat/m1-05-punch-hit-boss-hp`, 선우 10/18: 어느 마일스톤의 무슨 작업인지 이름만 보고 알게), PR은 `develop` 대상. 스레드는 마일스톤마다 하나(M1 스레드, M2 스레드), 브랜치는 지시서마다 하나.
- 코드를 쓰지 않는 팀원(유진·성재·윤주·고건)은 자기 이름 브랜치 하나(`yujin`·`sungjae`·`yunju`·`gogeon`)에 푸시만 하고 PR은 열지 않는다. 팀 단톡방의 `[올림]`을 받으면 코드 담당 세션이 그 브랜치에서 필요한 파일만 골라(`git checkout <브랜치> -- <경로>`) 자기 `feat/` 브랜치로 가져와 PR로 `develop`에 넣는다. 프로젝트 설정·패키지·`Main.unity` 변경은 가져오지 않는다. 규칙: `docs/GitHub_작업_규칙.md`.
- 파일 하나 50MB 이하. 베이크된 대용량 `.anim`, 백업 폴더, 빌드 결과, 검증 로그·캡처를 커밋하지 않는다.

## 작업 방식

1. `git pull` 후 지시서(`docs/tasks/<번호>.md`)를 읽고 **계획부터** 제안한다. 승인 전에는 수정하지 않는다.
2. 지시서 범위 밖의 기능·리팩터링은 하지 않는다. 필요해 보이면 지시서의 "결과" 칸에 제안으로 적는다.
3. 끝나면 지시서의 "결과" 칸(바뀐 파일, 사람이 확인할 것, 남은 문제)을 채우고 커밋·PR.

## 기존 레포 (읽기 전용 참고)

- GitHub `Lunatic2339/anseong-steel` (develop), 로컬 `C:\Users\anthony\Desktop\Capstone\anseong-steel`.
- 파일을 통째로 복사하지 않는다. `03_reuse_from_old_repo.md`가 가리키는 로직만 참고해 새 구조로 다시 쓴다.
