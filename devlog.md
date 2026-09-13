# Devlog

## 2026-09-13

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
