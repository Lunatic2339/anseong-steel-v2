# GitHub 작업 규칙 (팀원용)

레포: https://github.com/Lunatic2339/anseong-steel-v2
초대 메일(또는 GitHub 알림)에서 **Accept invitation**을 눌러야 푸시할 수 있습니다.

## 핵심 3줄

1. **내 브랜치에서만 작업한다.** `main`에는 직접 커밋·푸시하지 않는다.
2. **내 브랜치에 커밋·푸시까지만 한다.** 직접 머지(Merge)하지 않는다.
3. **main에 넣고 싶으면 PR을 연다.** 병합은 코드 담당(선우)이 리뷰 후에 한다.

## 브랜치 이름

`feat/<내 이름>-<하는 일>` 영어 소문자, 띄어쓰기 대신 `-`. 자기 지시서에 브랜치 이름이 적혀 있으면 그것을 씁니다.

| 사람 | 예시 |
|---|---|
| 유진 | `feat/yujin-boss-anim` |
| 성재 | `feat/sungjae-material-slots` |
| 윤주 | `feat/yunju-environment` |
| 고건 | `feat/gogeon-exo-firmware` |

지시서 하나 = 브랜치 하나. 다른 일을 시작하면 새 브랜치를 만든다.

## 작업 순서 (Claude Code 데스크톱 앱 Code 탭에 이렇게 말하면 됩니다)

1. 시작: "main 최신으로 받고 `feat/yujin-boss-anim` 브랜치 만들어줘."
2. 중간중간 저장: "지금까지 커밋하고 내 브랜치에 푸시해줘."
3. 다 됐을 때: "main으로 PR 만들고 리뷰어에 Lunatic2339 넣어줘."
4. PR 링크를 **팀 단톡방**에 올린다.
5. 수정 요청이 오면 같은 브랜치에 고쳐서 다시 푸시하면 PR에 자동으로 붙는다. 새 PR을 만들 필요 없다.

GitHub Desktop으로 직접 할 때:
1. 위쪽 **Fetch origin** → **Pull origin**으로 최신 받기.
2. Current branch → **New branch** → 이름 `feat/<이름>-<하는 일>` (Create branch based on `main`).
3. 지시서에 적힌 폴더에 파일을 넣기(Unity가 만든 `.meta` 파일도 같이).
4. 왼쪽 아래에 한 줄 설명 → **Commit to feat/...** → **Push origin**.
5. **Create Pull Request** → 브라우저에서 아래 "GitHub 웹에서" 순서대로.

GitHub 웹에서 직접 할 때: 레포 페이지 → **Compare & pull request** → base `main`, compare `내 브랜치` → 오른쪽 **Reviewers**에 `Lunatic2339` → **Create pull request**. 초록색 **Merge** 버튼은 누르지 않는다.

## Unity 충돌 주의

- `Scenes/Main.unity`는 코드 담당(선우)만 수정한다. 나는 `Scenes/Sandbox/Sandbox_<이름>.unity`에서 작업하고 결과는 **프리팹**으로 넘긴다.
- 씬·프리팹은 두 사람이 동시에 고치면 합칠 수 없다. 남이 쓰는 프리팹을 고쳐야 하면 먼저 팀 단톡방에 말한다.
- 새 파일과 함께 생긴 `.meta` 파일은 반드시 같이 커밋한다.
- 파일 하나 50MB 이하. 빌드 결과·백업 폴더·캡처 영상은 올리지 않는다.
- 공개 레포라 비밀번호·토큰·Wi-Fi 정보는 절대 넣지 않는다.

막히면 팀 단톡방에 화면 캡처와 함께 물어보세요.
