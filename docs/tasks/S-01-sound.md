# S-01 효과음·배경음·음성

- 마일스톤: 1차 10/28 (중간발표), 2차 11/19 / 담당: 윤주 / 브랜치: 없음 (파일을 받은 선우의 세션이 `feat/sunwoo-sound`로 넣음)
- 선행: 없음. 지금 소리 자료는 하나도 없다.

## 목표
타격감과 거대감을 살릴 소리를 모은다. 소리를 재생하는 코드는 선우·Claude가 만든다. 여기서는 **파일을 고르고 이름을 맞춰 넘기는 것**까지.

## 배경
- 거대 로봇이므로 무겁고 낮은 소리(쇳덩이가 부딪히는 쾅, 유압·서보 모터 소리)가 기본.
- 시연장은 시끄럽다. 효과음은 짧고 또렷해야 한다.

## 파일 규칙
- 형식: WAV(또는 OGG), 44.1kHz 또는 48kHz. 효과음은 **모노**, 배경음은 스테레오.
- 앞뒤 무음은 잘라낸다. 효과음은 대부분 2초 이내.
- 이름: `SFX_<무엇>_<번호>.wav` (예: `SFX_PunchHit_01.wav`). 같은 소리를 2~3개 변형으로 받으면 반복감이 줄어든다.
- 위치: `Assets/_Anseong/Audio/SFX/`, `Audio/BGM/`, `Audio/Voice/`. 출처·라이선스는 `Audio/SOURCES.md`.
- 추천 출처: freesound.org(라이선스 필터를 **CC0**으로), Sonniss GDC 무료 번들, Kenney 오디오(CC0). 배경음은 저작권 표시가 필요 없는 무료 음원만.

## 목록

**어느 동작에 어떤 소리를 쓰는지는 `docs/design/06_feel_and_sound.md`가 기준이다.** 아래 표는 그 표에서 뽑은 파일 목록이다. 검색어는 영어로, 하나씩 넣어 본다.

고르는 기준: 시작이 또렷하고 짧을 것, 말소리·음악·잡음이 섞이지 않을 것. **사람 크기 소리여도 된다**(Unity에서 피치를 낮춰 거대하게 만든다. 드럼통을 망치로 치는 소리도 훌륭한 로봇 펀치가 된다).

### 조종사 로봇: 공격

| 언제 | 파일 이름 | 어떤 소리 (예시) | 검색어 | 길이·개수 | 차수 |
|---|---|---|---|---|---|
| 그립 쥠 | `SFX_GripLock` | 기계가 잠기는 "철컥". 예: 총 장전, 금속 걸쇠 | metal latch, mechanical lock, gun cock | 0.2~0.4초, 2 | 1차 |
| 그립 놓음 | `SFX_GripRelease` | 공기 빠지는 "치익". 예: 버스 문 | air release hiss, pneumatic release | 0.3~0.6초, 1 | 2차 |
| 잽 발사 | `SFX_JabLaunch` | 피스톤이 쏘아지는 "피슉/철컥". 예: 공압 실린더, 네일건 | piston, pneumatic punch, nail gun | 0.2~0.4초, 2 | 1차 |
| 잽 명중 | `SFX_PunchHit` | 두꺼운 철판을 큰 망치로 친 "쿠웅" + 낮은 울림. 예: 드럼통 망치질, 자동차 문 세게 닫기 | metal impact heavy, anvil hit, big metal hit | 0.5~1.5초, 3 | 1차 |
| 훅 스윙 | `SFX_HookSwing` | 낮은 바람 "후우웅" + 관절 기어 "끼긱" | heavy swing whoosh, gear whir | 0.5~0.8초, 2 | 1차 |
| 훅 명중 | `SFX_HookHit` | 금속이 찌그러지는 "콰직" + 파편. 예: 자동차 충돌 | metal crash, metal crunch, car crash | 0.8~1.5초, 2 | 1차 |
| 헛침 | `SFX_PunchWhoosh` | 바람만 "휘익". 예: 야구 방망이 헛스윙 | whoosh heavy, swing whoosh | 0.4~0.8초, 2 | 1차 |
| 연계 단계 오름 | `SFX_ComboStep` | 짧은 상승 신호 "딩↗". 2단계용·3단계용 높이가 다른 것 2개 | ui rise, sci-fi chime, power up short | 0.3초, 2 | 2차 |
| 부스트 가능 신호 | `SFX_BoostReady` | 짧게 차오르는 "위잉!" | charge up short, energy rise | 0.4초, 1 | 2차 |
| 부스터 분사 | `SFX_Boost` | 로켓·제트 "쿠와앙". 예: 로켓 발사 순간 | rocket thrust, jet burst, afterburner | 1~2초, 1 | 2차 |
| 부스트 명중 | `SFX_BoostHit` | 폭발 + 금속 충돌 + 저음 붐. 가장 크고 시원한 소리 | explosion metal, impact explosion heavy, sub boom | 1.5~2.5초, 1 | 2차 |
| 가드로 막음 | `SFX_Guard` | 둔탁한 "텅", 울림은 짧게. 예: 철문을 주먹으로 침 | metal thud, shield block, heavy door slam | 0.3~0.8초, 2 | 1차 |
| 가드로 버팀 | `SFX_GuardStrain` | 팔 관절 과부하 "끼이익". 예: 무거운 철문 경첩 | metal strain, hydraulic strain, metal creak short | 0.5~1초, 2 | 2차 |
| 팔을 움직임 | `SFX_ArmServo` | 유압·모터 "위잉-치익". 예: 굴착기 팔 | servo motor, hydraulic, robot arm movement | 0.5~1.5초, 3 | 1차 |
| 맞음 | `SFX_RobotHit` | 조종석 안 충격 "쿵" + 부품 떨림. 예: 배 선체에 부딪힘 | metal impact interior, ship hull impact | 0.8~1.5초, 2 | 2차 |
| HP 낮음 | `SFX_AlarmLowHP` | 조종석 경보 "삐—삐—" 반복 | alarm beep loop, warning beep | 반복, 1 | 2차 |

### 조종사 로봇: 이동

| 언제 | 파일 이름 | 어떤 소리 (예시) | 검색어 | 길이·개수 | 차수 |
|---|---|---|---|---|---|
| 이동 승인(두 스틱 일치) | `SFX_MoveConfirm` | "삑삑" 두 번. 호흡이 맞았다는 신호 | double beep confirm, ui confirm | 0.3초, 1 | 2차 |
| 걸음(접근·후퇴) | `SFX_Footstep` | 멀리서 울리는 "쿠웅…". 예: 큰 물체가 땅에 떨어짐 | giant footstep, heavy stomp, boom impact low | 1~2초, 3 | 1차 |
| 걸음 앞 다리 유압 | `SFX_LegHydraulic` | 짧은 "치익". 예: 증기 빠지는 소리 | hydraulic hiss, steam release short | 0.3~0.5초, 2 | 2차 |
| 회피 분사 | `SFX_DodgeBurst` | "피슉!" 가스 분사 | thruster burst, gas burst | 0.3~0.5초, 1 | 2차 |
| 회피 끌림 | `SFX_DodgeSlide` | 바닥에 끌리는 "드르륵" | metal scrape slide, heavy drag short | 0.5~0.8초, 1 | 2차 |
| 몸 회전 | `SFX_TorsoTurn` | 낮은 기어 "끼기기긱". 예: 탱크 포탑 | gear grinding low, turret rotate | 1초 반복, 1 | 2차 |
| 가만히 있을 때 | `AMB_CockpitHum` | 조종석 안 낮은 "웅—"(엔진·냉각팬) | spaceship interior hum, engine room ambience, machine hum loop | 30초 반복, 1 | 1차 |

### 보스

| 언제 | 파일 이름 | 어떤 소리 (예시) | 검색어 | 길이·개수 | 차수 |
|---|---|---|---|---|---|
| 맞고 휘청 | `SFX_BossStagger` | 금속이 삐걱대는 "끼이익" + "쿠웅". 예: 낡은 철문 | metal creak, metal groan, metal stress | 1~2초, 2 | 1차 |
| 일반 공격 예고 | `SFX_WarnNormal` | 짧고 높은 "삐빅" | warning beep short, alert beep | 0.3초, 1 | 2차 |
| 강공격 예고 | `SFX_WarnHeavy` | 낮고 긴 "부웅—". 예: 배 고동 | low warning horn, foghorn | 1초, 1 | 2차 |
| 가드 불가 예고 | `SFX_WarnUnblockable` | 급박한 사이렌. 세 경고음이 확실히 달라야 함 | siren alarm, klaxon | 1.5~2초, 1 | 2차 |
| 주먹 휘두름 | `SFX_BossSwing` | 낮고 묵직한 "후우웅" | heavy swing whoosh, large object swing | 0.5~1초, 2 | 2차 |
| 두 손 내려찍기 | `SFX_BossSlam` | 땅이 울리는 "콰르르" + 돌 부서짐. 예: 지진, 철거 현장 | ground impact, earthquake boom, rock smash | 1.5~2.5초, 1 | 2차 |
| 돌진 | `SFX_BossCharge` | 엔진 굉음 + 빠른 발소리. 예: 탱크가 달려옴 | engine rev heavy, rumble, tank engine | 2초, 1 | 2차 |
| 크게 밀려남 | `SFX_BossKnockback` | 쇳덩이가 끌리는 "드드드득" | metal drag, metal scrape long | 1~1.5초, 1 | 2차 |
| 페이즈 전환 포효 | `SFX_BossRoar` | 기계 괴물의 "크아아". 예: 동물 포효 + 기계음 | robot roar, mechanical growl, monster roar | 2초, 1 | 2차 |
| 쓰러짐 | `SFX_BossDown` | 거대한 금속이 무너지는 "콰르르쾅" | metal collapse, big crash debris | 2~3초, 1 | 2차 |

### 필살기·환경·흐름

| 언제 | 파일 이름 | 어떤 소리 (예시) | 검색어 | 길이·개수 | 차수 |
|---|---|---|---|---|---|
| 공장 배경 | `AMB_Factory` | 먼 기계 소리, 쇠 부딪히는 소리, 웅웅거림 | factory ambience, industrial ambience, steel mill | 30초 이상, 1 | 1차 |
| 높은 곳 바람 | `AMB_Wind` | 조종석 밖 강한 바람 "휴우우" | wind howling, strong wind | 30초 이상, 1 | 1차 |
| 전투 배경음 | `BGM_Battle` | 웅장하고 빠른 오케스트라나 묵직한 전자음. 가사 없음 | epic battle, orchestral action, cinematic drums | 2분 이상, 반복 가능 | 1차 |
| 필살기 충전 | `SFX_BeamCharge` | 점점 높아지는 "위이이잉" | energy charge, power up, laser charging | 1.5초, 1 | 2차 |
| 필살기 광선 | `SFX_BeamFire` | 굵고 지속되는 "부우우웅" | laser beam loop, energy beam | 3초 이상 반복, 1 | 2차 |
| 구조물 부서짐 | `SFX_StructureCollapse` | 철골이 휘며 무너지는 소리 + 파편 | building collapse, metal debris fall | 2~3초, 2 | 2차 |
| 출격: 정비고 문 | `SFX_HangarDoor` | 거대한 철문이 끌리며 열림 | hangar door, large metal door opening | 3~5초, 1 | 2차 |
| 출격: 경보 | `SFX_Siren` | 공습 경보 같은 사이렌 | air raid siren, alarm siren | 3초, 1 | 2차 |
| 기체 점검 단계 완료 | `SFX_UI_Confirm` | 짧고 깔끔한 "띠링" | ui confirm sci-fi, positive beep | 0.3초, 1 | 2차 |
| 결과 화면 | `BGM_Result` | 승리 느낌의 짧은 음악 | victory fanfare, win jingle | 10~20초, 1 | 2차 |
| 사령관 대사 | `VO_Commander_01~10` | 아래 대사 10줄, 팀원 녹음 | (직접 녹음) | 줄당 1 | 2차 |

배경음이 freesound에 없으면 무료 음악 사이트를 써도 된다. 그 사이트의 라이선스 페이지를 캡처해 SOURCES에 넣는다.

사령관 대사(제안, 바꿔도 됨): 1 "조종사, 담당 팔을 들어 연결을 확인하라." 2 "편한 가드 자세에서 그립." 3 "정면으로 끝까지 뻗고 그립." 4 "표적을 쳐라." 5 "양팔 가드. 출격 준비 완료." 6 "출격한다!" 7 "적의 공격이 온다, 막아라!" 8 "지금이다, 반격하라!" 9 "적이 무릎 꿇었다. 둘이 함께 팔을 뻗어라!" 10 "작전 성공."
- 녹음: 팀원이 조용한 곳에서 휴대폰으로 녹음해도 된다(무전기 효과는 Claude가 넣음). TTS를 쓰면 상업·전시 이용이 허용되는지 확인하고 `SOURCES.md`에 적는다.

## 사람이 할 일 (윤주)
**Unity·Git은 쓰지 않아도 된다.** 내 컴퓨터 폴더(또는 구글 드라이브)에 모아서 폴더째 선우에게 넘기면, 선우의 Claude Code가 레포에 넣는다.

1. 구글 드라이브에 `안성제철_소리` 폴더를 만들고 안에 `SFX`, `BGM`, `Voice` 폴더를 만든다.
2. freesound.org에 가입 → 검색창에 영어로 검색(예: `metal impact`, `robot servo`, `heavy footstep`) → 왼쪽 필터 License에서 **Creative Commons 0** 선택 → 들어보고 마음에 들면 Download.
3. 받은 파일 이름을 위 표의 이름 + 번호로 바꾼다(예: `SFX_PunchHit_01.wav`, `_02`, `_03`). WAV가 아니어도 괜찮다(MP3면 Claude가 바꾼다).
4. `SOURCES` 문서(구글 문서나 메모장)에 한 줄씩 적는다: `파일 이름 / 사이트 / 원래 제목 / 만든 사람 / 라이선스 / 주소`.
5. 후보가 여러 개면 2~3개씩 받아 두고, 선우와 같이 들어보고 고른다.
6. 폴더 링크를 선우에게 보낸다.
7. 2차 목록과 대사 녹음은 11/19까지 같은 방법으로.

## 하지 말 것
- 출처나 라이선스를 모르는 소리(유튜브에서 뜬 소리, 게임에서 추출한 소리 등).
- 재생 코드 작성, `Main.unity` 수정.

## 완료 기준
- 1차 목록 파일이 규칙대로 들어오고 `SOURCES.md`에 모두 출처가 있음.

## 결과 (작업 후 작성)
- 바뀐 파일:
- 남은 문제 / 제안:
