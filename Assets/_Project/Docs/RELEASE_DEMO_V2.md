# DEMO_V2 릴리스 노트

`DEMO_V2`는 `DEMO_V1`의 프레임 독립 훈련·지상 적 물리 핫픽스를 포함하고, F/G 원거리 적의 배치와 시각적 접지를 추가 교정한 Windows·macOS 배포본입니다.

## 배포 파일

- `Prometheus_MVP_DEMO_V2_Windows.zip`
- `Prometheus_MVP_DEMO_V2_macOS.zip`
- `Prometheus_MVP_DEMO_V2_SHA256.txt`

## V2 변경 사항

- `F01_EnemySpawn_03` 로컬 X를 `80`으로 이동
- 세 번째 F 원거리 적이 이동된 마커 위치에서 생성
- F/G 원거리 적 3기의 몸 Collider를 바닥 피벗 스프라이트에 맞춰 교정
- 중력 안착 시 원거리 적의 발과 플랫폼 바닥면 일치
- 적 물리 자동 적용 도구와 회귀 검사에 원거리 적 접지 규칙 추가

## 검증

- EditMode 전체 `123/123` 통과
- PlayMode 전체 `15/15` 통과
- 원거리 적 전용 PlayMode `1/1` 통과
- Tutorial Scene Validator 통과
- Scene Doctor 신규 오류 0
- 필수 Build Settings 씬 5개 포함

## 실행

압축을 완전히 해제한 뒤 Windows는 `Prometheus_MVP.exe`, macOS는 `Prometheus_MVP.app`을 실행합니다. 기본 해상도는 `1920×1080`이며 설정에서 실제 디스플레이 지원 해상도와 화면 모드를 변경할 수 있습니다.
