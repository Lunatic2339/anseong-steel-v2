# M3-01 PC 2대 접속 · 운영자 시작 화면 · 팔 주인 검사

- 마일스톤: M3 / 담당: 선우(코드는 Claude), PC-B·2인 검증 성재 / 브랜치: `feat/m3-01-connect`
- 선행: M2-01 병합(11/03). M2-02~05와 겹치지 않는다(접속·세션 파일만).
- 기한(제안): 11/08

## 목표
PC-A는 "호스트(왼팔)", PC-B는 "클라이언트(오른팔)"로 시작해 직결 7777로 붙는다. 3번째 PC와 다른 빌드는 거절한다. 서버는 각 팔의 보고를 그 팔 주인 PC에서 온 것만 받는다. 1인 양팔은 지금처럼 한 PC에서 돈다.

## 배경
- CLAUDE.md 고정 결정: PC-A = 호스트 + 왼쪽, PC-B = 클라이언트 + 오른쪽, Unity Transport 직결 7777, 2명까지만 승인. 1인 양팔 모드 항상 유지.
- `07_milestones.md` M3-01: 운영자 시작 화면(1인 양팔 / 호스트 / 클라이언트 + IP 입력, 마지막 IP 기억).
- M2-01: `SessionBootstrap`(Mode 칸만 있고 Host·Client 비어 있음), `CombatServer`의 팔 주인 자리(`pilotClient`).
- 손 역할은 이미 `PilotHandInput.ArmSide`·`ControlsBothArms`가 정하고 보정·펀치·가드·IK가 모두 따른다.

## Claude가 할 일
1. 순수 `Net/SessionRules`: 접속 승인(호스트 자신 OK, 1인 양팔은 남 거절, 약속 번호 다르면 거절, 호스트 포함 2명), 팔 주인(시작·들어옴·나감·검사), IPv4 읽기, 실행 인자 `-mode`·`-ip`.
2. `SessionBootstrap`: 호스트는 0.0.0.0:7777에서 기다림, 클라이언트는 IP로 접속(5초 안에 못 찾으면 실패), 접속 승인 콜백. 모드에 맞게 맡은 손을 바꾸고 보정을 처음부터. 마지막 모드·IP는 PlayerPrefs. 끊기면 이유를 띄우고 시작 화면으로. 에디터는 기본으로 시작 화면을 건너뛰고 Inspector Mode로 바로 시작(지금과 같음).
3. `CombatServer`: 팔 주인 채우기(1인 양팔 = 둘 다 호스트, 호스트 모드 = 왼팔 호스트·오른팔 접속한 PC), 명중·헛침·그립 보고를 주인 아니면 버림. `CombatDirector.Connect`는 맡은 팔만 보낸다.
4. `UI/OperatorStartScreen`: PC 모니터 화면(Screen Space Overlay). 모드 3개, 호스트는 이 PC IP 표시, 클라이언트는 IP 칸, 시작(Enter), 문제 빨간 줄. 시작 뒤에는 왼쪽 위 상태 줄(모드·조종사 수) + "다시 고르기". 헤드셋 안내판에는 "운영자가 시작하기를 기다리는 중".
5. 메뉴 **Add Network Session**이 시작 화면도 붙인다.
6. EditMode 테스트 `SessionRulesTests`.

## 사람이 할 일
- [ ] 에디터 Play(1인 양팔): 지금처럼 바로 시작, Console `[Session] Host started (1인 양팔…)`, M1·M2-01 기능 그대로.
- [ ] `SessionBootstrap` Skip Start Screen In Editor를 끄고 Play: 모니터에 시작 화면, 헤드셋은 안 보임(안내판에 노란 줄). 1인 양팔 시작 → 위와 같음. 다시 고르기 → 화면 다시.
- [ ] 빌드 2대(성재와): PC-A 호스트 → 화면에 나온 IP를 PC-B 클라이언트 칸에 → 시작. 두 PC 상태 줄 "조종사 2/2". Windows 방화벽 창이 뜨면 "개인 네트워크 허용".
- [ ] PC-A는 왼손만, PC-B는 오른손만 보정·펀치를 받는다. PC-B 오른손 명중 → 보스 HP가 두 PC에서 같이 준다.
- [ ] 3번째 PC(또는 같은 PC의 빌드 하나 더) 접속 → "이미 조종사 2명" 거절.
- [ ] PC-B 끄기 → PC-A 상태 줄 1/2, 다시 켜서 접속 → 2/2. PC-A 끄기 → PC-B에 "호스트와 연결이 끊겼습니다" + 시작 화면.
- [ ] 다음 실행에서 마지막 모드·IP가 미리 골라져 있다.

## 하지 말 것
- 상대 팔·머리 그리기·보간(M3-02), 가드·필살기 RPC(M3-02), 협동 이동(M3-03), 빌드 절차서(M3-05).

## 완료 기준
- 빌드 두 대가 호스트·클라이언트로 붙고, 3번째는 거절되고, 각 PC는 자기 팔만 서버에 보고한다(사람 확인). 1인 양팔 그대로. 테스트 통과.

## 결과 (작업 후 Claude가 작성)
- 바뀐 파일:
  - 새로: `Runtime/Net/SessionRules.cs`, `Runtime/UI/OperatorStartScreen.cs`, `Tests/EditMode/SessionRulesTests.cs`
  - 고침: `Net/SessionBootstrap.cs`(호스트·클라이언트·승인·역할·기억·끊김), `Net/CombatServer.cs`(팔 주인 채우기·검사), `Combat/CombatDirector.cs`(Connect 때 맡은 팔만 2줄), `UI/TestGuidePanel.cs`(대기 노란 줄), `Editor/NetworkSessionMenu.cs`(시작 화면 붙임), `Anseong.Runtime.asmdef`(`UnityEngine.UI` 참조), `docs/폴더_지도.md`
- 사람이 할 일(Unity):
  1. 코드 받고 `error CS` 없는지 확인(클라우드에서는 컴파일 못 돌림).
  2. `Sandbox_Pilot`에서 **Anseong → Setup → Add Network Session**(또는 Rebuild Robot + Pilot Input) → Ctrl+S. `Main`은 Build M1 Main → Ctrl+S. `Network Session` 오브젝트에 `OperatorStartScreen`이 붙는다.
  3. 새 `.cs`의 `.meta`와 저장한 씬을 커밋해 준다.
- 사람이 확인할 것: 위 "사람이 할 일" 7개. 빌드에서 바로 시작하려면 바로가기 대상 뒤에 `-mode host` / `-mode client -ip 192.168.0.10` / `-mode solo`.
- 남은 문제 / 제안:
  - PC-B의 왼쪽 로봇 팔(상대 팔)은 아직 움직이지 않는다(그리기·보간은 M3-02). PC-A의 오른팔도 마찬가지.
  - 클라이언트가 늦게 들어와도 보스 HP·연계는 `NetworkVariable`이라 맞게 보인다. 그 전 사건(소리 등)은 다시 안 나온다(정상).
  - 시작 화면이 Overlay라 헤드셋에는 안 보이고 모니터(미러)에만 보이는 것으로 안다. 헤드셋에서도 보이면 알려 달라(그러면 데스크톱 전용 카메라로 바꾼다).
  - 모드를 바꾸면 그 PC는 보정을 처음부터 한다(맡은 손이 바뀌므로).
  - 재시작 `R`은 어느 PC에서 눌러도 서버가 받는다(운영자 키). 막아야 하면 알려 달라.
