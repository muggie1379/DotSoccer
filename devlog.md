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
