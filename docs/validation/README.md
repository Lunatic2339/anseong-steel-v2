# 선별 패키지 검증

2026-11-14 / Unity6000.3.26f1 / Test Framework1.6.0

EditMode 결과: **40 통과 / 0 실패 / 0 건너뜀**, 테스트 실행 프로세스 종료코드0. [결과 XML](editmode-results.xml), [Unity 로그](unity-tests.log).

| 테스트 묶음 | 통과 |
|---|---:|
| HeightFit | 11 |
| MaterialSlotNames | 16 |
| HandDeadzone | 4 |
| TrackingStateRules | 5 |
| CockpitClearZoneMath | 4 |

검증용 프로젝트: `C:/Users/anthony/Documents/Codex/2026-11-13/new-chat/work/foundation-validation`. 기존 원본 프로젝트를 열거나 수정하지 않았습니다. 첫 샌드박스 실행은 라이선스/IPC 초기화 오류로 실패했고, 정상 실행 환경에서 위 테스트를 통과했습니다.

## 재현

1. Unity6000.3.26f1에서 별도의 빈 프로젝트를 생성합니다.
2. 해당 프로젝트의 Packages/manifest.json에 `com.unity.test-framework` 버전 `1.6.0`, `com.unity.modules.xr` 버전 `1.0.0`, `com.anseong.foundation`을 로컬 `file:` 경로로 추가합니다. 실제 사용한 [manifest](validation-manifest.json)를 참고하되 컴퓨터가 바뀌면 경로를 바꿉니다.
3. manifest 최상위 `testables`에 `com.anseong.foundation`을 추가합니다. 기존 Anseong.Runtime는 가져오지 않습니다.
4. Test Runner의 EditMode에서 실행합니다. 자동 실행은 아래 인수를 사용하며 테스트 실행에는 `-quit`을 추가하지 않습니다.

```text
Unity.exe -batchmode -nographics -projectPath <별도검증프로젝트> -runTests -testPlatform EditMode -testResults <결과.xml> -logFile <로그.txt>
```

현재 테스트는 기존의 계산/추적/보정 규칙 회귀 검사입니다. VR 하드웨어·멀미·보스·PC 지원 역할·원격 네트워크·전체 게임 빌드의 검증 결과가 아닙니다.
