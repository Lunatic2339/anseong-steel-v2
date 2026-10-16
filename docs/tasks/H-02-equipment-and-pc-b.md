# H-02 장비 준비와 PC-B 세팅

- 마일스톤: 10/22 (M0 끝) / 담당: 고건 / 브랜치: 없음(파일 변경 없으면 결과는 총괄 채팅에 보고)
- 선행: 없음

## 목표
두 PC로 2인 협동을 시험할 수 있게 **장비를 갖추고 PC-B를 PC-A와 같은 상태로** 만든다. 코드는 쓰지 않는다.

## 배경
- `docs/design/02_architecture.md`의 "장비"와 "헤드셋 연결": PC-A = 호스트·왼쪽 조종사, PC-B = 클라이언트·오른쪽 조종사. 두 PC는 유선 LAN. 시연은 유선 Link, Air Link는 예비.
- PC-B는 M0-02의 "PC-B에서 같은 확인"이 아직 안 됐다.

## 사람이 할 일 (고건)
1. **구매·대여 목록** (가격·링크를 결과 칸에, 구매 전 선우 확인):
   - USB-C 3.x Link 지원 케이블 약 5m × 3 (PC-A, PC-B, 예비)
   - 랜선 2개 + 작은 스위칭 허브 1개(또는 두 PC 직결용 랜선 1개)
   - Air Link 예비용 Wi-Fi 6 공유기 1개(학교에 있으면 대여)
   - 멀티탭, 케이블 정리용 벨크로
2. **PC-B 세팅** (PC-B를 쓰는 사람과 함께):
   1. 그래픽카드·CPU·RAM 사양을 결과 칸에 적는다(작업 관리자 → 성능).
   2. Unity Hub에 **6000.3.26f1** 설치(Windows Build Support 포함).
   3. Meta Quest Link 앱 설치 → 설정 → 일반 → OpenXR 런타임을 Meta Quest Link로.
   4. 레포 clone(GitHub Desktop 또는 `git clone https://github.com/Lunatic2339/anseong-steel-v2.git`) → Unity Hub에서 열기.
   5. `Assets/_Anseong/Scenes/Main.unity` 열기 → 헤드셋 연결 → Play.
   6. M0-02 확인: 고개를 돌리면 시야가 따라옴, 양손 큐브가 컨트롤러를 따라옴, 프레임 끊김 없음.
3. **두 PC 연결 확인**: 랜선으로 연결 → 각 PC의 IP 확인(명령 프롬프트 `ipconfig`) → 서로 `ping <상대 IP>`가 되는지. Windows 방화벽에서 Unity 에디터 허용(처음 Play할 때 뜨는 창에서 "개인·공용 모두 허용").
4. 결과를 총괄 채팅(선우)에 보고: 사양, M0-02 확인 결과, ping 결과, 구매 목록.

## 하지 말 것
- PC-B에서 프로젝트 설정·패키지 변경. 문제가 생기면 고치지 말고 오류 화면을 찍어 보낸다.

## 완료 기준
- PC-B 헤드셋에서 머리·양손 추적이 보이고, 두 PC가 랜선으로 ping이 된다.
- 구매 목록이 선우 확인을 받았다.

## 결과 (작업 후 작성)
- PC-B 사양:
- M0-02 확인:
- ping:
- 구매 목록:
