# DEMO_V1 핫픽스 QA 보고서

## 대상

- Unity `6000.3.14f1`
- 씬: `TitleScene`, `Boot`, `TutorialScene`, `BossDevelopmentScene`, `Chapter01`
- 범위: 프레임 독립성, 점프 투사체 회피 훈련, F/G 적 지상 물리, F 전투 재시작, 테우스 동행

## 자동 검증 결과

- EditMode: `123/123` 통과
- PlayMode: `15/15` 통과
- Tutorial Scene Validator: 통과
- Scene Doctor: 신규 오류 0, 기존 레벨 저작용 비가시 충돌체 경고 54건
- 씬 스냅샷: 추가 1, 삭제 0, 수정 9
  - 추가: 외부 병력 연출용 카메라 밖 지원 발판
  - 수정: F/G 적 7기의 물리 구성, 점프 투사체 시작·종료 마커 2개

## 회귀 검증

- 점프 입력 3회만으로 `QST-TUTO-002`가 완료되지 않음
- 투사체가 끝 지점에 도달할 때만 `ProjectileAvoided`가 1회 증가함
- 피격 시 체크포인트 복귀와 누적 회피 0회 초기화
- 30/60/120FPS 시뮬레이션에서 이동 거리, 점프 궤적, 투사체 도달 시간이 동일함
- F/G 적 7기 모두 Dynamic Rigidbody2D와 고체 몸 Collider를 사용함
- 추적 적이 벽과 발판 끝을 통과하지 않음
- F 전투 중 사망·부활 시 첫 상태부터 재시작됨
- 숨겨진 방→회의장→복도에서 테우스가 사라지지 않고 동행함

## 빌드 QA 기준

- Windows 후보 빌드를 먼저 생성하고 압축 파일 구성과 SHA256을 검증한다.
- 동일 Git 커밋에서 macOS 빌드를 생성한다.
- 이 작업 환경은 macOS이므로 Windows 실행 파일의 실제 PC 조작 검증은 수행할 수 없다. Unity PlayMode 전체 흐름과 빌드 보고서·압축 무결성으로 후보를 검증하며, Windows 실기 시연 전에는 훈련장부터 헬테 직전까지 1회 확인을 권장한다.
