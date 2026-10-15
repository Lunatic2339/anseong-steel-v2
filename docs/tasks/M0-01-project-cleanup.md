# M0-01 프로젝트 정리와 설정 확인

- 마일스톤: M0 / 담당: 선우 / 브랜치: `feat/sunwoo-project-cleanup`

## 목표
템플릿이 넣은 불필요한 패키지를 빼고, XR·Player 설정을 기획대로 맞춘 깨끗한 시작 상태를 만든다.

## 배경
- `CLAUDE.md`의 "고정된 결정".
- 현재 `Packages/manifest.json`에 게임에 필요 없는 패키지가 있다: `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.multiplayer.center`, `com.unity.pipeline`, `com.unity.visualscripting`.
- 현재 OpenXR(PC) 컨트롤러 프로필이 Oculus Touch + **Touch Pro**로 켜져 있다. Quest 3는 **Touch Plus**다.

## Claude가 할 일
1. `Packages/manifest.json`에서 위 7개 패키지를 제거한다. 다른 패키지 버전은 바꾸지 않는다.
2. 남겨야 할 것이 정말 쓰이지 않는지 확인한다: 코드·에셋에서 해당 패키지 네임스페이스나 GUID를 참조하는 곳이 있으면 제거 전에 알려준다.
3. 사람이 Unity를 다시 연 뒤, `Editor.log`에서 패키지 해석·컴파일 에러가 없는지 확인한다.
4. 아래 "사람이 할 일" 체크리스트를 대화창에 다시 보여주고, 사람이 마친 뒤 `ProjectSettings/ProjectSettings.asset`과 `Assets/XR/Settings/OpenXRPackageSettings.asset`을 **읽기만** 해서 값이 바뀌었는지 확인해 결과에 적는다.

## 사람이 할 일
- [ ] Claude가 manifest를 고치는 동안 Unity를 닫아둔다. 끝나면 다시 연다.
- [ ] Edit → Project Settings → XR Plug-in Management → OpenXR(모니터 탭) → Enabled Interaction Profiles에서 **Meta Quest Touch Pro** 제거(−), **Meta Quest Touch Plus Controller Profile** 추가(+). Oculus Touch는 유지.
- [ ] Project Settings → Player → Other Settings → **Api Compatibility Level = .NET Framework**.
- [ ] Player → Company Name `AnseongSteel`, Product Name `AnseongSteel`.
- [ ] Project Validation(XR Plug-in Management 아래)에 경고가 있으면 내용을 Claude에게 붙여넣고 Fix 여부를 함께 판단.
- [ ] Unity를 저장하고 닫는다(설정 파일이 디스크에 쓰이도록).

## 하지 말 것
- 씬·스크립트 추가, 다른 설정 변경.
- `ProjectSettings/*`를 텍스트로 직접 수정(읽기만).

## 완료 기준
- Unity가 에러 없이 열린다.
- Package Manager → In Project에 위 7개가 없다.
- OpenXR PC 프로필이 Oculus Touch + Touch Plus 두 개.
- Api Compatibility Level이 .NET Framework.

## 결과 (작업 후 Claude가 작성)
- 바뀐 파일:
  - `Packages/manifest.json`: 7개 패키지 제거(Claude). 다른 패키지 버전 변경 없음.
  - `Packages/packages-lock.json`: Unity가 재생성. 7개 + 그것들만 쓰던 의존성 3개(`com.unity.2d.sprite`, `com.unity.dt.app-ui`, `com.unity.nuget.newtonsoft-json`) 제거, 남은 패키지 버전 변경 없음.
  - `Assets/XR/Settings/OpenXRPackageSettings.asset`: PC(Standalone) 프로필 Touch Pro 끔, Touch Plus 켬. 켜진 프로필 = Oculus Touch + Meta Quest Touch Plus.
  - `ProjectSettings/ProjectSettings.asset`: `companyName`·`productName` = `AnseongSteel`.
  - `ProjectSettings/VersionControlSettings.asset`: `Unity Version Control` → `Visible Meta Files` (아래 참고).
- 사람이 확인할 것:
  - Package Manager → In Project에 제거한 7개가 없는지.
  - Api Compatibility Level은 템플릿부터 이미 .NET Framework였다(파일의 `apiCompatibilityLevel: 3` = .NET Framework, 플랫폼별 값 없음). 에디터 화면에서도 .NET Framework로 확인됨. 그래서 이 항목은 파일 변경이 없다.
  - `Editor.log`: 패키지 해석 에러·`error CS` 없음. 경고는 `Anseong.Runtime`·`Anseong.Editor`·`Anseong.Tests.EditMode` asmdef에 스크립트가 없다는 것뿐(스크립트가 생기면 사라짐).
- 남은 문제 / 제안:
  - `collab-proxy`(Unity Version Control) 제거 후 Version Control 모드가 `Hidden Meta Files`로 바뀌어서, 사람이 `Visible Meta Files`로 되돌렸다. git 사용에 필요하므로 유지할 것.
  - `ProjectSettings/Packages/com.unity.ai.assistant/Settings.json`이 git에 남아 있다. 패키지를 뺐으므로 쓰이지 않는 파일이다. `ProjectSettings/*` 수정 금지 규칙 때문에 그대로 뒀다. 삭제 여부를 총괄 채팅에서 결정하기를 제안.
