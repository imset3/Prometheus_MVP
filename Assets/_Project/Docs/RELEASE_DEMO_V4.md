# DEMO_V4 릴리스 노트

`DEMO_V4`는 친구 피드백을 기준으로 UI·캐릭터 비율, 프로메 애니메이션, 패배 복구와 튜토리얼 맵 표현을 통합 개선한 Windows·macOS 배포본입니다.

## 배포 파일

- `Prometheus_MVP_DEMO_V4_Windows.zip`
- `Prometheus_MVP_DEMO_V4_macOS.zip`
- `Prometheus_MVP_DEMO_V4_SHA256.txt`

## V4 변경 사항

- 프로메가 1080p 화면 높이의 약 11~13%를 차지하도록 시각 비율 조정
- NPC·헬테와 공격/피격 VFX 비율 및 발 위치 재정렬
- Idle·Run·Jump·Dash·Attack01 PNG 프레임의 발 Pivot 정규화
- 기본 공격을 0.35초 모션과 0.16초 타격 이벤트에 연결
- 훈련장·F·G·헬테전의 패배 후 체크포인트 복구 보강
- 비활성 전투 컨트롤러가 Coroutine을 잘못 시작하던 진행 중단 오류 수정
- 8개 튜토리얼 구역에 비충돌 시각 타일맵 V2 적용
- 회의장 NPC를 의자 앞쪽에 배치하고 복도 중앙 상자를 통과 가능한 장식으로 변경
- 타이틀·설정·일시정지 UI 정렬과 전체 음량 슬라이더 가독성 개선
- 깨진 타이틀 프로메 및 대화창 프로메 화상 참조 복구
- 타이틀 보스전 버튼과 보스 개발 씬의 릴리즈 경로 제외 유지

## 검증

- EditMode 전체 `123/123` 통과
- PlayMode 전체 `16/16` 통과
- 프로메 모션, UI, 캐릭터 비율, 연결형 타일맵 V2 Validator 오류 `0`
- Scene Doctor 오류 등급 `0`, 전체 경고 `54 → 52`
- 훈련장부터 헬테 완료까지 실제 씬 시스템 흐름 테스트 통과
- 필수 Build Settings 씬 4개 포함

## 실행

압축을 완전히 해제한 뒤 Windows는 `Prometheus_MVP.exe`, macOS는 `Prometheus_MVP.app`을 실행합니다. 기본 해상도는 `1920×1080`이며 설정에서 실제 디스플레이 지원 해상도와 화면 모드를 변경할 수 있습니다.
