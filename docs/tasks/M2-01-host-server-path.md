# M2-01 1인 양팔을 호스트로 실행 + 서버 전투 경로

- 마일스톤: M2 / 담당: 선우 / 브랜치: `feat/m2-01-host-server-path`
- 선행: M1-05 병합 (안 됐으면 `feat/m1-05-punch-hit-boss-hp` 위에서 시작)
- 기한(제안): 10/31

## 목표
1인 양팔 모드를 처음부터 **NGO 호스트**로 실행하고, M1-05의 명중 보고가 서버 경로(`HitReport` → 서버 → 결과 이벤트)를 지나게 한다. M2 나머지(보스 공격·페이즈·필살기)를 모두 이 서버 경로 위에 올려서, M3에서 PC-B만 붙이면 되게 한다.

## 배경
- `docs/design/02_architecture.md` "누가 무엇을 계산하나", "1인 양팔 모드"(같은 코드를 한 PC가 호스트로 실행, 오프라인 전용 규칙 금지).
- CLAUDE.md 고정 결정: Unity Transport 직결, 포트 7777, 자기 팔은 로컬 입력으로 즉시 그림, 서버 판정에 `Time.time` 금지(`NetworkManager.ServerTime`).
- M1-05 결과: 판정은 순수 클래스 `CombatResolver`, `CombatDirector.Hit`이 "M3에서 RPC 자리". 결과는 `CombatEvent`(`CombatCue`)로만 나감.
- 패키지 `com.unity.netcode.gameobjects` 2.13.3은 이미 들어 있다.
- 2인 접속(IP 입력, 3번째 거절, 소유 검사)은 M3. 이번에는 호스트 하나만.

## Claude가 할 일
1. `Runtime/Net/`에 `SessionBootstrap`: 씬 시작 시 `NetworkManager` + `UnityTransport`(127.0.0.1:7777)로 `StartHost`. Inspector `Mode`(SoloBothArms 기본 / Host / Client, Host·Client는 M3에서 채움). 시작 실패하면 안내판에 이유를 띄운다.
2. `CombatServer`(NetworkBehaviour, 서버에서만 규칙 실행): `CombatResolver`를 서버가 소유. 때린 쪽은 `SubmitHit(HitReport)` 하나만 부른다(호스트에서는 로컬 호출과 같은 경로, M3에서 ServerRpc로 그대로). 판정 시각은 `NetworkManager.ServerTime.Time`.
3. 결과 복제: 보스 HP는 `NetworkVariable`, 전투 이벤트는 서버 → 모든 클라이언트 이벤트 RPC. 각 PC의 `CombatCuePlayer`·보스 반응·안내판은 지금처럼 이벤트만 듣는다(듣는 쪽 코드는 바꾸지 않는 것이 목표).
4. `CombatDirector`에서 규칙 실행을 빼고, 로컬 판정(분류·접촉·공격 보정)과 보고만 남긴다. 그립 잠금·발사 소리처럼 서버를 기다리면 늦는 것은 지금처럼 로컬 즉시 재생.
5. 프리팹·씬 구성은 에디터 메뉴로: **Anseong → Setup → Add Network Session**(`Sandbox_Pilot`에 NetworkManager·`CombatServer`·보스 `NetworkObject` 추가). 기존 Rebuild 메뉴가 이것도 부르게.
6. 재시작(운영자 `R`): 서버가 `CombatResolver.Reset()` → HP·연계 복제 초기화. 이전 판 상태가 남지 않는다.
7. EditMode 테스트: 같은 `HitReport` 두 번 → 한 번만, 서버 시각 기준 연계 시간 초과. (NGO 자체는 테스트하지 않는다.)

## 사람이 할 일 (헤드셋)
- [ ] 메뉴 실행 → Play → Console에 "Host started" 한 줄, `error CS` 없음.
- [ ] M1-05 확인 항목(명중·연계·부스트·휘청·HP 막대)이 그대로 된다.
- [ ] 자기 팔이 M1과 똑같이 즉시 따라온다(늦거나 끊기면 실패).
- [ ] `R`로 재시작하면 HP·연계가 처음으로 돌아간다. 3번 반복.

## 하지 말 것
- PC-B 접속, IP 입력 화면, 팔·머리 자세 동기화(M3). 보스 공격(M2-02 이후). 규칙 수치 변경.

## 완료 기준
- 1인 양팔 모드가 호스트로 돌고, 명중이 서버 경로를 지난다. M1 기능이 하나도 깨지지 않는다(헤드셋 확인).

## 결과 (작업 후 Claude가 작성)
- 바뀐 파일:
  - 새로: `Runtime/Combat/CombatAuthority.cs`(서버 규칙 묶음, 서버 시각), `Runtime/Combat/CombatSnapshot.cs`(복제 상태 + `ICombatLink` 보고 통로), `Runtime/Net/SessionBootstrap.cs`, `Runtime/Net/CombatServer.cs`, `Runtime/Net/NetCombatTypes.cs`, `Editor/NetworkSessionMenu.cs`, `Tests/EditMode/CombatAuthorityTests.cs`
  - 고침: `CombatDirector.cs`(규칙 실행 뺌 → 보고만. 명중·헛침·그립/트리거 바뀜·재시작 요청을 서버로, 결과는 서버 이벤트로 받아 `Cue`로 냄. 그립 잠금·발사는 지금처럼 바로), `BossHealthBar.cs`·`TestGuidePanel.cs`(`Resolver` 대신 `State` 읽기, 세션 실패·서버 미연결 빨간 줄), `SceneSetupMenu.cs`·`MainSceneMenu.cs`(Add Network Session 같이 부름), asmdef 2개(`Unity.Networking.Transport` 참조)
  - `SessionRestart`(R)는 그대로: `director.ResetBoss()` → 서버 초기화 RPC.
- 사람이 할 일(Unity):
  1. Unity에서 코드 받고 `error CS` 없는지 확인(클라우드에서는 컴파일을 못 돌림).
  2. `Sandbox_Pilot` 열고 **Anseong → Setup → Rebuild Robot + Pilot Input** → Ctrl+S. `Main`은 **Build M1 Main** → Ctrl+S. 새로 생긴 `Network Session`·`Combat Server` 오브젝트가 씬에 들어간다.
  3. Unity가 새 .cs의 `.meta`를 만든다. 그 `.meta`들과 저장한 씬을 커밋해 준다(클라우드는 .meta를 만들 수 없음).
- 사람이 확인할 것(헤드셋): 지시서 위 4개 그대로. 추가로 Console에 `[Session] Host started` 한 줄, 안내판에 빨간 "전투 서버 연결 안 됨"이 없는지.
- 남은 문제 / 제안:
  - 조절 메뉴의 연계·부스트 시간은 호스트 PC의 `CombatDirector` 설정을 서버가 같이 쓴다. M3에서 PC-B가 바꾼 값은 서버에 안 간다(서버 PC에서만 조절).
  - 명중 CSV(`strikes_*.csv`)의 "명중" 칸은 이제 "서버에 명중으로 보고함"이다. 보스 HP 0 뒤의 보고처럼 서버가 버린 것도 1로 남는다.
  - 팔 소유 검사(보낸 PC = 그 팔 주인)는 `CombatServer`의 RPC 자리에 주석으로 남김(M3-01).
  - 조종사 한 명에게만 보내기: `CombatServer.SendToPilot(side, e)`. 1인 양팔은 두 쪽 다 이 PC로 간다.
