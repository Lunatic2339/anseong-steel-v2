// 안성제철 포스피드백 장치 펌웨어 (초안, 2026-11-23)
//
// 부품: 스테퍼 모터 17HS3430 + 드라이버 L298N + 각도 센서 AS5600, 보드는 ESP32.
// 하는 일: PC(게임)가 시리얼로 "저항" 명령을 보내면 모터의 두 코일에 전기를 넣어 축을 붙든다.
//          그 붙드는 힘이 팔을 펼 때 느끼는 저항이다. 세게 당기면 축이 미끄러지므로 팔이 갇히지 않는다.
//
// PC와의 약속 (게임 쪽 ExoProtocol.cs와 같다. 115200bps, 한 줄 ASCII, 줄 끝 \n)
//   PC → 장치   P,<ms>             준비: 느슨한 줄을 약하게 감아 둔다. ms 안에 R이 안 오면 다시 푼다 (최대 3000)
//               R,<세기 0-255>,<ms> 저항: 그 세기로 ms 동안 붙든다 (최대 500)
//               X                  즉시 해제
//               H                  살아 있음 (PC가 0.1초마다 보낸다)
//   장치 → PC   S,<상태>,<출력>,<오류>  1초에 10번. 상태는 IDLE, PRE, RESIST, RELEASE, SAFE
//   0.2초 동안 아무 명령도 안 오면 SAFE가 되어 출력을 끈다. 명령이 다시 오면 IDLE로 돌아간다.
//
// 아직 확인하지 못한 것 (실물로 해 봐야 한다)
//   - ESP32(esp32:esp32 4.0.0, 일반 ESP32 개발 보드)와 Arduino Uno로 컴파일까지만 확인했다. 실물 보드에는 올려 보지 않았다.
//   - 핀 번호는 임시로 잡은 것이다. 실제 배선에 맞게 아래 "배선" 상수를 고친다.
//   - 어느 쪽이 "팔을 펴는 방향"인지는 조립에 따라 다르다. EXTEND_SIGN과 REEL_FORWARD를 맞춘다.
//   - 세기 값과 실제 힘(kg)의 관계는 재 보지 않았다.

#include <Wire.h>

// ───────── 배선 (실제 배선에 맞게 고친다) ─────────
#if defined(ESP32)
const int PIN_ENA = 25;   // L298N ENA: 코일 A 세기(PWM). 점퍼를 뽑고 연결한다
const int PIN_IN1 = 26;   // L298N IN1
const int PIN_IN2 = 27;   // L298N IN2
const int PIN_ENB = 33;   // L298N ENB: 코일 B 세기(PWM). 점퍼를 뽑고 연결한다
const int PIN_IN3 = 32;   // L298N IN3
const int PIN_IN4 = 14;   // L298N IN4
// AS5600은 I2C: SDA 21, SCL 22 (ESP32 기본), 전원 3.3V
#else                      // Arduino Uno 등 (문법 확인과 책상 시험용)
const int PIN_ENA = 5;
const int PIN_IN1 = 7;
const int PIN_IN2 = 8;
const int PIN_ENB = 6;
const int PIN_IN3 = 9;
const int PIN_IN4 = 10;
// AS5600은 I2C: SDA A4, SCL A5
#endif

// ───────── 조절하는 값 ─────────
const int MAX_PWM = 255;            // 세기 상한. 모터나 드라이버가 뜨거우면 낮춘다
const int PRE_PWM = 90;             // 준비(줄 감기) 때의 약한 세기
const unsigned int PRE_STEP_INTERVAL_MS = 4;  // 준비 때 한 칸 감는 간격. 작을수록 빨리 감는다
const bool PRE_REELS = true;        // false면 준비 때 감지 않고 약하게 붙들기만 한다
const bool REEL_FORWARD = true;     // 줄을 감는 회전 방향. 반대로 돌면 false
const int EXTEND_SIGN = 1;          // 팔을 펼 때 각도가 늘면 1, 줄면 -1
const bool RESIST_ONLY_WHEN_EXTENDING = true;  // true면 팔을 거둘 때는 저항을 끈다
const int MOVE_DEADBAND = 4;        // 이 칸 수(4096칸 = 한 바퀴)보다 작게 움직이면 멈춘 것으로 본다
const bool SEND_ANGLE = false;      // true면 "A,<누적 각도 도>" 줄도 보낸다 (게임은 무시한다. 시험용)

const unsigned long MAX_RESIST_MS = 500;
const unsigned long MAX_PREPARE_MS = 3000;
const unsigned long WATCHDOG_MS = 200;
const unsigned long RELEASE_MS = 50;
const unsigned long STATUS_INTERVAL_MS = 100;
const unsigned long SENSOR_INTERVAL_MS = 5;

// ───────── AS5600 ─────────
const uint8_t AS5600_ADDRESS = 0x36;
const uint8_t AS5600_RAW_ANGLE = 0x0C;   // 두 바이트, 0~4095

enum State { IDLE, PRE, RESIST, RELEASE, SAFE };
const char* const STATE_NAMES[] = { "IDLE", "PRE", "RESIST", "RELEASE", "SAFE" };

State state = SAFE;
int output = 0;                 // 지금 모터에 넣는 세기(0~255)
int errorCode = 0;              // 0 정상, 1 각도 센서가 응답하지 않음
int resistStrength = 0;
unsigned long stateUntil = 0;   // PRE, RESIST, RELEASE가 끝나는 시각
unsigned long lastCommandAt = 0, lastStatusAt = 0, lastSensorAt = 0, lastStepAt = 0;
int stepIndex = 0;              // 지금 붙들고 있는 코일 조합(0~3)

int lastRaw = -1;               // 마지막으로 읽은 각도(0~4095), 아직 못 읽었으면 -1
long totalCounts = 0;           // 켠 뒤로 누적된 회전(칸)
int motion = 0;                 // 1 펴는 중, -1 거두는 중, 0 멈춤

char line[32];
uint8_t lineLength = 0;

// 코일 조합 네 가지(한 칸씩 도는 순서). 붙들 때는 조합을 바꾸지 않는다.
void applyCoils() {
  bool aForward = (stepIndex == 0 || stepIndex == 3);
  bool bForward = (stepIndex == 0 || stepIndex == 1);
  digitalWrite(PIN_IN1, aForward ? HIGH : LOW);
  digitalWrite(PIN_IN2, aForward ? LOW : HIGH);
  digitalWrite(PIN_IN3, bForward ? HIGH : LOW);
  digitalWrite(PIN_IN4, bForward ? LOW : HIGH);
}

void setOutput(int value) {
  if (value < 0) value = 0;
  if (value > MAX_PWM) value = MAX_PWM;
  output = value;
  analogWrite(PIN_ENA, value);
  analogWrite(PIN_ENB, value);
}

void enter(State next, unsigned long durationMs) {
  state = next;
  stateUntil = millis() + durationMs;
  if (next == IDLE || next == SAFE || next == RELEASE) setOutput(0);
}

// 각도 센서를 읽는다. 못 읽으면 -1.
int readRawAngle() {
  Wire.beginTransmission(AS5600_ADDRESS);
  Wire.write(AS5600_RAW_ANGLE);
  if (Wire.endTransmission(false) != 0) return -1;
  if (Wire.requestFrom((int)AS5600_ADDRESS, 2) != 2) return -1;
  int high = Wire.read();
  int low = Wire.read();
  return ((high & 0x0F) << 8) | low;
}

void updateSensor() {
  int raw = readRawAngle();
  if (raw < 0) {
    errorCode = 1;
    motion = 0;   // 센서가 없으면 방향을 모르므로 멈춘 것으로 본다(저항은 그대로 건다)
    return;
  }
  errorCode = 0;
  if (lastRaw >= 0) {
    int delta = raw - lastRaw;
    if (delta > 2048) delta -= 4096;    // 한 바퀴 넘어가는 곳 처리
    if (delta < -2048) delta += 4096;
    totalCounts += delta;
    int signedDelta = delta * EXTEND_SIGN;
    if (signedDelta > MOVE_DEADBAND) motion = 1;
    else if (signedDelta < -MOVE_DEADBAND) motion = -1;
    else motion = 0;
  }
  lastRaw = raw;
}

void handleLine() {
  line[lineLength] = '\0';
  lineLength = 0;
  char command = line[0];
  if (command == '\0') return;
  unsigned long now = millis();
  bool wasSafe = (state == SAFE);

  if (command == 'H') {
    lastCommandAt = now;
    if (wasSafe) enter(IDLE, 0);
  } else if (command == 'X') {
    lastCommandAt = now;
    enter(RELEASE, RELEASE_MS);
  } else if (command == 'P' && line[1] == ',') {
    long ms = atol(line + 2);
    if (ms < 1) return;
    if ((unsigned long)ms > MAX_PREPARE_MS) ms = MAX_PREPARE_MS;
    lastCommandAt = now;
    applyCoils();
    enter(PRE, (unsigned long)ms);
    setOutput(PRE_PWM);
    lastStepAt = now;
  } else if (command == 'R' && line[1] == ',') {
    char* comma = strchr(line + 2, ',');
    if (comma == NULL) return;
    long strength = atol(line + 2);
    long ms = atol(comma + 1);
    if (ms < 1) return;
    if (strength < 0) strength = 0;
    if (strength > 255) strength = 255;
    if ((unsigned long)ms > MAX_RESIST_MS) ms = MAX_RESIST_MS;
    lastCommandAt = now;
    resistStrength = (int)strength;
    applyCoils();
    enter(RESIST, (unsigned long)ms);
    setOutput(resistStrength);
  }
  // 모르는 줄은 버린다(살아 있음 신호로 치지 않는다).
}

void readSerial() {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();
    if (c == '\r') continue;
    if (c == '\n') { handleLine(); continue; }
    if (lineLength < sizeof(line) - 1) line[lineLength++] = c;
    else lineLength = 0;   // 너무 긴 줄은 버린다
  }
}

void sendStatus() {
  Serial.print("S,");
  Serial.print(STATE_NAMES[state]);
  Serial.print(',');
  Serial.print(output);
  Serial.print(',');
  Serial.println(errorCode);
  if (SEND_ANGLE) {
    Serial.print("A,");
    Serial.println(totalCounts * 360L / 4096L);
  }
}

void setup() {
  pinMode(PIN_ENA, OUTPUT); pinMode(PIN_IN1, OUTPUT); pinMode(PIN_IN2, OUTPUT);
  pinMode(PIN_ENB, OUTPUT); pinMode(PIN_IN3, OUTPUT); pinMode(PIN_IN4, OUTPUT);
#if defined(ESP32)
  // 기본 주파수는 귀에 들린다. 들리지 않는 높이로 올린다.
  analogWriteFrequency(PIN_ENA, 20000);
  analogWriteFrequency(PIN_ENB, 20000);
#endif
  applyCoils();
  setOutput(0);
  Serial.begin(115200);
  Wire.begin();
  lastCommandAt = millis();
}

void loop() {
  unsigned long now = millis();
  readSerial();

  if (now - lastSensorAt >= SENSOR_INTERVAL_MS) {
    lastSensorAt = now;
    updateSensor();
  }

  // 살아 있음 신호가 끊기면 무조건 끈다.
  if (state != SAFE && now - lastCommandAt > WATCHDOG_MS) enter(SAFE, 0);

  switch (state) {
    case PRE:
      if ((long)(now - stateUntil) >= 0) { enter(RELEASE, RELEASE_MS); break; }
      if (PRE_REELS && now - lastStepAt >= PRE_STEP_INTERVAL_MS) {
        // 약한 힘으로 한 칸씩 감는다. 줄이 팽팽해지면 힘이 모자라 저절로 멈춘다.
        lastStepAt = now;
        stepIndex = (stepIndex + (REEL_FORWARD ? 1 : 3)) & 3;
        applyCoils();
      }
      break;
    case RESIST:
      if ((long)(now - stateUntil) >= 0) { enter(RELEASE, RELEASE_MS); break; }
      // 팔을 거두는 중에는 풀어 준다. 펴거나 멈춰 있으면 붙든다.
      setOutput(RESIST_ONLY_WHEN_EXTENDING && motion < 0 ? 0 : resistStrength);
      break;
    case RELEASE:
      if ((long)(now - stateUntil) >= 0) enter(IDLE, 0);
      break;
    default:
      break;
  }

  if (now - lastStatusAt >= STATUS_INTERVAL_MS) {
    lastStatusAt = now;
    sendStatus();
  }
}
