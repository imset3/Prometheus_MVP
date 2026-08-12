# DEMO_V3 릴리스 노트

`DEMO_V3`는 `DEMO_V2`의 모든 수정 사항을 포함하며, 훈련장 점프 회피 투사체의 발사 위치와 속도를 교정한 Windows·macOS 배포본입니다.

## 배포 파일

- `Prometheus_MVP_DEMO_V3_Windows.zip`
- `Prometheus_MVP_DEMO_V3_macOS.zip`
- `Prometheus_MVP_DEMO_V3_SHA256.txt`

## V3 변경 사항

- 점프 훈련 투사체가 오른쪽 대포의 실제 발사구에서 출발
- Editor와 Player 빌드가 같은 발사 앵커 사용
- 투사체 이동 시간 `3.8초 → 2.8초`로 조정
- 반복 노출되던 크리온 캐릭터 설명 카드 비활성화
- 타이틀·일시정지 설정의 BGM/SFX 개별 항목 제거 및 전체 음량 하나로 통합
- 플레이어 HUD를 체력 게이지와 중앙 `현재 / 최대` 숫자 표기로 개선
- 고정 물리 스텝과 Collider Cast 충돌 판정 유지
- 한 번에 한 발만 통과하고 피격 시 회피 진행도 초기화하는 기존 규칙 유지

## 검증

- EditMode 전체 `123/123` 통과
- PlayMode 전체 `15/15` 통과
- Tutorial Scene Validator 통과
- Scene Doctor 신규 오류 0
- 필수 Build Settings 씬 5개 포함

## 실행

압축을 완전히 해제한 뒤 Windows는 `Prometheus_MVP.exe`, macOS는 `Prometheus_MVP.app`을 실행합니다. 기본 해상도는 `1920×1080`이며 설정에서 실제 디스플레이 지원 해상도와 화면 모드를 변경할 수 있습니다.
