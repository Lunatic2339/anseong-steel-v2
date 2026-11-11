# 윤주 — ElevenLabs 효과음, Suno 배경음악

[← 윤주 처음으로](README.md) · 사령관 대사: [AI 도구 ② 사령관 대사](AI_도구_2_사령관_대사.md) · 효과음 이름표: [S-01](S-01_효과음.md) · 전체 도구 표: [13 AI 도구](../../design/13_ai_tools.md)

S-01(효과음 모으기)을 그대로 이어 갑니다. 달라진 것은 **무료 사이트에 없는 소리를 AI로 만들 수 있게 된 것**과 **배경음악을 Suno로 만드는 것**입니다. 둘 다 웹에서 글만 쓰면 되고 Unity는 필요 없습니다. 자르기·크기 맞추기·게임에 넣기는 코드 담당이 합니다.

## 공통 규칙
- 계정은 선우에게서 받습니다. 비밀번호는 어디에도 적지 않습니다.
- 파일 이름은 표 그대로 + 번호(`SFX_DroneShot_01.wav`). 이름이 맞으면 코드가 알아서 찾습니다.
- 드라이브 `안성제철_소리`(`SFX`, `BGM`, `Voice`, `후보`) + `SOURCES`에 한 줄: `파일 / 도구(플랜) / 날짜 / 프롬프트`.
- 프롬프트에 실제 게임·영화·가수 이름을 쓰지 않습니다.

## ElevenLabs 효과음

**언제**: freesound에서 CC0로 찾을 수 있으면 그게 먼저입니다(진짜 소리가 더 묵직함). 5분 찾아도 없거나, 드론·스캐너·해킹처럼 **세상에 없는 소리**면 ElevenLabs로 만듭니다.

순서
1. elevenlabs.io → 왼쪽 **Sound Effects**.
2. 영어로 설명을 씁니다. 아래 "잘 되는 말"을 섞으세요.
3. 길이(초)를 표대로 정하고 만들기 → 후보 여러 개를 들어 보고 고릅니다.
4. 다운로드 → 이름 바꾸기 → 드라이브 `SFX` + `SOURCES`.

잘 되는 말: `huge`, `heavy`, `low`, `metallic`, `giant robot`, `distant`, `industrial`, 그리고 꼭 `no music, no voice`. 거대한 느낌은 "크고 낮고 멀리서 울리는" 소리에서 납니다.

| 파일 이름 | 언제 나는 소리 | 예시 프롬프트 | 길이 |
|---|---|---|---|
| `SFX_DroneShot` | 숨은 드론이 쏨 | `small energy cannon shot from a distance, sharp zap with metallic echo, no music` | 1초 |
| `SFX_DroneExplode` | 드론 격추 | `small flying machine explodes, metal debris falling, heavy, no music` | 2초 |
| `SFX_ScanFound` | 스캐너가 드론을 찾음 | `sci-fi scanner lock-on beep, rising two tones, clean, no music` | 1초 |
| `SFX_ScanLost` | 드론을 놓침 | `sci-fi scanner lost signal, falling tone, short, no music` | 1초 |
| `SFX_CannonFire` | 우리 포 발사 | `giant robot arm cannon fires, huge low boom, metallic recoil clank, no music` | 2초 |
| `SFX_EquipClank` | 무기 장착 | `heavy mechanical clamp locks onto giant robot arm, hydraulic hiss, metal clank` | 2초 |
| `SFX_GuardStrain` | 방패로 버팀 | `huge metal shield under pressure, grinding and creaking steel, low groan` | 2초 |
| `SFX_HackNoise` | 팔이 해킹됨 | `electronic interference, glitchy digital noise, radio static, unsettling` | 3초 |
| `SFX_HackClear` | 해킹 풀림 | `machine system reboot chime, short clean confirmation, no music` | 1초 |
| `SFX_Siren` | 무기 파손 경보 | `industrial alarm siren inside a cockpit, urgent, looping` | 4초 |
| `SFX_MapImpact` | 코일 떨어짐·박힘 | `giant steel coil crashes onto metal, massive impact, debris, distant echo` | 3초 |
| `SFX_WarnNormal` | 보스 일반 공격 예고 | `single low warning tone, mechanical, short` | 1초 |
| `SFX_WarnHeavy` | 보스 강공격 예고 | `two deep warning horns, heavier, mechanical` | 1.5초 |
| `SFX_BeamCharge` | 필살기 충전 | `huge energy charging up, rising hum, electric crackle` | 3초 |
| `SFX_BeamFire` | 필살기 광선 | `massive energy beam blast, deep roar of power, long tail` | 3초 |
| `SFX_SceneSting` | 장면 바뀜 | `short dramatic metallic hit with reverb tail, no melody` | 2초 |

예고음 셋(일반·강공격·막기 불가)은 **서로 확실히 다르게** 들려야 합니다. 나머지 이름은 [S-01](S-01_효과음.md) 표를 따릅니다.

## Suno 배경음악

**꼭 지킬 것**: 우리 플랜은 Suno **Pro**(한 달 구독)라 상업 사용이 됩니다. 구독이 끝나면 새로 받은 곡은 못 쓰니 **12/08 전에 다 받습니다**. Suno는 유료 플랜(Pro·Premier)으로 만든 곡만 쓸 수 있고, **곡 화면의 다운로드 버튼으로 받은 파일만** 씁니다(화면 녹음·스트리밍 녹음 금지). 다운로드 횟수에 월 한도가 있다고 하니, 듣기는 웹에서 하고 **고른 곡만** 받습니다.

순서
1. suno.com → **Create** → 가사 없는 곡으로(**Instrumental** 켜기).
2. 스타일 칸에 아래 예시처럼 영어로. 곡마다 2~4개 만들어 들어 봅니다.
3. 고른 곡을 다운로드(WAV가 있으면 WAV) → 이름 바꾸기 → 드라이브 `BGM`.
4. 후보를 단톡방에 올려 같이 고릅니다(11/18 후보, 11/25 확정).

| 파일 이름 | 언제 | 예시 스타일 | 길이 |
|---|---|---|---|
| `BGM_Opening` | 오프닝(대기·기체 점검) | `ambient industrial, low drones, slow pulse, tense calm, instrumental` | 2~3분 |
| `BGM_Battle` | 드론 사냥~검·방패 근접전 | `epic orchestral industrial, heavy taiko drums, low brass, metallic percussion, 100 bpm, instrumental` | 3분 |
| `BGM_Bare` | 무기 다 잃고 맨손 | `desperate heroic, pounding drums, rising strings, instrumental` | 2~3분 |
| `BGM_Ending` | 엔딩(승리 뒤 결과판) | `triumphant short brass theme, instrumental` | 1분 |

- 음악은 대사·효과음보다 작게 깔리니 **너무 바쁘지 않은 곡**이 좋습니다. "육중한 로봇" 느낌: 빠른 전자음보다 느리고 무거운 북.
- 끝과 처음이 자연스럽게 이어지면 좋습니다(반복 재생). 아니면 코드 담당이 페이드로 잇습니다.

## 마감
| 날짜 | 할 일 |
|---|---|
| 11/14 | ElevenLabs 효과음 3개(드론 사격·스캐너 찾음·포 발사) 시험 |
| 11/18 | 위 표 효과음 1차, BGM 곡마다 후보 2개 |
| 11/25 | 효과음 2차(헤드셋으로 들어 본 뒤 고칠 것), BGM 확정 4곡 |
