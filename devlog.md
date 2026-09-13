# Devlog

## 2026-09-13

### 서버

- 기존 SVN 관리 Unity 프로젝트(DotSoccer)를 `client/`로 옮기고, `server/`를 새로
  만들어 저장소를 클라이언트/서버 구조로 재편했다. 버전 관리는 이 작업 범위에
  git으로 새로 시작한다.
- 커스텀 바이너리 프로토콜(12바이트 헤더: length/sequence/type/reserved +
  페이로드)을 설계하고, C++17 + Linux epoll 기반 stage 1 echo 서버 코드를
  작성했다. 논블로킹 소켓, TCP 스트림 상의 부분 read/write 버퍼링, 프레임
  파싱, 프로토콜 에러 시 연결 종료까지 반영했다.
- 검증용으로 파이썬 테스트 클라이언트(`server/tools/echo_test_client.py`)도
  작성했다.
- 아직 실제로 빌드/실행은 못 해봤다 -- 현재 작업 머신에 C++ 툴체인과 WSL 배포판이
  없어서, 다음 단계로 WSL/Linux 환경에서 빌드하고 echo 왕복이 실제로 되는지
  확인해야 한다.

### 클라이언트

- Unity 에디터 제어용 MCP 서버로 `mcp-unity`(CoderGamester, ⭐1.9k, 커뮤니티
  프로젝트)를 붙였다. 처음엔 프로젝트에 남아있던 6월 빌드 스탠드얼론 서버와
  패키지 안에 새로 들어온 서버 버전이 안 맞아 연결이 안 됐는데, `.mcp.json`이
  패키지에 번들된 서버(`Library/PackageCache/.../Server~`)를 직접 가리키도록
  고치고 나서 정상 연결됐다.
- Unity 쪽 MCP 서버 옵션은 이거 말고도 여러 개 있다 -- 특히 2026-09-10에 Unity가
  Claude Code용 공식 1st-party 플러그인(`unity-agent-plugin`)을 냈다(Unity CLI +
  29개 스킬 + 자체 Editor MCP 브리지 번들). 다만 관련 패키지가 전부 `-pre`
  프리뷰 태그이고 나온 지 며칠 안 돼 실전 검증이 부족하다. 이 프로젝트는 당분간
  이미 검증되고 활발히 유지보수되는 `mcp-unity`를 계속 쓰기로 했다. 두 개를
  동시에 켜면 같은 Unity 에디터에 서로 다른 브리지가 붙으려 해서 충돌 위험이
  있으므로 병행 사용은 하지 않는다. 공식 플러그인이 프리뷰 딱지를 떼고
  커뮤니티 피드백이 쌓이면 다시 검토한다.
- mcp-unity를 통해 `DotSoccer` 프로토타입 씬(`GameManager`)에 `GridView`,
  `MatchController` 컴포넌트를 붙이고 Play 모드로 실제 동작을 확인했다. 그리드
  라인(30개), 골대 마커(10개), 두 플레이어와 공 스프라이트가 정상 스폰됐고
  콘솔 에러 없이 동작했다. 인스펙터에서 씬 오브젝트 참조를 직접 연결하는 게
  mcp-unity 툴로는 안 돼서, `MatchController.Start()`에 같은 GameObject의
  `GridView`를 자동으로 찾는 폴백을 추가해 우회했다.

### 인프라/자동화

- 저장소를 GitHub([muggie1379/DotSoccer](https://github.com/muggie1379/DotSoccer),
  Public)에 연결했다. GitHub CLI(`gh`)를 설치하고 로그인한 뒤 `gh repo create
  --push`로 저장소를 만들고 지금까지의 커밋을 올렸다.
- 작업하다 커밋을 깜빡하고 놓치는 걸 막기 위해, 변경사항이 있을 때만
  add/commit/push하는 스크립트(`tools/git-auto-commit.ps1`)를 만들고 Windows
  작업 스케줄러에 1시간 주기 작업(`DotSoccer-AutoCommit`)으로 등록했다. 첫
  등록 시도는 반복 기간을 `[TimeSpan]::MaxValue`로 줬다가
  "Duration:P99999999DT23H59M59S" 에러로 실패했다 -- 작업 스케줄러 XML이 받는
  기간 범위를 넘어선 값이었다. 10년(`New-TimeSpan -Days 3650`)으로 바꾸니
  정상 등록됐다.

### Velog 자동화

- devlog.md를 그대로 미러링하는 "History" 게시글을 Workflow 시리즈에 새로
  만들었다. 카테고리 구분 없이 시작했다가, 다루는 내용이 서버/클라이언트/
  인프라/velog 자동화 네 갈래로 늘어나면서 날짜 아래 `###` 카테고리 소제목으로
  나누는 방식으로 바꿨다 (지금 devlog.md도 그 구조를 그대로 따른다).
- "Skill" 소개 게시글(Workflow 시리즈)에 최신 SKILL.md 전문을 반영하고, 버전
  이력을 velog 게시글 수정만으로 남기기 어려워서 Skill 파일 자체를 GitHub
  저장소로도 버전 관리하기 시작했다.
- Skill 이름을 `velog-weekly-devlog`에서 `velog-for-claude`로 바꿨다 -- 더 이상
  "매주"도 아니고 devlog 발행 외에 History/Skill 소개 글 관리까지 범위가
  넓어져서 이름이 실제 역할을 설명하지 못하게 됐다. 폴더명, SKILL.md
  frontmatter, GitHub 저장소명까지는 바꿨는데, Claude 앱이 내부적으로 갖고
  있는 스킬 등록 정보(manifest.json)는 손으로 고쳐도 앱이 자체적으로
  재동기화하면서 옛 이름으로 되돌려버리는 문제를 발견했다 -- 폴더/파일 수정과
  앱의 실제 스킬 등록이 별개의 소스로 관리되는 것으로 보이며, 아직 해결 방법을
  찾는 중이다.
- velog는 마크다운에 넣은 `<details><summary>...</summary></details>` 접이식
  토글을 에디터 미리보기에서는 그럴듯하게 보여주지만, 실제 발행된 페이지는 그
  태그들을 렌더링 시 제거해버린다는 것을 발견했다 (안의 텍스트만 평범한
  문단으로 남음). 미리보기와 실제 발행 결과가 다르다는 게 함정 -- 반드시
  발행 후 실제 글 URL에서 확인해야 한다. 대안으로 `####` 소제목을 쓰기로 했다.
- velog 에디터의 이미지 업로드 툴바 버튼은 자동화가 다룰 수 없는 네이티브
  파일 선택 대화상자를 띄운다는 것도 확인했다. 대신 PowerShell로 로컬 이미지를
  클립보드에 올리고 에디터에 Ctrl+V로 붙여넣으면 velog가 알아서 업로드하고
  마크다운 이미지 링크를 삽입해준다는 걸 확인해서, 이 방식으로 전환했다.
- "블로그 개요"(Velog 시리즈) 게시글에 velog를 플랫폼으로 선택한 이유(마크다운을
  그대로 쓰는 블로그라 AI 자동 발행 파이프라인을 만들기 쉬움)를 추가했다.

<!-- published 2026-09-13 to velog DevLog #1 -->
