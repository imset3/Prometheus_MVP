# DEMO_V5 릴리스 노트

`DEMO_V5`는 V4의 UI·레벨·진행 개선을 유지하면서 프로메와 헬테의 PNG 애니메이션 표시 오류를 바로잡은 Windows·macOS 배포본입니다.

## 배포 파일

- `Prometheus_MVP_DEMO_V5_Windows.zip`
- `Prometheus_MVP_DEMO_V5_macOS.zip`
- `Prometheus_MVP_DEMO_V5_SHA256.txt`

## V5 변경 사항

- 프로메 Idle 120프레임·Run 60프레임 Sprite 참조 복구
- Dash·Jump·Attack01 포함 프로메 애니메이터의 표시 무결성 재검증
- 프로메의 보이는 발끝을 Collider 바닥과 일치시켜 플랫폼 위 부유 현상 수정
- 헬테 19개 패턴의 불투명 캐릭터 실루엣 높이를 동일 기준으로 정규화
- 베기 패턴에서 헬테 크기가 갑자기 작아지는 현상 완화
- 중복 캐릭터와 잘린 외곽 실루엣이 있던 SwordVolley 소스 교정
- 튜토리얼 씬과 보스 개발 씬에 동일한 헬테 V2 Animator 적용

## 검증

- EditMode 전체 `123/123` 통과
- PlayMode 전체 `16/16` 통과
- 프로메 Idle·Run·Jump·Dash·Attack01의 Sprite 참조 누락 `0`
- 헬테 Animator 19개 상태 Motion 누락 `0`
- Scene Doctor 신규 문제 `0`
- 보스 개발 씬에서 프로메 플랫폼 접지 캡처 확인

## 실행

압축을 완전히 해제한 뒤 Windows는 `Prometheus_MVP.exe`, macOS는 `Prometheus_MVP.app`을 실행합니다. 기본 해상도는 `1920×1080`이며 설정에서 실제 디스플레이 지원 해상도와 화면 모드를 변경할 수 있습니다.
